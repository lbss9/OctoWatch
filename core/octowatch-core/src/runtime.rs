use once_cell::sync::Lazy;
use tokio::runtime::Runtime;

pub static RT: Lazy<Runtime> =
    Lazy::new(|| Runtime::new().expect("não foi possível iniciar o runtime tokio do OctoWatch"));
