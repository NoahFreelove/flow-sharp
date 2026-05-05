# Flow Language

A Vibecoded music production language.


Called flow-sharp because original implementation was in ansi c and unmaintainable.

Wanted to make it maintainable so I vibecoded the language translation from C to C#.

Then I realized I could have many more features if I kept vibecoding. So I did.

This is the product of it.

Flow-lang is probably not intuitive, but its meant for programmer-musicians.

Its fairly extensible, the builtin instruments don't sound too good. Very old MIDI sounding, but has a nice charm.

## Editor support

Flow ships with a **Language Server (`flow-lsp`)** and a **VSCode extension**
that together provide live diagnostics, completion, hover, go-to-definition,
signature help, and context-aware roman-numeral completion inside `key { }`
note streams.

### VSCode / Cursor / VSCodium / Windsurf

Install the **Flow Language** extension from the VSCode Marketplace or
OpenVSX (listings go live after the first release tag). The extension
bundles per-platform `flow-lsp` binaries for Linux, Windows, macOS x64,
and macOS arm64 — no .NET SDK required at runtime.

### Neovim, Helix, Emacs, Zed, and other LSP editors

The `flow-lsp` server speaks plain LSP 3.17 over stdio, so any editor
with an LSP client can drive it. See
[`docs/editor-setup/`](./docs/editor-setup/README.md) for per-editor
config snippets (Neovim `nvim-lspconfig`, Helix `languages.toml`,
Emacs `lsp-mode`/`eglot`) and binary install guidance.
