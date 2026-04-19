# Phase 13: Nyquist Validation Backfill — Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Retroactively author a `VALIDATION.md` for each v1.1 production phase (6–10), closing the documentation-lag tech debt carried out of v1.1 close. No production code under `flow-lang/` is modified; this is a pure docs + test-backfill phase. Every VALIDATION.md's sign-off checklist must be satisfiable against the current codebase, with at least one test per phase pinning a specific observable value (error text or numeric duration), and Phase 10's existing draft promoted to `nyquist_compliant: true` by automating what is feasible while preserving a manual-verification subsection for irreducibly-subjective items.

**In scope:**
- Write `.planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` (requirements QOL-01, FIX-01, FIX-02, FIX-03)
- Write `.planning/phases/07-developer-experience/07-VALIDATION.md` (requirements DX-01, DX-02, DX-03, DX-04)
- Write `.planning/phases/08-audio-production/08-VALIDATION.md` (requirements AUDIO-05, AUDIO-06, AUDIO-07)
- Write `.planning/phases/09-advanced-features/09-VALIDATION.md` (requirements AUDIO-08, QOL-02)
- Promote `.planning/phases/10-vocalization/10-VALIDATION.md` from `nyquist_compliant: false` → `nyquist_compliant: true` by automating feasible checks; keep manual-only subsection for perceptual/external-command items
- Any NEW xUnit Facts required to fill coverage gaps land in `flow-lang.Tests/Unit/` or `flow-lang.Tests/Integration/`
- REQUIREMENTS.md traceability row for TEST-04 marked Complete at end of phase (no separate rollup plan — 13-05 is the last plan and closes it)

**Out of scope:**
- Modifying production source under `flow-lang/` (ROADMAP Phase 11 D-04 convention: validation phases are pure investigation/test authoring)
- Adding new NuGet packages (minimal-dependencies philosophy from CLAUDE.md / Phase 12 stack research — no FFT library, no assertion framework beyond xUnit)
- Buffer byte-hash pinning for audio phases — brittle across any DSP refactor; use numeric durations and error strings instead
- Cross-phase AUDIT.md rollup in v1.1-MILESTONE-AUDIT.md style — `.planning/milestones/v1.1-MILESTONE-AUDIT.md` already exists and is sufficient
- Re-litigating INVALID FIX-04 (already reclassified in v1.1 audit; no validation test authored for it)
- Fixing the v1.1-audit-flagged §integration gap (`section { gain N { | notes | } }`) — already resolved per REQUIREMENTS.md §FIX-02 commit 2156690; Phase 6 VALIDATION.md pins it via test, not fixes it
- /gsd-validate-phase tooling run itself — Phase 13 HAND-AUTHORS the VALIDATION.md files rather than invoking gsd-nyquist-auditor (the auditor was designed to fill gaps in an existing VALIDATION.md, not author one from scratch)

</domain>

<decisions>
## Implementation Decisions

### Plan structure (5 plans, atomic commits per phase)

- **D-01:** **13-01** — Phase 6 VALIDATION.md (QOL-01 `--verbose`, FIX-01 Sequence overload, FIX-02 bare-expression capture incl. gain-nested, FIX-03 fatal/non-fatal errors) + any new xUnit Facts to fill gaps
- **D-02:** **13-02** — Phase 7 VALIDATION.md (DX-01 `//` line comments, DX-02 math stdlib, DX-03 writeWav/exportWav alias, DX-04 REPL auto-imports) + new Facts as needed
- **D-03:** **13-03** — Phase 8 VALIDATION.md (AUDIO-05 `mix`, AUDIO-06 per-section gain, AUDIO-07 strings/organ/bell presets) + new Facts as needed
- **D-04:** **13-04** — Phase 9 VALIDATION.md (AUDIO-08 tempoRamp, QOL-02 interactive tutorial) + new Facts as needed
- **D-05:** **13-05** — Phase 10 VALIDATION.md promotion (VOC-01 sing formants, VOC-02 tts external) by automating feasible checks + REQUIREMENTS.md TEST-04 row marked Complete + STATE/ROADMAP phase closure
- **D-06:** All five plans land in **Wave 1 parallel** — each plan touches a distinct phase directory plus additive test files in `flow-lang.Tests/`, zero file overlap. Phase 12 Wave 2 independence pattern reused.
- **D-07:** No separate rollup plan. 13-05 is the closing plan and updates REQUIREMENTS.md + STATE.md + ROADMAP.md as its last commit. Matches Phase 11's 11-06 pattern but without spinning a dedicated phase-exit plan.
- **D-08:** Each plan produces **one VALIDATION.md creation commit** (or promotion commit for 13-05) + optional additional commits if new xUnit Facts are required. Commit per plan enumerated in `<commits>` field at plan time. Bisectable per-phase.

### Test strategy

- **D-09:** New validation tests land as **native xUnit Facts** in `flow-lang.Tests/Unit/` (pure C# API assertions) or `flow-lang.Tests/Integration/` (invoke FlowEngine, capture stdout/stderr, assert on substrings or exit codes). Directory convention follows the one Phase 12 Plan 12-02 created (`Unit/CollectionsTests.cs`, `Unit/ThunkTests.cs`, `Unit/InterpreterTests.cs`).
- **D-10:** Existing `.flow` scripts **already wrapped as Theory rows by Plan 12-01** count as coverage wherever they target a v1.1 requirement. VALIDATION.md tables cite those existing rows by file path + Theory key. No new `.flow` scripts created if existing coverage suffices.
- **D-11:** **Observable-value pin per phase** is either (a) error message text (rarely changes, robust) or (b) numeric durations/sample counts (deterministic, survives refactors). **Buffer byte hashes are forbidden** — they break on any DSP change and generate false-positive failures. Examples: "Cannot get init of empty array" for Collections; `sing("ah", C4, 2.0)` returns 88200 samples at 44.1kHz; `tempoRamp(...)` buffer length = `durationAtStartBPM + durationAtEndBPM` ± 1 sample.
- **D-12:** **No new NuGet packages.** All DSP/audio assertions use hand-rolled zero-crossing counts or peak detection in the test file. No FFT library. Minimal-dependencies philosophy stands (CLAUDE.md + Phase 12 stack research).

### Requirements-first authorship (ROADMAP criterion 1)

- **D-13:** **Two-pass strict** authorship per phase:
  - **Pass 1 (planner):** author the VALIDATION.md `## Per-Task Verification Map` and `## Observable Invariants` sections, plus any new-Fact test SKELETONS (assertion text + expected values), reading ONLY `.planning/milestones/v1.1-REQUIREMENTS.md` and the phase goal from ROADMAP.md. **Do NOT read SUMMARY.md, source code, or existing test files during this pass.**
  - **Pass 2 (executor):** implement the skeletons against real code; adjust assertion text only if Pass 1 drafted something non-testable as stated (e.g., wrong method name, non-existent error string format).
- **D-14:** **Pass 2 adjustments are logged in `## Divergences` section of VALIDATION.md** — any requirement that was not literally testable as REQUIREMENTS.md wrote it becomes documented drift. Mirrors Phase 12's `## Empirical Overrides` in `12-VERIFICATION.md`. Honest documentation of requirement-vs-reality mismatch is the single most valuable output of this phase (Phase 11/12 caught audit false-positives precisely this way).
- **D-15:** When Pass 1 and Pass 2 disagree meaningfully (e.g., requirement says "errors mask function-not-found as success" but shipped code correctly distinguishes), the Divergence entry describes what REQUIREMENTS.md wrote, what the code does, and which is correct. Do NOT edit REQUIREMENTS.md — keep that file as the historical record. Future phases can reconcile if needed.

### Phase 10 — promote, don't waive

- **D-16:** **Phase 10's existing draft `10-VALIDATION.md` is promoted to `nyquist_compliant: true`.** The two manual-only items in the current draft (formant quality, espeak-ng external command) are partly automatable:
  - **Automatable:** `sing("ah", C4, 2.0)` returns a buffer of exactly 88200 samples (2.0s × 44100Hz), non-empty, differs from `silence(2.0)` by an RMS threshold, has a fundamental period matching C4 (~168 samples at 44.1kHz) measurable via zero-crossing count over the middle 50% of the buffer
  - **Still manual (listed in `## Manual-Only Verifications` subsection):** perceptual vowel recognizability ("does 'ah' sound like 'ah' to a human?"), `tts()` actually invokes the configured external command (requires espeak-ng installed)
- **D-17:** `nyquist_compliant: true` is earned because every REQUIREMENT has at least one automated test; truly-subjective verifications are documented as manual-only rather than treated as coverage gaps. ROADMAP criterion 2 authorizes this path explicitly.
- **D-18:** Phase 10 VALIDATION.md pin: `sing("ah", C4, 2.0)` buffer length equals exactly 88200 samples at the project's standard 44.1kHz sample rate. Numeric, deterministic, survives any formant-algorithm refactor that preserves sample-rate and duration semantics.

### Scope of new tests vs reuse

- **D-19:** **Existing coverage wins.** For each requirement, the Pass 1 author first checks Plan 12-01's Theory-row catalog (all 55 rows registered via FlowScriptData) to see if the behavior is already pinned. If yes: VALIDATION.md cites the Theory row path + required sentinel. If no: a new xUnit Fact is authored.
- **D-20:** **Integration tests over unit tests** where the requirement describes user-visible behavior (e.g., `//` comments skip during tokenization, `writeWav` creates a file). Unit tests where the requirement describes an internal contract (e.g., Sequence overload matching, error-reporter severity).
- **D-21:** **The 13-* plans MAY add tests but MAY NOT modify existing tests** except Phase 10's VALIDATION.md frontmatter. If a Phase 12-era test is discovered to be flaky or wrong, it's recorded as a Divergence and deferred — not silently fixed here.

### Phase-completion bookkeeping

- **D-22:** 13-05 updates `.planning/REQUIREMENTS.md` TEST-04 row to `[x]` + traceability table to `Complete` at phase close. Matches Phase 12's 12-06 closure pattern.
- **D-23:** Each phase's `*-VERIFICATION.md` (existing or newly-created) is NOT touched by Phase 13. The v1.1 audit already produced `.planning/milestones/v1.1-MILESTONE-AUDIT.md` as the aggregate verdict; Phase 13 adds the missing VALIDATION.md layer without disturbing the verification layer above it.
- **D-24:** Phase 13's own `13-VALIDATION.md` (Nyquist strategy for Phase 13 itself) is created by the plan-phase workflow per Phase 12 precedent, but its content is minimal because this phase is pure docs authoring — the `dotnet test` green gate + presence-check of each created VALIDATION.md is sufficient for 13's own Nyquist compliance.

### Claude's Discretion

- Exact xUnit Fact naming for new validation tests (e.g., `FlowLang.Tests.Unit.VerboseFlagTests` vs `FlowLang.Tests.Validation.Phase06.VerboseFlag`)
- Wording of each VALIDATION.md `## Observable Invariants` subsection entry
- How many hand-rolled zero-crossing helpers to share vs inline (a single `AudioTestHelpers.CountZeroCrossings(buffer, start, length)` feels right but not mandated)
- Whether Phase 9's QOL-02 interactive tutorial validation is a Theory row asserting the tutorial exits 0, or a smoke Fact asserting a key line of tutorial stdout — either satisfies the pin requirement
- Whether to add `nyquist_compliant: true` to Phase 12's existing VALIDATION.md as part of this phase or leave it for a separate pass — 12-VALIDATION.md is out of Phase 13 scope per ROADMAP

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone planning
- `.planning/ROADMAP.md` "Phase 13: Nyquist Validation Backfill" — 3 success criteria (esp. #1 requirements-first, #2 phase-10 promote-or-waive, #3 per-phase observable pin)
- `.planning/REQUIREMENTS.md` §"Testing Infrastructure" — TEST-04 entry being closed by plan 13-05

### Source of truth — requirements being validated
- `.planning/milestones/v1.1-REQUIREMENTS.md` — archival copy of all 16 v1.1 requirements; **this is the only file Pass 1 authors may read** for tests-from-requirements
- `.planning/milestones/v1.1-MILESTONE-AUDIT.md` — aggregate audit verdict (gaps, tech_debt, §integration gap). Useful context; NOT input to Pass 1 per D-13

### Phase directories being validated (Pass 2 inputs)
- `.planning/phases/06-diagnostics-bug-fixes/06-01-PLAN.md`, `06-01-SUMMARY.md`, `06-02-PLAN.md`, `06-02-SUMMARY.md`, `06-RESEARCH.md`
- `.planning/phases/07-developer-experience/07-01-PLAN.md`, `07-01-SUMMARY.md`, `07-02-PLAN.md`, `07-02-SUMMARY.md`, `07-CONTEXT.md`
- `.planning/phases/08-audio-production/08-01-PLAN.md`, `08-01-SUMMARY.md`, `08-02-PLAN.md`, `08-02-SUMMARY.md`, `08-CONTEXT.md`
- `.planning/phases/09-advanced-features/09-01-PLAN.md`, `09-01-SUMMARY.md`, `09-02-PLAN.md`, `09-02-SUMMARY.md`, `09-CONTEXT.md`
- `.planning/phases/10-vocalization/10-01-PLAN.md`, `10-01-SUMMARY.md`, `10-02-PLAN.md`, `10-02-SUMMARY.md`, `10-VALIDATION.md` (existing draft — target of 13-05 promotion), `10-VERIFICATION.md`

### Template and examples
- `~/.claude/get-shit-done/templates/VALIDATION.md` — canonical schema + sign-off checklist
- `.planning/phases/12-stability/12-VALIDATION.md` — working example with all sections filled in correctly
- `.planning/phases/12-stability/12-VERIFICATION.md` — format reference for `## Divergences` / `## Empirical Overrides` pattern that Phase 13 mirrors

### Test infrastructure (established by Phase 12)
- `flow-lang.Tests/flow-lang.Tests.csproj` — target project for new xUnit Facts (net10.0, xunit 2.9.3)
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — in-process FlowEngine driver with stdout/stderr capture; reuse for integration tests
- `flow-lang.Tests/FlowScriptTests.cs` + `flow-lang.Tests/FlowScriptData.cs` — wrap-as-Theory harness; Pass 1 author checks this for existing coverage before authoring new Facts
- `flow-lang.Tests/Unit/CollectionsTests.cs`, `Unit/ThunkTests.cs`, `Unit/InterpreterTests.cs` — example Fact patterns from Phase 12

### Agent contract (reference only — not used directly)
- `~/.claude/agents/gsd-nyquist-auditor.md` — describes the gap-filling auditor that `/gsd-validate-phase` would invoke. Phase 13 authors the VALIDATION.md by hand rather than running the auditor, because the auditor targets existing-VALIDATION gap filling, not greenfield authoring.
- `~/.claude/get-shit-done/workflows/validate-phase.md` — reference for what a Nyquist audit output looks like

### Code under validation (READ-ONLY, Pass 2 only)
- `flow-lang/Diagnostics/` — QOL-01 `--verbose` flag implementation
- `flow-lang/TypeSystem/OverloadResolver.cs` + `flow-lang/Interpreter/Interpreter.cs:347-406` — FIX-01 / FIX-02 targets
- `flow-lang/Runtime/ErrorReporter.cs` — FIX-03
- `flow-lang/Lexing/SimpleLexer.cs` — DX-01 `//` comments
- `flow-lang/std.flow` + `flow-lang/StandardLibrary/` — DX-02 math, DX-03 writeWav/exportWav alias, DX-04 REPL auto-imports
- `flow-lang/StandardLibrary/Audio/*.cs` — AUDIO-05 mix, AUDIO-06 section gain, AUDIO-07 synth presets, AUDIO-08 tempoRamp
- `flow-lang/StandardLibrary/Vocalization/` (or equivalent) — VOC-01 sing formants, VOC-02 tts external
- `examples/tutorial.flow` — QOL-02 interactive tutorial target

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`FlowEngineRunner` fixture** (`flow-lang.Tests/Fixtures/FlowEngineRunner.cs`) — in-process FlowEngine with stdout/stderr capture; every new Integration Fact for Phase 13 uses this
- **`FlowScriptData` glob + RequiredSentinels dictionary** (`flow-lang.Tests/FlowScriptData.cs`) — existing coverage catalog; Pass 1 consults this before authoring new tests
- **`Environment.CurrentDirectory` pivot** (Phase 12 deviation 4) — when Integration Facts need repo-relative paths to read example .flow files, mirror the pivot pattern FlowScriptTests uses
- **Existing error-message strings** in `Collections.cs`, `Interpreter.cs`, `FlowEngine.cs` — direct input to error-text pins per D-11
- **Existing `// AUDIT-VERIFIED YYYY-MM-DD: ...` markers** (Phase 11/12 convention) — Phase 13 does NOT add markers (no code changes); it only reads them for context

### Established Patterns
- **xUnit Fact file layout** (`Unit/` for pure C# API, `Integration/` folder to be created) — follow Phase 12 Unit/ pattern
- **Atomic commits per VALIDATION.md** (ROADMAP/Phase 11 D-08) — one commit per phase's validation file, plus additional commits only if new Fact files are added
- **VALIDATION.md frontmatter schema** (`phase`, `slug`, `status`, `nyquist_compliant`, `wave_0_complete`, `created`) — matches `~/.claude/get-shit-done/templates/VALIDATION.md` verbatim
- **Two-pass authorship record** — Pass 1 author writes `## Draft from REQUIREMENTS.md` section, Pass 2 author fills `## Implementation Map` + `## Divergences` sections; final file keeps both for audit trail

### Integration Points
- **`.planning/REQUIREMENTS.md` TEST-04 row** — updated once at phase close by 13-05
- **`.planning/ROADMAP.md` Phase 13 Progress table** — updated by phase-completion workflow, not inside any plan
- **`flow-lang.Tests/flow-lang.Tests.csproj`** — no csproj changes expected (existing packages cover xUnit + FluentAssertions if we use them); new Fact files auto-included via net10.0 implicit compile glob
- **No `flow-sharp.sln` changes** — no new projects
- **`dotnet test flow-sharp.sln`** must remain 100% green after each plan; any new Fact that fails is a plan bug, not acceptable output

</code_context>

<specifics>
## Specific Ideas

- **Error-string pinning beats buffer hashing.** A test that asserts `stderr contains "Cannot find function 'renderBars'"` will survive every audio refactor for the next 10 years. A test that asserts `sha256(wavBytes) == "abc..."` breaks the first time a DSP coefficient changes by one ULP. Phase 13 picks the durable axis.
- **Zero-crossing fundamental detection** is legit DSP. Count sign flips per millisecond in the middle 50% of a buffer, divide by 2, and you have fundamental frequency accurate to ~1Hz at 44.1kHz. No library needed.
- **Two-pass strict authorship catches the interesting bugs.** Phase 11 found C5 was a false alarm and FIX-07 was real; Phase 12 found TEST-01/02 were audit false positives. The pattern is: when you write tests FROM the requirements without peeking at code, you notice when requirements drift. Phase 13 bakes this into the workflow.
- **Phase 10's "manual verification preserved" is the right frame.** Some behaviors are genuinely subjective (does this vowel sound like "ah"?) and no amount of automation substitutes for a human listening. Keeping those as `## Manual-Only Verifications` while automating everything else is mature engineering, not a cop-out.
- **The v1.1 audit already identified the integration gap** (`section { gain N { | notes | } }` → 0 frames) and it's already fixed (commit 2156690 per REQUIREMENTS.md FIX-02). Phase 6's VALIDATION.md pins it with a test that would re-break if someone re-introduced the bug. This is what "retroactive validation" looks like in practice.
- **No rollup plan is needed** because TEST-04's closure is a 1-line edit to REQUIREMENTS.md plus STATE/ROADMAP entries — the kind of thing Phase 11 rolled into 11-06 and Phase 12 rolled into 12-06, but those had more content to carry. Phase 13's 13-05 carries both Phase 10 promotion and the traceability close.

</specifics>

<deferred>
## Deferred Ideas

- **v1.2 phase VALIDATION.md enrichment** (Phase 11, 12 both have VALIDATION.md at `nyquist_compliant: false`). Consider a Phase 13.1 or separate pass to promote those. Out of Phase 13 scope per ROADMAP wording "v1.1 phases 6–9" — v1.2 validation is a separate tech-debt bucket.
- **FIX-04 INVALID retroactive acknowledgment** — v1.1 audit already captured this in `v1.1-MILESTONE-AUDIT.md § gaps`. No validation test is authored for an INVALID requirement. If the requirement needs to be closed in `v1.1-REQUIREMENTS.md` traceability beyond its current `~` mark, that's a separate paper-trail cleanup pass.
- **Cross-phase Nyquist rollup doc** (e.g., `.planning/milestones/v1.1-NYQUIST-AUDIT.md`). Out of scope — `v1.1-MILESTONE-AUDIT.md` already serves this role aggregately, and the per-phase VALIDATION.md files are where detail lives.
- **Refactoring existing tests** discovered as flaky or wrong during Pass 2. Out of Phase 13 scope per D-21 — recorded as a Divergence and deferred to a dedicated test-hygiene pass.
- **Migrating the v1.1 test runner** (`for test in tests/test_*.flow; do ...; done`) docs out of CLAUDE.md now that xUnit exists — this was flagged in Phase 12 D-17 as "may update" and was NOT updated. Keep for a later DX pass.
- **FFT-based harmonic analysis** for deeper Phase 10 (VOC-01) validation. Rejected here (no new deps). If a v1.3 phase ever needs this, consider vendoring a small FFT rather than adding MathNet.Numerics.

</deferred>

---

*Phase: 13-nyquist-validation-backfill*
*Context gathered: 2026-04-19*
