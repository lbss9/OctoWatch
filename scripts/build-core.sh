#!/usr/bin/env bash
set -euo pipefail
core="$(cd "$(dirname "$0")/.." && pwd)/core"
cd "$core"
cargo build --release
cargo test
