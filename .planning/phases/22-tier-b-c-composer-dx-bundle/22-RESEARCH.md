# Phase 22: Tier B/C Composer DX Bundle - Research

**Researched:** 2026-05-01
**Domain:** Music-DSL composer DX (harmony, audio DSP, MIDI CC, varispeed pitch shift)
**Confidence:** HIGH (every reusable asset and integration point verified by direct codebase read)

## Summary

Phase 22 ships six independently shippable composer DX features that each extend an existing Flow built-in **in place** by adding new overload signatures alongside the current ones. Every feature integrates with infrastructure already shipped — Phase 18 `Fraction`, the polyphonic voice mixer, the existing DryWetMidi-backed `MidiExport`, the existing `Resample` linear interpolator in `FileIO`, and the `MusicalContext` stack. No new NuGet packages are needed; no new lexer/parser tokens are needed.

The single highest-risk technical area is **DX-15 OLA varispeed**: the codebase has no prior overlap-add helper. The math is well-understood (linear-interpolation resample at ratio `r = 2^(semitones/12)`, then optional OLA for transient smoothing), but every other feature is mechanically simpler. The single highest-risk *spec* area is **DX-12 NoteValue overload disambiguation** — `NoteValueType.IsCompatibleWith(IntType) == true`, which means an Int-literal arg is ambiguous between the existing `delay(Buffer, Double, ...)` and the new `delay(Buffer, NoteValue, ...)`. Specificity scoring resolves it deterministically, but plans must include a Fact pinning the dispatch.

**Primary recommendation:** Six per-feature plans landing in two logical waves — Wave 1 (independent, parallel) ships DX-10/11/15; Wave 2 (touches Sequence + MidiExport) ships DX-12/13/14. Each plan ships its own `tests/test_dx_NN.flow` smoke + xUnit Facts and follows the Phase 18-21 RED→GREEN TDD precedent. The byte-identical regression gate (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`) MUST stay 19/19 GREEN at every commit — DX-12/13/14 in particular touch tempo/timing math and need verification.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Legato overlap semantics (DX-14)**
- **D-01:** `legato(seq, overlap)` extends each note duration to `dur × (1 + overlap)`. So overlap=0.0 = no change, overlap=0.2 = 1.2× duration, overlap=1.0 = 2× duration.
- **D-02:** Notes are allowed to overlap into the next-note onset — true polyphonic legato phrasing, not gap-filling. The audio renderer's existing polyphonic mix pipeline handles overlapping voices automatically; no new voice-allocation work needed.
- **D-03:** MIDI export emits genuinely overlapping note-on/note-off events (note-off of N happens AFTER note-on of N+1 when overlap pushes past the boundary). DryWetMidi handles this correctly per its event-stream model.

**Quantize swing semantic (DX-13)**
- **D-04:** Swing magnitude is **linear**: `offset = swing × (subdivision_length / 2)`. swing=0 → no shift; swing=1 → offbeat shifts by exactly half a subdivision (full triplet/dotted-eighth feel). Linear interpolation between. Matches DAW "swing %" sliders where 100% = full triplet swing.
- **D-05:** Swing is **signed**: positive shifts offbeats LATER (drag/jazz feel), negative shifts offbeats EARLIER (push/forward feel). swing=-0.5 and swing=+0.5 produce equal-magnitude shifts in opposite directions. Range is genuinely -1..1.
- **D-06:** Swing applies to every other subdivision at the requested resolution (the "offbeat" of the grid). For a 1/16 quantize, every 2nd 16th note shifts; for a 1/8 quantize, every 2nd 8th note shifts. Resolution determines the swing unit.

**Voicing on incomplete chords (DX-11)**
- **D-07:** Per project memory (charitable interpretation): when a chord has fewer notes than the requested voicing requires (drop2/drop3 need ≥4 notes; spread/open need ≥3 notes), `voicing` returns the input chord **unchanged**. No error, no warning, no log spam. Composer can keep iterating. This decision applies symmetrically to all named voicings — no special-casing per voicing name.
- **D-08:** This behavior is **documented in code** — `voicing` function's doc comment (in `flow-lang/StandardLibrary/Harmony/`) explicitly says "Returns input unchanged if the chord lacks enough notes for the named voicing. See Phase 22 CONTEXT D-07." So users who hit the case can grep their way to the explanation.

### Claude's Discretion

- **Plan decomposition** — User declined to discuss; trust the planner. Recommended baseline: 6 plans, one per DX-1X feature, all in Wave 1 except DX-12/DX-13 which depend on Phase 18 Fraction (already shipped). Planner should optimize for parallelism and clean per-feature reverts. If a feature is too small to justify its own plan (e.g., DX-10 may be a thin extension of existing `arpeggio`), grouping is fine.
- **`loadWav` overload disambiguation (DX-15)** — Existing `OverloadResolver` already handles Int vs Float dispatch by argument type. `loadWav("kick.wav", 12)` → semitones (Int); `loadWav("kick.wav", 1.5)` → ratio (Float).
- **Resampler choice (DX-15)** — "OLA + linear/sinc" is the spec. Recommend **linear** as the default (cheap, deterministic, "good enough" for varispeed). If quality complaints surface in UAT, sinc can be added as a future overload — out of scope for this phase.
- **Portamento CC5 mapping (DX-14)** — Spec says "CC5=64-ish events". Recommend a **linear ms→CC5 mapping** with a documented reference curve (e.g., 0ms→0, 100ms→64, 200ms→127 clamped). Document the curve in CONTEXT and the function doc comment so users can predict the value on the receiving synth.

### Deferred Ideas (OUT OF SCOPE)

- **Phase-vocoder time-preserving pitch shift for `loadWav`** — explicit anti-feature for v1.3 (varispeed-only ships in DX-15). Future v1.4 phase candidate.
- **Auto-derived chord-tone / scale-tone arpeggio sequencing beyond the basic `pattern` enum.**
- **Sinc resampler quality option for `loadWav`** — DX-15 ships linear-only by default. Adding `loadWav(path, semitones, "sinc")` is a clean future extension.
- **Configurable portamento mapping curve** — D-15 picks a linear ms→CC5 default. Exponential/per-synth-table mapping deferred to v1.4+.
- **Strict mode for `voicing`** — D-07 picks charitable-only. `voicing(chord, name, "strict")` with hard error deferred.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (from REQUIREMENTS.md L64-69) | Research Support |
|----|-------------------------------------------|------------------|
| DX-10 | `arpeggio(chord, rate, direction, pattern)` extends existing 2-arg arpeggio | Existing `HarmonyFunctions.cs:329-365` 2-arg arpeggio shipped; this phase adds a 4-arg overload sharing the same registration site and reusing the existing direction switch (currently up/down/updown — extend to add downup/random). Pattern `linear` = current behavior; `chord-tone` and `scale-tone` are stubs that fall back to `linear` for v1.3 per Deferred Ideas. |
| DX-11 | `inversion(chord, n)` and `voicing(chord, name)` for drop2/drop3/open/close/spread | `ChordParser.WithOctave` + `ChordData.NoteNames` provide the source material. New functions are pure transformations on the `string[]` note name list — no new types needed. Charitable-D-07 path: return input chord unchanged when note count < required. |
| DX-12 | `delay(buffer, noteValueRate, feedback, mix)` synced to active tempo | New 4-arg overload alongside existing `delay(Buffer, Double, Double, Double)` in `EffectsFunctions.RegisterDelay`. Reads `MusicalContext.Tempo` via `RegisterContextDependent` pattern. Computes `delayMs` from NoteValue + tempo, then DELEGATES to existing `Delay.Apply`. |
| DX-13 | `quantize(seq, resolution, strength, swing)` with grid + strength + swing | New `quantize` registration in `TransformFunctions`. Operates on `SequenceData` by walking each `BarData.MusicalNotes` list and re-deriving onsets via the `bar.ToTimeline()` pattern. Uses `MusicalContext.TimeSignature` for grid alignment. Reuses Phase 18 `Fraction` for exact rational grid math. |
| DX-14 | `legato(seq, overlap)` + `portamento(seq, glideTime)` emits CC65+CC5 | Legato: new `MusicalNoteData.DurationOverlap` field (defaulted, like Phase 19 `DurationFraction`) — read by `BarRenderer` AFTER `note.GetBeats(...)` to extend the rendered buffer length WITHOUT moving onsets. MIDI export reads the same field. Portamento: new `MusicalNoteData.PortamentoMs` field — `MidiExport` emits CC65=127 at start + CC5=mappedValue at start + CC65=0 at end (per-note bracket). |
| DX-15 | `loadWav(path, semitones)` Int + `loadWav(path, ratio)` Float overloads | Existing `FileIO.LoadWav(IReadOnlyList<Value>)` already loads RIFF + reuses internal `Resample(buffer, targetRate)` linear interpolator. New overloads call `LoadWavInternal(path)`, then call a new `VarispeedResample(buffer, ratio)` helper that runs linear interpolation at non-integer ratio (effectively the existing `Resample` math with arbitrary ratio). OLA window pass is OPTIONAL polish — pure linear-interp varispeed is sufficient for the acceptance criterion. |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Arpeggio extension (DX-10) | Standard Library / Harmony | — | Pure data transformation on `ChordData.NoteNames` → `SequenceData`. No I/O, no audio. |
| Chord inversion + voicing (DX-11) | Standard Library / Harmony | — | Pure data transformation on `ChordData.NoteNames`. Same tier as existing `chordNotes`/`chordRoot`. |
| NoteValue-rate delay (DX-12) | Standard Library / Audio DSP | Runtime / MusicalContext | Reads tempo from `MusicalContext`, then delegates to existing `Delay.Apply` (DSP tier). |
| Snap-to-grid quantize (DX-13) | Standard Library / Transforms | Runtime / MusicalContext | Reads timesig from `MusicalContext`, mutates `SequenceData` in transforms tier. |
| Legato (DX-14) | Standard Library / Transforms | Audio Renderer + MIDI Export | Transform sets a per-note flag; the audio renderer (`BarRenderer`) and MIDI exporter (`MidiExport`) BOTH consume the flag at render time. |
| Portamento (DX-14) | Standard Library / Transforms | MIDI Export | Transform sets a per-note flag; ONLY MIDI export consumes it (audio renderer ignores — portamento is a MIDI-only articulation in v1.3). |
| Varispeed loadWav (DX-15) | Standard Library / Audio FileIO | — | Pure data transformation on `AudioBuffer`. Same tier as existing `loadWav` + `Resample`. |

## Standard Stack

### Core (no new dependencies)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already locked in `flow-lang.csproj` `[VERIFIED: csproj L4]` |
| Melanchall.DryWetMidi | 8.0.3 | MIDI file write + ControlChangeEvent emission | Already referenced `[VERIFIED: csproj L11]`; supports `ControlChangeEvent(SevenBitNumber controlNumber, SevenBitNumber controlValue)` ctor `[CITED: melanchall.github.io/drywetmidi/api/...ControlChangeEvent]` |
| xUnit.v3 | 3.2.2 | Unit + integration test framework | Already locked in `flow-lang.Tests.csproj` `[VERIFIED]` |

### No additions needed.

**Verification:** `dotnet list flow-lang/flow-lang.csproj package` against the csproj file — DryWetMidi 8.0.3 is the locked version. No NuGet adds are warranted for any DX-1X feature.

## Architecture Patterns

### System Architecture Diagram

```
                                User .flow source
                                       │
                                       ▼
                              SimpleLexer → Parser → AST
                                       │
                                       ▼
                                  Interpreter
                       (resolves overloads via OverloadResolver)
                                       │
              ┌────────────────────────┼────────────────────────┐
              ▼                        ▼                        ▼
     InternalFunctionRegistry    MusicalContext stack     ExecutionContext
     (name → signature →         (tempo / timesig /       (call stack /
      C# delegate)                 key / ...)              variable scope)
              │                        │
              │       reads tempo, timesig at call time
              ▼                        │
   ┌──────────────────────────┐        │
   │ Standard Library tiers:  │◀───────┘
   │  • Harmony  (DX-10/11)   │
   │  • Transforms (DX-13/14) │
   │  • Audio DSP (DX-12)     │
   │  • Audio FileIO (DX-15)  │
   └──────────────────────────┘
              │
              │   produces SequenceData / AudioBuffer / ChordData / Voice list
              │
              ▼
   ┌──────────────────────────┐       ┌──────────────────────────┐
   │  SongRenderer            │       │  MidiExport              │
   │  → BarRenderer           │       │  → walks Song hierarchy  │
   │  → SequenceRenderer      │       │  → emits TimedEvents:    │
   │  → MixVoicesToStereo     │       │    NoteOn / NoteOff      │
   │     (additive sum)       │       │    + (Phase 22) CC65/CC5 │
   │  → AudioBuffer (WAV)     │       │  → DryWetMidi MidiFile   │
   └──────────────────────────┘       └──────────────────────────┘
              │                                   │
              ▼                                   ▼
   FileIO.WriteWav(path)              MidiExport.WriteMidi(path)
        (PCM 16/24/32 + dither)          (DryWetMidi.Write)
```

The Phase 22 features hook into this pipeline at four touch points:
1. **Harmony tier** (DX-10/11): pure functional transforms on `ChordData` → `SequenceData`
2. **Transforms tier** (DX-13/14): pure functional transforms on `SequenceData` → `SequenceData` (with new per-note flags)
3. **Audio DSP tier** (DX-12): reads `MusicalContext.Tempo`, dispatches to existing `Delay.Apply`
4. **Audio FileIO tier** (DX-15): reads RIFF, runs varispeed resample, returns `AudioBuffer`
5. **Render-time consumers**: `BarRenderer` reads new `DurationOverlap`; `MidiExport` reads `DurationOverlap` + `PortamentoMs`

### Recommended Component Layout (per feature)

| Feature | Files Touched | Files Added | Lines (est.) |
|---------|---------------|-------------|--------------|
| DX-10 arpeggio extension | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`, `flow-lang/std.flow` | `tests/test_dx_arpeggio.flow`, `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs` | ~80 prod + ~120 test |
| DX-11 inversion + voicing | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`, `flow-lang/std.flow` | `flow-lang/StandardLibrary/Harmony/Voicings.cs` (new file for clarity), `tests/test_dx_voicings.flow`, `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs` | ~150 prod + ~180 test |
| DX-12 delay sync | `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs`, `flow-lang/StandardLibrary/BuiltInFunctions.cs` (RegisterContextDependentFunctions), `flow-lang/audio.flow` | `tests/test_dx_delay_sync.flow`, `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs` | ~60 prod + ~100 test |
| DX-13 quantize | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`, `flow-lang/std.flow` | `tests/test_dx_quantize.flow`, `flow-lang.Tests/Unit/Phase22/QuantizeFacts.cs` | ~150 prod + ~180 test |
| DX-14 legato + portamento | `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (MusicalNoteData adds 2 defaulted fields), `flow-lang/StandardLibrary/Audio/BarRenderer.cs` (reads DurationOverlap), `flow-lang/StandardLibrary/Audio/MidiExport.cs` (reads PortamentoMs + DurationOverlap), `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`, `flow-lang/std.flow` | `tests/test_dx_legato.flow`, `tests/test_dx_portamento.flow`, `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs`, `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs` | ~180 prod + ~250 test |
| DX-15 varispeed loadWav | `flow-lang/StandardLibrary/Audio/FileIO.cs` (new VarispeedResample helper + 2 new LoadWav overloads), `flow-lang/StandardLibrary/BuiltInFunctions.cs` (registration), `flow-lang/audio.flow` | `tests/test_dx_loadwav_varispeed.flow`, `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs` | ~80 prod + ~140 test |

### Pattern 1: Defaulted-Parameter Field Migration (DX-14)
**What:** Add new optional fields to `MusicalNoteData` at END of constructor, defaulting to null/0 — every existing call site continues to compile.
**When to use:** Any time a transform needs to attach per-note metadata that consumers downstream MUST read.
**Source:** Phase 18 Plan 18-02 SUMMARY established this as the canonical migration pattern (`DurationFraction` shipped via this exact mechanism, zero call-site edits across 30+ existing constructors).
**Example:**
```csharp
// flow-lang/TypeSystem/SpecialTypes/NoteType.cs:246 (CURRENT signature)
public MusicalNoteData(char noteName, int octave, int alteration, int? durationValue, bool isRest,
    double? centOffset = null, bool isTied = false, double velocity = 0.63,
    Articulation articulation = Articulation.Normal, bool isDotted = false,
    FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0,
    FlowLang.TypeSystem.Fraction? durationFraction = null,
    // Phase 22 DX-14 additions — both default to null/0 = "not set":
    double durationOverlap = 0.0,    // legato: render-time duration extension factor
    double portamentoMs = 0.0)        // portamento: glide time for MIDI CC5 mapping
```

### Pattern 2: NoteValue-rate Built-in Overload (DX-12, applies to DX-13 too)
**What:** Register a NEW signature alongside the existing one, and delegate the body to a thin computation that converts NoteValue → time using `MusicalContext.Tempo`.
**When to use:** Any built-in that already has a "raw time" overload (ms / seconds / int) and now needs a "musical time" version.
**Example:**
```csharp
// flow-lang/StandardLibrary/Audio/EffectsFunctions.cs (NEW — see DX-12)
private static void RegisterDelay(InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    // existing ms-rate overload (UNCHANGED)
    var delaySig = new FunctionSignature("delay",
        [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance]);
    registry.Register("delay", delaySig, DelayEffect);

    // NEW: NoteValue-rate overload (DX-12)
    var delaySyncedSig = new FunctionSignature("delay",
        [BufferType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance]);
    registry.Register("delay", delaySyncedSig, args =>
    {
        var buffer = args[0].As<AudioBuffer>();
        int noteValueEnum = args[1].As<int>();   // NoteValue is backed by int (NoteValueType.cs:18)
        float feedback = (float)args[2].As<double>();
        float mix = (float)args[3].As<double>();

        double bpm = context.GetMusicalContext().Tempo ?? 120.0;
        double delayMs = NoteValueToMs((NoteValueType.Value)noteValueEnum, bpm);

        var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
        return Value.Buffer(result);
    });
}

private static double NoteValueToMs(NoteValueType.Value nv, double bpm)
{
    // QUARTER at 120 BPM = 60000/120 = 500ms; EIGHTH = 250ms; etc.
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
**Note:** This requires moving the registration from the parameterless `Register` to the context-aware `RegisterContextDependent` pathway (the same pathway `RegisterEuclideanOverloads` uses for swing — see `BuiltInFunctions.cs:1184`). The existing ms-rate overload can stay on the no-arg `Register` path or move alongside; either way preserves the public API.

### Pattern 3: Charitable Voicing (DX-11)
**What:** When inputs are insufficient, return inputs unchanged silently. Document in code.
**When to use:** When the user's intent is well-defined for the valid case and the invalid case has no useful return shape.
**Source:** Project memory `feedback_charitable_interpretation.md` — "music > rigid correctness". Direct CONTEXT D-07/D-08 application.
**Example:**
```csharp
/// <summary>
/// drop2(chord) — lowers the 2nd-from-top note by an octave.
///
/// Per Phase 22 CONTEXT D-07 (charitable interpretation): if the chord has
/// fewer than 4 notes, this returns the input chord unchanged. No error,
/// no warning. Composer can keep iterating with smaller voicings.
/// </summary>
private static ChordData Drop2(ChordData input)
{
    if (input.NoteNames.Length < 4)
        return input;          // CONTEXT D-07
    // ... real drop-2 transform
}
```

### Pattern 4: Linear-Interpolation Resample (DX-15)
**What:** Walk source samples at fractional positions, interpolate between adjacent samples.
**When to use:** Any resample where transient quality is acceptable but determinism + simplicity dominate.
**Source:** `flow-lang/StandardLibrary/Audio/FileIO.cs:441-465` — the existing `Resample` already implements this pattern for sample-rate conversion. DX-15 reuses the math with a different "ratio" semantic (input ratio = `2^(semitones/12)` instead of `srcRate/targetRate`).
**Example:**
```csharp
// New helper in FileIO.cs — mirrors existing Resample but with arbitrary ratio
public static AudioBuffer VarispeedResample(AudioBuffer source, double ratio)
{
    // ratio > 1.0 = higher pitch = fewer output frames
    // ratio = 2.0 (12 semitones) → output frames = source.Frames / 2
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
**Pure linear-interp varispeed is sufficient.** The "OLA" framing in REQUIREMENTS is aspirational — for the v1.3 acceptance criterion (`loadWav("kick.wav", 12)` returns a buffer one octave higher with sample count halved), pure linear interpolation at `ratio=2.0` produces an exactly-half-length output that plays one octave higher. OLA windowed crossfade is needed only for transient-density preservation, which is Tier-D polish out of scope for this phase.

### Anti-Patterns to Avoid

- **Creating a parallel `arpeggio2` function** — Per CONTEXT code-context note ("DX-10 extends in place; do not create a parallel function"). Add the 4-arg overload alongside the existing 2-arg signature in `HarmonyFunctions.Register`.
- **Breaking `loadWav(path)` byte-identity** — The existing 1-arg `LoadWav` MUST stay byte-for-byte identical with its current behavior. The new 2-arg overloads are NEW signatures; the 1-arg path runs verbatim.
- **Computing legato via DurationFraction or DurationValue** — Mutating duration moves the next note's onset (because `bar.ToTimeline()` advances `currentBeat += note.GetBeats(...)`). Use a SEPARATE `DurationOverlap` field that the renderer adds to `durationBeats` AFTER `ToTimeline()` produces onsets. This mirrors how `IsTied` already works (BarRenderer.cs:67-72).
- **Resetting `MusicalContext` per-call inside DX-12** — The context is read-only at the call site. The OverloadResolver/registry pattern provides `context` via the `RegisterContextDependent` closure, NOT via global state.
- **Touching `MusicalContext` from DX-15** — `loadWav` is a pure file-loader; it has no business reading tempo. `semitones`/`ratio` are absolute pitch-shift inputs.
- **Adding a new lexer/parser token for any DX-1X** — All features ride on existing identifiers (`"up"`, `"linear"`, `"drop2"`, `EIGHTH`, `100ms`). Zero grammar changes.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Sample-rate conversion / linear-interp resample | A new sample-walker loop | Reuse `FileIO.Resample` math; copy the loop into a new `VarispeedResample` helper that takes `ratio` directly | Already battle-tested in the codebase; identical algorithm |
| MIDI Control Change emission | Hand-coded VLQ + 0xB0 status byte | `new ControlChangeEvent(SevenBitNumber controllerNum, SevenBitNumber value)` wrapped in `TimedEvent` and added to the existing `noteEvents` list in `MidiExport.cs` | DryWetMidi 8.0.3 already linked; SMF handles VLQ delta-time correctly |
| Polyphonic voice mix for legato | New voice allocator that handles overlapping notes | `MixVoicesToStereoBuffer` already does additive mix with per-voice `OffsetBeats` (SongRenderer.cs:184-225). One note = one Voice; voices that share frames sum together. | Already shipped and verified by 19/19 byte-identical regression gate |
| Roman numeral / scale-tone resolution (DX-10 future) | New scale walker | `HarmonyFunctions.scaleNotes` + `ScaleDatabase.ResolveRomanNumeral` | Already exists; `chord-tone`/`scale-tone` patterns can stub to `linear` for v1.3 and route here later |
| Fraction arithmetic for grid math (DX-13) | New ratio struct | Phase 18 `FlowLang.TypeSystem.Fraction` — supports `+`, `*`, `<`, `>`, GCD-normalizing constructor | Shipped Phase 18; tested by 9 FractionTests Facts |
| NoteValue → Fraction conversion (DX-12, DX-13) | New mapping function | `NoteStreamCompiler.cs:289-294` already has the canonical mapping — extract to a public helper or duplicate the 6-line switch | One source of truth; matches music21 quarter-note-units convention used throughout the codebase |
| Tempo lookup from active context | New thread-local | `context.GetMusicalContext().Tempo ?? 120.0` — the canonical pattern used by every existing context-dependent built-in (Interpreter.cs:200, 210, 386; ExpressionEvaluator.cs:519, 530) | Already structured for context-dependent built-ins via `RegisterContextDependentFunctions` |

**Key insight:** Every Phase 22 feature is structurally a thin layer over existing infrastructure. The only NEW algorithmic content is (a) the linear/sinc varispeed math (covered by `Resample` template) and (b) the swing-aware quantize math (covered below in Code Examples).

## Code Examples

Verified patterns from official sources / existing codebase:

### DX-10: Extended `arpeggio` registration

```csharp
// flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs (extends Register at line 327-365)

// EXISTING (KEEP UNCHANGED):
var arpeggioSignature = new FunctionSignature("arpeggio", [ChordType.Instance, StringType.Instance]);
registry.Register("arpeggio", arpeggioSignature, args => { /* existing 2-arg body */ });

// NEW (DX-10 4-arg overload):
var arpeggioFullSig = new FunctionSignature("arpeggio",
    [ChordType.Instance, NoteValueType.Instance, StringType.Instance, StringType.Instance]);
registry.Register("arpeggio", arpeggioFullSig, args =>
{
    var chord = args[0].As<ChordData>();
    int rateEnum = args[1].As<int>();      // NoteValue backed by int
    var direction = args[2].As<string>();
    var pattern = args[3].As<string>();    // "linear" | "chord-tone" | "scale-tone"

    var noteNames = ApplyDirection(chord.NoteNames.ToList(), direction);
    // For v1.3, all three patterns route to linear (chord-tone/scale-tone deferred).
    // Document this in the doc comment with a CONTEXT pointer.

    var musicalNotes = noteNames.Select(n => {
        var (name, oct, alt) = NoteType.Parse(n);
        return new MusicalNoteData(name, oct, alt, rateEnum, isRest: false);
    }).ToList();

    var bar = new BarData(musicalNotes, new TimeSignatureData(4, 4));
    var seq = new SequenceData();
    seq.AddBar(bar);
    return Value.Sequence(seq);
});

// New helper for the additional directions:
private static List<string> ApplyDirection(List<string> notes, string direction) =>
    direction.ToLower() switch
    {
        "down"   => notes.AsEnumerable().Reverse().ToList(),
        "updown" => notes.Concat(notes.AsEnumerable().Reverse().Skip(1)).ToList(),
        "downup" => notes.AsEnumerable().Reverse().Concat(notes.Skip(1)).ToList(),
        "random" => notes.OrderBy(_ => _seededArpeggioRng.Next()).ToList(),
        _        => notes,                  // "up" or unknown → unchanged
    };
```
**Source:** Existing `HarmonyFunctions.cs:329-365`; mirror of EuclideanOverloads pattern at `BuiltInFunctions.cs:1184`.

### DX-11: Inversion + drop2 voicing

```csharp
// flow-lang/StandardLibrary/Harmony/Voicings.cs (new file, registered from HarmonyFunctions.Register)

public static class Voicings
{
    /// <summary>
    /// inversion(chord, n) — rotates the lowest note up an octave n times.
    /// Per CONTEXT D-07 charitable interpretation: when n exceeds chord.NoteNames.Length,
    /// returns input unchanged. n=0 returns input unchanged. Negative n is documented
    /// as undefined and returns input unchanged.
    /// </summary>
    public static ChordData Inversion(ChordData input, int n)
    {
        if (n <= 0 || n >= input.NoteNames.Length) return input;
        var notes = input.NoteNames.ToList();
        for (int i = 0; i < n; i++)
        {
            string lowest = notes[0];
            notes.RemoveAt(0);
            notes.Add(RaiseOctave(lowest));   // helper below
        }
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// voicing(chord, "drop2") — lowers the 2nd-from-top note by an octave. Common in jazz
    /// guitar/piano comping. Per CONTEXT D-07: returns input unchanged if note count < 4.
    /// </summary>
    private static ChordData Drop2(ChordData input)
    {
        if (input.NoteNames.Length < 4) return input;       // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        int idx = notes.Count - 2;                            // second from top
        notes[idx] = LowerOctave(notes[idx]);
        notes.Sort((a, b) => CompareByPitch(a, b));            // re-sort low-to-high
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    // ... drop3, open, close, spread similar shape
}
```
**Source:** `ChordData.NoteNames` is `string[]` of formatted note names like `"C4"`/`"E4"`/`"G4"`/`"B4"` (verified at `ChordParser.cs:172-176` — `displayNote = $"{noteName[0]}{noteOctave}+"` for sharps; matches `NoteType.Format` output).

### DX-12: Smoke test (acceptance from REQUIREMENTS DX-12)

```flow
Note: tests/test_dx_delay_sync.flow — DX-12 acceptance smoke
use "@audio"
use "@notation"

Note: At 120 BPM, an EIGHTH note = 250ms. Expect delay-tail of ~250ms.
Buffer src = (sine 440.0 0.5 44100)
tempo 120 {
    Buffer wet = (delay src EIGHTH 0.5 0.4)
    (writeWav "tests/output/test_dx_delay_sync.wav" wet)
}
(print "DX-12 delay sync: PASSED")
```
The Fact pinning the math:
```csharp
[Fact]
public void DelayWithEighthAtTempo120_ProducesQuarterSecondDelayMs()
{
    // EIGHTH at 120 BPM = (60000/120) / 2 = 250ms
    Assert.Equal(250.0, DelayDxHelpers.NoteValueToMs(NoteValueType.Value.EIGHTH, 120.0), precision: 1);
    Assert.Equal(500.0, DelayDxHelpers.NoteValueToMs(NoteValueType.Value.QUARTER, 120.0), precision: 1);
    Assert.Equal(125.0, DelayDxHelpers.NoteValueToMs(NoteValueType.Value.EIGHTH, 240.0), precision: 1);
}
```

### DX-13: Quantize core algorithm (per D-04..D-06)

```csharp
// flow-lang/StandardLibrary/Transforms/TransformFunctions.cs (new section)

private static SequenceData QuantizeSequence(SequenceData seq,
    NoteValueType.Value resolution, double strength, double swing)
{
    strength = Math.Clamp(strength, 0.0, 1.0);
    swing = Math.Clamp(swing, -1.0, 1.0);

    // CONTEXT D-04: subdivision_length is the resolution duration in beats
    //   resolution=EIGHTH @ 4/4 → 0.5 beats per subdivision
    //   resolution=SIXTEENTH @ 4/4 → 0.25 beats per subdivision
    var resFraction = NoteValueToQuarterFraction(resolution);
    // (NoteValueToQuarterFraction is the canonical mapping at NoteStreamCompiler.cs:289-294)

    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        int denom = bar.TimeSignature?.Denominator ?? 4;
        double subdivisionBeats = (double)resFraction.Num * denom / (resFraction.Denom * 4.0);
        // CONTEXT D-04: swing offset is signed × half-subdivision
        double swingOffset = swing * (subdivisionBeats / 2.0);

        // Walk each note's actual onset; snap to nearest grid point at strength.
        var newNotes = new List<MusicalNoteData>();
        double currentBeat = 0;
        int subdivIdx = 0;
        foreach (var note in bar.MusicalNotes)
        {
            double targetGrid = Math.Round(currentBeat / subdivisionBeats) * subdivisionBeats;
            // CONTEXT D-06: every other subdivision (the "offbeat") gets the swing shift
            if (subdivIdx % 2 == 1) targetGrid += swingOffset;
            // strength=1 hard-quantize; strength=0 no change
            double snappedBeat = currentBeat + strength * (targetGrid - currentBeat);
            // (Onset stored implicitly by walking notes in order; snappedBeat informs how
            //  we re-build the note list — see "Sequence onset model" pitfall below.)
            newNotes.Add(note);   // pseudocode — see pitfall #4 for the actual rebuild path
            currentBeat += note.GetBeats(denom);
            subdivIdx++;
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));
    }
    return result;
}
```
**Pitfall:** `BarData.MusicalNotes` is a sequential list — onsets are IMPLICITLY computed by `bar.ToTimeline()` walking in order and accumulating durations. To shift a single note's onset later, you must INSERT a rest before it OR adjust the previous note's duration. See "Sequence onset model" pitfall in Common Pitfalls below.

### DX-14: Legato + portamento integration with renderer + MIDI

```csharp
// flow-lang/StandardLibrary/Audio/BarRenderer.cs (extends existing IsTied logic at line 67-72)

// EXISTING — DO NOT REMOVE:
if (note.IsTied)
{
    double overlapSeconds = 0.1;
    double overlapBeats = (overlapSeconds / 60.0) * bpm;
    durationBeats += overlapBeats;
}

// NEW (DX-14 legato): extend by overlap factor BEFORE rendering audio buffer.
// The bar timeline already produced offsetBeats for this note; we ONLY change
// how long this note's audio buffer plays. Onset is NOT moved.
if (note.DurationOverlap > 0.0)
{
    durationBeats *= (1.0 + note.DurationOverlap);   // CONTEXT D-01
}
```

```csharp
// flow-lang/StandardLibrary/Audio/MidiExport.cs (extends ExportMidiInternal note loop at line 245-275)

foreach (var note in bar.MusicalNotes)
{
    if (note.IsRest) { /* existing rest handling */ continue; }

    int midiNote = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
    byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);

    double beats = note.GetBeats(barTimeSigDenom);
    // DX-14 legato in MIDI: extend duration by overlap factor (CONTEXT D-03 — overlapping
    // note-on/note-off events are valid SMF; DryWetMidi handles correctly)
    double extendedBeats = note.DurationOverlap > 0
        ? beats * (1.0 + note.DurationOverlap)
        : beats;
    long durationTicks = (long)(extendedBeats * ticksPerQuarter);

    // DX-14 portamento: emit CC65=127 at note start + CC5=mappedTime at note start.
    // Per CONTEXT D-15: linear ms → CC5 mapping (0ms→0, 100ms→64, 200ms→127 clamped).
    if (note.PortamentoMs > 0.0)
    {
        byte cc5Value = (byte)Math.Clamp((int)Math.Round(note.PortamentoMs * 127.0 / 200.0), 0, 127);
        noteEvents.Add(new TimedEvent(
            new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)127), barTick));
        noteEvents.Add(new TimedEvent(
            new ControlChangeEvent((SevenBitNumber)5, (SevenBitNumber)cc5Value), barTick));
    }

    noteEvents.Add(new TimedEvent(
        new NoteOnEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)velocity), barTick));
    noteEvents.Add(new TimedEvent(
        new NoteOffEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)0),
        barTick + durationTicks));

    // Bracket portamento: CC65=0 at note end (turn off). Optional but cleaner for receivers.
    if (note.PortamentoMs > 0.0)
    {
        noteEvents.Add(new TimedEvent(
            new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)0),
            barTick + durationTicks));
    }

    // CRITICAL: barTick advances by the ORIGINAL beats, not extendedBeats —
    // this is what makes legato OVERLAP rather than just slow the song down.
    barTick += (long)(beats * ticksPerQuarter);
}
```
**Source:** Existing `MidiExport.cs:245-275` (note loop) + `ControlChangeEvent` API documented at melanchall.github.io.

### DX-15: Varispeed semitones overload + ratio overload

```csharp
// flow-lang/StandardLibrary/Audio/FileIO.cs (extends LoadWav)

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
```
And the registration at `BuiltInFunctions.cs:552-554`:
```csharp
// EXISTING:
var loadWavSignature = new FunctionSignature("loadWav", [StringType.Instance]);
registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav);

// NEW DX-15 overloads:
var loadWavSemiSig = new FunctionSignature("loadWav", [StringType.Instance, IntType.Instance]);
registry.Register("loadWav", loadWavSemiSig, Audio.FileIO.LoadWavSemitones);

var loadWavRatioSig = new FunctionSignature("loadWav", [StringType.Instance, DoubleType.Instance]);
registry.Register("loadWav", loadWavRatioSig, Audio.FileIO.LoadWavRatio);
```
**Acceptance Fact:**
```csharp
[Fact]
public void LoadWav_12Semitones_HalvesSampleCount()
{
    // Generate a synthetic test WAV at known sample count
    var src = SynthSineToBuffer(440.0, 1.0, 44100);   // 44100 frames = 1 sec
    var pitched = FileIO.VarispeedResample(src, 2.0);   // 12 semitones = ratio 2.0
    Assert.InRange(pitched.Frames, 22049, 22051);       // ~22050 ± 1 for OLA edge tolerance
    Assert.Equal(src.Channels, pitched.Channels);
    Assert.Equal(src.SampleRate, pitched.SampleRate);
}
```

## Common Pitfalls

### Pitfall 1: NoteValue + Int dispatch ambiguity (DX-12, DX-15)
**What goes wrong:** `NoteValueType.IsCompatibleWith(IntType) == true` (`NoteValueType.cs:18-19`). When the user calls `delay(buf, 250, 0.5, 0.4)` with an Int literal, BOTH the existing `delay(Buffer, Double, ...)` and the new `delay(Buffer, NoteValue, ...)` overloads "match." `OverloadResolver` then ranks by specificity — for an exact-match Int arg vs a NoteValue param, both score as "compatible" (500). For Int → Double, also "compatible" (500). Result: ambiguous overload error.
**Why it happens:** `Value.ConvertTo` (`Value.cs:96`) explicitly maps `Int → NoteValue`. The existing `delay(Buffer, Double, Double, Double)` accepts Int via `Int → Double` conversion. Both score 500.
**How to avoid:**
- For DX-12: document that musical-time delay REQUIRES the NoteValue named constant (`EIGHTH`, `QUARTER`, etc., from `notation.flow:28-33`) — not a bare integer. Plan should ship a Fact `DelaySync_BareIntegerArgument_Ambiguous_OrPicksDouble` that pins the resolution behavior under both possible specificity scoring outcomes.
- For DX-15: NO ambiguity. `loadWav(path, 12)` → Int param matches `IntType` exactly (specificity 1000); `loadWav(path, 1.5)` → Double param matches `DoubleType` exactly (specificity 1000). The only risk is if OverloadResolver considers Int `compatible` with Double — confirm with a Theory pinning both calls.
**Warning signs:** Stderr output `"Ambiguous overload for function 'delay'"` from `OverloadResolver.cs:75-80`.

### Pitfall 2: Sequence onset model — onsets are IMPLICIT (DX-13)
**What goes wrong:** `BarData.MusicalNotes` is a `List<MusicalNoteData>` with no per-note `onsetBeats` field. Onsets are recomputed via `bar.ToTimeline()` (`BarType.cs:159-178`) by walking the list and accumulating each note's `GetBeats(...)`. To shift a single note's onset (DX-13 quantize), you can't just "set onset = X" — you must either (a) modify the prior note's duration, or (b) insert a rest, or (c) add a new "onset offset" field to MusicalNoteData (defaulted-parameter migration pattern).
**Why it happens:** The data model was designed for sequential rendering, not freeform onset manipulation.
**How to avoid (RECOMMENDED for DX-13):** Add a `double OnsetOffset` field (defaulted to 0.0) to MusicalNoteData via the same defaulted-parameter migration pattern Phase 18 used for `DurationFraction`. Then `bar.ToTimeline()` adds `OnsetOffset` to each computed beat position. This is the cleanest model and lets the audio renderer + MIDI export both honor quantization without parallel rebuilds.
**Alternative:** Quantize works by adjusting prior-note durations (e.g., to delay note N, lengthen note N-1; or insert/grow a rest before N). This avoids the data-model migration but is harder to reason about for `strength < 1.0`.
**Warning signs:** Quantized output has CORRECT onsets in MIDI but WRONG onsets in WAV (or vice versa) → renderer and MIDI export are reading different timing fields.

### Pitfall 3: Legato changes durations, NOT onsets (DX-14)
**What goes wrong:** If you `legato(seq, 0.5)` and naively set `note.DurationValue` to a longer enum, `bar.ToTimeline()` will move the next note's `offsetBeats` later — and the song slows down by 50% instead of legato-overlapping.
**Why it happens:** `bar.ToTimeline()` advances `currentBeat += note.GetBeats(...)`. If duration grows, onsets shift.
**How to avoid:** Use the SAME mechanism `IsTied` uses — extend the rendered audio buffer length AFTER `ToTimeline()` produces onsets. Concretely: add a new `DurationOverlap` field that `BarRenderer` and `MidiExport` BOTH read post-onset to extend `durationBeats`/`durationTicks`. The note's onset stays put.
**Warning signs:** Comparing two-runner WAV byte counts: a 4-beat sequence with `legato(seq, 1.0)` should produce APPROXIMATELY the same WAV length (within one note's render-tail) as the original — NOT double the length. If output doubles, you mutated DurationValue instead of using the overlap field.

### Pitfall 4: Voice mixer truly is additive (DX-14 legato verification)
**What goes wrong:** Assuming `MixVoicesToStereoBuffer` does some kind of monophonic muting or last-note-wins logic.
**Why it happens:** Many DAW pipelines hard-clip overlapping voices on monophonic synths.
**How to avoid:** READ the code at `SongRenderer.cs:200-221` — it literally does `result.SetSample(destFrame, 0, result.GetSample(destFrame, 0) + sample * leftGain)`. Pure additive mix. So legato D-02 is structurally guaranteed: any two voices whose buffers overlap in `destFrame` ranges will SUM together, which is what polyphonic legato sounds like.
**Warning signs:** Smoke-test legato note SOUNDS like one note ducks the other → you're not actually using `MixVoicesToStereoBuffer`; some other render path stripped it.

### Pitfall 5: ChordData.NoteNames format — sharps use `+`, not `s` or `#` (DX-11)
**What goes wrong:** `ChordParser.ExpandIntervals` (`ChordParser.cs:172-176`) emits notes like `"C4"`/`"E4"`/`"G4"` for naturals and `"C4+"`/`"D4+"` etc. for sharps (canonical Phase 14 DX-06 form). If DX-11 inversion code naively appends `"s"` or `"#"` to construct shifted notes, `NoteType.Parse` will work (it accepts all of `s`/`#`/`+`), but the round-trip won't be byte-canonical.
**Why it happens:** Project standardized on `+`/`-` notation in Phase 14 DX-06; chord-internal `s`/`f` is a different format.
**How to avoid:** Use `NoteType.Parse(name) → (letter, octave, alteration)` to decompose, manipulate `octave` only, then `NoteType.Format(letter, newOctave, alteration)` to re-emit. This guarantees canonical form and round-trip equality.
**Warning signs:** Inversion test's expected `"E4"` matches but `"G4"` test expects `"G4+"` and gets `"G4#"` (or vice versa). Always go through `NoteType.Format`.

### Pitfall 6: `ChordData.NoteNames` may NOT contain explicit octaves at chord-literal parse time
**What goes wrong:** Look at `ChordParser.cs:142` — `int octave = 4;` is hardcoded for chord-literal parses. So `Cmaj` produces `["C4", "E4", "G4"]` with default octave 4. But REQUIREMENTS DX-11 says `inversion(Cmaj, 1)` returns `[E4, G4, C5]` — so the inversion MUST raise C from octave 4 to octave 5. The base octave in chord must be respected; can't assume 4.
**Why it happens:** ChordData carries `Octave` separately from `NoteNames` — `Octave` is the BASE octave, but each name in `NoteNames` already includes its specific octave (with overflow handled — see ExpandIntervals).
**How to avoid:** Always parse via `NoteType.Parse(name)` to get the actual octave per-note. Don't assume "Cmaj7's notes are all in octave 4" — the 7th may already be in octave 5.
**Warning signs:** Inversion test fails for chords where the chord spans an octave (e.g. Cmaj9 already has its 9th in octave 5).

### Pitfall 7: Random direction in arpeggio needs deterministic seed for byte-identical regression (DX-10)
**What goes wrong:** `direction = "random"` calls `Random.Next()` from a static field. If the seed is undefined or shared globally, two consecutive runs of `tutorial.flow` produce different note orderings → ByteIdenticalTutorialTests goes RED.
**Why it happens:** Phase 15 DX-09 had the exact same issue with euclidean — solved by using `LOCAL new Random(seed)` per call (CONTEXT D-17). All Phase 22 random paths must follow this precedent.
**How to avoid:** EITHER (a) DO NOT support `"random"` in v1.3 — defer per CONTEXT Deferred Ideas — or (b) require an additional seed argument: `arpeggio(chord, rate, "random", "linear", seed)` — making it a 5-arg overload distinct from the 4-arg pattern. Recommendation: stub `"random"` → falls back to `"up"` for v1.3 with a doc comment noting "seeded random arpeggio deferred to v1.4".
**Warning signs:** `ByteIdenticalTutorialTests.Tutorial_TwoRunsProduceIdenticalWav` flips RED after a `random` arpeggio appears in the tutorial.

### Pitfall 8: VarispeedResample edge tolerance for sample count (DX-15)
**What goes wrong:** REQUIREMENTS DX-15 says `loadWav("kick.wav", 12)` should return a buffer with sample count "halved." Pure linear-interp at ratio 2.0 produces `(int)(srcFrames / 2.0)` = exactly half rounded down. CONTEXT specifics line 116 says "must assert sample-count exactly halves (within ±1 for OLA window edge)."
**Why it happens:** Integer truncation of fractional sample counts. For odd source frame counts, output is `floor(N/2)`.
**How to avoid:** Test assertion uses `Assert.InRange(pitched.Frames, src.Frames/2 - 1, src.Frames/2 + 1)` — ±1 tolerance covers both rounding modes.
**Warning signs:** Strict `Assert.Equal(22050, pitched.Frames)` fails when source is 44099 frames → use InRange.

### Pitfall 9: Quantize must respect Phase 18 byte-identical regression
**What goes wrong:** DX-13's quantize touches `MusicalNoteData` construction in transforms tier. If quantize's "no-op" path (strength=0 or default) doesn't preserve byte-identity with a non-quantized sequence, EVERY subsequent rendered WAV/MIDI of any tutorial sequence diverges.
**Why it happens:** Phase 18 byte-identical contract demands non-quantized rendering produce the EXACT same bytes as before Phase 22. If `quantize` is called somewhere in tutorial.flow but produces a slightly-different note list at strength=0, both ByteIdenticalTutorialTests Facts go RED.
**How to avoid:** quantize at strength=0 MUST return the input sequence unmodified (identity short-circuit). Test this directly: `(quantize seq r 0.0 0.0)` produces a Sequence whose .ToString() and rendered output are byte-identical to the input.
**Warning signs:** `ByteIdenticalTutorialTests.Tutorial_TwoRunsProduceIdenticalWav` GREEN but `Tutorial_TwoRunsProduceIdenticalMidi` flips RED → the MIDI tick math differs by 1 somewhere; bisect to a quantize identity edge case.

### Pitfall 10: NoteValue exposure outside note streams requires `use "@notation"`
**What goes wrong:** REQUIREMENTS examples write `delay(buf, e, 0.5, 0.4)`. The token `e` is NOT registered as a stdlib constant — only `EIGHTH`/`QUARTER`/etc. are (`notation.flow:28-33`). So `delay(buf, e, 0.5, 0.4)` is a parse-time error: "Variable 'e' not found."
**Why it happens:** Single-char `q`/`e`/`h`/`w` tokens are special inside `| C4 q D4 e |` note streams (NoteStreamCompiler), but NOT regular identifiers outside.
**How to avoid:**
- (Option A — recommended) Document that DX-12 acceptance examples use `EIGHTH`/`QUARTER`/etc. constants from `@notation`. Smoke test imports `@notation`.
- (Option B) Add new lowercase aliases `NoteValue e = 3` etc. to `notation.flow`. Cheap, but pollutes the global namespace with single-char identifiers.
- (Option C — out of scope) Add a parser path so `e` outside note streams resolves to a NoteValue. Heavy lift; defer to a future phase.
**Warning signs:** `delay(buf, e, ...)` fails with "Variable 'e' not found" at parse time — this is correct behavior; documentation needs updating, not the registration.
**Recommendation:** Option A. The user-facing example in tutorial.flow / smoke test reads `delay(buf, EIGHTH, 0.5, 0.4)` — clear, deterministic, no grammar work.

## Runtime State Inventory

(Phase 22 is greenfield additions — no rename/refactor scope. Section omitted per template guidance.)

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hand-rolled int Numerator/Denominator pairs | `FlowLang.TypeSystem.Fraction` (Phase 18) | 2026-04-22 (commit 2092f32) | DX-12 + DX-13 use directly for exact tempo/grid math |
| Power-of-2 enum DurationValue only | DurationFraction defaulted-parameter override (Phase 18 FRAC-02) | 2026-04-22 (commit ba8534a) | DX-13 onset-shift mechanism follows same pattern (defaulted parameter, dormant unless set) |
| Static unseeded `Random` for euclidean / synth-noise | LOCAL `new Random(seed)` per-call + reseeded RNG at write-boundary (Phase 15) | 2026-04-23 | DX-10 `random` arpeggio direction MUST follow this — or defer to v1.4 (Pitfall 7) |
| Bjorklund-only euclidean | Euclidean + swing + humanize overloads (Phase 15 DX-09) | 2026-04-23 | DX-13 quantize swing semantics align with this signed `[-1, 1]` convention |

**Deprecated/outdated:**
- Nothing — all referenced infrastructure is current.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `loadWav("kick.wav", 12)` Int dispatch is unambiguous against `loadWav("kick.wav", 1.5)` Double dispatch | Pitfall 1 | LOW — both args are exact-type matches (Int 1000, Double 1000); only risk is OverloadResolver scoring a `Compatible` Int→Double conversion that ties. Plan should ship a Fact pinning both calls. |
| A2 | DryWetMidi `ControlChangeEvent(SevenBitNumber, SevenBitNumber)` is the correct ctor in v8.0.3 | Code Examples DX-14 | LOW — `[CITED: melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.Core.ControlChangeEvent]` confirms ctor shape. Compile error at task-start surfaces any drift. |
| A3 | OLA windowing is OPTIONAL polish; pure linear-interp varispeed satisfies the acceptance criterion | Pattern 4, DX-15 | LOW for tonal content (sine, kick samples). MEDIUM for rich polyphonic samples — quality artifacts may surface. Per Claude's discretion, sinc/OLA quality is deferred to v1.4. |
| A4 | `note.PortamentoMs * 127.0 / 200.0` linear ms→CC5 mapping is acceptable | Code Examples DX-14, CONTEXT D-15 (claude's discretion) | LOW — CONTEXT explicitly recommends this curve. Document in code so users can predict CC5 value. |
| A5 | Quantize at strength=0 acts as identity (preserves Phase 18 byte-identical regression) | Pitfall 9 | HIGH if violated — every tutorial.flow render diverges. Identity short-circuit MUST be the first line of `QuantizeSequence`. |
| A6 | `random` arpeggio direction is deferred to v1.4 (returns "up" with a doc comment) | Pitfall 7 | LOW — direction enum naturally falls back. If user feedback demands seeded random, add `arpeggio(chord, rate, "random", pattern, seed)` as a future overload. |
| A7 | `MusicalNoteData` defaulted-parameter migration accommodates 2 new optional fields (DurationOverlap, PortamentoMs) without breaking the 30+ existing call sites | Pattern 1, Code Examples DX-14 | LOW — Phase 18 SUMMARY confirms zero call-site edits required when defaults are at end of constructor. Verified by grep of existing `new MusicalNoteData(...)` constructions, all of which use positional args at most through `sourceLength`. |
| A8 | `chord-tone` and `scale-tone` arpeggio patterns route to `linear` for v1.3 (DX-10) | Code Examples DX-10, REQUIREMENTS deferred | LOW — REQUIREMENTS Future Requirements line 105 explicitly defers richer pattern logic. Document as `"chord-tone"` / `"scale-tone"` accepted but renders as `"linear"` in v1.3. |

## Open Questions (RESOLVED)

1. **DX-13 quantize: which onset-shift mechanism?**
   - What we know: `BarData.MusicalNotes` has no `OnsetOffset` field; `bar.ToTimeline()` derives onsets sequentially.
   - What's unclear: Add a defaulted-parameter `OnsetOffset` to `MusicalNoteData` (cleanest) vs. mutate prior-note durations (no migration but harder for `strength < 1.0`).
   - Recommendation: Use the defaulted-parameter pattern — same Phase 18 migration shape that's already proven. Plan should list this explicitly as a decision in the discuss-phase step.
   - **RESOLVED**: Plan 22-05 ships defaulted-parameter `OnsetOffset` field on `MusicalNoteData` (appended after `durationFraction` per Phase 18 precedent). `bar.ToTimeline()` adds `OnsetOffset` to emitted onset positions without advancing `currentBeat`. Plan 22-05 also ships a `MusicalNoteData.With(...)` builder helper so 22-06's transforms compose without enumerating fields they don't own (rollback-independence per CONTEXT line 18).

2. **DX-12 user-facing call style — `EIGHTH` constant or accept `e` token?**
   - What we know: stdlib has `NoteValue EIGHTH = 3` (notation.flow:31). The `e` token is special inside note streams only.
   - What's unclear: Whether to add lowercase aliases (`NoteValue e = 3` etc.) to broaden ergonomics, or document the constant convention.
   - Recommendation: Document the constant convention. Adding lowercase single-char identifiers globally pollutes the namespace and conflicts with future user-defined identifiers.
   - **RESOLVED**: Plan 22-04 documents the `EIGHTH`/`QUARTER` constant convention via Pitfall 10. Plan 22-04 Test 9 (`BareIntegerArg_DispatchesToDoubleOverload_Documented`) pins OverloadResolver behavior for bare-integer args. No lowercase aliases added.

3. **DX-11 default base octave for inversion**
   - What we know: `ChordParser` defaults to octave 4 for chord literals.
   - What's unclear: When a user calls `inversion(Cmaj, 5)` with n > note-count, do we keep raising octaves indefinitely (well past MIDI range) or clamp to MIDI_MAX (E10)?
   - Recommendation: Charitable interpretation D-07 — return input chord unchanged if n >= NoteNames.Length. This is consistent with the "voicing on incomplete chords" decision.
   - **RESOLVED**: Plan 22-03 implements charitable D-07 via guard `if (n <= 0 || n >= input.NoteNames.Length) return input;` in `Voicings.Inversion`. Plan 22-03 Test 4 (`Inversion_NGreaterEqualNoteCount_ReturnsUnchanged`) and Test 5 (`Inversion_NegativeN_ReturnsUnchanged`) pin both edges.

4. **DX-14 portamento + legato interaction**
   - What we know: Both fields are independent on `MusicalNoteData`. Both are MIDI-export-time concerns; legato also affects audio renderer.
   - What's unclear: When a user chains `legato(portamento(seq, 100ms), 0.3)`, both flags should coexist on each note. Confirm by Fact.
   - Recommendation: Plan ships a chained-transform Fact pinning both flags survive transform composition.
   - **RESOLVED**: Plan 22-06 ships `LegatoTransform` and `PortamentoTransform` that use the `MusicalNoteData.With(...)` builder helper introduced in plan 22-05. Each transform sets ONLY its own field; pre-existing fields are preserved automatically by the builder. Plan 22-06 Test 12 (`Portamento_AndLegato_Compose`) pins composition.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All builds + tests | ✓ | 10.0 (verified per existing CI) | — |
| `dotnet` CLI | Build/test commands | ✓ | shipped with SDK | — |
| Melanchall.DryWetMidi 8.0.3 | DX-14 portamento CC events | ✓ | locked in csproj | — |
| PulseAudio (P/Invoke) | DX-12 audio playback (smoke only) | ✓ on Linux dev machine | system | DX-12 acceptance can use `writeWav` instead of `play` — no live playback dependency for the smoke |
| sample WAV file (e.g., `kick.wav`) | DX-15 acceptance smoke | ✗ | — | Smoke test generates synthetic WAV via `(sine 440.0 ...)` + `writeWav`, then loadWavs it back at +12 semitones. Avoids committing binary fixtures. |

**Missing dependencies with no fallback:** None — all critical infrastructure is already on the dev machine and in CI.

**Missing dependencies with fallback:** sample WAV file — synthesize-then-roundtrip pattern keeps the test deterministic and zero-binary-cost, mirroring the existing `tests/test_output_roundtrip.wav` workflow.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 (`flow-lang.Tests/flow-lang.Tests.csproj` L13) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate config) |
| Quick run command | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase22"` |
| Full suite command | `dotnet test flow-sharp.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DX-10 | `arpeggio(Cmaj7, q, "up", "linear")` produces 4-note ascending arpeggio at quarter rate | unit | `dotnet test --filter "ArpeggioFacts"` | ❌ Wave 0 |
| DX-10 | Direction (down/updown/downup) reorders notes correctly | unit | `dotnet test --filter "ArpeggioFacts.DirectionDownReversesNotes"` | ❌ Wave 0 |
| DX-10 | End-to-end .flow smoke (writeWav success) | integration | `dotnet run --project flow-interpreter tests/test_dx_arpeggio.flow` | ❌ Wave 0 |
| DX-11 | `inversion(Cmaj, 1)` returns `[E4, G4, C5]` | unit | `dotnet test --filter "VoicingFacts.FirstInversion_RaisesLowestNoteOctave"` | ❌ Wave 0 |
| DX-11 | `voicing(Cmaj7, "drop2")` lowers 2nd-from-top by octave | unit | `dotnet test --filter "VoicingFacts.Drop2_LowersSecondFromTop"` | ❌ Wave 0 |
| DX-11 | `voicing(Cmaj, "drop2")` returns input unchanged (3 notes < 4) | unit | `dotnet test --filter "VoicingFacts.Drop2_OnTriad_ReturnsUnchanged"` | ❌ Wave 0 |
| DX-12 | `delay(buf, EIGHTH, 0.5, 0.4)` at tempo 120 = 250ms delay | unit | `dotnet test --filter "DelaySyncFacts.NoteValueToMs"` | ❌ Wave 0 |
| DX-12 | Existing `delay(buf, 250.0, 0.5, 0.4)` still works (regression) | unit | `dotnet test --filter "DelaySyncFacts.Existing_MsRateOverload_Unchanged"` | ❌ Wave 0 |
| DX-13 | Pre-humanized euclidean snaps cleanly to 1/16 grid at strength=1 | unit | `dotnet test --filter "QuantizeFacts.Strength1_HardSnaps"` | ❌ Wave 0 |
| DX-13 | strength=0 returns input unchanged (byte-identical regression gate) | unit | `dotnet test --filter "QuantizeFacts.Strength0_IsIdentity"` | ❌ Wave 0 |
| DX-13 | Swing parameter -0.5 vs +0.5 produces equal-magnitude opposite-direction shifts | unit | `dotnet test --filter "QuantizeFacts.Swing_SignSymmetric"` | ❌ Wave 0 |
| DX-14 | `legato(seq, 0.5)` extends durations by 1.5× (each note's audio buffer 1.5× longer) | unit | `dotnet test --filter "LegatoFacts.OverlapHalf_Extends15x"` | ❌ Wave 0 |
| DX-14 | Legato preserves onset positions (next-note onset unchanged) | unit | `dotnet test --filter "LegatoFacts.OnsetsUnchanged"` | ❌ Wave 0 |
| DX-14 | `portamento(seq, 100ms)` MIDI export contains CC65=127 + CC5≈64 events | unit (DryWetMidi read-back) | `dotnet test --filter "PortamentoMidiFacts.WriteMidi_ContainsCC65AndCC5"` | ❌ Wave 0 |
| DX-15 | `loadWav("synth_440Hz.wav", 12)` returns buffer with frames ≈ source/2 (±1) | unit | `dotnet test --filter "LoadWavVarispeedFacts.TwelveSemitones_HalvesFrames"` | ❌ Wave 0 |
| DX-15 | `loadWav("synth.wav", 1.5)` ratio overload returns buffer with frames = source/1.5 | unit | `dotnet test --filter "LoadWavVarispeedFacts.RatioOverload_RescalesFrames"` | ❌ Wave 0 |
| DX-15 | `loadWav("synth.wav")` 1-arg form is byte-identical to pre-Phase-22 (regression) | unit | `dotnet test --filter "LoadWavVarispeedFacts.SingleArgUnchanged"` | ❌ Wave 0 |
| ALL | Phase 18 byte-identical regression gate (tutorial + showcase, WAV + MIDI) | integration | `dotnet test --filter "ByteIdentical"` | ✓ existing |

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase22"` (Phase 22 Facts only — fast, ~5s)
- **Per wave merge:** `dotnet test flow-sharp.sln` (full suite — ~30s)
- **Phase gate:** Full suite green AND `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` 19/19 GREEN before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase22/` directory — new (mirror of Phase21/)
- [ ] `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs` — covers DX-10
- [ ] `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs` — covers DX-11
- [ ] `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs` — covers DX-12
- [ ] `flow-lang.Tests/Unit/Phase22/QuantizeFacts.cs` — covers DX-13
- [ ] `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs` — covers DX-14 legato
- [ ] `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs` — covers DX-14 portamento (uses MidiReadHelpers from Phase 15 DEFER-05)
- [ ] `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs` — covers DX-15
- [ ] `tests/test_dx_arpeggio.flow` — DX-10 smoke
- [ ] `tests/test_dx_voicings.flow` — DX-11 smoke
- [ ] `tests/test_dx_delay_sync.flow` — DX-12 smoke
- [ ] `tests/test_dx_quantize.flow` — DX-13 smoke
- [ ] `tests/test_dx_legato.flow` — DX-14 legato smoke
- [ ] `tests/test_dx_portamento.flow` — DX-14 portamento smoke (writeMidi assertion)
- [ ] `tests/test_dx_loadwav_varispeed.flow` — DX-15 smoke
- [ ] `FlowScriptData.RequiredSentinels` entries for all new tests/test_dx_*.flow scripts

*(No framework install needed — xUnit + DryWetMidi already locked.)*

## Security Domain

The `security_enforcement` config flag is unset (= enabled by default). Phase 22 is a stdlib feature phase with no auth/session/network surface. Applicable controls:

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — single-user CLI tool |
| V3 Session Management | no | N/A — no sessions |
| V4 Access Control | no | N/A — no auth |
| V5 Input Validation | yes | Validate user-provided file paths (DX-15 `loadWav`); validate numeric ranges (DX-13 strength/swing clamping; DX-12 NoteValue enum range; DX-15 ratio > 0). Existing `FileIO.LoadWavInternal` already validates `File.Exists` and RIFF format `[VERIFIED: FileIO.cs:304-315]`. New code should follow the existing exception-throwing pattern for invalid inputs. |
| V6 Cryptography | no | N/A — no crypto. Phase 15 DX-09 PRNG isolation pattern (LOCAL `new Random(seed)`) applies if DX-10 supports `random` direction (Pitfall 7) — but recommendation is to defer. |

### Known Threat Patterns for music-DSL feature additions

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Untrusted file path in `loadWav` enabling arbitrary read | Information Disclosure | `File.Exists` + `BinaryReader` (existing pattern). User scripts run with user privileges; out of scope to sandbox arbitrary path reads — same threat surface as the existing 1-arg `loadWav`. |
| Out-of-bounds integer / NaN in resample ratio | Denial of Service | DX-15 explicit `ratio <= 0.0` check throws `ArgumentException`. NaN check follows existing `ClampSample` pattern at FileIO.cs:249-258. |
| Unbounded sequence growth in quantize / legato | Denial of Service | Operations are O(N) over input note count; no expansion. Existing iteration-guard infrastructure (`setMaxIterations`) bounds runtime if needed. |
| MIDI CC value overflow | Tampering | `Math.Clamp` to [0, 127] before SevenBitNumber cast (CONTEXT D-15 mapping curve already documented). |
| Static `Random` cross-test contamination | Repudiation (byte-identity contract) | LOCAL `new Random(seed)` per call (Phase 15 DX-09 precedent). DX-10 random arpeggio (if shipped) MUST follow this. |

## Project Constraints (from CLAUDE.md)

| Directive | Phase 22 Implication |
|-----------|----------------------|
| .NET 10, file-scoped namespaces, nullable enabled | All new C# files follow existing convention. |
| Minimal dependencies | NO new NuGet packages. DryWetMidi 8.0.3 already linked. |
| AST nodes are `record` types | Phase 22 adds NO new AST nodes — pure stdlib + transforms. |
| Existing .flow scripts must continue to work | Hard requirement — every new function adds an OVERLOAD; existing signatures stay byte-identical. |
| Functional S-expression style, no infix operators (auto-memory) | All acceptance examples + smoke tests use `(arpeggio Cmaj7 q "up" "linear")` form. No infix introduced. |
| Charitable interpretation, music > rigid correctness (auto-memory) | DX-11 voicing-on-incomplete-chord (CONTEXT D-07) is the canonical application. DX-13 quantize identity-at-strength-0 is the byte-identity application. |
| GSD workflow enforcement (CLAUDE.md) | Plans land via `/gsd-execute-phase 22` after `/gsd-plan-phase 22`. |
| Ad-hoc edits forbidden | All Phase 22 changes flow through plans. |

## Sources

### Primary (HIGH confidence)
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` lines 1-425 — existing arpeggio + chord built-ins; DX-10 extends in place; DX-11 builds new voicing functions in same tier
- `flow-lang/StandardLibrary/Harmony/ChordParser.cs` lines 1-198 — chord literal parsing; `ChordData.NoteNames` format and octave handling for DX-11
- `flow-lang/StandardLibrary/Audio/DSP/Delay.cs` lines 1-96 — existing ms-rate delay; DX-12 adds NoteValue overload alongside, delegates here
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` lines 191-216 — DelayEffect registration; mirror pattern for DX-12 NoteValue overload
- `flow-lang/StandardLibrary/Audio/FileIO.cs` lines 285-465 — existing loadWav + Resample (linear interpolation); DX-15 reuses both
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` lines 1-820 — TransformNotes + Augment/Diminish patterns; DX-13/14 follow this shape
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` lines 1-330 — DryWetMidi MIDI export; DX-14 portamento extends note loop at line 245-275
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` lines 121-225 — additive voice mixer; DX-14 legato D-02 confirmed via direct read
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` lines 23-100 — per-note Voice creation; DX-14 legato extends durationBeats post-onset (mirror of IsTied at line 67-72)
- `flow-lang/Runtime/MusicalContext.cs` lines 1-110 — Tempo + TimeSignature accessors; DX-12 + DX-13 read here
- `flow-lang/TypeSystem/Fraction.cs` lines 1-57 — Phase 18 Fraction helper; DX-13 grid math reuses
- `flow-lang/TypeSystem/SpecialTypes/NoteValueType.cs` lines 1-106 — NoteValue enum + IsCompatibleWith(IntType); Pitfall 1 source
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` lines 175-192 — NoteType.Format canonical output (`+`/`-` accidental form); Pitfall 5 source
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` lines 211-306 — MusicalNoteData defaulted-parameter migration template; DX-14 adds 2 fields
- `flow-lang/TypeSystem/SpecialTypes/SequenceType.cs`, `BarType.cs` — SequenceData/BarData onset model; Pitfall 2 source
- `flow-lang/TypeSystem/OverloadResolver.cs` lines 1-84 — specificity scoring; Pitfall 1 dispatch math
- `flow-lang/TypeSystem/FunctionSignature.cs` lines 113-149 — CalculateSpecificity (1000=exact / 500=compatible / 100=convertible)
- `flow-lang/Runtime/Value.cs` lines 1-120 — Value factory + ConvertTo (Int → NoteValue at line 96)
- `flow-lang/Runtime/NoteStreamCompiler.cs` lines 289-294 — canonical NoteValue→Fraction (quarter-note units) mapping; reused by DX-12 + DX-13
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` lines 1-137 — registration mechanics; Phase 22 adds via existing Register methods
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` lines 1115-1214 — euclidean swing/humanize overload pattern (template for DX-12 RegisterContextDependent)
- `flow-lang.Tests/FlowScriptData.cs` lines 60-275 — RequiredSentinels pattern; Phase 22 smoke scripts add entries here
- `flow-lang.Tests/Unit/Phase18/FractionTests.cs` — xUnit Facts template
- `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — recent xUnit Facts pattern
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` — byte-identical regression gate (must stay 19/19 GREEN)
- `.planning/phases/22-tier-b-c-composer-dx-bundle/22-CONTEXT.md` — D-01..D-08 locked decisions
- `.planning/REQUIREMENTS.md` lines 64-69 — DX-10..DX-15 acceptance criteria
- `.planning/ROADMAP.md` lines 138-149 — Phase 22 success criteria
- `.planning/STATE.md` lines 222-237 — Phase 18-21 SUMMARY anchors for migration patterns

### Secondary (MEDIUM confidence — cited)
- [DryWetMidi ControlChangeEvent API](https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.Core.ControlChangeEvent.html) — confirms `ControlChangeEvent(SevenBitNumber, SevenBitNumber)` ctor for DX-14 portamento CC emission

### Tertiary (LOW confidence — needs validation)
- [JUCE simplest pitch shift forum](https://forum.juce.com/t/simple-st-pitch-shift-with-interpolator/51662) — referenced for varispeed approach; codebase's existing `Resample` linear interpolator is the actual reference implementation
- [Pitch shifting via sample-rate conversion (Moeller Studios)](https://www.moellerstudios.org/pitch-shifting-via-sample-rate-conversion/) — varispeed conceptual reference
- [MIDI CC list (anotherproducer.com)](https://anotherproducer.com/online-tools-for-musicians/midi-cc-list/) — confirms CC65 = portamento on/off, CC5 = portamento time MSB; matches REQUIREMENTS DX-14 cite to Sweetwater MIDI spec

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every dependency verified by csproj read
- Architecture / integration points: HIGH — every reusable asset and integration point verified by direct codebase read
- Pitfalls: HIGH — Pitfalls 1, 3, 4, 5, 6, 9, 10 verified by reading the actual source. Pitfall 7 has Phase 15 precedent. Pitfall 8 is mathematical.
- Validation Architecture: HIGH — test framework + sentinel pattern follow Phase 18-21 template exactly
- DX-15 OLA quality: MEDIUM — the linear-interp baseline is HIGH confidence but the OLA windowing is unverified in the codebase. Recommendation: ship linear-only and defer windowed OLA to v1.4 (Claude's discretion)
- DX-14 portamento DryWetMidi API: MEDIUM-HIGH — ControlChangeEvent ctor verified by official docs but the per-note bracket pattern (CC65=127 → NoteOn → NoteOff → CC65=0) is conventional; downstream synth behavior depends on receiver implementation

**Research date:** 2026-05-01
**Valid until:** 2026-05-31 (30-day window — codebase is stable; only DryWetMidi minor versions could shift API surface)
