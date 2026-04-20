---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Stability & Composer DX
status: "Phase 14 context gathered — 14-CONTEXT.md + 14-DISCUSSION-LOG.md written (commit 03b4fff). Key decisions: slice silent-clamp atomic · enharmonic key-context-aware · H alias deferred to future pragma phase (candidate keyword `enable`) · DX-06 flat surface extended to arbitrary b/#/+/- composition · DX-08 two-pass strict regression · 4-plan structure with wave-1 parallel. Ready for /gsd-plan-phase 14."
stopped_at: "Phase 14 context captured (03b4fff). Next: /gsd-plan-phase 14"
last_updated: "2026-04-20T04:30:00.000Z"
last_activity: 2026-04-20
progress:
  total_phases: 7
  completed_phases: 3
  total_plans: 17
  completed_plans: 17
  percent: 43
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-18)

**Core value:** Users can write musical ideas as code and hear them immediately -- the language must faithfully translate musical notation into correct, playable audio.
**Current focus:** Phase 14 — composer-dx-part-1 (context gathered)

## Current Position

Phase: 14
Plan: Not started (context captured, ready to plan)
Status: Phase 14 context gathered (03b4fff). DX-05/06/08 decisions locked; DX-06 H alias deferred to future pragma phase. 4-plan structure (14-01 slice · 14-02 flats+enharmonic · 14-03 DX-08 regression · 14-04 closure) with 14-01/02/03 wave-1 parallel. Ready for /gsd-plan-phase 14.
Last activity: 2026-04-20 - Phase 14 context gathered (14-CONTEXT.md + 14-DISCUSSION-LOG.md)

Progress: [█████████░░░] 43% (3/7 v1.2 phases complete — 11, 12, 13; phase 17 Language Server added to roadmap)

## Performance Metrics

**Velocity:**

- Total plans completed: 11 (v1.2 milestone)
- Average duration: -
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 12 | 6 | - | - |
| 13 | 5 | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
| Phase 01 P03 | 4min | 2 tasks | 4 files |
| Phase 01 P01 | 11min | 2 tasks | 15 files |
| Phase 01 P02 | 7min | 2 tasks | 6 files |
| Phase 02 P01 | 6min | 2 tasks | 8 files |
| Phase 02 P03 | 8min | 2 tasks | 5 files |
| Phase 02 P02 | 16min | 2 tasks | 13 files |
| Phase 02 P02 | 2min | 2 tasks | 13 files |
| Phase 04 P01 | 5min | 2 tasks | 6 files |
| Phase 09 P02 | 5min | 1 tasks | 1 files |
| Phase 10 P01 | 2min | 2 tasks | 3 files |
| Phase 10 P02 | 2min | 2 tasks | 5 files |
| Phase 12 P01 | 20min | 2 tasks | 5 files |
| Phase 12 P02 | 1min | 2 tasks | 2 files |
| Phase 12 P03 | 10min | 2 tasks | 3 files |
| Phase 12 P04 | 5min | 2 tasks | 3 files |
| Phase 12 P05 | 19min | 2 tasks | 7 files |
| Phase 12 P06 | 4min | 2 tasks | 3 files |
| Phase 13 P01 | 5min | 2 tasks | 4 files |
| Phase 13 P02 | 8min | 2 tasks | 3 files |
| Phase 13 P03 | 15min | 2 tasks | 4 files |
| Phase 13 P04 | 4.5min | 2 tasks | 3 files |
| Phase 13 P05 | 14min | 3 tasks | 6 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap v1.2]: Audit Spike must be Phase 11 (own phase) because architecture and pitfalls researchers disagreed on C1-C5 reality; fixing-without-reproducing risks regressions
- [Roadmap v1.2]: FIX-07 scope is contingent on SPIKE outcome; migration-comms work for C5 (if confirmed) bundles into Phase 12, not a separate phase
- [Roadmap v1.2]: Nyquist validation backfill is own phase (13) to avoid bundling with bug-fix confirmation bias
- [Roadmap v1.2]: DX split across Phases 14/15 by blast radius — DX-05/DX-06/DX-08 first, DX-07/DX-09 last; DX-07 (reverbTime, 9 files) ships last
- [Roadmap v1.2]: DX-08 (MIDI velocity) precedes DX-09 (euclidean humanize) because euclidean reuses the velocity infrastructure
- [Roadmap v1.2]: Tutorial refresh (Phase 16) is last because it documents shipped reality
- [Plan 12-01]: xUnit vsix adapter fallback — xunit.v3.runner.visualstudio 3.2.2 does not exist on nuget.org; substituted xunit.runner.visualstudio 3.1.5 (shared v2/v3 adapter)
- [Plan 12-01]: FlowEngineRunner.FlushErrorsToStderr mirrors flow-interpreter/Program.cs:78 so ExpectedErrorScripts substring assertions can match FormatErrors output
- [Plan 12-01]: Theory sets Environment.CurrentDirectory to repo root so relative-path test scripts (test_wav_loading, test_full_song) resolve as they would under `dotnet run --project flow-interpreter`
- [Plan 12-01]: test_custom_oscillator + test_full_song registered as pre-fix-baseline ExpectedErrorScripts entries; plan 12-05 will remove them after if-overload and exportWav-mkdir fixes land
- [Plan 12-02]: FIX-05 error message is literal `"Cannot get init of empty array"` matching Head/Last suffix pattern at Collections.cs:55-56 and :78-79 (per CONTEXT D-07 authority)
- [Plan 12-02]: Task 1 (RED tests) and Task 2 (GREEN fix) bundled into ONE atomic commit (6e5a960) for bisectability; tests authored pre-fix to prove RED before landing GREEN, preserving TDD discipline within the atomic-commit constraint
- [Plan 12-02]: BuiltInFunctions.cs:352-353 registration left untouched per plan; only Collections.Init implementation raises on empty. Signature (ArrayType(VoidType)) stable — no caller-facing API change beyond the empty-path behavior
- [Plan 12-03]: Thunk replaced with Lazy<Value> in LazyThreadSafetyMode.ExecutionAndPublication — single BCL primitive satisfies both D-05 (ExceptionDispatchInfo stack preservation via Lazy's internal Capture/.Throw) and D-06 (thread-safe memoization); 49→44 lines; _expression/_evaluator/_cachedValue/_isEvaluated/_lock fields removed
- [Plan 12-03]: ExpressionEvaluator.Evaluate promoted to virtual (1-token change) to enable CountingEvaluator test double — non-breaking (zero existing subclasses verified via grep); Rule 3 deviation (blocking for test authorship)
- [Plan 12-03]: 4 ThunkTests Facts shipped (success-cache, failure-cache with Assert.Same re-throw-identity, stack preservation, 5-call failure-cache durability) — plan called for 3; 4th added under Rule 2 to strengthen regression coverage of failure-cache durability
- [Plan 12-03]: IsEvaluated (Lazy.IsValueCreated) returns false after a factory-thrown exception per Microsoft docs; matches pre-refactor contract (old code only set _isEvaluated on success path) — public contract identical
- [Plan 12-04]: 7 return;→break; edits inside ExecuteMusicalContext switch flip spike/c1 RED→GREEN mechanically; AUDIT-VERIFIED marker at Interpreter.cs:292 updated from Phase 11 Confirmed to Phase 12 Fixed. Commit 327aa3c bundles Interpreter.cs edit with tests/test_musical_context_errors.flow sentinel update ("should not print - negative tempo" → "body ran under partial tempo context") per RESEARCH Pitfall 1 so HEAD never carries a misleading sentinel.
- [Plan 12-04]: Unit-test sources in flow-lang.Tests/Unit/InterpreterTests.cs must prepend `use "@std"` — FlowEngine.Execute does not auto-import stdlib; without std.flow's `internal proc print (String: s)` declaration, `print` is unresolved at parse time even though the C# StdLib.Print is registered at engine init. Rule 3 deviation discovered when initial test authoring per 12-PATTERNS §7 template failed with empty stdout; mirrors the stdlib-usage contract every .flow test script already follows.
- [Plan 12-04]: Soft-failure contract preserved — all 18 `_errorReporter.ReportError` calls in Interpreter.cs unchanged; frame-balance try/finally PushFrame/PopFrame pairing at lines 133/287-290 untouched. `break;` exits the switch but stays inside the `try` block, so `finally { PopFrame(); }` still runs on every path.
- [Plan 12-05]: CONTEXT D-16 overridden per RESEARCH Pitfall 5 — registered `if(Bool, Void, Void)` Void-wildcard overload (NOT String-specific). Wildcard covers String case at test_custom_oscillator.flow:42 AND Double case at line 57 and Int/Float/any concrete T. StdLib.IfStrict is a 2-line body leveraging the interpreter's strict-arg evaluation.
- [Plan 12-05]: CONTEXT D-02 scope extended per RESEARCH Pitfall 6 — Directory.CreateDirectory lives in shared ExportWavInternal (FileIO.cs:58-60), fixing exportWav, exportWavWithBitDepth, writeWav, writeWavWithBitDepth in one 4-line edit.
- [Plan 12-05]: InternalFunctionRegistry.TypesEqual tightened to exclude LazyType from VoidType-wildcard matching. Required because two .flow `if` proc declarations now exist (Lazy + strict); without the tightening, insertion-order iteration pairs BOTH to StdLib.If → Thunk-cast failure on strict args. Verified no other stdlib relies on Void-wildcard matching LazyType. Rule 3 deviation (blocking for Task 1).
- [Plan 12-05]: tests/test_custom_oscillator.flow:57 rewritten to use `Double posOne = 1.0 / Double negOne = (sub 0.0 1.0)` variables. Parser interprets `1.0 -1.0` as binary subtraction (collapses to one arg), causing `if(Bool, Double)` dispatch error. Follows test_panning.flow convention for negative Double literals. Rule 1 deviation (test-file bug blocking plan verification).
- [Plan 12-05]: Test 4 of test_custom_oscillator.flow deferred — `range` stdlib function missing (documented in CLAUDE.md, never registered). DEFER-01 logged in .planning/phases/12-stability/deferred-items.md with proposed fix for plan 12-06. ExpectedErrorScripts entry for test_custom_oscillator swapped from `"No matching overload for function 'if'"` to `"Function 'range' not found"` — Theory row stays GREEN. Scope boundary per deviation rules (pre-existing bug unrelated to plan 12-05 changes).
- [Plan 12-06]: DEFER-01 forward-referenced to a future phase, not implemented in 12-06. Plan frontmatter explicitly scopes 12-06 as documentation-only ("No code changes. Documentation only."); implementing `range` inline would violate the 2-commit atomic contract and bundle a stdlib feature addition into a milestone-close commit. Interim FlowScriptData.cs pin from plan 12-05 keeps the Theory row GREEN. Proposed 3-step implementation documented in deferred-items.md + 12-VERIFICATION.md §Deferred Items.
- [Plan 12-06]: TEST-01 closed as "audit false positive" despite `range` being genuinely missing. The false-positive framing applies to the audit's "blocks test_custom_oscillator" claim — Tests 1/2/3 are blocked by if-overload (TEST-03 territory), and Test 4's range dependency is orthogonal. The audit conflated two distinct problems under one REQ-ID.
- [Plan 12-06]: Status-marker vocabulary extended — `Closed (audit false positive)` introduced as a first-class Traceability row marker, distinct from `Shipped <hash>` (real commit, bisect-revertable) and `Pending` (not done). Preserves audit-trail visibility.
- [Plan 12-06]: CLAUDE.md updates deferred — `dotnet test flow-sharp.sln` is now canonical, and the net10.0-vs-net9.0 target-framework doc lag is known, but both are optional per CONTEXT and not part of the 2-commit atomic scope. Tracked for a future doc-hygiene pass.
- [Plan 13-01]: Two-pass strict authorship produced zero Divergences on Phase 6 because the v1.1 audit had already reconciled REQUIREMENTS.md with shipped behavior (FIX-02 × AUDIO-06 composition gap surfaced and fixed via commit 2156690 pre-milestone-close). Pass 1 draft and Pass 2 reality matched verbatim across QOL-01 prefix, FIX-01 sentinels, FIX-02 gain-nested script body, and FIX-03 error string.
- [Plan 13-01]: FIX-02 × AUDIO-06 regression gate authored as stdout numeric-frame assertion (Assert.DoesNotContain "frames: 0\n") rather than replacing the .flow sentinel script, because the pre-fix bug silently rendered 0 frames while still printing "PASSED". The new Fact fails under the pre-fix bug; the existing .flow Theory row would not have.
- [Plan 13-01]: Rule 2 addition — VerboseFlagTests.RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix pins the negative QOL-01 case. Pass 1 draft only specified positive case; without the negative pin a future refactor emitting `[verbose]` unconditionally would still pass QOL-01 regression.
- [Plan 13-01]: FIX-01 sentinel tightening uses RequiredSentinels append (not a new Fact) because test_transpose_int.flow already runs via Plan 12-01's Theory harness with errorCount==0 gate; additive sentinel ("transpose with int: ok" + "test_transpose_int: PASSED") converts the row from errorCount-only to substring-pinned. Matches D-19 (existing coverage wins).
- [Plan 13-01]: New directory convention `flow-lang.Tests/Integration/Phase{NN}/` established for phase-scoped Integration Facts — replicable for 13-02..13-04.
- [Plan 13-02]: DX-02 Double format drift DOCUMENTED per Pitfall 5 — Flow's `str` emits 10-sig-digit precision (`str pi` → `"3.141592654"`), NOT `Math.PI.ToString()` full precision (`"3.141592653589793"`). Pass 1 draft would have produced a RED sentinel if the plan had not explicitly warned. First divergence logged under two-pass strict in the v1.2 backfill series — validates the protocol.
- [Plan 13-02]: DX-02 whole-valued-Double Flow `str` format strips trailing `.0` — `sin 0.0` → `"0"`, `sqrt 16.0` → `"4"`, `pow 2.0 10.0` → `"1024"`. Chose `"1024"` as pow sentinel because it unambiguously pins pow-registration (short numerics like `"0"` / `"1"` match too many substrings).
- [Plan 13-02]: DX-04 proxy-Fact pattern — REPL auto-import is "not e2e-testable via piped stdin" per v1.1 audit line 49 (piped stdin routes to `RunFromStdin`, script mode, not the REPL — confirmed at 07-02-SUMMARY.md line 104). `RepLAutoImportTests.AutoImportedModulesResolve_StdAudioCollections` executes the SAME three `use` statements `Repl.cs::AutoImportStandardModules` hardcodes, asserting symbol resolution. Best automatable proxy.
- [Plan 13-02]: Array[Int] is NOT valid Flow type syntax — idiomatic array declaration is `Int[]` (confirmed at `tests/test_lambdas.flow:40`). Rule 3 blocking substitution in RepLAutoImportTests Fact source; logged in 07-VALIDATION.md §Divergences DX-04.
- [Plan 13-02]: Sentinel specificity matters — `"note stream ok"` (line 31 of test_comments.flow) pins the post-note-stream inline-`//` case distinctly from `"42"` (line 40, after empty-`//` on line 39). Each sentinel gates a distinct comment style; single-sentinel pins would not catch partial-support regressions.
- [Plan 13-03]: AudioCore.Mix signature is `(IReadOnlyList<Value> args)`, not `(AudioBuffer, AudioBuffer)` — plan template drafted the latter. Unit Fact wraps raw AudioBuffer instances via `Value.Buffer(...)` into the dispatcher arg shape, then unwraps result via `.As<AudioBuffer>()`. Mirrors CollectionsTests's `Value.Array(...)` + `.As<IReadOnlyList<Value>>()` pattern; discovered during Pass 2 reality check. Rule 3 deviation.
- [Plan 13-03]: SynthesizerFactory lives in `FlowLang.StandardLibrary.Audio` (outer), while synth classes live in `FlowLang.StandardLibrary.Audio.Synthesizers` (inner). Unit Fact imports BOTH namespaces. Second namespace-layer discrepancy surfaced by two-pass strict (first was 13-02's `Int[]` vs `Array[Int]` grammar correction).
- [Plan 13-03]: test_mix.flow "mix channels" sentinel is empirical 2 (stereo), not drafted 1 (mono) — `createSineTone` produces stereo buffers, so mix(stereo, stereo) returns stereo without triggering the MonoToStereo promotion path. Sentinel-drift pattern is consistent with 13-02's DX-02 Double-format drift (Pitfall 5): drafting from REQUIREMENTS produces assumptions that empirical stdout refutes.
- [Plan 13-03]: AUDIO-06 × FIX-02 cross-reference pattern — 08-VALIDATION.md cites `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` in its Per-Task Verification Map instead of authoring a duplicate Fact. First cross-phase citation in the 13-series; pattern available for future intersection requirements.
- [Plan 13-04]: Two-pass strict produced ZERO Divergences on Phase 9 — AUDIO-08 and QOL-02 both literally testable as drafted. `tests/test_tempo_ramp.flow` already encodes ritardando/accelerando invariants as `(concat "Test N - …: " (str testN))` boolean prints; all three sentinel strings matched Pass 1 draft verbatim. Second zero-divergence plan in the 13-series (after 13-01) — pattern: when v1.1 audit + Phase 12 stability have already reconciled requirements-vs-reality, Pass 1 and Pass 2 match verbatim.
- [Plan 13-04]: `examples/tutorial.flow` runs GREEN under HEAD (post-Phase-12) with exit 0, empty stderr, `/tmp/flow_tutorial_output.wav` produced. Defensive `[Fact(Skip=…)]` + `## Ultra-Important Finding` branch documented in plan was NOT triggered. Phase 16 QOL-03 remains scoped to tutorial feature-refresh (v1.1+v1.2 feature demonstration), independent of this correctness pin.
- [Plan 13-04]: Boolean-result-concat sentinel pattern documented — .flow scripts that `(concat "Test N - …: " (str bool))` their in-script invariants make backfill-friendly regression tests trivial (Bool `str` → "true"/"false"). Recommended idiom for future test-script authoring.

### Roadmap Evolution

- Phase 17 added (2026-04-20): Flow Language Server — LSP + VSCode extension for syntax highlighting, diagnostics, and intelligent completion/hover suggestions on .flow files

### Pending Todos

None yet for v1.2 execution.

### Blockers/Concerns

- Phase 11: Researcher disagreement on C1-C5 — cannot proceed to FIX-07 until spike resolves
- Phase 12: C5 (augment/diminish swap) confirmation determines whether BREAKING CHANGE migration artifacts (release notes, transitional aliases, example audit) are required for v1.2 release
- Phase 14 (DX-08): NoteStreamCompiler velocity propagation (647-line file) may be complete already — verification pass may obviate new-code work
- Phase 14 (DX-06) / Phase 15 (DX-07): Identifier collision grep required before landing (`H`, `Db`, `Eb`, `Fb`, `Cb`, `Bb`, `Gb`, `Ab`, `enharmonic`, `reverbTime`)
- Phase 15 (DX-09): Pinned PRNG (xorshift64* / splitmix64) required — `System.Random` is not stable across .NET patch versions and would violate "code is the score" reproducibility

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260420-0c0 | Add pure-Flow test library (flow-lang/test.flow + tests/test_test_library.flow) | 2026-04-20 | c8731d2 | [260420-0c0-write-a-pure-flow-test-library-at-flow-l](./quick/260420-0c0-write-a-pure-flow-test-library-at-flow-l/) |

## Session Continuity

Last session: 2026-04-20T03:55:00Z
Stopped at: Phase 13 Nyquist Validation Backfill closed (13-05 completed 2026-04-20; Phase 10 VALIDATION.md promoted to nyquist_compliant: true; 4 new Facts under flow-lang.Tests/Unit/Phase10/ (FormantSynthesizer 88200-pin, FormantData unknown-vowel, TtsHook round-trip + empty-command); test_vocalization.flow Theory row sentinel-tightened; TEST-04 marked Shipped 21e773d; 81/81 suite green)
Resume file: None — Phase 13 closed; next phase is 14 (Composer DX Part 1 — DX-05/DX-06/DX-08)
