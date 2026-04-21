# Flow Language for VSCode

Syntax highlighting, diagnostics, and IntelliSense for the
[Flow](https://github.com/noah-freelove/flow-sharp) music-production
language — right inside VSCode (and Cursor / VSCodium / Windsurf via
OpenVSX).

Flow is an interpreted, statically-typed language designed for music
production. It features a flow operator (`->`), music-specific types
(`Note`, `Chord`, `Song`), inline note-stream syntax (`| C4 D4 E4 |`),
musical context blocks (`tempo`, `key`, `timesig`, `swing`), and a full
audio pipeline. This extension ships a bundled LSP server so composing
in VSCode feels as live as composing in any mainstream editor.

## Features

- **Syntax highlighting** — TextMate grammar for baseline coloring plus
  LSP semantic tokens for lexer-precise scopes (chord vs note, roman
  numerals inside `key { }`, musical context keywords).
- **Live diagnostics** — every error the Flow parser and bind phase
  produce, surfaced as red squiggles inside ~200ms of each keystroke.
- **Completion** — built-ins, stdlib modules (`use "@..."`), language
  keywords, user-declared procs and variables, imported names, and
  context-aware roman numerals inside `key { }` note streams.
- **Hover** — function signatures plus short doc blurbs sourced from
  `BuiltInDocs`. User-declared symbols show their declared type.
- **Go-to-definition** — jump to user procs/variables, and to the stdlib
  `.flow` file for `use "@..."` imports.
- **Signature help** — active-parameter highlighting while typing inside
  function calls.
- **Snippets** — block templates for `tempo`, `key`, `timesig`, `proc`,
  and `section`.

## Installation

Once v1.0.0 is tagged, install from either marketplace:

- **VSCode Marketplace** — search "Flow Language" in the Extensions
  panel (Ctrl+Shift+X), or visit the listing at
  https://marketplace.visualstudio.com (link pending first release).
- **OpenVSX** (Cursor / VSCodium / Windsurf / Theia) — search "Flow
  Language" in the Extensions panel, or visit
  https://open-vsx.org (link pending first release).

The extension ships per-platform self-contained `flow-lsp` binaries for
Linux, Windows, macOS x64, and macOS arm64. **No .NET SDK is required
at runtime** — the right binary is selected automatically at activation
based on your OS and architecture.

## Requirements

- VSCode `>= 1.85.0` (or any compatible fork).
- No .NET SDK, no PulseAudio, no audio stack — the LSP is parse-only.

## Configuration

| Setting             | Type    | Default  | Purpose                                                                 |
|---------------------|---------|----------|-------------------------------------------------------------------------|
| `flow.server.path`  | string  | `""`     | Optional absolute path to a `flow-lsp` binary. Overrides the bundled server. |
| `flow.trace.server` | string  | `"off"`  | LSP trace verbosity: `off`, `messages`, or `verbose`. Logs to the `Flow LSP Trace` output channel. |

Open VSCode settings (Ctrl+,) and search for "flow" to edit these.

### Using a custom `flow-lsp` binary

Set `flow.server.path` to an absolute binary path when you want to
test a local `dotnet publish` build instead of the bundled one.
Remember that `flow-lsp` requires the six stdlib `.flow` files
(`std`, `audio`, `collections`, `bars`, `notation`, `composition`) in
the same directory. `dotnet publish` handles this automatically; moving
the binary by itself will break `use "@audio"` and friends.

## Troubleshooting

- **"Flow LSP binary not found at ..."** — the bundled per-platform
  binary is missing. Check the `Flow LSP Trace` output channel, then
  either reinstall the VSIX for your platform or set `flow.server.path`
  to a local build.
- **No diagnostics / completion** — confirm the status bar shows
  "Flow Language Server" attached. Run `Developer: Reload Window` if
  activation got stuck.
- **Chord vs note coloring looks wrong** — report the specific case;
  the TM grammar favors chord patterns before notes, and semantic
  tokens refine further, but edge cases may surface.

## Non-VSCode editors

For Neovim, Helix, Emacs, Zed, Cursor (direct), Windsurf, and other
LSP-capable editors, see
[`docs/editor-setup/README.md`](https://github.com/noah-freelove/flow-sharp/blob/master/docs/editor-setup/README.md)
in the Flow repository. The server is standard LSP 3.17 over stdio, so
every major editor with an LSP client can drive it.

## Links

- **Repository:** https://github.com/noah-freelove/flow-sharp
- **Issues:** https://github.com/noah-freelove/flow-sharp/issues
- **Language reference:** see `CLAUDE.md` and `wiki/` inside the repo.

## Publisher

Publisher ID: `flow-lang` (placeholder — the real marketplace publisher
identity is confirmed before the first tag push; see
`.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md`).

## License

See the Flow repository root for the license.
