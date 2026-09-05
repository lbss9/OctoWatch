# OctoWatch macOS — step-by-step execution guide

> Precise, do-exactly-this guide to finish the **macOS** app so it reaches parity with
> the Windows app. The shared logic already lives in the Rust core (`../core`) and is
> reached from Swift through the UniFFI bindings. Follow the tasks in order; each lists
> the files to touch, the exact approach, the core API to call, and how to verify.
> Match what's described here — don't invent a different architecture.

## Ground rules

- **Language of the code is English** — comments, logs, symbol names. User-facing UI
  strings are localized (see Task 7); everything else is English.
- **Comments**: short, punctual, human — explain the *why*, not the obvious.
- **Same functionality as Windows, macOS idioms.** The Windows app is the reference for
  *what* each feature does; the macOS UI uses native patterns (menu bar at the top, a
  `MenuBarExtra` popover instead of a tray flyout, Settings scene, Keychain, etc.).
- **Commits**: small and focused; end the message body with
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` if you keep that convention.

## Build & verify

Prerequisites on the Mac: **Xcode** (or the Swift toolchain) and **Rust** (`rustup`).

```bash
# From the repo root — builds the core as a universal static lib into macos/lib/
# AND regenerates the Swift bindings. Re-run this whenever the Rust core changes.
./scripts/build-core-macos.sh

cd macos
swift build          # or: open Package.swift in Xcode and Run
```

Run the app: `swift run` from `macos/`, or Run in Xcode. The app has **no Dock icon**
(menu-bar only, `NSApplication.setActivationPolicy(.accessory)` in `AppDelegate`); look
for its icon in the **top menu bar** and click it to open the popover.

Gotchas:
- If the linker can't find `-loctowatch_core`, the static lib isn't in `macos/lib/`
  (re-run `build-core-macos.sh`), or you're not running `swift build` from `macos/`.
- The core calls **block** (synchronous FFI). Never call them on the main actor — wrap
  in `Task.detached { ... }` and hop back (the scaffold already does this in `FeedStore`).
- `MenuBarExtra` content is created lazily on first click; for background polling from
  launch, start the store in the `AppDelegate` (see Task 12).

## The menu bar ("tray") pattern

macOS status items live in the **top menu bar**. The app uses SwiftUI
`MenuBarExtra("OctoWatch", systemImage:) { FeedView() }.menuBarExtraStyle(.window)` so a
**click opens a popover window** — the counterpart of the Windows tray flyout. There is
currently **no API to programmatically open/close** that popover, so the popover is the
click-to-open surface; use a separate `Settings` scene (⌘,) and a normal window if you
need something always-openable.

## The target: full parity with the Windows app

The macOS app must do **everything the Windows app does**, with macOS-native UI. The
Windows app is the **reference implementation and source of truth** — it's in the same
repo at `../windows/OctoWatch/`. **Read the Windows source for the exact behavior of each
feature and mirror it in Swift.** Complete inventory to build (nothing here is optional):

**Feed / Home**
- Feed of **GitHub Actions runs, pull requests, and branches** for the watched repos.
- Per-item **status**: green = success, red = failure, amber **pulsing** = running, gray = other.
- Card visuals: a **colored accent stripe** (by status), a **kind glyph**, title, subtitle
  (`owner/repo · branch`), and a **relative time** ("2m ago") that refreshes on a timer.
- **Filter**: All / Actions / PRs / Branches (toggle which kinds show).
- **Incremental refresh**: updating must not rebuild the whole list — reuse rows for
  unchanged items so scroll and the running animation are preserved. (In SwiftUI this comes
  from stable `Identifiable` ids + value equality; see Windows `FeedDiff` for the intent.)
- **Per-card action menu** (mirror Windows `Logic/CardActions.cs`): runs → open, re-run,
  re-run failed jobs, cancel; PRs → open, view files, view checks; branches → open, view
  commits. Actions that write require sign-in and confirm where destructive.
- **Empty / error** states and a **Clear** affordance.
- Source of repos: the **repositories selected in Settings**; a manual `owner/repo` field is
  only the fallback when none are selected.

**Pull request detail + actions** — Tasks 3 and 4 (expand to detail; approve / request
changes / comment / merge). Core is ready (`getPullRequest`, `submitReview`, `mergePull`).

**Menu bar item ("tray")**
- Menu bar popover (the feed) — the macOS flyout.
- A menu with **Open** and **Quit**, and clicking the item opens the popover.
- Custom **template icon** (Task 11).

**Settings** (mirror `Pages/SettingsPage.xaml` + `.cs`)
- **GitHub account**: OAuth **device flow** sign-in (show user code, open github.com/login/device,
  poll), signed-in-as, sign out. Token in the **Keychain**.
- **Repositories**: load via `listRepositories()`, search, checkbox selection.
- **Events** to monitor (global + per-repo): PR opened / merged / closed, review requested,
  mentioned, team mention, CI/Actions, push, assign (same set as Windows `MonitorEvents`).
- **Polling interval**: 30s, 1m, 2m, 5m, 10m, 15m, 30m, 1h (30s floor).
- **Language**: pt-BR / en (Task 7).  **Theme**: Light / Dark / System (Task 8).
- **Transparency**: enable-acrylic toggle + opacity — with **Apply / Restore default**
  (changes apply only on Apply, mirroring Windows) (Task 8).
- **Start at login** toggle (Task 12).  **Updates**: check-for-updates + update-on-launch (Task 10).
- **Quit app** button (fully exits, like the tray's Quit).

**About** (mirror `Pages/AboutPage.*`): app version, description, MIT license, a link.

**Changelog** (Task 9): localized in-app Markdown + the canonical `../CHANGELOG.md`.

**Notifications** (Task 5): native notifications for new, matching items.

**Security / robustness**: only open `http`/`https` links (Windows `SafeUrl`); token in the
Keychain; **graceful auth** — an expired/revoked token is dropped and re-sign-in is prompted
(Windows `CoreError` + `Auth_Expired`); clean error messages (extract the `msg` from `OctoError`).

**Shared, already done in the Rust core (free for macOS)**: OAuth device flow, **ETag
conditional caching** (saves rate limit), `listWorkflowRuns/PullRequests/Branches/Commits`,
`listRepositories`, `getPullRequest`, `submitReview`, `mergePull`, run rerun/cancel.

### Windows source map (read these to copy exact behavior)

| Windows file | What to mirror |
| --- | --- |
| `Pages/HomePage.xaml(.cs)` | feed list, filter, card template, per-card menu, expand |
| `Logic/FeedMapper.cs` | status mapping (run/pull/branch), identity, filter, clear |
| `Logic/CardActions.cs` | `FeedItem` shape + per-kind action catalog |
| `Logic/FeedDiff.cs` | incremental update intent (SwiftUI does this via Identifiable) |
| `Services/FeedService.cs` | how runs/PRs/branches become feed items + subtitles |
| `Services/FeedMonitor.cs` | polling loop, seen-set, notify-on-new, auth-failure handling |
| `Services/PullDetailStore.cs` | 60s PR-detail cache (Task 3) |
| `Pages/SettingsPage.xaml(.cs)` | every settings control + device-flow flow |
| `Models/AppSettings.cs` | the persisted settings shape + `MonitorEvents` list |
| `Services/CredentialStore.cs` | token storage (→ Keychain on macOS) |
| `Services/CoreError.cs` | auth detection + clean error text (Task 2) |
| `Services/SafeUrl.cs` | http/https-only link opening |
| `Services/UpdateToast.cs` | new-item notifications (→ UNUserNotificationCenter, Task 5) |
| `Services/UpdateService.cs` + `docs/RELEASING.md` | Velopack auto-update (Task 10) |
| `Ui/MarkdownLite.cs` + `Pages/ChangelogPage.*` | in-app changelog rendering (Task 9) |
| `MainWindow.xaml(.cs)` | glass backdrop + tray + title bar (→ menu bar + vibrancy) |
| `Strings/{en,pt-BR}/Resources.resw` | the exact localized phrases to reuse (Task 7) |

## Current scaffold (already in the repo)

```
macos/
  Package.swift                     SwiftPM; links liboctowatch_core.a + Security/CoreFoundation/SystemConfiguration
  lib/liboctowatch_core.a           universal static lib (produced by the build script; gitignored)
  Sources/
    octowatch_coreFFI/              C header + modulemap for the bindings
    OctoWatch/
      Generated/octowatch_core.swift  UniFFI Swift bindings (generated)
      OctoWatchApp.swift            @main: MenuBarExtra(.window) + Settings scene + AppDelegate(.accessory)
      FeedView.swift                the popover: feed list, refresh, quit, open-settings
      SettingsView.swift            device-flow sign-in, owner/repo, polling interval
      FeedStore.swift               @MainActor ObservableObject: polls the core, publishes items
      FeedItem.swift                view model + status colors + relative time
      Session.swift                 token in the macOS Keychain
```

Implemented today: menu bar feed of Actions/PRs/branches (status color + relative time),
device-flow sign-in (Keychain), manual owner/repo + polling interval. English strings.

## Core API reference (Swift names)

`try Client(token: String)` — empty token = anonymous (public data only). All methods throw.
- `whoami() -> String`
- `listWorkflowRuns(repo: Repo) -> [WorkflowRun]`
- `listPullRequests(repo: Repo) -> [PullRequest]`
- `listBranches(repo: Repo) -> [Branch]`
- `listCommits(repo: Repo, branch: String) -> [Commit]`
- `getPullRequest(repo: Repo, number: Int64) -> PullDetail`
- `listRepositories() -> [Repo]`
- `rerunWorkflow / rerunFailedJobs / cancelWorkflow(repo:, runId: Int64)`
- `submitReview(repo:, number: Int64, event: String, body: String)` — event = `"APPROVE"`, `"REQUEST_CHANGES"` or `"COMMENT"`; body required for the last two.
- `mergePull(repo:, number: Int64, method: String)` — method = `"merge"`, `"squash"` or `"rebase"`.

Global funcs: `startDeviceLogin(scopes: String) -> DeviceCode`,
`pollDeviceLogin(deviceCode: String) -> DeviceLoginStatus`.

Models: `Repo(owner:name:)`; `WorkflowRun(id,name,status,conclusion?,branch,event,commitMessage,updatedAt,htmlUrl)`;
`PullRequest(number,title,author,state,draft,merged,headBranch,baseBranch,updatedAt,htmlUrl)`;
`Branch(name,lastCommitSha,protected)`; `Commit(sha,message,author,date,htmlUrl)`;
`PullDetail(number,title,body,author,state,draft,merged,mergeable: Bool?,additions,deletions,changedFiles,comments,commits,headBranch,baseBranch,labels:[String],requestedReviewers:[String],htmlUrl,updatedAt)`;
`DeviceCode(userCode,verificationUri,deviceCode,interval:UInt32,expiresIn:UInt32)`.

Enums: `DeviceLoginStatus`: `.pending, .slowDown, .expired, .denied, .authorized(token: String)`.
Errors: `OctoError.Auth(msg:)`, `.NotFound(msg:)`, `.Api(msg:)` (all `: Error`).

The core already does OAuth device flow, **ETag conditional caching** (saves rate limit),
`getPullRequest`, and the review/merge actions — the macOS app gets all of that for free.

---

## Task 1 — Confirm the base builds and runs

Run `./scripts/build-core-macos.sh` then `swift build`. Fix any linker/toolchain issues
(see gotchas). Run it, sign in via Settings (⌘,) against a public repo (owner `cli`,
repo `cli`) to see the feed. **Verify** the popover opens from the menu bar, the feed
loads, and clicking a row opens the URL in the browser.

## Task 2 — Graceful auth handling + clean errors

Mirror the Windows behavior: an invalid/expired token must be dropped and the user asked
to sign in again (not shown as a raw error).

- Add `CoreError.swift` with helpers:
  ```swift
  enum CoreError {
      static func isAuth(_ error: Error) -> Bool {
          if case OctoError.Auth = error { return true }
          return false
      }
      static func describe(_ error: Error) -> String {
          switch error {
          case OctoError.Auth(let msg), OctoError.NotFound(let msg), OctoError.Api(let msg): return msg
          default: return "\(error)"
          }
      }
  }
  ```
- In `FeedStore.refresh()`'s `catch`: if `CoreError.isAuth(error)` → `Keychain.delete()`,
  set `signedIn = false`, and set `error = "Your GitHub sign-in expired. Sign in again."`
  (localized). Otherwise `error = CoreError.describe(error)`.

**Verify**: with a bad token, the popover shows the friendly message and the Settings
screen returns to the "Sign in" state.

## Task 3 — Expandable PR detail (core is ready)

Windows expands PR cards to show the full detail on demand. Do the same:

- In `FeedView`, render PR rows as a `DisclosureGroup` (or a tappable row that toggles a
  detail view). On first expand, call `Client.getPullRequest(repo:number:)` off the main
  actor and cache the result in a `@State`/store dictionary keyed by `owner/repo#number`
  (mirror the Windows `PullDetailStore`, TTL ~60s). Show a spinner while loading.
- Detail content: body (render as Markdown — SwiftUI `Text` supports basic Markdown via
  `try? AttributedString(markdown:)`), a stats line `+additions −deletions · changedFiles files`,
  a meta line (state / draft / merged / mergeable / reviewers / labels), and an
  "Open on GitHub" link. Guard link opening to http/https (parity with `SafeUrl`).

**Verify**: expanding a PR row loads and shows its detail; re-expanding is instant (cache).

## Task 4 — PR actions: approve / request changes / comment / merge

The core methods exist (`submitReview`, `mergePull`). In the PR detail from Task 3:

- Add buttons: **Approve** (accent), **Request changes**, **Comment**, **Merge**.
- Request changes / Comment reveal a `TextEditor` for the required body.
- **Approve** and **Merge** must **confirm first** (a `.confirmationDialog`). You can't
  approve your own PR (GitHub returns 422) — surface that error cleanly via `CoreError`.
- Run the action off the main actor, then refresh the feed and show the result inline.
  ```swift
  try await Task.detached {
      try Session.makeClient().submitReview(repo: r, number: n, event: "APPROVE", body: "")
  }.value
  ```

**Verify**: approving a PR you can review posts the review (check on GitHub); errors
(own PR / no permission) show a friendly message.

## Task 5 — Notifications for new items

Mirror the Windows toast. Use **UserNotifications** (`UNUserNotificationCenter`):

- Request authorization on first launch: `UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound])`.
- In `FeedStore`, keep a `Set<String>` of seen item ids across refreshes (like the Windows
  `_seen`). After a background refresh (not the first "priming" one), post a notification
  for items whose id is new **and** whose kind matches the user's selected events (Task 6).
- Tapping a notification should open the item URL / show the popover. Handle via a
  `UNUserNotificationCenterDelegate`.

**Verify**: a new run/PR while the app is running raises a macOS notification.

## Task 6 — Repository selection, events, and settings model

Mirror the Windows `AppSettings` (owner/repo is only the fallback):

- Model the settings (repos to watch, monitored events per repo/global, polling seconds,
  language, theme, transparency, auto-update, start-at-login). Persist as JSON in
  `FileManager.default.urls(for: .applicationSupportDirectory)` or `UserDefaults`.
- Settings UI (in `SettingsView`, use `TabView` or sections): sign-in (done), a
  **repository list with checkboxes** loaded via `Client.listRepositories()` with a search
  field, and **per-event toggles** (PR opened / merged / closed, review requested,
  mentioned, team mention, CI/Actions, push, assign — same set as Windows). Feed then
  uses the selected repos instead of the manual owner/repo.

**Verify**: selecting repos + events changes what the feed shows and what raises notifications.

## Task 7 — Localization (pt-BR + en)

- Add a **String Catalog** (`Localizable.xcstrings`) or `Localizable.strings` for `en` and
  `pt-BR`. Use `LocalizedStringKey` / `String(localized:)` for all user-facing text.
- The default should follow the system language; add an in-app language override (Picker)
  that sets the app's locale (e.g. via `environment(\.locale, ...)` and persisting the choice).
- Match the Windows string set (see `../windows/OctoWatch/Strings/{en,pt-BR}/Resources.resw`
  for the exact phrases to mirror).

## Task 8 — Appearance: theme + adjustable transparency

- **Theme**: Light / Dark / System. Set `NSApp.appearance = NSAppearance(named: .darkAqua / .aqua)`
  or leave `nil` for System. Persist the choice.
- **Transparency**: the `MenuBarExtra` window is vibrant by default. For an adjustable glass
  like Windows, wrap the popover background in an `NSVisualEffectView` (via
  `NSViewRepresentable`) and expose a material/opacity control in Settings. Note: macOS
  vibrancy is coarser than the Windows acrylic+alpha approach — a material picker
  (e.g. `.menu`, `.popover`, `.hudWindow`) is the idiomatic equivalent.

## Task 9 — In-app changelog

- Reuse the shared localized markdown: `../windows/OctoWatch/Assets/changelog/{en,pt-BR}.md`
  (copy them into a macOS resource bundle, or point at a shared `docs/changelog/`). Render
  with SwiftUI Markdown (`AttributedString(markdown:)`) or a small renderer.
- Add a "Changelog" section/window reachable from Settings or the popover menu. Keep the
  canonical `../CHANGELOG.md` (Keep a Changelog) in sync on releases.

## Task 10 — Auto-update

- **Velopack** is cross-platform and already used on Windows — prefer it for consistency:
  add the Velopack Swift/CLI integration, `VelopackApp` at startup, check GitHub Releases,
  and a "Check for updates" button + "update on launch" toggle in Settings. See
  `../docs/RELEASING.md` for the Windows pipeline and mirror it with `vpk` for macOS
  (produces a `.app` installer + delta).
- Alternative: **Sparkle** (the macOS-native updater) if Velopack's macOS support doesn't fit.

## Task 11 — Custom menu bar icon

- The Windows `.ico` won't work here. Add a **template image** (PDF or PNG, `isTemplate = true`)
  to an asset catalog and use it for the `MenuBarExtra` label instead of the SF Symbol, so
  it tints correctly for light/dark menu bars. Reuse the OctoWatch mark (radar/pulse) as a
  monochrome template.

## Task 12 — Background polling + start at login

- Start polling from launch (not only when the popover first opens): create the `FeedStore`
  in the `AppDelegate` (or a shared singleton) and call `start()` in
  `applicationDidFinishLaunching`, so notifications fire even before the popover is opened.
- **Start at login**: use `SMAppService.mainApp.register()` (macOS 13+) behind a Settings
  toggle (mirrors the Windows `StartupRegistry`).

---

## Definition of done

- `./scripts/build-core-macos.sh && (cd macos && swift build)` succeeds; the app runs.
- Menu bar popover shows the feed; sign-in, repo/event selection, polling all work.
- PR cards expand to detail; approve / request-changes / comment / merge work with
  confirmation and clean errors.
- Notifications fire for new, matching items.
- Localized (pt-BR/en), themeable, adjustable transparency, in-app changelog.
- Auto-update wired (or documented pending the first release), custom menu bar icon,
  start-at-login.
- No auth errors shown raw — expired tokens drop and prompt re-sign-in.
