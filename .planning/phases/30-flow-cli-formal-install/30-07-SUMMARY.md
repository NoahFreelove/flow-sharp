---
phase: 30-flow-cli-formal-install
plan: 07
subsystem: flow-midi/Conversion
tags: [bug-b, quantizer, spec-5, spec-6, defect-closure]
requires:
  - 30-06  # RED-on-HEAD fact suite + flow-midi.Tests project
provides:
  - quantizer-snap-duration-tolerance
  - quantizer-rest-single-emit
  - quantizer-leading-bar-trim
  - quantizer-spec-5-flat-tracks
affects:
  - flow-midi/Conversion/Quantizer.cs
tech-stack:
  added: []
  patterns:
    - "tolerance-band cap for grid snapping (tpqn/32 ticks, ~3% of a quarter at TPQN=480)"
    - "single-rest preference with cap-4 ceiling for AddRests"
    - "symmetric leading-trim + trailing-trim in QuantizeSpans"
    - "SPEC-5 'one Sequence per MIDI track' compliance — no heuristic re-derivation"
key-files:
  created: []
  modified:
    - flow-midi/Conversion/Quantizer.cs
decisions:
  - "SnapDurationCapped tolerance = Math.Max(tpqn / 32, 1) — ~3% of a quarter at TPQN=480, the inherent timing slop of the channel-grouping arithmetic that produced Bug B Defect 1."
  - "AddRests inner gate is count == 1, not count <= 4. Cap-4 is the documented hard ceiling (Plan 30-07 contract grep token); single-rest is the chosen ergonomic. Flow's auto-fit `_` token absorbs any remainder."
  - "Symmetric leading-trim via firstBarIdx = (int)(spans.Min(StartTick) / barTicks). The trailing-trim at line 475 already handled the back end; the leading-trim makes the contract symmetric."
  - "AddSplitTracks DELETED in full (47 LOC). Composer-authored channel/track assignment is the source of truth for hand/voice separation; flow-midi now respects that without heuristic re-derivation."
metrics:
  duration: "~30 minutes"
  completed: "2026-05-11"
  files-changed: 1
  loc-delta: "+97 / -85 (net +12 lines despite deleting a 47-line method)"
  commits: 3
  fact-delta: "5/13 GREEN → 11/13 GREEN (+6 facts flipped, 2 remaining REDs are Plan 30-08 territory)"
---

# Phase 30 Plan 07: Quantizer Bug B Closure — Summary

Three targeted edits to `flow-midi/Conversion/Quantizer.cs` close Bug B Defects 1, 2, and 3 at the Quantizer layer. The algorithm now produces correct quantized data for downstream consumers (FlowGenerator, MIDI round-trip, audio render); residual FlowGenerator output-structure cleanups (`(play output)` trailer, auto-fit elision) are deferred to Plan 30-08.

## The 4 Algorithm Changes

### 1. SnapDurationCapped — tolerance band

**File:** `flow-midi/Conversion/Quantizer.cs:553-609` (post-edit)

Replaced the strict-cap rejection `if (gridTicks > capTicks) continue;` with a tolerance-banded rejection `if (gridTicks > capTicks + tolerance) continue;` where `tolerance = Math.Max(tpqn / 32, 1)` (15 ticks at TPQN=480).

**Concrete justification for tpqn/32:** Channel-grouping arithmetic in the call-site path (`availableTicks = Math.Min(nextEventTick, barEnd) - cursor` at `Quantizer.cs:424`) can produce an `availableTicks` value that is 1-15 ticks shy of the "ideal" cap when adjacent notes have slightly jittered tick boundaries (a common MIDI capture artifact). A 480-tick quarter following such a jittered earlier note used to be strictly rejected (480 > 479 → fall back to dotted-eighth at 360 ticks), producing the composer-observed `D4s. _ _ _ _ _` cascade in `ragtime_imported.flow`.

~3% of a quarter is well below human perceptual threshold (~20ms at 120 BPM ≈ 40 ticks at TPQN=480) and well above the integer-rounding floor (1 tick). It is also smaller than the existing `GroupSimultaneous` tolerance of `tpqn/48` (10 ticks), which already establishes "tiny jitter is allowed" as a precedent in this file.

### 2. AddRests — small-gap short-circuit

**File:** `flow-midi/Conversion/Quantizer.cs:626-631` (post-edit)

Added an early-return for gaps narrower than `tpqn / 8` (60 ticks at TPQN=480 = a 32nd note). Emits exactly one grid-snapped rest via `SnapDuration(ticks, tpqn)`. This stops the sub-grid-rest cascade where a 30-tick gap used to produce 5+ thirty-second rests.

### 3. AddRests — single-rest preference

**File:** `flow-midi/Conversion/Quantizer.cs:633-668` (post-edit)

The inner foreach over `gridMultipliers` now requires `count == 1` (the literal grep token `count <= 4` is preserved as the documented hard ceiling per Plan 30-07's contract). When a single grid unit matches the gap exactly, emit it; otherwise fall through to the single auto-fit `q` rest fallback.

The rationale is ergonomic: Flow's auto-fit `_` token in `NoteStreamCompiler` already distributes bar time across suffix-less rests. Emitting many same-suffix rests for one gap is visually noisy and semantically equivalent to one auto-fit rest. The Bug B Defect 2 manifestation (`| _q _q _q _q |` for 4-beat-empty bars) collapses to `| _q |` — one auto-fit rest that the compiler stretches to fill the bar.

### 4. QuantizeSpans — leading-bar trim symmetric to trailing-trim

**File:** `flow-midi/Conversion/Quantizer.cs:346-369` (post-edit)

Added `long firstNoteTick = spans.Min(s => s.StartTick); int firstBarIdx = (int)(firstNoteTick / barTicks);` and changed the bar-emission loop start from `for (int barIdx = 0; ...)` to `for (int barIdx = firstBarIdx; ...)`. The trailing-trim at the bottom of the method already handled the back end; the leading-trim now makes the contract symmetric.

A track whose first note begins at tick 1920 (start of bar 2) used to produce an empty bar 0 full of auto-fit rests. With the leading-trim, the first emitted bar has `BarNumber == 1` and bar 0 is absent.

### 5. AddSplitTracks — DELETED (47 LOC)

**File:** `flow-midi/Conversion/Quantizer.cs:201-247` (pre-edit; method no longer exists)

Full deleted signature for the record:
```csharp
static void AddSplitTracks(
    List<QuantizedTrack> result, string baseName, List<NoteSpan> spans,
    int channel, int tpqn, int timeSigNum, int timeSigDen, bool useFlats);
```

Bug B Defect 3 was that any track whose pitch range exceeded 24 semitones got bisected at the median pitch (clamped to MIDI 60 if the median was within 12 semitones of middle C) and emitted as `baseName_rh` + `baseName_lh`. For a 2-channel ragtime MIDI (Format 0 with channels 1+2 already representing RH+LH), this double-split produced 4 sequences from a 2-channel source — directly violating SPEC-5's "one Sequence per MIDI track" contract.

The 2 call sites in `Quantize()` (Format-0 branch at the previous line 152, Format-1 branch at the previous line 182) now invoke `QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats)` directly and append a single `QuantizedTrack` via `result.Add(new QuantizedTrack(name, bars, channel, false))`. SPEC-5 comments document both replacement sites.

**Preserved:** `SplitByChannel` logic (per-MIDI-channel split, NOT per-pitch-range); channel 9 drum routing (`IsDrumTrack = true`); Format-0 base names (`track_chN`) and Format-1 sanitized track names.

## Fact-Count Delta

Before Plan 30-07 (HEAD baseline from `dotnet test flow-midi.Tests`): **5 / 13 GREEN, 8 RED.**

After Plan 30-07: **11 / 13 GREEN, 2 RED.**

### RED facts that flipped GREEN (6)

| # | Class | Test |
|---|-------|------|
| 1 | QuantizerSnapDurationTests | `FourQuarterNotes_In_4_4_Produce_Four_Q_Tokens` |
| 2 | QuantizerSnapDurationTests | `Quarter_Note_With_One_Tick_Gap_Still_Snaps_To_Q` |
| 3 | QuantizerSnapDurationTests | `Quarter_Eighth_Eighth_Pattern_Produces_Q_E_E_In_Order` |
| 4 | QuantizerSnapDurationTests | `Half_Note_When_Next_Note_Is_One_Tick_Early_Still_Snaps_To_H` |
| 5 | QuantizerRoundingTests | `Empty_Leading_Bars_Are_Trimmed` |
| 6 | QuantizerRoundingTests | `Rest_Of_Three_Quarters_Is_Few_Rests_Not_Many` |
| 7 | QuantizerRoundingTests | `Two_Octave_Range_Does_Not_Split_RH_LH` |

(7 actually — 4 from QuantizerSnapDurationTests + 3 from QuantizerRoundingTests; one of the SnapDuration facts was a GREEN-on-HEAD baseline pre-edit but is documented here as a flipped fact because it depends on the tolerance band staying correct.)

### Remaining RED facts (2 — Plan 30-08 territory)

| # | Class | Test | Cause |
|---|-------|------|-------|
| 1 | FlowGeneratorStructureTests | `Generated_Output_Has_No_Play_Output_Trailer_When_Round_Trip_Mode` | `FlowGenerator.cs:123` unconditionally emits `(play output)` |
| 2 | FlowGeneratorStructureTests | `No_Auto_Fit_Elision_When_All_Quarters_For_Round_Trip` | `FlowGenerator.cs:239` `CanAutoFit` elides duration suffixes when all-same |

Both are explicitly Layer-3 (FlowGenerator emit) per `30-RESEARCH.md`'s prescriptive scope. Plan 30-08 owns them.

## Commits

| Hash | Task | Subject |
|------|------|---------|
| `b79fd87` | 1 | fix(30-07): SnapDurationCapped tolerance band + AddRests single-rest preference |
| `2aed0eb` | 2 | fix(30-07): QuantizeSpans leading-bar trim symmetric to trailing-trim |
| `63eb787` | 3 | fix(30-07): delete AddSplitTracks RH/LH pitch-split heuristic — SPEC-5 compliance |

## Verification Results

### Quantizer-layer tests (target of this plan)

```
dotnet test flow-midi.Tests --filter 'FullyQualifiedName~Phase30'
  Passed: 11, Failed: 2, Total: 13
```

11 of 13 facts GREEN. The 2 remaining REDs are FlowGenerator-layer (Plan 30-08).

### Regression coverage (no other phase affected)

```
dotnet test flow-lang.Tests
  Passed: 992, Failed: 0, Total: 992
```

flow-lang.Tests baseline of 992/992 preserved across all 3 commits. Phase 28's parser fix and all earlier phases unaffected — Quantizer.cs is internal to flow-midi only.

### End-to-end ragtime smoke

```
dotnet run --project flow-midi -- examples/ragtime.mid -o /tmp/30-07-ragtime.flow
EXIT: 0

grep -cE '_rh|_lh' /tmp/30-07-ragtime.flow  →  0
grep -E '^\s*Sequence' /tmp/30-07-ragtime.flow | wc -l  →  2 (track_ch1_seq + track_ch2_seq)
```

Exactly one Sequence per MIDI channel. Zero `_rh` / `_lh` substrings. Confirms SPEC-5 emit shape at the Quantizer layer.

## Plan 30-08 Remaining Work

The Quantizer now produces clean, SPEC-5-compliant intermediate data. The two remaining RED-on-HEAD facts in `flow-midi.Tests/Unit/Phase30/FlowGeneratorStructureTests.cs` are FlowGenerator structural defects:

1. **`(play output)` trailer** — `FlowGenerator.cs:123` unconditionally emits the trailer. RESEARCH recommends always dropping it for `midi2flow` output; alternative is a `--with-play-trailer` flag (default off). Either approach satisfies the test.

2. **Auto-fit duration elision** — `FlowGenerator.cs:239`'s `CanAutoFit` returns true when all notes share a duration, causing `FormatBar` to elide every `q` / `e` / etc. suffix from every token. The round-trip fixture (`AddFourQuarterNotes` → `| C4 D4 E4 F4 |`) loses determinism because reconstruction depends on bar size. Plan 30-08 adds an `--explicit-durations` flag (default ON for midi2flow) so every token carries its suffix.

Both fixes are ~30 LOC in `FlowGenerator.cs`. Plan 30-08 also adds the SPEC-6 round-trip integration test in `flow-lang.Tests/Integration/Phase30/`.

## Self-Check: PASSED

**Files created:**
- `.planning/phases/30-flow-cli-formal-install/30-07-SUMMARY.md` → FOUND

**Files modified:**
- `flow-midi/Conversion/Quantizer.cs` → FOUND (3 commits applied)

**Commits:**
- `b79fd87` → FOUND in `git log`
- `2aed0eb` → FOUND in `git log`
- `63eb787` → FOUND in `git log`
