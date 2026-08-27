# Changelog

All notable changes to OctoWatch are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-08-27

### Added

- Windows app: GitHub Actions feed with live status dots, glass/acrylic backdrop,
  tray with open/quit, GitHub device-flow sign-in, per-repo event selection.
- Shared Rust core (GitHub client) exposed to the UI via UniFFI.
- Filter flyout for Actions / PRs / Branches, native Windows toasts on new feed items,
  and Velopack updates from GitHub Releases.

### Changed

- Adjustable window transparency (Windows Terminal-style opacity + acrylic toggle).

### Security

- External links are restricted to http/https.
