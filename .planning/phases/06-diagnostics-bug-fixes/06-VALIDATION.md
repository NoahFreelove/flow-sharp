---
phase: 6
slug: diagnostics-bug-fixes
status: passed
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-19
backfilled: true
---

# Phase 6 — Validation Strategy

> Retroactive VALIDATION.md authored under TEST-04 (Phase 13 Nyquist Validation Backfill). Phase 6 shipped without a VALIDATION.md; this file is authored two-pass strict (Pass 1 from v1.1-REQUIREMENTS.md alone; Pass 2 reconciles against the shipped codebase).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase06"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~20 seconds full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter` scoped to the just-authored Fact class (e.g. `FullyQualifiedName~VerboseFlagTests`)
- **After every plan wave:** Run `dotnet test flow-sharp.sln`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-backfill-01 | 13-01 | 1 | QOL-01 | — | `--verbose` flag emits `[verbose] Executing` prefix to stderr | integration | `dotnet test --filter "FullyQualifiedName~VerboseFlagTests"` | ✅ | ✅ green |
| 06-backfill-02 | 13-01 | 1 | FIX-01 | — | Sequence overload resolves for `transpose(seq, 2)` without "No matching overload" error | integration (Theory) | `dotnet test --filter "FullyQualifiedName~FlowScriptTests&Sentinel=test_transpose_int"` | ✅ | ✅ green |
| 06-backfill-03 | 13-01 | 1 | FIX-02 (bare expr) | — | Bare note stream inside `section { ... }` renders non-silent buffer | integration (Theory) | Existing Theory row `test_section_bare_expr.flow` | ✅ | ✅ green |
| 06-backfill-04 | 13-01 | 1 | FIX-02 × AUDIO-06 (gain-nested) | — | Bare note stream inside `section { gain N { ... } }` renders > 0 frames | integration | `dotnet test --filter "FullyQualifiedName~SectionGainBareExpressionTests"` | ✅ | ✅ green |
| 06-backfill-05 | 13-01 | 1 | FIX-03 | — | Missing-function call exits non-zero + stderr contains `Function 'nonExistentFunction' not found` | integration (Theory) | Existing `ExpectedErrorScripts["test_error_masking.flow"]` | ✅ | ✅ green |
| 06-backfill-06 | 13-01 | 1 | FIX-04 (INVALID) | — | N/A — reclassified INVALID 2026-04-18 per v1.1-MILESTONE-AUDIT | — | — | — | N/A |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `flow-lang.Tests/Integration/` — NEW directory created by this plan
- [x] `flow-lang.Tests/Integration/Phase06/` — NEW subdirectory

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| All Phase 6 behaviors have automated verification. | — | — | — |

---

## Observable Invariants

Each invariant is a concrete check that would fail if the Phase 6 fix were removed:

1. **QOL-01:** stderr of `FlowEngineRunner(verbose: true).RunSource("use \"@std\"\n(print \"ok\")")` contains literal `"[verbose] Executing"`.
2. **FIX-01:** Executing `tests/test_transpose_int.flow` via the Theory harness produces errorCount == 0 AND stdout contains the tightened sentinels (`transpose with int: ok`, etc.) per `RequiredSentinels` entry.
3. **FIX-02 (gain-nested):** `FlowEngineRunner.RunSource` of a script containing `section s { gain 0.5 { | C4 D4 E4 F4 | } } Song sg = [s] Buffer b = (renderSong sg "sine") Int frames = (getFrames b) (print $"frames: {(str frames)}")` produces stdout that does NOT contain `"frames: 0\n"` and DOES contain `"frames:"`.
4. **FIX-03:** Executing `tests/test_error_masking.flow` via the Theory harness produces stderr containing `"Function 'nonExistentFunction' not found"` per `ExpectedErrorScripts` entry.

---

## Pass 1 Draft (Requirements-First)

Authored by reading ONLY `v1.1-REQUIREMENTS.md` + the Phase 6 success criteria from `v1.1-ROADMAP.md` (lines 13–22). `.flow` source, `flow-lang/` source, phase SUMMARY/PLAN/RESEARCH files, and existing test code were NOT consulted during this pass. Per D-13, any reality-correction happens in Pass 2 and is logged in `## Divergences`.

- **QOL-01:** expected `--verbose` to produce a diagnostic line prefixed `[verbose]` to stderr when the interpreter starts. Observable pin: literal substring `"[verbose] Executing"` in stderr of an in-process `FlowEngineRunner(verbose: true).RunSource(...)` call.
- **FIX-01:** expected `transpose(sequence, 2)` with an `Int` 2nd argument to resolve without a "No matching overload" error. Observable pin: `tests/test_transpose_int.flow` exits errorCount == 0 AND stdout contains a `"transpose with int: ok"` sentinel (tightened from the errorCount-only Theory row Phase 12 installed).
- **FIX-02:** expected a bare note stream inside a `section` block (including inside nested `gain`/`tempo`/`timesig`/`key` context blocks per REQUIREMENTS.md §FIX-02 "— including inside nested gain/tempo/timesig/key context blocks") to render a non-silent buffer. Observable pin for the audit-driven composition gap: the `section { gain N { | notes | } }` case produces a `Buffer` with `frames > 0`, asserted as `stdout DOES NOT contain "frames: 0\n"` AND `stdout contains "frames:"`. This is a numeric pin, not a stdout sentinel — survives DSP refactors.
- **FIX-03:** expected calling a non-existent function to exit non-zero with a "function not found" message (not silently succeed). Observable pin: stderr of `tests/test_error_masking.flow` contains literal `"Function 'nonExistentFunction' not found"`, already installed as `ExpectedErrorScripts["test_error_masking.flow"]` by Plan 12-01.
- **FIX-04:** INVALID — architectural precondition does not hold per v1.1-MILESTONE-AUDIT 2026-04-18; `PlaybackFunctions.Register` captures the manager via per-registration closure (no static clobber surface), and `LiveReloadManager` uses a fresh `FlowEngine` per re-render. No validation test authored.

---

## Pass 2 Implementation Map

Reality check + test authoring performed 2026-04-20 against the post-v1.1 codebase at HEAD.

- **QOL-01:** `flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs::RunSource_WithVerbose_WritesVerbosePrefixToStderr` asserts `stderr` contains literal `"[verbose] Executing"`. A companion Fact `RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix` pins the negative case (verbose=false ⇒ no `[verbose]` prefix) under Rule 2 — strengthens the regression gate so silently enabling verbose-by-default would also break. Empirical anchor: `flow-lang/Core/FlowEngine.cs:81` emits `$"[verbose] Executing {fileName ?? \"<eval>\"}"` to stderr when `verbose=true`.
- **FIX-01:** `flow-lang.Tests/FlowScriptData.cs::RequiredSentinels["test_transpose_int.flow"]` — tightened from errorCount-only to substring-pin on `"transpose with int: ok"` + `"test_transpose_int: PASSED"`. Sentinels captured empirically from `dotnet run --project flow-interpreter tests/test_transpose_int.flow`; strings match Pass 1 draft verbatim.
- **FIX-02 (top-level bare expr):** Existing Theory row `tests/test_section_bare_expr.flow` cited; covered by the `errorCount == 0` gate in `FlowScriptTests.RunsToCompletion`. Script runs `renderSong song "piano"` inside `tempo { timesig { section { | notes | } } }` and prints `"render completed"` + `"export completed"`. No new Fact required.
- **FIX-02 × AUDIO-06 (gain-nested):** `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs::GainNestedInSection_RendersNonZeroFrames` — asserts `Assert.DoesNotContain("frames: 0\n", stdout)` (numeric-pin via stdout, survives DSP changes). The in-process `FlowEngineRunner.RunSource` script is the literal Pass 1 body; `getFrames`, `renderSong`, and `$"..."` string-interpolation all exist in the shipped codebase (`flow-lang/StandardLibrary/BuiltInFunctions.cs:399`). No script-body substitution needed.
- **FIX-03:** Existing `FlowScriptData.ExpectedErrorScripts["test_error_masking.flow"] = "Function 'nonExistentFunction' not found"` — cited directly (installed by Plan 12-01). The Theory harness asserts `stderr` contains the substring after `FlowEngineRunner.FlushErrorsToStderr()` mirrors `flow-interpreter/Program.cs:78`.
- **FIX-04:** N/A — reclassified INVALID per v1.1-MILESTONE-AUDIT 2026-04-18. No test authored.

---

## Divergences

None. Every Pass 1 assertion was literally testable against the shipped codebase:

- **QOL-01 prefix:** Pass 1 drafted `"[verbose] Executing"`; Pass 2 confirmed `FlowEngine.cs:81` emits exactly that (via `$"[verbose] Executing {fileName ?? \"<eval>\"}"`). Assertion ships unchanged.
- **FIX-01 sentinels:** Pass 1 drafted `"transpose with int: ok"` and `"test_transpose_int: PASSED"`; Pass 2 executed the script and captured those exact strings from stdout. Sentinels ship unchanged.
- **FIX-02 gain-nested script body:** Pass 1's literal Flow source `section s { gain 0.5 { | C4 D4 E4 F4 | } }` + `renderSong` + `getFrames` + `$"frames: {(str frames)}"` parses and executes at HEAD. No grammar or stdlib substitution.
- **FIX-03 error-string pin:** Pass 1 cited the existing `ExpectedErrorScripts["test_error_masking.flow"]` entry verbatim. Confirmed present in `FlowScriptData.cs:30` at HEAD.

Pass 1 was accurate to reality — the v1.1 audit had already surfaced the composition gap (FIX-02 × AUDIO-06) and commit `2156690` had already fixed it, so authoring tests from REQUIREMENTS.md produced assertions that pass against shipped behavior. The two-pass discipline produced zero drift because the audit did the reconciliation work up-front.

Under Rule 2 (auto-add missing critical functionality), one additional Fact was added:
`VerboseFlagTests.RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix` — the Pass 1 draft only specified the positive case (verbose⇒prefix). Pinning the negative case (no-verbose⇒no prefix) is a correctness requirement: without it, a future refactor that emits `[verbose]` output unconditionally would still pass. Shipped as `[Fact]` #2 in the same file.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-04-20 (71/71 `dotnet test flow-sharp.sln` green at commit `4cf0ccd`)
