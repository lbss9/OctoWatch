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
