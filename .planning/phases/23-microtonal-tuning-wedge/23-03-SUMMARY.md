---
phase: 23-microtonal-tuning-wedge
plan: 03
subsystem: audio
tags: [microtonal, scale-modes, diagnostics, dedup-warnings, charitable]

# Dependency graph
requires:
  - phase: 23-microtonal-tuning-wedge
    plan: 01
    provides: TuningSystem/Mode/RenderTuning/TuningTables/RatioMath foundation
  - phase: 23-microtonal-tuning-wedge
    plan: 02
    provides: ScaleDatabase.TryParseKeyWithMode canonical entry (major/minor branch only); MusicalContext.Tuning 9th field; HarmonyFunctions.Enharmonic context-dependent registration; MidiExport.WriteMidi (context-free, today migrated)
provides:
  - RenderingDiagnostics one-shot stderr warning helper (process-static dedup HashSet) at flow-lang/Diagnostics/
  - ScaleDatabase.TryParseKeyWithMode mode-detection branch widened from 2 modes (major/minor) to 7 modes (+ dorian/phrygian/lydian/mixolydian/locrian) — additive, longer-suffix-first ordering
  - MusicalContext.ValidKeys grows from 34 entries (17 roots × 2 modes) to 119 entries (17 roots × 7 modes) via BuildValidKeys() helper
  - HarmonyFunctions.Enharmonic D-11 one-shot warning under non-12-TET (conversion body unchanged)
  - MidiExport.WriteMidi migrated to context-dependent registration; D-13 one-shot warning under non-12-TET (MIDI bytes unchanged)
  - 19 Phase 23 Facts (9 ChurchModeParseFacts Theory rows + 5 ChurchModeParseFacts + 5 RenderingDiagnosticsFacts + 5 EnharmonicWarningFacts + 5 WriteMidiWarningFacts)
affects: [23-04, 23-05, phase-24-scale-linting]

# Tech tracking
tech-stack:
  added: []  # Pure additions; no new external deps
  patterns:
    - "One-shot stderr warning channel — RenderingDiagnostics.WarnOnce(sentinelKey, message) wraps Console.Error.WriteLine with HashSet-backed per-process dedup. Single source of truth for D-11 / D-13 / future Phase 24 scaleLint warnings."
    - "Closed-set additive widening — TryParseKeyWithMode mode-detection branch grows from 2 to 7 modes via additional EndsWith checks (longer-suffix-first to avoid prefix collision). Original TryParseKey UNCHANGED per WARNING-6."
    - "Programmatic ValidKeys generation — BuildValidKeys() helper replaces 34-entry literal with foreach roots × modes Cartesian product; Phase 24 mode additions extend modes[] only."
    - "Context-dependent registration migration — MidiExport.RegisterContextDependent mirrors HarmonyFunctions.RegisterContextDependent + Plan 23-02's Vocalization migration shape (closure over ExecutionContext for per-call MusicalContext access)."
    - "Cross-assembly testing visibility convention — RenderingDiagnostics.ResetForTesting is `public static` (no InternalsVisibleTo configured; same convention as EffectsFunctions cross-assembly Facts helpers)."

key-files:
  created:
    - flow-lang/Diagnostics/RenderingDiagnostics.cs
    - flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs
    - flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs
    - flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs
    - flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs
  modified:
    - flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs
    - flow-lang/Runtime/MusicalContext.cs
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
    - flow-lang/StandardLibrary/Audio/MidiExport.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs

key-decisions:
  - "ResetForTesting() is public, not internal — flow-lang.Tests is a separate assembly with no InternalsVisibleTo configured. This matches the existing convention (EffectsFunctions cross-assembly helpers exposed for testing). Additional XML doc rationale captures the convention at the call site."
  - "Longer-suffix-first ordering in TryParseKeyWithMode — `lydian` is a 6-char substring of `mixolydian` (10 chars). The else-if chain MUST test `mixolydian` before `lydian` or `Bmixolydian` would parse as root='Bmixo' mode=Lydian. The test `TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian` pins this invariant."
  - "MidiExport keeps both 1-arg and 2-arg WriteMidi overloads — the 1-arg overload is preserved for backwards compat (LSP proxy paths, direct test invocation, the new 2-arg overload itself delegates to it). The 2-arg overload reads context.GetMusicalContext() before delegation."
  - "BuildValidKeys() generates 119 entries (17 roots × 7 modes) via Cartesian product. ValidKeys count Fact pins this exact size — Phase 24 additional-mode work is the controlled point of growth."
  - "ThreadSafe Fact in RenderingDiagnosticsFacts captures stderr to prevent test-runner output pollution from the spurious warning emissions (5 distinct sentinel keys × 200 iterations = 5 emitted lines max). Asserts ≤ 5 lines AND each key fired."

patterns-established:
  - "RenderingDiagnostics.WarnOnce + ResetForTesting — process-static HashSet dedup; thread-safe via lock; ResetForTesting public for cross-assembly Fact isolation."
  - "Longer-suffix-first ordering for closed-set string-suffix dispatch — applies broadly when one suffix is a substring of another."
  - "WARNING-4 between-runs reset — sequential FlowEngineRunner instances inside one xUnit Fact body MUST call RenderingDiagnostics.ResetForTesting between runs to defend against future warning-gates-export changes leaking dedup state."
  - "writeMidi migrated to context-dependent registration — mirrors Phase 23-02's Vocalization migration shape; keeps existing 1-arg WriteMidi for backwards compat."

requirements-completed: [MICR-01, MICR-02]

# Metrics
duration: 7m 17s
completed: 2026-05-04
---

# Phase 23 Plan 03: RenderingDiagnostics + 5 Church Modes + Non-12-TET Warnings Summary

**Wave 3 closes the diagnostic + scale-mode tail of Phase 23: a one-shot per-process stderr warning channel (`RenderingDiagnostics.WarnOnce`) lands in `flow-lang/Diagnostics/`, `enharmonic()` and `writeMidi()` gain D-11/D-13 one-shot non-12-TET warnings (conversion body + MIDI bytes UNCHANGED), and `ScaleDatabase.TryParseKeyWithMode` (canonical entry shipped Wave 2 per WARNING-8) is widened in-place from 2 modes to 7 by adding 5 church-mode suffix checks (longer-first ordering avoids `lydian`-substring-of-`mixolydian` collision). `MusicalContext.ValidKeys` grows from 34 to 119 entries via a programmatic `BuildValidKeys()` helper. 19 new Phase 23 Facts pin every contract end-to-end. Full test suite GREEN at 600/600.**

## Performance

- **Duration:** 7m 17s
- **Started:** 2026-05-04T01:42:23Z
- **Completed:** 2026-05-04T01:49:40Z
- **Tasks:** 2
- **Files created:** 5 (1 production diagnostic + 4 unit Fact files)
- **Files modified:** 5 production files

## Accomplishments

- **`flow-lang/Diagnostics/RenderingDiagnostics.cs`** lands as the single source of truth for one-shot composer-facing render-time advisories. `WarnOnce(sentinelKey, message)` wraps `Console.Error.WriteLine` with a thread-safe HashSet dedup; `ResetForTesting()` is public for cross-assembly Fact isolation. Style mirrors `TransformFunctions.TransposeSemitone:286` — same channel, same wording shape, plus dedup wrapper.
- **`ScaleDatabase.TryParseKeyWithMode` mode-detection branch widened additively from 2 modes to 7** per WARNING-8. The Wave 2 method's else-if chain gains 5 new entries (mixolydian, phrygian, locrian, dorian, lydian) with longer-suffix-first ordering. The original `TryParseKey(out bool isMajor)` is UNCHANGED per WARNING-6 — verified by `grep -c "TryParseKey(" >= 3` (1 def + 2 caller sites at lines 117-200) and by Fact `ExistingTryParseKey_StillWorks_ForChordResolution` which calls `GetScaleNotes("Cmajor")` + `ResolveRomanNumeral("V", "Cmajor")` (both routed through legacy TryParseKey).
- **`MusicalContext.ValidKeys` grows from 34 to 119 entries** via `BuildValidKeys()` programmatic helper (17 roots × 7 modes Cartesian product). Without this, `key Cdorian { ... }` failed `IsValidKey` before tuning math saw it. The `MusicalContext_ValidKeys_HasExpectedCount` Fact pins the exact 119 count.
- **`HarmonyFunctions.Enharmonic` gains D-11 one-shot warning** at the top (after `GetMusicalContext()` runs at line 47). Emits exactly `[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)` ONCE per session under non-12-TET. The conversion body downstream is UNCHANGED — the warning is purely advisory (Pitfall 5 #3 / AUDIT-VERIFIED).
- **`MidiExport.WriteMidi` migrates to context-dependent registration** via new `MidiExport.RegisterContextDependent(registry, context)` joining the 5 existing context-dependent registrars at `BuiltInFunctions.cs:794`. New 2-arg overload reads `context.GetMusicalContext()?.Tuning`, emits D-13 one-shot warning under non-12-TET, then delegates to the existing 1-arg `WriteMidi(args)`. The 1-arg overload is preserved for backwards compat. **MIDI bytes UNCHANGED** — still 12-TET (faithful microtonal MIDI deferred to v1.4) — verified by the `WriteMidi_BytesUnchanged_UnderJI` Fact.
- **Per-Fact dedup isolation**: `EnharmonicWarningFacts`, `WriteMidiWarningFacts`, and `RenderingDiagnosticsFacts` all use `[Collection("FlowScripts")]` + `RenderingDiagnostics.ResetForTesting()` in ctor + Dispose. The `WriteMidi_BytesUnchanged_UnderJI` Fact additionally calls `ResetForTesting()` between sequential `FlowEngineRunner` instances per WARNING-4 — defensive against future writeMidi-warning-gates-export changes where dedup state leaking from runner1 to runner2 could mask a regression.
- **Full suite GREEN at 600/600.** ByteIdentical regression GREEN (8/8) — neither the Pattern A short-circuit nor the new D-11/D-13 warnings affect rendered audio bytes. Phase 14/17/21/22/23 cumulative GREEN at 373/373.

## Task Commits

Two atomic commits per the plan's 2-task structure:

1. **Task 1: RenderingDiagnostics + ScaleDatabase 5 church-mode widening + ValidKeys 119 entries + Facts** — `4ea0927` (feat). 1 new diagnostic file + 2 modified production files (ScaleDatabase + MusicalContext) + 2 new Fact files (ChurchModeParseFacts + RenderingDiagnosticsFacts). 19 Facts GREEN.
2. **Task 2: D-11 enharmonic + D-13 writeMidi non-12-TET warnings + writeMidi context migration + Facts** — `3e6a3ba` (feat). 3 modified production files (HarmonyFunctions + MidiExport + BuiltInFunctions) + 2 new Fact files (EnharmonicWarningFacts + WriteMidiWarningFacts). 10 Facts GREEN.

## Files Created (5)

- `flow-lang/Diagnostics/RenderingDiagnostics.cs` — one-shot stderr warning channel with per-process per-sentinel-key dedup. Public `WarnOnce` + `ResetForTesting`.
- `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs` — 9 Theory rows + 5 Facts pin TryParseKeyWithMode 7-mode coverage + WARNING-6 ResolveRomanNumeral/GetScaleNotes routing + ValidKeys 119-entry count.
- `flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs` — 5 Facts pin Pitfall 5 dedup contract: first-call writes, same-key second-call no-op, different-key re-emits, ResetForTesting clears, thread-safe under 200-iteration parallel hammer.
- `flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs` — 5 Facts: JI fires, Pythagorean fires, EqualTemperament silent, no-pragma silent, two-calls-warns-once dedup.
- `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs` — 5 Facts: JI fires, EqualTemperament silent, no-pragma silent, multi-call dedup, MIDI bytes unchanged under JI (with WARNING-4 ResetForTesting between sequential runs).

## Files Modified (5)

- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` — `TryParseKeyWithMode` mode-detection branch widened from 2 to 7 modes (longer-suffix-first ordering). Original `TryParseKey` UNCHANGED.
- `flow-lang/Runtime/MusicalContext.cs` — `ValidKeys` grows from 34-entry literal to 119 entries via `BuildValidKeys()` Cartesian-product helper.
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — D-11 warning gate at top of `Enharmonic` (after `GetMusicalContext`); conversion body UNCHANGED.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — new `RegisterContextDependent(registry, context)` + new 2-arg `WriteMidi(args, context)` overload (D-13 warning then delegates to 1-arg). Existing 1-arg `WriteMidi` preserved.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — context-free `writeMidi` registration removed at line 567-570; appended `Audio.MidiExport.RegisterContextDependent(registry, context)` to `RegisterContextDependentFunctions` at line 794.

## Decisions Made

- **TryParseKeyWithMode widening shape (per WARNING-8 single source of truth).** Wave 2 created the canonical entry with major/minor; Wave 3 widens the same method's mode-detection branch in-place by adding 5 EndsWith checks. No write-then-delete inline `TryParseTonicAndMode` helper was ever created in SongRenderer. `grep -c "TryParseKeyWithMode" ScaleDatabase.cs == 1` confirms single source of truth.
- **WARNING-6 verification: original TryParseKey caller integrity preserved.** `ResolveRomanNumeral` (line 118) and `GetScaleNotes` (line 234) still call `TryParseKey(out bool isMajor)`. Verified by `grep -c "TryParseKey(" ScaleDatabase.cs == 3` (1 def + 2 caller sites) AND by the `ExistingTryParseKey_StillWorks_ForChordResolution` Fact which exercises both public surfaces (`GetScaleNotes("Cmajor")`, `GetScaleNotes("Aminor")`, `ResolveRomanNumeral("V", "Cmajor")`).
- **Longer-suffix-first ordering is mandatory.** `lydian` (6 chars) is a substring of `mixolydian` (10 chars). The else-if chain tests `mixolydian` before `lydian` so `Bmixolydian` parses as root='B' mode=Mixolydian rather than root='Bmixo' mode=Lydian. Pinned by `TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian` Fact.
- **writeMidi context migration shape.** Mirrors `HarmonyFunctions.RegisterContextDependent` verbatim — closure over `ExecutionContext`, register signature with delegate `args => WriteMidi(args, context)`, new 2-arg WriteMidi overload reads `context.GetMusicalContext()?.Tuning`, emits warning, then delegates to 1-arg overload. The 1-arg overload remains for the LSP proxy path + direct test invocation.
- **WARNING-4 application: ResetForTesting between sequential FlowEngineRunner runs.** `WriteMidi_BytesUnchanged_UnderJI` is the only Fact in the suite that fires two FlowEngine instances in one Fact body. The dedup HashSet is process-static, so without the explicit reset, the second runner would not re-emit even if its tuning differed. The reset defends against a future change where the warning emission itself affects ExportMidiInternal control flow (e.g., warning-gates-export hypothetical regression).
- **LSP proxy verification.** `BuiltInFunctions.cs:111` re-uses `dummyContext` whose `GetMusicalContext()?.Tuning` is null because no pragma is applied during signature-only registration. `dummyContext.SetTuning` is never called. Therefore the new `MidiExport.RegisterContextDependent` does not emit a spurious warning during the LSP proxy registration path. Phase 17 regression preserved (102/102 GREEN — no unintended LSP path side effects).
- **ResetForTesting visibility: public, not internal.** flow-lang.Tests is a separate assembly with no `InternalsVisibleTo` attribute configured (same as the rest of the codebase per `EffectsFunctions.cs:225` cross-assembly helpers convention). The doc comment captures the rationale.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Test scripts initially used invalid Note literal syntax `Fsharp4`/`Csharp4`.**
- **Found during:** Task 2 verification (Facts compiled but ran with parse errors).
- **Issue:** `EnharmonicWarningFacts` initially wrote `(enharmonic Fsharp4)` and `(enharmonic Csharp4)` instead of the proper Flow surface syntax `F#4` / `C#4`. The Flow lexer parses `Fsharp4` as an Identifier, not a NoteLiteral, producing `Variable 'Fsharp4' not found` errors.
- **Fix:** Replaced with `F#4` / `C#4` (the canonical surface syntax for sharp notes per `tests/test_enharmonic.flow:5`).
- **Files modified:** `flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs`.
- **Verification:** All 5 EnharmonicWarningFacts GREEN after the fix.
- **Committed in:** `3e6a3ba` (Task 2 commit; the fix happened during Task 2 authoring before commit).

**2. [Rule 3 — Blocking] RenderingDiagnostics.ResetForTesting initially marked `internal`.**
- **Found during:** Task 1 build (3 CS0117 errors at RenderingDiagnosticsFacts:18,23,86).
- **Issue:** Plan said `internal static void ResetForTesting()`, but flow-lang.Tests is a separate assembly with no `InternalsVisibleTo` configured. The 3 ctor/Dispose call sites couldn't see the internal method.
- **Fix:** Changed to `public static void ResetForTesting()` with an XML doc comment capturing the cross-assembly convention (mirrors `EffectsFunctions.cs:225` "Public visibility required for cross-assembly Facts (no InternalsVisibleTo configured)" comment).
- **Files modified:** `flow-lang/Diagnostics/RenderingDiagnostics.cs`.
- **Verification:** Build succeeded; all 5 RenderingDiagnosticsFacts GREEN.
- **Committed in:** `4ea0927` (Task 1 commit).

**3. [Rule 1 — Bug] ThreadSafe Fact's stderr-content assertion was wrong.**
- **Found during:** Task 1 Fact authoring (caught at write time before first run).
- **Issue:** Initial Fact body asserted `Assert.Contains("concurrent-" + k, output)` where `concurrent-N` is the SENTINEL KEY, not the message body. The sentinel key is never written to stderr — only the message is. So the Fact would have failed even on a correct implementation.
- **Fix:** Changed message format to `"msg-key-" + (i % 5)` (so the message itself contains a key-discriminating suffix), then assert `Assert.Contains("msg-key-" + k, output)`. Also added an upper-bound assertion `Assert.True(lineCount <= 5)` to verify the dedup contract — without it, a buggy implementation that emitted N times per key would pass the contains-check.
- **Files modified:** `flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs`.
- **Verification:** All 5 RenderingDiagnosticsFacts GREEN after the rewrite.
- **Committed in:** `4ea0927` (Task 1 commit; the rewrite happened during Task 1 authoring before commit).

---

**Total deviations:** 3 auto-fixed (1 syntax bug in test script, 1 visibility/blocking, 1 fact-authoring bug).
**Impact on plan:** All three fixes scoped within Task authoring. Plan's intent and acceptance criteria satisfied exactly as authored. No scope creep.

## Issues Encountered

- **`flow-lang.sln` doesn't exist; project root has `flow-sharp.sln` instead.** First `dotnet build flow-lang.sln` invocation failed with "Project file does not exist." Switched to `dotnet build flow-sharp.sln` and the rest of the verification flowed cleanly. Not a Flow language issue — just a plan-template path that didn't match the actual filename.
- **One spurious "Fatal error. Internal CLR error. (0x80131506)" on the first `dotnet build` invocation.** Did not reproduce on subsequent invocations. Likely a transient .NET runtime hiccup, not a project-state issue.

## TDD Gate Compliance

The plan's `<task type="auto" tdd="true">` markers indicate per-task TDD intent. Both tasks bundled production + test in a single `feat(...)` commit — matching the established Phase 18-23 pattern (test files reference public API that only exists once production code lands; this is the documented Phase 18-22 precedent). Each task's tests GREEN before moving on. The 2-commit sequence is feat → feat, satisfying the GSD per-task atomicity requirement.

## WARNING-3/4/6/8 Resolution

- **WARNING-4 (ResetForTesting between sequential runs):** `grep -c "RenderingDiagnostics.ResetForTesting" WriteMidiWarningFacts.cs == 4` (ctor + Dispose + between-runs reset in BytesUnchanged + an additional ctor invocation) — well above the `>= 3` acceptance gate. The between-runs reset is the load-bearing one; it defends against future warning-gates-export changes leaking dedup state. Verified.
- **WARNING-6 (TryParseKey unchanged):** `grep -c "TryParseKey(" ScaleDatabase.cs == 3` (1 def + 2 caller sites at ResolveRomanNumeral + GetScaleNotes); `grep -c "out bool isMajor" ScaleDatabase.cs == 3` (preserved 1-def + 2-caller-site count). Verified.
- **WARNING-8 (canonical entry from Wave 2):** `grep -c "TryParseKeyWithMode" ScaleDatabase.cs == 1` (single source of truth — Wave 2 created, Wave 3 widened in-place; no second definition). Verified.

## TryParseKeyWithMode Widening Shape (Two-Pass Strict Outcome)

Wave 2 (Plan 23-02 Task 3) shipped `TryParseKeyWithMode` with the major/minor branch only:
```csharp
if      (lower.EndsWith("major")) { mode = Mode.Major; suffixLen = 5; }
else if (lower.EndsWith("minor")) { mode = Mode.Minor; suffixLen = 5; }
else return false;
```

Wave 3 (this plan, Task 1) widens the same method's mode-detection branch in-place to 7 modes with longer-suffix-first ordering:
```csharp
if      (lower.EndsWith("mixolydian")) { mode = Mode.Mixolydian; suffixLen = 10; }
else if (lower.EndsWith("phrygian"))   { mode = Mode.Phrygian;   suffixLen = 8; }
else if (lower.EndsWith("locrian"))    { mode = Mode.Locrian;    suffixLen = 7; }
else if (lower.EndsWith("dorian"))     { mode = Mode.Dorian;     suffixLen = 6; }
else if (lower.EndsWith("lydian"))     { mode = Mode.Lydian;     suffixLen = 6; }
else if (lower.EndsWith("major"))      { mode = Mode.Major;      suffixLen = 5; }
else if (lower.EndsWith("minor"))      { mode = Mode.Minor;      suffixLen = 5; }
else return false;
```

Root-extraction, casing normalization, and `NoteToSemitone` validation downstream are UNCHANGED from Wave 2. No method body rewrite. No companion helper.

## writeMidi Context Migration Shape

Mirrors `HarmonyFunctions.RegisterContextDependent` verbatim — three-line registration body + new 2-arg overload reads context, emits warning, delegates to 1-arg. Joins the 5 context-dependent registrars in `BuiltInFunctions.cs:RegisterContextDependentFunctions`:
```
Audio.SongRenderer.RegisterContextDependent(...)
Composition.SongFunctions.Register(...)
Harmony.HarmonyFunctions.RegisterContextDependent(...)
Audio.EffectsFunctions.RegisterContextDependent(...)
Transforms.TransformFunctions.RegisterContextDependent(...)
Audio.Vocalization.VocalizationFunctions.RegisterContextDependent(...)
Audio.MidiExport.RegisterContextDependent(...)  ← NEW (Phase 23-03 Task 2)
```

The 1-arg `WriteMidi(IReadOnlyList<Value>)` overload is preserved for backwards compat — the LSP proxy path delegates through it via the 2-arg overload's tail call, and any direct test invocation can still call it.

## Phase 24 Readiness

`TryParseKeyWithMode` now recognizes all 7 diatonic modes and is the canonical entry consumed by:
- `SongRenderer.ResolveRenderTuning` (Phase 23-02) — per-section tuning resolution.
- Phase 24 LINT-01 / LINT-03 `scaleLint` (planned) — nested-key resolution and out-of-scale warning.

The 7-mode shape is locked as a closed-enum return value (`Mode` enum, Phase 23-01). Phase 24 scaleLint can read this entry without further widening. RenderingDiagnostics is in place and ready for Phase 24 scaleLint warnings to reuse the same WarnOnce dedup channel.

## Self-Check

Verifying claims before finalizing:

**Files exist:**
- FOUND: flow-lang/Diagnostics/RenderingDiagnostics.cs
- FOUND: flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs

**Commits exist:**
- FOUND: 4ea0927 (Task 1 — RenderingDiagnostics + ScaleDatabase widening + ValidKeys 119)
- FOUND: 3e6a3ba (Task 2 — D-11 enharmonic + D-13 writeMidi + writeMidi context migration)

**Test status:**
- 19 Phase 23 Plan 03 Facts GREEN (9 ChurchModeParseFacts Theory rows + 5 ChurchModeParseFacts Facts + 5 RenderingDiagnosticsFacts + 5 EnharmonicWarningFacts + 5 WriteMidiWarningFacts).
- 8 ByteIdentical Facts STILL GREEN (tutorial + showcase + ByteIdenticalDefaultTuning).
- 373 Phase 14/17/21/22/23 Facts GREEN.
- 600/600 full test suite GREEN.

## Self-Check: PASSED

---
*Phase: 23-microtonal-tuning-wedge*
*Completed: 2026-05-04*
