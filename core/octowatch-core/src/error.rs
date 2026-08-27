#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum OctoError {
    #[error("authentication failed: {msg}")]
    Auth { msg: String },

    #[error("not found: {msg}")]
    NotFound { msg: String },

    #[error("API/network error: {msg}")]
    Api { msg: String },
}

impl From<octocrab::Error> for OctoError {
    fn from(e: octocrab::Error) -> Self {
        if let octocrab::Error::GitHub { source, .. } = &e {
            let status = source.status_code.as_u16();
            let msg = source.message.clone();
            return match status {
                401 | 403 => OctoError::Auth { msg },
                404 => OctoError::NotFound { msg },
                _ => OctoError::Api {
                    msg: format!("{status}: {msg}"),
                },
            };
        }
        OctoError::Api { msg: e.to_string() }
    }
}
