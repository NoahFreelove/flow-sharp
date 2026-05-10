# Editor Setup for Flow Language Server

Flow ships an LSP server (`flow-lsp`) that speaks plain LSP 3.17 over stdio.
Any editor with an LSP client can use it.

For VSCode users, the flagship artifact is the **Flow Language** extension
([Marketplace](https://marketplace.visualstudio.com) /
[OpenVSX](https://open-vsx.org) — links go live after the first release tag).
The extension bundles `flow-lsp` and its stdlib and activates on `.flow` files
automatically; no manual configuration required.

For everyone else, use one of the snippets in this directory and point your
editor at a `flow-lsp` binary on your `PATH`.

## Getting the binary

**Option A — Download a release tarball (recommended once releases are tagged):**
Pre-built per-platform binaries live at
https://github.com/noah-freelove/flow-sharp/releases. Extract into a directory
on your `PATH`, e.g. `~/.local/bin/`.

**Option B — Build from source:**
Requires the .NET 10 SDK. From the `flow-sharp` repo root:

```bash
dotnet publish flow-lsp/flow-lsp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -o ~/.local/bin/flow-lsp-dir
# then symlink or alias so `flow-lsp` is on PATH:
ln -s ~/.local/bin/flow-lsp-dir/flow-lsp ~/.local/bin/flow-lsp
```

Replace `linux-x64` with `win-x64`, `osx-x64`, or `osx-arm64` as appropriate.

**Critical:** the binary needs the six shipped stdlib `.flow` files
(`std.flow`, `audio.flow`, `collections.flow`, `bars.flow`, `notation.flow`,
`composition.flow`) in the same directory. `dotnet publish` already copies
them via the csproj's `<CopyToOutputDirectory>` settings — do not move the
binary away from those stdlib files or `use "@audio"` and friends will fail
at go-to-definition and import-resolution time.

## Per-editor configurations

| Editor  | Guide                             | Raw snippet                                          |
|---------|-----------------------------------|------------------------------------------------------|
| Neovim  | [neovim.md](./neovim.md)          | [nvim-lspconfig.lua](./nvim-lspconfig.lua)           |
| Helix   | [helix.md](./helix.md)            | [helix-languages.toml](./helix-languages.toml)       |
| Emacs / Zed / Cursor / Windsurf / others | [generic-lsp.md](./generic-lsp.md) | — |

All snippets assume `flow-lsp` is on `PATH`. Open a `.flow` file and the LSP
attaches automatically.

## Not your editor?

The server speaks standard LSP 3.17 over stdio. See the LSP spec at
https://microsoft.github.io/language-server-protocol/ for client
implementation guidance, or consult your editor's LSP-client documentation.

## Manual smoke checklist

After installing the extension or wiring up a non-VSCode editor, run through
[`manual-smoke.md`](./manual-smoke.md) to confirm syntax highlighting,
completion, hover, go-to-definition, signature help, and snippet expansion
all work end-to-end.

## Troubleshooting

- **"flow-lsp: command not found"** — binary is not on `PATH`. Check with
  `which flow-lsp` (POSIX) or `where.exe flow-lsp` (Windows).
- **"Flow LSP binary not found at ..."** (VSCode) — the bundled per-platform
  binary is missing. Use the `flow.server.path` setting to point at your own
  build, or reinstall the VSIX for your OS/architecture.
- **"Definition not found" on `use "@audio"`** — stdlib `.flow` files are not
  shipping next to the binary. Re-run `dotnet publish` (the csproj copies
  them automatically) or copy them manually.
- **No syntax highlighting** (non-VSCode) — most editors render LSP semantic
  tokens, but some also need a syntax/grammar file. Flow's TextMate grammar
  is VSCode-specific for now; non-VSCode editors receive semantic tokens
  from the LSP server and style them via the active theme.

## Contributing

Adding editor snippets is welcome. Submit a PR with
`docs/editor-setup/<editor>.md` (or a raw `.lua`/`.toml`/`.el` snippet as
appropriate), following the existing Neovim/Helix examples.
