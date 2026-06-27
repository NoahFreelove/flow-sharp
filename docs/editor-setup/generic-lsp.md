# Generic LSP setup for Flow (Emacs, Zed, Cursor, Windsurf, and others)

The Flow language server is a standard LSP 3.17 server over stdio, spawned
either via `flow lsp` (preferred — shipped by `flow install`) or via the
standalone `flow-lsp` binary (built directly from `flow-lsp/`). Any editor
with an LSP client can drive it. This page gives generic guidance for editors
that do not have a dedicated setup page here.

For the authoritative list of LSP capabilities (what works today, what does
not), see **[FEATURES.md § LSP](../../FEATURES.md#lsp-flow-lsp)**. In short:
diagnostics, semantic tokens, completion, hover, go-to-definition, and
signature help all work. Rename, find-references, code actions, and document
symbols are not yet implemented — do not wire keybindings to them.

## Prerequisite: the binary on PATH

See the [editor-setup overview](./README.md) for install options (`flow
install`, release tarball, or `dotnet publish` from source). If you went the
Option C `dotnet publish` route, make sure `flow-lsp`'s six sibling stdlib
`.flow` files live in the same directory as the binary. Option A / B handle
this automatically.

## Emacs

### Using `lsp-mode`

Add to your Emacs init:

```elisp
(use-package lsp-mode
  :hook (flow-mode . lsp-deferred)
  :config
  (add-to-list 'lsp-language-id-configuration '(flow-mode . "flow"))
  (lsp-register-client
    (make-lsp-client
      ;; Option A / B (preferred): the `flow lsp` subcommand.
      ;; For Option C (standalone build), use (lsp-stdio-connection "flow-lsp").
      :new-connection (lsp-stdio-connection '("flow" "lsp"))
      :major-modes '(flow-mode)
      :server-id 'flow-lsp)))

(define-derived-mode flow-mode prog-mode "Flow"
  "Major mode for Flow .flow files.")
(add-to-list 'auto-mode-alist '("\\.flow\\'" . flow-mode))
```

### Using `eglot` (Emacs 29+)

```elisp
(define-derived-mode flow-mode prog-mode "Flow"
  "Major mode for Flow .flow files.")
(add-to-list 'auto-mode-alist '("\\.flow\\'" . flow-mode))

(with-eval-after-load 'eglot
  ;; Option A / B (preferred): the `flow lsp` subcommand.
  ;; For Option C (standalone build), use ("flow-lsp") instead.
  (add-to-list 'eglot-server-programs '(flow-mode . ("flow" "lsp"))))

(add-hook 'flow-mode-hook 'eglot-ensure)
```

## Zed

Zed does not currently expose a raw LSP config surface for arbitrary languages
without a full extension. If you want to use `flow-lsp` in Zed, you will need
a Zed extension that declares the language and points at the binary. A minimal
extension scaffold (`extension.toml` + `languages/flow/config.toml`) is a
reasonable starting point. Upstream Zed extension docs are at
https://zed.dev/docs/extensions.

## Cursor / Windsurf (VSCode-compatible forks)

Install the Flow Language extension from [OpenVSX](https://open-vsx.org) once
the tag-triggered CI workflow publishes it. These forks read from OpenVSX, so
the same VSIX that ships to stock VSCode marketplace users is available here
via the same UI.

If OpenVSX is unreachable, you can also install the `.vsix` file directly:
download the release asset from
https://github.com/NoahFreelove/flow-sharp/releases and use the editor's
"Install from VSIX" command.

## JetBrains IDEs (IntelliJ, PyCharm, Rider, GoLand, etc.)

A stretch-deliverable plugin (`flow-jetbrains`) ships LSP-only support via
[LSP4IJ](https://github.com/redhat-developer/lsp4ij) on IntelliJ Platform
2024.2+. Install the `.zip` manually from a release. The plugin spawns
`flow lsp` via `GeneralCommandLine` and falls back to the `FLOW_LSP_PATH`
environment variable when `flow` is not on `PATH`. See `flow-jetbrains/README.md`
in the repo for details.

## Sublime Text, Kate, Nova, Vim-with-coc.nvim, etc.

Each editor has its own LSP client configuration. The shared contract is:

| Field           | Value                                                              |
|-----------------|--------------------------------------------------------------------|
| Language id     | `flow`                                                             |
| Scope / syntax  | `source.flow`                                                      |
| File extensions | `.flow`                                                            |
| Command         | `flow lsp` (preferred) — or standalone `flow-lsp`                  |
| Transport       | stdio                                                              |
| Root markers    | `.git`, `.flowproject`                                             |

Plug those values into your editor's LSP client and the server speaks plain
LSP 3.17 from there.

## See also

- [Editor-setup overview](./README.md)
- [Neovim setup](./neovim.md)
- [Helix setup](./helix.md)
- [LSP specification](https://microsoft.github.io/language-server-protocol/)
