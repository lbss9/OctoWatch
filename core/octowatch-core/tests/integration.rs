//! Testes de integração contra a API pública do GitHub.
//!
//! Rodam anonimamente por padrão (sujeitos ao rate limit baixo do GitHub).
//! Defina GITHUB_TOKEN para autenticar e habilitar o teste `whoami`.

use octowatch_core::{Client, Repo};

fn client() -> std::sync::Arc<Client> {
    let token = std::env::var("GITHUB_TOKEN").unwrap_or_default();
    Client::new(token).expect("criar cliente")
}

fn repo(owner: &str, name: &str) -> Repo {
    Repo {
        owner: owner.to_string(),
        name: name.to_string(),
    }
}

#[test]
fn lista_branches_e_commits_de_repo_publico() {
    let c = client();
    let r = repo("octocat", "Hello-World");

    let branches = c.list_branches(r.clone()).expect("branches");
    assert!(!branches.is_empty(), "esperava ao menos uma branch");
    assert!(branches.iter().any(|b| b.name == "master"));

    let commits = c.list_commits(r, "master".to_string()).expect("commits");
    assert!(!commits.is_empty(), "esperava ao menos um commit");
    assert_eq!(commits[0].sha.len(), 40);
}

#[test]
fn lista_pull_requests() {
    let c = client();
    // Repo grande e ativo: sempre tem PRs (abertos ou fechados).
    let prs = c
        .list_pull_requests(repo("cli", "cli"))
        .expect("pull requests");
    assert!(!prs.is_empty(), "esperava ao menos um PR");
    let pr = &prs[0];
    assert!(pr.number > 0);
    assert!(pr.html_url.starts_with("https://github.com/"));
}

#[test]
fn lista_workflow_runs() {
    let c = client();
    let runs = c
        .list_workflow_runs(repo("cli", "cli"))
        .expect("workflow runs");
    assert!(!runs.is_empty(), "esperava ao menos um workflow run");
    assert!(runs.iter().all(|r| !r.status.is_empty()));
}

#[test]
fn whoami_quando_autenticado() {
    if std::env::var("GITHUB_TOKEN").unwrap_or_default().trim().is_empty() {
        eprintln!("pulando: GITHUB_TOKEN não definido");
        return;
    }
    let login = client().whoami().expect("whoami");
    assert!(!login.is_empty());
}
