#!/usr/bin/env bash
# Compila o núcleo Rust e roda os testes de integração.
# Defina GITHUB_TOKEN para habilitar o teste `whoami` e um rate limit maior.
set -euo pipefail
core="$(cd "$(dirname "$0")/.." && pwd)/core"
cd "$core"
cargo build --release
cargo test
