# CLI and Tooling

Flow ships as a single `flow` binary with 14 verbs, an interactive REPL, a Language Server (LSP 3.17), and editor integrations. This page is the tour of everything around the language itself.

## The `flow` CLI

Run `flow <verb> ...`. The verbs:

| Verb | What it does |
|------|--------------|
| `flow run <script>` | Run a `.flow` script. `--device`, `-v` / `--verbose` |
| `flow eval <code>` | Evaluate a one-liner (`flow eval 'Int x = 5; (print (str x))'`) |
| `flow repl` | Start the interactive REPL (auto-imports `@std`, `@audio`, `@collections`) |
| `flow watch <script>` | Live-reload session — bar-quantized swap + 64-sample crossfade (see [Live Coding](Live-Coding.md)) |
| `flow play <script>` | Run a script that contains a `(play ...)` call |
| `flow render <script> -o out.wav` | Run a script that contains a `(writeWav ...)` call |
| `flow flow2midi <script> -o out.mid` | Run a script that contains a `(writeMidi ...)` call |
| `flow midi2flow <input.mid> [-o out.flow]` | Convert a MIDI file to Flow source |
| `flow check <script>` | Validate a script (parses AND executes it) |
| `flow new <name> [--dir <path>]` | Scaffold a new Flow project from an embedded template |
| `flow lsp` | Start the Flow Language Server over stdio |
| `flow test [path]` | Run `test_*.flow` files via the pure-Flow test framework |
| `flow doc [--out <dir>] [--format html\|md\|both]` | Generate static reference docs from `BuiltInDocs` + `///` comments |
| `flow version` | Print the version (`flow 1.5.0+<commit>`) |

A few honest notes:

- **`flow render` / `flow flow2midi`** require the script to already contain the `writeWav` / `writeMidi` call — the `-o` flag is currently informational (it does not inject or redirect the output path). Auto-injection is backlogged.
- **`flow check`** parses *and* executes the script today; a true parse-only mode is on the backlog. It still exits non-zero on any error, so it works as a pre-commit gate.
- **`flow play`** does not auto-inject a `(play ...)` — the script must call it.
- **`flow midi2flow`** takes `--sustain` / `--no-sustain`, `--sfz` / `--no-sfz`, and `--dump` in addition to `-o`. See [Playback and Export](Playback-and-Export.md) for the round-trip workflow.

The legacy `flow-interpreter` binary is still maintained for back-compat (`--watch`, `-e`, `--stdin`, `--device`, `--verbose`).

## The REPL

`flow repl` gives you an interactive session that auto-imports `@std`, `@audio`, and `@collections`. In a **real terminal** it uses PrettyPrompt for rich editing:

- **Tab completion**, backed by the same completion engine as the LSP.
- **Ctrl+R** reverse history search. History persists to `~/.config/flow/history` (10k-entry cap, `0600` mode).
- **Multi-line continuation** via paren / bracket nesting depth.

When stdin is piped or redirected (CI, scripts), it falls back to a plain line reader — no TTY required.

> Syntax highlighting is not wired up yet — PrettyPrompt is in place but the highlighting callback is not connected.

### Meta-Commands

| Command | Effect |
|---------|--------|
| `:help` | List commands |
| `:help <fn>` | Look up a builtin — prints its signature and an example from `BuiltInDocs` |
| `:clear` | Clear the screen |
| `:stop` | Stop any playing audio |
| `:strict on` / `:strict off` | Toggle [strict mode](Strict-Mode.md) for the session |
| `:quit` | Exit |

### Inspecting Sequences and Buffers

`(inspect seq)` and its alias `(visualize seq)` render an ASCII piano roll with articulation glyphs and a tick-mark row:

```
> (visualize (| C4q E4q G4q C5q |))
```

Glyphs: `>` accent, `.` staccato, `^` marcato, `_` tenuto, `!` sforzando, `~` legato. For buffers, `(visualize buf)` draws a waveform, and `(prettyBuffer buf)` / `(bufferHex buf)` give a summary and a hex dump. See [Visualization](Visualization.md).

## The Language Server (`flow lsp`)

`flow lsp` starts an LSP 3.17 server over stdio. It registers signatures only — no audio — so it is safe to run inside an editor. Capabilities that ship today:

| Capability | Notes |
|------------|-------|
| Diagnostics | Six analyzers merged per parse: parse errors, `scaleLint`, `unusedImport`, `unreachableSection`, `shadowedVariable`, `undefinedSymbol` |
| Completion | Context-aware — boosts roman numerals inside `key { }`, only stdlib paths inside `use "..."`, context-appropriate items inside note streams |
| Hover | Markdown from `BuiltInDocs`, user symbols, or stdlib procs |
| Go-to-definition | User symbols + stdlib import paths (builtins return null) |
| Signature help | Comma-count active-parameter tracking |
| Semantic tokens | Full-document (9-entry legend), hybrid with the TextMate grammar |

**Scale linting** deserves a callout: inside a `key { }` block the server flags out-of-key notes as you type. It runs unconditionally — the `enable scaleLint;` pragma is accepted as a no-op because the lint is already default-on.

Not yet implemented: find-references, rename, code actions, document/workspace symbols, inlay hints, a formatter, and semantic-token deltas.

## Editor Support

| Editor | Status |
|--------|--------|
| **VSCode** | Full extension — bundles a per-platform `flow-lsp`, TextMate grammar (all 5 comment variants), language config, snippets, and `flow.server.path` / `flow.trace.server` settings |
| **JetBrains** (IntelliJ/Rider) | LSP-only via LSP4IJ, IntelliJ Platform 2024.2+; installed manually from a `.zip` |
| **Neovim / Helix / Emacs / Zed / Cursor / Windsurf** | Generic LSP — point your client at `flow lsp`. Recipes live in `docs/editor-setup/` |

Any editor with an LSP client can use Flow: run `flow lsp` as the server command and associate `.flow` files with it.

## Testing

Flow ships a pure-Flow test framework via `@test`. A test file uses `(test "name" body)` with `(assert)`, `(assertEq)`, `(assertNotesMatch)`, `(assertBytesEqual)`, and `(assertWithinDb)`. Each test gets a hermetic snapshot/restore of mutable engine state so cases don't bleed into each other.

```bash
flow test tests/                       # run every test_*.flow under tests/
flow test tests/test_comprehensive.flow  # run a single file
```

See [Standard Library](Standard-Library.md#test-framework-test) for the assertion surface.

## Configuration

Flow reads `~/.config/flow/config.toml` if present (six keys, including `sfz_root` and `stdlib_search_path`). A missing file falls back to defaults silently; malformed TOML warns to stderr and uses defaults — it never aborts. The config path is currently hard-coded to `~/.config/flow/` (`$XDG_CONFIG_HOME` is not consulted yet).

## See Also

- [Quick Start](Quick-Start.md) — Install, run, REPL basics
- [Live Coding](Live-Coding.md) — `flow watch` and the live status panel
- [Strict Mode](Strict-Mode.md) — The `:strict` REPL toggle and `enable strict;`
- [Visualization](Visualization.md) — `visualize`, `prettyBuffer`, `bufferHex`
- [Playback and Export](Playback-and-Export.md) — `midi2flow` round-trips, export formats
- [Playground](Playground.md) — The browser version of the interpreter
