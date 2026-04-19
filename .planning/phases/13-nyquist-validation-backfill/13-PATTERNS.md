# Phase 13: Nyquist Validation Backfill — Pattern Map

**Mapped:** 2026-04-19
**Files analyzed:** 18 (5 new VALIDATION.md, 1 promoted VALIDATION.md, 7 new xUnit Fact files, 1 modified `FlowScriptData.cs`, 3 traceability files, 1 optional shared helper)
**Analogs found:** 17 / 18 (1 greenfield: `Integration/` subdirectory + optional `AudioTestHelpers.cs`)

Phase 13 is pure docs + test-backfill. **No production code under `flow-lang/` is modified.** All production files are READ-ONLY inputs. Analogs come almost entirely from Phase 12.

---

## Project-Convention Corrections (apply verbatim to every plan)

CLAUDE.md is stale; RESEARCH.md `§Project Constraints` documents the actual conventions. Plans MUST use:

| Convention | CLAUDE.md says | Reality (from csproj + sln) |
|-----------|----------------|-----------------------------|
| .NET target | `net9.0` | **`net10.0`** [VERIFIED: `flow-lang.Tests/flow-lang.Tests.csproj:3`] |
| Solution file | `flow-lang.sln` | **`flow-sharp.sln`** |
| Test runner | "No unit test framework" | **xUnit.v3 3.2.2** [VERIFIED: csproj:13] |
| Full-suite command | `for test in tests/test_*.flow; do ...` | **`dotnet test flow-sharp.sln`** |

Do NOT edit CLAUDE.md in Phase 13 (deferred per CONTEXT `<deferred>` + D-21).

Other invariant conventions:
- **File-scoped namespaces** — `namespace FlowLang.Tests.Unit;` (line 4 in `Unit/CollectionsTests.cs`)
- **Namespace roots:** `FlowLang.*` (library), `FlowLang.Tests.*` (test project), `FlowInterpreter` (console app)
- **Record types** for AST nodes; plain classes for test fixtures
- **`using Xunit;`** + **`[Fact]` / `[Theory]` + `[InlineData(...)]`** — the only attributes used
- **`Assert.Contains / Assert.DoesNotContain / Assert.Equal / Assert.True / Assert.Throws<T>`** — keep to xUnit's built-in assertions; no FluentAssertions
- **`[Collection("FlowScripts")]`** required on every class using `FlowEngineRunner` (Pitfall 3, Console.SetOut serialization)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `.planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` | docs (VALIDATION.md) | request-response (docs → planner) | `.planning/phases/12-stability/12-VALIDATION.md` | exact |
| `.planning/phases/07-developer-experience/07-VALIDATION.md` | docs (VALIDATION.md) | request-response | `.planning/phases/12-stability/12-VALIDATION.md` | exact |
| `.planning/phases/08-audio-production/08-VALIDATION.md` | docs (VALIDATION.md) | request-response | `.planning/phases/12-stability/12-VALIDATION.md` | exact |
| `.planning/phases/09-advanced-features/09-VALIDATION.md` | docs (VALIDATION.md) | request-response | `.planning/phases/12-stability/12-VALIDATION.md` | exact |
| `.planning/phases/10-vocalization/10-VALIDATION.md` | docs (frontmatter promotion) | transform (file-in-place) | existing `10-VALIDATION.md` draft + `12-VALIDATION.md` for new sections | exact |
| `flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs` | test (xUnit Fact, integration) | request-response (FlowEngine → stdout/stderr) | `flow-lang.Tests/Unit/InterpreterTests.cs` | exact (role+flow) |
| `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` | test (xUnit Fact, integration) | request-response | `flow-lang.Tests/Unit/InterpreterTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` | test (xUnit Fact, integration) | request-response | `flow-lang.Tests/Unit/InterpreterTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase08/MixTests.cs` | test (xUnit Fact, unit) | CRUD (pure C# API) | `flow-lang.Tests/Unit/CollectionsTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase08/SynthesizerFactoryTests.cs` | test (xUnit Fact, unit) | CRUD | `flow-lang.Tests/Unit/CollectionsTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase09/TutorialTests.cs` | test (xUnit Fact, integration) | request-response (FlowEngine.RunFile) | `flow-lang.Tests/FlowScriptTests.cs` (CWD pivot) + `Unit/InterpreterTests.cs` (class layout) | exact |
| `flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs` | test (xUnit Fact, unit) | CRUD | `flow-lang.Tests/Unit/CollectionsTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` | test (xUnit Fact, unit) | CRUD (exception assertion) | `flow-lang.Tests/Unit/CollectionsTests.cs` (`Assert.Throws<InvalidOperationException>` pattern) | exact |
| `flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` | test (xUnit Fact, unit) | CRUD (round-trip + validation) | `flow-lang.Tests/Unit/CollectionsTests.cs` | exact |
| `flow-lang.Tests/FlowScriptData.cs` (MODIFIED) | data (sentinel catalog) | transform (dict entries) | file itself (pre-existing `RequiredSentinels` dict at line 60-71) | exact — self-analog |
| `.planning/REQUIREMENTS.md` (MODIFIED) | docs (traceability) | transform (line edit) | TEST-03 closure pattern at line 92 (Phase 12) | exact |
| `.planning/STATE.md` (MODIFIED) | docs (phase pointer) | transform | phase-completion workflow (not plan-level) | N/A |
| `.planning/ROADMAP.md` (MODIFIED) | docs (progress table) | transform | phase-completion workflow (not plan-level) | N/A |
| `flow-lang.Tests/Integration/AudioTestHelpers.cs` (OPTIONAL) | shared helper | transform (buffer → zero-crossing count) | **no analog** — greenfield | none (skip if not needed) |

**Plan isolation (Wave 1 parallel, zero file overlap):**

| Plan | Phase directory written | Fact files created | `FlowScriptData.cs` edit |
|------|-------------------------|--------------------|--------------------------|
| 13-01 | `phases/06-*/` | `Integration/Phase06/` | adds `test_transpose_int.flow` entry |
| 13-02 | `phases/07-*/` | `Integration/Phase07/` | adds `test_comments.flow`, `test_math.flow`, `test_writewav.flow` entries |
| 13-03 | `phases/08-*/` | `Unit/Phase08/` | adds `test_mix.flow`, `test_gain_context.flow`, `test_synth_presets.flow` entries |
| 13-04 | `phases/09-*/` | `Integration/Phase09/` | adds `test_tempo_ramp.flow` entry |
| 13-05 | `phases/10-*/` (frontmatter promote) + `.planning/REQUIREMENTS.md` + `.planning/STATE.md` + `.planning/ROADMAP.md` | `Unit/Phase10/` | adds `test_vocalization.flow` entry |

The only shared file is `flow-lang.Tests/FlowScriptData.cs` — each plan appends a **distinct dictionary key** to `RequiredSentinels`. Since dictionary-literal entries are independent line ranges, any merge-conflict resolution is additive (append all). **CONFIRMED: no plan-pair writes to the same entry key.**

---

## Pattern Assignments

### `.planning/phases/{06,07,08,09}-*/XX-VALIDATION.md` (docs, creation)

**Analog:** `.planning/phases/12-stability/12-VALIDATION.md`

**Frontmatter pattern** (lines 1-8 of `12-VALIDATION.md`):
```yaml
---
phase: 12
slug: stability
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---
```

For Phase 13's new files, the frontmatter MUST land with `nyquist_compliant: true` and `wave_0_complete: true` at creation time (since Phase 13 authors the file to already satisfy the criteria — there is no separate "Wave 0" for a docs-only phase; see 13-CONTEXT D-22/D-24). Example for `06-VALIDATION.md`:

```yaml
---
phase: 6
slug: diagnostics-bug-fixes
status: passed
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-19
backfilled: true    # explicit flag: authored retroactively under TEST-04
---
```

**Section structure to copy** (12-VALIDATION.md lines 10-98 in order):
1. `# Phase N — Validation Strategy` + one-line intro
2. `## Test Infrastructure` table (Framework / Config file / Quick run command / Full suite command / Estimated runtime)
3. `## Sampling Rate` (4 bullets: per-commit, per-wave, pre-verify, max latency)
4. `## Per-Task Verification Map` table (Task ID / Plan / Wave / Requirement / Threat Ref / Secure Behavior / Test Type / Automated Command / File Exists / Status)
5. `## Wave 0 Requirements` (checkbox list)
6. `## Manual-Only Verifications` (table: Behavior / Requirement / Why Manual / Test Instructions)
7. `## Observable Invariants` (numbered list — each a concrete check that would fail if feature removed)
8. `## Validation Sign-Off` (6-item checkbox list ending with `Approval: pending` or filled date)

**`Test Infrastructure` table pattern** (12-VALIDATION.md lines 18-24) — the cells must be updated to reflect Phase 13's post-facto reality:

```markdown
| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase{N}"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~20 seconds full suite |
```

**`Observable Invariants` pattern** (12-VALIDATION.md lines 71-86):
```markdown
## Observable Invariants (from RESEARCH.md Validation Architecture)

Each invariant is a concrete check that would fail if the fix were removed:

1. **FIX-05:** `init([])` call produces stderr containing `"Cannot get init of empty array"` (matches head/last format)
2. **FIX-06:** Calling `Thunk.Force()` twice on a throwing evaluator returns the same `ExceptionDispatchInfo`-captured exception on both calls with original stack trace preserved
...
```

For Phase 13, each phase's `Observable Invariants` section is populated verbatim from 13-RESEARCH.md `§Per-Phase Observable Invariants` (lines 516-557), one numbered entry per row of the per-phase table (4 for Phase 6, 4 for Phase 7, 4 for Phase 8, 2 for Phase 9, 4 for Phase 10).

**`Manual-Only Verifications` pattern** (12-VALIDATION.md lines 62-67) — use this table shape:
```markdown
| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| v1.1 soft-failure semantics preserved end-to-end | FIX-07 | Covered by automated tests AND human-readable | `dotnet run --project flow-interpreter tests/test_musical_context_errors.flow` |
```

For Phases 6-9 most rows will be `"All phase behaviors have automated verification."` (per template instruction at line 63). Phase 10 is the exception — it keeps rows for perceptual vowel recognizability + espeak-ng live invocation (the two items in the current draft at lines 62-65 of `10-VALIDATION.md`).

**Pass-1/Pass-2 section add-on** (NEW for Phase 13; not in 12-VALIDATION.md but mandated by CONTEXT D-13, D-14, D-15):

After `## Observable Invariants`, append:

```markdown
## Pass 1 Draft (Requirements-First)

*Authored by reading ONLY `.planning/milestones/v1.1-REQUIREMENTS.md` + ROADMAP.md phase goal.
Source code NOT consulted.*

For each requirement: the assertion text the author expected to write.

- **REQ-ID:** expected assertion
...

## Pass 2 Implementation Map

*Authored after empirical verification against source / existing tests.*

- **REQ-ID:** actual assertion text shipped; link to Fact file or Theory row + sentinel

## Divergences

*Record of Pass-1-vs-Pass-2 mismatches. Mirrors `## Empirical Overrides`/`## Key Discrepancy Notes`
in `12-VERIFICATION.md` (lines 167-171).*

- **REQ-ID:** Pass 1 drafted "{expected text}"; Pass 2 found "{actual text}" at {source file:line}.
  {Which is correct / defer to future phase.}

*If Pass 1 and Pass 2 agree throughout: "No divergences — requirement-as-written is
literally testable."*
```

The Divergence format comes from Phase 12's `12-VERIFICATION.md:167-171`:
> One nuance worth documenting: the plan 12-06 rollup claims "..." When run directly via `dotnet run`, this script **exits 1** because... However, this is correct behavior — ...

---

### `.planning/phases/10-vocalization/10-VALIDATION.md` (docs, MODIFY — promotion)

**Analog:** the existing draft itself at this path.

**Frontmatter edit pattern** (13-RESEARCH.md `§Code Examples` lines 734-746):

```yaml
---
phase: 10
slug: vocalization
status: passed              # was: draft
nyquist_compliant: true     # was: false  ← THE PROMOTION
wave_0_complete: true       # was: false  ← also promoted
created: 2026-04-03
promoted: 2026-04-19        # NEW line added by 13-05
---
```

**Body change pattern:** preserve existing Body + `## Manual-Only Verifications` table (lines 62-65 keep both entries — formant quality + TTS external command), then APPEND the `## Observable Invariants`, `## Pass 1 Draft`, `## Pass 2 Implementation Map`, and `## Divergences` sections following the template above.

Do NOT rewrite or re-order the existing Per-Task Verification Map (lines 39-44). The original per-task rows are kept as historical record; the new Observable Invariants section is what upgrades the file to `nyquist_compliant: true`.

---

### `flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs` (test, integration — new)

**Analog:** `flow-lang.Tests/Unit/InterpreterTests.cs` (same namespace root swap: `Unit` → `Integration.Phase06`)

**Full file template** (copy lines 1-16 of InterpreterTests.cs, changing class + fact):

```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase06;

/// <summary>
/// QOL-01 regression test: the --verbose flag MUST cause FlowEngine to emit
/// "[verbose] Executing ..." diagnostic lines to stderr. Pre-QOL-01 the flag
/// did not exist; post-QOL-01 (commit <hash>) FlowEngine.cs:42 sets
/// _diagnosticOutput = verbose ? Console.Error : null and FlowEngine.cs:81
/// writes "[verbose] Executing <file>" at engine start.
/// </summary>
[Collection("FlowScripts")]   // serialize Console.SetOut (Pitfall 3)
public class VerboseFlagTests
{
    [Fact]
    public void RunSource_WithVerbose_WritesVerbosePrefixToStderr()
    {
        using var runner = new FlowEngineRunner(verbose: true);
        var (_, _, stderr, _) = runner.RunSource(@"
use ""@std""
(print ""ok"")
");
        Assert.Contains("[verbose] Executing", stderr);
    }
}
```

Key reused patterns from `InterpreterTests.cs`:
- **Class-level XML `<summary>` comment** documenting what the test is a regression for (lines 6-13 of InterpreterTests.cs)
- **`[Collection("FlowScripts")]`** attribute (line 14) — MANDATORY, Pitfall 3
- **`using var runner = new FlowEngineRunner(...)`** (line 20) — disposable pattern
- **`var (_, stdout, stderr, errorCount) = runner.RunSource(...)`** tuple destructuring (line 25) — unused tuple slots use `_`
- **`use "@std"` at top of literal script** (line 26) — MANDATORY, Pitfall 2. If the test touches buffers, also add `use "@audio"`

---

### `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` (test, integration — new)

**Analog:** `flow-lang.Tests/Unit/InterpreterTests.cs`

**Exact file** (from 13-RESEARCH.md `§Code Examples` lines 606-637):

```csharp
using Xunit;
using FlowLang.Tests.Fixtures;

namespace FlowLang.Tests.Integration.Phase06;

[Collection("FlowScripts")]
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

This is the AUDIT integration-gap regression gate. The 2nd `use "@audio"` is needed because the script touches `renderSong`/`getFrames`/`Buffer`.

---

### `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` (test, integration — new)

**Analog:** `flow-lang.Tests/Unit/InterpreterTests.cs` (class template)

**Core pattern** (mirrors InterpreterTests.cs:38-52 Theory pattern):

```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase07;

/// <summary>
/// DX-04 regression test: REPL auto-imports @std, @audio, @collections so
/// that interactive users can call print/list/createSineTone without
/// typing `use` statements. This test executes the SAME three imports
/// via FlowEngine and verifies the symbols resolve. The v1.1 audit noted
/// DX-04 "is not e2e-testable via piped stdin — verified by code inspection
/// only" [v1.1-MILESTONE-AUDIT.md line 49]; this is the best proxy.
/// </summary>
[Collection("FlowScripts")]
public class RepLAutoImportTests
{
    [Fact]
    public void AutoImportedModulesResolve_StdAudioCollections()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
use ""@collections""
Array[Int] xs = (list 1 2 3)
Buffer b = (createSineTone 0.1 440.0 0.3)
(print ""ok"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
    }
}
```

---

### `flow-lang.Tests/Unit/Phase08/MixTests.cs` (test, unit — new)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs`

**Imports pattern** (CollectionsTests.cs lines 1-7):
```csharp
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase08;
```

Additional imports likely needed: `using FlowLang.StandardLibrary.Audio;` (for `AudioCore.Mix`) and `using FlowLang.Runtime;` (for `AudioBuffer` / `Value`).

**Core test pattern** (CollectionsTests.cs:11-40 shape — `Value.X` factories + `result.As<T>()` pattern):

```csharp
public class MixTests
{
    [Fact]
    public void Mix_SumsSamples_AdditiveSemantics()
    {
        // AUDIO-05 pin: AudioCore.Mix uses result.Data[i] = sampleA + sampleB,
        // not overwrite or average. [VERIFIED: AudioCore.cs:200]
        var bufA = /* AudioBuffer with Data[0] = 0.5f */;
        var bufB = /* AudioBuffer with Data[0] = 0.3f */;
        var result = AudioCore.Mix(new[] { Value.Audio(bufA), Value.Audio(bufB) });
        var outBuf = result.As<AudioBuffer>();
        Assert.Equal(0.8f, outBuf.Data[0], precision: 4);  // 0.5 + 0.3 exact in float32
    }
}
```

Pass 2 will fill in the AudioBuffer constructor per its actual signature; the PATTERN is `Value factory → function call → result.As<T>() → assertion`.

---

### `flow-lang.Tests/Unit/Phase08/SynthesizerFactoryTests.cs` (test, unit — new)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs`

**Core pattern** (structural `is`-check; deterministic, no audio):

```csharp
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Unit.Phase08;

public class SynthesizerFactoryTests
{
    [Theory]
    [InlineData("strings", typeof(StringsSynthesizer))]
    [InlineData("organ", typeof(OrganSynthesizer))]
    [InlineData("bell", typeof(BellSynthesizer))]
    public void Create_ReturnsExpectedSynthesizerType(string presetName, Type expectedType)
    {
        var synth = SynthesizerFactory.Create(presetName);
        Assert.IsType(expectedType, synth);
    }
}
```

Theory + `[InlineData]` pattern copied from `Unit/InterpreterTests.cs:38-44`.

---

### `flow-lang.Tests/Integration/Phase09/TutorialTests.cs` (test, integration — new)

**Analog:** `flow-lang.Tests/Unit/InterpreterTests.cs` (class shell) + `flow-lang.Tests/FlowScriptTests.cs` (CWD pivot, lines 19-24, 50-53)

**CWD pivot pattern** (FlowScriptTests.cs:19-24 + try/finally at 26, 50-53):
```csharp
var origCwd = Environment.CurrentDirectory;
Environment.CurrentDirectory = Path.GetDirectoryName(testsRoot)!;
try
{
    // test body using runner.RunFile(absolutePath)
}
finally
{
    Environment.CurrentDirectory = origCwd;
}
```

**Combined file template:**

```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase09;

/// <summary>
/// QOL-02: examples/tutorial.flow must run to completion without errors.
/// The tutorial is NOT in tests/ so it is not covered by the Theory harness
/// [VERIFIED: FlowScriptData.cs:8 globs tests/ only]. This Fact pins the
/// tutorial's exit code post-v1.1 stability fixes.
/// </summary>
[Collection("FlowScripts")]
public class TutorialTests
{
    [Fact]
    public void TutorialRunsToCompletion()
    {
        var testsRoot = FlowScriptData.FindTestsRoot();
        var repoRoot = Path.GetDirectoryName(testsRoot)!;
        var tutorialPath = Path.Combine(repoRoot, "examples", "tutorial.flow");

        var origCwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = repoRoot;
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, errorCount) = runner.RunFile(tutorialPath);
            Assert.True(ok, $"tutorial errored: {stderr}");
            Assert.Equal(0, errorCount);
        }
        finally
        {
            Environment.CurrentDirectory = origCwd;
        }
    }
}
```

Per-CONTEXT A1 + Open Question 2: if the tutorial errors, the Pass-2 executor MUST flag as `## Ultra-Important Finding` in `09-VALIDATION.md` and document as deferred-to-Phase-16, NOT fix the tutorial. Replace the Fact with a `Skip` attribute and record in Divergences.

---

### `flow-lang.Tests/Unit/Phase10/FormantSynthesizerTests.cs` (test, unit — new)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs`

**Exact file** (from 13-RESEARCH.md `§Code Examples` lines 641-677):

```csharp
using Xunit;
using FlowLang.StandardLibrary.Audio.Vocalization;

namespace FlowLang.Tests.Unit.Phase10;

public class FormantSynthesizerTests
{
    [Fact]
    public void SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames()
    {
        // D-18 canonical pin: 2.0s × 44100Hz = 88200 samples.
        // 261.63Hz is C4 per 12-tone equal temperament.
        var buffer = FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0);
        Assert.Equal(88200, buffer.Frames);
        Assert.Equal(1, buffer.Channels);
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
        bool hasAudibleSample = false;
        foreach (var s in buffer.Data)
            if (Math.Abs(s) > 0.01f) { hasAudibleSample = true; break; }
        Assert.True(hasAudibleSample, $"Vowel '{vowel}' produced near-silent buffer");
    }
}
```

Per Pitfall 8: do NOT extend the 88200 pin to consonant-vowel syllables (`"na"`, `"ta"`, `"sa"`) — their crossfade math produces different counts.

---

### `flow-lang.Tests/Unit/Phase10/FormantDataTests.cs` (test, unit — new)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs` (lines 12-18 — `Assert.Throws<T>` pattern with message check)

**Imports + test pattern:**

```csharp
using Xunit;
using FlowLang.StandardLibrary.Audio.Vocalization;

namespace FlowLang.Tests.Unit.Phase10;

public class FormantDataTests
{
    [Fact]
    public void GetFormants_UnknownVowel_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => FormantData.GetFormants("xyz"));
        Assert.Equal("Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo", ex.Message);
    }
}
```

Exception-pattern reference from `CollectionsTests.cs:14-18`:
```csharp
var ex = Assert.Throws<InvalidOperationException>(
    () => Collections.Init(new[] { emptyArray }));
Assert.Equal("Cannot get init of empty array", ex.Message);
```

---

### `flow-lang.Tests/Unit/Phase10/TtsHookTests.cs` (test, unit — new)

**Analog:** `flow-lang.Tests/Unit/CollectionsTests.cs`

**Core pattern** (2 Facts: round-trip + empty-input validation):

```csharp
using Xunit;
using FlowLang.StandardLibrary.Audio.Vocalization;

namespace FlowLang.Tests.Unit.Phase10;

public class TtsHookTests
{
    [Fact]
    public void SetCommand_RoundTrips_ViaGetCommand()
    {
        var original = TtsHook.GetCommand();
        try
        {
            TtsHook.SetCommand("echo");
            Assert.Equal("echo", TtsHook.GetCommand());
        }
        finally
        {
            TtsHook.SetCommand(original);   // reset global static (Pitfall 9)
        }
    }

    [Fact]
    public void SetCommand_Empty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => TtsHook.SetCommand(""));
        Assert.Contains("TTS command cannot be null or whitespace", ex.Message);
    }
}
```

Per Pitfall 9: do NOT invoke `TtsHook.RunTts(...)` in any automated Fact. Subprocess invocation is Manual-Only.

The `try { ... } finally { TtsHook.SetCommand(original); }` pattern restores the global static; without it, parallel tests (Pitfall 3 via `[Collection("FlowScripts")]` still serializes) or later tests would see the mutated default. Note: this class does NOT need `[Collection("FlowScripts")]` because it doesn't touch Console — but adding it is harmless and consistent.

---

### `flow-lang.Tests/FlowScriptData.cs` (data, MODIFY — sentinel tightening)

**Analog:** the file itself (pre-existing `RequiredSentinels` dictionary at lines 60-71)

**Existing structure to extend** (FlowScriptData.cs:60-71):
```csharp
public static readonly Dictionary<string, string[]> RequiredSentinels = new()
{
    // spike/c1 body-execution evidence. Absent pre-FIX-07a → assertion fails (RED).
    // Present post-FIX-07a → assertion passes (GREEN). Flip commit lives in plan 12-04.
    [Path.Combine("spike", "c1-musical-context-body.flow")] = new[]
    {
        "c1-probe1-body-ran",
        "c1-probe2-stmt1",
        "c1-probe2-stmt2",
        "c1-probe3-body-ran",
    },
};
```

**Edit pattern per plan:** APPEND a new key/value to the dictionary. Comment line above each entry identifies the plan + requirement. Pattern from 13-RESEARCH.md `§Code Examples` lines 682-719:

```csharp
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
```

Per plan (9 total tightened entries; see 13-RESEARCH.md `§Summary of New Facts Required`):

- 13-01: `test_transpose_int.flow` (pin values above)
- 13-02: `test_comments.flow` → `["5", "10", "15", "8"]`; `test_math.flow` → Pass-2 empirically captures exact `(str pi)` etc.; `test_writewav.flow` → `["PASS: writeWav(String, Buffer) succeeded", "PASS: exportWav(Buffer, String) backwards compat succeeded", "All writeWav tests passed"]`
- 13-03: `test_mix.flow` (above); `test_gain_context.flow` → `["gain 0.5 block executed", "nested gain context executed"]`; `test_synth_presets.flow` → `["strings: rendered voices OK", "organ: rendered voices OK", "bell: rendered voices OK"]`
- 13-04: `test_tempo_ramp.flow` (above)
- 13-05: `test_vocalization.flow` → `["frames: 22050", "PASS: sing ah produced audio buffer", "PASS: all 5 vowels synthesized", "PASS: consonant syllables synthesized", "PASS: vocal mixed with instrumental"]`

**Sentinel-text rule** (Pitfall 1): the sentinel substring is the SHORTEST unique string that uniquely identifies the expected output. `"22050"` alone is too loose (would match any frame count with those digits); `"mix frames: 22050"` is minimal + unique.

**Pitfall 5 rule:** for any sentinel referencing a `Double`/`Float` value (pi, sqrt, trig), Pass 2 MUST run the script empirically via `dotnet run --project flow-interpreter tests/test_math.flow` and copy the exact printed string into the sentinel array. Do NOT infer format from `Math.PI.ToString()`.

---

### `.planning/REQUIREMENTS.md` (docs, MODIFY — TEST-04 closure)

**Analog:** TEST-03 close pattern at `REQUIREMENTS.md:92` (Phase 12 plan 12-06 edit)

**Before** (`REQUIREMENTS.md:44`):
```markdown
- [ ] **TEST-04**: Retroactive Nyquist validation — `.planning/phases/06-diagnostics-bug-fixes/`, `07-developer-experience/`, `08-audio-production/`, `09-advanced-features/` each gain a `VALIDATION.md` satisfying the Nyquist checklist; phase 10 draft updated to `nyquist_compliant: true` or explicit waiver.
```

**After** (apply the same `[ ]` → `[x]` + parenthetical-closure-note pattern from FIX-05's line 87 `"Shipped 6e5a960"` or TEST-01's line 41 `"(CLOSED as audit false positive, 2026-04-19)"`):

```markdown
- [x] **TEST-04** (Shipped <commit-hash>, 2026-04-19): Retroactive Nyquist validation — Phase 6–9 each gained a `VALIDATION.md` with `nyquist_compliant: true`; Phase 10 draft promoted to `nyquist_compliant: true` (commit <hash>). All 16 observable-value pins across 5 plans verified; dotnet test green.
```

**Traceability table edit** (`REQUIREMENTS.md:93`):
```markdown
| TEST-04 | Phase 13 | Pending |
```
becomes:
```markdown
| TEST-04 | Phase 13 | Shipped <commit-hash> (closed 2026-04-19) |
```

Pattern copied from TEST-03's row at line 92: `| TEST-03 | Phase 12 | Shipped 9afbe7a + c09cd82 (reframed per CONTEXT D-01) |`.

**Footer edit** (`REQUIREMENTS.md:103`):
```markdown
*Last updated: 2026-04-19 — Phase 12 Stability closed; FIX-05/06/07a shipped, TEST-01/02 closed (audit false positives), TEST-03 reframed + shipped.*
```
becomes:
```markdown
*Last updated: 2026-04-19 — Phase 13 Nyquist Validation Backfill closed; TEST-04 shipped (Phase 6–10 VALIDATION.md authored + promoted).*
```

---

### `.planning/STATE.md` + `.planning/ROADMAP.md` (phase-completion workflow)

Out of plan scope — the phase-completion workflow handles these. No analog-excerpt required in PATTERNS.md; the `/gsd-execute-phase` closure step edits them.

---

### `flow-lang.Tests/Integration/AudioTestHelpers.cs` (OPTIONAL shared helper — greenfield)

**Analog:** none — greenfield.

Per CONTEXT `<Claude's Discretion>`: "How many hand-rolled zero-crossing helpers to share vs inline (a single `AudioTestHelpers.CountZeroCrossings(buffer, start, length)` feels right but not mandated)"

Only create this file if ≥2 Facts actually need zero-crossing detection. Based on 13-RESEARCH.md `§Per-Phase Observable Invariants`, the 16 pins use:
- Frame-count assertions (numeric): 10.1, 10.2, 6.3, 8.1, 8.2
- Error-text substrings: 6.1, 6.4, 7.1, 10.2, 10.4
- Structural type checks: 6.2, 7.3, 7.4, 8.4, 10.3
- Boolean-in-stdout: 9.1
- Exit code: 9.2

**Zero-crossing detection is NOT required** by any of the 16 pins. The helper is therefore unnecessary for Phase 13 — skip creating it. If Pass 2 discovers a need (e.g., a flaky buffer check that needs fundamental-frequency validation), add it then, not speculatively.

Should it become necessary, the skeleton would be:

```csharp
namespace FlowLang.Tests.Integration;

public static class AudioTestHelpers
{
    /// <summary>
    /// Counts sign changes in buffer.Data over [start, start+length).
    /// Fundamental frequency ≈ (crossings / 2) / (length / sampleRate).
    /// </summary>
    public static int CountZeroCrossings(float[] data, int start, int length)
    {
        int crossings = 0;
        for (int i = start + 1; i < start + length; i++)
            if (Math.Sign(data[i]) != Math.Sign(data[i - 1])) crossings++;
        return crossings;
    }
}
```

No analog file; recorded here for completeness only.

---

## Shared Patterns

### `[Collection("FlowScripts")]` Console.SetOut Serialization (Pitfall 3)

**Source:** `flow-lang.Tests/FlowScriptTests.cs:6-9` (definition) + `Unit/InterpreterTests.cs:14` (consumer)
**Apply to:** every test class that uses `FlowEngineRunner`

**Definition** (FlowScriptTests.cs:6-9 — already exists; test classes just apply the attribute):
```csharp
[CollectionDefinition("FlowScripts", DisableParallelization = true)]
public class FlowScriptsCollection { }
```

**Usage** (InterpreterTests.cs:14):
```csharp
[Collection("FlowScripts")]   // serialize Console.SetOut with wrap-as-Theory (Pitfall 3)
public class ExecuteMusicalContextTests
```

Pure-API Unit Facts (e.g., `FormantSynthesizerTests`, `SynthesizerFactoryTests`, `TtsHookTests` — pure C# calls, no FlowEngine) do NOT strictly need this attribute, but Phase 13 authors SHOULD add it uniformly for consistency (harmless; protects against future `Console.WriteLine` additions).

### `use "@std"` in Literal Scripts (Pitfall 2)

**Source:** `Unit/InterpreterTests.cs:22-25` commentary + line 26 code
**Apply to:** every `RunSource(string)` call; every literal script block

```csharp
var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
tempo -5 { (print ""body-ran"") }
");
```

If the script touches buffers: ALSO add `use "@audio"` on the next line (see SectionGainBareExpressionTests above). If it uses `list`/`head`/`tail`: add `use "@collections"`.

### CWD Pivot for File-System-Relative Scripts (Pitfall 4)

**Source:** `flow-lang.Tests/FlowScriptTests.cs:19-24` + `:50-53`
**Apply to:** every Integration Fact using `runner.RunFile(...)` on a script that touches relative file paths

```csharp
var origCwd = Environment.CurrentDirectory;
Environment.CurrentDirectory = Path.GetDirectoryName(testsRoot)!;
try
{
    using var runner = new FlowEngineRunner();
    var (_, stdout, stderr, errorCount) = runner.RunFile(absolute);
    // ... assertions ...
}
finally
{
    Environment.CurrentDirectory = origCwd;
}
```

Phase 13 needs this ONLY in `TutorialTests.cs` (tutorial.flow may write WAV with a relative path).

### Tuple-Destructure Unused Slots with `_`

**Source:** `Unit/InterpreterTests.cs:25, 49`
**Apply to:** every `.RunSource(...)` / `.RunFile(...)` call

```csharp
var (_, stdout, stderr, _) = runner.RunSource(...);          // ignore Success + ErrorCount
var (ok, _, stderr, errorCount) = runner.RunSource(...);     // ignore Stdout
```

### Xml `<summary>` Regression-Pin Comment on Test Classes

**Source:** `Unit/InterpreterTests.cs:6-13` + `Unit/ThunkTests.cs:13-19`
**Apply to:** every new Fact class

Format: describe WHAT requirement/bug the class is a regression for, WHY the invariant matters, and cite the source file:line where the invariant is enforced. Example (ThunkTests.cs:13-19):

```csharp
/// <summary>
/// FIX-06 regression tests: Thunk caches both successful values and exceptions.
/// After the Lazy&lt;Value&gt; refactor, failed evaluators must re-throw the SAME
/// exception with the original stack trace preserved (ExceptionDispatchInfo
/// semantics), and the evaluator must be invoked exactly once regardless of
/// how many times Force() is called.
/// </summary>
```

Phase 13 equivalent: cite the v1.1 REQ-ID (QOL-01, FIX-02, DX-03, AUDIO-05, VOC-01, etc.) + the source file:line where the behavior is implemented (per the Canonical References list in CONTEXT).

### `Pass 1 Draft` / `Pass 2 Implementation Map` / `Divergences` Sections (NEW — Phase 13 mandate)

**Source:** 12-VERIFICATION.md:167-171 (`## Key Discrepancy Notes`) — the format, not the section name
**Apply to:** every VALIDATION.md authored or modified by Phase 13

Per CONTEXT D-13/D-14/D-15: two-pass strict authorship. Pass 1 writes the `## Pass 1 Draft` section reading ONLY `v1.1-REQUIREMENTS.md`. Pass 2 verifies against code, fills `## Pass 2 Implementation Map`, and logs mismatches in `## Divergences`. Honest documentation of requirement-vs-reality drift is the single most valuable phase output.

Divergence entry format (mirror 12-VERIFICATION.md:167-171 prose shape):

```markdown
- **REQ-{XX}:** Pass 1 drafted assertion `"{text}"` based on REQUIREMENTS.md wording.
  Pass 2 found actual error format `"{real text}"` at `{source file}:{line}`.
  Which is correct: {codebase / requirements / document deferral reason}.
```

### Atomic Commit Per VALIDATION.md + Per-Fact-File (Phase 11/12 pattern)

**Source:** 12-VERIFICATION.md:187-193 (per-FIX commit list)
**Apply to:** every plan's commits

- 1 commit: `docs(validation): author {N}-VALIDATION.md` (pure docs creation)
- 0-2 commits: `test(validation): add {Phase{N}}/{TestClass}` per new Fact file (if any)
- 0 commits: `test(data): tighten {test_X.flow} sentinel` (part of the Fact-file commit is also acceptable — one commit per plan is the target per D-08)

Plan 13-05 carries the extra closure commit: `docs(traceability): close TEST-04 + promote 10-VALIDATION.md`.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang.Tests/Integration/` directory | directory | N/A | Does not exist yet; 13-01 creates it. Convention follows existing `flow-lang.Tests/Unit/` subdirectory pattern from Phase 12. |
| `flow-lang.Tests/Integration/AudioTestHelpers.cs` | shared helper | transform (buffer → int) | Greenfield; optional — only create if 2+ Facts need zero-crossing detection. Current 16-pin inventory does NOT require it. |

---

## Metadata

**Analog search scope:**
- `.planning/phases/12-stability/` (VALIDATION.md + VERIFICATION.md formats)
- `.planning/phases/10-vocalization/` (existing draft to promote)
- `.planning/REQUIREMENTS.md` (TEST-03 closure pattern)
- `flow-lang.Tests/` (all files)
- `~/.claude/get-shit-done/templates/VALIDATION.md` (canonical schema)

**Files read during mapping:**
- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md`
- `.planning/phases/13-nyquist-validation-backfill/13-RESEARCH.md` (§Existing Coverage Map, §Validation Architecture, §Code Examples, §Sources, §Assumptions)
- `.planning/phases/12-stability/12-VALIDATION.md`
- `.planning/phases/12-stability/12-VERIFICATION.md` (§Key Discrepancy Notes + §Plan 12-06 Rollup)
- `.planning/phases/10-vocalization/10-VALIDATION.md`
- `.planning/REQUIREMENTS.md`
- `flow-lang.Tests/FlowScriptData.cs`
- `flow-lang.Tests/FlowScriptTests.cs`
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`
- `flow-lang.Tests/Unit/InterpreterTests.cs`
- `flow-lang.Tests/Unit/CollectionsTests.cs`
- `flow-lang.Tests/Unit/ThunkTests.cs`
- `flow-lang.Tests/flow-lang.Tests.csproj`
- `~/.claude/get-shit-done/templates/VALIDATION.md`

**Total files scanned:** 14
**Pattern extraction date:** 2026-04-19
