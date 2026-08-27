use once_cell::sync::Lazy;
use tokio::runtime::Runtime;

pub static RT: Lazy<Runtime> =
    Lazy::new(|| Runtime::new().expect("failed to start the OctoWatch tokio runtime"));
