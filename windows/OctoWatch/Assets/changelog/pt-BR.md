# Changelog

Todas as mudanças notáveis do OctoWatch são documentadas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e o projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Não publicado]

## [0.1.0] - 2026-08-27

### Adicionado

- App Windows: feed de GitHub Actions com bolinhas de status ao vivo, fundo
  acrílico/vidro, bandeja com abrir/sair, login GitHub por Device Flow e seleção
  de eventos por repositório.
- Núcleo Rust compartilhado (cliente GitHub) exposto à UI via UniFFI.
- Flyout de filtro para Actions / PRs / Branches, toasts nativos do Windows em
  itens novos do feed, e atualizações Velopack a partir de GitHub Releases.

### Alterado

- Transparência da janela ajustável (opacidade estilo Windows Terminal + toggle de acrílico).

### Segurança

- Links externos ficam restritos a http/https.
