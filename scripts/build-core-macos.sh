#!/usr/bin/env bash
# Builds the Rust core as a universal (arm64 + x86_64) static library for the
# macOS app and regenerates the Swift bindings. Run on a Mac, from anywhere.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
core="$root/core"

rustup target add aarch64-apple-darwin x86_64-apple-darwin

( cd "$core" && cargo build --release --target aarch64-apple-darwin )
( cd "$core" && cargo build --release --target x86_64-apple-darwin )

mkdir -p "$root/macos/lib"
lipo -create \
  "$core/target/aarch64-apple-darwin/release/liboctowatch_core.a" \
  "$core/target/x86_64-apple-darwin/release/liboctowatch_core.a" \
  -output "$root/macos/lib/liboctowatch_core.a"

# Regenerate the Swift bindings from the built dylib.
dylib="$core/target/aarch64-apple-darwin/release/liboctowatch_core.dylib"
gen="$root/macos/Sources/OctoWatch/Generated"
( cd "$core" && cargo run --bin uniffi-bindgen -- generate --library "$dylib" --language swift --out-dir "$gen" )

# UniFFI emits the C header + modulemap next to the Swift file; the SwiftPM layout
# keeps the Swift here and the C header in the octowatch_coreFFI target.
mv -f "$gen/octowatch_coreFFI.h" "$root/macos/Sources/octowatch_coreFFI/include/octowatch_coreFFI.h"
rm -f "$gen/octowatch_coreFFI.modulemap"

echo "Done. Next: cd macos && swift build   (or open Package.swift in Xcode)."
