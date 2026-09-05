# OctoWatch — macOS

Native **SwiftUI** app that shares the same Rust core (`../core`) as the Windows
build, through the UniFFI-generated Swift bindings. Same functionality, but with
macOS's own idioms: the app lives in the **menu bar at the top of the screen**
(not a Windows-style tray at the bottom).

> This is a working **base** to clone and finish on a Mac. It was scaffolded on
> Windows, so it hasn't been compiled with Xcode yet — expect small tweaks
> (linker flags, signing) on the first build.

## How the menu bar works (the "tray")

macOS apps put their status item in the **top menu bar**. This app uses SwiftUI's
`MenuBarExtra` with `.menuBarExtraStyle(.window)`, so **clicking the menu bar icon
opens a popover window** with the feed — the direct counterpart of the Windows
tray flyout. The Dock icon is hidden (`NSApplication.setActivationPolicy(.accessory)`),
so it's a menu-bar-only app. Settings open with **⌘,** (the standard `Settings`
scene).

## Build

Prerequisites: macOS 14+, Xcode (or the Swift toolchain) and Rust.

```bash
# 1) Build the shared core as a universal static lib + regenerate Swift bindings.
./scripts/build-core-macos.sh        # from the repo root

# 2) Build/run the app.
cd macos
swift build          # or: open Package.swift in Xcode and Run
```

First launch: open **Settings (⌘,) → Sign in with GitHub** (OAuth device flow),
then set the owner/repo and polling interval.

## Layout

```
macos/
  Package.swift                       SwiftPM manifest (links liboctowatch_core.a)
  lib/liboctowatch_core.a             universal static lib (produced by the script)
  Sources/
    octowatch_coreFFI/                C header + modulemap for the bindings
    OctoWatch/
      Generated/octowatch_core.swift  UniFFI Swift bindings (generated)
      OctoWatchApp.swift              @main: MenuBarExtra (.window) + Settings scene
      FeedView.swift                  the menu bar popover (feed list)
      SettingsView.swift              device-flow sign-in, repo, polling
      FeedStore.swift                 polls the core, publishes the feed
      FeedItem.swift                  view model + status colors + relative time
      Session.swift                   token in the macOS Keychain
```

## Status vs. the Windows app

Shared already (in the Rust core, so both platforms get it for free):
- GitHub client, OAuth device flow, and **ETag conditional caching** (saves rate limit).
- `get_pull_request` for PR detail.

Implemented here (base): menu bar feed of Actions/PRs/branches with status colors
and relative time, device-flow sign-in (Keychain), manual repo + polling interval.

To reach parity with Windows (good next tasks):
- Expandable PR detail (the core `getPullRequest` is ready) and PR actions.
- Notifications (`UNUserNotificationCenter`) for new items.
- Localization (pt-BR/en), theme, adjustable transparency, changelog, auto-update.
- Custom template icon for the menu bar, repo selection list, per-event filters.
- Graceful auth handling: on an auth error, drop the token and prompt re-sign-in
  (mirror the Windows `CoreError` flow).
