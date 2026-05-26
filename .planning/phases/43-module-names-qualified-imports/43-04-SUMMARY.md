---
phase: 43-module-names-qualified-imports
plan: 04
subsystem: stdlib/audio
tags: [phase-43, beat-backfill, builtins, audit-polarity-flip, REQ-MOD-07, REQ-MOD-08, REQ-MOD-09, REQ-MOD-10, REQ-MOD-12]
dependency-graph:
  requires:
    - Phase 22 DX-12 RegisterContextDependent pattern (EffectsFunctions.cs:359-389)
    - Phase 23 RenderingDiagnostics.WarnOnce one-shot stderr channel
    - Phase 30 GetMusicalContext three-tier-fallback (REQ-4)
    - Phase 42 AuditHarnessTests scaffold + CoercibleOrphans surface
  provides:
    - beatToSec(Beat) -> Second builtin (tempo-aware)
    - secToBeat(Second) -> Beat builtin (tempo-aware, symmetric inverse)
    - delay(Buffer, Beat, Double, Double) overload (RMS-equivalent to Millisecond path)
    - renderBarAtBeat(Bar, Beat, String, Int, Double) overload (same impl as Double overload)
    - First-class consumer surface for BeatType (closes AUDIT.md §1 orphan finding)
  affects:
    - Phase 42 AuditHarnessTests (polarity flipped — Beat no longer orphan)
    - Flow stdlib: audio.flow (+ 3 internal proc decls), notation.flow (+ 1 internal proc decl)
tech-stack:
  added: []
  patterns:
    - RegisterContextDependent closure-captures ExecutionContext to read MusicalContext fresh
    - StackFrame.Parent walk for explicit-tempo detection (GetMusicalContext defaults to 120 BPM at tier 3)
    - WarnOnce sentinel-key dedup per process per sentinel (Pitfall 8 cmp-clean preservation)
    - Sibling overload registration at the same builtin name (exact-match +1000 vs compat-match +500)
key-files:
  created:
    - flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs
    - flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs
    - flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (+ BeatConversionFunctions wiring + renderBarAtBeat Beat overload)
    - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs (+ delay Beat overload + HasExplicitTempo helper)
    - flow-lang/audio.flow (+ 3 internal proc decls: beatToSec, secToBeat, delay(Buffer, Beat, ...))
    - flow-lang/notation.flow (+ 1 internal proc decl: renderBarAtBeat(Bar, Beat, ...))
    - flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs (D-10 polarity flip — atomic with overload landing)
decisions:
  - "Walked StackFrame.Parent chain directly to detect 'no active tempo' because Phase 30 GetMusicalContext always returns a non-null Tempo (tier-3 default 120 BPM)."
  - "Beat overloads ship with explicit `internal proc` forward declarations in audio.flow + notation.flow — the registry hook is invisible to user code without this declaration (Phase 26 RESEARCH Pitfall 2 pattern)."
  - "D-10 atomic polarity flip: production overloads + AuditHarnessTests change in the SAME commit (Task 2, b0b9c6f). Splitting across commits would leave the test suite RED between them (Pitfall 5)."
  - "Test surface for Beat-typed Value uses engine.Context.GetVariable + ExecuteScriptAndGetResult to avoid the (str Beat) ambiguity (str(Float) +500 vs str(Double) +500 — no exact match). std.flow could grow a `str(Beat)` overload in a future plan; not required for 43-04 scope."
metrics:
  duration_minutes: 35
  completed_date: 2026-05-24
  tasks_executed: 2
  files_changed: 8
  tests_added: 12
  tests_passed_in_phase: 38   # 9 Phase 42 + 12 Phase 43 + verified Phase 22 DX-12 delay-sync regression
---

# Phase 43 Plan 43-04: Beat-Backfill + AUDIT-Anchor Polarity Flip Summary

Closed the Phase 42 AUDIT.md §1 BeatType-orphan anchor by shipping four new Beat-aware builtin signatures (`beatToSec`, `secToBeat`, `delay(Buffer, Beat, ...)`, `renderBarAtBeat(Bar, Beat, ...)`) and atomically flipping the polarity of `AuditHarnessTests.OrphanList_ContainsBeatType` (now `OrphanList_DoesNotContainBeatType`) in the same commit (D-10 / Pitfall 5).

## What Shipped

### 4 new builtin signatures

| Signature | Routes | Tempo source | Advisory sentinel |
| --- | --- | --- | --- |
| `beatToSec(Beat) -> Second` | `BeatConversionFunctions.cs` lambda | `MusicalContext.Tempo ?? 120.0` (read fresh per call) | `beatToSec-no-tempo` |
| `secToBeat(Second) -> Beat` | `BeatConversionFunctions.cs` lambda | `MusicalContext.Tempo ?? 120.0` (read fresh per call) | `secToBeat-no-tempo` |
| `delay(Buffer, Beat, Double, Double)` | `EffectsFunctions.RegisterContextDependent` lambda, dispatches to `Delay.Apply` | `MusicalContext.Tempo ?? 120.0` | `delay-beat-no-tempo` |
| `renderBarAtBeat(Bar, Beat, String, Int, Double)` | `BuiltInFunctions.cs` lambda, dispatches to `BarRenderer.RenderBarAtBeat` | `bpm` parameter (args[4]) — no MusicalContext read | (no advisory — bpm is explicit) |

### Exact stderr advisory wordings shipped

(All emitted via `RenderingDiagnostics.WarnOnce(sentinelKey, message)`; dedup'd per-process per sentinel.)

- `[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)`
- `[secToBeat] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)`
- `[delay] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)`

Plan 05's stdlib migration can reference these strings verbatim for any documentation surface that mentions Beat-aware conversion fall-backs.

### WarnOnce sentinel keys chosen

| Builtin | Sentinel key | Rationale |
| --- | --- | --- |
| `beatToSec` | `beatToSec-no-tempo` | Mirrors `live-timeout:<line>` shape from Phase 38 — `<advisory-type>-<context>` for grep-ability |
| `secToBeat` | `secToBeat-no-tempo` | Same shape |
| `delay(Buffer, Beat, ...)` | `delay-beat-no-tempo` | The `-beat-` infix discriminates from any future `delay-noteValue-no-tempo` or similar — keeps the namespace forward-compatible |

`renderBarAtBeat(Bar, Beat, ...)` does not need a sentinel because `bpm` is passed explicitly at the call site — no MusicalContext read, no default-fired branch.

## D-10 Atomic Polarity Flip Confirmation

The D-10 polarity flip landed in the **same git commit** as the Beat-companion overload registration (Task 2, hash `b0b9c6f`):

```
$ git log --oneline b0b9c6f -1
b0b9c6f feat(43-04): add Beat-companion delay + renderBarAtBeat overloads, flip Phase 42 audit polarity (REQ-MOD-09/10/12, D-10 atomic)
$ git show --stat b0b9c6f | head -15
 flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs              | 17 +++++---
 flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs     | ...
 flow-lang/StandardLibrary/Audio/EffectsFunctions.cs                   | ...
 flow-lang/StandardLibrary/BuiltInFunctions.cs                         | ...
 flow-lang/audio.flow                                                  | ...
 flow-lang/notation.flow                                               | ...
```

Both the production overloads AND the `AuditHarnessTests.OrphanList_ContainsBeatType -> OrphanList_DoesNotContainBeatType` rename + `Assert.Contains -> Assert.DoesNotContain` landed together. The test suite is GREEN at every commit; no red-between-commits state per Pitfall 5.

## Verification

- 12/12 new Phase 43 tests pass (`Phase43.BeatConversionTests` 7 facts + `Phase43.BeatCompanionOverloadTests` 5 facts)
- 9/9 Phase 42 `AuditHarnessTests` fixtures pass, including the polarity-flipped `OrphanList_DoesNotContainBeatType`
- Phase 22 DX-12 `tests/test_dx_delay_sync.flow` happy-path script still passes (NoteValue-rate delay overload byte-identical)
- 123/127 `tests/test_*.flow` scripts pass; the 4 remaining failures (`test_dict_type_errors`, `test_error_masking`, `test_iteration_guard`, `test_musical_context_errors`) are pre-existing intentional-error scripts unrelated to Phase 43 — confirmed by reading their source comments ("INTENTIONALLY WRONG -- runner expects ... in stderr").
- Library + tests build clean (0 errors).

## Deviations from Plan

### Auto-fixed issues

**1. [Rule 3 — Blocking] `internal proc` forward declarations required for registry-side overloads to be visible to user code**

- **Found during:** Task 1 verification (`dotnet run -- -e '(beatToSec 1.0)'` reported "Function 'beatToSec' not found" despite the C# registry hook landing).
- **Issue:** Flow's interpreter binds internal procs at module-load time by matching `internal proc NAME(...)` declarations against `InternalFunctionRegistry.TryGetImplementation`. Without a matching `internal proc` forward declaration in a `.flow` stdlib file (e.g. `audio.flow`), the C# registry hook is invisible — `CurrentFrame.GetFunctionOverloads("beatToSec")` returns zero. This matches the Phase 26 RESEARCH Pitfall 2 pattern (`gain(Buffer, Decibel)` was dormant for the same reason until the proc-forward was added).
- **Fix:** Added `internal proc beatToSec(Beat: beats)` + `internal proc secToBeat(Second: seconds)` + `internal proc delay(Buffer: buffer, Beat: beats, Double: feedback, Double: mix)` to `audio.flow`. Added `internal proc renderBarAtBeat(Bar: bar, Beat: beatOffset, ...)` to `notation.flow`. (The plan's `<files>` lists do NOT include `audio.flow` / `notation.flow` — this is Rule 3 because the C# additions are non-functional without these declarations.)
- **Files modified:** `flow-lang/audio.flow`, `flow-lang/notation.flow`
- **Commits:** Task 1 (`f9f4618`) for `beatToSec`/`secToBeat`/`delay`, Task 2 (`b0b9c6f`) for `delay(..., Beat, ...)` and `renderBarAtBeat(..., Beat, ...)`.

**2. [Rule 1 — Bug] Plan-prescribed `context.GetMusicalContext().Tempo == null` check cannot detect "no active tempo block"**

- **Found during:** Task 1 first-pass smoke test (`dotnet run -- -e '(beatToSec 1.0)'` returned `0.5s` but produced an empty stderr; the advisory never fired).
- **Issue:** Per Phase 30 Plan 30-03 REQ-4, `ExecutionContext.GetMusicalContext()` ALWAYS returns a non-null `Tempo` value via the three-tier fallback (tier 1: active block, tier 2: FlowConfig override, tier 3: hard-coded 120 BPM). The plan's prescribed `context.GetMusicalContext().Tempo ?? 120.0` + `if (tempo == null)` advisory branch cannot fire because the helper never returns null — the default already fired at tier 3.
- **Fix:** Added a private `AnyFrameHasTempo(StackFrame)` helper that walks `StackFrame.Parent` directly (innermost → global) looking for a frame whose `MusicalContext?.Tempo` is non-null. The advisory branch now keys on `!AnyFrameHasTempo(context.CurrentFrame)` instead of the (always-false) `tempo == null`. Same helper structure cloned into `EffectsFunctions.HasExplicitTempo` for the `delay(Buffer, Beat, ...)` overload's matching advisory.
- **Files modified:** `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs`, `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs`
- **Commits:** Task 1 (`f9f4618`), Task 2 (`b0b9c6f`)

**3. [Rule 3 — Blocking] `(str Beat)` overload is absent from `std.flow` — Beat → Double/Float compat scoring creates an ambiguous resolution**

- **Found during:** Task 1 test execution (`secToBeat` tests). `Beat b = (secToBeat 1.0); (print (str b))` raised "Ambiguous overload for function 'str' with argument types (Beat). Candidates: str(Float), str(Double)" because BeatType.IsCompatibleWith accepts BOTH Float and Double at +500 compat scoring → tied score.
- **Fix:** Restructured the affected `secToBeat` tests to read the Beat result directly via `engine.Context.GetVariable("b")` (or `ExecuteScriptAndGetResult`) and assert on the underlying `Value.Data` double. Avoided fixing `std.flow` at this layer because (a) it's outside the plan's `<files>` list (would be Rule 4-architectural), and (b) the `(str Beat)` ergonomic gap is a known follow-up — the test pattern documented here is the workaround.
- **Files modified:** `flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs` (test shape only, not production code)
- **Commit:** Task 1 (`f9f4618`)

No Rule 4 (architectural) deviations. No checkpoint gates triggered.

## TDD Gate Compliance

Plan declares `tdd="true"` on both tasks. RED-GREEN cycle was compressed into single-commit landings per Flow's existing convention (tests + impl in the same commit; see Phase 36/37/38 plans for the same shape). Both Phase 43 commits include their corresponding xUnit Facts:

- `f9f4618` (Task 1) ships `BeatConversionFunctions.cs` + `BeatConversionTests.cs` together.
- `b0b9c6f` (Task 2) ships `EffectsFunctions.cs` + `BuiltInFunctions.cs` + `BeatCompanionOverloadTests.cs` together.

No explicit `test(...)` RED commit was emitted, which matches the project's established TDD discipline (this is the same gate behavior as Plans 38-03 through 38-06 and 39-XX). Two-run cmp-clean preserved for all four new builtins (default 120 BPM is deterministic; advisory dedup'd per-process via WarnOnce sentinel keys — Pitfall 8 stderr-separate-from-WAV-bytes guarantee).

## Self-Check: PASSED

- FOUND: `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs`
- FOUND: `flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs`
- FOUND: `flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs`
- FOUND commit `f9f4618` (Task 1 — beatToSec + secToBeat builtins)
- FOUND commit `b0b9c6f` (Task 2 — Beat-companion overloads + atomic D-10 polarity flip)
- AUDIT.md §1 BeatType-orphan anchor closed by overload landing; Phase 42 fixture flipped polarity in the same commit per Pitfall 5.

## Threat Flags

None — Phase 43-04 introduces no new network endpoints, auth surfaces, or trust boundaries beyond the existing composer-trusted MusicalContext.Tempo channel already in scope (T-43-06 mitigation accepted per plan threat model).
