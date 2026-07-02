---
phase: quick-260702-gmm
plan: 01
subsystem: language-core
status: complete
tags: [musical-context, note-stream, lexer, parser, interpreter, ergonomics]
requires:
  - "MusicalContext push/pop frame-walk (GetMusicalContext dynamic scope)"
  - "NoteStreamCompiler eval-time context threading (ExpressionEvaluator seam)"
provides:
  - "octave N { ... } musical-context block (7th reserved keyword)"
  - "MusicalContext.DefaultOctave field + frame-walk resolution"
  - "NoteType.Parse(string, int defaultOctave) overload"
  - "Bare single note letters (A-G) as notes in note streams / brackets / ghost / grace"
affects:
  - "flow-lang note-stream parsing + compilation"
tech-stack:
  added: []
  patterns:
    - "Mirrored the voicePool integer-argument context-block path end-to-end"
    - "Charitable clamp + one-shot RenderingDiagnostics.WarnOnce advisory (house style)"
key-files:
  created:
    - "tests/test_octave_block.flow"
    - ".planning/quick/260702-gmm-add-octave-block-for-default-octave-musi/deferred-items.md"
  modified:
    - "flow-lang/Lexing/TokenType.cs"
    - "flow-lang/Lexing/SimpleLexer.cs"
    - "flow-lang/Ast/Statements/MusicalContextStatement.cs"
    - "flow-lang/Parsing/Parser.cs"
    - "flow-lang/Parsing/Parser.NoteStream.cs"
    - "flow-lang/Interpreter/Interpreter.cs"
    - "flow-lang/Runtime/MusicalContext.cs"
    - "flow-lang/Runtime/ExecutionContext.cs"
    - "flow-lang/Runtime/NoteStreamCompiler.cs"
    - "flow-lang/TypeSystem/SpecialTypes/NoteType.cs"
    - "CLAUDE.md"
decisions:
  - "Bare single note letters (A-G) are recognized as notes at the note-stream PARSER, not the lexer — the lexer keeps excluding single letters (Int C = 5 variable-name safety)."
  - "octave N clamps charitably to [1, 9] (widest range where bare A-G stays inside NoteType's E0-E10 window) — never throws."
  - "Named chords + roman numerals keep resolver-assigned octaves (out of scope by design)."
  - "Test harness uses @test's assertNotesMatch (octave-aware) — str(Sequence) only shows a bar/beat summary, so plain str-equality can't distinguish octaves."
metrics:
  duration: "~1h"
  completed: "2026-07-02"
  tasks: 2
  files_changed: 12
  commits: 2
---

# Quick 260702-gmm: octave N { } default-octave musical-context block — Summary

Added `octave N { ... }` as the 7th reserved musical-context keyword. Bare note letters
written without an explicit octave digit inside a `| ... |` stream now compile at the block's
octave (`octave 3 { | C D E | }` → C3/D3/E3), including bracket chords `[C E G]`, ghost/grace
notes, and tuplet notes. Explicit octave digits (`C5`) always win; nesting is innermost-wins;
the default reverts to octave 4 after the block; and the block reaches note streams inside
called procs/sections via dynamic scope (like tempo/swing). Out-of-range `octave N` clamps
charitably to `[1, 9]` with a one-shot `[octave]` advisory — never throws.

## What shipped

**Task 1 — language machinery (commit `6276915`)**
- `TokenType.Octave` + `"octave" => TokenType.Octave` lexer keyword.
- `MusicalContextType.Octave` enum arm; parser dispatch (integer-literal lookahead, mirroring
  voicePool) + value-parse arm.
- Interpreter apply arm: evaluates N, charitably clamps to `[1, 9]` via
  `RenderingDiagnostics.WarnOnce`, sets `MusicalContext.DefaultOctave`.
- `MusicalContext.DefaultOctave` nullable field — participates in `Clone()` + `ToString()`.
- `ExecutionContext.GetMusicalContext` resolves `DefaultOctave` through the `??=` frame walk
  (dynamic scope) and gates the early-completion break on it.
- `NoteType.Parse(string, int defaultOctave)` overload; the 1-arg form delegates with `4`, so
  every existing caller is byte-identical.

**Task 2 — note-stream threading + bare-letter support + tests/docs (commit `616782d`)**
- `NoteStreamCompiler` passes `context.DefaultOctave ?? 4` to `NoteType.Parse` at the five
  in-scope bare-letter sites (main notes, ghost, grace, tuplet notes, bracket chords).
  `CompileChordElement` gained a `MusicalContext context` parameter (threaded at both call
  sites). Named chords + roman numerals intentionally keep the 1-arg `Parse` (resolver-assigned
  octaves), with explanatory comments.
- `tests/test_octave_block.flow` — 8 octave-aware assertions via the `@test` framework.
- `CLAUDE.md` documents the 7th reserved keyword.

## Deviations from Plan

### Auto-added (Rule 2/3 — missing critical functionality / blocking)

**1. [Rule 2/3] Bare single note letters (A-G) now parse as notes in note streams**
- **Found during:** Task 2 (first test run).
- **Issue:** The plan assumed bare note letters already reach `NoteStreamCompiler` as
  `NoteElement`s. They do NOT — `SimpleLexer.TryParseNote` deliberately excludes single letters
  (line 1190-1191, to protect variable names like `Int C = 5;`), so a bare `C` in a stream
  lexed as an Identifier and rendered as a **rest** (`| C D E |` → three rests + advisory), and
  `[C E G]` was a **hard parse error**. Without recognizing bare letters as notes, the entire
  feature was inert.
- **Fix:** Extended the note-stream PARSER (not the lexer) to treat a bare single uppercase
  A-G letter as a note: a new `IsBareNoteLetter` predicate + an `ExpectNoteLiteralOrBareLetter`
  helper. Wired into the main-stream identifier branch (before the uppercase-typo recovery),
  both chord-bracket loops, and `(ghost X)` / `(grace X)`. The octave is supplied downstream by
  `NoteType.Parse(name, DefaultOctave ?? 4)`. This keeps the lexer's variable-name safety intact
  (single letters still lex as Identifiers everywhere else).
- **Safety:** Previously `| C |` (bare uppercase single letter) rendered as a rest — a broken /
  typo state no valid script relies on. The full `.flow` suite still shows only the 4
  known-baseline non-zero-exit scripts; no regression.
- **Files:** `flow-lang/Parsing/Parser.NoteStream.cs`.
- **Commit:** `616782d`.

**2. [Rule 1 — test-harness correction] Test uses `@test` assertNotesMatch, not str-equality**
- **Found during:** Task 2.
- **Issue:** The plan prescribed `(check (equals (str a) (str b)) ...)` on Sequences. But
  `str` of a `Sequence` returns only a summary (`Sequence[1 bars, 4 beats total]`) — it does
  NOT expose note pitch/octave, so str-equality is a false positive (all three octaves compared
  equal). Direct `(equals seqA seqB)` is reference equality (also unusable).
- **Fix:** Rewrote `tests/test_octave_block.flow` to use the `@test` framework's
  `assertNotesMatch`, which compares Sequences structurally INCLUDING octave. Sequences are
  built inside helper procs that `return` a stream from under an `octave` block (context blocks
  create their own variable scope, so locals can't leak out). Run with:
  `dotnet run --project flow-cli -- test tests/test_octave_block.flow` → emits PASS/FAIL +
  `Total/Passed/Failed`; exits non-zero on any FAIL. When run via the plain interpreter (e.g.
  the full-suite loop) it registers tests without running them → clean exit 0.

## Verification

- **Desktop build** (`dotnet build`): 0 errors.
- **Web build** (`dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web`): 0 errors
  (pure-core feature, no web-strip surface).
- **New test** (`flow test tests/test_octave_block.flow`): 8/8 PASS — bare letters adopt block
  octave, explicit octaves win, bracket chords, ghost+grace, nested innermost-wins, revert to 4
  after block, dynamic scope through a proc, and the `[1, 9]` charitable clamp (octave 12 → 9
  with `[octave]` advisory).
- **Full `.flow` suite** (`for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done`):
  only the 4 documented known non-zero-exit scripts fail (`test_dict_type_errors`,
  `test_error_masking`, `test_iteration_guard`, `test_musical_context_errors`) — every other
  script exits 0. Zero new `.flow` regressions.
- **xUnit** (`dotnet test flow-lang.Tests`): 2738 passed / **1 failed** / 19 skipped. The single
  failure — `Phase10.FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException` — is a
  **pre-existing stale test** from commit `44da382` (the 260701-vx4 charitable vocal fix that
  made `GetFormants` degrade-not-throw); it is in the vocal subsystem, entirely untouched by
  this task. **Zero new failures introduced.** Logged in `deferred-items.md`.
- **Determinism:** no-`octave` scripts are byte-identical — `DefaultOctave` stays null →
  `context.DefaultOctave ?? 4` → octave 4, exactly as before. Scripts using explicit octave
  digits never touch the new bare-letter path.

## Known Stubs

None.

## Self-Check: PASSED
- Created files exist: `tests/test_octave_block.flow` (FOUND, tracked via `git add -f` since
  `tests/` is gitignored), `deferred-items.md` (FOUND).
- Commits exist: `6276915` (FOUND), `616782d` (FOUND).
