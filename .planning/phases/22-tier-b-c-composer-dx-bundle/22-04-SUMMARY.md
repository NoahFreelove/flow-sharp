---
phase: 22-tier-b-c-composer-dx-bundle
plan: 04
subsystem: audio
tags: [delay, tempo-sync, musical-context, overload, charitable-interpretation, dx-12]

requires:
  - phase: 14-composer-dx-part-1
    provides: existing ms-rate delay(Buffer, Double, Double, Double) registration in EffectsFunctions
  - phase: 15-composer-dx-part-2
    provides: RegisterContextDependentFunctions wiring template (RegisterEuclideanOverloads)
  - phase: 18-foundation-rational-duration-arithmetic
    provides: byte-identical regression gate (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI)
  - phase: 22-tier-b-c-composer-dx-bundle
    provides: 22-01 sibling-overload registration pattern; 22-02 Wave 0 RED stub pattern
provides:
  - "delay(Buffer, NoteValue, Double, Double) overload synced to MusicalContext.Tempo"
  - "EffectsFunctions.NoteValueToMs(NoteValueType.Value, double) -> double helper"
  - "EffectsFunctions.RegisterContextDependent(InternalFunctionRegistry, ExecutionContext) entry point"
  - "Tempo fallback to 120 BPM when no tempo block active (?? 120.0 pattern)"
  - "Charitable D-07: out-of-range NoteValue enum falls through switch default to quarterMs (no exception)"
affects: [22-05-quantize, 22-06-legato-portamento, 22-07-closure]

tech-stack:
  added: []
  patterns:
    - "Sibling-overload registration: NoteValue overload registered context-dependently while existing ms-rate overload stays in stateless Register path (preserves byte-identical regression)"
    - "Closure captures ExecutionContext to read MusicalContext.Tempo fresh per call (matches Interpreter.cs:200/210 pattern)"
    - "Convergence at DSP layer: both delay overloads call DSP.Delay.Apply — single regression-stable boundary"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs
    - tests/test_dx_delay_sync.flow
  modified:
    - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "DX-12 NoteValue overload registered context-dependently (RegisterContextDependent) while existing ms-rate overload stays in stateless Register path — closure capture of ExecutionContext is the only way to read MusicalContext.Tempo fresh per call"
  - "NoteValueToMs is public static (not internal) because flow-lang and flow-lang.Tests are separate assemblies with no InternalsVisibleTo configured — same convention as FileIO.VarispeedResample / FileIO.LoadWavSemitones (22-02)"
  - "Both delay overloads converge at DSP.Delay.Apply — RegisterDelay/DelayEffect bytes are byte-identical to pre-DX-12 (regression gate)"
  - "120 BPM fallback via `context.GetMusicalContext().Tempo ?? 120.0` matches Interpreter.cs:200,210 pattern; works inside or outside a tempo block"
  - "Test 9 (Pitfall 1) corrected mid-task: bare-Int (delay buf 250 0.5 0.4) is AMBIGUOUS in v1.3 — OverloadResolver flags ambiguity rather than picking a side. Fact pins observed behavior; if a future plan disambiguates, the assertion goes RED requiring an explicit decision (Rule 1 deviation)"
  - "Out-of-range NoteValue enum falls through switch `_ => quarterMs` (charitable D-07, threat T-22-V5-14 mitigation: no exception, no crash)"

patterns-established:
  - "Pattern: context-dependent effect registration — when an audio effect needs MusicalContext state, register it via a sibling RegisterContextDependent method on the same Functions class, wired from BuiltInFunctions.RegisterContextDependentFunctions alongside RegisterEuclideanOverloads. Existing stateless registrations stay untouched"
  - "Pattern: convergence at DSP layer — overload variants on the same effect (ms-rate vs NoteValue-rate delay) compute their inputs differently but call the same DSP.Apply method, giving one regression-stable boundary instead of two"

requirements-completed: [DX-12]

duration: 6min
completed: 2026-05-02
---

# Phase 22 Plan 04: DX-12 NoteValue-Rate Delay Sync Summary

**`delay(Buffer, NoteValue, Double, Double)` reads `MusicalContext.Tempo` at call time so EIGHTH at 120 BPM = 250ms; existing ms-rate overload stays byte-identical via convergence at `DSP.Delay.Apply`.**

## Performance

- **Duration:** ~6 min (341 s wall clock)
- **Started:** 2026-05-02T19:09:40Z
- **Completed:** 2026-05-02T19:15:21Z
- **Tasks:** 3 (RED + GREEN + verify)
- **Files modified:** 6 (2 created, 4 modified)

## Accomplishments

- DX-12 closed: `delay(Buffer, NoteValue, Double, Double)` registered context-dependently and synced to `MusicalContext.Tempo`
- `EffectsFunctions.NoteValueToMs(NoteValueType.Value, double)` helper computes ms from NoteValue + BPM (WHOLE=4×qtr, HALF=2×qtr, QUARTER=qtr, EIGHTH=qtr/2, SIXTEENTH=qtr/4, THIRTYSECOND=qtr/8; charitable fallback to qtr for out-of-range enums)
- `EffectsFunctions.RegisterContextDependent` wired from `BuiltInFunctions.RegisterContextDependentFunctions` (sibling of `RegisterEuclideanOverloads`)
- Closure captures `ExecutionContext` so the active tempo is read fresh per call — works inside `tempo X { ... }` blocks AND outside (defaults to 120 BPM via `?? 120.0`)
- Existing ms-rate `RegisterDelay` + `DelayEffect` bodies untouched (byte-identical regression gate; both overloads converge at `DSP.Delay.Apply`)
- 9 DelaySyncFacts GREEN; `tests/test_dx_delay_sync.flow` exits 0 with `DX-12 delay sync: PASSED` sentinel
- **Strongest possible byte-identity evidence:** `cmp tests/output/dx_delay_eighth_120.wav tests/output/dx_delay_msrate.wav` returns exit 0 (IDENTICAL bytes — `(delay src EIGHTH 0.5 0.4)` at tempo 120 produces the same WAV as `(delay src 250.0 0.5 0.4)`)
- ByteIdentical regression gate **6/6** GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI)
- Full test suite **464/464** GREEN — zero regressions (was 454/454 at 22-03 close + 9 new DelaySyncFacts + 1 new sentinel theory row = 464)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 RED — Failing DelaySyncFacts + DX-12 smoke** — `9e175b1` (test)
   - 9 xUnit Facts: 5 direct `NoteValueToMs` math (RED), 1 ms-rate regression GREEN baseline (Test 6), 2 engine-eval through tempo blocks (RED), 1 Pitfall 1 ambiguity Fact (RED at Task 1)
   - `tests/test_dx_delay_sync.flow` smoke script with `DX-12 delay sync: PASSED` sentinel
   - `flow-lang.Tests/FlowScriptData.cs` sentinel entry
   - `EffectsFunctions.NoteValueToMs` Wave 0 RED stub returning `0.0` (public for cross-assembly Facts)
2. **Task 2: Wave 3 GREEN — Implement DX-12 NoteValue overload** — `98da48e` (feat)
   - `EffectsFunctions.NoteValueToMs`: switch dispatch over `NoteValueType.Value`
   - `EffectsFunctions.RegisterContextDependent`: registers `delay(Buffer, NoteValue, Double, Double)` closure reading `context.GetMusicalContext().Tempo ?? 120.0`
   - `BuiltInFunctions.RegisterContextDependentFunctions`: wires `EffectsFunctions.RegisterContextDependent` alongside `RegisterEuclideanOverloads`
   - `audio.flow`: sibling `internal proc delay (Buffer, NoteValue, Double, Double)` declaration
   - DelaySyncFacts Test 9 corrected mid-task to pin OBSERVED ambiguous-overload behavior
   - All 9 DelaySyncFacts flipped GREEN
3. **Task 3: Wave 3 — Smoke run + byte-identical regression gate** — `0129ec4` (chore, verification-only empty commit)
   - `dotnet run --project flow-interpreter tests/test_dx_delay_sync.flow` → exit 0, sentinel printed
   - DelaySyncFacts 9/9 GREEN; ByteIdentical 6/6 GREEN; full suite 464/464 GREEN
   - WAV byte-identity verified: `dx_delay_eighth_120.wav` == `dx_delay_msrate.wav` (cmp exit 0)

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs` (created) — 9 xUnit Facts: 5 direct `NoteValueToMs` math assertions (EIGHTH at 120, QUARTER at 120, EIGHTH at 240, WHOLE at 60, SIXTEENTH at 120) + 4 engine-eval Facts (ms-rate regression, NoteValue smoke, no-tempo defaults to 120 BPM, Pitfall 1 ambiguity documentation)
- `tests/test_dx_delay_sync.flow` (created) — Smoke: synth 0.5s sine → `tempo 120 { (delay src EIGHTH 0.5 0.4) }` → `tempo 240 { (delay src EIGHTH 0.5 0.4) }` → ms-rate `(delay src 250.0 0.5 0.4)`, writes 3 WAVs
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` (modified) — Added `using FlowLang.TypeSystem.SpecialTypes;` for `NoteValueType` access; added `NoteValueToMs` public static helper (switch math); added `RegisterContextDependent` public static method registering the new overload context-dependently. Existing `RegisterDelay`, `delaySig`, `DelayEffect`, and all other effects (reverb, filters, compress, gain, sidechain) UNTOUCHED (byte-identity invariant)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modified) — Single one-line addition in `RegisterContextDependentFunctions`: `Audio.EffectsFunctions.RegisterContextDependent(registry, context);` immediately after `RegisterEuclideanOverloads` (sibling pattern)
- `flow-lang/audio.flow` (modified) — Added sibling `internal proc delay(Buffer, NoteValue, Double, Double)` declaration immediately after the existing ms-rate one, with explanatory comment
- `flow-lang.Tests/FlowScriptData.cs` (modified) — `RequiredSentinels` entry for `test_dx_delay_sync.flow` pinning the `DX-12 delay sync: PASSED` sentinel

## Decisions Made

- **Context-dependent registration over stateless**: The NoteValue overload MUST read `MusicalContext.Tempo` at call time (not at registration time), so registration goes through `RegisterContextDependent` (closure captures `ExecutionContext`). The existing ms-rate overload stays in the stateless `Register` path — no churn for the regression gate. This mirrors the `RegisterEuclideanOverloads` pattern from Phase 15.
- **`NoteValueToMs` is `public static`** (not `internal`): Cross-assembly visibility is required for Facts in `flow-lang.Tests` to call the helper directly. No `InternalsVisibleTo` is configured for the project, and adding one for a single helper would expand the public surface unnecessarily. The same convention is used by `FileIO.VarispeedResample` (22-02) and `FileIO.LoadWavSemitones` (22-02) — all helpers consumed by Facts are `public`.
- **Both overloads converge at `DSP.Delay.Apply`**: Rather than duplicating the buffer allocation and feedback loop, the NoteValue overload computes `delayMs` from NoteValue+BPM and then delegates to the same DSP routine the ms-rate path uses. This gives ONE regression-stable boundary (the DSP routine) instead of TWO. Byte-identity verified by `cmp` on the produced WAVs at tempo 120 EIGHTH vs 250.0 ms-rate.
- **120 BPM fallback at the closure boundary**: `context.GetMusicalContext().Tempo ?? 120.0` matches the Interpreter.cs:200,210 pattern verbatim. When no tempo block is active, the call still resolves; the user gets a deterministic 250ms delay for EIGHTH (the default DAW tempo). Test 8 pins this.
- **Charitable D-07 in switch default**: Out-of-range NoteValue enum values (theoretically impossible from valid Flow source, but possible if `Value.Int` is constructed manually) fall through to `_ => quarterMs`. No exception, no crash — matches threat T-22-V5-14 mitigation. The cast `(NoteValueType.Value)noteValueEnum` is what makes this charitable: an out-of-range int becomes an out-of-range enum, which silently becomes "quarter".

## Deviations from Plan

**Total deviations:** 2 (1 plan-text bug, 1 mid-task test correction)
**Impact on plan:** None — verification still GREEN; both deviations follow established Phase 22 conventions.

### 1. [Rule 1 - Plan-text bug] Smoke script used non-existent `(sine 440.0 0.5 44100)` builtin

- **Found during:** Task 1 (smoke script authoring)
- **Issue:** Plan's Task 1 `<action>` block specified `Buffer src = (sine 440.0 0.5 44100)` for the smoke script. No such builtin exists — `sine` (lowercase) is `generateSine`, which fills an existing buffer; the `(amplitude, freq, duration, sampleRate)` shape doesn't match any registered signature. SAME bug as 22-02 plan-text (which used `(sine 440.0 1.0 44100)` and was corrected to `createSineTone`).
- **Fix:** Used `(createSineTone 0.5 440.0 0.8)` — the canonical 3-arg sine-buffer generator (duration, frequency, amplitude → 44100 Hz stereo Buffer). Same correction pattern as 22-02.
- **Files modified:** `tests/test_dx_delay_sync.flow`
- **Verification:** Smoke runs to completion, sentinel prints, exit 0. Three WAV files produced with predicted byte sizes (527732, 307968, 527732).
- **Committed in:** `9e175b1` (Task 1 commit)

### 2. [Rule 1 - Test bug] Test 9 BareIntegerArg assertion was too optimistic

- **Found during:** Task 2 (GREEN run; 8/9 GREEN, Test 9 RED)
- **Issue:** Plan's Task 1 spec for Test 9 said "MAY dispatch to either overload depending on OverloadResolver scoring. Pin the actual behavior with `Assert.True(...)` and document." The original assertion was `Assert.True(errorCount == 0, ...)` — assuming the resolver would pick a side. Actual observed behavior: `errorCount == 2` with `"Ambiguous overload for function 'delay' with argument types (Buffer, Int, Double, Double). Candidates: delay(Buffer, Double, Double, Double), delay(Buffer, NoteValue, Double, Double)"`.
  This is precisely the ambiguity Pitfall 1 warned about: Int → NoteValue is `IsCompatibleWith` (NoteValueType.cs:19) AND Int → Double widens through the numeric ladder, so both candidates score identically and the resolver flags ambiguity.
- **Fix:** Inverted the assertion to pin the OBSERVED ambiguity: `Assert.True(errorCount > 0, ...)` + `Assert.Contains("Ambiguous overload", stderr)`. Renamed the Fact `BareIntegerArg_DispatchesAmbiguous_DocumentedPitfall1`. Documented in the Fact's doc comment that this is the v1.3 observed behavior — if a future plan disambiguates the dispatch (e.g., adds a tie-break preferring NoteValue when literal int matches an enum value), this assertion goes RED requiring an explicit decision.
- **Files modified:** `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs`
- **Verification:** All 9 DelaySyncFacts GREEN after the correction.
- **Committed in:** `98da48e` (Task 2 commit) — the corrected assertion shipped together with the GREEN body since the original assertion was only meaningful AFTER the new overload was registered (before that, only the Double overload existed and bare-Int dispatched cleanly to it).

### Echo deviations from prior plans (informational only, not adjudicated again)

- **Plan referenced "ByteIdentical 19/19" but actual count is 6**: Same documentation lag observed in 22-01, 22-02, 22-03. The actual byte-identical regression gate consists of 6 tests across 3 classes (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI). All 6 GREEN. No code change required.
- **`tests/` directory is gitignored**: `git add -f` used for `tests/test_dx_delay_sync.flow`, matching the convention from 22-01 / 22-02 / 22-03.

## Threat Surface — STRIDE Compliance

The plan's `<threat_model>` lists four dispositions. All four are honored by the GREEN implementation:

| Threat ID | Disposition | Mitigation in EffectsFunctions.cs |
|-----------|-------------|-----------------------------------|
| T-22-V5-13 (DoS: extreme tempo → astronomical delay ms) | accept | Same surface as ms-rate path (`(delay buf 60000000.0 ...)`); existing `Delay.Apply` allocates the buffer either way. No new attack surface. |
| T-22-V5-14 (DoS: NoteValue enum out of range) | mitigate | `(NoteValueType.Value)noteValueEnum` cast — out-of-range values fall through `_ => quarterMs` in switch. No exception, no crash. Charitable D-07. |
| T-22-V5-15 (Tampering: bare-integer arg ambiguity) | mitigate | Documented in Test 9 (`BareIntegerArg_DispatchesAmbiguous_DocumentedPitfall1`) + acceptance smoke uses `EIGHTH` named constant. OverloadResolver flags ambiguity rather than silently picking a side — surface error early. |
| T-22-V5-16 (Repudiation: ms-rate delay byte-identity) | mitigate | `RegisterDelay` and `DelayEffect` bodies UNCHANGED — `git diff HEAD~3 HEAD -- flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` shows zero `-` lines for those identifiers. ByteIdentical 6/6 GREEN. WAV byte-identity verified by `cmp`. |

## Issues Encountered

- **Test project did not see `internal NoteValueToMs`**: Plan suggested `internal static double NoteValueToMs(...)`. The `flow-lang.Tests` project is a separate assembly with no `InternalsVisibleTo` configured, so `internal` would have made the Facts uncompilable. Switched to `public static` — same convention as `FileIO.VarispeedResample` (22-02). No deviation tracking needed (build error caught immediately at Task 1).

## Next Phase Readiness

- **DX-12 is the fourth of seven Phase 22 plans (22-01 DX-10 + 22-02 DX-15 + 22-03 DX-11 shipped; 22-04 DX-12 closes Wave 3)**. 22-05 (DX-13 quantize), 22-06 (DX-14 legato/portamento), and 22-07 (closure) remain. None depend on this plan's outputs (per Phase 22 design — features are independently shippable).
- **Context-dependent effect registration pattern is now established** for any future plan where an audio effect needs MusicalContext state. Sibling `RegisterContextDependent` method on the same Functions class, wired from `BuiltInFunctions.RegisterContextDependentFunctions` alongside `RegisterEuclideanOverloads`. 22-05 quantize is the obvious next consumer (quantize grid needs `TimeSignature` + `Tempo`).
- **Convergence-at-DSP pattern is now established** for any future plan that adds a sibling overload to an effect. Compute the effect's parameter from the new arg type, then call the same DSP.Apply routine — gives one regression-stable boundary (the DSP routine) instead of two. WAV byte-identity verifiable by `cmp`.
- Byte-identical regression gate proven robust under tempo-math extension (DX-12 reads `MusicalContext.Tempo` and the 6/6 ByteIdentical tests stayed GREEN — confirms no accidental tempo-block re-entry or cache invalidation).

## Self-Check

Files verified:
- FOUND: `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs`
- FOUND: `tests/test_dx_delay_sync.flow`
- FOUND: `.planning/phases/22-tier-b-c-composer-dx-bundle/22-04-SUMMARY.md`

Commits verified:
- FOUND: `9e175b1` (Task 1 RED)
- FOUND: `98da48e` (Task 2 GREEN)
- FOUND: `0129ec4` (Task 3 verification)

## Self-Check: PASSED

---
*Phase: 22-tier-b-c-composer-dx-bundle*
*Completed: 2026-05-02*
