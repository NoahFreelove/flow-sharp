# Phase 13: Nyquist Validation Backfill - Research

**Researched:** 2026-04-19
**Domain:** Retroactive test-coverage audit + VALIDATION.md authorship against shipped v1.1 feature set
**Confidence:** HIGH (all observable invariants verified against live source; test-infrastructure capabilities verified by reading `FlowEngineRunner.cs`, `FlowScriptData.cs`, and existing Unit/ Facts)

## Summary

Phase 13 is a pure-documentation phase: no `flow-lang/` edits, no new NuGet packages, no new `.flow` scripts unless a gap truly cannot be filled by existing coverage. The five plans each author one `VALIDATION.md` for phases 6-9 (greenfield) and promote Phase 10's draft to `nyquist_compliant: true`. The heavy lifting in planning is **choosing the observable-value pin per phase** and **classifying existing coverage as strong / weak / missing**.

The key finding of the coverage audit is nuanced: every v1.1 requirement has *some* existing coverage via a `tests/test_*.flow` script wrapped as a Theory row by Plan 12-01, but most of those scripts end with `(print "PASSED")` regardless of whether the behavior under test actually succeeded. **Existing Theory-row coverage proves scripts execute without errors; it does NOT pin observable values.** A Theory row that asserts only `errorCount == 0` for `test_section_gain_bare_expr.flow` would have stayed GREEN during the entire pre-fix (commit 2156690) period when the script silently rendered 0 frames. The phase's job is to add the observable-value pins on top of the existing scripts, using either (a) tightened Theory-row sentinels (preferred when the script already prints the value) or (b) new xUnit Facts in `flow-lang.Tests/Unit/` and the new `flow-lang.Tests/Integration/` directory (required when the existing script's output is insufficient).

**Primary recommendation:** Adopt **Option (b) from research question 3** — two-TASK split within each plan, with Task 1's `<read_first>` constrained to `v1.1-REQUIREMENTS.md` + `ROADMAP.md` Phase section only. This preserves bisectability (one commit per plan rather than two), honors D-13's two-pass discipline mechanically via the planning format, and matches the Phase 12 pattern where RED-test and GREEN-fix were bundled into one atomic commit per plan.

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Plan structure (5 plans, atomic commits per phase):**
- **D-01:** 13-01 = Phase 6 VALIDATION.md (QOL-01, FIX-01, FIX-02, FIX-03) + new xUnit Facts as needed
- **D-02:** 13-02 = Phase 7 VALIDATION.md (DX-01, DX-02, DX-03, DX-04) + new Facts as needed
- **D-03:** 13-03 = Phase 8 VALIDATION.md (AUDIO-05, AUDIO-06, AUDIO-07) + new Facts as needed
- **D-04:** 13-04 = Phase 9 VALIDATION.md (AUDIO-08, QOL-02) + new Facts as needed
- **D-05:** 13-05 = Phase 10 VALIDATION.md promotion (VOC-01, VOC-02) + REQUIREMENTS.md TEST-04 Complete + STATE/ROADMAP closure
- **D-06:** All five plans land in Wave 1 parallel (distinct phase directories + additive test files, zero file overlap)
- **D-07:** No separate rollup plan — 13-05 closes TEST-04 as last commit
- **D-08:** One VALIDATION.md creation commit per plan (+ optional Fact-file commits)

**Test strategy:**
- **D-09:** New validation tests land as native xUnit Facts in `flow-lang.Tests/Unit/` or `flow-lang.Tests/Integration/`
- **D-10:** Existing `.flow` scripts wrapped as Theory rows by Plan 12-01 count as coverage when they target a v1.1 requirement — cite the Theory row path + required sentinel instead of authoring a duplicate Fact
- **D-11:** Observable-value pin per phase is either (a) error-message text OR (b) numeric durations/sample counts. **Buffer byte hashes are forbidden.**
- **D-12:** No new NuGet packages. Hand-rolled zero-crossing / peak detection in test file. No FFT library.

**Requirements-first authorship (ROADMAP criterion 1):**
- **D-13:** Two-pass strict — Pass 1 reads ONLY `v1.1-REQUIREMENTS.md` + ROADMAP Phase section, authors `## Per-Task Verification Map` + `## Observable Invariants` + test skeletons. Pass 2 reads full context (SUMMARY, code, existing tests) and implements skeletons.
- **D-14:** Pass-2 adjustments logged in `## Divergences` section. Mirrors Phase 12's `## Empirical Overrides`.
- **D-15:** On meaningful Pass-1 vs Pass-2 disagreement: document in Divergences which is correct; do NOT edit REQUIREMENTS.md.

**Phase 10 — promote, don't waive:**
- **D-16:** Manual-only items (formant quality, espeak-ng external) partly automatable. Automate what is feasible; keep `## Manual-Only Verifications` for genuinely subjective / external-dependent items.
- **D-17:** `nyquist_compliant: true` earned when every REQUIREMENT has at least one automated test.
- **D-18:** Phase 10 pin — `sing("ah", C4, 2.0)` buffer length equals exactly 88200 samples at 44.1kHz.

**Scope of new tests vs reuse:**
- **D-19:** Existing coverage wins. Pass-1 author checks Plan 12-01 Theory-row catalog first.
- **D-20:** Integration tests where requirement is user-visible; unit tests where requirement is internal contract.
- **D-21:** 13-* plans MAY add tests but MAY NOT modify existing tests (except Phase 10 VALIDATION.md frontmatter).

**Phase-completion bookkeeping:**
- **D-22:** 13-05 updates REQUIREMENTS.md TEST-04 to `[x]` and Traceability to `Complete`
- **D-23:** `*-VERIFICATION.md` files NOT touched by Phase 13
- **D-24:** 13-VALIDATION.md (phase-13's own) minimal — dotnet test green + presence-check of each created VALIDATION.md suffices

### Claude's Discretion

- xUnit Fact naming convention (e.g., `Validation.Phase06.VerboseFlagTests` vs `Unit.VerboseFlagTests`)
- Wording of each `## Observable Invariants` entry
- Whether to share a `AudioTestHelpers.CountZeroCrossings(buffer, start, length)` helper vs inline per-test
- Whether QOL-02 pin is a Theory row on `examples/tutorial.flow` (exit 0) OR an Integration Fact asserting a specific stdout line
- Whether Phase 12 VALIDATION.md gets promoted here (out of scope per ROADMAP — recommend NO)

### Deferred Ideas (OUT OF SCOPE)

- v1.2 phase VALIDATION.md enrichment (Phase 11, 12 both `nyquist_compliant: false`) — future pass
- FIX-04 INVALID retroactive acknowledgment — already captured in `v1.1-MILESTONE-AUDIT.md`
- Cross-phase Nyquist rollup doc (aggregate verdict lives in `v1.1-MILESTONE-AUDIT.md` already)
- Refactoring existing tests discovered flaky during Pass-2 — logged as Divergence, deferred
- Migrating CLAUDE.md test runner docs from `for test in tests/test_*.flow` — separate DX pass
- FFT-based harmonic analysis for deeper Phase 10 — rejected, no new deps

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-04 | Retroactive Nyquist validation — phases 06/07/08/09 each gain a `VALIDATION.md`; phase 10 draft updated to `nyquist_compliant: true` or explicit waiver | All 15 v1.1 requirements mapped below with existing-coverage status and proposed observable-value pin. Test infrastructure (xUnit 2.9.3, FlowEngineRunner, Theory harness) already established by Phase 12 — no new NuGet packages, no new fixtures required. |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| VALIDATION.md authorship | Documentation (`.planning/phases/`) | - | Per-phase per-requirement coverage map + observable-pin design. No code tier. |
| Test skeleton (Pass 1) | Documentation embedded in VALIDATION.md | - | Skeleton is text; Pass 2 implements. |
| Fact implementation (Pass 2) | `flow-lang.Tests/Unit/` or `flow-lang.Tests/Integration/` (C#) | `tests/*.flow` (Flow source, read-only by this phase) | Integration Facts use `FlowEngineRunner` to exec literal strings or existing .flow files. Unit Facts call C# APIs directly (`FormantSynthesizer.SynthesizeVowel`, `TtsHook.GetCommand`, etc.). |
| Existing Theory-row sentinel augmentation | `flow-lang.Tests/FlowScriptData.cs` | - | Prefer adding a `RequiredSentinels` entry over creating a whole new Fact when the existing `tests/test_*.flow` script already prints the value being pinned. |
| Phase 10 promotion | `.planning/phases/10-vocalization/10-VALIDATION.md` frontmatter `nyquist_compliant: true` | `flow-lang.Tests/Unit/FormantSynthesizerTests.cs` (new) | Frontmatter edit + one or two new Facts. |
| TEST-04 closure | `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/ROADMAP.md` | - | 13-05's closing commit. |

## Standard Stack

### Core (Already Installed — No Changes)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit.v3 | 3.2.2 [VERIFIED: flow-lang.Tests/flow-lang.Tests.csproj:13] | Unit + Theory test framework | Already established by Plan 12-01; `dotnet test flow-sharp.sln` runs 68 tests [VERIFIED: 12-VERIFICATION.md line 41] |
| Microsoft.NET.Test.Sdk | 17.13.0 [VERIFIED: csproj:12] | Test runner | Already present |
| xunit.runner.visualstudio | 3.1.5 [VERIFIED: csproj:14] | VSIX adapter (v2/v3 shared) | Already present |
| coverlet.collector | 6.0.2 [VERIFIED: csproj:15] | Coverage | Already present; not required for Phase 13 but available |
| .NET 10.0 | net10.0 [VERIFIED: csproj:3] | Runtime | Already in use (note: CLAUDE.md says net9.0 but actual target is net10.0 per Phase 12 correction) |

### Supporting (Already Available)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `FlowEngineRunner` fixture | internal | In-process FlowEngine with stdout/stderr capture | Every Integration Fact that exec's a literal script string or reads a .flow file |
| `FlowScriptData.RequiredSentinels` | internal | Per-file required stdout substring list | When an existing .flow script prints the value we want to pin — add a sentinel entry rather than a new Fact |
| `FlowScriptData.ExpectedErrorScripts` | internal | Per-file expected stderr substring | When a script intentionally errors and we want to pin the error text |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled zero-crossing fundamental detection (VOC-01) | MathNet.Numerics FFT | Explicitly forbidden by D-12; FFT is overkill for a pin that `sing("ah", C4, 2.0)` returns buffer-length 88200 (the buffer-length pin is sufficient and FFT-free) |
| New xUnit Fact per requirement | Tighten existing Theory-row `RequiredSentinels` | D-19 (existing coverage wins): tighten sentinels where scripts already print observable values; author new Facts only when existing output is insufficient |
| `tests/validation/*.flow` subdirectory | xUnit Facts in `flow-lang.Tests/Integration/` | D-20 recommends xUnit Facts for user-visible behaviors — more explicit assertions than "print PASSED" pattern |

**Installation:**
```bash
# No new packages. Phase 13 uses the existing xUnit stack.
# dotnet test flow-sharp.sln
```

**Version verification:**
```bash
# Verified via file read 2026-04-19:
# - flow-lang.Tests/flow-lang.Tests.csproj (xunit.v3 3.2.2, Microsoft.NET.Test.Sdk 17.13.0)
# - No new NuGet packages introduced in this phase (D-12 forbids)
```

## Architecture Patterns

### System Architecture Diagram

```
                       +-----------------------------------+
                       |  v1.1 Requirements Archive        |
                       |  (.planning/milestones/           |
                       |   v1.1-REQUIREMENTS.md)           |
                       +-----------------+-----------------+
                                         |
                                         | Pass 1 reads ONLY this + ROADMAP
                                         v
     +-----------------------------------+-----------------------------------+
     |  Pass 1 (planner-as-author, REQUIREMENTS-only view)                   |
     |  Authors: ## Per-Task Verification Map                                |
     |           ## Observable Invariants                                    |
     |           test skeletons (assertion text + expected values)           |
     +-----------------------------------+-----------------------------------+
                                         |
                                         | Pass 1 output becomes executor input
                                         v
     +-----------------------------------+-----------------------------------+
     |  Pass 2 (executor, full context) reads:                               |
     |  - Pass 1 VALIDATION.md draft                                         |
     |  - Existing FlowScriptData.cs (coverage catalog)                      |
     |  - SUMMARY.md, phase CONTEXT.md, source under flow-lang/              |
     |  - Existing tests/test_*.flow                                         |
     |  Decides per invariant:                                               |
     |    (a) existing Theory row + RequiredSentinels tighten     (prefer)   |
     |    (b) new xUnit Fact in Unit/ (C# API)                    (if needed)|
     |    (c) new xUnit Fact in Integration/ (FlowEngineRunner)   (if needed)|
     |  Records any Pass1 vs Pass2 drift in ## Divergences                   |
     +-----------------------------------+-----------------------------------+
                                         |
                                         v
     +-----------------------------------+-----------------------------------+
     |  Output per plan: one VALIDATION.md creation commit,                  |
     |  optional new-Fact commits, all in a single wave (Wave 1 parallel).   |
     |  13-05 closes TEST-04 + STATE/ROADMAP + Phase 10 promotion.           |
     +-----------------------------------------------------------------------+
```

### Recommended Project Structure

```
.planning/phases/
  06-diagnostics-bug-fixes/06-VALIDATION.md      # NEW — plan 13-01
  07-developer-experience/07-VALIDATION.md       # NEW — plan 13-02
  08-audio-production/08-VALIDATION.md           # NEW — plan 13-03
  09-advanced-features/09-VALIDATION.md          # NEW — plan 13-04
  10-vocalization/10-VALIDATION.md               # PROMOTE (existing draft) — plan 13-05
  13-nyquist-validation-backfill/13-VALIDATION.md # NEW — minimal, dotnet-test-green gate only

flow-lang.Tests/
  Fixtures/FlowEngineRunner.cs                   # EXISTING — reuse
  FlowScriptData.cs                              # EXISTING — add sentinel entries
  FlowScriptTests.cs                             # EXISTING — no changes
  Unit/
    CollectionsTests.cs                          # EXISTING (Phase 12)
    InterpreterTests.cs                          # EXISTING (Phase 12)
    ThunkTests.cs                                # EXISTING (Phase 12)
    Phase06/                                     # NEW subdir (optional — see Pattern 2)
    Phase07/
    Phase08/
    Phase09/
    Phase10/
  Integration/                                   # NEW directory — the first plan that needs it creates it
    Phase06/
    Phase07/
    Phase08/
    Phase09/
```

### Pattern 1: Two-Task Plan Structure (RECOMMENDED per research question 3)

**What:** Each 13-* plan contains Task 1 (Pass 1: REQUIREMENTS-only draft) and Task 2 (Pass 2: full-context implementation), bundled into ONE atomic commit per plan.

**When to use:** All five 13-* plans. Preserves D-08's "one VALIDATION.md creation commit per plan."

**Example:**
```yaml
tasks:
  - id: "13-01-01"
    name: "Pass 1: REQUIREMENTS-only VALIDATION.md draft for Phase 6"
    read_first:
      - .planning/milestones/v1.1-REQUIREMENTS.md  # ONLY
      - .planning/ROADMAP.md                        # Phase 6 section + Phase 13 success criteria ONLY
      - ~/.claude/get-shit-done/templates/VALIDATION.md
      - .planning/phases/12-stability/12-VALIDATION.md  # format reference
    forbidden_reads:
      - .planning/phases/06-diagnostics-bug-fixes/06-01-SUMMARY.md
      - .planning/phases/06-diagnostics-bug-fixes/06-02-SUMMARY.md
      - flow-lang/**
      - flow-lang.Tests/**
      - tests/**
    actions:
      - Write draft 06-VALIDATION.md with ## Per-Task Verification Map + ## Observable Invariants from REQ text only
      - Leave assertion text bracketed {expected value TBD by Pass 2} where not derivable from REQ

  - id: "13-01-02"
    name: "Pass 2: Implementation + Divergences log for Phase 6"
    read_first:
      - (everything from Pass 1 +)
      - .planning/phases/06-diagnostics-bug-fixes/06-*-SUMMARY.md
      - flow-lang/Diagnostics/**
      - flow-lang/TypeSystem/OverloadResolver.cs
      - flow-lang/Interpreter/Interpreter.cs
      - flow-lang.Tests/FlowScriptData.cs  # check existing coverage catalog
      - tests/test_verbose.flow, tests/test_error_masking.flow, tests/test_section_gain_bare_expr.flow
    actions:
      - Fill {expected value TBD} brackets with real values from source
      - Add new xUnit Facts for any observable invariant not coverable via sentinel
      - Log Pass1-vs-Pass2 disagreements in ## Divergences
      - Set frontmatter nyquist_compliant: true, wave_0_complete: true
```

**Source:** D-13 two-pass strict authorship; D-08 one-commit-per-plan atomicity; Phase 12 plans 12-02 through 12-05 pattern.

### Pattern 2: Observable-Value Pin via Tightened Sentinel (preferred)

**What:** When an existing `tests/test_*.flow` script already prints the observable value, add a `RequiredSentinels` entry in `flow-lang.Tests/FlowScriptData.cs` rather than authoring a new xUnit Fact.

**When to use:** Most AUDIO-* requirements (existing scripts print frame counts).

**Example:**
```csharp
// FlowScriptData.cs additions by plan 13-03 (AUDIO-08 tempoRamp)
public static readonly Dictionary<string, string[]> RequiredSentinels = new()
{
    // ... existing spike/c1 entry ...

    // AUDIO-08: existing test_tempo_ramp.flow already prints "Test 2" and "Test 3"
    // boolean results; pin the "true" values to assert ritardando/accelerando semantics.
    ["test_tempo_ramp.flow"] = new[]
    {
        "Test 1 - tempoRamp produces non-zero buffer: true",
        "Test 2 - Ritardando produces more frames than constant fast: true",
        "Test 3 - Accelerando produces fewer frames than constant slow: true",
    },
};
```

**Source:** D-19 "existing coverage wins"; pattern mirrors the spike/c1 sentinel array at FlowScriptData.cs:64-70.

### Pattern 3: Integration Fact via FlowEngineRunner (for literal-script pins)

**What:** Use `FlowEngineRunner.RunSource(literalString)` for pins that require an assertion stronger than a stdout sentinel — e.g., frame-count ranges, error-count assertions, post-run reads of generated files.

**When to use:** FIX-02 gain-nested bare-expression pin; DX-03 writeWav output file existence; AUDIO-05 mix additive math.

**Example:**
```csharp
// Integration/Phase06/SectionGainBareExpressionTests.cs (NEW — plan 13-01)
using Xunit;
using FlowLang.Tests.Fixtures;

namespace FlowLang.Tests.Integration.Phase06;

[Collection("FlowScripts")]  // serialize Console.SetOut per 12-04 pattern
public class SectionGainBareExpressionTests
{
    [Fact]
    public void GainNestedInSection_RendersNonZeroFrames()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
section s { gain 0.5 { | C4 D4 E4 F4 | } }
Song sg = [s]
Buffer b = (renderSong sg ""sine"")
Int frames = (getFrames b)
(print $""frames: {(str frames)}"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        // Pre-fix: "frames: 0". Post-fix (commit 2156690): non-zero.
        Assert.DoesNotContain("frames: 0\n", stdout + "\n");
        Assert.Contains("frames:", stdout);
    }
}
```

**Source:** `flow-lang.Tests/Unit/InterpreterTests.cs:18-36` ExecuteMusicalContextTests pattern (ships as `use "@std"` + literal script).

### Anti-Patterns to Avoid

- **Asserting "print PASSED" strings as proof of behavior.** `tests/test_section_gain_bare_expr.flow` prints `PASSED` whether frames are 0 or non-zero. Every `PASSED`-only assertion is a false sense of safety — pin the numeric value printed just above the `PASSED` line instead.
- **Buffer byte hashes.** D-11 forbidden. `sha256(wavBytes)` breaks on any DSP coefficient change; use frame counts or sample-count semantics.
- **FFT-based frequency pinning.** D-12 forbidden. Zero-crossing count on middle 50% of buffer is sufficient for fundamental-frequency pinning at ±5%.
- **Editing REQUIREMENTS.md to match reality.** D-15: document drift in `## Divergences`, do NOT rewrite historical requirements.
- **Inventing error strings.** Pass 2 MUST grep the code for exact error-message format before asserting `Assert.Contains`. Examples: `"Cannot get init of empty array"` [VERIFIED: Collections.cs:91-92 + InterpreterTests existing coverage], `"Function '{name}' not found"` [VERIFIED: ExecutionContext.cs:168], `"No matching overload for function '{name}'"` [VERIFIED: OverloadResolver.cs:51], `"Unknown vowel phoneme: '{vowel}'. Valid: ah, ee, eh, oh, oo"` [VERIFIED: FormantData.cs:74-75].
- **Pass-2 reading before Pass 1.** The whole point of two-pass strict is that Pass 1 drafts skeletons against the requirement text. Peeking at code during Pass 1 defeats the confirmation-bias-catching property (Phase 11 caught C5 exactly because researchers did not peek).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Stdout capture | Custom `Console.SetOut` wrapper | `FlowEngineRunner` [VERIFIED: flow-lang.Tests/Fixtures/FlowEngineRunner.cs:13-20] | Already handles stdout/stderr capture + `FlushErrorsToStderr` mirror of Program.cs:78 |
| Error-count assertion | Parse stderr for "Error:" substrings | `runner.RunSource(...).ErrorCount` [VERIFIED: FlowEngineRunner.cs:27] | `ErrorReporter.Errors.Count` is already surfaced by the fixture |
| Writing relative-path .flow scripts | Custom path resolution | Copy the CWD-pivot pattern from `FlowScriptTests.cs:23-24` | Phase 12 Plan 12-01 solved this — `Environment.CurrentDirectory = Path.GetDirectoryName(testsRoot)!` pre-test, restore in `finally` |
| Running flow code from tests | Manually invoke parser / interpreter | `FlowEngineRunner.RunSource(literalString)` OR `runner.RunFile(absolutePath)` | Both paths go through `FlowEngine.Execute` which is the production entry point |
| xUnit test parallelism gotchas | Per-test `Console.SetOut` wrangling | `[Collection("FlowScripts")]` attribute [VERIFIED: FlowScriptTests.cs:6-9, ExecuteMusicalContextTests uses it at InterpreterTests.cs:14] | Plan 12-01 established the pattern — any test that captures Console MUST join this collection for serialization |
| Fundamental-frequency estimation | FFT library (MathNet.Numerics) | Zero-crossing count over middle 50% of buffer (see research question 4) | D-12 forbids new deps; zero-crossing is ~10 LOC and ±5% accurate |
| TTS external-command mocking | Mock subprocess framework | `TtsHook.SetCommand("echo"); TtsHook.GetCommand()` round-trip [VERIFIED: TtsHook.cs:17-28] | `setTtsCommand` is exactly the public API we want to pin; no need to actually invoke |
| Vowel-phoneme validation | Parse output buffer spectrum | `FormantData.GetFormants("notaphoneme")` throws `ArgumentException` with known message [VERIFIED: FormantData.cs:74-75] | Unit Fact on static method, no audio required |

**Key insight:** The Phase 12 test infrastructure already provides every primitive Phase 13 needs. Zero new fixtures, zero new dependencies, zero new base classes. Phase 13's job is to enumerate observable invariants and wire them to the existing primitives.

## Existing Coverage Map

Coverage status per v1.1 requirement, using Nyquist terminology:

- **COVERED**: Existing test pins an observable value (error text, numeric duration, structural assertion). No new work needed beyond citing the test.
- **PARTIAL**: Existing test executes the behavior but asserts only "no error" or prints `PASSED` regardless of result. Need to tighten via sentinel or new Fact.
- **MISSING**: No existing test targets the requirement. Need a new Fact.

### Phase 6 — Diagnostics & Bug Fixes (4 requirements)

| REQ | Existing Coverage | Status | Gap / Proposed Pin |
|-----|-------------------|--------|---------------------|
| QOL-01 `--verbose` | `tests/test_verbose.flow` (Theory row; prints `verbose test ok`) [VERIFIED: file read] + source `FlowEngine.cs:42,81` emits `[verbose]` prefix to stderr | PARTIAL | Existing script runs flow engine WITHOUT `--verbose` — no pin on the flag's actual behavior. **New Integration Fact:** `RunSource("use \"@std\"\n(print \"ok\")", verbose: true)` → stderr contains `[verbose] Executing <test>`. `FlowEngineRunner` already exposes `verbose` constructor arg [VERIFIED: FlowEngineRunner.cs:13]. |
| FIX-01 Sequence overload | `tests/test_transpose_int.flow` (Theory row; prints `transpose with int: ok` on success, exits 1 on failure) [VERIFIED: file read] | COVERED (weak) | Theory row already asserts `errorCount == 0`. Sufficient for FIX-01 since pre-fix `transpose(seq, 2)` raised "No matching overload" [VERIFIED: OverloadResolver.cs:51 error format]. **Tighten:** add `RequiredSentinels["test_transpose_int.flow"] = ["transpose with int: ok", "transpose with semitone: ok", "test_transpose_int: PASSED"]`. |
| FIX-02 bare-expression capture (incl. gain-nested) | `tests/test_section_bare_expr.flow` (top-level bare expr) + `tests/test_section_gain_bare_expr.flow` (gain-nested — the AUDIT integration gap) [VERIFIED: file read] | PARTIAL | Both scripts print `PASSED` regardless of whether frames > 0 (the bug manifested as 0 frames silently). **New Integration Fact:** `SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames` — literal script + `Assert.DoesNotContain("frames: 0\n", ...)`. This is the audit's §integration regression gate. |
| FIX-03 fatal vs non-fatal errors | `tests/test_error_masking.flow` (Theory row in ExpectedErrorScripts, pins `"Function 'nonExistentFunction' not found"`) [VERIFIED: FlowScriptData.cs:30] | COVERED | Existing ExpectedErrorScripts entry pins the exact error text. Cite directly. **No new Fact.** |

### Phase 7 — Developer Experience (4 requirements)

| REQ | Existing Coverage | Status | Gap / Proposed Pin |
|-----|-------------------|--------|---------------------|
| DX-01 `//` line comments | `tests/test_comments.flow` (Theory row; 43 lines exercising full-line, inline, pre-code, post-code, inside-proc, post-note-stream comments) [VERIFIED: file read] | COVERED | Script would fail to parse without `//` support → Theory row already pins tokenizer behavior. **Tighten:** `RequiredSentinels["test_comments.flow"] = ["5", "10", "15", "8"]` (outputs from the prints — 8 is `addOne(7)`). |
| DX-02 math stdlib | `tests/test_math.flow` (Theory row; prints results of sin/cos/tan/abs/sqrt/min/max/floor/ceil/pi/tau/pow/log) [VERIFIED: file read, head] | PARTIAL | Theory row asserts `errorCount == 0` but doesn't pin specific numeric results. **Tighten:** `RequiredSentinels` with specific values — `"0"` (sin 0.0), `"1"` (cos 0.0), `"4"` (sqrt 16.0), `"3"` (min 3 7), `"7"` (max 3 7), `"3.141592653589793"` (pi). Value format must match `str` output — Pass 2 empirically confirms. |
| DX-03 writeWav primary + exportWav alias | `tests/test_writewav.flow` (Theory row; tests writeWav + exportWav both, loads back, prints frames) [VERIFIED: file read] | PARTIAL | Script prints `All writeWav tests passed` on success; Theory row GREEN. **Tighten:** `RequiredSentinels["test_writewav.flow"] = ["PASS: writeWav(String, Buffer) succeeded", "PASS: exportWav(Buffer, String) backwards compat succeeded", "All writeWav tests passed"]`. **Additional Unit Fact:** `registry.Register` confirms BOTH `writeWav` and `exportWav` signatures register — grep `BuiltInFunctions.cs:445-449` already verifies. Optional but strengthens. |
| DX-04 REPL auto-imports | `tests/test_repl_autoimport.flow` (Theory row; script-mode only — comments in file explicitly note: "REPL auto-import must be tested interactively") [VERIFIED: file read] | PARTIAL | Existing script tests SCRIPT mode with explicit imports — this does NOT test the REPL auto-import behavior. **New Unit Fact:** `RepLAutoImportTests.AutoImportStandardModules_IncludesStdAudioCollections` — either (a) construct a `FlowEngine` and invoke the three `use` statements the Repl.cs:86-90 hardcodes, verify `print`/`createSineTone`/`list` resolve without error, OR (b) simpler: grep Repl.cs:86-90 for the three module literals (trivial but lower-value). Option (a) preferred — mirrors v1.1 audit's observation that "DX-04 is not e2e-testable via piped stdin — verified by code inspection only" [VERIFIED: v1.1-MILESTONE-AUDIT.md line 49]. |

### Phase 8 — Audio Production (3 requirements)

| REQ | Existing Coverage | Status | Gap / Proposed Pin |
|-----|-------------------|--------|---------------------|
| AUDIO-05 `mix` | `tests/test_mix.flow` (Theory row; prints mix frame counts for equal-length, piped, and different-length buffers) [VERIFIED: file read] | PARTIAL | Script prints `mix tests passed` always. **Tighten:** `RequiredSentinels["test_mix.flow"] = ["mix frames: 22050", "mix channels: 1", "piped mix frames: 22050", "diff length frames: 22050"]`. (22050 = 0.5s × 44100Hz.) **Additional Unit Fact:** `MixTests.Mix_SumsSamples_AdditiveSemantics` — `AudioCore.Mix({buf_samples:[0.5], buf_samples:[0.3]})` → output `Data[0] == 0.8f`. Pin the additive math [VERIFIED: AudioCore.cs:200 `result.Data[i] = sampleA + sampleB`]. |
| AUDIO-06 per-section gain | `tests/test_gain_context.flow` (Theory row; prints frame counts for nested gain blocks and in-song gain) [VERIFIED: file read] | PARTIAL | Script prints frame counts but doesn't assert values. **Tighten:** `RequiredSentinels["test_gain_context.flow"] = ["gain 0.5 block executed", "nested gain context executed", "quiet section frames:"]`. **Critical:** AUDIO-06 is also covered by the new Integration Fact for FIX-02 gain-nested (Phase 6 plan) since the audit integration gap is their intersection — the one test covers both REQ-IDs. **Cross-reference in both VALIDATION.md files.** |
| AUDIO-07 strings/organ/bell | `tests/test_synth_presets.flow` (Theory row; calls `renderSequenceToVoices` for each preset, prints "OK") [VERIFIED: file read] | PARTIAL | Script prints `rendered voices OK` always. **Tighten:** `RequiredSentinels["test_synth_presets.flow"] = ["strings: rendered voices OK", "organ: rendered voices OK", "bell: rendered voices OK"]`. **Additional Unit Fact:** `SynthesizerFactoryTests.Create_SupportsStringsOrganBell` — direct test of the switch at `NoteSynthesizer.cs:231-233` [VERIFIED: grep] — `SynthesizerFactory.Create("strings") is StringsSynthesizer`, same for `"organ"` → `OrganSynthesizer`, `"bell"` → `BellSynthesizer`. Deterministic, no audio required. |

### Phase 9 — Advanced Features (2 requirements)

| REQ | Existing Coverage | Status | Gap / Proposed Pin |
|-----|-------------------|--------|---------------------|
| AUDIO-08 tempoRamp | `tests/test_tempo_ramp.flow` (Theory row; prints ritardando/accelerando comparison results as "Test N: true/false") [VERIFIED: file read] | PARTIAL → COVERED via sentinel | Script prints booleans. **Tighten:** `RequiredSentinels["test_tempo_ramp.flow"] = ["Test 1 - tempoRamp produces non-zero buffer: true", "Test 2 - Ritardando produces more frames than constant fast: true", "Test 3 - Accelerando produces fewer frames than constant slow: true"]`. This converts weak coverage into strong observable-value pins with zero new Facts — matches D-19 "existing coverage wins" cleanly. |
| QOL-02 interactive tutorial | `examples/tutorial.flow` (348 lines; NOT currently in the Theory harness — `FlowScriptData.GetFlowScripts` globs `tests/**/*.flow` only) [VERIFIED: FlowScriptData.cs:8; wc -l examples/tutorial.flow] | MISSING | Tutorial is NOT executed by any existing test. **New Integration Fact:** `TutorialTests.TutorialRunsToCompletion` — `runner.RunFile("examples/tutorial.flow")` → `errorCount == 0`, exit 0. Deliberately DO NOT pin specific feature demonstrations — per research question 6, tutorial-refresh is Phase 16 (QOL-03). Pin existence + exit 0 only. **Flag as `## Ultra-Important Finding` if tutorial.flow currently errors** (confirmed-working per v1.1-MILESTONE-AUDIT.md line 105, but Pass 2 must re-verify because stability fixes landed since). |

### Phase 10 — Vocalization (2 requirements, existing draft to promote)

| REQ | Existing Coverage | Status | Gap / Proposed Pin |
|-----|-------------------|--------|---------------------|
| VOC-01 sing formants | `tests/test_vocalization.flow` (Theory row; 58 lines; tests 5 vowels + 3 consonant syllables + mix + pitches + WAV export) [VERIFIED: file read] | PARTIAL | Script prints frame counts but doesn't assert exact values. **Tighten:** `RequiredSentinels["test_vocalization.flow"] = ["frames: 22050", "PASS: sing ah produced audio buffer", "PASS: all 5 vowels synthesized", "PASS: consonant syllables synthesized", "PASS: vocal mixed with instrumental"]`. (0.5s × 44100 = 22050.) **Additional Unit Fact** (required for D-18 compliance): `FormantSynthesizerTests.SynthesizeVowel_Ah_At_C4_For_2s_Returns_88200_Samples` — calls `FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0)` → `result.Frames == 88200`. [VERIFIED: FormantSynthesizer.cs:24 `numSamples = (int)(durationSeconds * sampleRate)`; default sampleRate=44100]. **Additional Fact:** `FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException` → message matches `"Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo"` [VERIFIED: FormantData.cs:74-75]. |
| VOC-02 tts external | `tests/test_vocalization.flow` line 55 explicitly skips TTS (`Note: TTS test skipped (requires espeak-ng installed)`) [VERIFIED: file read] | MISSING | **New Unit Fact (safe, no-subprocess):** `TtsHookTests.SetCommand_RoundTrips_ViaGetCommand` — `TtsHook.SetCommand("echo"); Assert.Equal("echo", TtsHook.GetCommand())` [VERIFIED: TtsHook.cs:17-28 public API]. Default is `"espeak-ng --stdout"` [VERIFIED: TtsHook.cs:11]. **Additional Fact:** `TtsHookTests.SetCommand_Empty_ThrowsArgumentException` — pins the validation at TtsHook.cs:19-20. **Defer actual subprocess invocation to Manual-Only Verifications** — requires espeak-ng installed, not automatable in CI. |

### Summary of New Facts Required

| Plan | New Facts | Location |
|------|-----------|----------|
| 13-01 (Phase 6) | 1: `VerboseFlagTests.Stderr_Contains_VerbosePrefix` (new), 1: `SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames` (new) | `Integration/Phase06/` |
| 13-02 (Phase 7) | 1: `RepLAutoImportTests.AutoImportedModulesResolve` (new) | `Integration/Phase07/` |
| 13-03 (Phase 8) | 2: `MixTests.Mix_SumsSamples_AdditiveSemantics` (new), `SynthesizerFactoryTests.Create_SupportsStringsOrganBell` (new) | `Unit/Phase08/` (pure C# API tests) |
| 13-04 (Phase 9) | 1: `TutorialTests.TutorialRunsToCompletion` (new) | `Integration/Phase09/` |
| 13-05 (Phase 10) | 2: `FormantSynthesizerTests` (2 Facts: 88200-sample pin + unknown-vowel exception), `TtsHookTests` (2 Facts: SetCommand round-trip + empty-command exception) | `Unit/Phase10/` |

**Plus Tightened Sentinels** in `FlowScriptData.cs` (additive, single edit per plan, one commit file):
- test_transpose_int.flow, test_error_masking.flow (already entry) — Phase 6
- test_comments.flow, test_math.flow, test_writewav.flow — Phase 7
- test_mix.flow, test_gain_context.flow, test_synth_presets.flow — Phase 8
- test_tempo_ramp.flow — Phase 9
- test_vocalization.flow — Phase 10

Total new Fact files: 7. Total tightened sentinel entries: 9.

## Common Pitfalls

### Pitfall 1: Sentinel Text Drift
**What goes wrong:** A sentinel entry like `"mix frames: 22050"` relies on the exact format of the script's `(print $"mix frames: {...}")` output. If a future script edit changes "mix frames:" to "mix-frames:", the assertion fails on an irrelevant change.
**Why it happens:** Format strings and sentinels are authored independently.
**How to avoid:** Keep sentinel substrings as SHORT as possible while still being unique — `"mix frames: 22050"` is the value we care about; `"22050"` alone is too loose (could false-positive against other frame counts). Pick the minimum unique string.
**Warning signs:** Sentinel fails on test runs where the behavior is known correct — check if the script's print format changed.

### Pitfall 2: Missing `use "@std"` in Literal-Script Integration Facts
**What goes wrong:** `FlowEngineRunner.RunSource("(print \"x\")")` fails with "Function 'print' not found" because FlowEngine does NOT auto-import stdlib — Repl.cs does this at REPL init, but `FlowEngine.Execute` does not.
**Why it happens:** Easy to forget that test_*.flow scripts all start with `use "@std"` — the auto-import is REPL-only.
**How to avoid:** Every literal-string RunSource MUST start with `use "@std"` (and `use "@audio"` if touching buffers) [VERIFIED: InterpreterTests.cs:26 + commentary at lines 22-25; STATE.md line 97].
**Warning signs:** Fact fails with "Function 'print' not found" or similar on a script that should work.

### Pitfall 3: Test Parallelism Breaks Console Capture
**What goes wrong:** Two xUnit tests running in parallel both call `Console.SetOut(_stdout)` — they cross-contaminate stdout buffers.
**Why it happens:** xUnit runs test classes in parallel by default.
**How to avoid:** Every test class that uses `FlowEngineRunner` MUST have `[Collection("FlowScripts")]` attribute [VERIFIED: FlowScriptTests.cs:6-9 defines `[CollectionDefinition("FlowScripts", DisableParallelization = true)]`; InterpreterTests.cs:14 uses it].
**Warning signs:** Tests pass individually but fail when run together; stdout contains output from a different test.

### Pitfall 4: Working Directory Mismatch for .flow Files Using Relative Paths
**What goes wrong:** `test_full_song.flow` uses `tests/output/test_full_song.wav` as a relative path. When run via xUnit from `flow-lang.Tests/bin/Debug/net10.0/`, the relative path resolves to `flow-lang.Tests/bin/Debug/net10.0/tests/output/` which doesn't exist.
**Why it happens:** `dotnet run --project flow-interpreter tests/foo.flow` runs from the repo root; xUnit runs from the test-bin output.
**How to avoid:** Integration Facts that read/write files MUST pivot `Environment.CurrentDirectory` to repo root using the `FlowScriptTests.cs:23-24` pattern and restore in `finally`.
**Warning signs:** Test fails with "Could not find file" or "Directory not found" for a script that runs correctly via `dotnet run`.

### Pitfall 5: Pin Value Depends on Flow Parser's Display Format
**What goes wrong:** A sentinel like `"3.141592653589793"` pins `(print (str pi))` output, but C# `Math.PI.ToString()` may return `"3.1415926535897931"` (one extra digit) or `"3.141592653589793"` depending on culture.
**Why it happens:** `Value.ToString()` for a Double delegates to `double.ToString()` which is culture- and .NET-version-dependent.
**How to avoid:** Pass 2 MUST run the script empirically and copy the EXACT printed output as the sentinel. Do not infer format from C# math expectations.
**Warning signs:** Pi/trig assertions fail on a machine that previously passed — culture/locale or .NET runtime version changed.

### Pitfall 6: Pass-1 Drafted Assertion Is Non-Testable
**What goes wrong:** Pass 1 drafts "stderr contains 'Sequence type is not compatible with Int'" for FIX-01, but the actual OverloadResolver error format is `"No matching overload for function 'transpose' with argument types (Sequence, Int)"` [VERIFIED: OverloadResolver.cs:51].
**Why it happens:** REQUIREMENTS.md describes the BEHAVIOR, not the exact error text format.
**How to avoid:** Pass 2 MUST grep for the error format. Record the Pass-1-vs-Pass-2 mismatch in `## Divergences` with explicit before/after text. This is exactly D-14's pattern and the value-additive output of two-pass strict.
**Warning signs:** Pass-1 skeleton uses natural-language assertion that doesn't match any literal in the codebase.

### Pitfall 7: `(int)(duration * sampleRate)` Truncation for VOC-01 Pin
**What goes wrong:** `sing("ah", C4, 2.0)` → `(int)(2.0 * 44100)` = 88200 exactly. But `sing("ah", C4, 1.5)` → `(int)(1.5 * 44100)` = 66150 (no rounding issue at half-second). At `2.001`, `(int)(2.001 * 44100)` = 88244 (89244 would be wrong — `44100 * 0.001 = 44.1` truncated to 44). Off-by-one is POSSIBLE with other values.
**Why it happens:** `FormantSynthesizer.cs:24` uses `(int)` cast which truncates toward zero, not rounds.
**How to avoid:** Pin ONLY the `2.0 → 88200` case (exact). D-18 specifies this value. If additional pins are needed, compute `(int)(duration * 44100)` in C# test code and compare — don't hardcode.
**Warning signs:** Frame-count assertion fails by exactly 1 — duration value has floating-point imprecision that changes truncation.

### Pitfall 8: `sing("na", C4, 2.0)` Returns MORE Than 88200 Samples
**What goes wrong:** A naive "sing returns duration × sample-rate samples" rule fails for consonant-vowel syllables because `SynthesizeSyllable` PREPENDS consonant samples to the vowel with a crossfade [VERIFIED: FormantSynthesizer.cs:113-148]. The total sample count is `consonantNonOverlap + crossfadeSamples + vowelNonOverlap`, not `duration × sampleRate`.
**Why it happens:** The consonant onset is fixed-duration, independent of the `duration` parameter (which controls vowel length).
**How to avoid:** D-18 pin applies ONLY to pure vowels (`"ah"`, `"ee"`, `"eh"`, `"oh"`, `"oo"`). Do not extend the 88200 pin to syllables like `"na"`, `"ta"`, `"sa"`.
**Warning signs:** Test `sing("na", C4, 2.0).Frames == 88200` fails with a value like 89105 (due to consonant onset + crossfade).

### Pitfall 9: TTS Command Default Is `"espeak-ng --stdout"` — Do NOT `RunTts()` in Tests
**What goes wrong:** A Fact calling `TtsHook.RunTts("hello")` would either (a) succeed on a machine with espeak-ng installed (flaky CI) or (b) throw `InvalidOperationException: "TTS command not found: 'espeak-ng'..."` on clean machines.
**Why it happens:** `RunTts` is a subprocess call with a 30-second timeout.
**How to avoid:** Pin VOC-02 via `SetCommand(cmd) + GetCommand() == cmd` round-trip and empty-string validation only. Invoke `RunTts` ONLY in Manual-Only Verifications subsection.
**Warning signs:** Flaky test results based on whether espeak-ng is installed.

## Runtime State Inventory

Not applicable — Phase 13 is a pure documentation / test-authoring phase. No renames, no refactors, no migrations. No stored data, live service config, OS-registered state, secrets, or build artifacts carry the phase's output.

**Nothing found in any category: VERIFIED. All outputs are:**
- New files under `.planning/phases/XX/` (six new VALIDATION.md files)
- New files under `flow-lang.Tests/Unit/Phase*/` and `flow-lang.Tests/Integration/Phase*/`
- Additive edits to `flow-lang.Tests/FlowScriptData.cs` (sentinel entries)
- Frontmatter edit to existing `10-VALIDATION.md`
- Traceability edits to `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/ROADMAP.md`

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10.0 SDK | xUnit test execution (`dotnet test`) | - | - (assumed present — Phase 12 closed with 68/68 green) | - |
| `flow-lang.Tests` project | All Fact authoring | ✓ (verified by Read of csproj) | xUnit.v3 3.2.2 | - |
| `FlowEngineRunner` fixture | Integration Facts | ✓ (verified by Read) | - | - |
| `FlowScriptData.cs` catalog | Sentinel tightening | ✓ (verified) | - | - |
| espeak-ng | VOC-02 `RunTts()` live invocation (ONLY if attempted) | Unknown | - | **Skip `RunTts()` invocation entirely; pin via `SetCommand`/`GetCommand` round-trip only** — matches v1.1-MILESTONE-AUDIT's acceptance that TTS is "Manual-Only" |
| No new NuGet packages | All Phase 13 work | N/A | N/A | D-12 forbids — no FFT, no assertion framework beyond xUnit |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** espeak-ng — fallback is to constrain VOC-02 automation to `SetCommand`/`GetCommand` API surface and mark live TTS invocation as Manual-Only in Phase 10 VALIDATION.md.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 [VERIFIED: csproj] |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Validation.Phase{N}"` |
| Full suite command | `dotnet test flow-sharp.sln` |
| Runtime (current) | ~14 seconds for 68 tests [VERIFIED: 12-VERIFICATION.md line 41] |
| Runtime (post-Phase 13) | ~20-25 seconds estimated (+7 Facts + sentinel re-runs) |

### Phase Requirement → Test Map (Phase 13 itself; TEST-04 only)

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| TEST-04 | Each of 06/07/08/09 VALIDATION.md exists with `nyquist_compliant: true` | structural | `for p in 06 07 08 09; do test -f .planning/phases/${p}*/${p}-VALIDATION.md; done && grep -l "nyquist_compliant: true" .planning/phases/0[6-9]*/0[6-9]-VALIDATION.md \| wc -l` should equal 4 | ❌ — created by Phase 13 |
| TEST-04 | Phase 10 VALIDATION.md promoted to `nyquist_compliant: true` | structural | `grep "nyquist_compliant: true" .planning/phases/10-vocalization/10-VALIDATION.md` | ✓ (exists) — frontmatter edit by 13-05 |
| TEST-04 | REQUIREMENTS.md TEST-04 row marked Complete | structural | `grep "TEST-04.*Complete\|TEST-04.*\[x\]" .planning/REQUIREMENTS.md` | ✓ (exists) — edit by 13-05 |
| TEST-04 | dotnet test suite green post-authoring | integration | `dotnet test flow-sharp.sln` → `Failed: 0` | ✓ (exists) — gate |
| TEST-04 | At least one observable-value pin per phase | structural | Per-phase VALIDATION.md contains at least one `## Observable Invariants` entry pinning error-text or numeric-duration | ❌ — created by Phase 13 |

### Per-Phase Observable Invariants (≥1 per phase, ≥6 total enumerated — far exceeds ROADMAP criterion 3's "at least one per phase")

Each invariant below would fail if the feature under test were removed. Pin type encodes the strategy; test location cites either an existing Theory row + sentinel OR a new Fact file path.

#### Phase 6 Invariants (plan 13-01)

| # | REQ | Pin Type | Assertion | Test Location |
|---|-----|----------|-----------|---------------|
| 6.1 | QOL-01 | error-text | stderr of `FlowEngineRunner(verbose:true).RunSource("use \"@std\"\n(print \"ok\")")` contains literal `"[verbose] Executing"` | NEW: `Integration/Phase06/VerboseFlagTests.cs` |
| 6.2 | FIX-01 | structural | `transpose(| C4 D4 |, 2)` (Int as 2nd arg, not Semitone literal `+2st`) does not raise "No matching overload" | EXISTING Theory: `tests/test_transpose_int.flow` + NEW sentinel `RequiredSentinels["test_transpose_int.flow"] = ["transpose with int: ok", "test_transpose_int: PASSED"]` |
| 6.3 | FIX-02 (gain-nested) | numeric | `section s { gain 0.5 { | C4 D4 E4 F4 | } } Song sg = [s] (getFrames (renderSong sg "sine"))` > 0 — the AUDIT integration gap regression gate | NEW: `Integration/Phase06/SectionGainBareExpressionTests.cs::GainNestedInSection_RendersNonZeroFrames` |
| 6.4 | FIX-03 | error-text | stderr of `tests/test_error_masking.flow` contains `"Function 'nonExistentFunction' not found"` AND exit code is non-zero | EXISTING: `FlowScriptData.ExpectedErrorScripts["test_error_masking.flow"]` [VERIFIED: FlowScriptData.cs:30] |

#### Phase 7 Invariants (plan 13-02)

| # | REQ | Pin Type | Assertion | Test Location |
|---|-----|----------|-----------|---------------|
| 7.1 | DX-01 | structural | `tests/test_comments.flow` parses and executes to completion (line 43 of script; would fail to tokenize if `//` unsupported) | EXISTING Theory: `tests/test_comments.flow` + NEW sentinel `["5", "10", "15", "8"]` |
| 7.2 | DX-02 | numeric | stdout of `tests/test_math.flow` contains exact printed values for `sin(0.0)`, `sqrt(16.0)`, `min(3,7)`, `max(3,7)`, `pi` — values empirically locked in by Pass 2 | EXISTING Theory: `tests/test_math.flow` + NEW sentinels (Pass 2 captures exact strings) |
| 7.3 | DX-03 | structural | Both `writeWav(String, Buffer)` AND `exportWav(Buffer, String)` signatures registered in `BuiltInFunctions.RegisterAudio` — script `(writeWav "p.wav" buf)` AND `(exportWav buf "p.wav")` both succeed | EXISTING Theory: `tests/test_writewav.flow` + NEW sentinels `["PASS: writeWav(String, Buffer) succeeded", "PASS: exportWav(Buffer, String) backwards compat succeeded"]` |
| 7.4 | DX-04 | structural | REPL's AutoImportStandardModules invokes exactly 3 `use` statements (`@std`, `@audio`, `@collections`) | NEW: `Integration/Phase07/RepLAutoImportTests.cs::AutoImportedModulesResolve` — executes the same 3 imports via FlowEngine + verifies `(list 1 2 3)` / `(createSineTone 0.1 440.0 0.3)` / `(print "ok")` all resolve without errors |

#### Phase 8 Invariants (plan 13-03)

| # | REQ | Pin Type | Assertion | Test Location |
|---|-----|----------|-----------|---------------|
| 8.1 | AUDIO-05 | numeric (sample math) | `AudioCore.Mix({buf:[0.5]},{buf:[0.3]}).Data[0] == 0.8f` — proves additive semantics, not overwrite or average | NEW: `Unit/Phase08/MixTests.cs::Mix_SumsSamples_AdditiveSemantics` (pure C# API) |
| 8.2 | AUDIO-05 | numeric (frame count) | `mix(createSineTone 0.5 440.0 0.5, createSineTone 0.5 880.0 0.5)` returns buffer with 22050 frames (max of both) | EXISTING Theory: `tests/test_mix.flow` + NEW sentinel `["mix frames: 22050"]` |
| 8.3 | AUDIO-06 | **cross-ref to 6.3** | Gain-nested bare-expression renders non-zero frames (AUDIO-06 × FIX-02 intersection) | EXISTING Integration Fact 6.3 covers AUDIO-06 too — VALIDATION.md cross-refs |
| 8.4 | AUDIO-07 | structural | `SynthesizerFactory.Create("strings")` returns `StringsSynthesizer`, `"organ"` → `OrganSynthesizer`, `"bell"` → `BellSynthesizer` | NEW: `Unit/Phase08/SynthesizerFactoryTests.cs::Create_SupportsStringsOrganBell` (pure C# API, calls `NoteSynthesizer.cs:231-233` switch) |

#### Phase 9 Invariants (plan 13-04)

| # | REQ | Pin Type | Assertion | Test Location |
|---|-----|----------|-----------|---------------|
| 9.1 | AUDIO-08 | structural (boolean) | `tempoRamp(seq, 120.0, 80.0)` produces more frames than `tempoRamp(seq, 120.0, 120.0)` (ritardando ≠ constant); accelerando inverse | EXISTING Theory: `tests/test_tempo_ramp.flow` + NEW sentinels for all three test-N-true strings |
| 9.2 | QOL-02 | structural (exit code) | `examples/tutorial.flow` runs under `FlowEngineRunner.RunFile` with `errorCount == 0` and produces a WAV file (tutorial writes output) | NEW: `Integration/Phase09/TutorialTests.cs::TutorialRunsToCompletion` |

#### Phase 10 Invariants (plan 13-05)

| # | REQ | Pin Type | Assertion | Test Location |
|---|-----|----------|-----------|---------------|
| 10.1 | VOC-01 | numeric (sample count) | `FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0).Frames == 88200` (D-18 canonical pin) | NEW: `Unit/Phase10/FormantSynthesizerTests.cs::SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames` |
| 10.2 | VOC-01 | error-text | `FormantData.GetFormants("xyz")` throws `ArgumentException` with message `"Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo"` | NEW: `Unit/Phase10/FormantDataTests.cs::GetFormants_UnknownVowel_ThrowsArgumentException` |
| 10.3 | VOC-02 | structural (round-trip) | `TtsHook.SetCommand("echo"); TtsHook.GetCommand() == "echo"` | NEW: `Unit/Phase10/TtsHookTests.cs::SetCommand_RoundTrips_ViaGetCommand` |
| 10.4 | VOC-02 | error-text | `TtsHook.SetCommand("")` throws `ArgumentException` matching `"TTS command cannot be null or whitespace"` | NEW: `Unit/Phase10/TtsHookTests.cs::SetCommand_Empty_ThrowsArgumentException` |

**Total observable-value pins across all 5 plans: 16** (≥1 per phase per ROADMAP criterion 3 — easily satisfied).

### Sampling Rate

- **Per task commit:** `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase{N}"` (~3-5s scoped)
- **Per wave merge:** `dotnet test flow-sharp.sln` (~20-25s estimated)
- **Phase gate:** Full suite green + presence-check of each created VALIDATION.md before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Integration/` — NEW directory (first plan that needs it creates it; 13-01 does)
- [ ] `flow-lang.Tests/Unit/Phase08/`, `Unit/Phase10/` — NEW subdirectories (plans 13-03, 13-05)
- [ ] `flow-lang.Tests/Integration/Phase06/`, `Integration/Phase07/`, `Integration/Phase09/` — NEW subdirectories (plans 13-01, 13-02, 13-04)
- [ ] No framework install needed — xUnit already present
- [ ] No conftest/shared fixture needed — `FlowEngineRunner` already covers everything

## Security Domain

> Phase 13 is pure documentation / test authoring. No user input processing, no authentication, no cryptography, no network I/O, no file-system writes outside test output dirs.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A |
| V5 Input Validation | no (new test inputs only, internally controlled) | N/A |
| V6 Cryptography | no | N/A |
| V12 File and Resources | minimal | WAV output from tutorial / `sing` tests writes to `/tmp/` and `tests/output/` — directories already in use, no new attack surface |

### Known Threat Patterns for xUnit test authorship

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Subprocess injection via `RunTts("malicious; rm -rf /")` | Tampering | **DO NOT invoke `RunTts` from automated tests** (Pitfall 9). Mock via `SetCommand("echo")` round-trip only. |
| WAV output paths collide between parallel tests | N/A (test flakiness, not security) | Use `Path.GetTempFileName()` or `/tmp/test_phase13_XX_` prefix; never overwrite tracked files |
| Test pollutes global `TtsHook._ttsCommand` static field | Tampering | `TtsHook` exposes `SetCommand` but has no Reset — tests SHOULD reset to default `"espeak-ng --stdout"` in cleanup, OR run with `[Collection("FlowScripts")]` serialization to avoid cross-test state bleed |

No security-sensitive code paths are authored by Phase 13.

## Code Examples

Verified patterns from existing codebase (references are cited).

### Integration Fact with Literal Flow Script (Pattern 3)

```csharp
// Source: flow-lang.Tests/Unit/InterpreterTests.cs:18-36 (production reference)
using Xunit;
using FlowLang.Tests.Fixtures;

namespace FlowLang.Tests.Integration.Phase06;

[Collection("FlowScripts")]  // serialize Console.SetOut (Pitfall 3)
public class SectionGainBareExpressionTests
{
    [Fact]
    public void GainNestedInSection_RendersNonZeroFrames()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
section s { gain 0.5 { | C4 D4 E4 F4 | } }
Song sg = [s]
Buffer b = (renderSong sg ""sine"")
Int frames = (getFrames b)
(print $""frames: {(str frames)}"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        // Pre-fix (before commit 2156690): "frames: 0" — bug manifested silently.
        // Post-fix: frames > 0.
        Assert.DoesNotContain("frames: 0\n", stdout);
        Assert.Contains("frames:", stdout);
    }
}
```

### Unit Fact for Pure C# API (VOC-01 pin, D-18 canonical)

```csharp
// Source: flow-lang/StandardLibrary/Audio/Vocalization/FormantSynthesizer.cs:22-81
using Xunit;
using FlowLang.StandardLibrary.Audio.Vocalization;

namespace FlowLang.Tests.Unit.Phase10;

public class FormantSynthesizerTests
{
    [Fact]
    public void SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames()
    {
        // D-18 canonical pin: 2.0s × 44100Hz = 88200 samples.
        // 261.63Hz is C4 per 12-tone equal temperament (PitchConversion).
        var buffer = FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0);
        Assert.Equal(88200, buffer.Frames);
        Assert.Equal(1, buffer.Channels);  // mono per FormantSynthesizer.cs:43
        Assert.Equal(44100, buffer.SampleRate);
    }

    [Theory]
    [InlineData("ah")]
    [InlineData("ee")]
    [InlineData("eh")]
    [InlineData("oh")]
    [InlineData("oo")]
    public void SynthesizeVowel_AllFiveVowels_ProduceNonSilentOutput(string vowel)
    {
        var buffer = FormantSynthesizer.SynthesizeVowel(vowel, 261.63, 0.5);
        Assert.Equal(22050, buffer.Frames);
        // Non-silent: at least one sample > 0.01 in absolute value (RMS-adjacent heuristic)
        bool hasAudibleSample = false;
        foreach (var s in buffer.Data)
            if (Math.Abs(s) > 0.01f) { hasAudibleSample = true; break; }
        Assert.True(hasAudibleSample, $"Vowel '{vowel}' produced near-silent buffer");
    }
}
```

### Tightened Sentinel in FlowScriptData.cs (Pattern 2)

```csharp
// Source: flow-lang.Tests/FlowScriptData.cs:60-71 (extend existing dictionary)
public static readonly Dictionary<string, string[]> RequiredSentinels = new()
{
    // ... existing spike/c1 entry (12-04) ...
    [Path.Combine("spike", "c1-musical-context-body.flow")] = new[]
    {
        "c1-probe1-body-ran",
        "c1-probe2-stmt1",
        "c1-probe2-stmt2",
        "c1-probe3-body-ran",
    },

    // Phase 13-04 (AUDIO-08): pin the ritardando/accelerando boolean outputs.
    ["test_tempo_ramp.flow"] = new[]
    {
        "Test 1 - tempoRamp produces non-zero buffer: true",
        "Test 2 - Ritardando produces more frames than constant fast: true",
        "Test 3 - Accelerando produces fewer frames than constant slow: true",
    },

    // Phase 13-03 (AUDIO-05): pin mix frame count (0.5s × 44100 = 22050).
    ["test_mix.flow"] = new[]
    {
        "mix frames: 22050",
        "mix channels: 1",
        "piped mix frames: 22050",
    },

    // Phase 13-01 (FIX-01): pin transpose-with-int success sentinel.
    ["test_transpose_int.flow"] = new[]
    {
        "transpose with int: ok",
        "transpose with semitone: ok",
        "test_transpose_int: PASSED",
    },
};
```

### Error-Text Pin via ExpectedErrorScripts (Pattern referenced)

```csharp
// EXISTING — no new work for Phase 6 FIX-03.
// Source: flow-lang.Tests/FlowScriptData.cs:30
public static readonly Dictionary<string, string> ExpectedErrorScripts = new()
{
    // ... entries ...
    ["test_error_masking.flow"] = "Function 'nonExistentFunction' not found",
    // ^^ this is FIX-03's observable-value pin, already shipped by Phase 12.
};
```

### VALIDATION.md Frontmatter for Promotion (Pattern for 13-05)

```yaml
---
phase: 10
slug: vocalization
status: passed
nyquist_compliant: true    # <-- the promotion (was false)
wave_0_complete: true      # <-- also promoted (was false)
created: 2026-04-03
promoted: 2026-04-19       # <-- NEW line added by 13-05
---
```

Everything else in the file body stays — the Manual-Only Verifications subsection for perceptual quality and espeak-ng live invocation remains.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| VERIFICATION.md (goal-backward, post-hoc) | VALIDATION.md (requirements-first, pre-execution pins) | GSD workflow evolution through Phases 6-12 | v1.1 shipped with no VALIDATION.md; Phase 10 draft was first attempt; Phase 13 retroactively backfills for 6-9 |
| `tests/test_*.flow` with `(print "PASSED")` as success marker | xUnit Theory + `RequiredSentinels` numeric-value pins | Phase 12 Plan 12-01 introduced the Theory harness | Existing scripts still run; Phase 13 layers tightened sentinels on top |
| Buffer byte-hash pinning (rejected) | Zero-crossing count + frame-count + error-text pinning | D-11 / D-12 | Survives any DSP refactor that preserves sample-rate + duration semantics |
| Separate rollup plan per phase closure | Closing plan (13-05) carries traceability updates | Phase 11/12 pattern (11-06, 12-06) | Phase 13 follows; 13-05 is leaner because TEST-04 closure is 1-line REQUIREMENTS.md edit |

**Deprecated/outdated:**
- "No unit test framework" assertion in CLAUDE.md — OUTDATED since Phase 12 Plan 12-01 introduced `flow-lang.Tests/` (xUnit). Phase 13 should NOT edit CLAUDE.md per D-21 / deferred-ideas list; next DX pass handles it.
- `net9.0` target in CLAUDE.md — OUTDATED. Actual target is `net10.0` [VERIFIED: csproj:3]. Same deferred note.
- `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` command in CLAUDE.md — still works but `dotnet test flow-sharp.sln` is canonical. Same deferred note.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `examples/tutorial.flow` currently runs to completion under `dotnet run --project flow-interpreter examples/tutorial.flow` | Existing Coverage Map (QOL-02 row) | If broken, plan 13-04 discovers a regression during Pass 2 that Phase 16 was supposed to handle. Mitigation: Pass 2 MUST empirically run it; if broken, flag as `## Ultra-Important Finding` in 09-VALIDATION.md and log as deferred-to-Phase-16 rather than block Phase 13. [ASSUMED — v1.1-MILESTONE-AUDIT.md line 105 asserted working as of 2026-04-18 but intervening stability fixes could have changed surface] |
| A2 | `(int)(2.0 * 44100) == 88200` exactly across all .NET runtime versions | Pitfall 7 + VOC-01 pin 10.1 | IEEE-754 guarantees exact representation of 2.0 and 44100; 88200 is well within int range. Very low risk. [CITED: IEEE-754 spec + .NET documentation; VERIFIED: trivially computable] |
| A3 | `PitchConversion.NoteToFrequency('C', 4, 0)` returns ~261.63Hz | VOC-01 pin code example | Standard 12-TET reference; might return slightly different value depending on A4 reference (440Hz vs 432Hz). Check `PitchConversion.cs` during Pass 2. If it returns, say, 261.6255653006 instead of 261.63, the `Math.Abs(s) > 0.01f` check still passes; only frame count matters. [ASSUMED — Pass 2 can verify trivially] |
| A4 | `FlowEngineRunner(verbose: true).RunSource("use \"@std\"\n(print \"ok\")")` writes `[verbose] Executing <test>` to stderr (not stdout) | Phase 6 invariant 6.1 | `FlowEngine.cs:42` sets `_diagnosticOutput = verbose ? Console.Error : null` — confirmed routes to stderr. [VERIFIED: FlowEngine.cs:42] |
| A5 | The `FlowEngineRunner` constructor's `verbose: true` argument threads correctly through `new FlowEngine(verbose: verbose)` | Phase 6 invariant 6.1 | [VERIFIED: FlowEngineRunner.cs:13, 19 — constructor signature matches] |
| A6 | `tests/test_tempo_ramp.flow` stdout format `"Test 1 - tempoRamp produces non-zero buffer: true"` matches the exact concat output | Phase 9 invariant 9.1 sentinels | `(print (concat "Test 1 - tempoRamp produces non-zero buffer: " (str test1)))` per file read. `str` of `Bool` → `"true"`/`"false"`. [VERIFIED: test_tempo_ramp.flow head read] |
| A7 | `SynthesizerFactory.Create` is publicly accessible from test assembly | Phase 8 invariant 8.4 | `NoteSynthesizer.cs:201 public static class SynthesizerFactory` — accessible. [VERIFIED: grep] |
| A8 | `AudioCore.Mix` is publicly accessible from test assembly | Phase 8 invariant 8.1 | `AudioCore.cs:170 public static Value Mix` — accessible. `Value.As<AudioBuffer>()` exists. [VERIFIED: AudioCore.cs:170-172] |
| A9 | `TtsHook.GetCommand()` and `TtsHook.SetCommand()` are public statics | Phase 10 invariants 10.3, 10.4 | [VERIFIED: TtsHook.cs:17, 28 — both `public static`] |
| A10 | `FormantData.GetFormants` is public and throws `ArgumentException` with exact message shown | Phase 10 invariant 10.2 | [VERIFIED: FormantData.cs:69-76] |

**Flagged for user confirmation:** None. All assumptions are either VERIFIED against source or trivially empirical (Pass 2 can confirm in seconds).

## Project Constraints (from CLAUDE.md)

- **.NET 9 / .NET 10 discrepancy:** CLAUDE.md states `net9.0`; actual target is `net10.0` [VERIFIED: flow-lang.Tests.csproj:3]. Phase 13 uses `net10.0`; do NOT edit CLAUDE.md (deferred per CONTEXT).
- **Minimal dependencies:** D-12 already restates this. No new NuGet packages from Phase 13.
- **PulseAudio on Linux:** No playback invoked by Phase 13 tests — all tests are deterministic (no audio-backend dependency).
- **Performance:** Phase 13 adds ~7 new Facts, ~9 tightened sentinels. Estimated runtime delta +5-10 seconds (tutorial.flow is ~1s, formant synthesis tests ~0.5s each, rest are near-instant C# API calls).
- **Compatibility:** ALL existing .flow scripts + ALL existing tests must remain GREEN after Phase 13. D-21 forbids modifying existing tests; tightened sentinels are ADDITIVE — they require extra stdout content, not different content.
- **GSD Workflow Enforcement (CLAUDE.md):** Phase 13 is executed via `/gsd:execute-phase` — no direct edits outside the workflow.
- **Language Philosophy (user memory):** Keep functional S-expression style, no infix operators, Haskell-inspired — no impact on Phase 13 (pure documentation authoring, no Flow language edits).

## Sources

### Primary (HIGH confidence)

- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md` — 24 user decisions (D-01 through D-24), full scope definition
- `.planning/ROADMAP.md` Phase 13 (3 success criteria)
- `.planning/milestones/v1.1-REQUIREMENTS.md` — the 16 requirements being validated (15 Complete, 1 Invalid)
- `.planning/milestones/v1.1-MILESTONE-AUDIT.md` — aggregate audit verdict, integration gap identification
- `.planning/REQUIREMENTS.md` — TEST-04 current entry (line 44)
- `~/.claude/get-shit-done/templates/VALIDATION.md` — canonical schema
- `.planning/phases/12-stability/12-VALIDATION.md` — format reference (all sections filled)
- `.planning/phases/12-stability/12-VERIFICATION.md` — `## Empirical Overrides` pattern reference (lines 167-171)
- `.planning/phases/10-vocalization/10-VALIDATION.md` — promotion target (existing draft)
- `flow-lang.Tests/flow-lang.Tests.csproj` — xUnit.v3 3.2.2, net10.0, no new packages needed
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — in-process FlowEngine runner (complete API)
- `flow-lang.Tests/FlowScriptData.cs` — Theory catalog + ExpectedErrorScripts + RequiredSentinels
- `flow-lang.Tests/FlowScriptTests.cs` — Theory harness + CWD pivot pattern
- `flow-lang.Tests/Unit/InterpreterTests.cs` — literal-script Fact pattern reference
- `flow-lang.Tests/Unit/CollectionsTests.cs` — pure C# API Fact pattern reference
- `flow-lang/Core/FlowEngine.cs` — verbose flag implementation (line 42, 81)
- `flow-lang/StandardLibrary/Audio/Vocalization/FormantSynthesizer.cs` — sample-count formula (line 24)
- `flow-lang/StandardLibrary/Audio/Vocalization/FormantData.cs` — unknown-vowel exception (line 74-75)
- `flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs` — Set/GetCommand public API + empty-command validation
- `flow-lang/StandardLibrary/Audio/AudioCore.cs` — Mix additive semantics (line 196-201)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — SynthesizerFactory switch (line 231-233)
- `flow-lang/TypeSystem/OverloadResolver.cs` — "No matching overload" error format (line 51)
- `flow-lang/Runtime/ExecutionContext.cs` — "Function 'X' not found" error format (line 168)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:445-455` — writeWav + exportWav registration
- `flow-lang/Diagnostics/ErrorReporter.cs` — error vs warning distinction
- `flow-interpreter/Repl.cs:84-99` — AutoImportStandardModules implementation
- `tests/test_*.flow` — all referenced files read during coverage audit
- `examples/tutorial.flow` — 348 lines, confirmed present

### Secondary (MEDIUM confidence)

- `.planning/phases/06-*/SUMMARY.md` through `10-*/SUMMARY.md` headers (Pass-2 read inputs per D-13)
- `.planning/phases/12-stability/12-VERIFICATION.md` Key Discrepancy Notes (line 167-171 — Divergence pattern)
- `.planning/STATE.md` Decisions section — contextual background on Phase 12 decisions

### Tertiary (LOW confidence)

- None — all claims verified against source files or canonical planning docs.

## Open Questions

None blocking. Two Pass-2 empirical discoveries expected:

1. **Exact numeric format of `(print (str pi))`** — must be captured empirically by Pass 2 when tightening `test_math.flow` sentinels. Recommendation: Pass 2 runs the script once, copies the literal stdout values into the sentinel array.

2. **Whether `examples/tutorial.flow` currently runs GREEN** — Pass 2 of plan 13-04 MUST verify by running it. If it errors, do NOT fix it (scope of Phase 16); document as `## Ultra-Important Finding` + Manual-Only entry in 09-VALIDATION.md with a pointer to Phase 16.

Both are mechanical checks, not research questions.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all test infrastructure verified by reading csproj, fixtures, and existing Unit/ tests
- Architecture (two-pass / patterns): HIGH — derived from CONTEXT.md decisions + Phase 11/12 precedent
- Existing Coverage Map: HIGH — every cell in the table verified by reading the cited .flow script and/or source file
- Observable Invariants: HIGH — every pin's assertion text derives from a VERIFIED source file quote or documented runtime behavior
- Pitfalls: HIGH — each pitfall has a specific source file reference (several are learned from Phase 12 deviations in STATE.md)
- Environment Availability: MEDIUM — .NET SDK assumed present (Phase 12 closed green 2026-04-19, so assumption is safe for the next 24h at least)

**Research date:** 2026-04-19
**Valid until:** 2026-05-03 (14 days — stable because backing infrastructure was just stabilized by Phase 12, and no external deps are involved)

---

## RESEARCH COMPLETE

**Phase:** 13 — Nyquist Validation Backfill
**Confidence:** HIGH

### Key Findings

1. **Zero new NuGet packages required** (D-12 satisfiable). Phase 12's xUnit 2.9.3 + FlowEngineRunner + Theory harness cover every primitive Phase 13 needs.
2. **Every v1.1 requirement has SOME existing coverage** via `tests/test_*.flow` Theory rows, but **most are PARTIAL** — scripts print `"PASSED"` regardless of underlying behavior. The phase's value-add is tightening observable-value pins via `RequiredSentinels` entries + 7 new targeted xUnit Facts.
3. **The FIX-02 × AUDIO-06 integration gap** (`section { gain N { | notes | } }` → 0 frames pre-fix) is the ONLY v1.1 regression that requires a genuinely new Integration Fact. `test_section_gain_bare_expr.flow` exists but its always-`PASSED` output makes it unreliable as a regression gate. The new Fact would have caught the original bug.
4. **D-18's VOC-01 pin (88200 samples for `sing("ah", C4, 2.0)`) is exact** — `(int)(2.0 * 44100) == 88200` in IEEE-754. No zero-crossing heuristics needed for the primary pin; those are over-engineering.
5. **Two-task-per-plan structure (Pattern 1) operationalizes D-13 two-pass strict** — Task 1's `<read_first>` + `forbidden_reads` YAML fields enforce the REQUIREMENTS-only constraint mechanically, preserving the confirmation-bias-catching property that caught C5 in Phase 11 and TEST-01/02 in Phase 12.
6. **Phase 10 promotion is safe** — all four Phase 10 invariants (VOC-01 frame count, VOC-01 unknown-vowel exception, VOC-02 SetCommand round-trip, VOC-02 empty-command exception) are automatable without espeak-ng or subprocess invocation.

### File Created

`.planning/phases/13-nyquist-validation-backfill/13-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | csproj/fixtures read directly |
| Architecture / Two-Pass Pattern | HIGH | CONTEXT.md D-13 + Phase 11/12 precedent documented in STATE.md |
| Existing Coverage Map | HIGH | Every test file read and cited verbatim |
| Observable Invariants | HIGH | Every pin's source quote VERIFIED |
| Pitfalls | HIGH | Each has specific source-line citation |

### Open Questions

None blocking. Two mechanical Pass-2 empirical checks noted (exact `pi` print format; tutorial.flow current GREEN status) — neither is a research question, both are trivially discoverable in execution.

### Ready for Planning

Research complete. Planner can now create five `13-XX-PLAN.md` files using:
- Pattern 1 (two-task-per-plan) for authorship structure
- Pattern 2 (tightened sentinels) as the primary observable-value pinning technique
- Pattern 3 (Integration Fact via FlowEngineRunner) for cases where sentinel tightening is insufficient
- The Existing Coverage Map table to decide per-REQ whether the plan authors a new Fact or cites an existing Theory row
- The Per-Phase Observable Invariants enumeration (16 total pins) as the Pass-1 skeleton seed

**Recommended plan slugs:**
- `13-01-PLAN.md` → Phase 6 VALIDATION.md + 2 new Integration Facts + 2 sentinel additions
- `13-02-PLAN.md` → Phase 7 VALIDATION.md + 1 new Integration Fact + 3 sentinel additions
- `13-03-PLAN.md` → Phase 8 VALIDATION.md + 2 new Unit Facts + 3 sentinel additions
- `13-04-PLAN.md` → Phase 9 VALIDATION.md + 1 new Integration Fact + 1 sentinel addition
- `13-05-PLAN.md` → Phase 10 VALIDATION.md promotion + 4 new Unit Facts + 1 sentinel addition + REQUIREMENTS.md/STATE.md/ROADMAP.md TEST-04 closure

All five land in Wave 1 parallel (distinct phase directories, additive test-file additions, zero file overlap per D-06).
