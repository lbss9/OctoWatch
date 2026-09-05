<h1 align="center">OctoWatch</h1>

<p align="center">
  <strong>Monitore seus repositórios do GitHub a partir do desktop.</strong><br />
  GitHub Actions, Pull Requests, branches e commits — em três aplicativos <strong>nativos</strong>
  (Windows, macOS, Linux) que compartilham um único <strong>núcleo em Rust</strong>.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/status-em%20desenvolvimento-F5A623?style=flat-square" alt="Em desenvolvimento" />
  <img src="https://img.shields.io/badge/vers%C3%A3o-0.1.0-58A6FF?style=flat-square" alt="Versão 0.1.0" />
  <img src="https://img.shields.io/badge/core-Rust-DEA584?style=flat-square&logo=rust&logoColor=white" alt="Núcleo em Rust" />
  <img src="https://img.shields.io/badge/Windows-WinUI%203-0078D4?style=flat-square&logo=windows&logoColor=white" alt="Windows / WinUI 3" />
  <img src="https://img.shields.io/badge/macOS-SwiftUI-000000?style=flat-square&logo=apple&logoColor=white" alt="macOS / SwiftUI" />
  <img src="https://img.shields.io/badge/Linux-GTK4-4A86CF?style=flat-square&logo=linux&logoColor=white" alt="Linux / GTK4 (planejado)" />
</p>

<p align="center">
  <a href="#arquitetura">Arquitetura</a> ·
  <a href="#estado-atual">Estado atual</a> ·
  <a href="#estrutura">Estrutura</a> ·
  <a href="#como-compilar">Compilar</a> ·
  <a href="CHANGELOG.md">Changelog</a>
</p>

> **Nota:** nome provisório. O foco atual é acompanhar repositórios open source.

---

## Visão geral

O OctoWatch mantém você a par do que acontece nos seus repositórios sem abrir o navegador:
um feed com **Actions**, **Pull Requests**, **branches** e **commits**, com bolinhas de status
e detalhes sob demanda. A ideia central é escrever **toda a lógica do GitHub uma única vez**, em
Rust, e dar a cada sistema operacional uma interface **nativa e idiomática** por cima dela.

---

## Arquitetura

Toda a lógica do GitHub (autenticação, chamadas à API, cache, futuro polling) vive uma só vez no
núcleo Rust (`core/octowatch-core`) e é exposta a cada UI via [UniFFI](https://github.com/mozilla/uniffi-rs):

| Plataforma | UI nativa | Como consome o núcleo |
|---|---|---|
| **Windows** | C# + WinUI 3 (Windows App SDK) | `octowatch_core.dll` (`cdylib`) + bindings C# (`uniffi-bindgen-cs`) |
| **macOS** | Swift + SwiftUI (`MenuBarExtra`) | `staticlib` + bindings Swift (UniFFI oficial) |
| **Linux** *(planejado)* | Rust + GTK4 + libadwaita | linka a crate do núcleo **direto** (sem FFI) |

APIs específicas de cada SO (fora do núcleo): bandeja / menu bar, notificações
(toast WinRT / `UNUserNotificationCenter` / libnotify) e armazenamento seguro do token
(Credential Manager / Keychain / libsecret).

### O núcleo em Rust

`octowatch-core` usa **octocrab** sobre **tokio** e expõe um `Client` com:

- `login` / `whoami` — autenticação e identidade do usuário;
- `list_workflow_runs`, `list_pull_requests`, `list_branches`, `list_commits` — o feed;
- `get_pull_request` — detalhe de um PR sob demanda;
- `submit_review`, `merge_pull` — ações sobre Pull Requests;
- **cache condicional (ETag)** nos GETs, para economizar o rate limit da API.

Os testes de integração rodam contra a API real do GitHub.

---

## Estado atual

- [x] **Núcleo Rust** — `Client` completo (auth, feed, detalhe de PR, review/merge) com cache ETag;
      testes de integração contra a API real passando.
- [x] **Geração de bindings** Swift e C# validada (superfície FFI equivalente nas duas linguagens).
- [x] **App Windows (WinUI 3)** — flyout ancorado no canto inferior direito, fundo **Mica/acrílico**,
      barra de título própria (só minimizar + fechar), feed **Todos / Actions / PRs / Branches** com
      bolinhas de status, **cards de PR expansíveis** com detalhe sob demanda, controle de
      transparência, changelog localizado in-app e recolhimento para a **bandeja**. Auto-update via
      Velopack. Smoke test de FFI passando.
- [x] **App macOS (SwiftUI)** — app de **menu bar** (`MenuBarExtra`) com sign-in, feed e
      configurações, compartilhando o mesmo núcleo Rust via FFI. Paridade de recursos com o Windows.
- [ ] **App Linux (GTK4 + libadwaita)** — planejado, ainda não iniciado.
- [ ] **Motor de polling no núcleo** — hoje o refresh é disparado pela UI; a ideia é centralizá-lo.
- [ ] **Notificações de mudança de status** (toast no Windows, etc.).

---

## Estrutura

```
core/     workspace Rust — núcleo compartilhado (octocrab + tokio + uniffi)
windows/  app C# / WinUI 3          (bindings gerados em windows/OctoWatch/Interop)
macos/    app Swift / SwiftUI       (bindings gerados em macos/Sources/OctoWatch/Generated)
scripts/  build-core, gen-bindings  (.ps1 e .sh, + build-core-macos.sh)
docs/     guias de release e handoff
```

> O app Linux (`linux/`) entra quando essa fase começar; hoje ele existe só como plano na tabela acima.

---

## Como compilar

### Núcleo (Rust)

```powershell
pwsh scripts/build-core.ps1        # compila + testa   (bash: scripts/build-core.sh)
```

No macOS, use `scripts/build-core-macos.sh` para gerar o `staticlib` consumido pelo app Swift.
Defina `GITHUB_TOKEN` (um PAT) para autenticar os testes e evitar o rate limit anônimo.

### Bindings

```powershell
# instale o gerador C# uma vez:
cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.9.2+v0.28.3
pwsh scripts/gen-bindings.ps1      # gera Swift + C# a partir da lib do núcleo
```

### App Windows

Requer o **.NET 8+ SDK** e o workload do **Windows App SDK / WinUI 3**.
Instalar o SDK: <https://aka.ms/dotnet/download>.

### App macOS

Requer **Xcode** (Swift 5.9+). Abra `macos/Package.swift` no Xcode ou rode `swift build` a partir
de `macos/`, depois de ter gerado o núcleo e os bindings.

---

## Pré-requisitos por alvo

- **Núcleo:** Rust (`cargo`).
- **Windows:** .NET 8+ SDK + Windows App SDK.
- **macOS:** Xcode (Swift 5.9+).
- **Linux** *(quando começar):* Rust + `gtk4` / `libadwaita` de desenvolvimento
  (ex.: `libgtk-4-dev libadwaita-1-dev`).
