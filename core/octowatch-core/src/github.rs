use crate::error::OctoError;
use crate::models::{Branch, Commit, PullDetail, PullRequest, Repo, WorkflowRun};
use crate::runtime::{HTTP, RT};
use serde::Deserialize;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};

/// A cached GET response, keyed by URL, for conditional requests.
struct CacheEntry {
    etag: String,
    body: String,
}

#[derive(uniffi::Object)]
pub struct Client {
    inner: octocrab::Octocrab,
    token: String,
    // URL -> last ETag + body, so unchanged responses come back as 304 (which
    // GitHub does not count against the rate limit) and reuse the cached body.
    etags: Mutex<HashMap<String, CacheEntry>>,
}

#[uniffi::export]
impl Client {
    #[uniffi::constructor]
    pub fn new(token: String) -> Result<Arc<Self>, OctoError> {
        let mut builder = octocrab::Octocrab::builder();
        if !token.trim().is_empty() {
            builder = builder.personal_token(token.clone());
        }
        let _guard = RT.enter();
        let inner = builder
            .build()
            .map_err(|e| OctoError::Auth { msg: e.to_string() })?;
        Ok(Arc::new(Self {
            inner,
            token,
            etags: Mutex::new(HashMap::new()),
        }))
    }

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

    /// Full detail for one pull request (loaded on demand when a card expands).
    pub fn get_pull_request(&self, repo: Repo, number: i64) -> Result<PullDetail, OctoError> {
        let route = format!("/repos/{}/{}/pulls/{number}", repo.owner, repo.name);
        let dto: PullDetailDto = self.get(&route)?;
        Ok(dto.into())
    }

    pub fn list_repositories(&self) -> Result<Vec<Repo>, OctoError> {
        let mut all = Vec::new();
        for page in 1..=3u32 {
            let route = format!("/user/repos?per_page=100&sort=updated&page={page}");
            let dtos: Vec<RepoDto> = self.get(&route)?;
            let count = dtos.len();
            all.extend(dtos.into_iter().map(Into::into));
            if count < 100 {
                break;
            }
        }
        Ok(all)
    }

    pub fn rerun_workflow(&self, repo: Repo, run_id: i64) -> Result<(), OctoError> {
        self.post_empty(&format!(
            "/repos/{}/{}/actions/runs/{run_id}/rerun",
            repo.owner, repo.name
        ))
    }

    pub fn rerun_failed_jobs(&self, repo: Repo, run_id: i64) -> Result<(), OctoError> {
        self.post_empty(&format!(
            "/repos/{}/{}/actions/runs/{run_id}/rerun-failed-jobs",
            repo.owner, repo.name
        ))
    }

    pub fn cancel_workflow(&self, repo: Repo, run_id: i64) -> Result<(), OctoError> {
        self.post_empty(&format!(
            "/repos/{}/{}/actions/runs/{run_id}/cancel",
            repo.owner, repo.name
        ))
    }

    /// Submits a review on a pull request. `event` is APPROVE, REQUEST_CHANGES or
    /// COMMENT; `body` is required by GitHub for the latter two.
    pub fn submit_review(
        &self,
        repo: Repo,
        number: i64,
        event: String,
        body: String,
    ) -> Result<(), OctoError> {
        let mut payload = serde_json::json!({ "event": event });
        if !body.trim().is_empty() {
            payload["body"] = serde_json::Value::String(body);
        }
        let route = format!("/repos/{}/{}/pulls/{number}/reviews", repo.owner, repo.name);
        self.send_json(reqwest::Method::POST, &route, payload)
    }

    /// Merges a pull request. `method` is merge, squash or rebase (defaults to merge).
    pub fn merge_pull(&self, repo: Repo, number: i64, method: String) -> Result<(), OctoError> {
        let method = if method.trim().is_empty() {
            "merge".to_string()
        } else {
            method
        };
        let route = format!("/repos/{}/{}/pulls/{number}/merge", repo.owner, repo.name);
        self.send_json(
            reqwest::Method::PUT,
            &route,
            serde_json::json!({ "merge_method": method }),
        )
    }
}

impl Client {
    fn get<R: serde::de::DeserializeOwned>(&self, route: &str) -> Result<R, OctoError> {
        let body = self.get_raw(route)?;
        serde_json::from_str(&body).map_err(|e| OctoError::Api { msg: e.to_string() })
    }

    /// GET with a conditional (ETag) request. When GitHub answers 304 Not Modified
    /// the cached body is reused and no rate limit is spent.
    fn get_raw(&self, route: &str) -> Result<String, OctoError> {
        let url = format!("https://api.github.com{route}");
        let prev_etag = self.etags.lock().unwrap().get(&url).map(|e| e.etag.clone());

        let token = self.token.clone();
        let request_url = url.clone();
        let outcome = RT.block_on(async move {
            let mut req = HTTP
                .get(&request_url)
                .header("Accept", "application/vnd.github+json")
                .header("X-GitHub-Api-Version", "2022-11-28");
            if !token.trim().is_empty() {
                req = req.header("Authorization", format!("Bearer {token}"));
            }
            if let Some(etag) = prev_etag {
                req = req.header("If-None-Match", etag);
            }
            let response = req
                .send()
                .await
                .map_err(|e| OctoError::Api { msg: e.to_string() })?;
            let status = response.status();
            let etag = response
                .headers()
                .get(reqwest::header::ETAG)
                .and_then(|v| v.to_str().ok())
                .map(str::to_string);
            if status == reqwest::StatusCode::NOT_MODIFIED {
                return Ok(GetOutcome::NotModified);
            }
            let text = response
                .text()
                .await
                .map_err(|e| OctoError::Api { msg: e.to_string() })?;
            if !status.is_success() {
                return Err(status_error(status.as_u16(), text));
            }
            Ok(GetOutcome::Fresh { etag, body: text })
        })?;

        match outcome {
            GetOutcome::NotModified => Ok(self
                .etags
                .lock()
                .unwrap()
                .get(&url)
                .map(|e| e.body.clone())
                .unwrap_or_default()),
            GetOutcome::Fresh { etag, body } => {
                if let Some(etag) = etag {
                    self.etags.lock().unwrap().insert(
                        url,
                        CacheEntry {
                            etag,
                            body: body.clone(),
                        },
                    );
                }
                Ok(body)
            }
        }
    }

    /// Sends a JSON body to a route and maps a non-2xx status to a typed error.
    fn send_json(
        &self,
        method: reqwest::Method,
        route: &str,
        payload: serde_json::Value,
    ) -> Result<(), OctoError> {
        let url = format!("https://api.github.com{route}");
        let token = self.token.clone();
        RT.block_on(async move {
            let response = HTTP
                .request(method, &url)
                .header("Accept", "application/vnd.github+json")
                .header("X-GitHub-Api-Version", "2022-11-28")
                .header("Authorization", format!("Bearer {token}"))
                .json(&payload)
                .send()
                .await
                .map_err(|e| OctoError::Api { msg: e.to_string() })?;
            let status = response.status();
            if status.is_success() {
                return Ok(());
            }
            let text = response.text().await.unwrap_or_default();
            Err(status_error(status.as_u16(), text))
        })
    }

    fn post_empty(&self, route: &str) -> Result<(), OctoError> {
        let client = self.inner.clone();
        let route = route.to_string();
        RT.block_on(async move {
            let uri = http::Uri::builder()
                .path_and_query(&route)
                .build()
                .map_err(|e| OctoError::Api { msg: e.to_string() })?;
            let response = client
                ._post(uri, None::<&()>)
                .await
                .map_err(OctoError::from)?;
            let status = response.status();
            if status.is_success() {
                Ok(())
            } else {
                Err(OctoError::Api {
                    msg: format!("{status}"),
                })
            }
        })
    }
}

enum GetOutcome {
    NotModified,
    Fresh {
        etag: Option<String>,
        body: String,
    },
}

/// Turns a failed HTTP status + GitHub error body into a typed error, pulling out
/// the human-readable `message` field (e.g. "Bad credentials") when present.
fn status_error(status: u16, body: String) -> OctoError {
    let msg = serde_json::from_str::<serde_json::Value>(&body)
        .ok()
        .and_then(|v| {
            v.get("message")
                .and_then(|m| m.as_str())
                .map(str::to_string)
        })
        .unwrap_or(body);
    match status {
        401 | 403 => OctoError::Auth { msg },
        404 => OctoError::NotFound { msg },
        _ => OctoError::Api {
            msg: format!("{status}: {msg}"),
        },
    }
}

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
    #[serde(default)]
    merged_at: Option<String>,
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
            merged: d.merged_at.is_some(),
            head_branch: d.head.ref_name,
            base_branch: d.base.ref_name,
            updated_at: d.updated_at,
            html_url: d.html_url,
        }
    }
}

#[derive(Deserialize)]
struct PullDetailDto {
    number: i64,
    title: String,
    #[serde(default)]
    body: Option<String>,
    user: Option<Login>,
    state: String,
    #[serde(default)]
    draft: bool,
    #[serde(default)]
    merged: bool,
    #[serde(default)]
    mergeable: Option<bool>,
    #[serde(default)]
    additions: i64,
    #[serde(default)]
    deletions: i64,
    #[serde(default)]
    changed_files: i64,
    #[serde(default)]
    comments: i64,
    #[serde(default)]
    commits: i64,
    head: GitRef,
    base: GitRef,
    #[serde(default)]
    labels: Vec<LabelDto>,
    #[serde(default)]
    requested_reviewers: Vec<Login>,
    updated_at: String,
    html_url: String,
}

#[derive(Deserialize)]
struct LabelDto {
    name: String,
}

impl From<PullDetailDto> for PullDetail {
    fn from(d: PullDetailDto) -> Self {
        PullDetail {
            number: d.number,
            title: d.title,
            body: d.body.unwrap_or_default(),
            author: d.user.map(|u| u.login).unwrap_or_default(),
            state: d.state,
            draft: d.draft,
            merged: d.merged,
            mergeable: d.mergeable,
            additions: d.additions,
            deletions: d.deletions,
            changed_files: d.changed_files,
            comments: d.comments,
            commits: d.commits,
            head_branch: d.head.ref_name,
            base_branch: d.base.ref_name,
            labels: d.labels.into_iter().map(|l| l.name).collect(),
            requested_reviewers: d
                .requested_reviewers
                .into_iter()
                .map(|u| u.login)
                .collect(),
            html_url: d.html_url,
            updated_at: d.updated_at,
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
            author: d
                .author
                .map(|u| u.login)
                .filter(|s| !s.is_empty())
                .unwrap_or(author_name),
            date,
            html_url: d.html_url,
        }
    }
}

#[derive(Deserialize)]
struct RepoDto {
    name: String,
    owner: Login,
}

impl From<RepoDto> for Repo {
    fn from(d: RepoDto) -> Self {
        Repo {
            owner: d.owner.login,
            name: d.name,
        }
    }
}

fn first_line(s: &str) -> String {
    s.lines().next().unwrap_or("").to_string()
}
