---
phase: 33-sfz-orchestral-sampler
plan: 07
subsystem: audio
tags: [sfz, sampler, song-renderer, midi-export, gm-program, dispatch, byte-identical]

# Dependency graph
requires:
  - phase: 33-sfz-orchestral-sampler
    provides:
      - 33-02 — ExecutionContext.SfzPatchRegistry strongly-typed Dictionary<string, SfzData> + Value.Sfz factory + SfzType
      - 33-04 — SfzParser entry point producing SfzData with fully-resolved sample paths
      - 33-05 — Composer-visible @sfz stdlib + loadSfz Symbol/String builtins + SfzBuiltins.Register call site in FlowEngine
      - 33-06 — SfzRenderer single-note render + SfzSampleCache eager-load
  - phase: 29-sampled-tonal-instruments
    provides: FlowEngine.CurrentSampleCache static accessor pattern + RenderingDiagnostics.WarnOnce one-shot advisory pattern + INoteSynthesizer interface + SongRenderer.RenderSection(SectionData, INoteSynthesizer) reuse target
  - phase: 28-articulation-multi-track-midi
    provides: SequenceTrackInfo per-sequence track accumulator + ResolveGmProgram prefix-match dispatch + writeMidi multi-track export
provides:
  - flow-lang/Interpreter/Interpreter.cs — D-12 typed-Sfz binding hook in ExecuteVariableDeclaration writes (varDecl.Name, sfzData) into ExecutionContext.SfzPatchRegistry BEFORE _context.DeclareVariable
  - flow-lang/Core/FlowEngine.cs — per-engine SfzSampleCache field + CurrentSfzSampleCache + CurrentExecutionContext static accessors + Dispose cleanup
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs — D-13 sampler:NAME dispatch BEFORE the existing Phase 29 path; SfzNoteSynthesizer adapter wrapping SfzRenderer in INoteSynthesizer so the existing RenderSection / SequenceRenderer / BarRenderer / VoiceAllocator pipeline is reused verbatim
  - flow-lang/StandardLibrary/Audio/MidiExport.cs — D-15 sampler: prefix-strip at top of ResolveGmProgram + 12 new D-16 GM-program entries (more-specific names ordered first) + D-17 SequenceTrackName meta-event with stripped name
  - 2 integration test classes — SfzBindingTests (5 facts) + SfzMidiExportTests (10 facts), 15 facts total all green
affects: [34-symphony-showcase]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "INoteSynthesizer adapter wrapping SfzRenderer (SfzNoteSynthesizer private class in SongRenderer.cs) so the sampler: dispatch reuses the existing RenderSection / SequenceRenderer / BarRenderer / VoiceAllocator pipeline verbatim — Phase 28 voice pool, Phase 28 reverb, pan/gain, stereo mixing all come for free without duplicating the rendering pipeline"
    - "Static-accessor lifecycle pair (CurrentSfzSampleCache + CurrentExecutionContext) mirrors Phase 29's CurrentSampleCache pattern exactly — single-engine-per-process project convention with ReferenceEquals back-to-back-engines guard in Dispose"
    - "Single StripSamplerPrefix helper drives BOTH the GM-program lookup AND the SequenceTrackName meta-event payload so the two sites cannot drift if the prefix syntax ever changes (e.g. case sensitivity, trailing colon)"
    - "Public-by-convention test surface (ResolveGmProgram + StripSamplerPrefix bumped to public) instead of InternalsVisibleTo — matches the existing EffectsFunctions / RenderingDiagnostics convention documented in their class comments"
    - "Additive-branch-with-StartsWith-gate Phase 29 byte-identical preservation — the new sampler: dispatch checks synthType.StartsWith(\"sampler:\", StringComparison.Ordinal) and short-circuits; non-sampler renders fall through to the existing dispatch with byte-identical output"

key-files:
  created:
    - flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs
    - flow-lang.Tests/Integration/Phase33/SfzMidiExportTests.cs
  modified:
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang/StandardLibrary/Audio/MidiExport.cs

key-decisions:
  - "SfzNoteSynthesizer ADAPTER strategy (not a duplicate render pipeline) — inner private class in SongRenderer.cs implementing INoteSynthesizer that captures the bound SfzData patch and delegates RenderNote calls to SfzRenderer.Render. The plan's <interfaces> sketch suggested duplicating the RenderSection mixing math; the adapter approach reuses RenderSection(SectionData, INoteSynthesizer) verbatim, which means Phase 28 voice pool / per-section reverb / pan / gain context all apply to the sampler path with zero new code. Cleaner contract, less surface area to maintain."
  - "Bumped ResolveGmProgram + StripSamplerPrefix to public (not internal). The plan suggested either InternalsVisibleTo OR a public test-hook method. flow-lang has no InternalsVisibleTo configured; existing helpers (EffectsFunctions / RenderingDiagnostics) document the public-by-convention rule explicitly in their class comments. Bumping these two helpers to public matches that convention without adding the InternalsVisibleTo build dependency."
  - "TrackName_StripsSamplerPrefix uses C# direct API (BuildAndWriteSamplerViolinSong helper) instead of a Flow-source script. Flow identifiers don't accept the `:` character, so a script CAN'T author a sequence literally named `sampler:violin`. The MIDI export path consumes SongData by string name regardless of how the song was assembled — direct C# construction sidesteps the identifier restriction without changing the export-side surface under test."
  - "RenderTuning is intentionally discarded by SfzNoteSynthesizer's RenderNote (`_ = tuning`). SFZ patches encode their own pitch table via sample.pitch_keycenter + varispeed shift, so the Phase 23 tuning system does not apply to the sampled path. The `_ =` discard is documented inline so future readers see this is deliberate rather than a missing wire-through."
  - "Plan's <verify> step refers to 'Phase29.RmsBaselineTests' but that class doesn't exist — the actual byte-identical regression gate is 'Phase29.Phase29ByteIdenticalTests'. The 6-flavor instrument matrix (piano, brass, sax, strings, flute, drums) is what the plan author meant. Verified all 6 stay green after Plan 33-07's changes."

patterns-established:
  - "INoteSynthesizer-adapter pattern for non-synthesis renderers — wrap any per-note renderer (SFZ, future sample-bundle extensions, custom DSP) in an INoteSynthesizer adapter and feed it through the existing SongRenderer.RenderSection. Free reuse of the Phase 28 voice pool + per-section reverb + pan/gain pipeline."
  - "Plan-text-correction protocol — when a plan's <verify> step names a non-existent test class, do the verification work using the actual test class that fulfils the verification intent and document the correction in key-decisions. Bias toward fulfilling the intent rather than fabricating a test class to match the typo."

requirements-completed: [SPEC-1, SPEC-6]

# Metrics
duration: 18min
completed: 2026-05-16
tasks: 3
commits: 3
files-touched: 6
new-test-classes: 2
new-test-facts: 15
---

# Phase 33 Plan 33-07: SFZ Audio + MIDI Pipeline Wiring Summary

**`Sfz violin = (loadSfz #violin)` followed by `renderSong song "sampler:violin"` now produces real audio AND `writeMidi` of a sampler-instrument song emits the correct GM-program track — Phase 29 byte-identical contract preserved by gating every change on `synthType.StartsWith("sampler:")`.**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-16T03:28:20Z
- **Completed:** 2026-05-16T03:46:34Z
- **Tasks:** 3 (Interpreter+FlowEngine wiring; SongRenderer dispatch + tests; MidiExport prefix-strip + tests)
- **Files modified:** 4 production + 2 new test files

## Accomplishments

- **D-12 typed-Sfz binding** — `Interpreter.ExecuteVariableDeclaration` writes `(varDecl.Name, sfzData)` into `ExecutionContext.SfzPatchRegistry` BEFORE `_context.DeclareVariable` whenever the declared type is `SfzType` and the value carries `SfzData`. Pitfall 10 last-bound-wins is naturally handled by Dictionary indexer semantics.
- **FlowEngine static-accessor pair** — `CurrentSfzSampleCache` + `CurrentExecutionContext` mirror Phase 29's `CurrentSampleCache` exactly. Per-engine `SfzSampleCache` field with ReferenceEquals back-to-back-engines guard in Dispose.
- **D-13 sampler: dispatch** — `SongRenderer.RenderSong` recognizes `synthType.StartsWith("sampler:", StringComparison.Ordinal)` BEFORE the existing Phase 29 path; strips the prefix; reads the registry; on miss throws `InvalidOperationException` with the locked composer-facing message format including the known-names list and the `Did you forget Sfz {name} = (loadSfz #...)?` hint. On hit, eager-loads via `FlowEngine.CurrentSfzSampleCache.EagerLoad` then routes through a private `SfzNoteSynthesizer` adapter so the existing `RenderSection` / `SequenceRenderer` / `BarRenderer` / `VoiceAllocator` pipeline is reused verbatim.
- **Advisory #2** — one-shot stderr `[sfz] SFZ patch '{name}' not loaded (config-disabled) — sampler:NAME requires 'use \"@sfz\"' before binding` (when `SfzEnabled=false`) OR `[sfz] SFZ patch '{name}' not loaded; voice rendered as silence` (when registry is empty but `SfzEnabled=true`). Fires BEFORE the throw so composers see the stderr guidance even if the exception is caught upstream. Sentinel-keyed dedup prevents flooding.
- **D-15 sampler: prefix-strip in MidiExport** — first statement after the empty-name check (Pitfall 6); single `StripSamplerPrefix` helper drives BOTH the GM-program lookup AND the SequenceTrackName meta-event payload.
- **D-16 12 new GM-program entries** — violin (40), viola (41), cello (42), contrabass (43), oboe (68), clarinet (71), bassoon (70), horn (60), trombone (57), tuba (58), timpani (47/ch9), choir (52), harp (46), guitar (24), harpsichord (6), celeste (8). Ordered MORE-SPECIFIC-FIRST so `horn` resolves to (60, 0) French horn instead of falling through to the Phase 28 brass→56 entry.
- **D-17 SequenceTrackName meta-event** — added at tick 0 of every per-sequence track with the prefix-stripped name. Receiving DAWs display "violin" not "sampler:violin".
- **15 integration test facts** — 5 in SfzBindingTests (audio path) + 10 in SfzMidiExportTests (MIDI path), all green.
- **Phase 29 byte-identical contract preserved** — `Phase29.Phase29ByteIdenticalTests` (6 flavors: piano, brass, sax, strings, flute, drums) stays 6/6 green.
- **Phase 28 MultiTrackMidi contract preserved** — `Phase28.MultiTrackMidiTests` (5 facts) stays 5/5 green; the SequenceTrackName meta-event is structural-additive (doesn't change chunk count, ProgramChange events, NoteOn/NoteOff events, or tick alignment).

## Task Commits

| # | Name                                                          | Commit    |
| - | ------------------------------------------------------------- | --------- |
| 1 | Interpreter typed-binding + FlowEngine cache + statics        | `d6681d4` |
| 2 | sampler:NAME dispatch in SongRenderer + SfzBindingTests       | `20ee7d3` |
| 3 | MidiExport prefix-strip + 12 new GM entries + SfzMidiExportTests | `7b919fa` |

Plan metadata commit: _(orchestrator-managed in worktree mode)_

## Files Created/Modified

### Created
- `flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs` — 5 facts: SamplerDispatch_Render_NonEmpty, SamplerDispatch_UnknownName_Errors, SamplerDispatch_WithoutImport_Errors (the SPEC-1 closure deferred from Plan 33-05), SamplerDispatch_NonSamplerInstrument_FallsThroughToPhase29Path (StartsWith() contract pin), SamplerDispatch_MultipleBindings_AllRegistered (Pitfall 10 last-bound-wins).
- `flow-lang.Tests/Integration/Phase33/SfzMidiExportTests.cs` — 10 facts: 7 ResolveGmProgram unit-style facts (SamplerPrefix_Stripped, SamplerPrefix_Flute_RoutesCorrectly, NewEntry_Violin/Cello/Timpani_Channel9, NewEntry_Horn_BeatsBrass, ExistingEntry_Brass/Piano_Unchanged) + TrackName_StripsSamplerPrefix (end-to-end MIDI write+read) + Phase28_MidiExport_NonSamplerInstruments_StillWork (regression spot-check).

### Modified
- `flow-lang/Interpreter/Interpreter.cs` — D-12 typed-Sfz binding hook added inside `ExecuteVariableDeclaration` BEFORE `_context.DeclareVariable`. Single 7-line branch with inline rationale comment.
- `flow-lang/Core/FlowEngine.cs` — `_sfzSampleCache` field, `CurrentSfzSampleCache` static accessor, `CurrentExecutionContext` static accessor, ctor publication of both statics, Dispose cleanup mirroring the existing CurrentSampleCache pattern. Plan 33-05's `SfzBuiltins.Register(internalRegistry, _context)` line on line 86 was verified-via-grep but NOT re-added (duplicate-edit prevention; grep gate asserts exactly 1 occurrence).
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — D-13 sampler: dispatch branch in `RenderSong` BEFORE the existing Phase 29 path; new private `RenderSongWithSfz(SongData, string)` helper; new private `SfzNoteSynthesizer : INoteSynthesizer` adapter class. Plus `using FlowLang.Diagnostics; using FlowLang.StandardLibrary.Audio.Sfz;` at the top.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — `StripSamplerPrefix` (public helper), `ResolveGmProgram` rewritten with prefix-strip + 12 new entries (visibility bumped to public). `SequenceTrackInfo` constructor takes a new `trackName` parameter and emits a `SequenceTrackNameEvent` at tick 0 when the name is non-empty. Call site in `ExportMidiInternal` passes `StripSamplerPrefix(seqName)` as the track name.

## Decisions Made

- **SfzNoteSynthesizer adapter strategy** — the plan's `<interfaces>` block sketched a duplicated-RenderSection approach. I picked a cleaner adapter strategy: a private `SfzNoteSynthesizer` class implementing `INoteSynthesizer` that wraps the bound `SfzData` patch + an `SfzRenderer`. The existing `RenderSection(SectionData, INoteSynthesizer)` overload is then reused verbatim, so the entire Phase 28 voice pool / per-section reverb / pan / gain pipeline applies to the sampler path with zero new code. Less surface area, free Phase 28 features.
- **Public visibility for ResolveGmProgram + StripSamplerPrefix** — bumped from internal to public to support the cross-assembly direct-test calls in SfzMidiExportTests. This matches the existing convention (documented inline in EffectsFunctions.cs:318 and RenderingDiagnostics.cs:43) — flow-lang has no `InternalsVisibleTo` configured, and existing testable helpers use public-by-convention rather than InternalsVisibleTo.
- **Direct C# API for TrackName_StripsSamplerPrefix** — Flow identifiers don't accept the `:` character, so a script can't author a sequence literally named `sampler:violin`. The MIDI export path consumes `SongData` by string name regardless of authoring path; the C# API path exercises the export-side prefix-strip without requiring a Flow-source workaround.
- **RenderTuning intentionally discarded by SfzNoteSynthesizer.RenderNote** — SFZ patches encode their own pitch table via `pitch_keycenter` + varispeed shift, so the Phase 23 tuning system does not apply to the sampled path. Documented inline with `_ = tuning` and a one-paragraph rationale comment.
- **Plan's <verify> typo correction** — the plan refers to `Phase29.RmsBaselineTests` but that class doesn't exist. The actual byte-identical regression gate is `Phase29.Phase29ByteIdenticalTests` (6 flavors: piano/brass/sax/strings/flute/drums). I ran that suite as the byte-identical gate; all 6 stay green. Documented as a key-decision so the next maintainer doesn't grep for a non-existent class.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Plan's <verify> step references a non-existent test class**
- **Found during:** Task 2 (verification gate)
- **Issue:** The plan's `<verify>` automated step runs `dotnet test --filter "FullyQualifiedName~Phase29.RmsBaselineTests"` but no class named `Phase29.RmsBaselineTests` exists in the codebase. The closest analogue is `Phase29.Phase29ByteIdenticalTests`, which is the actual Phase 29 byte-identical regression gate (6 instrument flavors via Theory).
- **Fix:** Substituted the correct test class name in the verify command. Ran `Phase29.Phase29ByteIdenticalTests` instead — all 6 instruments stay green after Plan 33-07's changes, confirming the Phase 29 byte-identical contract is preserved.
- **Files modified:** None (verification-step correction only; no plan-file edit)
- **Verification:** All 6 `Phase29ByteIdenticalTests.RealismAbFixture_TwoRunsProduceIdenticalWav` (piano, brass, sax, strings, flute, drums) pass.
- **Committed in:** N/A — verification step only

**2. [Rule 2 - Missing Critical Functionality] Test scripts needed `use "@audio"` for renderSong visibility**
- **Found during:** Task 2 (initial test run RED phase)
- **Issue:** The plan's example test script in the `<action>` body sketched `use "@sfz"` only, but `renderSong`'s forward-decl lives in `audio.flow` (not `std.flow` or `sfz.flow`). Without `use "@audio"`, all 5 SfzBindingTests facts fail with "Function 'renderSong' not found" before reaching the sampler: dispatch branch.
- **Fix:** Added `use "@audio"` as the first import in every SfzBindingTests script + the parametric `Phase28_MidiExport_NonSamplerInstruments_StillWork` script. The `use "@sfz"` import remains where SFZ-specific gating is needed.
- **Files modified:** `flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs`
- **Verification:** All 5 SfzBindingTests facts pass.
- **Committed in:** `20ee7d3` (Task 2)

**3. [Rule 2 - Missing Critical Functionality] Removed extraneous tempo-block wrapper from test scripts**
- **Found during:** Task 2 (initial test run iteration)
- **Issue:** The plan's `<action>` script sketch wrapped declarations in a `tempo 120 { ... }` block. Variables declared inside the block scope to the block's frame, which `runner.GetVariable("mix")` (which reads from `GlobalFrame`) cannot reach.
- **Fix:** Lifted `Sfz`, `Section`, `Song`, `Buffer mix` declarations to file scope — they're now in the global frame and reachable via `GetVariable`. The musical-context behavior is unchanged because there is no nested musical-context-dependent rendering involved in these tests.
- **Files modified:** `flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs`
- **Verification:** `GetVariable("mix").As<AudioBuffer>().Frames > 0` succeeds.
- **Committed in:** `20ee7d3` (Task 2)

---

**Total deviations:** 3 (1 Rule 3 verification-step typo correction; 2 Rule 2 missing critical functionality in plan's test-script sketch).

**Impact on plan:** All deviations were prerequisites or corrections to the plan's example-script sketch — the actual production-code instructions in the plan (Interpreter D-12 hook, FlowEngine statics, SongRenderer D-13 dispatch, MidiExport D-15/D-16/D-17) landed exactly as specified. No scope creep; no behavior beyond what the plan's `<success_criteria>` required.

## Issues Encountered

- **Pre-existing Phase 28 test failures (26)** — same 26 failures Plan 33-05's summary documented (24 PerSynthArticulation FFT facts + 2 Ragtime RmsRegression facts). Verified pre-existing by `git stash` + re-run before my changes: same 24 PerSynthArticulation failures present without Task 3's MidiExport changes applied. Out-of-scope per the executor SCOPE BOUNDARY rule.

## User Setup Required

None — Plan 33-07's changes are entirely self-contained within the engine surface. The composer-facing entry points (`use "@sfz"`, `Sfz {name} = (loadSfz #...)`, `renderSong song "sampler:{name}"`, `writeMidi path song`) are now end-to-end functional; the existing per-install `sfz_root` config requirement (introduced in Plan 33-05) is the only setup step downstream composers need to perform, and Plan 33-05's SfzGatingTests already pin its error-path contract.

## Threat Model Compliance

| Threat ID         | Disposition | Mitigation Status                                                                                                |
| ----------------- | ----------- | ---------------------------------------------------------------------------------------------------------------- |
| T-33-DISPATCH-01  | accept      | UnknownSamplerNameError lists registry keys; same posture as Phase 32 listing TuningStack keys.                  |
| T-33-MIDI-01      | mitigate    | Prefix-strip is the FIRST statement after the empty-name check; SamplerPrefix_Flute_RoutesCorrectly fact pins it. |
| T-33-REG-01       | mitigate    | All Phase 33 changes are additive branches BEFORE the existing dispatch; Phase29.Phase29ByteIdenticalTests is the gate (6/6 green). |
| T-33-DUP-01       | mitigate    | Task 1's verify gate asserts `grep -c "SfzBuiltins.Register" flow-lang/Core/FlowEngine.cs` equals 1 (verified).   |

All four threats from the plan's `<threat_model>` are mitigated or accepted per the locked dispositions; the two `mitigate` ones (T-33-MIDI-01 prefix-strip ordering + T-33-REG-01 Phase 29 byte-identical preservation) have direct test-suite assertions confirming the mitigations hold.

## Known Stubs

None. All shipped code paths produce real values, real errors, or real one-shot advisories with composer-facing diagnostic text. The SfzNoteSynthesizer's `RenderTuning` discard is documented inline as deliberate (SFZ patches encode their own pitch table) — not a stub.

## Threat Flags

None. No new network endpoints, auth paths, file-access patterns, or schema changes at trust boundaries beyond what the plan's threat model already enumerated.

## Next Phase Readiness

- **Phase 33 closes here.** SPEC-1 + SPEC-6 acceptance criteria all green. The four-wave Phase 33 timeline (Wave 1 Plan 33-01 audit; Wave 2 Plans 33-02 + 33-03 + 33-04; Wave 3 Plans 33-05 + 33-06; Wave 4 Plan 33-07) is complete. Composer-facing surface is end-to-end functional: `use "@sfz"; Sfz violin = (loadSfz #violin); renderSong song "sampler:violin"` produces real audio AND `writeMidi path song` emits a GM-compatible .mid file with correct ProgramChange + SequenceTrackName events.
- **Phase 34 (symphony showcase)** is the downstream consumer. Available now:
  - All 19 GM orchestral symbols from the Plan 33-01 audit reachable via `(loadSfz #symbol)` (15 immediate + 4 TBD with workaround via the absolute-path overload).
  - `sampler:NAME` dispatch correctly resolves bound patches AND fails fast with an actionable error message on misses.
  - Multi-instrument symphony score export via `writeMidi` produces sensible GM programs per voice (violin → 40, cello → 42, etc.) without VSCO-CE installed on the receiver.
- **No blockers** — Plan 33-07 is the final Plan in Phase 33.

## Self-Check: PASSED

Files-on-disk verification:

```
FOUND: flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs
FOUND: flow-lang.Tests/Integration/Phase33/SfzMidiExportTests.cs
FOUND: flow-lang/Interpreter/Interpreter.cs (modified — SfzPatchRegistry hook)
FOUND: flow-lang/Core/FlowEngine.cs (modified — CurrentSfzSampleCache + CurrentExecutionContext)
FOUND: flow-lang/StandardLibrary/Audio/SongRenderer.cs (modified — sampler: dispatch + SfzNoteSynthesizer)
FOUND: flow-lang/StandardLibrary/Audio/MidiExport.cs (modified — D-15 + D-16 + D-17)
```

Commit verification (worktree-agent-ad4abc88351f6b171 branch):

```
FOUND: d6681d4  feat(33-07): SFZ patch registry + per-engine SfzSampleCache + statics (Task 1)
FOUND: 20ee7d3  feat(33-07): sampler:NAME dispatch in SongRenderer + SfzBindingTests (Task 2)
FOUND: 7b919fa  feat(33-07): MidiExport sampler: prefix-strip + 12 new GM entries (Task 3)
```

Test verification:
- `dotnet test --filter "FullyQualifiedName~Phase33.SfzBindingTests|FullyQualifiedName~Phase33.SfzMidiExportTests"` exits 0 — **Passed 15 / Failed 0**.
- `dotnet test --filter "FullyQualifiedName~Phase29.Phase29ByteIdenticalTests"` exits 0 — **Passed 6 / Failed 0** (Phase 29 byte-identical preserved).
- `dotnet test --filter "FullyQualifiedName~Phase28.MultiTrackMidi"` exits 0 — **Passed 5 / Failed 0** (Phase 28 MIDI multi-track preserved).
- `dotnet test --filter "FullyQualifiedName~Phase33"` exits 0 — **Passed 63 / Failed 0** (full Phase 33 suite green: Plan 33-04/05/06/07).
- `dotnet test --filter "FullyQualifiedName~Phase32|FullyQualifiedName~Phase26|FullyQualifiedName~Phase30"` exits 0 — **Passed 218 / Failed 0** (no upstream regression).
- `grep -c "SfzBuiltins.Register" flow-lang/Core/FlowEngine.cs` returns **1** (duplicate-edit prevention; Plan 33-05 owns the insertion).

---
*Phase: 33-sfz-orchestral-sampler*
*Completed: 2026-05-16*
