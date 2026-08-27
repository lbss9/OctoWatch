#!/usr/bin/env bash
# Gera os bindings do núcleo para cada UI (equivalente ao gen-bindings.ps1).
#   - Swift (macOS): uniffi-bindgen embutido.
#   - C# (Windows): uniffi-bindgen-cs (rodar no Windows; ver install no .ps1).
#   - Linux liga a crate direto; não precisa de bindings.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
core="$root/core"

echo "==> Compilando o núcleo (release)..."
( cd "$core" && cargo build --release )

# .dylib no macOS, .so no Linux
lib="$core/target/release/liboctowatch_core.dylib"
[ -f "$lib" ] || lib="$core/target/release/liboctowatch_core.so"

echo "==> Bindings Swift (macOS)..."
( cd "$core" && cargo run --bin uniffi-bindgen -- generate --library "$lib" \
    --language swift --out-dir "$root/macos/Sources/OctoWatch/Generated" )

echo "Pronto."
