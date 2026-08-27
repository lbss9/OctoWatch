# Releasing OctoWatch

Unpackaged Windows builds update from **GitHub Releases** via [Velopack](https://docs.velopack.io).
App Installer / MSIX auto-update is not available without package identity.

## One-time setup

```bash
export PATH="$PATH:/c/Program Files/dotnet"
dotnet tool install -g vpk
```

Use the same `vpk` version as the `Velopack` package in `windows/OctoWatch/OctoWatch.csproj` (currently 1.2.0).

## Each release

Bump `<Version>` in `windows/OctoWatch/OctoWatch.csproj` and `--packVersion` together.

```bash
export PATH="$PATH:/c/Program Files/dotnet"
cd windows/OctoWatch
powershell -Command "Get-Process OctoWatch -EA SilentlyContinue | Stop-Process -Force"
dotnet publish -c Release -r win-x64 -o ./publish
vpk pack --packId OctoWatch --packVersion 0.1.0 --packDir ./publish --mainExe OctoWatch.exe --packTitle OctoWatch
vpk upload github --repoUrl https://github.com/lbss9/octowatch --publish --releaseName "OctoWatch 0.1.0" --tag v0.1.0 --token <GH_TOKEN>
```

`GH_TOKEN` needs permission to create releases on `lbss9/octowatch`.

End-to-end update can only be verified after **two** published Velopack releases exist (the installed build plus a newer one).
