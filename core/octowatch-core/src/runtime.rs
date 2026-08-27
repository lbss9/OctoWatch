use once_cell::sync::Lazy;
use tokio::runtime::Runtime;

/// Shared Tokio runtime. The FFI surface is synchronous, so each call blocks on
/// this runtime instead of exposing async across the language boundary.
pub static RT: Lazy<Runtime> =
    Lazy::new(|| Runtime::new().expect("failed to start OctoWatch's Tokio runtime"));
