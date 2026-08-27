# Compila o núcleo Rust e roda os testes de integração.
# Defina $env:GITHUB_TOKEN para habilitar o teste `whoami` e um rate limit maior.
$ErrorActionPreference = "Stop"
$core = Join-Path (Split-Path $PSScriptRoot -Parent) "core"
Push-Location $core
try {
    cargo build --release
    cargo test
}
finally {
    Pop-Location
}
