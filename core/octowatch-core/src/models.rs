#[derive(Debug, Clone, uniffi::Record)]
pub struct Repo {
    pub owner: String,
    pub name: String,
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct WorkflowRun {
    pub id: i64,
    pub name: String,
    pub status: String,
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
    pub state: String,
    pub draft: bool,
    pub merged: bool,
    pub head_branch: String,
    pub base_branch: String,
    pub updated_at: String,
    pub html_url: String,
}

/// Rich detail for a single pull request, loaded on demand when a card expands.
#[derive(Debug, Clone, uniffi::Record)]
pub struct PullDetail {
    pub number: i64,
    pub title: String,
    pub body: String,
    pub author: String,
    /// open | closed
    pub state: String,
    pub draft: bool,
    pub merged: bool,
    /// None while GitHub is still computing mergeability.
    pub mergeable: Option<bool>,
    pub additions: i64,
    pub deletions: i64,
    pub changed_files: i64,
    pub comments: i64,
    pub commits: i64,
    pub head_branch: String,
    pub base_branch: String,
    pub labels: Vec<String>,
    pub requested_reviewers: Vec<String>,
    pub html_url: String,
    pub updated_at: String,
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

#[derive(Debug, Clone, uniffi::Record)]
pub struct DeviceCode {
    pub user_code: String,
    pub verification_uri: String,
    pub device_code: String,
    pub interval: u32,
    pub expires_in: u32,
}

#[derive(Debug, Clone, uniffi::Enum)]
pub enum DeviceLoginStatus {
    Pending,
    SlowDown,
    Expired,
    Denied,
    Authorized { token: String },
}
