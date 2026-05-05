# Phase 23: Microtonal Tuning (Wedge) — Pattern Map

**Mapped:** 2026-05-03
**Files analyzed:** 18 (8 new, 10 modified)
**Analogs found:** 18/18 (every new/modified file has a strong existing analog)

## File Classification

### New Files

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs` | enum (closed-set type) | static lookup | `flow-lang/Lexing/TokenType.cs:6-78` | exact (closed enum house style) |
| `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs` | enum (closed-set type) | static lookup | `flow-lang/Lexing/TokenType.cs:6-78` | exact |
| `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` | static config / data table | static lookup | `flow-lang/Lexing/PragmaRegistry.cs:16-20` (closed dict + IsKnown lookup); `flow-lang/Runtime/MusicalContext.cs:14-33` (static `HashSet`/`Dictionary` literal) | exact |
| `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs` | utility (math helpers) | transform (ratio → Hz) | `flow-lang/StandardLibrary/Audio/PitchConversion.cs:13-17,34-50` | exact (math-only static helper class) |
| `flow-lang/Diagnostics/RenderingDiagnostics.cs` | utility (one-shot stderr warning) | event (per-session dedup) | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:276-279,297-298` (warning style); no existing dedup helper — net-new | role-match (warning style proven; dedup HashSet is net-new but trivial) |
| `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` | test (xUnit Fact) | request-response | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` | exact (Fact-per-canary ratio template) |
| `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` | test (xUnit Fact + integration) | request-response | `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` + `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` | exact |
| `flow-lang.Tests/Unit/Phase23/ParseKeyNameFacts.cs` | test (xUnit Theory) | request-response | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` (Fact rows) | role-match |
| `flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs` | test (capture-stderr Fact) | event-driven | `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (uses `FlowEngineRunner` for stderr) | exact |
| `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs` | test (xUnit Theory) | request-response | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` | role-match |
| `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` | test (integration determinism) | file-I/O | `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` | exact |
| `tests/test_tuning_ji.flow` | smoke (.flow script) | request-response | `tests/test_h_alias.flow` | exact (pragma + Note: comments + smoke print) |
| `tests/test_tuning_pythagorean.flow` | smoke | request-response | `tests/test_h_alias.flow` | exact |
| `tests/test_tuning_equal.flow` | smoke (D-08 explicit no-op) | request-response | `tests/test_h_alias.flow` | exact |
| `tests/test_tuning_determinism.flow` | smoke (byte-identical pin) | file-I/O | `tests/test_h_alias.flow` | role-match |

### Modified Files

| Modified File | Role | Change Shape | Pattern Being Extended |
|---------------|------|--------------|------------------------|
| `flow-lang/StandardLibrary/Audio/PitchConversion.cs:13-50` | utility | add tuning-aware overload; existing 1-arg + 3-arg paths UNCHANGED | static method overload addition (existing 1-arg `NoteToFrequency(MusicalNoteData)` already delegates to 3-arg `NoteToFrequency(char,int,int)`) |
| `flow-lang/Runtime/MusicalContext.cs:35-62` | runtime state | add `Tuning` property + extend `Clone()` | repeated `Type? Property { get; set; }` pattern (8 existing properties) |
| `flow-lang/Core/FlowEngine.cs:59-101` | orchestrator | insert pragma → `_context.SetTuning` bridge between parse (line 82) and interpret (line 92) | linear pipeline insertion |
| `flow-lang/Lexing/PragmaRegistry.cs:16-20` | config | add 3 entries to `KnownPragmas` dictionary | closed-set growth (Phase 21 D-17 reservation) |
| `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:154-191` | parser helper | extend `TryParseKey` `EndsWith("major")/("minor")` chain with 5 church-mode suffixes | additive `else if` ladder |
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:40-85` | context-dependent built-in | add D-11 one-shot warning at top of `Enharmonic`; existing logic unchanged | additive guard call before existing body |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs:127-137` | flow built-in | add D-13 one-shot warning at top of `WriteMidi`; needs migration to context-dependent registration | parallels Phase 14 `RegisterContextDependent` migration of `enharmonic` |
| `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:12,25,65,106,147` AND `Synthesizers/*.cs` | render layer | (Pattern A only) extend `INoteSynthesizer.RenderNote` signature with `RenderTuning tuning` param; pass through to `PitchConversion.NoteToFrequency(note, tuning)` | additive parameter on interface + ~12 implementer call sites |
| `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs:59` | render layer | (Pattern A only) update `NoteToFrequency` call to pass tuning | one-line call-site update |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs:121-153` | render orchestrator | (Pattern A only) resolve `RenderTuning` from `section.Context` once per section, thread into synthesizer | parallels existing `bpm/pan/gain/rt60` resolution at lines 128-134 |

## Pattern Assignments

### `flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs` (enum, static lookup)

**Analog:** `flow-lang/Lexing/TokenType.cs:6-78` — closed enum, simple flat list, no values assigned.

**Imports + namespace pattern** (TokenType.cs lines 1-6):
```csharp
namespace FlowLang.Lexing;

/// <summary>
/// Types of tokens in the Flow language.
/// </summary>
public enum TokenType
{
```

**Closed-enum body shape** (TokenType.cs lines 7-12, abridged):
```csharp
public enum TokenType
{
    // Keywords
    Proc,
    EndProc,
    Return,
    ...
}
```

**What to copy:** file-scoped namespace, single XML doc comment above the enum, comma-separated members one per line, group-comments allowed (`// Keywords`, `// Literals`). Apply directly to `TuningSystem` (3 members: `EqualTemperament`, `JustIntonation`, `Pythagorean`) and `Mode` (7 members: `Major`, `Minor`, `Dorian`, `Phrygian`, `Lydian`, `Mixolydian`, `Locrian`).

---

### `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` (static class, data table)

**Analog A:** `flow-lang/Lexing/PragmaRegistry.cs:16-23` — `IReadOnlyDictionary` initialized in field initializer; `IsKnown` accessor.

**Closed-dict shape** (PragmaRegistry.cs lines 16-23):
```csharp
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation)."
    };

/// <summary>True iff <paramref name="name"/> is a recognized pragma.</summary>
public static bool IsKnown(string name) => KnownPragmas.ContainsKey(name);
```

**Analog B:** `flow-lang/Runtime/MusicalContext.cs:14-33` — static readonly initialized literal collection.

**Static collection shape** (MusicalContext.cs lines 14-33):
```csharp
public static readonly HashSet<string> ValidKeys = new(StringComparer.OrdinalIgnoreCase)
{
    "Cmajor", "Cminor",
    "Csharpmajor", "Csharpminor",
    ...
};
```

**What to copy:**
- `public static readonly Dictionary<(TuningSystem, Mode), ChromaticRatioTable> Tables = new() { [...] = ..., ... };`
- One `ChromaticRatioTable` per `(TuningSystem, Mode)` key.
- Lookup accessor `public static double LookupRatio(TuningSystem sys, Mode mode, char letter, int alteration)`.
- Doc-comment cite the canonical sources verbatim (Wikipedia Five-limit tuning + Mudcat for mode tables) per Pitfall 2 mitigation.

---

### `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs` (utility, transform)

**Analog:** `flow-lang/StandardLibrary/Audio/PitchConversion.cs:6-51` — pure-static math helper class, no state.

**Static math helper shape** (PitchConversion.cs lines 4-17):
```csharp
namespace FlowLang.StandardLibrary.Audio
{
    public static class PitchConversion
    {
        /// <summary>
        /// Converts a musical note to its frequency in Hz.
        /// Uses the formula: freq = 440 * 2^((midiNote - 69) / 12)
        /// where A4 = 440 Hz (MIDI note 69)
        /// </summary>
        public static double NoteToFrequency(char noteName, int octave, int alteration)
        {
            int midiNote = GetMidiNote(noteName, octave, alteration);
            return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
        }
```

**What to copy:** file-scoped namespace, `public static class`, each method gets its own XML doc with the math formula written out, `Math.Pow(2.0, x/1200.0)` for cent-offset composition (mirrors the existing 12-TET formula style).

---

### `flow-lang/Diagnostics/RenderingDiagnostics.cs` (utility, one-shot warning)

**Analog (warning style):** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:276-279, 297-298` — two existing `Console.Error.WriteLine` warning sites.

**Warning emit pattern** (TransformFunctions.cs lines 273-280):
```csharp
if (midi < MIDI_MIN || midi > MIDI_MAX)
{
    int clamped = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
    Console.Error.WriteLine(
        $"Warning: transpose would put {NoteType.Format(note.NoteName, note.Octave, note.Alteration)} " +
        $"out of range (MIDI {midi}), clamping to MIDI {clamped}");
    midi = clamped;
}
```

**Cents warning** (TransformFunctions.cs lines 295-299):
```csharp
if (Math.Abs(cents - semitones * 100.0) > 0.01)
{
    Console.Error.WriteLine(
        $"Warning: transpose by {cents}c rounded to {semitones} semitones (not an exact multiple of 100c)");
}
```

**Net-new addition:** dedup HashSet wrapping. Per RESEARCH §Pitfall 5:
```csharp
namespace FlowLang.Diagnostics;

public static class RenderingDiagnostics
{
    private static readonly HashSet<string> _emitted = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public static void WarnOnce(string sentinelKey, string message)
    {
        lock (_lock)
        {
            if (!_emitted.Add(sentinelKey)) return;
        }
        Console.Error.WriteLine(message);
    }

    /// <summary>For tests — clear the dedup set between runs.</summary>
    internal static void ResetForTesting()
    {
        lock (_lock) { _emitted.Clear(); }
    }
}
```

**Note:** `flow-lang/Diagnostics/` already exists (contains `DiagnosticLevel.cs`, `ErrorReporter.cs`, `FlowError.cs`); namespace is `FlowLang.Diagnostics`.

---

### `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` (xUnit Fact)

**Analog:** `flow-lang.Tests/Unit/Phase18/FractionTests.cs` — Fact-per-canary-value template.

**Imports + class header** (FractionTests.cs lines 1-13):
```csharp
using System;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Unit.Phase18;

/// <summary>
/// FRAC-01 acceptance: Fraction rational-arithmetic primitive.
/// Pins canonical examples from REQUIREMENTS.md FRAC-01 + edge cases from
/// 18-RESEARCH.md §6 Pitfall 3 (zero denom) + Pattern 3 (sign normalization).
/// Per D-USER-03 ToString always emits "Num/Denom" (no special-casing 1/1).
/// </summary>
public class FractionTests
```

**Per-canary Fact pattern** (FractionTests.cs lines 15-20):
```csharp
[Fact]
public void TripletThirds_SumToOne()
{
    var third = new Fraction(1, 3);
    Assert.Equal(new Fraction(1, 1), third + third + third);
}
```

**What to copy:**
- `namespace FlowLang.Tests.Unit.Phase23;`
- One `[Fact]` per pinned canary (e.g., `JustMajor_CtoE_Is5to4`, `PythagoreanMajor_CtoE_Is81to64`, `JI_Eb_DistinctFrom_DSharp`, `CentOffsetIsAdditive`).
- Assert on **ratio**, not absolute Hz, per CONTEXT.md §Specifics: `Assert.Equal(5.0/4.0, ratio, precision: 10)` keeps the test resilient to A4 reference choice.

---

### `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` (xUnit Fact)

**Analog A (registry Facts):** `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:16-52`.

**Registry-Fact shape** (PragmaRegistryFacts.cs lines 16-29):
```csharp
[Fact]
public void IsKnown_HAsB_ReturnsTrue()
{
    Assert.True(PragmaRegistry.IsKnown("hAsB"));
}

[Fact]
public void IsKnown_UnknownName_ReturnsFalse()
{
    // justIntonation will land in Phase 23; not in the Phase 21 closed set.
    Assert.False(PragmaRegistry.IsKnown("justIntonation"));
    ...
}
```

**Analog B (end-to-end pragma exec):** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:38-58` — uses `FlowEngineRunner` to exercise the full pipeline.

**End-to-end Fact** (HAliasFacts.cs lines 38-58):
```csharp
[Fact]
public void HMatchesB_InNoteStream()
{
    using var runner = new FlowEngineRunner();
    var (ok, stdout, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Sequence seq = | H4q B4q |
(print (str seq))
");
    Assert.True(ok, $"expected clean parse + run; stderr: {stderr}");
    Assert.Equal(0, errorCount);
    Assert.Contains("Sequence[", stdout);
}
```

**What to copy:**
- `[Collection("FlowScripts")]` attribute for serialization (mandatory for any Fact that uses `FlowEngineRunner` — also serializes `RenderingDiagnostics` HashSet across tests).
- `using var runner = new FlowEngineRunner();` block.
- D-14 unknown-tuning test: assert error message **contains** the Scala-loader-deferral string.

---

### `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` (integration)

**Analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs:25-50`.

**Two-runner byte-identical pattern** (ByteIdenticalTutorialTests.cs lines 25-50):
```csharp
[Collection("FlowScripts")]
public class ByteIdenticalTutorialTests
{
    [Fact]
    public void Tutorial_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare(isMidi: false);
    }

    [Fact]
    public void Tutorial_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare(isMidi: true);
    }

    private static void RunTwiceAndCompare(bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "tutorial.flow");
        ...
```

**What to copy:** verbatim two-runner pattern. New Fact: `ExplicitEqualTemperament_ProducesIdenticalOutput` runs `tests/test_tuning_equal.flow` (which declares `enable equalTemperament;`) twice and proves byte-identical to a copy without the pragma.

---

### `tests/test_tuning_*.flow` (smoke scripts)

**Analog:** `tests/test_h_alias.flow:1-21` — pragma-driven smoke script.

**Smoke-script shape** (test_h_alias.flow full):
```flow
enable hAsB;

use "@std"

Note: DEFER-02/03 acceptance — H-as-B alias inside note streams (Phase 21 plan 21-02)
Note: With `enable hAsB;` declared, every B-shape works with H per D-14.

Note: Basic alias — H4q parses identically to B4q
Sequence basic = | H4q B4q |
(print (str basic))

...

(print "test_h_alias: PASSED")
```

**What to copy:**
- `enable XXX;` on line 1 (file-scope pragma).
- Blank line then `use "@std"`.
- `Note:` comments call out the requirement ID and decision (e.g., `Note: MICR-01 acceptance — JI 5:4 ratio`).
- Trailing `(print "test_tuning_ji: PASSED")` line — exit-zero is the gate.
- For `test_tuning_determinism.flow`: write a WAV via `writeWav` and verify presence (the byte-identical comparison itself happens in the xUnit integration test, which runs this script twice).

## Pattern A vs Pattern B Analogs (per RESEARCH §Pitfall 1 — planner decision)

The renderer reaching the active tuning is the one architectural decision RESEARCH explicitly flags as unresolved. Both patterns satisfy locked decisions. Below are the analog call-site shapes for each.

### Pattern A: Thread `RenderTuning` through `INoteSynthesizer.RenderNote`

**Existing interface signature** (`flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:10-13`):
```csharp
public interface INoteSynthesizer
{
    AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm);
}
```

**Existing call-site shape (every synthesizer follows this — sample SineSynthesizer.cs:20-25):**
```csharp
public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
{
    if (note.IsRest)
        return CreateSilence(sampleRate, durationBeats, bpm);

    double frequency = PitchConversion.NoteToFrequency(note);
```

**All synthesizer implementations using this exact call:**
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:25` (SineSynthesizer)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:65` (SawSynthesizer)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:106` (SquareSynthesizer)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:147` (TriangleSynthesizer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs:17`
- `flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs` (parallel call site)
- `flow-lang/StandardLibrary/Audio/Synthesizers/SaxSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/FluteSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs:59`

**Resolution site to thread tuning from** (`flow-lang/StandardLibrary/Audio/SongRenderer.cs:121-134`):
```csharp
private static AudioBuffer RenderSection(SectionData section, INoteSynthesizer synthesizer)
{
    double bpm = section.Context?.Tempo ?? DefaultBpm;
    double pan = section.Context?.Pan ?? 0.0;
    double gain = section.Context?.Gain ?? 1.0;
    double? rt60 = section.Context?.ReverbTime;
```

This is the **resolution site** that already pulls tempo/pan/gain/rt60 from `section.Context`. Pattern A adds `var renderTuning = ResolveRenderTuning(section.Context);` here and passes it through to each synthesizer. The pattern of pulling per-section state from `section.Context` is established and proven.

### Pattern B: Static `MusicalContext.Current` ambient accessor

**Searched the codebase: `MusicalContext.Current` does NOT exist** (verified via `grep -rn "MusicalContext.Current" flow-lang/` — zero hits).

**The actual ambient-context analog is** `flow-lang/Runtime/ExecutionContext.cs:186-213` — `GetMusicalContext()` walks the call stack:

```csharp
public MusicalContext GetMusicalContext()
{
    var resolved = new MusicalContext();
    foreach (var frame in _callStack)
    {
        if (frame.MusicalContext != null)
        {
            resolved.TimeSignature ??= frame.MusicalContext.TimeSignature;
            resolved.Tempo ??= frame.MusicalContext.Tempo;
            resolved.Swing ??= frame.MusicalContext.Swing;
            resolved.Key ??= frame.MusicalContext.Key;
            ...
        }
        ...
    }
    // Defaults
    resolved.TimeSignature ??= new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
    resolved.Tempo ??= 120.0;
    resolved.Swing ??= 0.5;
    return resolved;
}
```

**Closest existing static-accessor analog (any global mutable state):** none in `flow-lang/Runtime/`. The only static state is in `flow-lang/StandardLibrary/Audio/SynthUtils.Rng` (which `SongRenderer:96` resets between renders for byte-identical determinism — see the `ResetNoiseRng()` call). This is a precedent that any new ambient state needs an explicit reset hook.

**Pattern B implementation analog** would be a NEW property on `MusicalContext`:
```csharp
// in flow-lang/Runtime/MusicalContext.cs
public static TuningSystem? Current { get; set; }
```
…with `FlowEngine.Execute` setting it before interpretation and the `FlowEngine` constructor / `Dispose` resetting it. **No existing analog for this shape — Pattern B is genuinely net-new for the codebase.**

**Recommendation per RESEARCH:** Pattern A (no precedent for static mutable ambient accessor; clear precedent for threading per-section state from `section.Context`). The "10-file mechanical churn" is bounded by the synthesizer interface; testing is dramatically simpler.

---

### `flow-lang/StandardLibrary/Audio/PitchConversion.cs` (modify — add tuning-aware overload)

**Existing pattern** (PitchConversion.cs:6-28):
```csharp
public static class PitchConversion
{
    public static double NoteToFrequency(char noteName, int octave, int alteration)
    {
        int midiNote = GetMidiNote(noteName, octave, alteration);
        return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    /// <summary>
    /// Overload that takes a MusicalNoteData object.
    /// </summary>
    public static double NoteToFrequency(MusicalNoteData note)
    {
        if (note.IsRest)
            return 0.0; // Rests have no frequency

        return NoteToFrequency(note.NoteName, note.Octave, note.Alteration);
    }
}
```

**What stays unchanged:** Both existing overloads (3-arg `(char, int, int)` and 1-arg `(MusicalNoteData)`) are NOT modified. Phase 23 ADDS new overloads accepting a `RenderTuning` (Pattern A) — the 12-TET path short-circuits to the old code literally per Pitfall 6.

**What's added:** new overload(s) following the exact same XML-doc + early-return pattern. The first line of the new overload — `if (note.IsRest) return 0.0;` — is copied verbatim from line 24-25.

---

### `flow-lang/Runtime/MusicalContext.cs` (modify — add `Tuning` property)

**Existing pattern** (MusicalContext.cs:35-62):
```csharp
public TimeSignatureData? TimeSignature { get; set; }
public double? Tempo { get; set; }
public double? Swing { get; set; }
public string? Key { get; set; }
public double? Velocity { get; set; }
public double? Pan { get; set; }
public double? Gain { get; set; }
public double? ReverbTime { get; set; }

public MusicalContext() { }

public MusicalContext Clone() => new()
{
    TimeSignature = TimeSignature,
    Tempo = Tempo,
    Swing = Swing,
    Key = Key,
    Velocity = Velocity,
    Pan = Pan,
    Gain = Gain,
    ReverbTime = ReverbTime
};
```

**What's added:** `public TuningSystem? Tuning { get; set; }` — same `Type? Property { get; set; }` shape as existing 8 properties. Add corresponding line to `Clone()`. Per D-05 the property is top-level non-stacked, but the shape on the type matches the existing properties exactly. Also extend `ToString()` (lines 97-109) and `ExecutionContext.GetMusicalContext()` (lines 186-213 of ExecutionContext.cs) to merge `Tuning` symmetrically.

---

### `flow-lang/Core/FlowEngine.cs` (modify — pragma → tuning bridge)

**Existing pipeline** (FlowEngine.cs:59-101):
```csharp
public bool Execute(string source, string? fileName = null)
{
    _errorReporter.Clear();
    try
    {
        // 0. Pre-lex: extract file-scope pragmas (Phase 21 D-01).
        var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, fileName, _errorReporter);
        if (_errorReporter.HasErrors) return false;

        // 1. Lex transformed source into tokens (pragmaSet wired for Plan 21-02).
        var lexer = new SimpleLexer(transformedSource, _errorReporter, fileName, pragmaSet);
        var tokens = lexer.Tokenize();
        if (_errorReporter.HasErrors) return false;

        // 2. Parse tokens into AST (pragmaSet attached to Program per D-08).
        var parser = new Parser(tokens, _errorReporter, pragmaSet);
        var program = parser.Parse();
        if (_errorReporter.HasErrors) return false;

        // 3. Type check AST (skipped for now - types checked at runtime)
        _diagnosticOutput?.WriteLine($"[verbose] Executing {fileName ?? "<eval>"}");

        // 4. Interpret AST
        _interpreter.Execute(program);
        return !_errorReporter.HasErrors;
    }
    ...
}
```

**Insertion point:** between line 85 (parser produces `program`, including `program.Pragmas`) and line 92 (`_interpreter.Execute(program)`). Add a numbered step "3.5 Apply tuning pragma" calling a new `ApplyTuningPragma(program)` private method per RESEARCH §Code Examples.

**Key D-07 detail:** pragma absence MUST NOT reset previously-resolved tuning (REPL persistence) — only OVERWRITE on explicit pragma.

---

### `flow-lang/Lexing/PragmaRegistry.cs` (modify — 3 new entries)

**Existing pattern** (PragmaRegistry.cs:16-20):
```csharp
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation)."
    };
```

**What's added:** 3 keys — verbatim per RESEARCH §Code Examples §Pragma registration. The accessor methods `IsKnown`, `AlphabetizedKnownNames`, `SuggestNearest` (lines 22-54) are completely unchanged — they iterate `KnownPragmas.Keys` so they pick up the new entries automatically. D-14 unknown-tuning error message extension lives in the **PragmaScanner** (not here) since that's where the unknown-pragma error is built — see Phase 21 D-12 for the existing error path.

---

### `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` (modify — `TryParseKey` extension)

**Existing pattern** (ScaleDatabase.cs:154-191):
```csharp
private static bool TryParseKey(string keyName, out string? rootNote, out bool isMajor)
{
    rootNote = null;
    isMajor = true;

    if (string.IsNullOrEmpty(keyName))
        return false;

    string lower = keyName.ToLowerInvariant();

    if (lower.EndsWith("major"))
    {
        isMajor = true;
        rootNote = keyName[..^5];
    }
    else if (lower.EndsWith("minor"))
    {
        isMajor = false;
        rootNote = keyName[..^5];
    }
    else
    {
        return false;
    }

    if (rootNote.Length == 0) return false;
    rootNote = char.ToUpper(rootNote[0]) + rootNote[1..].ToLower();

    if (!NoteToSemitone.ContainsKey(rootNote))
    {
        rootNote = null;
        return false;
    }

    return true;
}
```

**What's extended:** the `EndsWith` chain. Per D-04, the `out bool isMajor` return shape is too narrow for 7 modes — the planner must promote it to `out Mode mode` (or add a sibling `TryParseKeyWithMode`). Per CONTEXT.md `<canonical_refs>` line 98, callers at lines 164, 169, 196 use `isMajor` and need parallel updates.

**Suggested shape (mirroring the existing else-if chain):**
```csharp
if (lower.EndsWith("major"))      { mode = Mode.Major;      rootNote = keyName[..^5]; }
else if (lower.EndsWith("minor")) { mode = Mode.Minor;      rootNote = keyName[..^5]; }
else if (lower.EndsWith("dorian"))     { mode = Mode.Dorian;     rootNote = keyName[..^6]; }
else if (lower.EndsWith("phrygian"))   { mode = Mode.Phrygian;   rootNote = keyName[..^8]; }
else if (lower.EndsWith("lydian"))     { mode = Mode.Lydian;     rootNote = keyName[..^6]; }
else if (lower.EndsWith("mixolydian")) { mode = Mode.Mixolydian; rootNote = keyName[..^10]; }
else if (lower.EndsWith("locrian"))    { mode = Mode.Locrian;    rootNote = keyName[..^7]; }
else return false;
```

---

### `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (modify — D-11 warning + reuse `GetInKeyEnharmonic`)

**Existing pattern (context-dependent registration)** (HarmonyFunctions.cs:21-26):
```csharp
public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    var enharmonicSig = new FunctionSignature("enharmonic", [NoteType.Instance]);
    registry.Register("enharmonic", enharmonicSig, args => Enharmonic(args, context));
}
```

**Existing `Enharmonic` body — context-aware** (HarmonyFunctions.cs:40-48):
```csharp
private static Value Enharmonic(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
{
    string noteStr = args[0].As<string>();
    var (letter, octave, alteration) = NoteType.Parse(noteStr);

    int inputMidi = NoteType.ToMidiNote(letter, octave, alteration);
    var musicalCtx = context.GetMusicalContext();
    string? key = musicalCtx?.Key;
```

**What's added (D-11):** at the very top of `Enharmonic` (after retrieving `musicalCtx`), insert:
```csharp
if (musicalCtx?.Tuning is TuningSystem t && t != TuningSystem.EqualTemperament)
{
    RenderingDiagnostics.WarnOnce(
        "enharmonic-non-equal-temperament",
        "[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)");
}
// ... existing logic continues unchanged
```

**What stays unchanged:** Everything from line 50 onwards — the in-key branch (`TryEnharmonicInKey`), the natural-edge switch, the no-key sharp/flat flip. D-12 reuses `GetInKeyEnharmonic` from the renderer side; the function itself doesn't change.

---

### `flow-lang/StandardLibrary/Audio/MidiExport.cs` (modify — D-13 warning + context migration)

**Existing pattern (context-FREE registration)** — `WriteMidi` is registered without `ExecutionContext` access (per RESEARCH §Code Examples and `BuiltInFunctions.cs:111` line for `RegisterContextDependentFunctions` at line 785-792).

**Existing `WriteMidi` body** (MidiExport.cs:124-137):
```csharp
public static Value WriteMidi(IReadOnlyList<Value> args)
{
    string filepath = args[0].As<string>();
    var song = args[1].As<SongData>();

    if (string.IsNullOrWhiteSpace(filepath))
        throw new ArgumentException("MIDI filepath cannot be null or empty");

    ExportMidiInternal(filepath, song);
    return Value.Void();
}
```

**Migration analog (verified pattern):** `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:21-26` shows the registration shape `args => Enharmonic(args, context)`. To get `MusicalContext.Tuning` access, `WriteMidi` migrates from `RegisterAllImplementations` to `RegisterContextDependentFunctions` (BuiltInFunctions.cs:785-792 already lists the four migrated registrars: `SongRenderer`, `HarmonyFunctions`, `EffectsFunctions`, `TransformFunctions`). Same migration shape for `MidiExport`.

**What's added (D-13):** at the top of `WriteMidi` (after `args`-parsing but before `ExportMidiInternal`), insert the same `RenderingDiagnostics.WarnOnce` guard as D-11 with key `"writemidi-non-equal-temperament"` and message `"[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI deferred to v1.4)"`.

**What stays unchanged:** All of `ExportMidiInternal` (lines 144+) — the actual MIDI bytes are 12-TET in this phase per D-13.

---

## Shared Patterns

### Pattern S1: One-Shot Stderr Warning Style

**Source:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:276-279, 297-298`

**Apply to:** D-11 (`enharmonic` non-12-TET), D-13 (`writeMidi` non-12-TET).

**Excerpt (canonical reference):**
```csharp
Console.Error.WriteLine(
    $"Warning: transpose would put {NoteType.Format(note.NoteName, note.Octave, note.Alteration)} " +
    $"out of range (MIDI {midi}), clamping to MIDI {clamped}");
```

**Departure for Phase 23:** wrap with `RenderingDiagnostics.WarnOnce(sentinelKey, message)` to dedup per-session (matches per-session HashSet pattern from RESEARCH §Pitfall 5). Sentinel keys are stable strings (`"enharmonic-non-equal-temperament"`, `"writemidi-non-equal-temperament"`). Message prefix uses `[enharmonic]` / `[midi]` bracketed marker rather than the existing `Warning:` prefix — this is intentional per CONTEXT.md D-11 quoted text.

### Pattern S2: Closed-Set Lookup (enum + static dict)

**Sources:**
- `flow-lang/Lexing/TokenType.cs:6-78` — closed enum (78 members).
- `flow-lang/Lexing/PragmaRegistry.cs:16-23` — closed `IReadOnlyDictionary` + `IsKnown`.

**Apply to:** `TuningSystem`, `Mode`, `TuningTables.Tables`.

### Pattern S3: Context-Dependent Registration

**Source:** `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:21-26` + call site `flow-lang/StandardLibrary/BuiltInFunctions.cs:785-792`.

**Apply to:** `MidiExport.WriteMidi` migration (D-13 needs context to read `MusicalContext.Tuning`).

**Excerpt (registration site)** (BuiltInFunctions.cs:785-792):
```csharp
public static void RegisterContextDependentFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    Audio.SongRenderer.RegisterContextDependent(registry, context);
    Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);
    Audio.EffectsFunctions.RegisterContextDependent(registry, context);
    Transforms.TransformFunctions.RegisterContextDependent(registry, context);
}
```

Phase 23 adds: `Audio.MidiExport.RegisterContextDependent(registry, context);` and ports `WriteMidi` to take `(args, context)`.

### Pattern S4: Per-Section Context Resolution

**Source:** `flow-lang/StandardLibrary/Audio/SongRenderer.cs:121-134`

**Apply to:** Pattern A — resolve `RenderTuning` once per section here, pass to synthesizer.

**Excerpt:**
```csharp
private static AudioBuffer RenderSection(SectionData section, INoteSynthesizer synthesizer)
{
    double bpm = section.Context?.Tempo ?? DefaultBpm;
    double pan = section.Context?.Pan ?? 0.0;
    double gain = section.Context?.Gain ?? 1.0;
    double? rt60 = section.Context?.ReverbTime;
```

### Pattern S5: Test Collection for Stateful Facts

**Source:** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:35` (`[Collection("FlowScripts")]`).

**Apply to:** All Phase 23 Facts that exercise `RenderingDiagnostics` (the dedup HashSet is process-global — parallel xUnit threads would race). Mandatory for `RenderingDiagnosticsFacts.cs`, `PragmaTuningFacts.cs`, `ByteIdenticalDefaultTuningTests.cs`. Add `RenderingDiagnostics.ResetForTesting()` to test setup.

### Pattern S6: Two-Runner Byte-Identical Determinism

**Source:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs:25-50` — `RunTwiceAndCompare` helper.

**Apply to:** `ByteIdenticalDefaultTuningTests.cs` (D-08 explicit `enable equalTemperament;` byte-identity) and the `tests/test_tuning_determinism.flow` JI/Pythagorean independent pin. Reuses `FlowScriptData.FindTestsRoot()` for cwd portability.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (none) | — | — | Every new and modified file in Phase 23 has a strong existing analog in the codebase. The only genuinely net-new shape is `RenderingDiagnostics.WarnOnce` per-session-HashSet dedup, but its emit-style mirrors `TransformFunctions.cs:276-279` exactly — the dedup wrapper is a 5-line addition with no precedent needed. |

## Metadata

**Analog search scope:**
- `flow-lang/Lexing/` (PragmaRegistry, TokenType)
- `flow-lang/Runtime/` (MusicalContext, ExecutionContext)
- `flow-lang/Core/` (FlowEngine)
- `flow-lang/StandardLibrary/Audio/` (PitchConversion, NoteSynthesizer, SongRenderer, MidiExport, Synthesizers/, Vocalization/)
- `flow-lang/StandardLibrary/Harmony/` (ScaleDatabase, HarmonyFunctions)
- `flow-lang/StandardLibrary/Transforms/` (TransformFunctions)
- `flow-lang/Diagnostics/` (existing dir)
- `flow-lang.Tests/Unit/Phase18/` (FractionTests)
- `flow-lang.Tests/Unit/Phase21/` (PragmaRegistryFacts, HAliasFacts)
- `flow-lang.Tests/Integration/Phase18/` (ByteIdenticalTutorialTests)
- `tests/` (test_h_alias.flow, test_chords.flow, test_pragma_isolation.flow)

**Files scanned:** ~25 (verified via Read tool — no full-file scans of large >2000-line files; targeted line-range reads only).

**Pattern extraction date:** 2026-05-03

**Pattern A vs Pattern B:** Documented analogs for both. Pattern A has clear precedent in the per-section-context-resolution pattern at `SongRenderer.cs:121-134` and zero codebase analogs for ambient mutable state outside `SynthUtils.Rng` (which itself has an explicit `ResetNoiseRng()` reset hook at line 96 — precedent that any global state needs explicit lifecycle management). Pattern B is genuinely net-new for the codebase. Per RESEARCH §Pitfall 1 the planner should pick Pattern A unless they have a strong reason to add ambient state.
