//! OctoWatch — núcleo compartilhado.
//!
//! Toda a lógica de acesso ao GitHub (auth, chamadas à API e, futuramente, o
//! motor de polling) vive aqui e é exposta às três UIs nativas via UniFFI.

uniffi::setup_scaffolding!();

mod error;
mod github;
mod models;

pub use error::OctoError;
pub use github::Client;
pub use models::{Branch, Commit, PullRequest, Repo, WorkflowRun};
