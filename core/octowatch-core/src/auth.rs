use crate::error::OctoError;
use crate::models::{DeviceCode, DeviceLoginStatus};
use crate::runtime::RT;
use once_cell::sync::Lazy;
use serde::Deserialize;

const CLIENT_ID: &str = "Ov23liLv2MuCd5fzdkMJ";
const DEVICE_CODE_URL: &str = "https://github.com/login/device/code";
const ACCESS_TOKEN_URL: &str = "https://github.com/login/oauth/access_token";
const USER_AGENT: &str = "OctoWatch/0.1";

static HTTP: Lazy<reqwest::Client> = Lazy::new(|| {
    let _guard = RT.enter();
    reqwest::Client::builder()
        .user_agent(USER_AGENT)
        .build()
        .expect("failed to create OctoWatch's HTTP client")
});

#[uniffi::export]
pub fn start_device_login(scopes: String) -> Result<DeviceCode, OctoError> {
    let body = form(&[("client_id", CLIENT_ID), ("scope", scopes.trim())])?;
    let text = post_form(DEVICE_CODE_URL, &body)?;
    parse_device_code_response(&text)
}

#[uniffi::export]
pub fn poll_device_login(device_code: String) -> Result<DeviceLoginStatus, OctoError> {
    let body = form(&[
        ("client_id", CLIENT_ID),
        ("device_code", device_code.trim()),
        ("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
    ])?;
    let text = post_form(ACCESS_TOKEN_URL, &body)?;
    parse_access_token_response(&text)
}

fn form(pairs: &[(&str, &str)]) -> Result<String, OctoError> {
    serde_urlencoded::to_string(pairs).map_err(|e| OctoError::Api { msg: e.to_string() })
}

fn post_form(url: &str, body: &str) -> Result<String, OctoError> {
    let url = url.to_string();
    let body = body.to_string();
    RT.block_on(async move {
        let response = HTTP
            .post(&url)
            .header("Accept", "application/json")
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(body)
            .send()
            .await
            .map_err(|e| OctoError::Api { msg: e.to_string() })?;
        let status = response.status();
        let text = response
            .text()
            .await
            .map_err(|e| OctoError::Api { msg: e.to_string() })?;
        if !status.is_success() && !looks_like_oauth_json(&text) {
            return Err(OctoError::Api {
                msg: format!("{status}: {text}"),
            });
        }
        Ok(text)
    })
}

fn looks_like_oauth_json(text: &str) -> bool {
    let trimmed = text.trim_start();
    trimmed.starts_with('{') && (trimmed.contains("error") || trimmed.contains("access_token"))
}

#[derive(Deserialize)]
struct DeviceCodeDto {
    #[serde(default)]
    user_code: String,
    #[serde(default)]
    verification_uri: String,
    #[serde(default)]
    device_code: String,
    #[serde(default = "default_interval")]
    interval: u32,
    #[serde(default)]
    expires_in: u32,
    #[serde(default)]
    error: Option<String>,
    #[serde(default)]
    error_description: Option<String>,
}

fn default_interval() -> u32 {
    5
}

pub(crate) fn parse_device_code_response(body: &str) -> Result<DeviceCode, OctoError> {
    let dto: DeviceCodeDto =
        serde_json::from_str(body).map_err(|e| OctoError::Api { msg: e.to_string() })?;
    if let Some(error) = dto.error.filter(|e| !e.is_empty()) {
        return Err(OctoError::Auth {
            msg: dto.error_description.unwrap_or(error),
        });
    }
    if dto.user_code.is_empty() || dto.device_code.is_empty() {
        return Err(OctoError::Api {
            msg: "incomplete device code response".into(),
        });
    }
    Ok(DeviceCode {
        user_code: dto.user_code,
        verification_uri: dto.verification_uri,
        device_code: dto.device_code,
        interval: dto.interval.max(1),
        expires_in: dto.expires_in,
    })
}

#[derive(Deserialize)]
struct AccessTokenDto {
    #[serde(default)]
    access_token: Option<String>,
    #[serde(default)]
    error: Option<String>,
    #[serde(default)]
    error_description: Option<String>,
}

pub(crate) fn parse_access_token_response(body: &str) -> Result<DeviceLoginStatus, OctoError> {
    let dto: AccessTokenDto =
        serde_json::from_str(body).map_err(|e| OctoError::Api { msg: e.to_string() })?;
    if let Some(token) = dto.access_token.filter(|t| !t.is_empty()) {
        return Ok(DeviceLoginStatus::Authorized { token });
    }
    match dto.error.as_deref() {
        Some("authorization_pending") => Ok(DeviceLoginStatus::Pending),
        Some("slow_down") => Ok(DeviceLoginStatus::SlowDown),
        Some("expired_token") => Ok(DeviceLoginStatus::Expired),
        Some("access_denied") => Ok(DeviceLoginStatus::Denied),
        Some(other) => Err(OctoError::Auth {
            msg: dto.error_description.unwrap_or_else(|| other.to_string()),
        }),
        None => Err(OctoError::Api {
            msg: "OAuth response had neither token nor error".into(),
        }),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_device_code_ok() {
        let json = r#"{
            "device_code":"abc",
            "user_code":"WDJB-MJHT",
            "verification_uri":"https://github.com/login/device",
            "expires_in":900,
            "interval":5
        }"#;
        let code = parse_device_code_response(json).unwrap();
        assert_eq!(code.user_code, "WDJB-MJHT");
        assert_eq!(code.device_code, "abc");
        assert_eq!(code.interval, 5);
        assert_eq!(code.expires_in, 900);
        assert!(code.verification_uri.contains("github.com"));
    }

    #[test]
    fn parse_device_code_error() {
        let json = r#"{"error":"incorrect_client_credentials","error_description":"bad id"}"#;
        match parse_device_code_response(json) {
            Err(OctoError::Auth { msg }) => assert_eq!(msg, "bad id"),
            other => panic!("unexpected: {other:?}"),
        }
    }

    #[test]
    fn parse_token_authorized() {
        let json = r#"{"access_token":"gho_secret","token_type":"bearer","scope":"repo"}"#;
        match parse_access_token_response(json).unwrap() {
            DeviceLoginStatus::Authorized { token } => assert_eq!(token, "gho_secret"),
            other => panic!("unexpected: {other:?}"),
        }
    }

    #[test]
    fn parse_token_pending_and_slow_down() {
        let pending = parse_access_token_response(r#"{"error":"authorization_pending"}"#).unwrap();
        assert!(matches!(pending, DeviceLoginStatus::Pending));
        let slow = parse_access_token_response(r#"{"error":"slow_down"}"#).unwrap();
        assert!(matches!(slow, DeviceLoginStatus::SlowDown));
    }

    #[test]
    fn parse_token_expired_and_denied() {
        let expired = parse_access_token_response(r#"{"error":"expired_token"}"#).unwrap();
        assert!(matches!(expired, DeviceLoginStatus::Expired));
        let denied = parse_access_token_response(r#"{"error":"access_denied"}"#).unwrap();
        assert!(matches!(denied, DeviceLoginStatus::Denied));
    }
}
