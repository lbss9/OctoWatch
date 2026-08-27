//! Integration tests against the public GitHub API.
//!
//! They run anonymously by default (subject to GitHub's low anonymous rate
//! limit). Set GITHUB_TOKEN to authenticate and enable the token-only tests.

use octowatch_core::{start_device_login, Client, Repo};

fn client() -> std::sync::Arc<Client> {
    let token = std::env::var("GITHUB_TOKEN").unwrap_or_default();
    Client::new(token).expect("create client")
}

fn repo(owner: &str, name: &str) -> Repo {
    Repo {
        owner: owner.to_string(),
        name: name.to_string(),
    }
}

#[test]
fn lists_branches_and_commits_of_public_repo() {
    let c = client();
    let r = repo("octocat", "Hello-World");

    let branches = c.list_branches(r.clone()).expect("branches");
    assert!(!branches.is_empty(), "expected at least one branch");
    assert!(branches.iter().any(|b| b.name == "master"));

    let commits = c.list_commits(r, "master".to_string()).expect("commits");
    assert!(!commits.is_empty(), "expected at least one commit");
    assert_eq!(commits[0].sha.len(), 40);
}

#[test]
fn lists_pull_requests() {
    let c = client();
    let prs = c
        .list_pull_requests(repo("cli", "cli"))
        .expect("pull requests");
    assert!(!prs.is_empty(), "expected at least one PR");
    let pr = &prs[0];
    assert!(pr.number > 0);
    assert!(pr.html_url.starts_with("https://github.com/"));
}

#[test]
fn lists_workflow_runs() {
    let c = client();
    let runs = c
        .list_workflow_runs(repo("cli", "cli"))
        .expect("workflow runs");
    assert!(!runs.is_empty(), "expected at least one workflow run");
    assert!(runs.iter().all(|r| !r.status.is_empty()));
}

#[test]
fn whoami_when_authenticated() {
    if std::env::var("GITHUB_TOKEN")
        .unwrap_or_default()
        .trim()
        .is_empty()
    {
        eprintln!("skipping: GITHUB_TOKEN not set");
        return;
    }
    let login = client().whoami().expect("whoami");
    assert!(!login.is_empty());
}

#[test]
fn lists_repositories_when_authenticated() {
    if std::env::var("GITHUB_TOKEN")
        .unwrap_or_default()
        .trim()
        .is_empty()
    {
        eprintln!("skipping: GITHUB_TOKEN not set");
        return;
    }
    let repos = client().list_repositories().expect("repos");
    assert!(!repos.is_empty(), "expected at least one repository");
    assert!(repos.iter().all(|r| !r.owner.is_empty() && !r.name.is_empty()));
}

#[test]
fn start_device_login_returns_code() {
    let code = start_device_login("repo".into()).expect("device code");
    assert!(!code.user_code.is_empty());
    assert!(!code.device_code.is_empty());
    assert!(code.verification_uri.contains("github.com"));
    assert!(code.interval >= 1);
    assert!(code.expires_in > 0);
}
