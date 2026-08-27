#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
core="$root/core"

echo "==> Compilando o núcleo (release)..."
( cd "$core" && cargo build --release )

lib="$core/target/release/liboctowatch_core.dylib"
[ -f "$lib" ] || lib="$core/target/release/liboctowatch_core.so"

echo "==> Bindings Swift (macOS)..."
( cd "$core" && cargo run --bin uniffi-bindgen -- generate --library "$lib" \
    --language swift --out-dir "$root/macos/Sources/OctoWatch/Generated" )

echo "Pronto."
