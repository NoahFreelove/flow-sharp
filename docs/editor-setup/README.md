# Editor Setup for Flow Language Server

Flow ships an LSP server (`flow-lsp`) that speaks plain LSP 3.17 over stdio.
Any editor with an LSP client can use it.

For VSCode users, the flagship artifact is the **Flow Language** extension
([Marketplace](https://marketplace.visualstudio.com) /
[OpenVSX](https://open-vsx.org) — links go live after the first release tag).
The extension bundles `flow-lsp` and its stdlib and activates on `.flow` files
automatically; no manual configuration required.

For everyone else, use one of the snippets in this directory and point your
editor at the LSP server. Two equivalent entry points:

- `flow lsp` — subcommand on the unified `flow` CLI (preferred; shipped by
  `scripts/install.sh`).
- `flow-lsp` — standalone binary (built directly from `flow-lsp/`; also bundled
  per-platform inside the VSCode extension at `server/{platform}-{arch}/`).

Both speak identical LSP 3.17 over stdio — pick whichever your editor's LSP
client wires up more cleanly.

## What the server provides

See **[FEATURES.md § LSP (`flow-lsp`)](../../FEATURES.md#lsp-flow-lsp)** for the
authoritative capability table. Short summary:

- **Shipped:** publish diagnostics (6 analyzer sources merged), semantic tokens
  (full), context-aware completion, hover (markdown), go-to-definition (user
  symbols + stdlib imports), signature help.
- **Not yet:** semantic tokens delta, find references, rename, code actions,
  document/workspace symbols, inlay hints, formatter.

Do not wire keybindings to capabilities in the second list — they will simply
return null and your editor will show "no references found" / "rename not
supported".

## Getting the binary

**Option A — `flow install` (recommended once a release tag is cut):**

```bash
# Per-user install: ~/.local/share/flow/flow-v<VERSION>/ + ~/.local/bin/flow symlink
curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash

# Or system-wide (needs sudo): /usr/local/share/flow/... + /usr/local/bin/flow
curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash -s -- --system
```

This drops a `flow` binary on your `PATH` that handles `flow lsp` plus every
other CLI subcommand. The stdlib `.flow` files ship inside the versioned
install directory next to the binary — no further setup required.

**Option B — Download a release tarball directly:**
Pre-built per-platform binaries live at
https://github.com/NoahFreelove/flow-sharp/releases. Extract the archive and
either run `bin/flow lsp` directly or symlink `bin/flow` onto your `PATH`.

**Option C — Build from source:**
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

**Critical:** the standalone `flow-lsp` binary needs the six shipped stdlib
`.flow` files (`std.flow`, `audio.flow`, `collections.flow`, `bars.flow`,
`notation.flow`, `composition.flow`) in the same directory. `dotnet publish`
already copies them via the csproj's `<CopyToOutputDirectory>` settings — do
not move the binary away from those stdlib files or `use "@audio"` and friends
will fail at go-to-definition and import-resolution time. (Option A / Option B
handle this automatically.)

## Per-editor configurations

| Editor  | Guide                             | Raw snippet                                          |
|---------|-----------------------------------|------------------------------------------------------|
| Neovim  | [neovim.md](./neovim.md)          | [nvim-lspconfig.lua](./nvim-lspconfig.lua)           |
| Helix   | [helix.md](./helix.md)            | [helix-languages.toml](./helix-languages.toml)       |
| Emacs / Zed / Cursor / Windsurf / others | [generic-lsp.md](./generic-lsp.md) | — |

All snippets assume either `flow` (via the `flow lsp` subcommand) or
`flow-lsp` is on `PATH`. Open a `.flow` file and the LSP attaches
automatically.

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

- **"flow-lsp: command not found" / "flow: command not found"** — binary is
  not on `PATH`. Check with `which flow` / `which flow-lsp` (POSIX) or
  `where.exe flow` (Windows). If you used `scripts/install.sh` in per-user
  mode, ensure `~/.local/bin` is on `PATH`.
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
