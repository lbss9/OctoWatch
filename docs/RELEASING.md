# Releasing OctoWatch (Windows)

OctoWatch ships as an **unpackaged** WinUI 3 app and auto-updates with
[Velopack](https://docs.velopack.io) from **GitHub Releases**.

## One-time setup

```bash
dotnet tool install -g vpk
```

Set the real repository URL in `windows/OctoWatch/Services/UpdateService.cs`
(`RepoUrl`) before the first release.

## Cut a release

Bump the version in `windows/OctoWatch/OctoWatch.csproj` (`<Version>`) and use the
same value for `--packVersion`.

```bash
export PATH="$PATH:/c/Program Files/dotnet"
cd windows/OctoWatch

# 1) Publish a self-contained build.
dotnet publish -c Release -r win-x64 -o ./publish

# 2) Pack it into a Velopack release (installer + delta).
vpk pack \
  --packId OctoWatch \
  --packVersion 0.1.0 \
  --packDir ./publish \
  --mainExe OctoWatch.exe \
  --packTitle OctoWatch \
  --icon Assets/OctoWatch.ico

# 3) Upload to GitHub Releases (creates the tag/release and uploads assets).
vpk upload github \
  --repoUrl https://github.com/OWNER/octowatch \
  --publish \
  --releaseName "OctoWatch 0.1.0" \
  --tag v0.1.0 \
  --token "$GH_TOKEN"
```

Velopack produces the installer (`OctoWatch-win-Setup.exe`), the full package and a
delta against the previous release. Installed clients pick up the new version via
**Settings → Updates → Check for updates**, or automatically on launch when
"Update automatically on launch" is enabled.

Notes:
- Auto-update only works for builds installed via the Velopack **Setup.exe**; a
  plain `dotnet run`/dev build reports "not available in this build".
- Keep `CHANGELOG.md` (and `Assets/changelog/{en,pt-BR}.md`) in sync with each release.
