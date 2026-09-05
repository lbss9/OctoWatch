# OctoWatch — Remaining work (step-by-step execution guide)

> **Status (2026-08-27): Tasks 1–5 are DONE** (icon wiring, C# translation to English,
> organization pass, Velopack auto-update, localized in-app changelog). App builds clean;
> Rust `cargo test` and the 14 xUnit tests pass. Only **Task 6 (optional UI polish)** and
> the real release setup (set `RepoUrl` in `UpdateService`, publish the first GitHub release)
> remain. The guide below is kept for reference.
>
> This is a precise, do-exactly-this guide for finishing OctoWatch. Follow each task
> in order. Every task lists the files to touch, the exact code, and how to verify.
> Don't improvise architecture — match what's described here.

## Ground rules (read first)

- **Language of the code**: everything in the code is **English** — comments, log/console
  strings, exception messages, test names. The Rust core is already fully English.
- **User-facing UI strings** are **NOT** hardcoded — they live in the i18n resource files
  `windows/OctoWatch/Strings/pt-BR/Resources.resw` and `.../en/Resources.resw`, read via
  `Loc.Get("Key")` or `x:Uid`. When you add UI text, add both pt-BR and en entries.
- **Comments/docs**: English, short, punctual, human — explain the *why*, not the obvious.
  No essay blocks. One or two lines is usually enough.
- **Commits**: small and focused; end the message body with
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

### Build & verify (Windows app)

```bash
# dotnet isn't on PATH in Git Bash by default:
export PATH="$PATH:/c/Program Files/dotnet"
# ALWAYS kill running instances first — the app lives in the tray and locks the .exe:
powershell -Command "Get-Process OctoWatch -EA SilentlyContinue | Stop-Process -Force"
cd windows/OctoWatch && dotnet build -c Debug
```

- Rust core: `cd core && cargo test` (needs `GITHUB_TOKEN` env for the auth-only tests;
  the rest run anonymously).
- Regenerate C# bindings only if the core's public API changed:
  `cd core && cargo build --release && uniffi-bindgen-cs --library target/release/octowatch_core.dll --out-dir ../windows/OctoWatch/Interop && cp target/release/octowatch_core.dll ../windows/OctoWatch/octowatch_core.dll`
- Screenshot the app: it anchors bottom-right; enumerate its window ("OctoWatch") with
  EnumWindows/GetWindowRect and `Graphics.CopyFromScreen` that rect (SetForegroundWindow from
  another process fails). Open the nav pane in tests via UIAutomation `TogglePaneButton`.

### Project layout (keep this shape)

```
core/octowatch-core   Rust shared core (octocrab + tokio + uniffi). English. Done.
windows/OctoWatch     WinUI 3 app (net8.0-windows, x64, unpackaged, self-contained)
  Pages/              HomePage, SettingsPage, AboutPage, ChangelogPage
  Services/           FeedMonitor, FeedService, GitHubSession, CredentialStore,
                      SettingsStore, StartupRegistry, UpdateToast, Loc, SafeUrl
  Logic/              FeedMapper, FeedDiff, CardActions   (pure, unit-testable)
  Models/             AppSettings, RepoChoice
  Controls/           StatusDot
  Interop/            octowatch_core.cs (GENERATED — do not edit, gitignored)
  Strings/{pt-BR,en}  Resources.resw (i18n)
  Assets/             OctoWatch.ico
windows/OctoWatch.Tests   xUnit tests for Logic/*
macos/, linux/        skeletons (not this milestone)
```

---

## Task 1 — Finish wiring the app icon

The icon already exists at `windows/OctoWatch/Assets/OctoWatch.ico` (multi-size) and is set as
the **exe** icon via `<ApplicationIcon>` in the csproj. Finish wiring it into the window, the
tray, and the title-bar logo.

**1a. Ship the .ico with the app.** In `windows/OctoWatch/OctoWatch.csproj`, add to an
`<ItemGroup>`:

```xml
<Content Include="Assets\OctoWatch.ico">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

**1b. Window / taskbar / alt-tab icon.** In `windows/OctoWatch/MainWindow.xaml.cs`, in the
constructor right after the `AppWindow.TitleBar...` lines, add:

```csharp
// System window icon (taskbar, alt-tab, title bar).
var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "OctoWatch.ico");
if (System.IO.File.Exists(iconPath))
    AppWindow.SetIcon(iconPath);
```

**1c. Tray icon.** In `MainWindow.xaml.cs`, method `SetupTray()`, replace the
`IconSource = new GeneratedIconSource { Text = "O", ... }` initializer with the real icon:

```csharp
IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
    new Uri("ms-appx:///Assets/OctoWatch.ico")),
```

If the tray icon renders blank at runtime (unpackaged ms-appx quirk), fall back to a GDI icon
after `_tray.ForceCreate();`:

```csharp
try { _tray.Icon = new System.Drawing.Icon(iconPath, 32, 32); } catch { /* keep default */ }
```

(`_tray.Icon` is `System.Drawing.Icon`; `iconPath` is the same absolute path from 1b — hoist it
to a field or recompute it.)

**1d. Title-bar logo.** In `windows/OctoWatch/MainWindow.xaml`, inside `AppTitleBar`, replace the
`<FontIcon Glyph="&#xE945;" .../>` with the real mark:

```xml
<Image Width="18" Height="18" VerticalAlignment="Center"
       Source="ms-appx:///Assets/OctoWatch.ico" />
```

**Verify**: build, run. The taskbar/alt-tab icon, the tray icon, and the title-bar logo all show
the OctoWatch mark (dark→cyan tile, radar arcs, green center). No blank/placeholder icons.

---

## Task 2 — Translate the remaining C# code to English

The Rust core is done. Now make the **C#** code English. Only **code** text (comments, log
strings, exception messages, internal debug text). **Do NOT touch** `Strings/*/Resources.resw`
(those are the localized UI strings and must stay bilingual) and **do NOT touch** the generated
`Interop/octowatch_core.cs`.

**How to find it**: grep the C# tree for Portuguese. Accented words and common function words:

```bash
grep -rnE '[áàâãéêíóôõúçÁÉÍÓÚ]|\b(não|está|própr|conteúdo|janela|vidro|fundo|bandeja|senão|usuário|repositório|Reverte|Faixa|Menu|Só|ao vivo|Traduz|Cria|Ancora|Esconde)\b' windows/OctoWatch --include='*.cs' | grep -v '/Interop/'
```

Go file by file. For each Portuguese **comment**, rewrite it as a short English comment (keep the
intent, make it human and punctual). For each Portuguese **string that is a log / exception /
debug message** (not shown as UI, or not already localized), translate it to English. Files known
to contain Portuguese comments include (non-exhaustive — trust the grep):
`MainWindow.xaml.cs`, `Pages/HomePage.xaml.cs`, `Pages/SettingsPage.xaml.cs`,
`Logic/FeedDiff.cs`, `Models/AppSettings.cs`, `Services/*` (check each), plus the XAML files'
`<!-- comments -->` (translate those too).

Notes:
- `App.xaml.cs` writes a `crash.log` — keep it, it's fine (local, per-user). Just ensure any
  comment there is English.
- macOS (`macos/`) is a skeleton; translate the few comments if present, but there's little there.

**Verify**: `dotnet build -c Debug` is clean; re-run the grep above and confirm **zero** matches
outside `Interop/` and `Strings/`.

Then commit: `chore: translate C# code comments/logs to English`.

---

## Task 3 — Organization / maintainability pass

The structure above is already good; keep it. Do a light pass, not a rewrite:

- Each `Services/*` type should have **one** responsibility. If a class is doing two things,
  split it. `Logic/*` must stay **pure** (no UI/WinRT types) so it's unit-testable — verify that
  `FeedMapper`, `FeedDiff`, `CardActions` have no `Microsoft.UI.*`/`Windows.*` dependencies.
- Keep `MainWindow.xaml.cs` focused on window/shell concerns (title bar, backdrop, tray, nav).
  It's grown large — if it passes ~300 lines, extract the backdrop/glass logic into a
  `Services/WindowBackdrop.cs` helper that takes the `Window` and applies acrylic + the alpha
  overlay, and the tray logic into a `Services/TrayIconHost.cs`. Only do this if it stays simpler.
- Confirm there are no `async void` methods except event handlers.
- Add xUnit tests in `windows/OctoWatch.Tests` for any `Logic/*` you touched (there's already a
  `FeedMapperTests.cs` to follow as a pattern). Run: `dotnet test windows/OctoWatch.Tests`.
- Do **not** introduce a heavy DI framework or MVVM rewrite — the app is small; code-behind +
  static services is fine. Favor clarity over ceremony.

Commit: `refactor: tighten service/logic separation`.

---

## Task 4 — Auto-update with Velopack

Use **Velopack** (https://github.com/velopack/velopack, https://docs.velopack.io) — the modern
Squirrel successor, cross-platform, delta updates, reads from **GitHub Releases**. Chosen because
the app is **unpackaged** (no MSIX/store identity, so App Installer auto-update is unavailable).

**4a. Add the package.** `windows/OctoWatch/OctoWatch.csproj`:

```xml
<PackageReference Include="Velopack" Version="0.0.*" />
```

(Use the latest stable Velopack from NuGet.)

**4b. Initialize Velopack first thing at startup.** Velopack MUST run before any other app code.
In `windows/OctoWatch/App.xaml.cs`, make it the **first** statement of the `App()` constructor,
before `this.InitializeComponent()` and before the crash-log handler:

```csharp
public App()
{
    Velopack.VelopackApp.Build().Run(); // must be first — handles install/update hooks
    // ... existing crash-log handler ...
    this.InitializeComponent();
}
```

**4c. Update service.** Add `windows/OctoWatch/Services/UpdateService.cs`:

```csharp
using Velopack;
using Velopack.Sources;

namespace OctoWatch;

/// <summary>Checks GitHub Releases for a newer build and applies it via Velopack.</summary>
internal static class UpdateService
{
    // TODO: set to the real repo once it's published.
    private const string RepoUrl = "https://github.com/OWNER/octowatch";

    public static bool IsSupported => Velopack.Locators.VelopackLocator.GetDefault(null).CurrentlyInstalledVersion is not null;

    public static async Task<bool> CheckAndApplyAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!mgr.IsInstalled) return false;
            var info = await mgr.CheckForUpdatesAsync();
            if (info is null) return false;            // already up to date
            await mgr.DownloadUpdatesAsync(info);
            mgr.ApplyUpdatesAndRestart(info);          // restarts into the new version
            return true;
        }
        catch
        {
            return false;                              // never crash the app over an update check
        }
    }
}
```

**4d. Settings UI.** In `windows/OctoWatch/Pages/SettingsPage.xaml`, add a new section
(mirror the existing card style) with a **"Check for updates"** button and a status `TextBlock`,
plus a **"Update automatically on launch"** `ToggleSwitch`. Add resw keys in **both** languages:
`Settings_UpdatesHeader` ("Atualizações"/"Updates"), `Settings_CheckUpdates`
("Verificar atualizações"/"Check for updates"), `Settings_AutoUpdate`
("Atualizar automaticamente ao abrir"/"Update automatically on launch"),
`Settings_UpToDate` ("Você está na versão mais recente."/"You're on the latest version."),
`Settings_Updating` ("Baixando atualização…"/"Downloading update…").
Persist the auto-update toggle in `Models/AppSettings.cs` as `bool AutoUpdate` (default `false`),
handled like the other settings in `SettingsPage.xaml.cs`.

Wire the button to `UpdateService.CheckAndApplyAsync()` (run off the UI thread via `Task.Run`
is not needed — it's already async; just `await` it and show the status). In `App.OnLaunched`,
if `SettingsStore.Load().AutoUpdate` is true, fire-and-forget `_ = UpdateService.CheckAndApplyAsync();`
after the window is shown.

**4e. Release pipeline (document it in `docs/RELEASING.md`).** Install the CLI once:
`dotnet tool install -g vpk`. Then per release:

```bash
export PATH="$PATH:/c/Program Files/dotnet"
cd windows/OctoWatch
dotnet publish -c Release -r win-x64 -o ./publish
vpk pack --packId OctoWatch --packVersion 0.1.0 --packDir ./publish --mainExe OctoWatch.exe --packTitle OctoWatch
vpk upload github --repoUrl https://github.com/OWNER/octowatch --publish --releaseName "OctoWatch 0.1.0" --tag v0.1.0 --token <GH_TOKEN>
```

Bump `<Version>` in the csproj and `--packVersion` together each release.

**Verify**: build succeeds; the Settings "Check for updates" button runs without throwing when the
app is not Velopack-installed (it returns false quietly). Full end-to-end update can only be
tested after the first two GitHub releases exist.

---

## Task 5 — Changelog (Keep a Changelog, in-app + i18n)

There is already a `CHANGELOG.md` at the repo root, it's included as content in the csproj, and a
`Pages/ChangelogPage` exists. Make it complete and localized.

**5a. Canonical `CHANGELOG.md`** (repo root) follows **Keep a Changelog**
(https://keepachangelog.com/en/1.0.0/): newest version first, ISO dates, grouped sections. In
**English**. Shape:

```markdown
# Changelog

All notable changes to OctoWatch are documented here.
The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [Unreleased]

## [0.1.0] - 2026-08-27
### Added
- Windows app: GitHub Actions feed with live status dots, glass/acrylic backdrop,
  tray with open/quit, GitHub device-flow sign-in, per-repo event selection.
- Shared Rust core (GitHub client) exposed to the UI via UniFFI.
### Changed
- Adjustable window transparency (Windows Terminal-style opacity + acrylic toggle).
### Security
- External links are restricted to http/https.
```

Backfill real entries from the git history.

**5b. Localized in-app changelog.** The in-app page should show the changelog in the app's
language, falling back to English. Create bundled markdown per language:
`windows/OctoWatch/Assets/changelog/en.md` and `.../pt-BR.md` (the en.md can be the same content
as the root `CHANGELOG.md`; keep them in sync). Add them as `Content` (CopyToOutputDirectory) in
the csproj.

**5c. Render markdown natively.** Add the Community Toolkit markdown control (renders markdown to
native WinUI controls):

```xml
<PackageReference Include="CommunityToolkit.WinUI.Controls.MarkdownTextBlock" Version="8.*" />
```

Rewrite `Pages/ChangelogPage.xaml` to host a `MarkdownTextBlock` inside a `ScrollViewer`, and in
`ChangelogPage.xaml.cs` load the file for the current language:

```csharp
var lang = SettingsStore.Load().Language;          // "pt-BR" or "en"
var file = $"changelog/{lang}.md";
var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", file);
if (!System.IO.File.Exists(path))
    path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "changelog", "en.md");
MarkdownView.Text = System.IO.File.ReadAllText(path);   // MarkdownTextBlock.Text
```

Make links open via `SafeUrl.OpenAsync` (hook the control's link-clicked event; do **not** call
`Launcher` directly — keep the http/https guard).

If you prefer zero third-party deps, write a tiny markdown-to-`RichTextBlock` renderer that
handles `#`/`##` headings, `-` bullets, `**bold**`, and links — but the Community Toolkit control
is the pragmatic, native-looking choice; use it unless told otherwise.

**Verify**: open the Changelog page in the app in both languages (change language in Settings →
the page shows the matching file; unknown language falls back to en). Links open in the browser.

---

## Task 6 — Optional UI polish (do after the above)

These were proposed and are nice-to-have; implement if there's time:

- **Card status accent**: a thin colored left stripe on each feed card matching the `StatusDot`
  state (green/red/amber/gray) for at-a-glance scanning. Add it to the card `Border` in
  `HomePage.xaml` (a 3px `Rectangle`/`Border` in a leading column, color bound from `State`).
- **Relative time**: show "2m ago", "yesterday" in the card subtitle instead of raw status echo.
  Add a `RelativeTime(DateTimeOffset)` helper in `Logic/` (pure, unit-tested), parse the ISO
  `updated_at` the core already returns, and prepend it to the subtitle.
- **Hover-only "…" menu**: reveal the per-card overflow button only on pointer-over (VisualState
  in the item template), for a cleaner list.

---

## Definition of done

- App builds clean; Rust `cargo test` and `dotnet test` pass.
- No Portuguese left in code (comments/logs/exceptions) — only in `Strings/*.resw`.
- Icon shows everywhere (exe, taskbar, tray, title bar).
- Settings has a working "Check for updates"; release steps documented.
- Changelog renders in-app in the app's language and a canonical `CHANGELOG.md` exists.
- Every new UI string exists in both `pt-BR` and `en` resw files.
