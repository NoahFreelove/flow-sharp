# Phase 25: Gaussian Humanize (LAST PRNG phase) - Pattern Map

**Mapped:** 2026-05-04
**Files analyzed:** 9 (3 production code, 1 helper extension, 1 std.flow, 2 tests, 1 .flow smoke, 2 examples updates)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (new `HumanizeGaussian`) | transform-function | request-response (Sequence→Sequence) | `Humanize` at same file:875-903 | exact (same file, sibling) |
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (new `RegisterHumanizeGaussian`) | registry-registration | one-shot registration | `RegisterHumanize` at same file:866-871 | exact (same file, sibling) |
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (new `NextGaussianSample` helper) | utility (private static math) | pure function | greenfield — no prior private static math helper in this file | no analog (new pattern) |
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (extend `With(...)` with `velocity` slot) | type-helper-extension | builder method | existing `With(...)` at same file:317-330 | exact (extending existing) |
| `flow-lang/std.flow` (new `internal proc humanizeGaussian`) | docs (.flow declaration) | declaration | seeded last-arg variant: `euclidean` 6-arg at `std.flow:154` | exact role + data flow |
| `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` (NEW) | xUnit Facts | unit test | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:32-87` | exact (deterministic-pin pattern) |
| `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (NEW) | xUnit Facts | integration test | `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:1-90` | exact (verbatim mirror) |
| `flow-lang.Tests/FlowScriptData.cs` (new sentinel entry) | test fixture data | dictionary entry | `FlowScriptData.cs:225-232` Phase 15 euclidean humanize entry | exact |
| `tests/test_humanize_gaussian.flow` (NEW) | .flow smoke | end-to-end | `tests/test_euclidean_humanize.flow` | exact (seeded PRNG byte-identical smoke) |
| `examples/showcase.flow` (additive call site) | docs/example | example modification | existing wrap pattern at `showcase.flow:13-14` (`padBase -> crescendo ...`) | role-match (additive transform) |
| `examples/tutorial.flow` (new chapter) | docs/example | tutorial chapter | existing humanize chapter region around `tutorial.flow:567-577` | exact (immediate context) |

## Pattern Assignments

### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — `HumanizeGaussian` method (new)

**Role:** transform-function (Sequence→Sequence Gaussian velocity perturbation)
**Analog:** `Humanize` at `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:875-903`

**CRITICAL CAVEAT — pre-existing bug in analog:** The analog `Humanize:896-898` rebuilds notes via the OLD positional `MusicalNoteData` ctor passing only 12 args, silently dropping `DurationFraction`, `OnsetOffset`, `DurationOverlap`, `PortamentoMs`, `IsChordTone` (5 fields). The analog is FROZEN per D-18 (cannot fix). `HumanizeGaussian` MUST NOT copy the ctor call — instead use `note.With(velocity: newVelocity)` (after extending the With helper per pattern below).

**Imports pattern** (lines 1-6):
```csharp
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Transforms;
```

**Sequence iteration loop pattern** (lines 880-901) — COPY VERBATIM (this part of `Humanize` is correct):
```csharp
var result = new SequenceData();
foreach (var bar in seq.Bars)
{
    var newNotes = new List<MusicalNoteData>();
    foreach (var note in bar.MusicalNotes)
    {
        if (note.IsRest)
        {
            newNotes.Add(note);
            continue;
        }

        // Velocity jitter: random variation scaled by amount
        double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
        double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);

        // ⚠ DO NOT COPY THIS LINE — bug per D-18 (drops 5 fields):
        newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
            note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
            newVelocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
    }
    result.AddBar(new BarData(newNotes, bar.TimeSignature!));
}
return Value.Sequence(result);
```

**Argument unpacking + clamp pattern** (lines 877-878) — COPY:
```csharp
var seq = args[0].As<SequenceData>();
double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
```

**LOCAL `new Random(seed)` pattern** — supplemental analog at `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77`:
```csharp
private static Value VarySeeded(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
}
```

**LOCAL `new Random(seed)` comment-style precedent** — `flow-lang/StandardLibrary/BuiltInFunctions.cs:1256-1258` (cite this verbatim in `HumanizeGaussian`'s header comment):
```csharp
// D-17: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
// ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
var rng = new Random(seed);
```

**Final assembled body** (per RESEARCH §Code Examples — recommended skeleton):
```csharp
// ===== Humanize Gaussian =====
// CONTEXT D-01..D-25 anchor decisions:
//   D-01  signature (Sequence, Double, Int) order (seq, amount, seed)
//   D-03  LOCAL new Random(seed) per call; does NOT touch ExecutionContext.GetRand.
//         Mirrors VariationFunctions.VarySeeded:71-77 and BuiltInFunctions.cs:1258.
//   D-05  basic Box-Muller (cos branch); D-06 sin discarded
//   D-07  velJitter = z * amount * 0.2 (matches uniform humanize jitter range)
//   D-08  amount clamped to [0, 1]; D-09 velocity clamped to [0.05, 1.0]
//   D-10  amount==0 short-circuit returns input unchanged
//   D-11  rests pass through; D-12/D-13 empty/all-rest sequences pass through
private static Value HumanizeGaussian(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);   // D-08
    int seed = args[2].As<int>();                                 // D-15

    if (amount == 0.0) return Value.Sequence(seq);                // D-10 short-circuit

    var rng = new Random(seed);                                   // D-03 LOCAL
    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }    // D-11
            double z = NextGaussianSample(rng);                   // D-05/D-06
            double velJitter = z * amount * 0.2;                  // D-07
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);  // D-09
            newNotes.Add(note.With(velocity: newVelocity));       // ← bug-free path (NOT analog's ctor)
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));
    }
    return Value.Sequence(result);
}
```

---

### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — `RegisterHumanizeGaussian` method (new)

**Role:** registry-registration
**Analog:** `RegisterHumanize` at `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:866-871`

**Registration pattern** (lines 866-871) — COPY exactly, swap name + add `IntType.Instance` for seed:
```csharp
private static void RegisterHumanize(InternalFunctionRegistry registry)
{
    var humanizeSig = new FunctionSignature("humanize",
        [SequenceType.Instance, DoubleType.Instance]);
    registry.Register("humanize", humanizeSig, Humanize);
}
```

**`RegisterAll` insertion pattern** (lines 17-30) — insert `RegisterHumanizeGaussian(registry);` immediately after `RegisterHumanize(registry);`:
```csharp
public static void Register(InternalFunctionRegistry registry)
{
    RegisterTranspose(registry);
    RegisterInvert(registry);
    RegisterRetrograde(registry);
    RegisterAugmentDiminish(registry);
    RegisterOctaveShift(registry);
    RegisterRepeat(registry);
    RegisterConcat(registry);
    RegisterDynamicTransforms(registry);
    RegisterTempoTransforms(registry);
    RegisterHumanize(registry);
    // PHASE 25: insert here →   RegisterHumanizeGaussian(registry);
    RegisterOrnamentTransforms(registry);
}
```

---

### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — `NextGaussianSample` helper (new)

**Role:** utility (private static math helper)
**Analog:** No prior private static math helper exists in `TransformFunctions.cs`. This is a greenfield helper. Style guidance: emit at the bottom of the class (after the new `HumanizeGaussian` block), file-scoped namespace continues, `private static` matching the rest of the file's helper conventions.

**Recommended body** (per RESEARCH §Pattern 2 + §Claude's Discretion #1, #2):
```csharp
private static double NextGaussianSample(Random rng)
{
    double u1 = rng.NextDouble();
    double u2 = rng.NextDouble();
    // Guard against u1 == 0.0 (legal per Random.NextDouble [0, 1) contract).
    // Math.Log(0) = -infinity → NaN propagation through subsequent Math.Clamp
    // would silently produce a clamped-to-0.05 ghost note. The 1e-300 floor is
    // ~37 stddevs out — clamped at the velocity boundary, no audible artifact.
    u1 = Math.Max(u1, 1e-300);
    return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
}
```

**Style precedent for `Math.Max(value, floor)` clamp idiom** — `flow-lang/StandardLibrary/BuiltInFunctions.cs:1312`:
```csharp
v = Math.Max(0.0, Math.Min(1.0, v));
```
The `Math.Max(u1, 1e-300)` floor is the same idiom.

---

### `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — extend `With(...)` helper

**Role:** type-helper-extension
**Analog:** existing `With(...)` at `NoteType.cs:317-330` (extending in place)

**Existing signature** (lines 317-330) — the planner extends this by adding `double? velocity = null` and one body line:
```csharp
public MusicalNoteData With(
    double? onsetOffset = null,
    double? durationOverlap = null,
    double? portamentoMs = null)
{
    return new MusicalNoteData(
        NoteName, Octave, Alteration, DurationValue, IsRest,
        CentOffset, IsTied, Velocity, Articulation, IsDotted,
        SourceLocation, SourceLength, DurationFraction,
        onsetOffset: onsetOffset ?? OnsetOffset,
        durationOverlap: durationOverlap ?? DurationOverlap,
        portamentoMs: portamentoMs ?? PortamentoMs,
        isChordTone: IsChordTone);
}
```

**Recommended extension** — add the `velocity` parameter and replace the positional `Velocity` argument:
```csharp
public MusicalNoteData With(
    double? onsetOffset = null,
    double? durationOverlap = null,
    double? portamentoMs = null,
    double? velocity = null)              // ← NEW (Phase 25)
{
    return new MusicalNoteData(
        NoteName, Octave, Alteration, DurationValue, IsRest,
        CentOffset, IsTied,
        velocity ?? Velocity,             // ← was: Velocity
        Articulation, IsDotted,
        SourceLocation, SourceLength, DurationFraction,
        onsetOffset: onsetOffset ?? OnsetOffset,
        durationOverlap: durationOverlap ?? DurationOverlap,
        portamentoMs: portamentoMs ?? PortamentoMs,
        isChordTone: IsChordTone);
}
```

**Convention:** matches the Phase 22 docstring at `NoteType.cs:305-316` — "transforms call `With(...)` naming ONLY the field they own." Phase 25's `HumanizeGaussian` owns `velocity`, calls `note.With(velocity: newVelocity)`.

---

### `flow-lang/std.flow` — new `internal proc humanizeGaussian` declaration

**Role:** docs (.flow declaration making the registered C# function visible to user scripts)
**Analog:** `euclidean` 6-arg seeded form at `flow-lang/std.flow:154` (seed-as-last-Int convention)

**Existing humanize section** (lines 135-136):
```flow
Note: Humanize
internal proc humanize (Sequence: seq, Double: amount)
```

**Existing seeded last-arg precedent** (line 154):
```flow
internal proc euclidean (Int: hits, Int: steps, Note: pitch, Double: swing, Double: humanize, Int: seed)
```

**Insertion** — append immediately after line 136:
```flow
Note: Humanize
internal proc humanize (Sequence: seq, Double: amount)
internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)   // ← PHASE 25
```

---

### `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` (NEW)

**Role:** xUnit Facts (deterministic-pin + statistical-sanity unit tests)
**Analog:** `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:32-87`

**Imports + class header pattern** (lines 1-33):
```csharp
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase15;

/// <summary>
/// Phase 15 Plan 04 (DX-09): swing accent semantics on the 4-arg
/// <c>euclidean(Int, Int, Note, Double)</c> overload.
/// ...
/// </summary>
[Collection("FlowScripts")]
public class EuclideanSwingTests
{
    private const double BaseVelocity = 0.63;
    private const double Tol = 1e-9;
```

**For Phase 25, mirror with namespace `FlowLang.Tests.Unit.Phase25`, class `HumanizeGaussianFacts`, identical `Tol = 1e-9` and `BaseVelocity = 0.63` constants.** RESEARCH §Claude's Discretion #4 + Wave-0 gap list explicitly require these constants.

**Helper extraction pattern** (lines 35-53) — extract a `HitNotes` / `RunHumanizeGaussian` helper to avoid repetition:
```csharp
private static List<MusicalNoteData> HitNotes(SequenceData seq)
{
    var bar = seq.Bars[0];
    var hits = new List<MusicalNoteData>();
    foreach (var n in bar.MusicalNotes)
        if (!n.IsRest) hits.Add(n);
    return hits;
}

private static SequenceData RunEuclidean(FlowEngineRunner runner, string callExpr, string varName = "s")
{
    var (success, _, stderr, errorCount) = runner.RunSource(
        "use \"@std\"\n" +
        $"Sequence {varName} = {callExpr}\n");
    Assert.True(success, $"euclidean call failed. stderr={stderr}");
    Assert.Equal(0, errorCount);
    var v = runner.GetVariable(varName);
    return v.As<SequenceData>();
}
```

**Caveat for the deterministic-pin Fact (Pitfall 4 in RESEARCH):** the pin Fact MUST construct the input via direct C# `MusicalNoteData(...)` to bypass `MusicalContext.Velocity` interference. The `RunEuclidean`-style runner helper is fine for the other 6 Facts where exact velocity is not pinned.

**Exact-velocity pinning pattern** (lines 83-87) — the canonical assertion shape for the deterministic-pin Fact:
```csharp
Assert.Equal(BaseVelocity, hits[0].Velocity, Tol);
Assert.Equal(BaseVelocity + 0.3, hits[1].Velocity, Tol);
Assert.Equal(BaseVelocity, hits[2].Velocity, Tol);
Assert.True(hits[1].Velocity > hits[0].Velocity);
```

**Header docstring style** — mirror Phase 22 `ArpeggioFacts.cs:7-25` "byte-identical determinism" comment style; reference D-IDs (D-01..D-25) and cite RESEARCH §Validation Architecture.

---

### `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (NEW)

**Role:** xUnit Facts (two-runner byte-identical integration)
**Analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:1-90` — VERBATIM MIRROR. Only changes: namespace `FlowLang.Tests.Integration.Phase25`, run-file basenames `phase25_showcase_run1.{ext}` / `phase25_showcase_run2.{ext}`.

**Full file pattern** (lines 1-90):
```csharp
using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase18;

/// <summary>
/// FRAC-02 byte-identical determinism gate for examples/showcase.flow.
/// ...
/// </summary>
[Collection("FlowScripts")]
public class ByteIdenticalShowcaseTests
{
    [Fact]
    public void Showcase_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare(isMidi: false);
    }

    [Fact]
    public void Showcase_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare(isMidi: true);
    }

    private static void RunTwiceAndCompare(bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "showcase.flow");
        Assert.True(File.Exists(scriptPath), $"showcase.flow missing at {scriptPath}");

        string ext = isMidi ? "mid" : "wav";
        string baseName = "flow_showcase";
        string outDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outDir, $"phase18_showcase_run1.{ext}");
        string path2 = Path.Combine(outDir, $"phase18_showcase_run2.{ext}");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);
        Directory.CreateDirectory(outDir);

        string source = File.ReadAllText(scriptPath);
        string defaultRel = $"examples/output/{baseName}.{ext}";
        string sourceRun1 = source.Replace(defaultRel, $"tests/output/phase18_showcase_run1.{ext}");
        string sourceRun2 = source.Replace(defaultRel, $"tests/output/phase18_showcase_run2.{ext}");

        Assert.NotEqual(source, sourceRun1);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using (var runner1 = new FlowEngineRunner())
            {
                var (success1, _, stderr1, errorCount1) = runner1.RunSource(sourceRun1);
                Assert.True(success1, $"run1 failed: stderr={stderr1}");
                Assert.Equal(0, errorCount1);
            }

            using (var runner2 = new FlowEngineRunner())
            {
                var (success2, _, stderr2, errorCount2) = runner2.RunSource(sourceRun2);
                Assert.True(success2, $"run2 failed: stderr={stderr2}");
                Assert.Equal(0, errorCount2);
            }

            Assert.True(File.Exists(path1), $"output not written: {path1}");
            Assert.True(File.Exists(path2), $"output not written: {path2}");

            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);

            Assert.True(bytes1.Length > 0, $"empty output: {path1}");
            Assert.True(bytes1.SequenceEqual(bytes2),
                $"{ext} bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
```

**Phase 25 substitutions:**
- `namespace FlowLang.Tests.Integration.Phase18` → `namespace FlowLang.Tests.Integration.Phase25`
- `class ByteIdenticalShowcaseTests` → `class ByteIdenticalShowcaseGaussianTests`
- `phase18_showcase_run1.{ext}` → `phase25_showcase_run1.{ext}` (same for run2)
- Update the docstring's "FRAC-02 byte-identical determinism gate" reference to "DEFER-06" / "Phase 25 humanizeGaussian"

---

### `flow-lang.Tests/FlowScriptData.cs` — register sentinel for new smoke

**Role:** test fixture data (dictionary entry)
**Analog:** Phase 15 DX-09 entry at `FlowScriptData.cs:225-232`

**Existing entry pattern** (lines 225-232):
```csharp
// Phase 15 DX-09: euclidean 6-arg humanize overload, same-seed byte-identical.
// Wave 0 placeholder — Plan 06 replaces the body with euclidean humanize +
// writeMidi + byte-identical-two-runs check while preserving both sentinels.
["test_euclidean_humanize.flow"] = new[]
{
    "euclidean humanize seed=42: PASSED",
    "two runs byte-identical: PASSED",
},
```

**Phase 25 entry to add** (mirror the format):
```csharp
// Phase 25 DEFER-06: humanizeGaussian(Sequence, Double, Int) seeded Box-Muller.
// Wave 0 placeholder — implementation plan replaces the body with humanizeGaussian +
// writeMidi + byte-identical-two-runs check while preserving both sentinels.
["test_humanize_gaussian.flow"] = new[]
{
    "humanizeGaussian seed=42: PASSED",
    "two runs byte-identical: PASSED",
},
```

The dictionary lives in the same `ExpectedSuccessSentinels` (or equivalently named) collection alongside the Phase 15 entry. The fixture file already does file enumeration in `GetFlowScripts()` (lines 5-14) — no new wiring needed beyond the dictionary entry.

---

### `tests/test_humanize_gaussian.flow` (NEW)

**Role:** .flow smoke test (end-to-end seeded PRNG byte-identical check)
**Analog:** `tests/test_euclidean_humanize.flow` — verbatim structural mirror

**Full analog file** (entire `tests/test_euclidean_humanize.flow`):
```flow
use "@std"
use "@audio"

// Phase 15 DX-09 — 6-arg euclidean overload (swing + humanize + seed).
// Humanize = 0.1 adds ±0.1 uniform jitter to velocity; seed = 42 pins deterministic output.
// The FIRST render writes to run_a.mid and prints the 'seed=42' sentinel.
// The SECOND render writes to run_b.mid with identical seed; prints the 'byte-identical' sentinel.
// Plan 05's xUnit Fact (EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi) is the authoritative
// byte-level gate — this script asserts only that the two renders complete cleanly.
tempo 120 {
    timesig 4/4 {
        // Run A
        Sequence a = (euclidean 3 8 C4 0.3 0.1 42)
        section sa { a }
        Song songA = [sa]
        (writeMidi "tests/output/phase15_euclidean_humanize_a.mid" songA)
        (print "euclidean humanize seed=42: PASSED")

        // Run B — identical seed, must produce byte-identical MIDI to run A.
        Sequence b = (euclidean 3 8 C4 0.3 0.1 42)
        section sb { b }
        Song songB = [sb]
        (writeMidi "tests/output/phase15_euclidean_humanize_b.mid" songB)
        (print "two runs byte-identical: PASSED")
    }
}
```

**Phase 25 mirror pattern** — identical structure with `humanizeGaussian` substituted, plus a base sequence to feed the transform:
```flow
use "@std"
use "@audio"

// Phase 25 DEFER-06 — humanizeGaussian(Sequence, Double, Int) Box-Muller.
// amount = 0.1 + seed = 42 pins deterministic Gaussian-perturbed velocities.
// Run A writes to phase25_humanize_gaussian_a.mid; Run B with identical seed writes to
// phase25_humanize_gaussian_b.mid. Authoritative byte-level gate is
// flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs.
tempo 120 {
    timesig 4/4 {
        // Run A
        Sequence baseSeq = | C4q D4q E4q F4q |
        Sequence a = (humanizeGaussian baseSeq 0.1 42)
        section sa { a }
        Song songA = [sa]
        (writeMidi "tests/output/phase25_humanize_gaussian_a.mid" songA)
        (print "humanizeGaussian seed=42: PASSED")

        // Run B — identical seed, must produce byte-identical MIDI to run A.
        Sequence b = (humanizeGaussian baseSeq 0.1 42)
        section sb { b }
        Song songB = [sb]
        (writeMidi "tests/output/phase25_humanize_gaussian_b.mid" songB)
        (print "two runs byte-identical: PASSED")
    }
}
```

Output filename basenames `phase25_humanize_gaussian_{a,b}.mid` mirror the `phase15_euclidean_humanize_{a,b}.mid` precedent.

---

### `examples/showcase.flow` — additive `humanizeGaussian` call site

**Role:** docs/example (additive transform on `melody`)
**Analog:** the existing `pad = padBase -> crescendo 0.18 0.6` wrap pattern at `showcase.flow:13-14`. RESEARCH §Showcase Confirmation locks the target as `melody` at line 20 with seed `314`, amount `0.08`.

**Existing context** (showcase.flow lines 1-30 — relevant region):
```flow
use "@std"
use "@audio"
use "@composition"

(print "Flow Showcase — v1.2 Ambient Piece")
(print "Generating examples/output/flow_showcase.{wav,mid} ...")

tempo 72 {
    timesig 4/4 {
        key Aminor {

            // Slow pad bed — long whole notes outline the i / VI / iv / v shape
            Sequence padBase = | A3w | F3w | D3w | E3w |
            Sequence pad = padBase -> crescendo 0.18 0.6

            // Sparse euclidean pulse on the low A — humanized, fixed seed for byte-identical reruns
            Sequence pulse = (euclidean 5 16 A2 0.18 0.12 7)

            // Soft mezzo-piano melody floating above the bed
            Sequence melody = | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |  // ← TARGET: line 20

            // Long reverb tail wraps the whole piece
            reverbTime 3.2 {
                section atmosphere {
                    Sequence layerPad = pad
                    Sequence layerPulse = pulse
                    Sequence layerMelody = melody
                }

                Song showcase = [atmosphere]
                ...
```

**Modification** (per RESEARCH lines 514-519 — exact form):
```flow
// BEFORE (line 20):
Sequence melody = | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |

// AFTER:
Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w | 0.08 314)
```

**Important:** Single line replacement. Do NOT touch `pad`, `padBase`, `pulse`, the `reverbTime` block, or any `writeWav` / `writeMidi` calls. The S-expression form `(humanizeGaussian seq 0.08 314)` is canonical Flow style per project memory `feedback_language_philosophy.md`.

---

### `examples/tutorial.flow` — new `humanizeGaussian` chapter

**Role:** docs/example (new chapter appended after existing humanize content)
**Analog:** existing humanize chapter region at `tutorial.flow:567-577`. RESEARCH locates the existing humanize chapter at line 567 (verified by reading lines 555-594).

**Existing chapter format context** (lines 555-594 — the chapter structure to mirror):
```flow

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            // Quiet intro -- per-section gain shapes the dynamic arc
            gain 0.6 {
                section sunriseIntro {
                    Sequence chords = | [C4 E4 G4]h [F4 A4 C5]h |
                    Sequence bass = | C3h F3h |
                }
            }

            // Euclidean groove with humanize + seed=42 (byte-identical across runs)
            section sunriseGroove {
                Sequence drums = (euclidean 5 16 C3 0.2 0.1 42)
            }

            Note: A melodic verse
            section sunriseVerse {
                Sequence mel = | E4q G4q A4q G4q | C5q B4q A4q G4q |
                Sequence bass = | C3h G3h | A3h E3h |
            }
            ...
```

**Chapter pattern conventions extracted:**
- Comment header `// <one-line description>` precedes each `section` block
- `Note: <heading>` style for sub-section labels
- S-expression call style for built-ins: `(euclidean 5 16 C3 0.2 0.1 42)`
- Sequence assignment: `Sequence <name> = (<call>)` or `Sequence <name> = | ... |`

**Phase 25 chapter to append** (per CONTEXT D-22 + `<specifics>` lines 191-194 — uniform vs Gaussian contrast):
```flow

// Gaussian humanize: bell-curve velocity perturbation with deterministic seed.
// Same `amount` parameter as uniform humanize (D-07), but z ~ N(0, 1) means most
// jitter clusters near zero with occasional larger excursions — sounds more "human."
section humanizeGaussianChapter {
    Sequence myMelody     = | C4q D4q E4q F4q |
    Sequence uniformFeel  = (humanize myMelody 0.1)              // flat jitter (non-deterministic)
    Sequence naturalFeel  = (humanizeGaussian myMelody 0.1 42)   // bell jitter, seed=42 → byte-identical
}
```

Append after the existing humanize chapter content (insertion point: ~line 567 area, after the existing `sunriseGroove` section that demonstrates uniform humanize via euclidean's 6-arg form).

---

## Shared Patterns

### LOCAL `new Random(seed)` per-call PRNG isolation

**Source 1:** `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77`
```csharp
private static Value VarySeeded(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
}
```

**Source 2 (with explanatory comment):** `flow-lang/StandardLibrary/BuiltInFunctions.cs:1256-1258`
```csharp
// D-17: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
// ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
var rng = new Random(seed);
```

**Apply to:** `HumanizeGaussian` method body. The comment block (citing both sources) MUST appear immediately above `var rng = new Random(seed);`. This guards against Pitfall 6 (any unseeded PRNG draw breaks byte-identity) and Pitfall 3 (showcase byte-identity coupling).

### `Math.Clamp(velocity, 0.05, 1.0)` velocity clamp

**Source:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:894`
```csharp
double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);
```

**Apply to:** `HumanizeGaussian` non-rest branch (D-09). `0.05` lower bound (NOT 0.0) prevents inaudible "ghost" notes that would silently drop in MIDI export.

### `Math.Clamp(amount, 0.0, 1.0)` amount clamp

**Source:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:878`
```csharp
double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
```

**Apply to:** `HumanizeGaussian` argument unpacking (D-08). Silent clamp per charitable-interpretation memory.

### Rest passthrough loop guard

**Source:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:886-890`
```csharp
if (note.IsRest)
{
    newNotes.Add(note);
    continue;
}
```

**Apply to:** `HumanizeGaussian` per-note loop body (D-11). Critical for determinism: rests do not consume PRNG state, so determinism stays insensitive to rest density.

### `[Collection("FlowScripts")]` test class attribute

**Source:** `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:29-30`, `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:17-18`, `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs:26-27`

```csharp
[Collection("FlowScripts")]
public class EuclideanSwingTests
```

**Apply to:** Both new Phase 25 test classes (`HumanizeGaussianFacts` and `ByteIdenticalShowcaseGaussianTests`). Required to avoid parallel-execution conflicts on shared `FlowScriptData` / `FlowEngineRunner` resources.

### `private const double Tol = 1e-9; private const double BaseVelocity = 0.63;`

**Source:** `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:32-33`
```csharp
private const double BaseVelocity = 0.63;
private const double Tol = 1e-9;
```

**Apply to:** `HumanizeGaussianFacts` deterministic-pin Fact + cross-seed-difference Fact. RESEARCH §Wave 0 gaps explicitly require these constants.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `NextGaussianSample(Random rng)` private static helper | utility (math) | pure function | No prior private static math helper exists in `TransformFunctions.cs`. Closest distant analog is `Bjorklund(int hits, int steps)` at `BuiltInFunctions.cs` (private static algorithmic helper) but the role is different (returns `bool[]` pattern, not a single double). The Gaussian helper is greenfield. RESEARCH §Pattern 2 supplies the canonical body; recommended location is at the bottom of the `TransformFunctions` class after the new `// ===== Humanize Gaussian =====` block. |

## Metadata

**Analog search scope:**
- `flow-lang/StandardLibrary/Transforms/` (sibling for HumanizeGaussian)
- `flow-lang/StandardLibrary/Composition/` (VariationFunctions for LOCAL Random precedent)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (euclidean 6-arg D-17 precedent + comment style)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (With(...) helper extension target)
- `flow-lang/std.flow` (proc declaration site)
- `flow-lang.Tests/Unit/Phase15/` (deterministic-pin Fact pattern)
- `flow-lang.Tests/Unit/Phase22/` (header docstring style for D-ID-anchored Facts)
- `flow-lang.Tests/Integration/Phase18/` (two-runner byte-identical pattern)
- `flow-lang.Tests/FlowScriptData.cs` (sentinel registration pattern)
- `tests/test_euclidean_humanize.flow` + `tests/test_humanize.flow` (smoke .flow analogs)
- `examples/showcase.flow`, `examples/tutorial.flow` (example modification analogs)

**Files scanned:** 12 files read (each within token budget; no file re-read)
**Pattern extraction date:** 2026-05-04
