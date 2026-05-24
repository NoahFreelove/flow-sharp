# Neovim (nvim-lspconfig) setup for Flow

Flow ships an LSP server reachable via the `flow lsp` subcommand (or the
standalone `flow-lsp` binary). Any Neovim installation with
[`nvim-lspconfig`](https://github.com/neovim/nvim-lspconfig) can drive it.

For the authoritative list of LSP capabilities (what works today, what does
not), see **[FEATURES.md § LSP](../../FEATURES.md#lsp-flow-lsp)**. In short:
diagnostics, semantic tokens, completion, hover, go-to-definition, and
signature help all work. Rename, find-references, code actions, and document
symbols are not yet implemented — `vim.lsp.buf.rename` / `references` /
`document_symbol` will return nothing.

## Prerequisite: the `flow` (or `flow-lsp`) binary on PATH

**Option A — `flow install` (recommended once a release tag is cut):**

```bash
curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash
```

Drops `flow` onto `~/.local/bin/`. The nvim config below uses `flow lsp` as
the spawn command.

**Option B — Download a release tarball directly:**
Pre-built per-platform archives live at
https://github.com/NoahFreelove/flow-sharp/releases. Extract and ensure
`bin/flow` lands on `PATH` (e.g. via symlink into `~/.local/bin/`).

**Option C — Build from source:**
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

If you go Option C, the standalone `flow-lsp` binary needs the six shipped
stdlib `.flow` files (`std.flow`, `audio.flow`, `collections.flow`,
`bars.flow`, `notation.flow`, `composition.flow`) next to it. `dotnet
publish` copies them automatically via the csproj's `<CopyToOutputDirectory>`
settings. Do not move the binary away from its directory without taking
those files along. (Options A / B handle this automatically.)

Verify:

```bash
which flow         # Option A/B: should print a path
flow lsp --help    # the lsp subcommand itself has no flags, but the
                   # outer `flow --help` will list it
# OR
which flow-lsp     # Option C: should print a path
```

## Configuration

Copy [`nvim-lspconfig.lua`](./nvim-lspconfig.lua) to
`~/.config/nvim/lua/plugins/flow-lsp.lua` (or adapt to your plugin manager's
layout). The snippet registers Flow as a filetype, points `nvim-lspconfig` at
`flow lsp` (Option A/B) — swap to `{ 'flow-lsp' }` in `cmd` if you went
Option C — and wires a `root_dir` based on `.git` / `.flowproject`.

Open any `.flow` file — the LSP attaches automatically. You should see
diagnostics, completion, hover, go-to-definition, and signature help.

## Troubleshooting

- **"flow: command not found" / "flow-lsp: command not found"** — the
  binary is not on your `PATH`. Check with `which flow` or `which flow-lsp`;
  either install via Option A/B/C above or point
  `cmd = { '/absolute/path/to/flow', 'lsp' }` (or
  `cmd = { '/absolute/path/to/flow-lsp' }`) in the config.
- **"Definition not found" when following a `use "@audio"` import** — stdlib
  `.flow` files are not shipping beside the binary. Re-run `dotnet publish`
  (the csproj handles this), or copy them manually next to the binary.
- **Completion seems sparse** — make sure your Neovim LSP client is sending
  `completionItem.snippetSupport = true`; the Flow server emits snippet
  completions for block constructs (`tempo`, `key`, `timesig`, etc.).

## See also

- [Editor-setup overview](./README.md)
- [Helix setup](./helix.md)
