# Phase 22: Tier B/C Composer DX Bundle - Pattern Map

**Mapped:** 2026-05-01
**Files analyzed:** 22 (10 modified + 12 added)
**Analogs found:** 22 / 22 (every new/modified file has a strong codebase analog)

## File Classification

### Production code (modified)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (DX-10 add 4-arg `arpeggio`) | service | transform | `HarmonyFunctions.cs:327-365` (existing 2-arg `arpeggio`) | exact (extends in place) |
| `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` (DX-12 add NoteValue `delay` overload) | service | transform | `EffectsFunctions.cs:191-216` (existing `RegisterDelay` + `DelayEffect`) | exact (sibling overload) |
| `flow-lang/StandardLibrary/Audio/FileIO.cs` (DX-15 add `loadWav` Int+Double overloads + `VarispeedResample` helper) | service | file-I/O + transform | `FileIO.cs:285-295` (`LoadWav`), `FileIO.cs:438-465` (`Resample`) | exact (sibling overload + math reuse) |
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (DX-13 `quantize`, DX-14 `legato`/`portamento`) | service | transform | `TransformFunctions.cs:75-89` (`TransformNotes`), `RegisterTranspose` (lines 93-104), `RegisterAugmentDiminish` (existing) | exact (transforms tier) |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (register DX-12 RegisterContextDependent + DX-15 LoadWav overloads) | config | event-driven | `BuiltInFunctions.cs:552-554` (existing `loadWav` reg), `BuiltInFunctions.cs:1184-1205` (`RegisterEuclideanOverloads` ContextDependent) | exact (registration site) |
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (DX-14 `MusicalNoteData` defaulted-parameter migration: `DurationOverlap`, `PortamentoMs`) | model | — | `NoteType.cs:211-306` (existing `MusicalNoteData` + Phase 18 `DurationFraction` migration) | exact (precedent: same class, same pattern) |
| `flow-lang/StandardLibrary/Audio/BarRenderer.cs` (DX-14 read `DurationOverlap` post-`ToTimeline`) | service | transform | `BarRenderer.cs:67-72` (existing `IsTied` overlap extension) | exact (mirror pattern) |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs` (DX-14 emit CC65/CC5 + extended NoteOff for legato) | service | event-driven | `MidiExport.cs:245-275` (existing per-note loop emitting NoteOn/NoteOff) | exact (extends note loop) |
| `flow-lang/std.flow` (DX-10/11/13/14 `internal proc` declarations) | config | — | `std.flow:79-90` (existing transform internal procs), `std.flow:105-114` (existing harmony internal procs) | exact |
| `flow-lang/audio.flow` (DX-12 `delay` NoteValue overload, DX-15 `loadWav` overloads) | config | — | `audio.flow:48` (existing `loadWav`), `audio.flow:343-344` (existing `delay`) | exact |

### Production code (added)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Harmony/Voicings.cs` (DX-11 inversion + drop2/drop3/open/close/spread) | service | transform | `flow-lang/StandardLibrary/Harmony/ChordParser.cs` (peer file in same directory; pure static class on `ChordData`) | role-match (sibling tier file) |

### Tests (added — Wave 0 RED)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (recent xUnit Facts) | exact |
| `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` | exact |
| `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` (numeric/arithmetic Facts template) | exact |
| `flow-lang.Tests/Unit/Phase22/QuantizeFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` (defaulted-parameter migration Facts) | exact |
| `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` + DryWetMidi read-back (Phase 15 `MidiReadHelpers`) | role-match |
| `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs` | test | — | `flow-lang.Tests/Unit/Phase18/FractionTests.cs` (numeric Facts) | exact |
| `flow-lang.Tests/FlowScriptData.cs` (sentinel registration entries, modified) | config | — | `FlowScriptData.cs:61-150` (existing `RequiredSentinels` dictionary) | exact |
| `tests/test_dx_arpeggio.flow` | test | — | `tests/test_chords.flow`, `tests/test_euclidean_swing.flow` | exact |
| `tests/test_dx_voicings.flow` | test | — | `tests/test_chords.flow` | exact |
| `tests/test_dx_delay_sync.flow` | test | — | `tests/test_euclidean_swing.flow` | exact |
| `tests/test_dx_quantize.flow` | test | — | `tests/test_transforms.flow` | exact |
| `tests/test_dx_legato.flow` | test | — | `tests/test_transforms.flow` | exact |
| `tests/test_dx_portamento.flow` | test | — | `tests/test_transforms.flow` (writeMidi assertion) | role-match |
| `tests/test_dx_loadwav_varispeed.flow` | test | — | `tests/test_wav_loading.flow` (synth → writeWav → loadWav roundtrip) | exact |

---

## Pattern Assignments

### `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — DX-10 4-arg arpeggio

**Analog:** `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:327-365` (existing 2-arg `arpeggio`).

**Imports pattern** (lines 1-6):
```csharp
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;
```

**Core registration pattern** (analog at lines 327-365 — DX-10 ADDS a 4-arg overload alongside, keeping the 2-arg signature byte-identical):
```csharp
// EXISTING (KEEP UNCHANGED — DO NOT touch the 2-arg signature):
var arpeggioSignature = new FunctionSignature("arpeggio", [ChordType.Instance, StringType.Instance]);
registry.Register("arpeggio", arpeggioSignature, args =>
{
    var chord = args[0].As<ChordData>();
    var direction = args[1].As<string>();

    var noteNames = chord.NoteNames.ToList();

    switch (direction.ToLower())
    {
        case "down":
            noteNames.Reverse();
            break;
        case "updown":
            var down = new List<string>(noteNames);
            down.Reverse();
            if (down.Count > 1) down = down.Skip(1).ToList();
            noteNames.AddRange(down);
            break;
        // "up" is default order
    }

    var musicalNotes = new List<MusicalNoteData>();
    foreach (var noteName in noteNames)
    {
        var (name, octave, alteration) = NoteType.Parse(noteName);
        musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
            (int)NoteValueType.Value.EIGHTH, isRest: false));
    }

    var timeSig = new TimeSignatureData(4, 4);
    var bar = new BarData(musicalNotes, timeSig);
    var sequence = new SequenceData();
    sequence.AddBar(bar);

    return Value.Sequence(sequence);
});
```

**DX-10 NEW 4-arg overload (registered immediately after the 2-arg block):**
```csharp
// NEW (DX-10):
var arpeggioFullSig = new FunctionSignature("arpeggio",
    [ChordType.Instance, NoteValueType.Instance, StringType.Instance, StringType.Instance]);
registry.Register("arpeggio", arpeggioFullSig, args =>
{
    var chord = args[0].As<ChordData>();
    int rateEnum = args[1].As<int>();      // NoteValue backed by int
    var direction = args[2].As<string>();
    var pattern = args[3].As<string>();    // "linear" | "chord-tone" | "scale-tone"

    var noteNames = ApplyDirection(chord.NoteNames.ToList(), direction);
    // For v1.3, "chord-tone"/"scale-tone" route to "linear" per Deferred Ideas.
    // Document with a doc comment pointing at REQUIREMENTS line 105.

    var musicalNotes = noteNames.Select(n => {
        var (name, oct, alt) = NoteType.Parse(n);
        return new MusicalNoteData(name, oct, alt, rateEnum, isRest: false);
    }).ToList();

    var bar = new BarData(musicalNotes, new TimeSignatureData(4, 4));
    var seq = new SequenceData();
    seq.AddBar(bar);
    return Value.Sequence(seq);
});

// New helper for the additional directions (downup; "random" defers to v1.4 — Pitfall 7):
private static List<string> ApplyDirection(List<string> notes, string direction) =>
    direction.ToLower() switch
    {
        "down"   => notes.AsEnumerable().Reverse().ToList(),
        "updown" => notes.Concat(notes.AsEnumerable().Reverse().Skip(1)).ToList(),
        "downup" => notes.AsEnumerable().Reverse().Concat(notes.Skip(1)).ToList(),
        _        => notes,                  // "up" / "random" / unknown → unchanged
    };
```

**Anti-pattern:** Do NOT create `arpeggio2` — extend in place per CONTEXT D-(implicit) and Anti-Patterns.

---

### `flow-lang/StandardLibrary/Harmony/Voicings.cs` — DX-11 inversion + voicing (NEW FILE)

**Analog:** `flow-lang/StandardLibrary/Harmony/ChordParser.cs` (sibling file in same directory — pure static class operating on `ChordData`/`string[]`).

**Recommended file header** (mirror ChordParser.cs structure):
```csharp
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Phase 22 DX-11: chord-shape transforms — inversion (rotate lowest note up an octave) and
/// named voicings (drop2, drop3, open, close, spread).
///
/// Per CONTEXT D-07 (charitable interpretation): when a chord lacks enough notes for the
/// requested voicing (drop2/drop3 need ≥4; spread/open need ≥3), `voicing` returns the input
/// chord unchanged. No error, no warning. See Phase 22 CONTEXT D-07.
/// </summary>
public static class Voicings
{
    // Registered from HarmonyFunctions.Register — see HarmonyFunctions.cs:292
    public static void Register(InternalFunctionRegistry registry) { /* ... */ }
}
```

**Charitable D-07 pattern** (from RESEARCH §"Pattern 3: Charitable Voicing"):
```csharp
/// <summary>
/// drop2(chord) — lowers the 2nd-from-top note by an octave. Common in jazz comping.
/// Per Phase 22 CONTEXT D-07 (charitable interpretation): if the chord has fewer than 4 notes,
/// returns the input chord unchanged. No error, no warning. Composer can keep iterating.
/// </summary>
private static ChordData Drop2(ChordData input)
{
    if (input.NoteNames.Length < 4)
        return input;          // CONTEXT D-07
    var notes = input.NoteNames.ToList();
    int idx = notes.Count - 2;
    notes[idx] = LowerOctave(notes[idx]);
    notes.Sort((a, b) => CompareByPitch(a, b));
    return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
}
```

**Note-name canonicalization pattern (Pitfall 5):** Always decompose via `NoteType.Parse(name) → (letter, octave, alteration)`, manipulate `octave` only, then re-emit via `NoteType.Format(letter, newOctave, alteration)` to guarantee canonical `+`/`-` accidental form (NOT `s`/`#`).

**Registration call site:** Inside the existing `HarmonyFunctions.Register` body (HarmonyFunctions.cs:292), add `Voicings.Register(registry);` alongside the existing `chordNotes`/`arpeggio` registrations. The std.flow `internal proc` declarations live in `flow-lang/std.flow` next to existing harmony procs (line 105-114).

---

### `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` — DX-12 NoteValue delay overload

**Analog:** `EffectsFunctions.cs:191-216` (existing `RegisterDelay` + `DelayEffect`).

**Existing ms-rate registration to PRESERVE byte-identical** (lines 193-199):
```csharp
private static void RegisterDelay(InternalFunctionRegistry registry)
{
    // delay(Buffer, Double, Double, Double) -> Buffer — time ms, feedback, mix
    var delaySig = new FunctionSignature("delay",
        [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance]);
    registry.Register("delay", delaySig, DelayEffect);
}
```

Existing body (lines 204-216):
```csharp
private static Value DelayEffect(IReadOnlyList<Value> args)
{
    var buffer = args[0].As<AudioBuffer>();
    float delayMs = (float)args[1].As<double>();
    float feedback = (float)args[2].As<double>();
    float mix = (float)args[3].As<double>();

    if (buffer.Frames == 0)
        return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

    var result = Delay.Apply(buffer, delayMs, feedback, mix);
    return Value.Buffer(result);
}
```

**DX-12 NEW NoteValue overload pattern** — must move registration into a NEW `RegisterContextDependent(registry, context)` static method on `EffectsFunctions` (mirror of `HarmonyFunctions.RegisterContextDependent` at HarmonyFunctions.cs:21-25 + the euclidean swing pattern at `BuiltInFunctions.cs:1184-1205`):

```csharp
// NEW: NoteValue-rate overload (DX-12)
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var delaySyncedSig = new FunctionSignature("delay",
        [BufferType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance]);
    registry.Register("delay", delaySyncedSig, args =>
    {
        var buffer = args[0].As<AudioBuffer>();
        int noteValueEnum = args[1].As<int>();   // NoteValue backed by int
        float feedback = (float)args[2].As<double>();
        float mix = (float)args[3].As<double>();

        double bpm = context.GetMusicalContext().Tempo ?? 120.0;
        double delayMs = NoteValueToMs((NoteValueType.Value)noteValueEnum, bpm);

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = DSP.Delay.Apply(buffer, (float)delayMs, feedback, mix);
        return Value.Buffer(result);
    });
}

private static double NoteValueToMs(NoteValueType.Value nv, double bpm)
{
    double quarterMs = 60_000.0 / bpm;
    return nv switch {
        NoteValueType.Value.WHOLE        => quarterMs * 4,
        NoteValueType.Value.HALF         => quarterMs * 2,
        NoteValueType.Value.QUARTER      => quarterMs,
        NoteValueType.Value.EIGHTH       => quarterMs / 2,
        NoteValueType.Value.SIXTEENTH    => quarterMs / 4,
        NoteValueType.Value.THIRTYSECOND => quarterMs / 8,
        _ => quarterMs,
    };
}
```

**Wire-up call site:** In `BuiltInFunctions.RegisterContextDependentFunctions` (called from `FlowEngine.cs:50`), add `EffectsFunctions.RegisterContextDependent(registry, context);` alongside existing `RegisterEuclideanOverloads` calls.

**Pitfall 1 (NoteValue + Int dispatch ambiguity):** `delay(buf, 250, 0.5, 0.4)` with bare Int literal is ambiguous between Double and NoteValue overloads. Plans must ship a Fact pinning OverloadResolver behavior — use `EIGHTH`/`QUARTER` named constants from `notation.flow:28-33` in user-facing examples (CONTEXT Pitfall 10 Option A).

---

### `flow-lang/StandardLibrary/Audio/FileIO.cs` — DX-15 varispeed loadWav overloads

**Analog (file-I/O entry):** `FileIO.cs:285-295`:
```csharp
public static Value LoadWav(IReadOnlyList<Value> args)
{
    string filepath = args[0].As<string>();
    var buffer = LoadWavInternal(filepath);
    return Value.Buffer(buffer);
}
```

**Analog (resample math):** `FileIO.cs:438-465` — the existing `Resample` is the algorithmic template for `VarispeedResample`. Copy the for-frame loop, replace `(double)source.SampleRate / targetRate` ratio derivation with a caller-supplied `ratio` parameter:

```csharp
public static AudioBuffer Resample(AudioBuffer source, int targetRate)
{
    if (source.SampleRate == targetRate)
        return source;

    double ratio = (double)source.SampleRate / targetRate;
    int newFrames = (int)(source.Frames / ratio);
    var result = new AudioBuffer(newFrames, source.Channels, targetRate);

    for (int frame = 0; frame < newFrames; frame++)
    {
        double srcPos = frame * ratio;
        int srcFrame = (int)srcPos;
        float frac = (float)(srcPos - srcFrame);

        for (int ch = 0; ch < source.Channels; ch++)
        {
            float s0 = source.GetSample(Math.Min(srcFrame, source.Frames - 1), ch);
            float s1 = source.GetSample(Math.Min(srcFrame + 1, source.Frames - 1), ch);
            result.SetSample(frame, ch, s0 + frac * (s1 - s0));
        }
    }
    return result;
}
```

**DX-15 NEW overloads (alongside existing `LoadWav`):**
```csharp
public static Value LoadWavSemitones(IReadOnlyList<Value> args)
{
    string filepath = args[0].As<string>();
    int semitones = args[1].As<int>();
    var buffer = LoadWavInternal(filepath);
    if (semitones == 0) return Value.Buffer(buffer);     // identity short-circuit
    double ratio = Math.Pow(2.0, semitones / 12.0);
    return Value.Buffer(VarispeedResample(buffer, ratio));
}

public static Value LoadWavRatio(IReadOnlyList<Value> args)
{
    string filepath = args[0].As<string>();
    double ratio = args[1].As<double>();
    var buffer = LoadWavInternal(filepath);
    if (ratio == 1.0) return Value.Buffer(buffer);       // identity short-circuit
    if (ratio <= 0.0) throw new ArgumentException(
        $"loadWav ratio must be positive (got {ratio})");
    return Value.Buffer(VarispeedResample(buffer, ratio));
}

public static AudioBuffer VarispeedResample(AudioBuffer source, double ratio)
{
    int newFrames = (int)Math.Round(source.Frames / ratio);
    var result = new AudioBuffer(newFrames, source.Channels, source.SampleRate);
    for (int frame = 0; frame < newFrames; frame++)
    {
        double srcPos = frame * ratio;
        int srcFrame = (int)srcPos;
        float frac = (float)(srcPos - srcFrame);
        for (int ch = 0; ch < source.Channels; ch++)
        {
            float s0 = source.GetSample(Math.Min(srcFrame, source.Frames - 1), ch);
            float s1 = source.GetSample(Math.Min(srcFrame + 1, source.Frames - 1), ch);
            result.SetSample(frame, ch, s0 + frac * (s1 - s0));
        }
    }
    return result;
}
```

**Registration call site analog:** `BuiltInFunctions.cs:552-554`:
```csharp
// EXISTING (DO NOT CHANGE):
var loadWavSignature = new FunctionSignature("loadWav", [StringType.Instance]);
registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav);

// NEW DX-15 overloads (add immediately after):
var loadWavSemiSig = new FunctionSignature("loadWav",
    [StringType.Instance, IntType.Instance]);
registry.Register("loadWav", loadWavSemiSig, Audio.FileIO.LoadWavSemitones);

var loadWavRatioSig = new FunctionSignature("loadWav",
    [StringType.Instance, DoubleType.Instance]);
registry.Register("loadWav", loadWavRatioSig, Audio.FileIO.LoadWavRatio);
```

**Anti-pattern:** Do NOT modify `LoadWav(IReadOnlyList<Value>)` 1-arg path or `LoadWavInternal` — must stay byte-identical to preserve existing tutorial/showcase tests (CONTEXT Anti-Patterns "Breaking loadWav(path) byte-identity").

---

### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — DX-13 quantize + DX-14 legato/portamento

**Analog (registration shape):** `TransformFunctions.cs:93-104` (`RegisterTranspose`) — existing pattern of one-method-per-feature registration with sibling overloads.

**Analog (per-note transform helper):** `TransformFunctions.cs:75-89`:
```csharp
private static SequenceData TransformNotes(SequenceData seq, Func<MusicalNoteData, MusicalNoteData> transform)
{
    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)
        {
            newNotes.Add(transform(note));
        }
        var newBar = new BarData(newNotes, bar.TimeSignature!);
        result.AddBar(newBar);
    }
    return result;
}
```

**DX-13 quantize:** Identity short-circuit at strength=0.0 is mandatory (Pitfall 9 — Phase 18 byte-identical regression gate). See RESEARCH lines 421-465 for the algorithm scaffold; key decision (Open Question 1) is whether to add `OnsetOffset` defaulted-parameter to `MusicalNoteData` (cleaner, mirrors Phase 18 `DurationFraction`) vs. adjust prior-note durations.

**DX-14 legato:** Use `TransformNotes` pattern, but produce a new `MusicalNoteData` with `durationOverlap` set:
```csharp
private static Value LegatoTransform(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double overlap = args[1].As<double>();
    return Value.Sequence(TransformNotes(seq, note =>
        new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
            note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
            note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation,
            note.SourceLength, note.DurationFraction,
            durationOverlap: overlap)));    // NEW DX-14 field
}
```

**DX-14 portamento:** Same shape, sets `portamentoMs` instead.

**Registration:** Add `RegisterQuantize(registry, context);` (context-dependent, reads timesig) and `RegisterArticulationTransforms(registry);` (legato/portamento) to the `Register` method at line 17-30.

**Wire to std.flow:** Add `internal proc quantize (Sequence: seq, NoteValue: resolution, Double: strength, Double: swing)` and `internal proc legato (Sequence: seq, Double: overlap)` and `internal proc portamento (Sequence: seq, Millisecond: glideTime)` next to existing transforms at `std.flow:79-90`.

---

### `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — DX-14 MusicalNoteData migration

**Analog:** `NoteType.cs:211-306` — the existing `MusicalNoteData` class itself. Phase 18 already established the defaulted-parameter migration pattern when it added `DurationFraction` (line 244, 246, 260).

**Existing constructor signature** (line 246) — DX-14 appends two new defaulted parameters at the END:
```csharp
public MusicalNoteData(char noteName, int octave, int alteration, int? durationValue, bool isRest,
    double? centOffset = null, bool isTied = false, double velocity = 0.63,
    Articulation articulation = Articulation.Normal, bool isDotted = false,
    FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0,
    FlowLang.TypeSystem.Fraction? durationFraction = null,
    // Phase 22 DX-14 additions — both default to 0.0 = "not set":
    double durationOverlap = 0.0,
    double portamentoMs = 0.0)
```

**Property additions** (mirror line 244 `DurationFraction` doc-comment shape):
```csharp
/// <summary>
/// Phase 22 DX-14 legato: render-time duration extension factor.
/// 0.0 = no extension (default); 0.5 = play 1.5× longer; 1.0 = play 2× longer.
/// Read by BarRenderer and MidiExport AFTER bar.ToTimeline() produces onsets,
/// so onsets are NOT moved (true polyphonic legato per CONTEXT D-02).
/// </summary>
public double DurationOverlap { get; }

/// <summary>
/// Phase 22 DX-14 portamento: glide time in milliseconds for MIDI CC5 mapping.
/// 0.0 = no portamento (default). MidiExport emits CC65=127 + CC5=mappedValue at
/// note start, CC65=0 at note end (per-note bracket). Linear ms→CC5 mapping per
/// CONTEXT D-15: 0ms→0, 100ms→64, 200ms→127 clamped.
/// Audio renderer ignores this field — portamento is MIDI-only in v1.3.
/// </summary>
public double PortamentoMs { get; }
```

**Critical:** Defaults MUST be at the END of the constructor signature so all 30+ existing `new MusicalNoteData(...)` call sites continue to compile unmodified. This is the exact same migration shape Phase 18 used for `DurationFraction` (verified Assumption A7).

---

### `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — DX-14 legato render-time consumer

**Analog:** `BarRenderer.cs:67-72` (existing `IsTied` overlap extension):
```csharp
// For tied notes, extend render duration so the audio tail overlaps the next note.
// This creates a legato transition since voices mix additively on the timeline.
if (note.IsTied)
{
    double overlapSeconds = 0.1; // 100ms overlap for smooth crossfade
    double overlapBeats = (overlapSeconds / 60.0) * bpm;
    durationBeats += overlapBeats;
}
```

**DX-14 NEW block (added immediately after the IsTied block, lines 67-72):**
```csharp
// DX-14 legato: extend render duration by overlap factor BEFORE rendering audio buffer.
// The bar timeline already produced offsetBeats for this note (line 45); we ONLY change
// how long this note's audio buffer plays. Onset is NOT moved.
// Per CONTEXT D-01: durationOverlap=0.5 → durationBeats × 1.5; D-02 → polyphonic mix
// pipeline at SongRenderer.cs:200-221 sums overlapping voices automatically.
if (note.DurationOverlap > 0.0)
{
    durationBeats *= (1.0 + note.DurationOverlap);
}
```

**Pitfall 3 (Legato changes durations, NOT onsets):** Do NOT mutate `DurationValue` to extend a note — that would shift `bar.ToTimeline()` onsets and slow the song down. Use the post-onset render-time extension above.

---

### `flow-lang/StandardLibrary/Audio/MidiExport.cs` — DX-14 portamento + legato

**Analog:** `MidiExport.cs:245-275` (existing per-note loop). DX-14 extends the loop body:

```csharp
// EXISTING per-note loop (lines 245-275):
foreach (var note in bar.MusicalNotes)
{
    if (note.IsRest)
    {
        double restBeats = note.GetBeats(barTimeSigDenom);
        barTick += (long)(restBeats * ticksPerQuarter);
        continue;
    }

    int midiNote = PitchConversion.GetMidiNote(
        note.NoteName, note.Octave, note.Alteration);

    byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);

    double beats = note.GetBeats(barTimeSigDenom);
    long durationTicks = (long)(beats * ticksPerQuarter);

    noteEvents.Add(new TimedEvent(
        new NoteOnEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)velocity),
        barTick));

    noteEvents.Add(new TimedEvent(
        new NoteOffEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)0),
        barTick + durationTicks));

    barTick += durationTicks;
}
```

**DX-14 modifications inside this loop:**
1. Compute `extendedBeats = note.DurationOverlap > 0 ? beats * (1.0 + note.DurationOverlap) : beats;` and use `extendedBeats` for `durationTicks` (legato → overlapping NoteOff per CONTEXT D-03).
2. Before NoteOn, emit CC65=127 + CC5=mappedValue when `note.PortamentoMs > 0.0`:
   ```csharp
   if (note.PortamentoMs > 0.0)
   {
       byte cc5Value = (byte)Math.Clamp(
           (int)Math.Round(note.PortamentoMs * 127.0 / 200.0), 0, 127);
       noteEvents.Add(new TimedEvent(
           new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)127), barTick));
       noteEvents.Add(new TimedEvent(
           new ControlChangeEvent((SevenBitNumber)5, (SevenBitNumber)cc5Value), barTick));
   }
   ```
3. After NoteOff, emit CC65=0 bracket close:
   ```csharp
   if (note.PortamentoMs > 0.0)
   {
       noteEvents.Add(new TimedEvent(
           new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)0),
           barTick + durationTicks));
   }
   ```
4. **CRITICAL:** Advance `barTick` by the ORIGINAL `beats * ticksPerQuarter` (not extendedBeats) — this is what makes legato OVERLAP rather than slow the song down (Pitfall 3 mirror).

**Imports already present** (lines 1-7):
```csharp
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;     // ControlChangeEvent + NoteOnEvent + NoteOffEvent
using Melanchall.DryWetMidi.Interaction;
```

`ControlChangeEvent(SevenBitNumber controllerNum, SevenBitNumber value)` ctor is verified in csproj DryWetMidi 8.0.3 (Assumption A2).

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` — DX-12 + DX-15 registration wire-up

**Analog (parameterless register, used by DX-15):** `BuiltInFunctions.cs:540-554`:
```csharp
var writeWavSignature = new FunctionSignature(
    "writeWav", [StringType.Instance, BufferType.Instance]);
registry.Register("writeWav", writeWavSignature, Audio.FileIO.WriteWav);

var writeWavWithDepthSignature = new FunctionSignature(
    "writeWav", [StringType.Instance, BufferType.Instance, IntType.Instance]);
registry.Register("writeWav", writeWavWithDepthSignature, Audio.FileIO.WriteWavWithBitDepth);

// loadWav(String) -> Buffer - load WAV file
var loadWavSignature = new FunctionSignature("loadWav", [StringType.Instance]);
registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav);
```

**DX-15:** Add the two new `loadWav` registrations (Int + Double overloads) immediately after line 554. See FileIO.cs section above for the full registration block.

**Analog (context-dependent register, used by DX-12 + DX-13):** `BuiltInFunctions.cs:1184-1205`:
```csharp
private static void RegisterEuclideanOverloads(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var euclideanSwingSig = new FunctionSignature(
        "euclidean",
        [IntType.Instance, IntType.Instance, NoteType.Instance, DoubleType.Instance]);
    registry.Register("euclidean", euclideanSwingSig, args =>
    {
        // ... reads context.GetMusicalContext().Velocity etc.
    });
    // ...
}
```

**DX-12:** `EffectsFunctions.RegisterContextDependent(registry, context)` is wired from `BuiltInFunctions.RegisterContextDependentFunctions` — same call shape as `RegisterEuclideanOverloads`. Find the existing `RegisterContextDependentFunctions` method body and add the DX-12 call alongside.

**DX-13 quantize:** If quantize reads timesig (it does — see RESEARCH §"DX-13 Code Examples"), it ALSO registers via the context-dependent pathway. Add `TransformFunctions.RegisterContextDependent(registry, context)` to `RegisterContextDependentFunctions`.

---

### `flow-lang.Tests/Unit/Phase22/*.cs` — xUnit Facts

**Analog (recent Facts pattern):** `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs:1-66`:
```csharp
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase21;

/// <summary>
/// PRAG-01 acceptance Facts pinning the pre-lex PragmaScanner.Scan algorithm.
///
/// Decisions referenced (locked in 21-CONTEXT.md):
///   D-01 — Pre-scan returns (PragmaSet, transformedSource).
///   D-03 — Comments + blanks are legal anywhere in the prefix region.
///   ...
/// </summary>
public class PragmaScannerFacts
{
    private static (PragmaSet pragmas, string transformed, ErrorReporter reporter) Scan(string source)
    {
        var reporter = new ErrorReporter();
        var (pragmas, transformed) = PragmaScanner.Scan(source, fileName: null, reporter);
        return (pragmas, transformed, reporter);
    }

    [Fact]
    public void Empty_Source_ReturnsEmptyPragmasAndSource()
    {
        var (pragmas, transformed, reporter) = Scan("");
        Assert.Same(PragmaSet.Empty, pragmas);
        Assert.Equal("", transformed);
        Assert.False(reporter.HasErrors);
    }
    // ... many more Facts
}
```

**Phase 22 mirrors:** `namespace FlowLang.Tests.Unit.Phase22;` + class-level summary doc citing the relevant CONTEXT decision IDs (D-01 through D-08).

**Numeric/arithmetic Facts analog:** `flow-lang.Tests/Unit/Phase18/FractionTests.cs:1-75` — apply this style for `DelaySyncFacts.NoteValueToMs`, `QuantizeFacts.Strength0_IsIdentity`, and `LoadWavVarispeedFacts.TwelveSemitones_HalvesFrames`.

**Defaulted-parameter migration analog:** `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs:1-56`:
```csharp
[Fact]
public void DurationFraction_DefaultsToNull()
{
    var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
    Assert.Null(n.DurationFraction);
}

[Fact]
public void DurationFraction_OptionalCtorParam_AcceptedAtEndOfSignature()
{
    var n = new MusicalNoteData(
        'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
        durationFraction: new Fraction(1, 3));
    Assert.Equal(new Fraction(1, 3), n.DurationFraction);
}
```

`LegatoFacts.cs` and `PortamentoMidiFacts.cs` mirror these "defaults to 0.0" + "ctor param accepted at end" Facts for `DurationOverlap` / `PortamentoMs`.

---

### `flow-lang.Tests/FlowScriptData.cs` — Sentinel registration

**Analog:** `FlowScriptData.cs:61-150` — existing `RequiredSentinels` dictionary entries:
```csharp
public static readonly Dictionary<string, string[]> RequiredSentinels = new()
{
    // Phase 13-02 (DX-01): pin arithmetic outputs that follow comment lines.
    ["test_comments.flow"] = new[]
    {
        "note stream ok",
        "42",
        "All comment tests passed",
    },

    ["test_math.flow"] = new[]
    {
        "3.141592654",           // pi — empirical Flow `str` format
        "6.283185307",           // tau — empirical (10-sig-digit precision)
        "1024",                  // pow(2.0, 10.0) — gates pow registration
        "All math tests passed",
    },
    // ...
};
```

**Phase 22 entries to add (one per smoke script):**
```csharp
["test_dx_arpeggio.flow"]   = new[] { "DX-10 arpeggio: PASSED" },
["test_dx_voicings.flow"]   = new[] { "DX-11 voicings: PASSED" },
["test_dx_delay_sync.flow"] = new[] { "DX-12 delay sync: PASSED" },
["test_dx_quantize.flow"]   = new[] { "DX-13 quantize: PASSED" },
["test_dx_legato.flow"]     = new[] { "DX-14 legato: PASSED" },
["test_dx_portamento.flow"] = new[] { "DX-14 portamento: PASSED" },
["test_dx_loadwav_varispeed.flow"] = new[] { "DX-15 varispeed: PASSED" },
```

---

### `tests/test_dx_*.flow` — `.flow` smoke scripts

**Analog (chord/harmony smoke):** `tests/test_chords.flow` — uses `use "std.flow"`, exercises chord literals + `chordRoot`/`chordQuality`/`chordNotes`/`scaleNotes`/`resolveNumeral`, prints `"All chord tests passed!"` sentinel.

**Analog (audio render smoke):** `tests/test_euclidean_swing.flow`:
```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        Sequence pos = (euclidean 3 8 C4 0.3)
        section sp { pos }
        Song songPos = [sp]
        Buffer bufPos = (renderSong songPos "piano")
        (writeWav "tests/output/phase15_euclidean_swing_pos.wav" bufPos)
        (print "euclidean swing: PASSED")
    }
}
```

**Analog (transform smoke):** `tests/test_transforms.flow:1-60` — uses `timesig 4/4 { Sequence x = ... ; (print (str ...)); }` blocks with explicit-call S-expression style.

**Analog (WAV roundtrip smoke for DX-15):** `tests/test_wav_loading.flow`:
```flow
use "@std"
use "@audio"

Buffer original = (createSineTone 0.1 440.0 0.8)
(exportWav original "tests/test_output_roundtrip.wav")
Buffer loaded = (loadWav "tests/test_output_roundtrip.wav")
Int origFrames = (getFrames original)
Int loadedFrames = (getFrames loaded)
(print (str origFrames))
(print (str loadedFrames))
```

**Phase 22 smoke convention:** Each script ends with `(print "DX-NN <feature>: PASSED")` matching the FlowScriptData sentinel entry above. Use S-expression style only — no infix operators (CLAUDE.md auto-memory).

---

## Shared Patterns

### Functional S-expression style (auto-memory: `feedback_language_philosophy.md`)
**Source:** Project memory + REQUIREMENTS DX-10..DX-15 acceptance examples.
**Apply to:** All Phase 22 smoke scripts, doc comments, and tutorial mentions.
**Form:** `(arpeggio Cmaj7 q "up" "linear")` not `Cmaj7 -> arpeggio q "up" "linear"`. Both parse, but acceptance examples lock the S-expression form.

### Charitable interpretation (auto-memory: `feedback_charitable_interpretation.md`)
**Source:** Project memory + CONTEXT D-07.
**Apply to:** DX-10 (`random` → falls back to `up`), DX-11 (incomplete-chord voicing returns input unchanged), DX-13 (strength=0 identity short-circuit). NEVER throw or warn when the user's intent is well-defined for the valid case.

### Defaulted-parameter migration (Phase 18 precedent)
**Source:** `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:244-260` (`DurationFraction`).
**Apply to:** DX-14 `DurationOverlap` + `PortamentoMs`; DX-13 `OnsetOffset` if planner picks the cleaner mechanism (Open Question 1).
**Rule:** New optional fields go at END of constructor signature with default value (null/0.0/false). Property has read-only getter, doc comment explaining DORMANT-when-default semantics.

### Atomic-overload registration alongside existing
**Source:** `EffectsFunctions.cs:191-216` (RegisterDelay), `BuiltInFunctions.cs:552-554` (loadWav).
**Apply to:** Every Phase 22 stdlib registration — DO NOT modify the existing 1-arg / 2-arg / ms-rate signature. ADD a new `FunctionSignature` alongside.

### Context-dependent registration via `RegisterContextDependentFunctions`
**Source:** `flow-lang/Core/FlowEngine.cs:50` (call site), `BuiltInFunctions.cs:1184-1205` (`RegisterEuclideanOverloads`), `HarmonyFunctions.cs:21-25` (`HarmonyFunctions.RegisterContextDependent`).
**Apply to:** DX-12 `delay(NoteValue)` reads `MusicalContext.Tempo`; DX-13 `quantize` reads `MusicalContext.TimeSignature`. Both require the context-aware registration pathway — NOT the parameterless `Register`.

### LOCAL `new Random(seed)` for deterministic byte-identity (Phase 15 DX-09 precedent)
**Source:** `BuiltInFunctions.cs:1184` (RegisterEuclideanOverloads — comment about CONTEXT D-17).
**Apply to:** DX-10 if it ever supports `random` direction (Pitfall 7). Recommendation per RESEARCH: defer `random` to v1.4 — fall back to `up` in v1.3 with a doc comment.

### xUnit Facts test scaffolding (Phase 21 RED→GREEN TDD)
**Source:** `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs:1-66`.
**Apply to:** All seven Phase 22 Facts files. Class-level summary doc cites CONTEXT decision IDs. Each `[Fact]` name reads as `What_Condition_Outcome` (e.g., `DelayWithEighthAtTempo120_ProducesQuarterSecondDelayMs`).

### Wave 0 RED-first scaffolding (Phase 18-21 precedent)
**Source:** RESEARCH §"Validation Architecture" Wave 0 Gaps + STATE.md Phase 18-21 SUMMARY anchors.
**Apply to:** Every Phase 22 plan ships its Facts + smoke script BEFORE production code (RED → GREEN). The Facts file commits in plan-NN-01, production in plan-NN-02 (or later), per Phase 18-21 commit pattern `feat(22-NN): ...` / `test(22-NN): ...`.

### Byte-identical regression gate (Phase 18 contract)
**Source:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs` (19/19 GREEN).
**Apply to:** Every plan's commit. DX-12/13/14 specifically must verify these stay 19/19 GREEN — the per-note `MusicalNoteData` field additions and tempo/timing math touch the rendered output.

---

## No Analog Found

**(none)** — every Phase 22 file has a strong codebase analog. The features are mechanically structured as overload-additions and per-note-field-additions; nothing creates a new tier or new architectural concept.

The OLA-windowed varispeed alternative path (DX-15 ambition) has no codebase analog, but the recommendation per RESEARCH is to ship pure linear-interp (which DOES have a strong analog at `FileIO.cs:438-465`) and defer OLA windowing to v1.4.

---

## Metadata

**Analog search scope:**
- `flow-lang/StandardLibrary/Harmony/` (3 files: ChordParser, HarmonyFunctions, ScaleDatabase)
- `flow-lang/StandardLibrary/Audio/` (DSP/, Synthesizers/, EffectsFunctions, FileIO, MidiExport, BarRenderer, SongRenderer)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`
- `flow-lang/StandardLibrary/BuiltInFunctions.cs`
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (`MusicalNoteData` + Phase 18 migration)
- `flow-lang/Runtime/MusicalContext.cs`, `Fraction.cs`
- `flow-lang/Core/FlowEngine.cs` (registration call sites)
- `flow-lang.Tests/Unit/Phase18/` (FractionTests, MusicalNoteDataTests)
- `flow-lang.Tests/Unit/Phase21/` (PragmaScannerFacts)
- `flow-lang.Tests/FlowScriptData.cs` (sentinel pattern)
- `tests/` (test_chords, test_transforms, test_wav_loading, test_euclidean_swing)
- `flow-lang/std.flow`, `flow-lang/audio.flow`, `flow-lang/notation.flow` (`internal proc` declaration patterns)

**Files scanned:** ~25 source files (read-only).

**Pattern extraction date:** 2026-05-01.
