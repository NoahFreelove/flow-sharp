---
phase: 27-tutorial-showcase-refresh
plan: 02
subsystem: tutorial
tags: [tutorial, music-features, batch-chapter, tuplets, fractional, microtonal, scale-lint, dx-bundle, range, enharmonics, negative-slice, hAsB, humanize-gaussian]
requires: [27-01]
provides: [tutorial-v1.3-music-features-batch-chapter]
affects: [examples/tutorial.flow]
tech-stack:
  added: []
  patterns: [chapter-divider-S4, demo-body-S5]
key-files:
  created: []
  modified:
    - examples/tutorial.flow
key-decisions:
  - "Added 'use \"@notation\"' to tutorial preamble so QUARTER / EIGHTH NoteValue keyword constants resolve (required by arpeggio / delay / quantize signatures)."
  - "DX-15 (varispeed) demoted to prose-only — the codebase has no Buffer-only varispeed function; varispeed lives as the second arg of (loadWav path Int|Double), and tutorial.flow does not ship a sample file."
  - "All Composer DX signatures corrected against actual registered builtins (arpeggio uses NoteValue not Double; chord inversion is 'inversion' not 'invertChord'; DX-12 is a 'delay' overload not 'delaySync'; quantize takes (Sequence, NoteValue, Double, Double); portamento takes (Sequence, Millisecond) not (Note, Note, Double))."
  - "Negative integer literals (-1) are not lexed as Int tokens in argument or array-index position; must be bound via (neg N) and used through a variable. Demo preserved by binding 'negOne' / 'negThree' / 'negOneIdx' helpers."
  - "key 'Aflatmajor' -> 'Abmajor' — ValidKeys spells flat keys with 'b' (not 'flat')."
requirements-completed: [QOL-04]
duration: ~25 min
completed: 2026-05-10
---

# Phase 27 Plan 02: Tutorial v1.3 Music Capabilities Mega-Chapter Summary

Append a single new mega-chapter "19.5 v1.3 Music Capabilities" to tutorial.flow with four sub-sections (A–D) batched between chapter 19 (Voice Synthesis) and chapter 20 (Graduation Piece), plus 13 new bullets in the closing Congratulations list.

## What was built

| Sub-section | Status | Runnable demos |
|-------------|--------|----------------|
| Header | Inserted | Mega-chapter banner + 4-line introduction prose |
| 19.5.A Tuplets and Fractional Durations | Inserted | 6 variants — bracket {3:2 ...}q, shorthand {3 ...}q, fractional C4/12, per-note C4/3:2, nested {3:2 ... {3:2 ...}q ...}h, tied-out |
| 19.5.B Microtonal + Scale-Lint Pragmas | Inserted | Prose-only (FILE-SCOPED rule prevents in-file activation); pointers to companion files |
| 19.5.C Composer DX Bundle | Inserted | DX-10 arpeggio + DX-11 inversion + DX-12 delay-NoteValue + DX-13 quantize + DX-14a legato + DX-14b portamento + Hertz-literal createSineTone (DX-15 prose-only — see deviations) |
| 19.5.D Misc Small Wins | Inserted | (range), (enharmonic B3) in Abmajor (Cb edge), negative slice ((neg)-bound), humanizeGaussian (seed=314) |
| Congratulations bullets | Appended | 13 new v1.3 bullets covering Symbols/Tuples, Dict, prefix-arithmetic, tuplets, Hertz/Ms/s literals, gain-vs-volume, DX bundle, pragmas, scale-lint, humanizeGaussian, range/enharmonics/negative slice |

## Line counts

- Before Wave 2 start: 830 lines
- After Wave 2 end: 1021 lines (+191 lines)
- Three commits: 7915552 (header + 19.5.A), b9ccf03 (19.5.B + 19.5.C), 4206373 (19.5.D + bullets)

## Verification

```
$ dotnet run --project flow-interpreter examples/tutorial.flow
EXIT=0
$ ls examples/output/flow_tutorial.{wav,mid}
flow_tutorial.wav (5503724 bytes), flow_tutorial.mid (non-empty)
$ dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorial"
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
$ grep -E "^Note: 19\.5\.[ABCD]" examples/tutorial.flow | wc -l
4
$ grep -c "{3:2\|C4/12\|(arpeggio\|(quantize\|(legato\|(humanizeGaussian\|(range\|@negOneIdx\|createSineTone .* 440Hz\|(enharmonic B3" examples/tutorial.flow
24  (>= 11 required)
```

## Deviations from Plan

**[Rule 1 — Bug] String interpolation does not support `{{` for literal `{`** (Task 1) — Found during: Task 1 first run | Issue: `(print $"shorthand {{3 ...}}: ...")` errored "Nested braces not supported in string interpolation". | Fix: Replaced with `(print (concat "shorthand {3 ...}: " (str shorthand)))` (concat keeps the literal brace + same prefix-arithmetic style we just taught in chapter 2). | Files modified: examples/tutorial.flow line 745. | Verification: tutorial re-ran clean. | Commit hash: 7915552.

**[Rule 1 — Bug] Tutorial.flow missing `use "@notation"` import** (Task 2) — Found during: Task 2 first run | Issue: `QUARTER`, `EIGHTH`, etc. NoteValue keyword constants are declared in flow-lang/notation.flow but tutorial.flow only imported `@std`/`@audio`/`@composition`/`@collections`. The arpeggio / delay / quantize signatures all take NoteValue. | Fix: Added `use "@notation"` to the preamble (line 10). Verified Phase 18 byte-identical determinism preserved (2/2 tests still pass). | Files modified: examples/tutorial.flow line 10. | Verification: byte-identical test green; tutorial exits 0. | Commit hash: b9ccf03.

**[Rule 1 — Bug] Six stale Composer DX signatures in plan** (Task 2) — Found during: Task 2 grep run | Issue: Plan called `(arpeggio Cmaj7 0.25 "up" "diatonic")`, `(invertChord Cmaj7 1)`, `(delaySync dxBuf 0.25 0.5 0.4)`, `(quantize loose 0.125)`, `(portamento C4 G4 0.5)`, `(varispeed src 1.5)`. Cross-referenced flow-lang/StandardLibrary/{Harmony/HarmonyFunctions.cs, Harmony/Voicings.cs, Audio/EffectsFunctions.cs, Transforms/TransformFunctions.cs} and flow-lang/StandardLibrary/Audio/FileIO.cs to find the actual registered signatures. | Fix:
  - `arpeggio` takes `(Chord, NoteValue, String, String)` — used `QUARTER` keyword + `"linear"` (the only registered pattern; `chord-tone`/`scale-tone` route to linear in v1.3).
  - `invertChord` does not exist — the registered builtin is `inversion(Chord, Int)`.
  - `delaySync` does not exist — DX-12 is a `delay` overload `(Buffer, NoteValue, Double, Double)`.
  - `quantize` takes 4 args `(Sequence, NoteValue, strength, swing)` — used `EIGHTH 1.0 0.0`.
  - `portamento` takes `(Sequence, Millisecond)` — rewrote demo to apply portamento to the legato sequence with a 50ms glide.
  - There is no `varispeed(Buffer, Double)` function. DX-15 is bundled into `loadWav(path, Int|Double)`. Tutorial does not ship a sample file → demoted DX-15 to prose-only with a worked code example showing the loadWav syntax. The `createSineTone 0.5 440Hz 0.3` Hertz-literal demo (separate must_have line) is preserved as a runnable line. | Files modified: examples/tutorial.flow sub-section C (lines ~803-848). | Verification: tutorial exits 0; all DX-10..14 print runnable output. | Commit hash: b9ccf03.

**[Rule 1 — Bug] Negative integer literals do not parse as Int tokens in argument/index position** (Task 3) — Found during: Task 3 first run | Issue: `(range 5 0 -1)`, `(slice sliceDemo -3 5)`, `(slice sliceDemo 0 -1)`, `sliceDemo@-1` all fail to lex with "Unexpected token Minus '-'". | Fix: Bound `Int negOne = (neg 1)`, `Int negThree = (neg 3)`, `Int negOneIdx = (neg 1)` and used the bound names in argument/index positions. Print messages updated to show the (neg N) call form (more honest pedagogy). Negative-step + negative-slice + from-end-indexing semantics all preserved at runtime. | Files modified: examples/tutorial.flow sub-section D. | Verification: range / slice / arr@var with negative all print expected output. | Commit hash: 4206373.

**[Rule 1 — Bug] `key Aflatmajor` not in ValidKeys** (Task 3) — Found during: Task 3 third run | Issue: `MusicalContext.BuildValidKeys()` spells flat keys with `b` (Db, Eb, Gb, Ab, Bb), not the word "flat". `Aflatmajor` is rejected. | Fix: Renamed `key Aflatmajor` → `key Abmajor` (and the print prose). The Cb-edge enharmonic semantics are unchanged: B3 in Abmajor still resolves to Cb (`(enharmonic B3)` returns `C4-` which is the Cb spelling). | Files modified: examples/tutorial.flow sub-section D enharmonic block. | Verification: tutorial exits 0; B3 prints as "C4- (Cb edge)". | Commit hash: 4206373.

**Total deviations:** 5 auto-fixed (5 × Rule 1 bug). **Impact:** moderate — multiple stale plan signatures suggest the plan-checker did not verify against the actual codebase. Final tutorial behavior matches plan intent; only the literal call syntax differs.

## Issues Encountered

None remaining — all auto-fixed.

## Self-Check: PASSED

- All 3 tasks executed and committed atomically.
- Tutorial.flow exits 0 with non-empty WAV (5.5 MB) + MIDI artifacts.
- Phase 18 ByteIdenticalTutorialTests: 2/2 passed (deterministic two-run gate).
- 4 sub-section dividers (A/B/C/D) present.
- 24 hits on the v1.3 features grep audit (≥11 required).
- Ready for Wave 3 (graduation song refactor + showcase replacement + pragma companion files).
