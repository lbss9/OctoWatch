uniffi::setup_scaffolding!();

mod auth;
mod error;
mod github;
mod models;
mod runtime;

pub use auth::{poll_device_login, start_device_login};
pub use error::OctoError;
pub use github::Client;
pub use models::{Branch, Commit, DeviceCode, DeviceLoginStatus, PullRequest, Repo, WorkflowRun};
