---
phase: quick-260621-n9y
plan: 01
subsystem: flow-interpreter (REPL)
tags: [repl, note-stream, completeness, bug-fix]
status: complete
requires:
  - flow-interpreter/ReplLineEditor.cs
  - flow-lang/Parsing/Parser.NoteStream.cs (model reference)
  - flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs (IsRomanNumeral)
provides:
  - "ReplInputCompleteness.IsInputComplete with a note-stream-aware inStream scan"
  - "ContinuesNoteStream(Token) helper mirroring IsEndOfNoteStream()"
affects:
  - flow-interpreter REPL multi-line continuation behavior
tech-stack:
  added: []
  patterns:
    - "Token-indexed scan with next-token lookahead (replaces foreach + parity counter)"
key-files:
  created: []
  modified:
    - flow-interpreter/ReplLineEditor.cs
    - flow-lang.Tests/Integration/Phase38/ReplMultiLineTests.cs
decisions:
  - "Omit parser-private TryParseDynamicMarking; duration-letter + lowercase-identifier rules cover the common dynamic-marked overlaps without a parser-private dependency (per plan)."
  - "A pipe that is the very last token is ambiguous mid-typing -> leave inStream = true (request continuation)."
metrics:
  duration: ~6min
  completed: 2026-06-21
---

# Quick Task 260621-n9y: Fix the REPL Multi-Line Completeness Bug Summary

Replaced the `pipeCount % 2 == 0` parity heuristic in `ReplInputCompleteness.IsInputComplete` with a note-stream-aware `inStream` scan that mirrors `Parser.NoteStream.IsEndOfNoteStream()`, so single-line N-bar note streams (N+1 pipes) submit instead of freezing the REPL in continuation mode.

## What Was Done

### Task 1 — Replace pipe-parity heuristic with note-stream-aware scan (commit `7ef1ee5`)

- Added `using FlowLang.StandardLibrary.Harmony;` for `ScaleDatabase.IsRomanNumeral`.
- Removed `int pipeCount = 0;`, the `else if (token.Type == TokenType.Pipe) pipeCount++;` arm (and its sweep-0614 comment), and the `&& (pipeCount % 2 == 0)` return term.
- Converted the `foreach (var token in tokens)` loop to `for (int i = 0; i < tokens.Count; i++)` (brace/proc/paren/bracket counting unchanged, just sourced from `tokens[i]`) so the pipe arm can peek `tokens[i + 1]`.
- Added the pipe arm: a pipe not `inStream` opens a stream; a pipe already `inStream` closes it unless the next token `ContinuesNoteStream` (bar separator); a trailing pipe with no next token leaves `inStream = true` (mid-typing).
- Added `private static bool ContinuesNoteStream(Token next)` mirroring the inverse of `IsEndOfNoteStream()`: NoteLiteral / Underscore / LBracket / Pipe / ChordLiteral / LParen / GreaterThan / LBrace, plus Identifier when roman numeral, articulation (`stacc`/`ten`/`marc`/`leg`/`cresc`/`decresc`), duration letter (`w`/`h`/`q`/`e`/`s`/`t`), or lowercase-initial variable ref (excluding duration letters).
- Final return now `... && !inStream;` with an explanatory comment on the N-bar → N+1-pipe root cause.
- The empty/whitespace, backslash-at-EOL, and `internal proc` early-returns + the SimpleLexer tokenization were preserved verbatim. File's existing bracketed-namespace style untouched (note: this file actually uses a file-scoped namespace already; no style was converted).

### Task 2 — Add multi-bar regression cases (commit `95dd5db`)

Added three `[Fact]` methods to the existing `ReplMultiLineTests` class (reusing its `RenderingDiagnostics.ResetForTesting()` + `FlowConfig.Reset()` harness):

- `MultiBarSingleLineStream_DoesNotRequestContinuation` — the reported 4-bar freeze (5 pipes) and a two-bar form both assert COMPLETE.
- `MidTypedMultiBarStream_RequestsContinuation` — `Sequence s = | C4 | D4` and `Sequence s = | C4 D4` both assert INCOMPLETE.
- `BalancedBlockWithMultiBarStream_DoesNotRequestContinuation` — balanced `tempo 120 { ... | 4-bar | ... }` asserts COMPLETE; the open-block variant asserts INCOMPLETE.

## Verification

**Build (`dotnet build flow-interpreter`):**

```
    21 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.17
```

(21 warnings are all pre-existing VSTHRD002/xUnit analyzer warnings; 0 errors.)

**Targeted tests (`dotnet test flow-lang.Tests --filter "FullyQualifiedName~ReplMultiLineTests"`):**

```
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - flow-lang.Tests.dll (net10.0)
```

All 8 facts pass (5 existing + 3 new). The reported 4-bar freeze input asserts True; `Sequence s = | C4 | D4` asserts False; the balanced block asserts True. No pre-existing fact regressed.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

- `flow-interpreter/ReplLineEditor.cs` — FOUND (modified; `inStream` + `ContinuesNoteStream` present, no `pipeCount`).
- `flow-lang.Tests/Integration/Phase38/ReplMultiLineTests.cs` — FOUND (3 new facts present).
- Commit `7ef1ee5` — FOUND.
- Commit `95dd5db` — FOUND.
