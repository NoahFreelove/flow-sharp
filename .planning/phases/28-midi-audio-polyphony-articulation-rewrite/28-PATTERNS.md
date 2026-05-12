# Phase 28 — Pattern Map

For each new or modified file in Phase 28's scope, the closest existing analog plus concrete code excerpts the planner/executor should mirror.

## File-by-File Pattern Map

### NEW: `flow-lang/Ast/Expressions/VoiceBlockElement.cs` (or extend existing NoteStreamElement record union)
**Role:** AST node for `{voice ...}` block inside a note stream
**Closest analog:** `TupletElement` in the same union — also a brace-delimited element with children
**Pattern reference:**
```csharp
// Existing TupletElement form (Parser.NoteStream.cs:198, used in switch in NoteStreamCompiler)
new TupletElement(elemLoc, n, denominator, children, durSuffix, isDottedTuplet)
```
**For Phase 28:**
```csharp
public record VoiceBlockElement(SourceLocation Location, IReadOnlyList<NoteStreamElement> Children) : NoteStreamElement(Location);
```

### MODIFIED: `flow-lang/Parsing/Parser.NoteStream.cs`
**Role:** Add `{voice ...}` brace dispatch and `leg` articulation token
**Closest analog:** Existing `{N:M ...}q` tuplet brace dispatch (lines 153-200) and existing articulation switch (lines 397-421)
**Pattern reference:**
```csharp
// Existing tuplet brace dispatch (Parser.NoteStream.cs:153)
if (Check(TokenType.LBrace))
{
    var elemLoc = CurrentToken.Location;
    Advance(); // consume {
    var nToken = Expect(TokenType.IntLiteral, "Expected integer N in tuplet bracket");
    // ...
}
```
**For Phase 28 — peek at next token to disambiguate:**
```csharp
if (Check(TokenType.LBrace))
{
    var elemLoc = CurrentToken.Location;
    Advance(); // consume {
    // Peek for "voice" keyword to disambiguate from tuplet
    if (Check(TokenType.Identifier) && CurrentToken.Text == "voice")
    {
        Advance(); // consume "voice"
        var children = ParseVoiceBlockChildren(); // mirrors ParseTupletChildren
        Expect(TokenType.RBrace, "Expected '}' to close voice block");
        currentBarElements.Add(new VoiceBlockElement(elemLoc, children));
        continue;
    }
    // else fall through to tuplet parser as today
    var nToken = Expect(TokenType.IntLiteral, "Expected integer N in tuplet bracket");
    // ...
}
```

For the `leg` token, mirror existing `stacc`/`ten`/`marc` switch in `TryParseArticulation` (lines 404-418):
```csharp
case "leg": Advance(); return Articulation.Legato;
```

### MODIFIED: `flow-lang/Runtime/NoteStreamCompiler.cs`
**Role:** Compile `VoiceBlockElement` into parallel `BarData` per voice block; apply locked articulation rules
**Closest analog:** `CompileNoteElement` (lines 627-682) for per-note compilation; `CompileTupletElement` (similar pattern) for brace-delimited compilation
**Pattern reference (locked rules section, lines 668-675):**
```csharp
// Existing velocity boost — Phase 28 expands to all 6 articulations + Legato
if (articulation == Articulation.Accent)
    velocity = Math.Min(velocity + 0.2, 1.0);
else if (articulation == Articulation.Marcato)
    velocity = Math.Min(velocity + 0.3, 1.0);
else if (articulation == Articulation.Sforzando)
    velocity = 0.95;
```
**For Phase 28:**
```csharp
// Locked velocity adjustments per SPEC requirement #4
switch (articulation)
{
    case Articulation.Accent:    velocity = Math.Min(velocity + 0.30, 1.0); break;
    case Articulation.Marcato:   velocity = Math.Min(velocity + 0.30, 1.0); break;
    // Sforzando: NO scalar velocity bump here — envelope shaper applies time-varying spike
    // Legato / Tenuto / Staccato / Normal: velocity unchanged
}
```

### MODIFIED: `flow-lang/StandardLibrary/Audio/BarRenderer.cs`
**Role:** Drop-in locked duration multipliers; voice-block onset preservation
**Closest analog:** Existing duration switch (lines 67-77) — Phase 28 replaces constants
**Pattern reference:**
```csharp
// Existing — to be replaced
case Articulation.Staccato: durationBeats *= 0.5; break;
case Articulation.Marcato:  durationBeats *= 0.8; break;
```
**For Phase 28 (per SPEC #4 LOCKED rules):**
```csharp
switch (note.Articulation)
{
    case Articulation.Staccato: durationBeats *= 0.25; break;
    case Articulation.Marcato:  durationBeats *= 0.25; break; // Marcato = Staccato + Accent
    case Articulation.Legato:   durationBeats *= 1.10; break; // 10% overlap into next note
    case Articulation.Tenuto:   /* × 1.00 — no change */ break;
    case Articulation.Accent:   /* × 1.00 */ break;
    case Articulation.Sforzando:/* × 1.00 — envelope shaper handles spike */ break;
}
```

For voice-block parallel rendering, mirror the existing `RenderBarsToVoices` pattern (lines 132-163) but with `currentOffset` SHARED across all voice blocks within one bar (instead of accumulated).

### NEW: `flow-lang/StandardLibrary/Audio/Synthesizers/SynthUtils.cs` — `GenerateArticulationADSR` helper
**Role:** Single shared helper that all 9 synths call to get the articulation-shaped envelope
**Closest analog:** Existing `SynthUtils.GenerateADSR(attack, decay, sustain, release, frames, sampleRate)` (SynthUtils.cs:131-136)
**Pattern reference:**
```csharp
// Existing helper — Phase 28 builds the articulation-aware variant alongside it
public static float[] GenerateADSR(double attack, double decay, double sustain, double release, int frames, int sampleRate)
{
    var parameters = new double[] { attack, decay, sustain, release };
    var envelope = new Envelope(EnvelopeKind.ADSR, parameters, sampleRate);
    return EnvelopeProcessor.GenerateEnvelopeCurve(envelope, frames);
}
```
**For Phase 28:**
```csharp
public static float[] GenerateArticulationADSR(
    Articulation articulation,
    double baseAttack, double baseDecay, double baseSustain, double baseRelease,
    int frames, int sampleRate, bool isPercussion = false)
{
    if (isPercussion)
        return GenerateADSR(baseAttack, baseDecay, baseSustain, baseRelease, frames, sampleRate);

    double attack = baseAttack, decay = baseDecay, sustain = baseSustain, release = baseRelease;
    switch (articulation)
    {
        case Articulation.Staccato:
        case Articulation.Marcato:  // Marcato envelope = Staccato envelope
            attack  = baseAttack * 0.66;  // 1.5× faster
            sustain = 0.0;
            release = baseRelease * 0.5;
            break;
        case Articulation.Tenuto:
            release = baseRelease * 1.2;
            break;
        case Articulation.Legato:
            // Synth-default envelope; crossfade overlap happens at BarRenderer level via DurationOverlap
            break;
        case Articulation.Accent:
        case Articulation.Sforzando:
        case Articulation.Normal:
            // Synth-default
            break;
    }
    var curve = GenerateADSR(attack, decay, sustain, release, frames, sampleRate);

    // Sforzando: time-varying spike during first 15% of frames
    if (articulation == Articulation.Sforzando)
    {
        int spikeFrames = (int)(frames * 0.15);
        for (int i = 0; i < spikeFrames; i++)
        {
            float t = (float)i / spikeFrames;
            curve[i] *= 1.5f * (1.0f - t) + 1.0f * t;  // 1.5× → 1.0× linear decay
        }
    }
    return curve;
}
```

### MODIFIED: `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` (and 8 others)
**Role:** Replace `GenerateADSR` call with `GenerateArticulationADSR(note.Articulation, ...)`
**Closest analog:** Self (lines 51-53)
**Pattern reference:**
```csharp
// Existing in PianoSynthesizer.cs:51-53
float[] envelope = SynthUtils.GenerateADSR(
    attack: 0.003, decay: 0.6, sustain: 0.12, release: 0.3,
    frames: numSamples, sampleRate: sampleRate);
```
**For Phase 28:**
```csharp
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack: 0.003, baseDecay: 0.6, baseSustain: 0.12, baseRelease: 0.3,
    frames: numSamples, sampleRate: sampleRate);
```

DrumSynthesizer passes `isPercussion: true` to short-circuit articulation shaping.

### MODIFIED: `flow-lang/StandardLibrary/Audio/MidiExport.cs`
**Role:** Refactor single-track `noteTrackChunk` into N tracks (one per Sequence)
**Closest analog:** Self (lines 245-391); the existing for-each loop structure already iterates `sectionData.Sequences`
**Pattern reference (existing single-track structure):**
```csharp
// MidiExport.cs:245-251 — single track accumulating events from all sequences
var noteTrackChunk = new TrackChunk();
var noteEvents = new List<TimedEvent>();
noteEvents.Add(new TimedEvent(new ProgramChangeEvent((SevenBitNumber)0), 0));
```
**For Phase 28:**
```csharp
// Multi-track: one TrackChunk per uniqueSequenceName, keyed by name
var sequenceTracks = new Dictionary<string, (TrackChunk chunk, List<TimedEvent> events, int gmProgram, int channel)>(
    StringComparer.OrdinalIgnoreCase);

// Helper: map sequence-name prefix → GM program + channel
static (int gmProgram, int channel) ResolveGmProgram(string seqName)
{
    string lower = seqName.ToLowerInvariant();
    if (lower.StartsWith("piano")) return (0, 0);
    if (lower.StartsWith("brass") || lower.StartsWith("horn")) return (56, 0);
    if (lower.StartsWith("sax")) return (65, 0);
    if (lower.StartsWith("flute")) return (73, 0);
    if (lower.StartsWith("string")) return (48, 0);
    if (lower.StartsWith("organ")) return (19, 0);
    if (lower.StartsWith("bell")) return (14, 0);
    if (lower.StartsWith("drum")) return (0, 9);  // GM percussion = channel 9
    return (0, 0);  // default piano
}

// Inside the section/sequence loop, route events to the per-sequence track
foreach (var (seqName, sequence) in sectionData.Sequences)
{
    if (!sequenceTracks.TryGetValue(seqName, out var trackInfo))
    {
        var (gm, ch) = ResolveGmProgram(seqName);
        var newChunk = new TrackChunk();
        var newEvents = new List<TimedEvent> {
            new TimedEvent(new ProgramChangeEvent((SevenBitNumber)gm) { Channel = (FourBitNumber)ch }, 0)
        };
        trackInfo = (newChunk, newEvents, gm, ch);
        sequenceTracks[seqName] = trackInfo;
    }
    // ... emit NoteOn/NoteOff to trackInfo.events with the resolved channel
}

// Add all sequence tracks to the file in insertion order
foreach (var (_, info) in sequenceTracks)
{
    using var manager = info.chunk.ManageTimedEvents();
    manager.Objects.Add(info.events);
    midiFile.Chunks.Add(info.chunk);
}
```

### MODIFIED: `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs`
**Role:** Add `AllocateWithPool` method using steal-oldest policy
**Closest analog:** Self — existing `Allocate` method (lines 18-40) keeps loudest-N. Phase 28 adds parallel method using onset-based scheduling.
**Pattern reference (existing loudest-N):**
```csharp
public static List<Voice> Allocate(List<Voice> voices, int sampleRate, int maxVoices)
{
    if (voices.Count <= maxVoices) return voices;
    var sorted = voices
        .Select(v => (voice: v, peak: GetPeakAmplitude(v)))
        .OrderByDescending(x => x.peak)
        .ToList();
    // ...
}
```
**For Phase 28:**
```csharp
public static List<Voice> AllocateWithPool(List<Voice> voices, int sampleRate, int poolSize, double bpm)
{
    if (poolSize < 1 || poolSize > 256)
        throw new ArgumentOutOfRangeException(nameof(poolSize), $"Voice pool size must be in [1, 256], got {poolSize}");
    if (voices.Count <= poolSize) return voices;

    double secondsPerBeat = 60.0 / bpm;

    // Sort by onset (deterministic tiebreaker: original index)
    var ordered = voices
        .Select((v, i) => (voice: v, idx: i, onsetSec: v.OffsetBeats * secondsPerBeat))
        .OrderBy(x => x.onsetSec).ThenBy(x => x.idx)
        .ToList();

    // Walk in onset order; maintain active set
    var active = new List<(Voice voice, int idx, double endSec)>();
    var output = new List<Voice>();

    foreach (var (voice, idx, onsetSec) in ordered)
    {
        // Drop voices that ended before this onset
        active.RemoveAll(a => a.endSec <= onsetSec);

        if (active.Count >= poolSize)
        {
            // Steal oldest (smallest onset, then smallest idx)
            var oldest = active.OrderBy(a => a.endSec).First(); // earliest end = "stalest"
            // Truncate the oldest voice's buffer at onsetSec
            int truncFrames = (int)((onsetSec - (oldest.voice.OffsetBeats * secondsPerBeat)) * sampleRate);
            TruncateVoiceBuffer(oldest.voice, Math.Max(0, truncFrames));
            active.Remove(oldest);
        }

        double durSec = (double)voice.Buffer.Frames / sampleRate;
        active.Add((voice, idx, onsetSec + durSec));
        output.Add(voice);
    }

    return output;
}

private static void TruncateVoiceBuffer(Voice voice, int newFrameCount) { /* zero out frames beyond newFrameCount + apply 5ms fade */ }
```

### NEW: `flow-lang.Tests/Helpers/RmsRegressionTests.cs`
**Role:** RMS-windowed comparison helper class
**Closest analog:** Existing `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` (test-helper class with static methods invoked from xUnit Facts)
**Pattern reference:**
```csharp
// Existing test helper pattern — flow-lang.Tests/Fixtures/FlowEngineRunner.cs is a class with static helper methods
namespace FlowLang.Tests.Helpers;
public static class RmsRegressionTests
{
    public static void AssertRmsWithinTolerance(
        AudioBuffer rendered, string baselineWavPath,
        double windowMs = 100.0, double toleranceDb = 0.5,
        string? overrideReason = null)
    {
        var baseline = ReadWav(baselineWavPath);
        Assert.Equal(baseline.Frames, rendered.Frames); // exact frame count match
        // ... slice into windowMs windows, compute RMS each, assert dB diff
    }

    private static AudioBuffer ReadWav(string path) { /* RIFF parsing — reverse of FileIO.WriteWav */ }
}
```

### NEW: `examples/tests/ragtime_polyphony.flow` and `examples/tests/maple_leaf_opening.flow`
**Role:** Composer-authored synthetic + real-piece fixtures for manual UAT
**Closest analog:** Existing `examples/tutorial.flow` and `examples/showcase.flow` (top-level renderable scripts that produce WAV + MID via `writeWav` and `writeMidi`)
**Pattern reference:** open `examples/tutorial.flow` for the canonical structure (musical context blocks, sections, sequences, `writeWav("examples/output/...")`).

### MODIFIED: `flow-lang/Lexing/SimpleLexer.cs`
**Role:** Add `voicePool` keyword to identifier→token map at line 867-ish
**Closest analog:** Existing `reverbTime` keyword entry (line 867)
**Pattern reference:**
```csharp
// Existing keyword map (SimpleLexer.cs:847-887)
"reverbTime" => TokenType.ReverbTime,
```
**For Phase 28:**
```csharp
"voicePool" => TokenType.VoicePool,
```

Plus add `VoicePool` entry to the `TokenType` enum.

### MODIFIED: `flow-lang/Ast/Statements/MusicalContextStatement.cs`
**Role:** Add `VoicePool` to `MusicalContextType` enum
**Closest analog:** Self (line 8: existing enum with Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain, ReverbTime)
**Pattern reference:**
```csharp
public enum MusicalContextType { Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain, ReverbTime, VoicePool }
```

### MODIFIED: `flow-lang/Runtime/MusicalContext.cs`
**Role:** Add `VoicePoolSize` field to `MusicalContext`
**Closest analog:** Existing nullable fields like `Tempo`, `ReverbTime` (lines 42-49)
**Pattern reference:**
```csharp
// Existing
public double? ReverbTime { get; set; }
```
**For Phase 28:**
```csharp
public int? VoicePoolSize { get; set; }  // null = inherit; default 32 at usage site (per SPEC)
```

Don't forget the `Clone()` method (line 69) — copy the new field.

### MODIFIED: `flow-lang/Parsing/Parser.cs`
**Role:** Add `voicePool` dispatch to musical-context-statement family
**Closest analog:** Existing `reverbTime` dispatch (lines 138-144)
**Pattern reference:**
```csharp
// Existing reverbTime dispatch
if (Check(TokenType.ReverbTime) && _current + 1 < _tokens.Count
    && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
        or TokenType.Minus or TokenType.Plus))
{
    Advance();
    return ParseMusicalContextStatement(MusicalContextType.ReverbTime);
}
```
**For Phase 28:** mirror with VoicePool, integer-only (reject float):
```csharp
if (Check(TokenType.VoicePool) && _current + 1 < _tokens.Count
    && _tokens[_current + 1].Type is TokenType.IntLiteral)
{
    Advance();
    return ParseMusicalContextStatement(MusicalContextType.VoicePool);
}
```
And add a `case MusicalContextType.VoicePool:` to the switch in `ParseMusicalContextStatement` (line 502+) that parses an int literal only.

### MODIFIED: `flow-lang/Interpreter/Interpreter.cs`
**Role:** Add `case MusicalContextType.VoicePool:` to ExecuteMusicalContext
**Closest analog:** Existing `case MusicalContextType.ReverbTime:` (lines 251-260)
**Pattern reference:**
```csharp
// Existing
case MusicalContextType.ReverbTime:
{
    var rtVal = _evaluator.Evaluate(ctx.Value);
    double rt60 = rtVal.Type is IntType ? (double)rtVal.As<int>() : rtVal.As<double>();
    rt60 = Math.Min(rt60, 30.0);
    musicalCtx.ReverbTime = rt60;
    break;
}
```
**For Phase 28:**
```csharp
case MusicalContextType.VoicePool:
{
    var poolVal = _evaluator.Evaluate(ctx.Value);
    int poolSize = poolVal.As<int>();
    if (poolSize < 1 || poolSize > 256)
    {
        _errorReporter.ReportError(
            $"Voice pool size must be between 1 and 256, got {poolSize}", ctx.Location);
        break;
    }
    musicalCtx.VoicePoolSize = poolSize;
    break;
}
```

### Existing tests to migrate (Phase 18/25/27 byte-pin → RMS-window)
**Closest analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`
**Migration pattern:** Tests that `Assert.True(bytes1.SequenceEqual(bytes2))` for two-run determinism stay UNCHANGED (Phase 28 preserves two-run determinism). Tests that compare bytes against a PIN (committed reference bytes) get migrated to `RmsRegressionTests.AssertRmsWithinTolerance(rendered, baselinePath, 100, 0.5)`.

## PATTERN MAPPING COMPLETE
