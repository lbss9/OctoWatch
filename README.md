# OctoWatch

Monitor de repositórios do GitHub — **GitHub Actions, Pull Requests, branches e commits** — com
três aplicativos desktop **nativos idiomáticos** (Windows, macOS, Linux) compartilhando um **núcleo
em Rust**.

> Nome provisório. Foco atual: repositórios open source.

## Arquitetura

Toda a lógica do GitHub (auth, chamadas à API, futuro polling) vive uma única vez no núcleo Rust
(`core/octowatch-core`) e é exposta a cada UI via [UniFFI](https://github.com/mozilla/uniffi-rs):

| Plataforma | UI nativa | Consumo do núcleo |
|---|---|---|
| **Windows** | C# + WinUI 3 (Windows App SDK) | `octowatch_core.dll` (`cdylib`) + bindings C# (`uniffi-bindgen-cs`) |
| **macOS** | Swift + SwiftUI / AppKit (`MenuBarExtra`) | `staticlib` + bindings Swift (UniFFI oficial) |
| **Linux** | Rust + GTK4 + libadwaita | linka a crate do núcleo **direto** (sem FFI) |

APIs nativas por SO (fora do núcleo): bandeja/menu bar, notificações (toast WinRT /
`UNUserNotificationCenter` / libnotify) e armazenamento seguro do token (Credential Manager /
Keychain / libsecret).

## Estrutura

```
core/     workspace Rust — núcleo compartilhado (octocrab + tokio + uniffi)
windows/  app C# / WinUI 3          (bindings em windows/OctoWatch/Interop, gerados)
macos/    app Swift / SwiftUI       (bindings em macos/.../Generated, gerados)
linux/    app Rust / GTK4+libadwaita
scripts/  build-core, gen-bindings  (.ps1 e .sh)
```

## Estado atual

- [x] **Núcleo Rust**: `Client` com `login/whoami`, `list_workflow_runs`, `list_pull_requests`,
      `list_branches`, `list_commits`. Testes de integração contra a API real passando.
- [x] **Geração de bindings** Swift e C# validada (superfície FFI correta nas duas linguagens).
- [ ] App Windows (WinUI 3) — próximo.
- [ ] Motor de polling + notificações no núcleo.
- [ ] Apps Linux e macOS.

## Como buildar

### Núcleo (funciona nesta máquina)

```powershell
pwsh scripts/build-core.ps1        # compila + testa   (bash: scripts/build-core.sh)
```

Defina `GITHUB_TOKEN` (PAT) para autenticar e evitar o rate limit anônimo.

### Bindings

```powershell
# instale o gerador C# uma vez:
cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.9.2+v0.28.3
pwsh scripts/gen-bindings.ps1      # gera Swift + C# a partir da .dll
```

### App Windows (pendências de ambiente)

Requer o **.NET SDK** (a máquina tem só o runtime) e o workload do **Windows App SDK / WinUI 3**.
Instalar o SDK: <https://aka.ms/dotnet/download>.

## Pré-requisitos por alvo

- **Núcleo**: Rust (`cargo`).
- **Windows**: .NET 8+ SDK + Windows App SDK.
- **macOS**: Xcode (Swift 5.9+).
- **Linux**: Rust + `gtk4`/`libadwaita` de desenvolvimento (ex.: `libgtk-4-dev libadwaita-1-dev`).
