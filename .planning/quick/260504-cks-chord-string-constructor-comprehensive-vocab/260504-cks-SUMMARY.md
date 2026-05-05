---
phase: quick-260504-cks
plan: 01
status: complete
subsystem: harmony
tags: [chord, parser, harmony, runtime-constructor, vocabulary, ergonomics]

# Dependency graph
requires:
  - phase: existing
    provides: "ChordParser.TryParse, NamedChordElement note-stream injection, IsChordSymbol lexer dispatch, internal proc registry pattern"
provides:
  - "(chord <String>) and (chord <Note>) runtime constructors — composers can now build a Chord value from any computed/dynamic symbol, not only bare-token literals at parse time"
  - "ChordParser quality vocabulary expanded from 18 entries to ~80, covering triads, power chord, sixths (incl. 6/9), full 7th family, sus + 7sus/9sus, 9/11/13 dom+maj+min, adds, alterations with dual b/# === f/s aliases, and slash-bass syntax"
  - "38 new xUnit facts pinning the expanded vocabulary against silent regression"
  - "Flow-script regression covering 70+ chord shapes (tests/test_chord_runtime.flow)"
affects: [chord-literal-tokens, note-stream-chord-injection, scale-database-roman-numeral-resolution, harmony-builtins]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual-listing pattern in QualityIntervals: every alteration is listed with both Flow's identifier-safe accidentals (f/s) and common-practice notation (b/#) so a single dictionary lookup serves both surface forms without a normalization layer"
    - "Two-overload runtime constructor pattern (String + Note): defends against Flow's TryParseSpecialLiteral auto-coercing note-shaped quoted strings to Note values; both overloads route through the same TryParseFlexible parser so behavior is identical"
    - "A-G suffix gate for slash-bass: only treat `/` as a slash-chord delimiter when the suffix starts with an actual note letter, otherwise the slash belongs to the quality (preserves '6/9' vs 'C/G' disambiguation)"

key-files:
  created:
    - .planning/quick/260504-cks-chord-string-constructor-comprehensive-vocab/260504-cks-PLAN.md
    - .planning/quick/260504-cks-chord-string-constructor-comprehensive-vocab/260504-cks-SUMMARY.md
    - flow-lang.Tests/Unit/QuickFixes/ChordStringConstructorFacts.cs
    - tests/test_chord_runtime.flow
  modified:
    - flow-lang/StandardLibrary/Harmony/ChordParser.cs
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
    - flow-lang/std.flow
    - tests/std.flow

key-decisions:
  - "(chord) takes a String AND a Note. The Note overload is required because Flow's ExpressionEvaluator.TryParseSpecialLiteral auto-coerces any quoted literal that parses as a Note (`\"C\"`, `\"C5\"`, `\"G7\"`, `\"Bb\"`) into a Note value at evaluation time. Without the second overload, the most natural composer spellings would die with 'No matching overload'."
  - "Charitable on hopeless input: TryParseFlexible returns false (and the builtin returns Void) instead of throwing. Mirrors resolveNumeral's existing pattern. The composer is responsible for handling Void if the input is dynamic."
  - "Bare-digit qualities stay as note literals at lex time. Project convention from tests/test_chords.flow:13 ('G7 is parsed as note G at octave 7, use dom7 for chord') is preserved by the IsChordSymbol IsAllDigits gate. The runtime (chord \"G7\") still produces a dom7 chord because TryParseFlexible bypasses the lexer-side gate by routing through the Note overload."
  - "Dual-listing of alteration aliases (b5 + f5, #11 + s11, etc.) is cleaner than a normalization layer because alterations live in mid-token positions where the lexer already absorbs both characters into the chord identifier — and the dictionary stays the single source of truth for what's valid."
  - "Slash-bass guard: only consume the slash when the next character is A-G. Without this, 'C6/9' would get split into chord='C6' + bass='9' (charitably failing back to plain C6, losing the 9th)."

patterns-established:
  - "Runtime-constructor pattern for parser-locked types: when a type is currently only buildable via parse-time literals, add a (typeName <String>) builtin that wraps the same parser used by the lexer. Pair with a (typeName <Note>) overload if the input strings auto-coerce to Note via TryParseSpecialLiteral — this is the trap most string-form constructors will hit."
  - "Charitable failure for runtime constructors: return Value.Void() on parse failure, matching resolveNumeral. Composers can guard with type-checks if needed; rigid binding to a specific typed variable surfaces the failure as a type-mismatch."

requirements-completed:
  - QUICK-260504-cks

# Metrics
duration: ~50 min
completed: 2026-05-04
---

# Quick 260504-cks: (chord <String>) Runtime Constructor + Comprehensive Vocabulary

**Add a `(chord <String>)` and `(chord <Note>)` runtime constructor that builds a `Chord` value from any common-practice chord symbol, and massively expand the underlying `ChordParser` quality dictionary so the same vocabulary is available everywhere `ChordParser` is consumed (chord literals, note-stream injection, roman-numeral resolution).**

## What's now supported

Composers can write any of these as a runtime call:

```flow
(chord "Cmaj7")         // major 7
(chord "Cm7b5")         // half-diminished (b/# accidental form)
(chord "Cm7f5")         // half-diminished (Flow accidental form — same chord)
(chord "C13")           // dominant 13
(chord "Cmaj13")        // major 13
(chord "Cm9")           // minor 9
(chord "Cmaj7#11")      // lydian dominant
(chord "C13b9")         // altered dominant
(chord "C5")            // power chord (no 3rd)
(chord "C6/9")          // major 6/9
(chord "C7sus4")        // sus + 7
(chord "Cadd9")         // add9 (no 7th)
(chord "C/G")           // C major over G — bass note prepended one octave below
(chord "G7/B")          // dom7 with B in the bass
(chord "F#maj7")        // common-practice sharp form
(chord "Bbm7")          // common-practice flat form
```

Existing chord-literal tokens (`Cmaj7`, `Dm`, `Bdim`, `Csmaj`, `Bfm`, etc.) and
note-stream chord injection (`| Cmaj7 Am7 Dm G7 |`) **continue to work
byte-identically** — the same vocabulary expansion benefits every consumer of
`ChordParser`.

## What's NOT supported (and why)

- `(chord "C")` — a bare letter or `"C5"`/`"C7"`/`"D9"` etc. is auto-coerced to
  a `Note` value by Flow's `TryParseSpecialLiteral`. The `chord(Note)` overload
  catches these and re-parses them through `TryParseFlexible`, so they DO work
  — they just route via the Note path internally instead of the String path.
  Either spelling is fine.
- Microtonal/non-12-tone chord symbols. Out of scope for this quick task.

## Where the changes live

- **`flow-lang/StandardLibrary/Harmony/ChordParser.cs`** — `QualityIntervals`
  expansion (the single source of truth) + `TryParseFlexible` runtime entry
  point + slash-bass / accidental-normalization helpers + lexer-side
  bare-digit gate tightening.
- **`flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`** — registers both
  `chord(String)` and `chord(Note)` overloads.
- **`flow-lang/std.flow`** + **`tests/std.flow`** — `internal proc chord (...)`
  declarations so the C# implementation is reachable from `.flow` scripts.
- **`flow-lang.Tests/Unit/QuickFixes/ChordStringConstructorFacts.cs`** — 38
  precision facts.
- **`tests/test_chord_runtime.flow`** — flow-script regression covering 70+
  shapes end-to-end.

## Test status

- 698 → 736 xUnit tests (38 new, all passing)
- `tests/test_chords.flow` (existing chord regression) — byte-identical output
- `tests/test_chord_runtime.flow` (new) — passes end-to-end
- Full-tree `tests/test_*.flow` sweep: only the 3 pre-existing intentional-error
  scripts (`test_error_masking.flow`, `test_iteration_guard.flow`,
  `test_musical_context_errors.flow`) report errors — same as pre-change.

## Commits

- `415b251` docs(260504-cks): pre-dispatch plan
- `73ef06a` test(260504-cks): xUnit facts (38 new)
- `00d9e2e` feat(260504-cks): parser + builtin
- `222ce7c` test(260504-cks): flow-script regression
