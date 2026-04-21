---
phase: 14-composer-dx-part-1
reviewed: 2026-04-20T00:00:00Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs
  - flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs
  - flow-lang.Tests/Unit/Phase14/LexerTests.cs
  - flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs
  - flow-lang.Tests/Unit/Phase14/SliceTests.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/Collections.cs
  - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
  - flow-lang/TypeSystem/SpecialTypes/NoteType.cs
  - flow-lang/collections.flow
  - flow-lang/flow-lang.csproj
  - flow-lang/std.flow
  - flow-lang/test.flow
  - tests/test_dynamics_midi_velocity.flow
  - tests/test_enharmonic.flow
  - tests/test_flat_literals.flow
  - tests/test_slice.flow
  - tests/test_test_library.flow
findings:
  critical: 0
  warning: 1
  info: 6
  total: 7
status: issues_found
---

# Phase 14: Code Review Report

**Reviewed:** 2026-04-20
**Depth:** standard
**Files Reviewed:** 20 (19 changed source files + `flow-lang/flow-lang.csproj` in config scope but unchanged by the phase)
**Status:** issues_found

## Summary

Phase 14 landed three DX features (`slice`, flat-literal parsing + `enharmonic()`, and a MIDI velocity regression test) cleanly. Implementation quality is high: overload registration is consistent with existing stdlib patterns, silent two-sided clamping in `slice` is correctly applied on both overloads, and `NoteType.Parse` / `Format` round-trip is well covered. The `enharmonic()` implementation correctly handles the Pitfall 3 flat-key affinity rewrite for `Dbmajor` and explicitly documents its non-involutive behavior on double-accidentals.

No Critical or security issues. One Warning addresses a latent over-eager alteration pickup in `SimpleLexer.ScanIdentifierOrKeyword` that broadens the set of identifiers affected by trailing `+`/`-` glueing beyond what is strictly needed. Six Info items cover weak test assertions, a stale doc comment in a neighboring file, and a couple of minor defensive-coding opportunities.

Out-of-scope notes: the repo-wide `net10.0` TargetFramework in `flow-lang/flow-lang.csproj:4` diverges from CLAUDE.md's ".NET 9 — all code must target net9.0" constraint, but `git diff 2133f4f..HEAD` confirms the csproj was NOT modified in this phase — this is a pre-existing condition and is documented here only for awareness.

## Warnings

### WR-01: SimpleLexer alteration pickup triggers on any identifier starting with A-G (over-broad gate)

**File:** `flow-lang/Lexing/SimpleLexer.cs:552-563`
**Issue:** The D-07 alteration pickup loop was intentionally relaxed to support bare flats (e.g., `Bb`) and unbounded runs (per the inline comment), but in doing so it dropped both the `text.Length >= 2` and `char.IsDigit(text[1])` gates. The loop now runs for ANY identifier whose first character uppercases to A-G, including lowercase variable names like `foo`, `attack`, `bar`, `decay`, `enable`, `flag`, `gain`, and their capitalized forms.

Pre-Phase-14 behavior (commit 2133f4f): `text.Length >= 2 && firstChar >= 'A' && firstChar <= 'G' && char.IsDigit(text[1])` — required a digit at index 1, so the loop only fired for note-like shapes (`A3`, `C4`, etc.).

Post-Phase-14 behavior: given source `foo+bar` (no whitespace), the scanner reads `foo`, enters the pickup loop (because `'f'.ToUpper() == 'F'`), consumes `+`, stops at `b`. The resulting identifier is `foo+`, which fails every downstream parser (chord, note, semitone, time, decibel) and emerges as a malformed Identifier token. Pre-Phase-14 the same source tokenized as three tokens: `Identifier("foo")`, `Plus`, `Identifier("bar")`.

Observable impact is currently nil — Flow uses prefix function calls with whitespace around operators, and a grep of `tests/ examples/` found no source that relies on the old tokenization. But the silent expansion of the affected identifier set (from A-G-plus-digit prefixes only to any A-G prefix) is larger than the feature required, and future code that juxtaposes an identifier and `+`/`-` without whitespace would break silently.

**Fix:** Tighten the gate to only match identifiers that could plausibly be note literals. One approach is to require either a digit somewhere in `text` OR `text` being exactly a single letter, since those are the only shapes `TryParseNote` accepts. For example:
```csharp
if (text.Length >= 1)
{
    char firstChar = char.ToUpper(text[0]);
    bool looksNoteLike = firstChar >= 'A' && firstChar <= 'G'
        && (text.Length == 1 || text.Any(char.IsDigit) || text[1] == 'b' || text[1] == '#');
    if (looksNoteLike)
    {
        while (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
            sb.Append(Advance());
        text = sb.ToString();
    }
}
```
Alternative: add a regression Fact in `LexerTests.cs` pinning the desired behavior for `foo+bar` so the contract is explicit either way.

## Info

### IN-01: Natural-enharmonic tests use `Contains` substring match (weak assertion)

**File:** `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs:62-105`
**Issue:** The four natural-unchanged Facts (`NoKey_NaturalUnchanged_C4/E4/B4/F4`) all assert `stdout.Contains("C4")` / `Contains("E4")` / etc. Because these are substring matches, an incorrect output like `"C4+"` or `"C4-"` would still satisfy `Contains("C4")`. The paired `DoesNotContain("B#")` / `DoesNotContain("Cb")` / `DoesNotContain("Fb")` / `DoesNotContain("E#")` catch the specific D-05 edge respellings the plan was worried about, but they do NOT catch the common regression where a natural accidentally picks up a `+` or `-`.
**Fix:** Add `DoesNotContain("C4+")` and `DoesNotContain("C4-")` (with the correct letter/octave per Fact), or replace `Assert.Contains` with an exact stdout match such as `Assert.Equal("C4\n", stdout)` (after trimming test prelude noise).

### IN-02: Stale docstring in `ChordParser.IsChordSymbol` after lexer dispatch reorder

**File:** `flow-lang/StandardLibrary/Harmony/ChordParser.cs:60-62` (not in changed file list but directly referenced by the Phase 14 lexer change)
**Issue:** The docstring reads:
> "Note: The lexer calls TryParseNote first, so anything reaching this method has already failed note parsing (e.g., C4 is caught as a note before this runs)."

Phase 14 reordered the lexer to dispatch `IsChordSymbol` BEFORE `TryParseNote` (`SimpleLexer.cs:625-634`, per D-21 defence-in-depth). The comment is now the opposite of reality and will mislead future readers.
**Fix:** Update the docstring to: "Note: The lexer calls IsChordSymbol before TryParseNote (Phase 14 DX-06 D-21), so this method runs first for A-G-prefixed tokens. Tokens rejected here fall through to note parsing." This is outside the changed-file list for Phase 14 but is a direct doc-drift consequence of the reorder.

### IN-03: Docstring for `slice` parameter names differs from user-visible names

**File:** `flow-lang/StandardLibrary/Collections.cs:151-156`, `flow-lang/collections.flow:15-16`
**Issue:** The `SliceArray` / `SliceSequence` C# docstrings describe parameters as "start (inclusive) to end (exclusive)", but the user-visible parameter names in `collections.flow` are `s` and `e` (documented Rule 1 deviation because `end` is a reserved keyword). The C# summary using the same names readers will see in tooling (when Flow gets hover/completion later) would make the source-to-docs mapping clearer.
**Fix:** Either rename the C# `start`/`end` locals in the docstring to `s`/`e` (matching the Flow side), or add a one-line note in the C# docstring that says "Flow-side param names are `s` / `e` because `end` is a reserved keyword." Minor — purely documentation polish.

### IN-04: `flow-lang.csproj` targets `net10.0` despite CLAUDE.md mandating `net9.0`

**File:** `flow-lang/flow-lang.csproj:4`
**Issue:** CLAUDE.md §Constraints says ".NET 9 — all code must target net9.0", but the csproj has `<TargetFramework>net10.0</TargetFramework>` (and `<Folder Include="bin\Debug\net9.0\" />` at line 40, suggesting it was previously net9.0). `git diff 2133f4f..HEAD -- flow-lang/flow-lang.csproj` shows this file was NOT modified in Phase 14, so the drift predates this phase — but it was in scope for review per the config file list. Recording here so the drift is not lost.
**Fix:** Out of scope for a Phase 14 revert. Raise as a separate concern for the next phase; either align `.csproj` back to `net9.0` or update CLAUDE.md's constraint to reflect the actual target.

### IN-05: `test.flow` contains user-facing test-library code with no Phase 14 changes

**File:** `flow-lang/test.flow`
**Issue:** The file is a pure-Flow assertion library (`assertTrue`, `assertEqual`, `test`, `summary`) that was in the review scope but contains no Phase 14 changes. `git diff 2133f4f..HEAD -- flow-lang/test.flow` shows no modifications. Flagging for traceability so the SUMMARY reader can confirm the review did inspect it.
**Fix:** None required. Informational.

### IN-06: `tests/test_test_library.flow` likewise unchanged in Phase 14

**File:** `tests/test_test_library.flow`
**Issue:** End-to-end regression of the `@test` pure-Flow library. Reviewed; no Phase 14 content. Structurally sound: deliberately includes FAIL cases labeled `(expected FAIL)` to exercise the assert-false branches. No issues observed.
**Fix:** None required. Informational.

---

_Reviewed: 2026-04-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
