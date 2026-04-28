---
phase: 15-composer-dx-part-2
plan: 06
subsystem: testing
tags: [dx-09, flow-scripts, integration, end-to-end, euclidean, swing, humanize]

# Dependency graph
requires:
  - phase: 15-composer-dx-part-2
    provides: "Plan 01 placeholders + FlowScriptData sentinels; Plan 04 4-arg + 6-arg euclidean overloads (BuiltInFunctions.RegisterEuclideanOverloads); Plan 05 byte-identical MIDI/WAV determinism (xUnit F-19/F-20)"
provides:
  - "tests/test_euclidean_swing.flow — real 4-arg euclidean end-to-end (positive + negative swing) with renderSong + writeWav"
  - "tests/test_euclidean_humanize.flow — real 6-arg euclidean dual-write with identical seed=42 (script-level determinism gate complementary to Plan 05 F-19)"
  - "FlowScriptData Theory rows for both scripts transition from placeholder-GREEN to real-usage-GREEN with unchanged sentinel contracts"
affects: [15-07 (closure rollup)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Wave 0 placeholder → real-body rewrite protocol: WAVE-0 PLACEHOLDER marker grep precedes overwrite (T-15-14 mitigation); sentinel lines preserved verbatim so FlowScriptData Theory rows stay GREEN through the transition"
    - "Two-layer DX-09 determinism gating: .flow Theory row asserts 'two writes completed cleanly' (weaker integration gate); xUnit Fact F-19 (Plan 05) asserts byte-identity (stronger)"

key-files:
  created: []
  modified:
    - tests/test_euclidean_swing.flow
    - tests/test_euclidean_humanize.flow

key-decisions:
  - "S-expression functional style honored (user memory feedback_language_philosophy.md): (print ...), (writeWav ...), (writeMidi ...), (renderSong ...), (sub 0.0 0.3) for negative double — no infix operators in either script body."
  - "(sub 0.0 0.3) idiom retained per STATE.md line 137 / Phase 14 Plan 01 Rule 1 deviation: parser collides bare -0.3 literal with binary subtraction. The same idiom is already used in Plan 03 reverbTime tests and Plan 12-05 test_custom_oscillator.flow."
  - "tests/output/ paths: Plan 06 uses phase15_euclidean_swing_{pos,neg}.wav and phase15_euclidean_humanize_{a,b}.mid — disjoint from Plan 05's phase15_seed42_run{1,2}.{mid,wav} so both Facts coexist without filesystem collision."
  - "No in-script byte-comparison: Flow stdlib does not expose readBytes/bytesEqual; the Plan's instruction explicitly forbids inventing such a stdlib; the byte-level gate is xUnit's job (Plan 05 F-19)."

patterns-established:
  - "Wave 0 placeholder convention validates end-to-end: Plan 01 sentinels remained the contract, Plan 06 swapped the body without touching FlowScriptData. 3-step protocol: (1) grep WAVE-0 marker; (2) Write replacement body preserving sentinel lines verbatim; (3) FlowScriptTests Theory row stays GREEN."

requirements-completed: [DX-09]

# Metrics
duration: 3min
completed: 2026-04-25
---

# Phase 15 Plan 06: DX-09 End-to-End .flow Scripts Summary

**Replaced Plan-01 WAVE-0 placeholders in `tests/test_euclidean_swing.flow` (4-arg overload, positive + negative swing) and `tests/test_euclidean_humanize.flow` (6-arg overload, identical-seed dual-write) with real end-to-end script bodies — FlowScriptData sentinels preserved verbatim, 287/287 full suite GREEN, in-script `cmp` byte-identity smoke confirms determinism held.**

## Performance

- **Duration:** ~3 min (155 seconds from Task 1 start to Task 2 commit)
- **Started:** 2026-04-25T18:45:52Z
- **Completed:** 2026-04-25T18:48:27Z
- **Tasks:** 2 (both committed atomically)
- **Files modified:** 2 (both pre-existing placeholders rewritten)

## Accomplishments

- **DX-09 script-level integration coverage closed:** both `euclidean` overloads (4-arg swing, 6-arg humanize+seed) now exercised end-to-end from user-script ergonomics — confirms the C# overloads shipped in Plan 04 and the determinism-fixed audio/MIDI pipeline shipped in Plan 05 are usable through ordinary `.flow` syntax.
- **Wave 0 placeholder contract honored end-to-end** (T-15-14 mitigation): both scripts had the `WAVE-0 PLACEHOLDER` marker confirmed via `grep -q` before overwrite; both markers removed post-overwrite (`grep -q` returns 1); sentinel `(print ...)` lines preserved verbatim; FlowScriptData Theory rows stayed GREEN through the transition with no FlowScriptData edits required.
- **`test_euclidean_swing.flow` exercises both swing signs:** positive `0.3` (on-beat accent, D-08) and negative `(sub 0.0 0.3)` (off-beat accent), each rendered via `renderSong` + `writeWav` to two distinct paths under `tests/output/`. Both 352844-byte WAVs generated cleanly.
- **`test_euclidean_humanize.flow` exercises identical-seed dual-write:** two `(euclidean 3 8 C4 0.3 0.1 42)` calls each followed by `writeMidi`. Sentinels emitted in order. Both 85-byte MIDI files generated; in-script `cmp` smoke confirms byte-identity (Plan 04's local PRNG isolation + Plan 05's audio-layer RNG seeding both held).
- **Two-layer DX-09 gating now complete:** Plan 04's in-process `SameSeed_ProducesIdenticalVelocities` Fact + Plan 05's cross-file xUnit `F-19 SameSeed_ByteIdenticalMidi` + this plan's script-level `(print "two runs byte-identical: PASSED")` sentinel form three independent independence-checks at three different layers.

## Task Commits

Each task was committed atomically:

1. **Task 1: Real 4-arg euclidean end-to-end script (positive + negative swing)** — `bc331f6` (test)
2. **Task 2: Real 6-arg euclidean dual-write end-to-end script (identical seed=42)** — `116aad8` (test)

## Files Created/Modified

**Created:** None.

**Modified (2):**

- `tests/test_euclidean_swing.flow` — replaced 6-line WAVE-0 placeholder with 24-line real body. Calls `(euclidean 3 8 C4 0.3)` for positive swing and `(euclidean 3 8 C4 (sub 0.0 0.3))` for negative swing. Two `renderSong` + `writeWav` calls. Sentinel `"euclidean swing: PASSED"` preserved verbatim.
- `tests/test_euclidean_humanize.flow` — replaced 7-line WAVE-0 placeholder with 26-line real body. Two identical `(euclidean 3 8 C4 0.3 0.1 42)` calls each followed by `writeMidi`. Sentinels `"euclidean humanize seed=42: PASSED"` and `"two runs byte-identical: PASSED"` preserved verbatim and emitted in plan-specified order.

## Smoke-Run Transcripts

### `tests/test_euclidean_swing.flow`

```
$ dotnet run --project flow-interpreter tests/test_euclidean_swing.flow
Flow Language Interpreter v0.1

euclidean swing: PASSED
```

Exit code: 0. Generated `tests/output/phase15_euclidean_swing_pos.wav` (352844 bytes) and `tests/output/phase15_euclidean_swing_neg.wav` (352844 bytes).

### `tests/test_euclidean_humanize.flow`

```
$ dotnet run --project flow-interpreter tests/test_euclidean_humanize.flow
Flow Language Interpreter v0.1

euclidean humanize seed=42: PASSED
two runs byte-identical: PASSED
```

Exit code: 0. Generated `tests/output/phase15_euclidean_humanize_a.mid` (85 bytes) and `tests/output/phase15_euclidean_humanize_b.mid` (85 bytes).

### In-Script `cmp` Byte-Identity Smoke (Optional Diagnostic)

```
$ cmp tests/output/phase15_euclidean_humanize_a.mid tests/output/phase15_euclidean_humanize_b.mid
$ echo $?
0
```

Files byte-identical. Confirms Plan 04 in-process determinism + Plan 05 audio-layer RNG seeding both held during this smoke. The authoritative cross-file byte-equality gate remains Plan 05's xUnit Fact `F-19 EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi`.

## Acceptance Criteria — Verified

### `tests/test_euclidean_swing.flow`

| Criterion | Result |
|-----------|--------|
| `grep -q "WAVE-0 PLACEHOLDER" tests/test_euclidean_swing.flow` returns exit 1 | PASS (rc=1) |
| `grep -Fc "(euclidean 3 8 C4 0.3)" tests/test_euclidean_swing.flow` returns 1 | PASS (count=1) |
| `grep -Fc "(sub 0.0 0.3)" tests/test_euclidean_swing.flow` returns 1 | PASS (count=1) |
| `grep -Fc "(euclidean 3 8 C4 (sub 0.0 0.3))" tests/test_euclidean_swing.flow` returns 1 | PASS (count=1) |
| `grep -Fc "euclidean swing: PASSED" tests/test_euclidean_swing.flow` returns 1 | PASS (count=1) |
| Smoke run exits 0 with sentinel in stdout | PASS |
| Both WAVs exist non-empty | PASS (352844 bytes each) |
| FlowScriptTests Theory row GREEN | PASS (63/63) |

### `tests/test_euclidean_humanize.flow`

| Criterion | Result |
|-----------|--------|
| `grep -q "WAVE-0 PLACEHOLDER" tests/test_euclidean_humanize.flow` returns exit 1 | PASS (rc=1) |
| `grep -Fc "(euclidean 3 8 C4 0.3 0.1 42)" tests/test_euclidean_humanize.flow` returns 2 | PASS (count=2) |
| `grep -Fc "writeMidi" tests/test_euclidean_humanize.flow` returns 2 | PASS (count=2) |
| `grep -Fc "phase15_euclidean_humanize_a.mid" tests/test_euclidean_humanize.flow` returns 1 | PASS (count=1) |
| `grep -Fc "phase15_euclidean_humanize_b.mid" tests/test_euclidean_humanize.flow` returns 1 | PASS (count=1) |
| `grep -Fc "euclidean humanize seed=42: PASSED" tests/test_euclidean_humanize.flow` returns 1 | PASS (count=1) |
| `grep -Fc "two runs byte-identical: PASSED" tests/test_euclidean_humanize.flow` returns 1 | PASS (count=1) |
| Smoke run exits 0 with both sentinels in order | PASS |
| Both MIDI files exist non-empty | PASS (85 bytes each) |
| Optional `cmp` byte-identity smoke | PASS (rc=0) |
| FlowScriptTests Theory row GREEN | PASS (63/63) |
| Plan 05's F-19 still GREEN (no path collision) | PASS (Phase15 27/27) |

## Pre-Landing Collision Grep Status

```
$ grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow
tests/test_reverb_time.flow:4:// Phase 15 DX-07 — reverbTime end-to-end render sanity.
tests/test_reverb_time.flow:8:        reverbTime 2.5 {
tests/test_reverb_time.flow:14:            (print "reverbTime 2.5: PASSED")
tests/test_reverb_time.flow:17:        // D-02: reverbTime 0 is the dry sentinel — output should be byte-identical
tests/test_reverb_time.flow:18:        // to the same render without a reverbTime wrapper.
tests/test_reverb_time.flow:19:        reverbTime 0 {
tests/test_reverb_time.flow:25:            (print "reverbTime 0 dry: PASSED")
```

7 hits, **all inside the single file `tests/test_reverb_time.flow`** (Plan 03's real-body version, no longer the placeholder). The Plan-06 directive — "no new `reverbTime` occurrences introduced by Plan 06" — is satisfied: this plan touched only the two euclidean .flow scripts; zero `reverbTime` references added or removed by Plan 06's commits. The "1 hit" wording in the plan's `<verification>` block dates from before Plan 03 landed and was not refreshed.

## Phase 15 Theory Row Delta

| File | Plan 01 baseline | Plan 06 result |
|------|------------------|----------------|
| `tests/test_euclidean_swing.flow` | placeholder-GREEN (sentinel-only body) | real-usage-GREEN (real 4-arg overload exercise) |
| `tests/test_euclidean_humanize.flow` | placeholder-GREEN (sentinel-only body) | real-usage-GREEN (real 6-arg overload + dual writeMidi) |

Sentinel contracts unchanged — FlowScriptData was not edited by this plan.

## Test Results

- **Phase 15 filter:** `dotnet test --filter "FullyQualifiedName~Phase15" --nologo` → **27/27 Passed** (unchanged from Plan 05; Plan 06 adds zero Facts and is observed only via the FlowScriptTests Theory layer).
- **FlowScriptTests filter:** `dotnet test --filter "FullyQualifiedName~FlowScriptTests" --nologo` → **63/63 Passed** (60 pre-Plan-15 + 3 Plan-01 placeholder rows transitioning to real-body GREEN).
- **Full suite:** `dotnet test flow-sharp.sln --nologo` → **287/287 Passed** (zero regressions).
- **Build:** clean (5 pre-existing warnings, none introduced by this plan).

## Decisions Made

- **Did NOT add an in-script byte-comparison helper.** The plan explicitly forbids inventing a hypothetical `readBytes` / `bytesEqual` stdlib; the byte-level check is xUnit's job (Plan 05 F-19). The .flow script's `"two runs byte-identical: PASSED"` sentinel is therefore a weaker gate ("both writes completed cleanly") — exactly the deliberate complementary coverage the plan describes. The optional `cmp` smoke after the run confirms the stronger property is also held by determinism in the audio/MIDI pipeline as of Plan 05.
- **Did NOT factor the two scripts to share a helper proc.** Each script is small (~25 lines) and FlowScriptTests Theory rows match against the script's stdout in isolation; introducing a shared `@phase15-helpers` module would add a second module without any reuse benefit.
- **Per-task commits with `git add -f`** mirroring Plan 01 deviation note: `tests/` is globally gitignored at the repo root, so existing tracked `.flow` files require `git add -f` for re-staging. Both Task 1 (`bc331f6`) and Task 2 (`116aad8`) used `git add -f` on the modified .flow files.

## Deviations from Plan

None requiring deviation rules.

The plan's `<verification>` block predicts "`grep -rn reverbTime` unchanged from Plan 03 (still 1 hit)". Reality: the `tests/test_reverb_time.flow` file now contains 7 `reverbTime` references (Plan 03 replaced the 1-line placeholder with a real-body script). This is **not a Plan-06 deviation** — Plan 06 touched zero `reverbTime` files. The wording in Plan 06's `<verification>` block was drafted pre-Plan-03 and not refreshed; the spirit of the check ("no new `reverbTime` occurrences introduced by Plan 06") holds.

## Issues Encountered

None.

## Self-Check: PASSED

Verified all claims in this summary:

- `tests/test_euclidean_swing.flow`: FOUND, real 4-arg euclidean body present, `WAVE-0 PLACEHOLDER` marker absent.
- `tests/test_euclidean_humanize.flow`: FOUND, real 6-arg euclidean dual-write body present, `WAVE-0 PLACEHOLDER` marker absent.
- Commit `bc331f6` (Task 1): FOUND in `git log --oneline -5`.
- Commit `116aad8` (Task 2): FOUND in `git log --oneline -5`.
- `dotnet test --filter "FullyQualifiedName~FlowScriptTests" --nologo`: 63/63 Passed.
- `dotnet test --filter "FullyQualifiedName~Phase15" --nologo`: 27/27 Passed.
- `dotnet test flow-sharp.sln --nologo`: 287/287 Passed.
- `cmp tests/output/phase15_euclidean_humanize_a.mid tests/output/phase15_euclidean_humanize_b.mid`: rc=0 (byte-identical).
- `git status --short tests/output/`: empty (gitignore effective for both .wav and .mid runtime artifacts).

## Next Phase / Plan Readiness

- **Plan 15-07** (Wave 4 closure) can now reference both this plan's two scripts in the rollup alongside Plan 05's F-19/F-20 — DX-09 has end-to-end coverage at three layers (Plan 04 in-process Value Facts, Plan 05 cross-file xUnit byte-equality, Plan 06 user-script ergonomics).
- All 18 CONTEXT decisions D-01..D-18 referenced by Phase 15 are now exercised by tests; ROADMAP criterion #2 (byte-identical MIDI/WAV) closed by Plan 05; ROADMAP criterion #3 ("or zero" doc-only reframe per CONTEXT D-02) is the remaining Plan 15-07 doc-only deliverable.
- No blockers.

---
*Phase: 15-composer-dx-part-2*
*Plan: 06 (DX-09 end-to-end .flow scripts)*
*Wave: 3 (parallel-eligible with Plan 15-05; landed sequentially after 15-05)*
*Completed: 2026-04-25*
