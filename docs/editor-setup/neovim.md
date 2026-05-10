# Neovim (nvim-lspconfig) setup for Flow

Flow ships an LSP server, `flow-lsp`, that speaks plain LSP over stdio. Any
Neovim installation with [`nvim-lspconfig`](https://github.com/neovim/nvim-lspconfig)
can drive it.

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
# Then add ~/.local/bin/flow-lsp-dir to PATH, or symlink the binary:
ln -s ~/.local/bin/flow-lsp-dir/flow-lsp ~/.local/bin/flow-lsp
```

Replace `linux-x64` with `win-x64`, `osx-x64`, or `osx-arm64` as appropriate.

**Critical:** the binary needs the six shipped stdlib `.flow` files
(`std.flow`, `audio.flow`, `collections.flow`, `bars.flow`, `notation.flow`,
`composition.flow`) next to it. `dotnet publish` copies them automatically
via the csproj's `<CopyToOutputDirectory>` settings. Do not move the binary
away from its directory without taking those files along.

Verify:

```bash
which flow-lsp     # should print a path
flow-lsp --help    # should respond (though flow-lsp is an LSP server, not a CLI;
                   # if no help is shown it is still likely working over stdio)
```

## Configuration

Copy [`nvim-lspconfig.lua`](./nvim-lspconfig.lua) to
`~/.config/nvim/lua/plugins/flow-lsp.lua` (or adapt to your plugin manager's
layout). The snippet registers Flow as a filetype, points `nvim-lspconfig` at
`flow-lsp` on `PATH`, and wires a `root_dir` based on `.git` / `.flowproject`.

Open any `.flow` file — the LSP attaches automatically. You should see
diagnostics, completion, hover, go-to-definition, and signature help.

## Troubleshooting

- **"flow-lsp: command not found"** — the binary is not on your `PATH`.
  Check with `which flow-lsp`; either install via Option A/B above or
  point `cmd = { '/absolute/path/to/flow-lsp' }` in the config.
- **"Definition not found" when following a `use "@audio"` import** — stdlib
  `.flow` files are not shipping beside the binary. Re-run `dotnet publish`
  (the csproj handles this), or copy them manually next to the binary.
- **Completion seems sparse** — make sure your Neovim LSP client is sending
  `completionItem.snippetSupport = true`; the Flow server emits snippet
  completions for block constructs (`tempo`, `key`, `timesig`, etc.).

## See also

- [Editor-setup overview](./README.md)
- [Helix setup](./helix.md)
