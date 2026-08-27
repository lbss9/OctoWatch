//! Cliente do GitHub exposto às UIs como um objeto UniFFI.
//!
//! v1: métodos síncronos que bloqueiam em um runtime tokio compartilhado.
//! As UIs devem chamá-los fora da thread de interface (Task.Run no C#, uma
//! fila de background no Swift, um worker no GTK). Isso mantém a fronteira FFI
//! simples e idêntica entre as linguagens; async cross-FFI fica para depois.

use crate::error::OctoError;
use crate::models::{Branch, Commit, PullRequest, Repo, WorkflowRun};
use once_cell::sync::Lazy;
use serde::Deserialize;
use std::sync::Arc;
use tokio::runtime::Runtime;

static RT: Lazy<Runtime> = Lazy::new(|| {
    Runtime::new().expect("não foi possível iniciar o runtime tokio do OctoWatch")
});

#[derive(uniffi::Object)]
pub struct Client {
    inner: octocrab::Octocrab,
}

#[uniffi::export]
impl Client {
    /// Cria um cliente autenticado por Personal Access Token.
    /// Um token vazio cria um cliente anônimo (só dados públicos, rate limit baixo).
    #[uniffi::constructor]
    pub fn new(token: String) -> Result<Arc<Self>, OctoError> {
        let mut builder = octocrab::Octocrab::builder();
        if !token.trim().is_empty() {
            builder = builder.personal_token(token);
        }
        // Constrói dentro do contexto do runtime: o cliente do octocrab (tower/hyper)
        // spawna workers internos que exigem um reactor tokio ativo.
        let _guard = RT.enter();
        let inner = builder
            .build()
            .map_err(|e| OctoError::Auth { msg: e.to_string() })?;
        Ok(Arc::new(Self { inner }))
    }

    /// Valida o token retornando o login do usuário autenticado.
    pub fn whoami(&self) -> Result<String, OctoError> {
        let me: User = self.get("/user")?;
        Ok(me.login)
    }

    pub fn list_workflow_runs(&self, repo: Repo) -> Result<Vec<WorkflowRun>, OctoError> {
        let route = format!(
            "/repos/{}/{}/actions/runs?per_page=30",
            repo.owner, repo.name
        );
        let resp: WorkflowRunsResponse = self.get(&route)?;
        Ok(resp.workflow_runs.into_iter().map(Into::into).collect())
    }

    pub fn list_pull_requests(&self, repo: Repo) -> Result<Vec<PullRequest>, OctoError> {
        let route = format!(
            "/repos/{}/{}/pulls?state=all&sort=updated&direction=desc&per_page=30",
            repo.owner, repo.name
        );
        let dtos: Vec<PullDto> = self.get(&route)?;
        Ok(dtos.into_iter().map(Into::into).collect())
    }

    pub fn list_branches(&self, repo: Repo) -> Result<Vec<Branch>, OctoError> {
        let route = format!("/repos/{}/{}/branches?per_page=50", repo.owner, repo.name);
        let dtos: Vec<BranchDto> = self.get(&route)?;
        Ok(dtos.into_iter().map(Into::into).collect())
    }

    pub fn list_commits(&self, repo: Repo, branch: String) -> Result<Vec<Commit>, OctoError> {
        let route = format!(
            "/repos/{}/{}/commits?sha={}&per_page=30",
            repo.owner, repo.name, branch
        );
        let dtos: Vec<CommitDto> = self.get(&route)?;
        Ok(dtos.into_iter().map(Into::into).collect())
    }
}

impl Client {
    /// GET tipado num endpoint REST, bloqueando no runtime compartilhado.
    fn get<R: serde::de::DeserializeOwned>(&self, route: &str) -> Result<R, OctoError> {
        let client = self.inner.clone();
        let route = route.to_string();
        RT.block_on(async move {
            client
                .get::<R, _, ()>(&route, None::<&()>)
                .await
                .map_err(OctoError::from)
        })
    }
}

// ---------------------------------------------------------------------------
// DTOs internos: mapeiam o JSON do GitHub e são convertidos para os modelos
// públicos. Ficam privados para desacoplar a FFI da forma exata da API.
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct User {
    login: String,
}

#[derive(Deserialize)]
struct WorkflowRunsResponse {
    #[serde(default)]
    workflow_runs: Vec<WorkflowRunDto>,
}

#[derive(Deserialize)]
struct WorkflowRunDto {
    id: i64,
    #[serde(default)]
    name: Option<String>,
    #[serde(default)]
    head_branch: Option<String>,
    #[serde(default)]
    event: String,
    status: String,
    conclusion: Option<String>,
    updated_at: String,
    html_url: String,
    #[serde(default)]
    head_commit: Option<HeadCommit>,
}

#[derive(Deserialize)]
struct HeadCommit {
    #[serde(default)]
    message: String,
}

impl From<WorkflowRunDto> for WorkflowRun {
    fn from(d: WorkflowRunDto) -> Self {
        WorkflowRun {
            id: d.id,
            name: d.name.unwrap_or_default(),
            status: d.status,
            conclusion: d.conclusion,
            branch: d.head_branch.unwrap_or_default(),
            event: d.event,
            commit_message: d
                .head_commit
                .map(|c| first_line(&c.message))
                .unwrap_or_default(),
            updated_at: d.updated_at,
            html_url: d.html_url,
        }
    }
}

#[derive(Deserialize)]
struct PullDto {
    number: i64,
    title: String,
    user: Option<Login>,
    state: String,
    #[serde(default)]
    draft: bool,
    head: GitRef,
    base: GitRef,
    updated_at: String,
    html_url: String,
}

#[derive(Deserialize)]
struct Login {
    login: String,
}

#[derive(Deserialize)]
struct GitRef {
    #[serde(rename = "ref")]
    ref_name: String,
}

impl From<PullDto> for PullRequest {
    fn from(d: PullDto) -> Self {
        PullRequest {
            number: d.number,
            title: d.title,
            author: d.user.map(|u| u.login).unwrap_or_default(),
            state: d.state,
            draft: d.draft,
            head_branch: d.head.ref_name,
            base_branch: d.base.ref_name,
            updated_at: d.updated_at,
            html_url: d.html_url,
        }
    }
}

#[derive(Deserialize)]
struct BranchDto {
    name: String,
    commit: CommitRef,
    #[serde(default)]
    protected: bool,
}

#[derive(Deserialize)]
struct CommitRef {
    sha: String,
}

impl From<BranchDto> for Branch {
    fn from(d: BranchDto) -> Self {
        Branch {
            name: d.name,
            last_commit_sha: d.commit.sha,
            protected: d.protected,
        }
    }
}

#[derive(Deserialize)]
struct CommitDto {
    sha: String,
    commit: CommitDetail,
    author: Option<Login>,
    html_url: String,
}

#[derive(Deserialize)]
struct CommitDetail {
    #[serde(default)]
    message: String,
    author: Option<CommitAuthor>,
}

#[derive(Deserialize)]
struct CommitAuthor {
    #[serde(default)]
    name: String,
    #[serde(default)]
    date: String,
}

impl From<CommitDto> for Commit {
    fn from(d: CommitDto) -> Self {
        let (author_name, date) = d
            .commit
            .author
            .map(|a| (a.name, a.date))
            .unwrap_or_default();
        Commit {
            sha: d.sha,
            message: first_line(&d.commit.message),
            author: d.author.map(|u| u.login).filter(|s| !s.is_empty()).unwrap_or(author_name),
            date,
            html_url: d.html_url,
        }
    }
}

fn first_line(s: &str) -> String {
    s.lines().next().unwrap_or("").to_string()
}
