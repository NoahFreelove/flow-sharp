# Helix setup for Flow

Helix has a built-in LSP client — no plugin required. Point it at the `flow-lsp`
binary and Helix handles the rest.

## Prerequisite: get the `flow-lsp` binary

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
ln -s ~/.local/bin/flow-lsp-dir/flow-lsp ~/.local/bin/flow-lsp
```

Replace `linux-x64` with `win-x64`, `osx-x64`, or `osx-arm64` as appropriate.

**Critical:** the binary needs the six shipped stdlib `.flow` files
(`std.flow`, `audio.flow`, `collections.flow`, `bars.flow`, `notation.flow`,
`composition.flow`) next to it. `dotnet publish` copies them automatically via
the csproj's `<CopyToOutputDirectory>` settings. Do not move the binary alone.

## Configuration

Append the contents of [`helix-languages.toml`](./helix-languages.toml) to
your `~/.config/helix/languages.toml`. The snippet declares the `flow`
language, maps `.flow` as its file extension, and registers `flow-lsp` as
the language server.

```toml
[[language]]
name = "flow"
scope = "source.flow"
file-types = ["flow"]
roots = [".git", ".flowproject"]
comment-token = "//"
indent = { tab-width = 4, unit = "    " }
language-servers = ["flow-lsp"]

[language-server.flow-lsp]
command = "flow-lsp"
```

Reload Helix (`:config-reload`) and open any `.flow` file. You should see
diagnostics, completion, hover, go-to-definition, and signature help in the
status line.

## Troubleshooting

- **"Server failed to start: No such file or directory"** — the `flow-lsp`
  binary is not on `PATH`. Either install per Option A/B above or set
  `command = "/absolute/path/to/flow-lsp"` in the `[language-server.flow-lsp]`
  block.
- **"Definition not found" when following a `use "@audio"` import** — stdlib
  `.flow` files are not shipping beside the binary. Re-run `dotnet publish`
  (the csproj handles this) or copy them manually next to the binary.
- **Syntax highlighting looks wrong** — Helix's tree-sitter-based highlighting
  does not ship a Flow grammar. The LSP provides semantic tokens, and Helix
  respects them, but scope colors may differ from the VSCode extension's
  TextMate grammar rendering.

## See also

- [Editor-setup overview](./README.md)
- [Neovim setup](./neovim.md)
