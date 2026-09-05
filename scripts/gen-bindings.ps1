$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$core = Join-Path $root "core"
$dll  = Join-Path $core "target/release/octowatch_core.dll"

Push-Location $core
try {
    Write-Host "==> Compilando o núcleo (release)..." -ForegroundColor Cyan
    cargo build --release

    Write-Host "==> Bindings Swift (macOS)..." -ForegroundColor Cyan
    cargo run --bin uniffi-bindgen -- generate --library $dll `
        --language swift --out-dir (Join-Path $root "macos/Sources/OctoWatch/Generated")

    if (Get-Command uniffi-bindgen-cs -ErrorAction SilentlyContinue) {
        Write-Host "==> Bindings C# (Windows)..." -ForegroundColor Cyan
        uniffi-bindgen-cs --library $dll `
            --out-dir (Join-Path $root "windows/OctoWatch/Interop")
        Copy-Item $dll (Join-Path $root "windows/OctoWatch") -Force
    } else {
        Write-Warning "uniffi-bindgen-cs não encontrado — pulando C#. Instale com: cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.9.2+v0.28.3"
    }
}
finally {
    Pop-Location
}
Write-Host "Pronto." -ForegroundColor Green
