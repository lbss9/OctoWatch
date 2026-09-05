# Changelog

Todas as mudanças notáveis do OctoWatch estão documentadas aqui. O formato segue o
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/).

## 0.1.0 — 2026-08-27

### Adicionado
- Núcleo Rust compartilhado: cliente do GitHub (Actions, pull requests, branches,
  commits) exposto às UIs nativas via UniFFI.
- Login no GitHub por OAuth device flow e listagem dos seus repositórios.
- App Windows (WinUI 3): flyout no canto inferior direito com fundo de vidro acrílico,
  ícone na bandeja (abrir / sair) e um menu de navegação.
- Feed na Home com filtro **Tudo / Actions / PRs / Branches** e bolinhas de status por
  item (verde = sucesso, vermelho = falha, amarelo pulsante = em execução).
- Configurações: conta, repositórios, eventos monitorados, intervalo de polling, idioma,
  tema e iniciar com o Windows.
- Transparência da janela ajustável (slider de opacidade + toggle de acrílico).

### Segurança
- Links externos são restritos aos esquemas `http`/`https`.
