use once_cell::sync::Lazy;
use tokio::runtime::Runtime;

/// Shared Tokio runtime. The FFI surface is synchronous, so each call blocks on
/// this runtime instead of exposing async across the language boundary.
pub static RT: Lazy<Runtime> =
    Lazy::new(|| Runtime::new().expect("failed to start OctoWatch's Tokio runtime"));

/// Shared HTTP client used for the GitHub REST calls (with ETag caching) and the
/// OAuth device flow.
pub static HTTP: Lazy<reqwest::Client> = Lazy::new(|| {
    let _guard = RT.enter();
    reqwest::Client::builder()
        .user_agent("OctoWatch/0.1")
        .build()
        .expect("failed to build the OctoWatch HTTP client")
});
