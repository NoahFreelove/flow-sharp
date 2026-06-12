# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.2 — Stability & Composer DX

**Shipped:** 2026-04-26
**Phases:** 7 | **Plans:** 41

### What Was Built
- Audit Spike (Phase 11) — pure investigation produced 1 Confirmed (C1 → FIX-07a) + 4 Dismissed (C2/C3/C4/C5) before any production code change
- Stability fixes — `init([])` raises matching head/last semantics (FIX-05), `Thunk` refactored to `Lazy<Value>` for failure caching (FIX-06), `ExecuteMusicalContext` `return;`→`break;` so partial-context body still runs (FIX-07a), `if(Bool, Void, Void)` wildcard overload + auto-mkdir for writeWav (TEST-03)
- Retroactive Nyquist Validation — Phases 6–10 each gained or promoted `VALIDATION.md` to `nyquist_compliant: true` with observable-value pins (TEST-04)
- Composer DX Tier A — `slice` (Sequence + Array[T]) atomic with silent two-sided clamp, extended flat-literal Parse + `enharmonic()` + lexer dispatch reorder, MIDI velocity byte-array regression via DryWetMidi (DX-05/06/08)
- Composer DX Tier B — `reverbTime <s> { ... }` musical-context block with per-voice RT60 (Schroeder closed-form mapping, dry-render sentinel at 0s), `euclidean(hits, steps, note, swing)` + 6-arg humanize/seed overload with byte-identical output across runs (DX-07/09)
- Tutorial + showcase refresh — `examples/tutorial.flow` and `examples/showcase.flow` exercise every v1.1 + v1.2 feature with byte-identical determinism contract holding across two consecutive runs (QOL-03)
- Flow Language Server + VSCode extension — OmniSharp.Extensions.LanguageServer over stdio, references flow-lang only, per-platform self-contained VSIX (linux-x64, win-x64, osx-x64, osx-arm64) with bundled stdlib `.flow` files; semantic tokens + diagnostics + completion + hover + signature help + go-to-definition + 5 snippet templates + roman-numeral context inside note streams + `BuiltInDocs` populated to 104 entries

### What Worked
- **Audit-spike-as-its-own-phase pattern**: Pure investigation Phase 11 with no production source modified produced binding evidence for 5 audit claims. Researcher disagreement was resolved by failing tests and AUDIT-VERIFIED markers, not opinion. 4 dismissals would have wasted weeks of fixing-without-reproducing work.
- **Two-pass strict authorship**: Pass 1 from REQUIREMENTS alone, Pass 2 reality-check with empirical assertions. Surfaced format drift (DX-02 Double precision: `str pi` → "3.141592654" not full precision; `Math.PI.ToString()` mismatch), signature corrections (`AudioCore.Mix` is `IReadOnlyList<Value>`, not `(AudioBuffer, AudioBuffer)`), and namespace-layer discrepancies (`SynthesizerFactory` outer namespace vs synth classes in inner namespace) before any commit. Three plans went zero-divergence (13-01, 13-04, 14-03 Outcome A) once requirements were reconciled with shipped behavior.
- **Charitable interpretation as load-bearing**: 4 criterion-moot/reframe events (TEST-03 P12, DX-06 P14, criterion #3 P15, criterion #4 P16) where original wording contradicted shipped reality. Audit-trail preservation via `*Original audit-trail:*` preamble kept history visible without forcing wrong fixes.
- **Determinism contract end-to-end**: Synth white-noise RNG (`SynthUtils.Rng`) + TPDF dither RNG (`FileIO.Random`) reseeded at renderSong/writeWav boundaries. Two consecutive runs cmp-clean for both `tutorial.flow` and `showcase.flow` WAV+MIDI. "Code is the score" contract genuinely holds across .NET patch versions.
- **HUMAN-UAT for non-blocking checkpoints**: Phase 17 `17-HUMAN-UAT.md` (status: partial) instead of fake-passing manual verification. Rows 1-3 surface in `/gsd-progress` and `/gsd-audit-uat`; rows 4-5 explicitly deferred to first release tag (circular dep: marketplace verification needs a tag). Pattern reusable.
- **`Closed (audit false positive)` first-class marker**: TEST-01 / TEST-02 retained as audit-trail entries documenting the audit conflated two distinct problems (range-missing for Test 4 vs if-overload for Tests 1-3) and mis-read existing implementation (`break`/`continue` already at `Interpreter.cs:120-124`). Distinct from Pending or Shipped.

### What Was Inefficient
- **CONTEXT D-XX divergences surfaced late** — multiple plans hit Rule 1/2/3 deviations because CONTEXT-locked decisions contradicted shipped reality. Better calibration of which decisions are guesses vs facts would shorten the discuss-phase cycle.
- **Plan 17-02 carried a checkbox-but-shipped status** — VSCode extension scaffold rolled into 17-01 scope but the ROADMAP plan-row stayed `[ ]`; should have been struck through with a "shipped under 17-01" annotation.
- **Reserved-keyword collisions caught at fact-authoring time** — `end`, `_`, `buf`, `H` all collided with reserved tokens during plan execution (renamed `s`/`e`, `xyz`, `rendered1`/`rendered2` mid-plan). A reserved-token grep gate at plan-time would have prevented mid-execution Rule 3 deviations.
- **Test-count drift between worktree base and mainline** — Phase 16 closure noted 284/284 (worktree-base) vs 287/287 (mainline) requiring documentation. Worktree base/mainline drift after consecutive worktree closures should be normalized before merging.

### Patterns Established
- **Phase-scoped Integration test directories** — `flow-lang.Tests/Integration/Phase{NN}/` and `flow-lang.Tests/Unit/Phase{NN}/` keep Facts traceable to phases (introduced in 13-01, replicated 13-02..15-07).
- **Partial-class test split** — Task 1 commit + Task 2 partial-class extension keeps each commit compile-clean without forward-referencing symbols (17-03 DocumentManagerTests, 17-05 SymbolIndicesTests).
- **Audit-trail preservation via `*Original audit-trail:*` preamble** — when a requirement is reframed, original wording stays inside the entry as historical context (DX-06 reframe pattern).
- **Pre-landing collision grep transcript** — record empty-grep output in VERIFICATION.md as ROADMAP criterion (Phase 14 DX-06, Phase 15 F-24 reverbTime).
- **Closed (audit false positive)** as first-class traceability marker.
- **HUMAN-UAT deferral with status: partial** — captures real verification debt without faking pass.
- **Deferred-to-first-tag pattern** — circular dependency (phase closure ↔ tag push) resolved by scoping rows to the release-tag milestone, not phase closure (Phase 17 manual-smoke rows 4-5).

### Key Lessons
1. **Disputes between researchers warrant a spike phase, not a fix phase.** When two agents disagree on whether a bug exists, fixing without reproducing is more expensive than spiking — Phase 11 produced 4 dismissals that would have been weeks of churn.
2. **Format drift between assumptions and shipped reality is the dominant Pass-2 finding.** `str` precision, namespace layers, signature shapes — Pass-2 strict authorship surfaced these before they could become silent regressions. Two-pass strict should be standard for backfill phases.
3. **Determinism contracts only hold if every PRNG in the chain is seeded.** Phase 15 found a second unseeded RNG (synth white-noise) one phase after fixing the first (TPDF dither). Audit RNG usage explicitly when claiming "byte-identical" anywhere.
4. **Document-only closure plans need to be load-bearing** — closure plans (12-06, 13-05, 14-04, 15-07, 16-05) consistently surfaced reframe candidates that wouldn't have appeared during execution. Reserve doc-only plans as their own commit boundary.
5. **Reserved-token grep gates should be plan-time, not execution-time** — `end`, `_`, `H`, `buf` collisions all forced Rule-3 deviations mid-plan. Plan template should require a reserved-token sweep over new identifier names.
6. **HUMAN-UAT items don't have to block phase closure** — `status: partial` with explicit rows is honest, surfaces in `/gsd-audit-uat`, and lets non-blocking work proceed. Faking pass would have hidden 3 real verification gaps for Phase 17.

### Cost Observations
- Model mix: not tracked
- Sessions: multi-session over ~3 months (2026-01-24 → 2026-04-26)
- Notable: parallel waves used in Phases 15, 16, 17 (Wave 0/1/2/3) with up to 4 concurrent plans

---

## Milestone: v1.5 — Stage, Studio, Web

**Shipped:** 2026-06-12
**Phases:** 15 (35–49) | **Plans:** 103 | **Audit:** tech_debt (0 unsatisfied, integration CLEAN)

### What Was Built
- Language foundation (35): pattern matching + music-aware extractors, Rust-style diagnostics, pure-Flow `flow test` framework, `-> as name`.
- Generative + algebra (36): 13 Tidal combinators, Markov/L-system/cellular/chaos, parameterized sections, `jam` improv, `PrngRegistry` determinism foundation.
- Sound design (37): granular + hand-rolled vocoder/PSOLA stretch+pitch-shift, per-voice stereo pan, SFZ/sampler polish.
- Live + interop (38–40): `live { }` + watch + REPL polish + mic + OSC; MusicXML/LilyPond/ABC/MML; real-time MIDI + 24-PPQN clock + JACK.
- Consolidation (42–46): type/stdlib audit → strict mode + module names + Beat literals; bloat removal.
- Reach (41, 47–49): `flow doc`, 5-RID binaries, JetBrains plugin; `FlowTarget=Desktop|Web` → Mono-WASM runtime + real WebAudioBackend → flowlang.dev SvelteKit site. First audible .NET-in-WASM Flow.

### What Worked
- **The 47→48→49 compile-target chain held cleanly.** Conditioning the library first (47) de-risked WASM (48) and the site (49); the integration checker confirmed all 7 cross-phase seams wired with byte-identical E2E renders.
- **Determinism foundations paid forward.** `PrngRegistry` laid in 36 kept 37/38 two-run cmp-clean; the read-only `42-AUDIT.md` fed 43/44 as a concrete routing artifact.
- **Inline re-verification caught real gaps before close** — Phase 37 PIANO-01 cross-validation, Phase 38 `OscHandle` TypeParser — fixed and re-verified rather than shipped broken.
- **Honest human-gate discipline.** Phases 40/41/48/49 marked `human_needed` (hardware/deploy/marketplace/cross-browser) instead of faked passes; the milestone shipped on machine-verified evidence + tracked debt.

### What Was Inefficient
- **Bookkeeping lag.** 16 traceability checkboxes (Phase 35 + 39) stayed `Pending` for weeks despite passing verification; Phase 39 shipped with **0 SUMMARY.md**. Both needed a reconciliation pass at milestone-close audit.
- **Mid-milestone phase churn.** Phases 42–46 added 2026-05-24/25 and the WASM bullet was carved 41 → 47-49; STATE/ROADMAP summary rows drifted (Phase 39 "Not started", Phase 48 "5/7") and took several reconciliation passes.
- **In-process seams hid a native bug.** RtMidi.Core 1.0.53's ABI crash (`free(): invalid pointer` on `(midiPorts)`) only surfaced on real hardware — the `CaptureMidiBackend` seam masked it; cost a full Plan 40-04 rework to direct librtmidi P/Invoke.
- **One large fix run bypassed GSD** (2026-06-09, 56-agent audit + 3-wave fix, 60+ findings) — valuable but outside the planning trail.

### Patterns Established
- `FlowTarget=Desktop|Web` MSBuild conditioning + `#if !FLOW_WEB` at call sites + Mono.Cecil reflective gate = the cross-platform feature-strip pattern.
- Frozen-runtime contract: Phase 48 `flow-runtime.js` committed verbatim under `flow-site/static/wasm/` → CF build stays pure-Node, runtime never hand-edited.
- Read-only audit phase (42) emitting a routing deliverable consumed by downstream phases.
- Deferred-HUMAN-UAT: machine-verify what's verifiable, flag the rest, ship on evidence + STATE-tracked debt.

### Key Lessons
- **Flip traceability checkboxes at phase close, not milestone close** — stale `Pending` rows masquerade as coverage gaps.
- **Write a SUMMARY.md even for "obvious" phases** — VERIFICATION alone (Phase 39) leaves no per-plan artifact trail.
- **Pair in-process test seams with a real-target loopback test** (skip-when-absent) — seams hide ABI/native bugs.
- **Reconcile ROADMAP/STATE immediately on mid-milestone phase insertion** or the bookkeeping compounds.

### Cost Observations
- Model mix / session count not instrumented this milestone.
- Largest GSD milestone to date (15 phases); executed largely via autonomous chain with multiple re-verification loops. Notable: the reach track (47–49) carried most of the feasibility risk and absorbed the most rework.

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 | 5 | 12 | Initial MVP |
| v1.1 | 5 | 10 | First brownfield iteration; v1.1 audit surfaced bare-expression-in-section bug post-close |
| v1.2 | 7 | 41 | Audit spike pattern; two-pass strict authorship; HUMAN-UAT deferral; determinism-contract end-to-end |

### Cumulative Quality

| Milestone | Tests | Notable |
|-----------|-------|---------|
| v1.0 | 70+ .flow scripts (no harness) | No xUnit; tests verified by stdout |
| v1.1 | 70+ .flow scripts | Same; first VALIDATION.md draft (Phase 10) |
| v1.2 | 287/287 (xUnit Theory + Fact harness) | xUnit harness scaffolded in 12-01; all phases gained `VALIDATION.md` at `nyquist_compliant: true`; byte-identical determinism contract |

### Top Lessons (Verified Across Milestones)

1. **Brownfield audits surface real bugs that shipped milestones missed** — v1.1 audit found bare-expression-in-section composition bug post-close (commit 2156690); 2026-04-18 audit set up the entire v1.2 spike-then-fix cadence. Audit on milestone close (not just at planning time) is load-bearing.
2. **Soft-failure error model + accumulating ErrorReporter** continues to pay back — programs run past errors, multiple errors per pass surface naturally, REPL stays usable. Preserved through every milestone.
3. **Path-first arg conventions for file exports** (writeWav, writeMidi) read more naturally than buffer-first; established in v1.1, kept in v1.2.
