# Phase 38 REPL Polish — Narrated Session

Phase 38 ships four REPL upgrades (REPL-01..04). This chapter walks through
three real composer interactions exercising each surface. Mirrors the
`examples/notation/README.md` precedent — narrated, not executable.

Launch the REPL with:

```
dotnet run --project flow-interpreter
```

You will see the existing welcome banner (preserved from `Repl.cs`):

```
Flow REPL - Type ':quit' to exit, ':help' for help
Multi-line input: end a line with \ to continue on next line
>
```

The `:help` text gained one new line at Plan 38-04 (UI-SPEC line 362):

```
> :help

  :quit            - Exit the REPL
  :help            - Show this help text
  :help <name>     - Show docs for a builtin (e.g. ':help transpose')
  :clear           - Clear the terminal
  :stop            - Stop active audio playback
```

---

## Session 1 — Discovering a builtin via `:help fn` (REPL-02 / D-38-09)

The classic mid-session "what was the signature of `transpose` again?" loop
used to require opening another terminal and grepping `flow-lang/StandardLibrary/`.
With Phase 38, `:help <name>` queries the in-process Phase 31 `BuiltInDocs`
table (104 entries) and renders a 3-block layout per UI-SPEC lines 263-280:

```
> :help transpose

transpose
  (transpose Sequence interval) → Sequence
  (transpose Sequence cents) → Sequence

  Shifts every note in a Sequence by the given semitone interval or
  cent offset. Negative intervals shift down.

  Example:
    | C4 E4 G4 | -> (transpose 5)
    -- becomes -- | F4 A4 C5 |

>
```

The header `transpose` renders in **bold + green** (Color accent reserved-for #2
per UI-SPEC line 98), the signature lines render in **dim** (low visual
weight), the body is default attribute, and the `Example:` label is dim with
the example in default. The composer scans the header first, then drops to
the body if more context is needed.

**Unknown identifier path** (yellow advisory per UI-SPEC line 289):

```
> :help fooBar
[help] no documentation entry for 'fooBar' — try ':help' for the meta-command list
>
```

The `[help]` prefix matches Flow's existing structured-advisory pattern
(`[tuning]`, `[live]`, `[osc]`, `[audio-in]`). The yellow color signals
"informational, not alarming" — the REPL stays open and ready.

**Note on the override** (D-38-09): REQUIREMENTS.md initially specified the
form as bare `?transpose`. The locked CONTEXT decision shipped `:help fn`
instead to keep consistency with Flow's existing `:quit`/`:help`/`:clear`/
`:stop` meta-command family. Plan 38-07 sweeps the REQUIREMENTS.md wording
per D-v1.5-01 single-commit migration latitude.

---

## Session 2 — Visualizing a Sequence via `(inspect seq)` (REPL-04 / D-38-10)

`(visualize seq)` shipped in earlier Flow versions as a simple ASCII piano
roll. Plan 38-04 extends it with **articulation glyphs at note onsets** and a
new **tick-mark row** above the first pitch row, AND ships `(inspect seq)`
as a builtin-level alias backed by the same renderer — composers can call
either name (D-38-10 alias pair).

```
> tempo 120 { timesig 4/4 { key Cmajor {
...   Sequence demo = | C4q D4q> E4q. F4q^ |
... } } }

> (inspect demo)
    +----+
       1
F4  |       ^   |
E4  |    .      |
D4  | >         |
C4  |#          |
>
```

Articulation glyph inventory (UI-SPEC §"Glyph Inventory" — ASCII-only,
locked):

| Glyph | Meaning           | Phase 28 source         |
|-------|-------------------|-------------------------|
| `#`   | Sustained body    | (unchanged)             |
| `>`   | Accent onset      | `Articulation.Accent`   |
| `.`   | Staccato onset    | `Articulation.Staccato` |
| `^`   | Marcato onset     | `Articulation.Marcato`  |
| `_`   | Tenuto onset      | `Articulation.Tenuto`   |
| `!`   | Sforzando onset   | `Articulation.Sforzando` |
| `~`   | Legato gap-fill   | `Articulation.Legato`   |
| `|`   | Bar line          | (unchanged)             |
| `+`   | Bar-line tick row | (new in Plan 38-04)     |
| `-`   | Tick-row rule     | (new in Plan 38-04)     |

Collision rules (UI-SPEC §"Glyph Composition Rules"): bar line `|` wins over
sustain `#`; onset glyph wins over sustain `#` (it's the same cell — the
onset IS the start of the note); legato `~` renders in the gap cell BETWEEN
two connected notes, on the LATER note's row when pitches differ.

Why ASCII-only? UI-SPEC line 204 lock — Unicode box-drawing breaks in
`xterm` / `vt100` / `dumb` / older Windows consoles; ASCII glyphs are
grep-able and copy-paste-safe into commit messages and README snippets.

`(inspect seq)` and `(visualize seq)` produce **byte-identical output** —
the alias is a thin signature registration dispatching to the same C# body.
Pre-Phase-38 scripts calling `visualize` keep working unchanged (backwards
compat per UI-SPEC line 232).

---

## Session 3 — Tab completion + Ctrl+R history search (REPL-01 / REPL-03)

The PrettyPrompt 4.1.1 line editor (D-38-11 license + maintenance gate
satisfied at Plan 38-04: MPL-2.0, .NET 6+, 199 stars on GitHub, last
published 2023-09-30) replaces `Console.ReadLine()` and brings two
high-impact composer ergonomics:

**Tab completion** routes through the in-process `flow-lsp` per D-38-12
SIMPLIFICATION: `CompletionHandler.BuildItems()` is a static method with no
transport coupling, so Plan 38-04 calls it directly on each Tab keypress.
The 4 symbol indices (`BuiltInIndex` / `StdlibSymbolIndex` / `KeywordIndex` /
`UserSymbolIndex`) are constructed once at REPL ctor time so each Tab
keystroke does not pay the cold-load cost.

```
> (transp<TAB>
  ┌───────────────────────────────────────────────────────────┐
  │ transpose                                                 │
  │   (transpose Sequence interval) → Sequence               │
  │   Shifts every note in a Sequence by the given interval  │
  └───────────────────────────────────────────────────────────┘
> (transpose
```

The completion menu shows the proc name + signature + 1-line summary from
`BuiltInDocs`. Token-heuristic fallback fires on partial-parse failure
(Pitfall #13 — REPL completion must NEVER fail to surface a completion just
because the line doesn't parse yet).

**Ctrl+R reverse history search** queries `~/.config/flow/history` (10k cap,
mode 0600 on Linux/macOS per UI-SPEC line 300):

```
> <Ctrl+R>
(reverse-i-search): trans
> (transpose | C4q D4q E4q | 5)
```

History persists across REPL sessions; failed-parse entries also persist so
the composer can recall + edit them via Up arrow / Ctrl+R. The history file
follows the XDG-compatible `~/.config/flow/` path established by Phase 30
config.toml.

**Multi-line continuation** preserves the existing paren-balanced detection
(`Repl.cs:182-208` lexer-counted Brace + Proc + LParen + LBracket nesting,
extended in Plan 38-04 from brace-only to also cover parens and brackets per
the auto-add Rule 2 deviation). Backslash-EOL continuation also preserved.

```
> (transpose
...   | C4q D4q E4q |
...   5)
| F4q G4q A4q |
>
```

Bare Enter on unbalanced input becomes Shift+Enter soft-newline (PrettyPrompt
convention); the composer sees the `... ` continuation prompt.

---

## Composer ergonomics summary

| Surface              | Keystroke         | What it does                                |
|----------------------|-------------------|---------------------------------------------|
| `:help <name>`       | type + Enter      | 3-block builtin docs (header + sig + body) |
| `(inspect seq)`      | call + Enter      | ASCII piano roll + articulation glyphs      |
| Tab                  | partial identifier | LSP-backed completion menu                  |
| Ctrl+R               | type substring    | Reverse history search                      |
| `\` at EOL           | Enter             | Manual continuation                         |
| Auto (unbalanced)    | Enter             | Continuation prompt fires                   |
| `:quit`              | type + Enter      | Exit REPL                                   |
| Ctrl+C               | press             | Stop audio (preserves session — Pitfall #12)|
| Ctrl+D               | press             | EOF — exit REPL                             |
| `:clear` / Ctrl+L    | type + Enter      | Clear screen                                |

Pitfall #12 "live session never dies mid-set" is enforced throughout: every
recovery path (parse error, LSP failure, history-file unwriteable) biases
toward CONTINUE the REPL session + emit advisory rather than throw/exit.

---

## See also

- `examples/live/hello_live.flow` — minimal `live 1bar { }` block
- `examples/live/multi_block.flow` — multiple `live` blocks with independent swap timelines
- `examples/live/mic_granular.flow` — audio input → granular DSP composability
- `examples/live/osc_controller.flow` — OSC server + client loopback round-trip
- `.planning/phases/38-live-coding-2-0/38-UI-SPEC.md` — full visual + interaction contract
- `.planning/phases/38-live-coding-2-0/38-VERIFICATION.md` — per-REQ shipped-status log
