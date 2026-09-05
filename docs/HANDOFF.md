# OctoWatch — Handoff para continuar o desenvolvimento

> Documento auto-contido para outra IA/dev tocar o projeto sem depender do chat original.
> Última atualização: 2026-08-27.

## 1. O que é

Monitor de repositórios GitHub (GitHub Actions, Pull Requests, branches, commits) com **três apps
desktop nativos** (Windows, macOS, Linux) compartilhando um **núcleo em Rust** exposto via UniFFI.
Foco atual: **app Windows** (WinUI 3). Nome provisório: **OctoWatch**. Monorepo em `E:\Workspace\OctoWatch`,
git iniciado.

Princípio-guia do dono: **usar o máximo de API nativa de cada SO** e a UI precisa ficar **linda,
organizada, estilo Windows**.

## 2. Arquitetura

| Camada | Tech | Observação |
|---|---|---|
| Núcleo | Rust (`core/octowatch-core`): octocrab + tokio + uniffi 0.28 | Lógica do GitHub 1x, exposta às 3 UIs |
| Windows | C# + WinUI 3 (WindowsAppSDK 1.6, net8.0-windows, x64, desempacotado, self-contained) | bindings via `uniffi-bindgen-cs` |
| macOS | Swift + SwiftUI/AppKit (`MenuBarExtra`) | bindings Swift oficiais do UniFFI (já gerados em `macos/.../Generated`) |
| Linux | Rust + gtk4-rs + libadwaita | linka a crate do núcleo direto (sem FFI) |

O núcleo é **síncrono** na fronteira FFI (métodos bloqueiam num runtime tokio compartilhado —
`RT.block_on`). Async cross-FFI fica para depois.

## 3. Estrutura de pastas

```
core/octowatch-core/       # crate Rust (lib.rs, github.rs, models.rs, error.rs, bin/uniffi-bindgen.rs)
  tests/integration.rs     # testes contra a API real (passando)
windows/OctoWatch/         # app WinUI 3
  MainWindow.xaml(.cs)     # janela: título nativo, vidro, tray, aba Actions (SERÁ REFATORADO em shell)
  Controls/StatusDot.*     # bolinha de status verde/vermelho/amarelo-pulsante
  DispatcherQueueHelper.cs # pré-requisito do backdrop controller
  RelayCommand.cs
  Interop/octowatch_core.cs# binding C# GERADO (gitignored) — regerar via script
  octowatch_core.dll       # DLL nativa GERADA (gitignored) — copiada p/ junto do .exe
windows/smoketest/         # harness console headless do FFI (C#->Rust->GitHub)
macos/ , linux/            # esqueletos
scripts/                   # build-core, gen-bindings (.ps1 e .sh)
docs/HANDOFF.md            # este arquivo
```

## 4. Ambiente e como buildar/verificar

Ferramentas já instaladas na máquina do dono: Rust/cargo, .NET SDK 8.0.424, git, `csharpier`,
`uniffi-bindgen-cs` (tag `v0.9.2+v0.28.3`). No shell Bash, o dotnet não está no PATH por padrão:
`export PATH="$PATH:/c/Program Files/dotnet"`.

```bash
# Núcleo: compilar + testar (defina GITHUB_TOKEN p/ o teste whoami e rate limit maior)
cd core && cargo test

# Regerar bindings (após mudar a API pública do núcleo)
cargo build --release
uniffi-bindgen-cs --library target/release/octowatch_core.dll --out-dir ../windows/OctoWatch/Interop
cp target/release/octowatch_core.dll ../windows/OctoWatch/octowatch_core.dll
# Swift (macOS): cargo run --bin uniffi-bindgen -- generate --library <lib> --language swift --out-dir ../macos/Sources/OctoWatch/Generated

# App Windows: build + run
cd windows/OctoWatch && dotnet build -c Debug && dotnet run -c Debug

# Smoke test do FFI (headless)
cd windows/smoketest && dotnet run -c Release
```

### Gotchas de build/verificação (IMPORTANTES)

- **Matar instâncias antes de rebuildar**: o app fica na bandeja ao "fechar" e **trava o `.exe`**
  (`MSB3027 file locked`). Sempre: `powershell -Command "Get-Process OctoWatch -EA SilentlyContinue | Stop-Process -Force"`.
- **Screenshot do app**: trazer a janela pra frente via `SetForegroundWindow` a partir de outro
  processo **falha** (foreground-lock do Windows). A janela ancora no canto inferior direito; capture
  **direto a região** dela. Descubra o `rect` enumerando as janelas do processo (EnumWindows +
  GetWindowRect filtrando pelo PID) — a janela visível tem título `OctoWatch`. Ex. de rect observado:
  `(2088,380)-(2548,1020)` num monitor 2560x1080. Use `Graphics.CopyFromScreen`.
- **`MainWindowHandle` pode vir 0** no .NET mesmo com a janela visível — não é erro; enumere as janelas.
- **XamlCompiler que estoura sem mensagem** (`MSB3073`, exit 1): geralmente é **cascata de um erro C#**
  (a 2ª passagem do XamlCompiler roda após o compile). Procure o `CSxxxx` real; ao corrigir, o
  XamlCompiler passa. Também: **markup de libs de terceiros no XAML** (ex.: H.NotifyIcon) pode crashar
  o XamlCompiler — por isso o TaskbarIcon é criado **em código**, não em XAML.
- **`Color`** (`Color.FromArgb`) exige `using Windows.UI;`. `Colors.X` vem de `Microsoft.UI`.

## 5. Contrato do núcleo (API pública Rust → C#/Swift)

Objeto `Client` (é `internal` no C# gerado, acessível no mesmo assembly). Construtor `new Client(token)`
(token vazio = anônimo). Métodos (síncronos, podem lançar `OctoError`):
`whoami()`, `list_workflow_runs(repo)`, `list_pull_requests(repo)`, `list_branches(repo)`,
`list_commits(repo, branch)`. Records posicionais **camelCase** no C#: `Repo(owner,name)`,
`WorkflowRun(id,name,status,conclusion,branch,event,commitMessage,updatedAt,htmlUrl)`,
`PullRequest(number,title,author,state,draft,headBranch,baseBranch,updatedAt,htmlUrl)`,
`Branch(name,lastCommitSha,@protected)`, `Commit(sha,message,author,date,htmlUrl)`.
`DllImport` usa o nome `"octowatch_core"` (a DLL precisa estar ao lado do .exe).

## 6. Estado atual do app Windows (o que já funciona)

- Flyout ancorado no **canto inferior direito** (`DisplayArea.WorkArea`, ~460x640).
- **Efeito vidro**: `DesktopAcrylicController` customizado (não Mica). Valores atuais em
  `MainWindow.TrySetGlassBackdrop()`: `TintColor=#181C(24,24,28)`, `TintOpacity=0.15`,
  `LuminosityOpacity=0.25`, `FallbackColor=(42,42,46)`. **Ajustar essas 3 opacidades muda o quão
  "vidro"/transparente fica** (o dono quer estilo TranslucentTB; mais transparente = baixar valores).
- **Título nativo** (`ExtendsContentIntoTitleBar` + `SetTitleBar`) com botões min/max/close do sistema;
  **maximizar desativado** (`OverlappedPresenter.IsMaximizable=false`), caption buttons transparentes.
- **Bandeja** (`H.NotifyIcon.WinUI`, criado em código em `SetupTray()`): ícone gerado por texto "O",
  menu Abrir/Sair, clique reabre. **Fechar e minimizar recolhem para a bandeja** (`AppWindow.Closing`
  cancela+esconde; `AppWindow.Changed` detecta minimize). "Sair" encerra de verdade (`_allowClose`).
- Aba única **GitHub Actions** (TabView) com cards de workflow runs + `StatusDot`
  (verde=success, vermelho=failure/timed_out/..., amarelo pulsante=in_progress/queued, cinza=outro).
  Clique no card abre o run no navegador. Carrega ao abrir (prefill `cli/cli`).

## 7. BACKLOG — o redesign pedido (ordem sugerida de execução)

> Tudo **nativo** (sem Community Toolkit se der; usar `Expander`, `ToggleSwitch`, `ComboBox`, `Slider`,
> `NavigationView`, `SelectorBar`, `InfoBar`). UI **linda e organizada estilo Windows**.

### 7.1 Shell de navegação (transformar MainWindow em shell)
- `NavigationView` (hambúrguer à esquerda, `PaneDisplayMode="Auto"`/`LeftCompact`, expansível) + `Frame`.
- Manter a **title bar nativa** por cima; esconder back/toggle duplicados se usar TitleBar control.
- Páginas (Frame): **Home**, **Sobre**, **Changelog**, e **Configurações** (usar o botão de Settings
  embutido do NavigationView — `IsSettingsVisible=true`).
- Mover a UI atual de Actions para `HomePage`.

### 7.2 Home repaginada
- Seletor **Tudo / Actions / PRs / Branches** com **`SelectorBar`** (nativo, WinUI 1.5+) no topo da Home
  — **não** é item de menu, fica na Home. "Tudo" mostra tudo junto; os outros filtram.
- Cards bonitos (reusar o padrão atual + `StatusDot`). Estado vazio/erro com `InfoBar`.
- Usar os repos configurados (ver 7.4). Enquanto não há config, manter input manual owner/repo.

### 7.3 Login GitHub — OAuth **Device Flow** (client_id já disponível)
**Client ID (público, pode embutir):** `Ov23liLv2MuCd5fzdkMJ`. Sem client_secret no device flow.
Scopes sugeridos: `repo read:org notifications`.

Adicionar ao **núcleo Rust** (usar `reqwest` com rustls, ou octocrab http; endpoints ficam em
`github.com`, não `api.github.com`):
1. `start_device_login(scopes: String) -> DeviceCode` → `POST https://github.com/login/device/code`
   com `Accept: application/json`, body `client_id`, `scope`. Retorna
   `DeviceCode { user_code, verification_uri, device_code, interval, expires_in }`.
2. `poll_device_login(device_code: String) -> DeviceLoginStatus` → `POST
   https://github.com/login/oauth/access_token` body `client_id`, `device_code`,
   `grant_type=urn:ietf:params:oauth:grant-type:device_code`. Mapear erros:
   `authorization_pending` → Pending; `slow_down` → SlowDown (aumentar intervalo +5s);
   `expired_token` → Expired; `access_denied` → Denied; sucesso → `Authorized(token)`.
3. Regerar bindings.

No **C#** (SettingsPage): botão "Entrar com GitHub" → chama `start_device_login`, mostra o `user_code`
(com botão copiar) e abre `verification_uri` via `Launcher.LaunchUriAsync`; faz polling num
`DispatcherTimer` a cada `interval`s até `Authorized`. Guardar o token no **Windows Credential Manager**
(`Windows.Security.Credentials.PasswordVault`) — nunca em texto puro. Exibir usuário logado via
`whoami()`.

### 7.4 Seleção de repositórios a monitorar
- Nova função no núcleo: `list_repositories() -> Vec<Repo>` → `GET /user/repos?per_page=100&sort=updated`
  (autenticado; incluir `full_name`). Regerar bindings.
- UI: lista com busca + checkboxes para o usuário escolher quais repos monitorar. Persistir a seleção
  (ver 7.7).

### 7.5 Eventos a monitorar (por repo ou global) — checkboxes
Baseado nos "reasons"/eventos do GitHub. Oferecer:
- **PR aberto**, **PR mergeado**, **PR fechado** (PullRequestEvent action opened/closed+merged)
- **Review solicitado** (`review_requested`), **Mencionado** (`mention`), **Menção de time** (`team_mention`)
- **CI/Actions** — falha/sucesso de workflow (`ci_activity` / WorkflowRunEvent)
- **Novos commits/push** (PushEvent), **Assigned/Author** (`assign`/`author`)
Guardar as escolhas na config.

### 7.6 Demais opções da tela de Configurações (estilo Windows, `Expander`/`SettingsCard`-like feito à mão)
- **Polling rate**: `Slider`/`ComboBox` (ex.: 30s / 1m / 5m / 15m).
- **Idioma**: pt-BR (padrão) + en (ver 7.8). `ComboBox` com troca em runtime.
- **Tema**: Claro/Escuro/Sistema (`ElementTheme` no root; lembrar que o backdrop segue o tema).
- **Iniciar com o Windows**: chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (valor
  `OctoWatch` = caminho do .exe). Toggle liga/desliga.
- **Encerrar o app de vez**: botão que faz o mesmo que "Sair" do tray (`_allowClose=true; _tray.Dispose(); Close()`).
- Seções **Sobre** (versão, links, licença) e **Changelog** (renderizar de um `CHANGELOG.md` embutido).

### 7.7 Persistência de configuração
- JSON em `ApplicationData.Current.LocalFolder` (app desempacotado: usar
  `Windows.Storage.ApplicationData` funciona com WindowsAppSDK, ou `Environment.SpecialFolder.LocalApplicationData`).
- Modelo: repos selecionados, eventos por repo, polling rate, idioma, tema, startup on/off.

### 7.8 i18n (pt-BR padrão + en)
- Recursos `.resw` + `x:Uid` + `ResourceLoader` (nativo). Estrutura `Strings/pt-BR/Resources.resw`,
  `Strings/en/Resources.resw`. `DefaultLanguage` no csproj.
- Para troca de idioma **em runtime sem restart**, considerar o pacote `WinUI3Localizer` (senão,
  recarregar a página/Frame ao trocar).

### 7.9 (Depois) Notificações + motor de polling no núcleo
- `monitor.rs` no núcleo: loop de polling (respeitando o polling rate) + `trait MonitorObserver`
  (callback) disparando eventos de mudança. UniFFI suporta callback interfaces + async.
- No Windows: `AppNotification` (toast WinRT do WindowsAppSDK) quando um evento selecionado ocorre.
- Esse motor é reaproveitado por Linux/macOS.

## 8. Referências (pesquisadas)
- Materiais/vidro: Acrylic = blur do que está atrás da janela (vidro); Mica = tinge o wallpaper (mais
  opaco). Glass real e ajustável = `DesktopAcrylicController` (TintColor/TintOpacity/LuminosityOpacity/
  FallbackColor) + `ICompositionSupportsSystemBackdrop` via `this.As<>()`. Docs: MS Learn "System
  backdrops (Mica/Acrylic)" e "In-app acrylic".
- `NavigationView` (MS Learn) — hambúrguer + Settings embutido.
- i18n: MS Learn "Localize your WinUI 3 app" (resw/x:Uid); pacote `WinUI3Localizer` p/ runtime.
- GitHub Device Flow: docs.github.com "Authorizing OAuth apps → Device flow". Notification reasons:
  `review_requested, mention, team_mention, ci_activity, author, assign, subscribed, state_change, ...`.

## 9. Cross-platform (mais adiante)
- **Linux**: `linux/` (Rust + gtk4-rs + libadwaita) linkando `octowatch-core` direto. Precisa
  `libgtk-4-dev libadwaita-1-dev`. Roda/testa em máquina Linux.
- **macOS**: `macos/` (Swift + SwiftUI + `MenuBarExtra`) usando os bindings Swift já gerados. Build no
  Xcode/`swift build` num Mac (não compila no Windows).

## 10. Convenções
- Commits pequenos e descritivos; assinar `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Não versionar bindings gerados nem a DLL (já no `.gitignore`).
- `csharpier` formata o C# (há hook); rodar `dotnet build` sempre para validar.
