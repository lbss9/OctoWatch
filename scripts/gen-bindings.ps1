# Gera os bindings do núcleo para cada UI.
#   - Swift  (macOS): usa o uniffi-bindgen embutido (bin do crate).
#   - C#     (Windows): usa o uniffi-bindgen-cs (Nord Security), instalar antes:
#       cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.9.2+v0.28.3
#   - Linux liga a crate direto; não precisa de bindings.
#
# Uso:  pwsh scripts/gen-bindings.ps1
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
        Write-Warning "uniffi-bindgen-cs não encontrado — pulando C#. Veja o comando de install no topo deste script."
    }
}
finally {
    Pop-Location
}
Write-Host "Pronto." -ForegroundColor Green
