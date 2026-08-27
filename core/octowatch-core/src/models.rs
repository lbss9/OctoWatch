//! Modelos de dados expostos às UIs nativas via UniFFI.
//!
//! Mantemos `status`/`conclusion`/`state` como `String` de propósito: a API do
//! GitHub evolui esses valores com frequência e strings evitam quebras de FFI.

#[derive(Debug, Clone, uniffi::Record)]
pub struct Repo {
    pub owner: String,
    pub name: String,
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct WorkflowRun {
    pub id: i64,
    pub name: String,
    /// queued | in_progress | completed
    pub status: String,
    /// success | failure | cancelled | ... (nulo enquanto não concluído)
    pub conclusion: Option<String>,
    pub branch: String,
    pub event: String,
    pub commit_message: String,
    pub updated_at: String,
    pub html_url: String,
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct PullRequest {
    pub number: i64,
    pub title: String,
    pub author: String,
    /// open | closed
    pub state: String,
    pub draft: bool,
    pub head_branch: String,
    pub base_branch: String,
    pub updated_at: String,
    pub html_url: String,
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct Branch {
    pub name: String,
    pub last_commit_sha: String,
    pub protected: bool,
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct Commit {
    pub sha: String,
    pub message: String,
    pub author: String,
    pub date: String,
    pub html_url: String,
}
