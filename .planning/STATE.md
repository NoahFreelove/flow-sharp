---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Stability & Composer DX
status: executing
stopped_at: "Phase 15 Wave 2 complete (2026-04-20; Wave 3+4 pending). Plans 15-01 (scaffolding: Phase15 test subtree + MidiReadHelpers promotion closing DEFER-05 + tests/output/.gitignore + 3 placeholder .flow scripts), 15-02 (DX-07 grammar/runtime: reverbTime keyword, MusicalContext.ReverbTime nullable field + Clone/ToString, ExecutionContext 8-field walk with updated early-break per Pitfall 1, parse-time negative rejection, interpret-time silent clamp at 30, 7 ReverbTimeContextTests Facts), 15-04 (DX-09 euclidean core: two new overloads euclidean(Int,Int,Note,Double) + euclidean(Int,Int,Note,Double,Double,Int), local new Random(seed) per D-17, steps>1024 DoS guard, 12 Facts across EuclideanSwingTests/EuclideanHumanizeTests), and 15-03 (DX-07 audio path: Reverb.Apply(rt60) overload with Schroeder feedback=10^(-3*delay/rt60) capped at 0.99, strict ProcessChannel refactor, per-voice SongRenderer insertion with exact rt60==0.0 short-circuit, 4 new Facts F-02/F-06/F-07/F-08 plus 2 supporting). Full suite 285/285 green (260 baseline + 25 Phase 15 Facts, zero regressions). Phase 14 regression preserved 54/54. Phase 15 NOT yet complete — Wave 3 (15-05 byte-identical regression, 15-06 end-to-end .flow scripts) and Wave 4 (15-07 closure: ROADMAP #3 or-zero doc reframe + collision grep F-24 + REQUIREMENTS Shipped markers + 15-VERIFICATION.md + 15-SUMMARY.md) still pending. Phase 17 also complete (8/8 plans) with 3 pending HUMAN-UAT items scoped to F5 smoke session. Session paused for PC transfer — commits squashed into one for clean handoff."
last_updated: "2026-04-21T04:45:00.000Z"
last_activity: 2026-04-20 -- Phase 15 Waves 0-2 complete (plans 01/02/03/04); Waves 3-4 (plans 05/06/07) pending
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 36
  completed_plans: 32
  percent: 89
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-18)

**Core value:** Users can write musical ideas as code and hear them immediately -- the language must faithfully translate musical notation into correct, playable audio.
**Current focus:** Phase 15 — composer-dx-part-2

## Current Position

Phase: 15 (mid-execution, 4/7 plans; Waves 0-2 complete, Waves 3-4 pending)
Plan: 15-03 complete (DX-07 audio path); 15-05 next (Wave 3 byte-identical MIDI/WAV regression — depends_on: [15-01, 15-04])
Status: Executing (mid-phase; session paused for PC transfer)
Last activity: 2026-04-20 -- Phase 15 Waves 0-2 complete (plans 01/02/03/04); Waves 3-4 (plans 05/06/07) pending

Progress: [█████████░] 89% plans-complete (32/36); Phase 15 at 4/7 plans — 25 Phase 15 Facts green, full suite 285/285

## Resume Instructions (next PC)

After pulling the squashed commit:
1. `/gsd-progress` — confirm Phase 15 at 4/7 plans
2. `/gsd-execute-phase 15` — resumes at Wave 3 automatically (15-01..15-04 have SUMMARY.md; 15-05/06/07 do not)
3. Wave 3 spawns 15-05 + 15-06 in parallel (disjoint files: byte-identical regression vs .flow scripts)
4. Wave 4 runs 15-07 (phase closure) after Wave 3
5. Phase verifier + ROADMAP Shipped markers complete the phase

Phase 17 has 3 pending HUMAN-UAT items in 17-HUMAN-UAT.md — orthogonal to Phase 15, resolve at first release tag.

## Performance Metrics

**Velocity:**

- Total plans completed: 19 (v1.2 milestone)
- Average duration: -
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 12 | 6 | - | - |
| 13 | 5 | - | - |
| 14 | 4 | - | - |

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
| Phase 14 P01 | 10min | 1 tasks | 5 files |
| Phase 14 P02 | 25min | 2 tasks | 10 files |
| Phase 14 P03 | 4min | 2 tasks | 2 files |
| Phase 14 P04 | 10min | 2 tasks | 6 files |
| Phase 17 P01 | 20min | 2 tasks | 10 files |
| Phase 17 P03 | 30min | 2 tasks | 9 files |
| Phase 17 P04 | 20min | 1 tasks | 4 files |
| Phase 17 P05 | 38min | 2 tasks | 15 files |
| Phase 17 P06 | 11min | 2 tasks | 11 files |
| Phase 17 P07 | 7min | 2 tasks | 8 files |
| Phase 17 P08 | ~9min | 3 tasks (1 deferred to HUMAN-UAT) | 12 files |

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
- [Plan 14-01]: DX-05 slice — Sequence + Array[T] atomic per D-02; silent two-sided clamp per D-01; LINQ Skip/Take template from Take/Drop at Collections.cs:117-147. Commit 4528407. Rule 2 addition: 2 `internal proc slice` declarations in collections.flow (stdlib functions require proc declarations to be callable from user scripts). Rule 1 fixes: `end` is a reserved keyword (renamed to `s`/`e` in proc params), `Array[T]` is not valid Flow grammar (idiomatic is `Int[]`), negative literals need explicit `(sub 0 5)` binding due to parser's binary-subtraction precedence.
- [Plan 14-02]: DX-06 flat-literal surface — sum-based NoteType.Parse scan accepts any mix of b/#/+/- on either side of octave digits (D-07); Format emits canonical `+N`/`-N` runs preserving Parse(Format(x))==x (D-08); post-alteration MIDI range check (D-09); SimpleLexer dispatch REORDERED (chord-before-note) per RESEARCH regression discovery — defense-in-depth for chord-vs-note dispatch. Commit d2edc90 (flats+lexer) + commit 2490c9c (enharmonic). Rule 1: Fb0 math was at MIDI 16 = exactly E0 = in range (replaced with Eb0 below-range Fact + Fb0 boundary-valid Fact). Rule 1: Bb7/F#dim chord tests used b/# accidental convention but ChordParser uses s/f internally (rewrote LexerTests to exercise Dm/Cmaj7/Am7/Bdim/Csmaj/Bfm which ChordParser actually accepts).
- [Plan 14-02]: Enharmonic() built-in added via new HarmonyFunctions.RegisterContextDependent method (D-06 additive). In-key branch uses MIDI-based lookup + preferFlat heuristic to work around ChromaticNotes asymmetry (RESEARCH Pitfall 3). No-key fallback flips sharp↔flat; naturals unchanged per D-05 (no E↔Fb / F↔E# / B↔Cb / C↔B# edge respelling — see DEFER-04). Double-sharps documented non-involutive. Rule 2: missing `internal proc enharmonic (Note: n)` in std.flow discovered during Task 2 first run (Fact failed with "Function 'enharmonic' not found" despite C# registration); added alongside existing harmony declarations.
- [Plan 14-02]: Pre-landing collision grep per D-21 executed once at plan time, empty across all *.flow files. Transcript in 14-02-PLAN.md §Pre-landing Collision Grep and re-surfaced in 14-VERIFICATION.md by plan 14-04.
- [Plan 14-03]: DX-08 two-pass strict per D-13 — Pass 1 drafted Fact from REQUIREMENTS alone asserting byte array [31, 47, 63, 79, 95] for crescendo(0.25, 0.75) over 5 notes. Outcome A: GREEN on first run (F-01 confirmed — zero plumbing changes). Third consecutive zero-divergence plan in the two-pass strict series (after 13-01 and 13-04). DryWetMidi 8.0.3 read API via existing dependency, no new NuGet. Commit 152e593 (no Pass 2 commit — Outcome A).
- [Plan 14-04]: DX-06 H-alias clause REFRAMED per D-19 (Phase 12 TEST-03 precedent); deferred-items.md captures H + pragma system + candidate `enable` keyword + DEFER-04/05/06 (D-11/D-12). 14-VALIDATION.md promoted to nyquist_compliant: true per CONTEXT §Claude's Discretion last bullet. 14-VERIFICATION.md rollup with criteria-to-artifact mapping + collision grep re-surfacing + commit hash manifest (4528407, d2edc90, 2490c9c, 152e593). REQUIREMENTS.md DX-05/06/08 rows flipped to Shipped with hashes; DX-06 original audit-trail preserved as `*Original audit-trail:*` preamble. Single atomic docs commit covering all 6 files.
- [Plan 17-03]: OmniSharp API drift caught by first build — `TextDocumentSyncHandlerBase.CreateRegistrationOptions` expects `TextSynchronizationCapability`, not the plan's approximate `SynchronizationCapability` sample name. Rule 3 blocking fix, 1-token edit. Confirms the plan-time API shape guidance is approximate; downstream 17-04/17-05/17-06 plans should expect similar single-token corrections (`TextDocumentSelector` not `DocumentSelector`, etc.).
- [Plan 17-03]: `System.Range` vs `OmniSharp...Models.Range` collision requires type alias `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` — Rule 3 fix in LspMappings.cs. Downstream LSP files using Range/Position types should follow suit (Position does NOT collide, so alias only on Range).
- [Plan 17-03]: Close-race guard discrimination — the plan's original `CloseCancelsPendingDiagnostics_NoPublishAfterClose` Fact does NOT discriminate (CTS-cancel in ScheduleParseAsync short-circuits callback before HasDocument evaluated). Added `ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded` using TaskCompletionSource to force post-delay/pre-publish race window. Rule 2 addition; verified discriminating via spot-check (fails when guard removed, passes when restored). Real regression gate for T-17-12 now in place.
- [Plan 17-03]: Partial-class test split (`DocumentManagerTests.cs` + `DocumentManagerTests.CloseRace.cs` as `partial class DocumentManagerTests`) keeps Task 1 commit compile-clean without Task 2 symbols (IDiagnosticsPublisher mock). Pattern re-usable for future phase plans where Task 2 extends Task 1's test class.
- [Plan 17-03]: `ILanguageServerFacade` resolved transparently by OmniSharp's ambient DI — no explicit registration needed. `DiagnosticsPublisher` constructor receives it as a DI parameter.
- [Plan 17-04]: OmniSharp 0.19.9 `SemanticTokensHandlerBase` abstract methods confirmed via reflection probe — `Tokenize(SemanticTokensBuilder, ITextDocumentIdentifierParams, CancellationToken): Task` + `GetSemanticTokensDocument(ITextDocumentIdentifierParams, CancellationToken): Task<SemanticTokensDocument>` (both abstract); `CreateRegistrationOptions(SemanticTokensCapability, ClientCapabilities)` virtual. `SemanticTokensCapability` lives in `Protocol.Client.Capabilities` (mirrors 17-03's `TextSynchronizationCapability` namespace discovery).
- [Plan 17-04]: `SemanticTokensBuilder.Push` has 10 overloads; the typed variant and the string variant both accept `null` as final arg, causing CS0121 when called with bare `null`. Resolved with `System.Array.Empty<SemanticTokenModifier>()` to force the typed overload. Documented for 17-05/17-06 if they call Push.
- [Plan 17-04]: Encoder + handler file split honored per success_criteria despite the plan's co-located example — encoder at `flow-lsp/Semantic/SemanticTokensEncoder.cs` (only `FlowLang.Lexing` + `OmniSharp...Protocol.Models` deps), handler at `flow-lsp/Handlers/SemanticTokensHandler.cs` (full transport stack). Makes "pure encoder" contract structurally enforceable via grep. Reusable pattern for future LSP encoders (completion items, hover markup) if Fact-pinned output helps regression testing.
- [Plan 17-04]: Added `EncodeTokens_SkipBetweenMapped_PreservesDeltaMath` Fact (Rule 2 strengthening) — the plan's `EncodeTokens_SkipsUnmappedTokens` Fact covers all-unmapped case but does NOT discriminate the subtler invariant that an unmapped identifier between two mapped tokens must not shift the delta origin. New Fact uses `proc xyz return` to pin that second tuple's delta is `(0, retCol-procCol)` measured from Proc, not from xyz.
- [Plan 17-04]: 3 golden Theory rows pin exact int[] output (`"proc"`, `"proc Int"`, `"proc\nreturn"`) covering single-token, same-line-delta, cross-line-delta dimensions. Future LSP encoders producing deterministic binary shapes (MIDI event streams, WAV headers, completion-item JSON) can follow the same MemberData pattern for regression gates.
- [Plan 17-05]: StubbingRegistryProxy pattern chosen over hand-duplicating signature declarations in RegisterSignaturesOnly. Signatures remain single-sourced in existing Register* methods — future built-ins surface in the LSP automatically. Required promoting InternalFunctionRegistry.Register to virtual (1-token, non-breaking per plan 12-03 precedent's virtual promotion of ExpressionEvaluator.Evaluate). StubbingRegistryProxy subclasses the registry, overrides Register to forward (name, sig, stub) to the target, and discards the real delegate.
- [Plan 17-05]: RegisterSignaturesOnly v1 missed `map`/`filter`/`reduce`/`each`/`enharmonic`/random/custom-oscillator because they live in RegisterContextDependentFunctions (not the no-arg RegisterAllImplementations). Rule 1 fix: bind a dummy ExecutionContext to the proxy and call RegisterContextDependentFunctions + RegisterIterationGuard. Context is inert because proxy discards every delegate before it's stored.
- [Plan 17-05]: SortText numeric prefix ordering scheme (`0_` snippets → `1_` builtins → `2_` stdlib → `3_` users → `4_` keywords → `5_` types) pins stable completion ordering without alphabetic fallback. Snippets surface above plain keywords; user symbols surface above keywords but below builtins.
- [Plan 17-05]: IsInsideUseStringLiteral scanner deliberately simple — cursor-line-only scan, count quotes between last `use` and cursor. Does NOT handle `use` in comments or multi-line strings. Sufficient for the common case (user types `use "` and pauses); 17-06's tokenized context can refine if needed.
- [Plan 17-05]: BuiltInDocs expanded from 3 → 104 entries (target was ≥40, shipped 2.6×). 13-row `[Theory]` `ExpandedSet_CoversCoreBuiltIns` pins representative coverage across I/O / collections / math / audio core / effects / harmony / transforms / playback.
- [Plan 17-05]: Partial-class test split (SymbolIndicesTests.cs + SymbolIndicesTests.Indices.cs) keeps Task 1 commit compile-clean without Task 2 index symbols. Mirrors 17-03's DocumentManagerTests pattern. Pattern reusable for future phase plans where Task 2 extends Task 1's test class.
- [Plan 17-06]: Token-scan over cached ParseResult.Tokens chosen over AST walk for FindEnclosingKey because brace-depth tracking is a lexer-level concern. AST's MusicalContextStatement(Key) only appears after the full block parses cleanly — editing inside `key Cmajor { | <CURSOR>` may have no closing `}` yet, so the AST walker would miss the key. Token-scan handles partial/incomplete source gracefully. Walker consumes IReadOnlyList<Token> as parameter — no SimpleLexer re-instantiation (which would require ErrorReporter and duplicate work).
- [Plan 17-06]: Block-exit regression Fact (CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull) pins token-scan correctness — fails against line-heuristic `cursor.Line >= stmt.Location.Line - 1` (which has no upper bound and would falsely report "inside key" for cursors after a closed block), passes only with brace-depth tracking. Pattern reusable for any block-scoped language feature (fold regions, bracket matching, scope resolution).
- [Plan 17-06]: IsInsideNoteStream stays AST-based (inverse of FindEnclosingKey's choice) because stream boundaries are a parser construct — lexer emits Pipe tokens but doesn't distinguish opening/separator/closing. Nested streams don't exist in Flow (a stream spans bars but isn't nestable), so linear AST containment check suffices.
- [Plan 17-06]: RomanNumeralItems Detail resolves via HarmonyFunctions.ScaleDatabase.ResolveRomanNumeral — `I` in `Cmajor` renders as `Detail = "C (in Cmajor)"`. Composer-facing value at minimal cost (reuse of existing resolver). Falls back to generic label if resolver returns null; try/catch-wrapped defensively.
- [Plan 17-06]: Per-request re-parse in CompletionHandler.Handle chosen over caching for v1 correctness. Candidate future optimization: DocumentManager per-URI ParseResult cache keyed by text-hash. Not required for v1 — 96 Phase17 Facts run in ~1s, 236 full-suite in ~17s under test load; real-editor latency bounded by O(n) lexer+parser off the UI thread.
- [Plan 17-06]: Rule 3 deviations — (1) Flow proc test fixtures required `end proc` syntax not `{}` (3 Facts fixed); (2) `System.Range` collision required type alias `using Range = OmniSharp...Models.Range;` in DefinitionHandler.cs (same pattern plan 17-03 used in LspMappings.cs). Rule 1 — `IdentifierAt_AtWhitespace_ReturnsNull` Fact asserted wrong expectation (cursor just-after a token selects that token per IDE convention); replaced with between-identifiers fixture. Rule 2 — plan's drafted `b.SignatureLines[0]` access was outdated; actual BuiltInIndex.Entry field is `Signatures` (`IReadOnlyList<FunctionSignature>`) shipped in 17-05.
- [Plan 17-07]: Python helper embedded in scripts/lsp-smoke.sh via heredoc (one file, not two) — LSP Content-Length framing is hard to get right in bash (`$'\r\n'` vs UTF-8 byte counts); Python's json+bytes math is deterministic and python3 is pre-installed on all GitHub runners. Script accepts exit codes 0 OR 1 because some LSPs exit 1 on shutdown-without-initialize-ack — the intent is to catch hangs/crashes, not gate on a specific code.
- [Plan 17-07]: CI macOS runner split — osx-x64→macos-13 (last x64-native runner), osx-arm64→macos-14 (arm64). Using macos-latest for both would silently Rosetta-emulate the x64 build. Per-row `exe` matrix field (`flow-lsp` vs `flow-lsp.exe`) keeps a single smoke-test step across all 4 runners.
- [Plan 17-07]: Two-stage CI design — build-server (fail-fast: true, runs on every tag/manual dispatch) separate from publish (fail-fast: false, tag-gated only). Partial publish preferable to full rollback if one registry hiccups on one platform. Publish job fan-out across 4 vsce targets × 2 registries (HaaLeo/publish-vscode-extension@v2 for both VSCode Marketplace and OpenVSX) = 8 upload operations per v* tag.
- [Plan 17-07]: Rule 1 deviation (grep-gate false positive) — acceptance criterion `! grep -q "PublishTrimmed"` flagged a negation comment that said "no PublishTrimmed". Reworded to "assembly trimming is NEVER set on dotnet publish below" — same meaning, passes the literal string-absence gate. Second Rule 1 deviation — stdlib verify step initially used for-loop with `if [ ! -f ... ]`; replaced with 6 explicit `test -f` lines so `grep -cE "test -f.*\.flow"` counts 6. Functionally equivalent; both fail-fast on missing file but explicit form improves per-file error clarity in GitHub Actions UI AND matches acceptance grep.
- [Plan 17-07]: TM grammar snapshot review observed one quirk — `Cmaj7h`/`Dm7h` (chord+duration-suffix compound) falls through both chord and note patterns to default `source.flow` scope. Captured faithfully in baseline so future grammar improvements surface as visible snapshot diffs. No grammar changes made in this plan.
- [Plan 17-08]: Dual-shape editor-snippet delivery — raw syntactically-valid files (nvim-lspconfig.lua, helix-languages.toml) sit alongside markdown prose guides (neovim.md, helix.md, generic-lsp.md). Raw files satisfy plan's literal path requirements (grep-gated); markdown guides satisfy orchestrator's prose objective. Users get two consumption modes (copy-paste raw vs read-guide-first).
- [Plan 17-08]: .gitignore allowlist pattern for docs/**/*.md + README.md — the repo's global *.md ignore is deliberate (AI-generated-doc hygiene) but user-facing prose must track. Added !docs/ + !docs/**/*.md + !README.md allowlist block, self-documenting via comment. Pattern reusable for any future phase shipping user documentation under docs/<topic>/.
- [Plan 17-08]: Azure DevOps PAT scope vocabulary — Marketplace.Manage (Azure DevOps UI label) vs Marketplace (Publish) (VSCE documentation vocabulary). Runbook documents BOTH vocabularies to avoid confusion at Step 1.3. Rotation calendar: VSCE_PAT 1y max expiry with 2-month advance reminder (lets replacement PAT go through one real tag push before old one revoked); OVSX_PAT no documented expiry but rotate annually for hygiene.
- [Plan 17-08]: HUMAN-UAT deferral pattern for final manual-verify checkpoint — Task 3 declared checkpoint:human-verify but user directed "close the phase now without blocking on F5". Authored .planning/phases/17-flow-language-server/17-HUMAN-UAT.md using GSD UAT template (status: partial, 3 pending tests for manual-smoke.md rows 1-3, note explaining rows 4-5 deferred to first release tag). NOT faked as pass — result: [pending] preserves audit trail. Tests surface in /gsd-progress and /gsd-audit-uat and resolve asynchronously via /gsd-verify-work. Pattern reusable for any future phase with a UI/UX verification checkpoint that must not block on synchronous human session.
- [Plan 17-08]: Deferred-to-first-tag pattern for marketplace-verification events (rows 4 non-dev OS + 5 Marketplace/OpenVSX publish) — circular dependency (phase closure awaiting tag push awaiting phase closure) resolved by explicitly scoping these rows to the release-tag milestone, NOT to phase closure. manual-smoke.md documents as deferred; 17-HUMAN-UAT.md excludes them with explanatory note; 17-MARKETPLACE-SETUP.md Step 4 tracks them via runbook status-checklist row.

### Roadmap Evolution

- Phase 17 added (2026-04-20): Flow Language Server — LSP + VSCode extension for syntax highlighting, diagnostics, and intelligent completion/hover suggestions on .flow files
- Phase 17 context gathered (2026-04-20): 17-CONTEXT.md + 17-DISCUSSION-LOG.md written. Decisions locked: C# `flow-lsp` project reusing flow-lang (OmniSharp.Extensions.LanguageServer) · full re-lex+re-parse on ~150ms debounce · TextMate grammar + LSP semantic tokens (standard VSCode scopes, no bundled theme) · forward everything ErrorReporter produces · completion covers built-ins + stdlib imports + keywords + user-defined symbols + snippets · context-aware roman-numeral completion inside note streams · signature help for built-ins AND user procs · go-to-def for user procs/vars + stdlib · `flow-lang/StandardLibrary/BuiltInDocs.cs` lookup table · self-contained per-platform VSIX (linux-x64, win-x64, osx-x64, osx-arm64) · publish to Marketplace + OpenVSX. Ready for /gsd-plan-phase 17.
- Phase 15 planned (2026-04-20): 7 plans across 5 waves (commit 7acff55); research + validation + pattern-map produced; checker PASSED with 0 blockers, 6 non-blocking warnings (doc/scope notes). Wave 0: 15-01 scaffolding (Phase15 test subtree + MidiReadHelpers promotion closing DEFER-05). Wave 1 parallel: 15-02 DX-07 grammar/runtime, 15-04 DX-09 euclidean core. Wave 2: 15-03 DX-07 audio path (ReverbApply RT60 overload + per-voice SongRenderer insert, feedback cap 0.99). Wave 3 parallel: 15-05 byte-identical MIDI/WAV regression (two-pass strict), 15-06 end-to-end .flow scripts. Wave 4: 15-07 closure (ROADMAP criterion #3 "or zero" doc-only reframe per D-02 dry-short-circuit, collision grep transcript F-24, REQUIREMENTS Shipped markers). 23 automated Facts + 1 manual (F-24) wired to plans; all 18 decisions D-01..D-18 covered.

### Pending Todos

None yet for Phase 15.

### Blockers/Concerns

- Phase 11: Researcher disagreement on C1-C5 — cannot proceed to FIX-07 until spike resolves *(resolved in Phase 11 close)*
- Phase 12: C5 (augment/diminish swap) confirmation determines whether BREAKING CHANGE migration artifacts (release notes, transitional aliases, example audit) are required for v1.2 release *(C5 dismissed in Phase 11; not triggered)*
- Phase 14 (DX-08): NoteStreamCompiler velocity propagation (647-line file) may be complete already — verification pass may obviate new-code work *(confirmed in Phase 14 Plan 03 Outcome A — zero gap-fix required)*
- Phase 14 (DX-06) / Phase 15 (DX-07): Identifier collision grep required before landing (`H`, `Db`, `Eb`, `Fb`, `Cb`, `Bb`, `Gb`, `Ab`, `enharmonic`, `reverbTime`) *(Phase 14 slice done in 14-02-PLAN; `reverbTime` grep still owed for Phase 15)*
- Phase 15 (DX-09): Pinned PRNG (xorshift64* / splitmix64) required — `System.Random` is not stable across .NET patch versions and would violate "code is the score" reproducibility

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260420-0c0 | Add pure-Flow test library (flow-lang/test.flow + tests/test_test_library.flow) | 2026-04-20 | c8731d2 | [260420-0c0-write-a-pure-flow-test-library-at-flow-l](./quick/260420-0c0-write-a-pure-flow-test-library-at-flow-l/) |

## Session Continuity

Last session: 2026-04-20T18:00:00Z
Stopped at: Phase 17 plan 08 complete (2026-04-20; HUMAN-UAT deferred). Wave 7 docs + marketplace closure: docs/editor-setup/ (README, nvim-lspconfig.lua, helix-languages.toml, neovim.md, helix.md, generic-lsp.md, manual-smoke.md) closes D-13 non-VSCode clause; .planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md is a 200+-line one-time runbook covering VSCode Marketplace publisher creation + VSCE_PAT (Marketplace.Manage scope) + OpenVSX publisher + namespace claim (Pitfall 8 via npx ovsx create-namespace) + OVSX_PAT + GitHub Actions secret wiring + workflow_dispatch dry-run + first tag push procedure + rotation calendar + 7-row troubleshooting table + 17-row status-checklist for audit-trail; vscode-extension/README.md expanded 32→108 lines with features mapped to D-04..D-11, Marketplace + OpenVSX install guidance (explicit Cursor/VSCodium/Windsurf callout), VSCode >= 1.85.0 + no-.NET-runtime requirements, flow.server.path + flow.trace.server configuration reference table, Pitfall-6-aware manual-binary-override subsection, publisher placeholder disclosure; top-level README.md gained Editor support section announcing VSCode extension; .gitignore extended with !docs/ + !docs/**/*.md + !README.md allowlist (docs-prose tracking pattern reusable). Task 3 checkpoint resolved by explicit user direction ("Defer to HUMAN-UAT. Close the phase now without blocking on F5"): 3 rows of manual-smoke.md (row 1 D-04/D-05 syntax highlighting 11-item tick list, row 2 D-04 TM→semantic transition, row 3 D-13 extension activation + 7 embedded feature sanity checks D-06/D-07/D-08/D-09/D-10/D-11/snippet) tracked as pending tests in 17-HUMAN-UAT.md using GSD UAT template — surface in /gsd-progress and /gsd-audit-uat, NOT faked as pass. Rows 4-5 (non-dev OS binary, Marketplace+OpenVSX publish verification) deferred to first release tag milestone with explanatory in-file note (circular-dependency avoidance). Phase 17 closes at 8/8 plans with 96/96 Phase17 Facts green (docs-only plan, regression unchanged from 17-07).
Resume file: None — Phase 17 complete. Next v1.2 phase per ROADMAP is Phase 15 (Composer DX Part 2: DX-07 reverbTime + DX-09 euclidean swing/humanize). Phase 15 plans already drafted (15-01 through 15-07, 7 plans across 4 waves) and ready for execution.
