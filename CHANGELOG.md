# Changelog

All notable changes to OctoWatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-08-27

### Added
- Shared Rust core: a GitHub client (Actions, pull requests, branches, commits)
  exposed to the native UIs via UniFFI.
- GitHub sign-in via OAuth device flow, plus listing of the signed-in user's repositories.
- Windows app (WinUI 3): a bottom-right flyout with an acrylic glass backdrop, a system
  tray icon (open / quit), and a `NavigationView` shell.
- Home feed with an **All / Actions / PRs / Branches** filter and per-item status dots
  (green = success, red = failure, amber pulsing = running).
- Settings: account, repositories, monitored events, polling interval, language (pt-BR / en),
  theme, and start-with-Windows.
- Adjustable window transparency (Windows Terminal-style opacity slider + acrylic toggle).
- Localized in-app changelog (this page).

### Security
- External links are restricted to `http`/`https` schemes.
