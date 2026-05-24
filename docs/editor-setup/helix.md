# Helix setup for Flow

Helix has a built-in LSP client — no plugin required. Point it at `flow lsp`
(or the standalone `flow-lsp` binary) and Helix handles the rest.

For the authoritative list of LSP capabilities (what works today, what does
not), see **[FEATURES.md § LSP](../../FEATURES.md#lsp-flow-lsp)**. In short:
diagnostics, semantic tokens, completion, hover, go-to-definition, and
signature help all work. Rename, find-references, code actions, and document
symbols are not yet implemented.

## Prerequisite: the `flow` (or `flow-lsp`) binary on PATH

**Option A — `flow install` (recommended once a release tag is cut):**

```bash
curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash
```

Drops `flow` onto `~/.local/bin/`. The Helix config below uses `flow lsp` as
the spawn command.

**Option B — Download a release tarball directly:**
Pre-built per-platform archives live at
https://github.com/NoahFreelove/flow-sharp/releases. Extract and ensure
`bin/flow` lands on `PATH`.

**Option C — Build from source:**
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
If you go Option C, swap `command = "flow"` + `args = ["lsp"]` in the config
below for plain `command = "flow-lsp"`.

If you go Option C, the standalone `flow-lsp` binary needs the six shipped
stdlib `.flow` files (`std.flow`, `audio.flow`, `collections.flow`,
`bars.flow`, `notation.flow`, `composition.flow`) next to it. `dotnet
publish` copies them automatically via the csproj's `<CopyToOutputDirectory>`
settings. Do not move the binary alone. (Options A / B handle this
automatically.)

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
# Option A / B: use the `flow lsp` subcommand shipped by `flow install`.
command = "flow"
args = ["lsp"]
# Option C alternative (standalone binary): comment the two lines above and
# uncomment:
# command = "flow-lsp"
```

Reload Helix (`:config-reload`) and open any `.flow` file. You should see
diagnostics, completion, hover, go-to-definition, and signature help in the
status line.

## Troubleshooting

- **"Server failed to start: No such file or directory"** — neither `flow`
  nor `flow-lsp` is on `PATH`. Either install per Option A/B/C above or set
  an absolute path: `command = "/absolute/path/to/flow"` (keep `args =
  ["lsp"]`) or `command = "/absolute/path/to/flow-lsp"` (drop `args`).
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
