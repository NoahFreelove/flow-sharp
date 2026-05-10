# Generic LSP setup for Flow (Emacs, Zed, Cursor, Windsurf, and others)

`flow-lsp` is a standard LSP 3.17 server over stdio. Any editor with an LSP
client can drive it. This page gives generic guidance for editors that do not
have a dedicated setup page here.

## Prerequisite: get the `flow-lsp` binary

See the [editor-setup overview](./README.md) for install options (release
tarball or `dotnet publish` from source). Make sure `flow-lsp` is on `PATH`
and its six sibling stdlib `.flow` files live in the same directory as the
binary.

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
      :new-connection (lsp-stdio-connection "flow-lsp")
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
  (add-to-list 'eglot-server-programs '(flow-mode . ("flow-lsp"))))

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
https://github.com/noah-freelove/flow-sharp/releases and use the editor's
"Install from VSIX" command.

## Sublime Text, Kate, Nova, Vim-with-coc.nvim, etc.

Each editor has its own LSP client configuration. The shared contract is:

| Field           | Value                                      |
|-----------------|--------------------------------------------|
| Language id     | `flow`                                     |
| Scope / syntax  | `source.flow`                              |
| File extensions | `.flow`                                    |
| Binary          | `flow-lsp` (on `PATH`)                     |
| Transport       | stdio                                      |
| Root markers    | `.git`, `.flowproject`                     |

Plug those values into your editor's LSP client and the server speaks plain
LSP 3.17 from there.

## See also

- [Editor-setup overview](./README.md)
- [Neovim setup](./neovim.md)
- [Helix setup](./helix.md)
- [LSP specification](https://microsoft.github.io/language-server-protocol/)
