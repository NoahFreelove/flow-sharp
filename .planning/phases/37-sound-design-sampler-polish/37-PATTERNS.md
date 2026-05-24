# Phase 37: Sound Design + Sampler Polish - Pattern Map

**Mapped:** 2026-05-22
**Files analyzed:** 47 new/modified files (15 DSP C#, 6 SFZ retrofit C#, 4 sample-renderer C#, 11 builtin-registration / stdlib `.flow`, 2 example `.flow`, 22 test C# fixtures, 6 closer markdown)
**Analogs found:** 44 / 47 (3 with no direct analog — PhaseVocoder.cs, Psola.cs, Fft.cs; closest is composite)

## File Classification

### New / Modified C# — DSP cores (Plan 37-01)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/DSP/WindowFunctions.cs` | DSP utility (static class) | params → float[] window | `flow-lang/StandardLibrary/Audio/DSP/Filter.cs` | exact |
| `flow-lang/StandardLibrary/Audio/DSP/Fft.cs` | DSP utility (static class) | float[] → Complex[] | composite — closest is `Filter.cs` (static math helper) + Phase29Fft test helper at `flow-lang.Tests/Helpers/Phase29Fft.cs` | composite |
| `flow-lang/StandardLibrary/Audio/DSP/Hps.cs` | DSP transient detector | spectrogram → per-frame mode pick | `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs` (envelope follower per-frame loop) | role-match |
| `flow-lang/StandardLibrary/Audio/DSP/GranularEngine.cs` | DSP processor + PRNG consumer | Buffer → Buffer (with jitter draws) | `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (Apply method shape) + PrngRegistry routing from `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` | composite |
| `flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs` | builtin registration | Value → Value | `flow-lang/StandardLibrary/Audio/PanningFunctions.cs` (single-builtin registration class) | exact |

### New / Modified C# — Stretch / PitchShift (Plan 37-02)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/DSP/PhaseVocoder.cs` | DSP engine | Buffer + factor → Buffer (STFT pipeline) | `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (Apply + ProcessChannel split) — no exact analog for STFT loop | role-match |
| `flow-lang/StandardLibrary/Audio/DSP/Psola.cs` | DSP engine | Buffer + factor → Buffer (epoch OLA) | `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (CombFilter shape + buffer ring) | role-match |
| `flow-lang/StandardLibrary/Audio/DSP/StretchEngine.cs` | DSP dispatcher | mode-enum → call PhaseVocoder OR Psola OR Hps-decision | `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (`hasVelocityLayers` branch dispatch) + `flow-lang/StandardLibrary/Audio/DSP/Filter.cs` (mode-by-method dispatch) | composite |
| `flow-lang/StandardLibrary/Audio/DSP/PitchShiftEngine.cs` | DSP dispatcher | reuses StretchEngine with inverse remap | self (StretchEngine, sibling) | self |
| `flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs` | builtin registration (Buffer, Double, [named args]) | Value → Value | `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` (multi-overload Register pattern, lines 32-102 for reverb) | exact |
| `flow-lang/StandardLibrary/Audio/DSP/PitchShiftFunctions.cs` | builtin registration (Buffer, Cent\|Semitone, [named args]) | Value → Value | `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` + `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (Cent/Semitone overloads) | exact |

### Modified C# — SFZ retrofit (Plan 37-03)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs` (MODIFY) | record extension — 6 new fields | data | itself (Phase 33 13-field positional record) | self |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (MODIFY) | parser opcode whitelist + ReadInt calls | string → record | itself (existing 14-opcode parse + BuildRegion, lines 79-95 + 440-510) | self |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` (MODIFY) | renderer — round-robin counter + xfin/xfout xfade + SAMP-03 multiplier + MIX-02 per-voice pan wire-up | render-time logic | itself (existing Render pipeline + `ToStereoBufferWithPan` helper, lines 94-209 + 397-423) | self |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (MODIFY — MIX-02 SFZ pan handoff) | mix-stage wiring | Voice → stereo additive mix | itself (existing per-voice pan loop, lines 297-338) | self |

### Modified C# — Sample assets / renderer (Plans 37-04, 37-05, 37-06)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/SampleCache.cs` (MODIFY — piano mp/mf manifest, flute middle-point manifest) | manifest extension | data | itself (Phase 29 `InstrumentManifest` dict, lines 47-58 + EagerLoad walk lines 71-120) | self |
| `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (MODIFY — 4-way velocity xfade + `release=` knob + SAMP-03 multiplier overlay) | renderer extension | render-time logic | itself (existing 2-way crossfade + 500ms tail, lines 73-155 + `LoudnessNormalizedCrossfade` lines 187-218) | self |
| `flow-lang/Samples/piano/{C2..C6}_{mp,mf}.wav` (NEW — 5 pitches × 2 layers = 10 new WAVs) | data asset (sample) | n/a | `flow-lang/Samples/piano/C4_pp.wav` (existing pp/ff layout) | exact (same filename convention) |
| `flow-lang/Samples/flute/{A4 or D5}.wav` (NEW — 1 sample) | data asset (sample) | n/a | `flow-lang/Samples/flute/G4.wav` (existing single-velocity layout) | exact |
| `flow-lang/Samples/CREDITS.md` + per-instrument `LICENSE.md` (MODIFY) | attribution | data | itself (Phase 29 attribution shape) | self |

### Modified `.flow` stdlib modules

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/audio.flow` (MODIFY — add granular/stretch/pitchShift `internal proc` forward decls) | stdlib module | declaration only | itself (existing `internal proc createSineTone` block lines 222-225 — Hertz/Double overloads) | self |
| `flow-lang/sfz.flow` (MODIFY — add `#drums "GM-StylePerc.sfz"` dict entry, ~line 60) | data dict | composer-facing dict | itself (19-entry GM dict at lines 38-62) | self |
| `flow-lang/std.flow` (MODIFY — add Tuning/Sfz-style `str` overload for any new exposed value types — likely NOT NEEDED for Phase 37 since no new types) | n/a | n/a | itself (str overload pattern lines 20-34) | self (only if needed) |

### Example `.flow` files (Plan 37-07 closer)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `examples/dsp/granular.flow` | tutorial chapter | `loadWav` → granular → `writeWav` | `examples/scala/intro.flow` (chapter shape with banner + numbered sections) + `examples/symphony/sfz_smoke.flow` (audio render path) | composite |
| `examples/dsp/stretch_pitchshift.flow` | tutorial chapter | `loadWav` → stretch → pitchShift → `writeWav`; shows `#vocoder` / `#psola` / `#auto` | same as granular.flow | composite |

### Closer markdown (Plan 37-07)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `.planning/phases/37-sound-design-sampler-polish/37-VERIFICATION.md` | phase verification report | derived from observable truths | `.planning/phases/36-sequence-algebra-generative/36-VERIFICATION.md` | exact |
| `.planning/phases/37-sound-design-sampler-polish/37-HUMAN-UAT.md` | composer subjective UAT log | composer signoff document | `.planning/phases/33-sfz-orchestral-sampler/` UAT docs (referenced via CONTEXT D-37-12 precedent) — fall back to 36-VERIFICATION shape if not present | role-match |
| `.planning/ROADMAP.md` (MODIFY — Phase 37 row flip) | roadmap state | one-row edit | itself (Phase 36 row flipped to Complete by Plan 36-12) | self |
| `.planning/STATE.md` (MODIFY) | progress state | frontmatter edit | itself | self |
| `.planning/REQUIREMENTS.md` (MODIFY — mark DSP-01..03, MIX-01..02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01 shipped) | requirement state | row-flip per REQ | itself (Phase 36 marks 9 REQs shipped) | self |
| `CLAUDE.md` (MODIFY — Phase 37 section under Language Features) | conventions/docs | append section | itself (Phase 32/33/36 sections appended in chronological order) | self |

### Wave 0 test fixtures (xUnit, under `flow-lang.Tests/Integration/Phase37/`)

All 22 test files (enumerated in RESEARCH.md §Validation Architecture Wave 0 Gaps) follow the same `[Collection("FlowScripts")] public class … : IDisposable` shape as `Phase33SfzSmokeTests`.

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `GranularSynthesisTests.cs`, `WindowFunctionTests.cs`, `GranularDeterminismTests.cs` | unit + integration | direct C# call OR Flow source → eval → assert | `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` | exact |
| `StretchVocoderTests.cs`, `StretchPsolaTransientTests.cs`, `StretchAutoAdvisoryTests.cs`, `StretchIdentityTests.cs`, `PitchShiftTests.cs` | integration | Buffer pipeline | `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` + Phase 29 `Phase29ByteIdenticalTests.cs` (RMS regression shape) | composite |
| `SfzRoundRobinTests.cs`, `SfzRoundRobinDeterminismTests.cs`, `SfzVelocityCrossfadeTests.cs`, `SfzHardSwitchRegression.cs`, `SfzPanRetrofitTests.cs`, `SfzPanCompositionTests.cs` | integration | SFZ patch fixture → render → assert | `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` + `SfzArticulationTests.cs` | exact |
| `SampledStaccatoEnergyTests.cs`, `PianoSampleCacheLayersTest.cs`, `PianoReleaseKnobTests.cs`, `FluteSampleCacheTests.cs`, `FluteD5CrossoverTests.cs` | unit + integration | render → energy/RMS check | `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` + `VelocityLayerTests.cs` | exact |
| `SfzDrumsLoadTest.cs`, `DrumPitchShiftAutoTests.cs` | integration | (loadSfz #drums) → assert + render | `flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs` + `SfzSmokeTests.cs` | exact |
| `Phase37MixSynthPathRegression.cs`, `Phase37RmsRegression.cs` | regression | render → AssertWavMatchesBaseline | `flow-lang.Tests/Helpers/RmsRegressionTests.cs` (helper) used by Phase 28 baselines under `flow-lang.Tests/baselines/Phase28/` | exact |
| `flow-lang.Tests/baselines/Phase37/` (NEW dir) + `*.wav` baselines | data | n/a | `flow-lang.Tests/baselines/Phase28/{maple_leaf_opening,ragtime_polyphony,staccato_baseline}.wav` | exact |
| `flow-lang.Tests/fixtures/Phase37/` (NEW dir — synthetic sines, drum hits, mixed material) | data | n/a | `flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz` shape | role-match |

## Pattern Assignments

### `flow-lang/StandardLibrary/Audio/DSP/WindowFunctions.cs` (DSP utility, static class)

**Analog:** `flow-lang/StandardLibrary/Audio/DSP/Filter.cs`

**Class shape pattern** (Filter.cs:1-8):
```csharp
namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Biquad filter implementation supporting lowpass, highpass, and bandpass modes.
/// All processing returns new buffers — inputs are never modified.
/// </summary>
public static class Filter
{
```
**Follow this pattern when:** writing the new `WindowFunctions` static class. One public Build/Apply method per window kind (Hann, Gaussian, Tukey), each returning `float[]` (NOT AudioBuffer — these are reusable inside other DSP). Mirror the `// === Section === / private helper` layout shown below.

**Validation pattern** (Filter.cs:53-87):
```csharp
public static AudioBuffer Bandpass(AudioBuffer input, float lowHz, float highHz)
{
    if (lowHz <= 0f)
        throw new ArgumentException("Lower cutoff frequency must be positive.");
    if (highHz <= lowHz)
        throw new ArgumentException("Upper cutoff frequency must be greater than lower cutoff.");
    // ...
}

private static void ValidateCutoff(float cutoffHz, int sampleRate) { /* throws ArgumentException */ }
```
**Follow this pattern when:** WindowFunctions validates `length > 0`, `sigma > 0` (Gaussian), `alpha ∈ [0,1]` (Tukey). Throw `ArgumentException` with a clear message — matches the Filter.cs convention.

**Pure-function pattern** (Filter.cs:90-106):
```csharp
private static void ComputeLowpassCoefficients(float cutoff, float q, int sampleRate,
    out float b0, out float b1, out float b2, out float a1, out float a2)
{
    double w0 = 2.0 * Math.PI * cutoff / sampleRate;
    double cosW0 = Math.Cos(w0);
    // ... closed-form math, no state
}
```
**Follow this pattern when:** writing window-function closed-form math from RESEARCH §Pattern 4. Each window is one `for (int n = 0; n < N; n++) result[n] = ...` loop — no state, no side effects.

---

### `flow-lang/StandardLibrary/Audio/DSP/GranularEngine.cs` (DSP processor + PRNG consumer)

**Analog:** composite — `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (shape) + `flow-lang/Runtime/PrngRegistry.cs` (PRNG routing)

**`Apply` method skeleton** (Reverb.cs:26-58):
```csharp
public static AudioBuffer Apply(AudioBuffer input, float roomSize, float damping, float mix)
{
    roomSize = Math.Clamp(roomSize, 0f, 1f);
    // ...
    var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);
    double rateScale = input.SampleRate / 44100.0;
    // ...
    for (int ch = 0; ch < input.Channels; ch++)
    {
        var dry = ExtractChannel(input, ch);
        var wet = ProcessChannel(dry, feedback, damping, rateScale);
        for (int frame = 0; frame < input.Frames; frame++) { /* mix into result */ }
    }
    return result;
}
```
**Follow this pattern when:** writing `GranularEngine.Apply(AudioBuffer input, double grainSeconds, double densityHz, double jitter, WindowKind window, PrngRegistry prng, SourceLocation site)`. Always allocate a fresh `AudioBuffer` for the result (input is never mutated); process per-channel; use `input.SampleRate` for any time-to-frames conversion.

**Helper-method extraction** (Reverb.cs:115-145):
```csharp
private static float[] ProcessChannel(float[] input, float feedback, float damping, double rateScale)
{
    int length = input.Length;
    var combOutputs = new float[4][];
    for (int i = 0; i < 4; i++) { /* ... */ }
    // ...
}
```
**Follow this pattern when:** breaking GranularEngine into `ScheduleGrainOnsets(...)` (returns `int[]` onset frames) + `ExtractAndWindowGrain(...)` (returns `float[]` window-multiplied grain) + `OverlapAddGrain(target, grain, onsetFrame)` (void, mutates target).

**PRNG draw pattern** (from RESEARCH §Pattern 4 + PrngRegistry.cs:108):
```csharp
double offsetJitter = prng.NextDouble(site, "granular_offset") * 2.0 - 1.0;  // [-1, +1]
double timeJitter   = prng.NextDouble(site, "granular_timing") * 2.0 - 1.0;
```
**Follow this pattern when:** any stochastic draw in granular. Use DISTINCT generator names per use site to avoid Pitfall 8 (PRNG collision); the names go in the registry as part of the key.

---

### `flow-lang/StandardLibrary/Audio/DSP/GranularFunctions.cs` (builtin registration)

**Analog:** `flow-lang/StandardLibrary/Audio/PanningFunctions.cs`

**Single-class single-builtin registration** (PanningFunctions.cs:13-43):
```csharp
public static class PanningFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        var panSig = new FunctionSignature("pan",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "pan"]);
        registry.Register("pan", panSig, PanEffect);
    }

    private static Value PanEffect(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var panValue = (float)args[1].As<double>();
        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, 2, buffer.SampleRate));
        var result = Panner.Apply(buffer, panValue);
        return Value.Buffer(result);
    }
}
```
**Follow this pattern when:** writing `GranularFunctions.Register(registry)`. The Phase 36 named-arg surface means the FunctionSignature carries `ParameterNames: ["buf", "grain", "density", "jitter", "windowing"]` and callers pass either positional or named. Multiple overloads (positional defaults + music-type variants like `Millisecond` for `grain`, `Hertz` for `density`) follow the `EffectsFunctions.cs` multi-overload pattern below.

---

### `flow-lang/StandardLibrary/Audio/DSP/StretchFunctions.cs` (builtin registration, multi-overload)

**Analog:** `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` (reverb section, lines 30-102)

**Multi-overload + music-typed registration** (EffectsFunctions.cs:32-101):
```csharp
private static void RegisterReverb(InternalFunctionRegistry registry)
{
    // reverb(Buffer, Double) -> Buffer — room size only, default damping=0.5, mix=0.3
    var reverbSimpleSig = new FunctionSignature("reverb",
        [BufferType.Instance, DoubleType.Instance],
        ParameterNames: ["buf", "room"]);
    registry.Register("reverb", reverbSimpleSig, ReverbSimple);

    // reverb(Buffer, Double, Double, Double) -> Buffer
    var reverbFullSig = new FunctionSignature("reverb",
        [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance],
        ParameterNames: ["buf", "room", "damping", "mix"]);
    registry.Register("reverb", reverbFullSig, ReverbFull);

    // Phase 26.2 ERG-02: reverb(Buffer, Double, Second) — decay time as Second.
    var reverbSecondSig = new FunctionSignature("reverb",
        [BufferType.Instance, DoubleType.Instance, SecondType.Instance],
        ParameterNames: ["buf", "room", "decay"]);
    registry.Register("reverb", reverbSecondSig, args => { /* lambda body */ });
}
```
**Follow this pattern when:** registering `stretch` overloads:
- `stretch(Buffer, Double)` — positional, default `mode=#auto`
- `stretch(Buffer, Double, Symbol)` — explicit mode
- Named-args via `FunctionSignature.ParameterNames` includes `["buf", "factor", "mode", "frameSize", "hopSize", "overlap", "pitchPeriod", "windowSize", "transientThreshold"]` so any subset can be passed by name (Phase 36-02 named-arg surface).

**Lambda body pattern** (EffectsFunctions.cs:82-101):
```csharp
registry.Register("reverb", reverbSecondSig, args =>
{
    var buffer = args[0].As<AudioBuffer>();
    float roomSize = (float)args[1].As<double>();
    float decaySec = (float)args[2].As<double>();
    float damping = (float)Math.Clamp(0.7 - decaySec * 0.15, 0.1, 0.7);
    const float mix = 0.3f;

    if (buffer.Frames == 0)
        return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

    var result = Reverb.Apply(buffer, roomSize, damping, mix);
    return Value.Buffer(result);
});
```
**Follow this pattern when:** writing the lambda body for stretch — always check `buffer.Frames == 0` for the empty short-circuit; coerce music-typed args via `args[i].As<double>()` (Decibel/Millisecond/Second/Hertz/Cent all back to double per their Value factory).

**Identity fast-path pattern** (per RESEARCH Pitfall 11 + Phase 32 pragma identity short-circuit precedent):
```csharp
// stretch(buf, 1.0) — fast-path: no FFT roundoff, return input verbatim.
if (Math.Abs(factor - 1.0) < 1e-12) return Value.Buffer(buffer);
```
**Follow this pattern when:** writing both `StretchFunctions` and `PitchShiftFunctions` lambdas. Identity preserves two-run cmp-clean determinism on scripts that pass `1.0` / `0c` / `0st`.

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs` (MODIFY — extend record)

**Analog:** itself (Phase 33 record)

**Existing record shape** (SfzRegion.cs:57-70):
```csharp
public sealed record SfzRegion(
    string SamplePath,
    int PitchKeycenter,
    int LoKey,
    int HiKey,
    int LoVel,
    int HiVel,
    SfzLoopMode LoopMode,
    int LoopStart,
    int LoopEnd,
    double AmpegAttack,
    double AmpegRelease,
    double Volume,
    double Pan);
```
**Follow this pattern when:** extending the record. Append 6 new positional fields at the END (positional records preserve constructor backwards-compat for any C# call site that still uses positional args — but check parser BuildRegion line ~501-509 in `SfzParser.cs` which calls the constructor positionally and update there too):
```csharp
public sealed record SfzRegion(
    // ... existing 13 fields ...
    double Pan,
    // Phase 37 SAMP-01 round-robin (default sentinel = 1/1 = "no rotation")
    int SeqPosition,    // default 1
    int SeqLength,      // default 1
    // Phase 37 SAMP-02 velocity crossfade (default sentinel = -1 = "absent")
    int XfinLoVel,      // default -1
    int XfinHiVel,      // default -1
    int XfoutLoVel,     // default -1
    int XfoutHiVel);    // default -1
```
The xmldoc block above the record (lines 1-56) is the single source of truth for field semantics — extend it with 3 new `<item>` blocks for the round-robin pair, the xfin pair, and the xfout pair, following the existing bullet style.

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (MODIFY — extend whitelist + BuildRegion)

**Analog:** itself

**Opcode whitelist extension** (SfzParser.cs:79-95):
```csharp
private static readonly HashSet<string> KnownOpcodes = new(StringComparer.Ordinal)
{
    "sample",
    "lokey",
    "hikey",
    "pitch_keycenter",
    "lovel",
    "hivel",
    "loop_mode",
    "loop_start",
    "loop_end",
    "ampeg_attack",
    "ampeg_release",
    "volume",
    "pan",
    "default_path",
};
```
**Follow this pattern when:** adding the 6 new SAMP-01/02 opcodes — append `"seq_position"`, `"seq_length"`, `"xfin_lovel"`, `"xfin_hivel"`, `"xfout_lovel"`, `"xfout_hivel"` to the same `HashSet<string>`. Update the xmldoc comment "14 opcodes" → "20 opcodes" (lines 19-26).

**BuildRegion charitable-default pattern** (SfzParser.cs:455-465):
```csharp
int pitchKeycenter = ReadInt(region, "pitch_keycenter", 60, patchDescription);
int loKey = ReadInt(region, "lokey", 0, patchDescription);
int hiKey = ReadInt(region, "hikey", 127, patchDescription);
int loVel = ReadInt(region, "lovel", 1, patchDescription);
int hiVel = ReadInt(region, "hivel", 127, patchDescription);
int loopStart = ReadInt(region, "loop_start", 0, patchDescription);
int loopEnd = ReadInt(region, "loop_end", 0, patchDescription);
double ampegAttack = ReadDouble(region, "ampeg_attack", 0.0, patchDescription);
```
**Follow this pattern when:** parsing the 6 new opcodes. Each gets a `ReadInt(region, "seq_position", 1, patchDescription)` line with the spec default per RESEARCH §Pattern 5 / Pattern 6:
- `seq_position` default = 1
- `seq_length` default = 1 (clamp upper to 100 per Pitfall 1 security note)
- `xfin_lovel`, `xfin_hivel`, `xfout_lovel`, `xfout_hivel` default = -1 (sentinel for "absent — use hard-switch")

**WarnOnce on out-of-spec values** (SfzParser.cs:483-485):
```csharp
RenderingDiagnostics.WarnOnce(
    $"sfz:opcode_value:{patchDescription}:loop_mode:{lmStr}",
    $"[sfz] unknown loop_mode value '{lmStr}' in '{patchDescription}' — falling back to no_loop");
```
**Follow this pattern when:** clamping `seq_length > 100` (spec max). Emit `[sfz] seq_length=N exceeds spec max 100 in '{patchDescription}' — clamping to 100`.

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` (MODIFY — round-robin + xfin/xfout + SAMP-03 + MIX-02)

**Analog:** itself (Phase 33 renderer)

**Existing constant-power pan helper to REUSE** (SfzRenderer.cs:409-423):
```csharp
private static AudioBuffer ToStereoBufferWithPan(float[] mono, int sampleRate, double pan)
{
    pan = Math.Clamp(pan, -1.0, 1.0);
    double theta = (pan + 1.0) * Math.PI / 4.0;
    float wL = (float)Math.Cos(theta);
    float wR = (float)Math.Sin(theta);

    var stereo = new AudioBuffer(mono.Length, 2, sampleRate);
    for (int i = 0; i < mono.Length; i++)
    {
        stereo.Data[i * 2]     = mono[i] * wL;
        stereo.Data[i * 2 + 1] = mono[i] * wR;
    }
    return stereo;
}
```
**Follow this pattern when:** wiring MIX-02. **Do NOT** add a parallel pan helper. The MIX-02 fix is to thread `voice.Pan` through to the SongRenderer's mix stage by emitting a `Voice` value where `voice.Pan` carries the composer's per-voice pan. SongRenderer.MixVoicesToStereoBuffer (lines 297-338) ALREADY applies the constant-power split per voice; SfzRenderer just needs to honor the per-voice attribute when constructing/returning its Voice payload. Per RESEARCH §Anti-Patterns and Pitfall 12: do NOT mutate SongRenderer; the SFZ retrofit lives at the SfzRenderer → Voice handoff.

**Existing region.Pan branch to EXTEND** (SfzRenderer.cs:202-208):
```csharp
// Pan via constant-power stereo split (Pitfall 7). Center (pan == 0)
// stays mono so unaffected patches don't double their channel count.
if (region.Pan != 0.0)
{
    return ToStereoBufferWithPan(fitted, sampleRate, region.Pan);
}
return SynthUtils.ToMonoBuffer(fitted, sampleRate);
```
**Follow this pattern when:** composing per-region + per-voice pan per Open Question 4. Recommend additive-with-clamp (see Open Question 4): `double effectivePan = Math.Clamp(region.Pan + voicePan, -1.0, 1.0)`. Apply ONCE — never apply twice.

**Articulation envelope (SAMP-03 stacks ON TOP, NOT inside)** (SfzRenderer.cs:188-200):
```csharp
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack:  region.AmpegAttack  > 0 ? region.AmpegAttack  : 0.005,
    baseDecay:                                                   0.05,
    baseSustain:                                                 1.0,
    baseRelease: region.AmpegRelease > 0 ? region.AmpegRelease : 0.05,
    frames: targetFrames,
    sampleRate: sampleRate,
    isPercussion: false);
SynthUtils.ApplyEnvelope(fitted, envelope);
```
**Follow this pattern when:** adding the SAMP-03 multiplier per Pitfall 10. After `ApplyEnvelope` returns, scan a `SamplePathArticulationMultipliers[note.Articulation]` table (default: identity scalar) and overlay multiplicatively:
```csharp
// SAMP-03 sample-path articulation multiplier (Plan 37-03 / Pattern 7 Option A).
// Stacks ON TOP of Phase 28's locked envelope; Phase 28 baseline is unchanged.
var samplePathMult = SamplePathArticulationMultipliers.For(note.Articulation);
if (samplePathMult.IsNontrivial)
    for (int i = 0; i < fitted.Length; i++)
        fitted[i] *= samplePathMult.Sample(i, fitted.Length);
```
The `SamplePathArticulationMultipliers` static class is a NEW file alongside `SynthUtils.cs` — Phase 28's `SynthUtils.GenerateArticulationADSR` itself stays Phase 28 verbatim (Pitfall 10).

**Round-robin counter pattern** (NEW — derived from RESEARCH §Pattern 5):
```csharp
// Per-region-group counter; reset at renderSong/writeWav boundary alongside PrngRegistry.
// Seed from voice ordinal (deterministic; not GetHashCode).
private readonly Dictionary<(int loKey, int hiKey, int loVel, int hiVel), int> _rrCounter = new();

internal void ResetAtRenderBoundary() { _rrCounter.Clear(); }
```
The reset call lives in the same place that calls `PrngRegistry.ResetAtRenderBoundary()` — Pitfall 6. Plan 37-03 will likely route the reset through `FlowEngine.CurrentExecutionContext.PrngRegistry.ResetAtRenderBoundary()` adjacent.

---

### `flow-lang/StandardLibrary/Audio/SampleCache.cs` (MODIFY — piano/flute manifest)

**Analog:** itself

**Existing manifest** (SampleCache.cs:47-58):
```csharp
private static readonly Dictionary<string, (int[] pitches, string[] velocities)> InstrumentManifest = new()
{
    // Piano: 5 pitches × pp/ff = 10 samples
    ["piano"] = (new[] { 36, 48, 60, 72, 84 }, new[] { "pp", "ff" }),  // C2, C3, C4, C5, C6
    ["brass"] = (new[] { 57, 69, 81 }, new[] { "mf" }),                 // A3, A4, A5 (single velocity)
    // ...
    ["flute"] = (new[] { 67, 79 }, new[] { "mf" }),                     // G4, G5
    ["bell"] = (new[] { 72 }, new[] { "mf" }),                          // C5
};
```
**Follow this pattern when:**
- Piano: change `new[] { "pp", "ff" }` → `new[] { "pp", "mp", "mf", "ff" }`. Update header comment to `Piano: 5 pitches × pp/mp/mf/ff = 20 samples` per D-37-09.
- Flute: change `new[] { 67, 79 }` → `new[] { 67, 69, 79 }` (G4, A4, G5) per RESEARCH §Pattern 10 recommendation, OR `new[] { 67, 74, 79 }` (G4, D5, G5) per D-v1.5-08 hint. Plan-phase decides; CONTEXT D-37 "Claude's Discretion" defers to RESEARCH which recommends A4 for varispeed-coverage of the low register.

**The eager-load walk already iterates `manifest.velocities.OrderBy(v => v, StringComparer.Ordinal)`** (SampleCache.cs:90-92), so adding velocity labels does NOT break determinism — the ordinal sort is the existing pattern.

**Synthesized mp layer pattern** (per RESEARCH §Pattern 9 Path 1 — IMPORTANT FINDING):
The U-Iowa MIS source has NO mp piano. Plan 37-04 must synthesize mp via RMS-interpolation between pp and mf at eager-load time. Pattern:
```csharp
// In EagerLoad after both pp + mf are loaded for a pitch:
if (instrument == "piano" && !_rawCache.ContainsKey((instrument, pitch, "mp")))
{
    var pp = _rawCache[(instrument, pitch, "pp")];
    var mf = _rawCache[(instrument, pitch, "mf")];
    _rawCache[(instrument, pitch, "mp")] = RmsInterpolate(pp, mf, alpha: 0.6);
}
```
`RmsInterpolate` is a NEW helper alongside `TrimLeadingSilence` (SampleCache.cs:190-229). Same private static method style.

---

### `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (MODIFY — 4-way crossfade + release= knob)

**Analog:** itself

**Existing 2-way crossfade** (SampledInstrumentRenderer.cs:97-110):
```csharp
if (_hasVelocityLayers)
{
    // Piano path: crossfade pp + ff (REQ-3 velocity-driven timbre).
    var pp = _cache.GetVarispeed(_instrument, sampleMidi, "pp", semitonesShift);
    var ff = _cache.GetVarispeed(_instrument, sampleMidi, "ff", semitonesShift);
    if (pp is null || ff is null)
        return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
    double v = Math.Clamp(note.Velocity, 0.0, 1.0);
    mono = LoudnessNormalizedCrossfade(pp.Data, ff.Data, v);
}
```
**Follow this pattern when:** extending to 4-way (pp/mp/mf/ff). Generalize `LoudnessNormalizedCrossfade(a, b, v)` → `LoudnessNormalized4WayCrossfade(pp, mp, mf, ff, v)` that:
- Identifies the 2 adjacent layers v falls between (pp↔mp for v < 0.33, mp↔mf for 0.33 ≤ v < 0.66, mf↔ff for v ≥ 0.66 — exact split points are Claude's Discretion D-37-09).
- Calls the existing `LoudnessNormalizedCrossfade(a, b, vLocal)` between those 2 layers with `vLocal = (v - lowEdge) / (highEdge - lowEdge)`.
- Existing transition-band constants `VelocityTransitionLow = 0.4` / `VelocityTransitionHigh = 0.6` (lines 225-226) get replaced by 3 transition bands.

**Existing release tail (`release=` replaces this constant)** (SampledInstrumentRenderer.cs:79-89, 143-152):
```csharp
double tailSeconds = 0.5;
int authoredFrames = (int)(durationSeconds * sampleRate);
int targetFrames = authoredFrames + (int)(tailSeconds * sampleRate);
// ...
if (authoredFrames < fitted.Length)
{
    double tailDecayPerFrame = Math.Exp(-1.0 / (sampleRate * 0.15));
    double level = 1.0;
    for (int i = authoredFrames; i < fitted.Length; i++)
    {
        fitted[i] = (float)(fitted[i] * level);
        level *= tailDecayPerFrame;
    }
}
```
**Follow this pattern when:** wiring `release=` knob (PIANO-01, D-37-11). Plumb a new `double releaseSec = 1.5` parameter through `Render(... double releaseSec = 1.5)` (default per RESEARCH Pattern 8 + A4 assumption). Replace `tailSeconds = 0.5` with `tailSeconds = releaseSec`; replace the `0.15` time-constant with `releaseSec * 0.3` (per Pattern 8). Per Phase 36-02 named-arg surface, this is exposed at the builtin layer as `release=2.0s` etc.

---

### `flow-lang/sfz.flow` (MODIFY — add #drums entry)

**Analog:** itself

**Existing GM dict** (sfz.flow excerpt above, lines ~38-62):
```text
Dict<Symbol, String> __sfzInstruments = (dict
    ...
    Note: ----- Keys + Plucked + Percussion (3 verified) -----
    #piano        "UprightPiano.sfz"
    #harp         "Harp.sfz"
    #timpani      "Timpani.sfz"
    Note: ----- 4 TBD rows (not bundled with VSCO-CE 1.1.0) -----
    ...
)
```
**Follow this pattern when:** adding `#drums "GM-StylePerc.sfz"` per D-37-13 / RESEARCH §Pattern 11. Insert under the "Keys + Plucked + Percussion" section. Update the comment to "(4 verified)" and bump the xmldoc claim "19-entry" → "20-entry" throughout. Update Phase 33's `VSCO-PATH-AUDIT.md` to mark the 20th entry verified per RESEARCH §Pattern 11 integration step 2.

---

### `flow-lang/audio.flow` (MODIFY — add forward decls)

**Analog:** itself

**Existing forward-decl pattern with multi-overload music types** (audio.flow:222-225):
```text
Note: Generate a sine tone with frequency and amplitude
internal proc createSineTone(Double: duration, Double: freq, Double: amp)

Note: Phase 26.2 ERG-04: Hertz-typed alternative for frequency clarity
internal proc createSineTone(Double: duration, Hertz: freqHz, Double: amplitude)
```
**Follow this pattern when:** declaring `granular`, `stretch`, `pitchShift` forward decls. Each builtin gets one block of decls covering its positional + music-typed overloads. Example:
```text
Note: Phase 37 DSP-01 — granular synthesis with composable jitter PRNG
internal proc granular(Buffer: buf, Double: grainMs, Double: densityHz, Double: jitter)
internal proc granular(Buffer: buf, Millisecond: grain, Hertz: density, Double: jitter)
internal proc granular(Buffer: buf, Millisecond: grain, Hertz: density, Double: jitter, Symbol: windowing)
```
The named-arg shape (Phase 36-02) is implicit — `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)` resolves against whichever overload matches the bound args.

---

### `examples/dsp/granular.flow` + `examples/dsp/stretch_pitchshift.flow` (NEW tutorial chapters)

**Analog:** `examples/scala/intro.flow`

**Tutorial chapter banner** (scala/intro.flow:1-17):
```text
enable justIntonation;

use "@std"
use "@audio"
use "@composition"

Note: ============================================================
Note:  Chapter: Scala Microtonal Tunings (Phase 32)
Note:  Run: dotnet run --project flow-interpreter examples/scala/intro.flow
Note: ============================================================
Note:
Note:  Flow ships three named tunings out of the box: justIntonation,
Note:  pythagorean, and equalTemperament (the default). Phase 32 extends
Note:  this to ~5300 community-curated tunings via the Scala (.scl) file
Note:  format: load any of them with (loadScala "path.scl") and apply via
Note:  the tuning t { ... } musical-context block.
```
**Follow this pattern when:** writing the granular/stretch tutorial banner. Open with `use "@audio"`, banner block describing what's new in Phase 37, run command. Number sections `Note: 1. Load a buffer + describe it`, `Note: 2. Apply granular at default density`, etc. End with `writeWav` so the chapter is self-contained.

**Numbered-section walkthrough** (scala/intro.flow:23-52):
```text
Note: -----------------------------------------------------------
Note: 1. Load a tuning + describe it
Note: -----------------------------------------------------------
Note: (loadScala "path.scl") parses the .scl file at call time and returns
Note: a first-class Tuning value. (str t) prints the description + step count + period.
Tuning partch = (loadScala "flow-lang.Tests/fixtures/scala/partch_43.scl")
(print $"Loaded: {(str partch)}")
```
**Follow this pattern when:** demoing each Phase 37 builtin variant — section 1: `granular` default, section 2: with `jitter=` and `windowing=`, section 3: `stretch` `#vocoder`, section 4: `stretch` `#psola`, section 5: `stretch` `#auto` with the stderr-advisory demo.

---

### `flow-lang.Tests/Integration/Phase37/*.cs` (NEW Wave 0 test fixtures, 22 files)

**Analog:** `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs`

**Class shape + namespace + Collection attribute** (SfzSmokeTests.cs:1-72):
```csharp
using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-08 — SPEC-7 acceptance gate. End-to-end smoke test that
/// renders the Plan 33-01 synthetic fixture through the full
/// use "@sfz" → loadSfz(String) → Sfz binding → renderSong "sampler:NAME"
/// pipeline ...
/// </summary>
[Collection("FlowScripts")]
public class Phase33SfzSmokeTests : IDisposable
{
    public Phase33SfzSmokeTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        // ...
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }
}
```
**Follow this pattern when:** writing every Phase 37 test class. Namespace `FlowLang.Tests.Integration.Phase37`. `[Collection("FlowScripts")]` to serialize against the shared `RenderingDiagnostics` singleton + `FlowConfig.Active`. `IDisposable` to reset diagnostics + config in ctor + Dispose so test isolation works.

**RMS regression assertion** (use `flow-lang.Tests/Helpers/RmsRegressionTests.cs`:39-63):
```csharp
RmsRegressionTests.AssertRmsWithinTolerance(
    rendered: audioBuffer,
    baselineWavPath: "flow-lang.Tests/baselines/Phase37/{name}.wav",
    windowMs: 100.0,           // SPEC-8 default
    toleranceDb: 0.5);         // SPEC-8 default
```
**Follow this pattern when:** writing `Phase37MixSynthPathRegression.cs` (MIX-01 baseline pin) and `Phase37RmsRegression.cs` (PIANO-01 close-out baseline). Always use the SPEC-8 default tolerance (no `overrideReason` needed) unless documented otherwise.

---

### `.planning/phases/37-sound-design-sampler-polish/37-VERIFICATION.md`

**Analog:** `.planning/phases/36-sequence-algebra-generative/36-VERIFICATION.md`

**Frontmatter + Goal Achievement table** (36-VERIFICATION.md:1-30):
```text
---
phase: 36-sequence-algebra-generative
verified: 2026-05-22T20:40:00Z
status: passed
score: 9/9 requirements verified
overrides_applied: 0
re_verification:
  previous_status: null
  previous_score: null
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 36: Sequence Algebra & Generative Verification Report

**Phase Goal:** Composer can write Tidal-style pattern algebra ... and improvise chord-aware Markov solos ...

**Verified:** 2026-05-22
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (9 must-haves derived from REQUIREMENTS.md Phase 36 REQ-IDs + ROADMAP.md Success Criteria)

| # | Must-have / Truth | Status | Evidence |
| 1 | **PAT-01:** 13 Tidal-style combinators on `Sequence` ship in `@patterns` stdlib ... | ✓ VERIFIED | `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` ...  |
```
**Follow this pattern when:** writing 37-VERIFICATION.md. The truth table covers all 11 Phase 37 REQs (DSP-01..03, MIX-01..02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01). `score: 11/11 requirements verified` on success. Match the 36-VERIFICATION.md row shape exactly: ID + name + ✓ VERIFIED + bullet list of evidence files.

---

## Shared Patterns

### Pattern A: PRNG routing via `PrngRegistry` (D-v1.5-06)

**Source:** `flow-lang/Runtime/PrngRegistry.cs:78-113`
**Apply to:** GranularEngine.cs (jitter draws), SfzRenderer round-robin counter (alternative — RESEARCH §Pattern 5 suggests voice-ordinal seeding instead, but if Plan 37-03 chooses PRNG-keyed seeding it must route through here)

```csharp
public Random GetRandom(SourceLocation site, string name)
{
    var key = (site, name);
    if (!_registry.TryGetValue(key, out var rng))
    {
        int seed = ComputeDeterministicSeed(site, name, _renderBoundarySalt);
        rng = new Random(seed);
        _registry[key] = rng;
        _drawCounts[key] = 0;
    }
    return rng;
}

public double NextDouble(SourceLocation site, string name)
{
    var rng = GetRandom(site, name);
    _drawCounts[(site, name)] = _drawCounts[(site, name)] + 1;
    return rng.NextDouble();
}
```
**Contract:** any new stochastic primitive MUST call `ctx.PrngRegistry.NextDouble(site, generatorName)` — never `new Random()` directly. Use DISTINCT generator names per use site (Pitfall 8 — collision between `granular_offset` / `granular_timing` / `sfz_rr` / etc.). The site comes from the call AST node; the registry is reseeded at every `renderSong`/`writeWav` boundary via `ResetAtRenderBoundary()`.

### Pattern B: One-shot stderr advisory (Phase 32 `[tuning]` precedent)

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36`
**Apply to:** All Phase 37 surfaces that warn — `[stretch] mode=#auto picked: X% vocoder / Y% psola`, `[pitchShift] >12 semitone shift on drum sample`, `[sfz] seq_length exceeds spec max 100`, etc.

```csharp
public static void WarnOnce(string sentinelKey, string message)
{
    lock (_lock)
    {
        if (!_emitted.Add(sentinelKey)) return;
    }
    Console.Error.WriteLine(message);
}
```
**Contract:**
- `sentinelKey` MUST disambiguate by call-site + context (e.g. `$"stretch:auto:{sourceLocation}:{summary}"`). The summary in the key dedups identical summaries across loop iterations naturally (Open Question 5 recommendation).
- Message prefix follows the `[surface]` convention: `[stretch]`, `[pitchShift]`, `[sfz]`, `[granular]`.
- Test isolation: every test class that exercises an advisory MUST call `RenderingDiagnostics.ResetForTesting()` in its ctor + Dispose (see SfzSmokeTests pattern).

### Pattern C: Articulation envelope stacking (SAMP-03)

**Source:** `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:134-139` + `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs:191-200`
**Apply to:** SAMP-03 sample-path envelope multiplier — STACKS multiplicatively on top of Phase 28's helper

```csharp
// Phase 28 baseline (do NOT modify):
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack: 0.005, baseDecay: 0.05, baseSustain: 1.0, baseRelease: 0.05,
    frames: authoredFrames, sampleRate: sampleRate, isPercussion: false);
for (int i = 0; i < authoredFrames && i < fitted.Length; i++)
    fitted[i] *= envelope[i];

// SAMP-03 sample-path multiplier (Phase 37 — stacks AFTER the baseline):
// applies ONLY in the sample/SFZ caller, never inside SynthUtils.
// See Pitfall 10 in 37-RESEARCH.md.
```
**Contract:** `SynthUtils.GenerateArticulationADSR` is the Phase 28 LOCKED contract (CLAUDE.md "Locked articulation rules"). The SAMP-03 multiplier MUST live at the caller site (`SampledInstrumentRenderer.Render` and `SfzRenderer.Render`), NOT inside `SynthUtils`. This keeps the synth path's Phase 28 RMS regression tests green.

### Pattern D: Buffer-out pure DSP

**Source:** `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs:18-58` + `Filter.cs:16-43` + `Compressor.cs:18-93`
**Apply to:** All new DSP — GranularEngine, PhaseVocoder, Psola, StretchEngine, PitchShiftEngine

```csharp
public static AudioBuffer Apply(AudioBuffer input, ...)
{
    // Validate inputs first (throw ArgumentException on negative durations, etc.)
    // Allocate NEW AudioBuffer for result — input is never mutated
    var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);
    // Process per-channel, return result
    return result;
}
```
**Contract:** `static class` exposing `Apply(...)` (or `Process(...)` per the engine semantic). Always allocate a fresh AudioBuffer. Never mutate the input. Match the input's `SampleRate` and `Channels` (granular/stretch/pitch-shift preserve channel count per RESEARCH §code_context).

### Pattern E: Charitable interpretation (silent fallback + WarnOnce)

**Source:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:483-487` + `SfzRenderer.cs:140-146`
**Apply to:** Every input-validation gate in Phase 37 surfaces (per CLAUDE.md "Charitable Interpretation" memory)

```csharp
// Pattern: silent fallback to spec default + one-shot advisory; never throw on composer-supplied data.
if (!region.TryGetValue("loop_mode", out var lmStr))
    return SfzLoopMode.NoLoop;
switch (lmStr)
{
    case "no_loop":          loopMode = SfzLoopMode.NoLoop; break;
    // ...
    default:
        RenderingDiagnostics.WarnOnce(
            $"sfz:opcode_value:{patchDescription}:loop_mode:{lmStr}",
            $"[sfz] unknown loop_mode value '{lmStr}' in '{patchDescription}' — falling back to no_loop");
        loopMode = SfzLoopMode.NoLoop;
        break;
}
```
**Contract:** Composer wrote a bad value → log + use the spec default. Composer never sees an exception unless they passed truly out-of-spec data that would corrupt downstream (e.g. `frameSize=0` for FFT — RESEARCH §Security Domain mandates `throw` there). For all 11 Phase 37 REQs, prefer silent-and-documented over exceptions — the music keeps playing.

## No Analog Found

Files with no close codebase match (planner should use RESEARCH.md patterns directly):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang/StandardLibrary/Audio/DSP/Fft.cs` | radix-2 Cooley-Tukey forward+inverse FFT | float[] → Complex[] | No FFT in production codebase. Closest is `flow-lang.Tests/Helpers/Phase29Fft.cs` (a TEST-side DFT helper used for harmonic-richness assertions). Use that as a reference for the math; ship a true production radix-2 FFT (~80 lines per RESEARCH §Standard Stack). |
| `flow-lang/StandardLibrary/Audio/DSP/PhaseVocoder.cs` | STFT analysis → phase-locked propagation → ISTFT overlap-add | Buffer → Buffer (frame-by-frame state) | No STFT pipeline in production codebase. Follow RESEARCH §Pattern 1 (Laroche-Dolson 1999 algorithm sketch) literally; structural pattern (Apply + private ProcessChannel) borrowed from Reverb.cs. |
| `flow-lang/StandardLibrary/Audio/DSP/Psola.cs` | YIN pitch detection + epoch-OLA pitch-period grains | Buffer → Buffer (with pitch-period state per voiced segment) | No PSOLA / pitch detection in production codebase. Follow RESEARCH §Pattern 2 (YIN cumulative-mean-normalized-difference + voicing gate); structural pattern (Apply + private epoch helpers) borrowed from Reverb.cs. |

## Metadata

**Analog search scope:**
- `flow-lang/StandardLibrary/Audio/` (29 files)
- `flow-lang/StandardLibrary/Audio/DSP/` (6 files — `Reverb`, `Filter`, `Compressor`, `Delay`, `Panner`, `SidechainCompressor`)
- `flow-lang/StandardLibrary/Audio/Sfz/` (8 files — including the `SfzBuiltins`/`SfzParser`/`SfzRenderer`/`SfzRegion` quartet under modification)
- `flow-lang/Runtime/PrngRegistry.cs` + `flow-lang/Diagnostics/RenderingDiagnostics.cs` (Phase 36 + Phase 32 cross-cutting infrastructure)
- `flow-lang/Core/FlowEngine.cs:90-180` (Register-call dispatch site for new builtin classes)
- `flow-lang.Tests/Integration/Phase29/` + `flow-lang.Tests/Integration/Phase33/` (Wave 0 test fixture analogs)
- `flow-lang.Tests/Helpers/RmsRegressionTests.cs` + `flow-lang.Tests/baselines/Phase28/` (RMS regression infrastructure)
- `flow-lang/Samples/{piano,flute,brass,sax,strings,bell}/` (Phase 29 sample bundle layout)
- `flow-lang/audio.flow` + `flow-lang/sfz.flow` + `flow-lang/std.flow` (composer-facing stdlib `.flow` modules)
- `examples/scala/intro.flow` + `examples/symphony/sfz_smoke.flow` (tutorial-chapter analogs for Plan 37-07)
- `.planning/phases/36-sequence-algebra-generative/` (Phase 36 patterns + closer-plan template)

**Files scanned:** ~62 production C# files + 8 `.flow` stdlib + 18 test C# + 3 example `.flow` + 2 markdown templates

**Pattern extraction date:** 2026-05-22

**Key insight:** Phase 37's reused-vs-new ratio is high (echo of RESEARCH §Don't Hand-Roll). Of 47 target files, only 3 have NO codebase analog (the FFT/PhaseVocoder/Psola greenfield trio). Every other file extends an existing record, parser, renderer, registration class, manifest dict, or test pattern.
