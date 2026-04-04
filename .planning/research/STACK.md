# Technology Stack

**Project:** Flow Language v1.1 -- Polish & Foundations
**Researched:** 2026-04-02
**Scope:** Math stdlib, synth presets (strings/organ/bell), tempo ramps, audio buffer mixing, Sequence overload resolution fix

## Guiding Principle: No New Dependencies

Every feature in this milestone is implementable with existing .NET 9 APIs and the current codebase architecture. No new NuGet packages, no new runtime requirements. The project's hand-roll philosophy continues to hold.

## Recommended Stack

### Core Runtime (No Changes)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 9 | net9.0 | Runtime | Already in use |
| C# 13 | Latest | Language | Record types, pattern matching already used throughout |
| PulseAudio (P/Invoke) | System | Audio playback | Already implemented |

### Features Requiring NO New Dependencies

| Feature | Implementation Approach | Why No Library Needed |
|---------|------------------------|----------------------|
| **Math stdlib** | `System.Math` wrappers registered as built-in functions | `System.Math` covers sin/cos/abs/sqrt/min/max/pow/log and more |
| **Strings synth** | Detuned saws + slow ADSR + vibrato in new `StringsSynthesizer.cs` | `SynthUtils.GenerateSaw()` already supports additive layering |
| **Organ synth** | Additive sine harmonics (Hammond model) in new `OrganSynthesizer.cs` | `SynthUtils.GenerateSine()` handles additive layering natively |
| **Bell synth** | Inharmonic partials + per-partial decay in new `BellSynthesizer.cs` | `SynthUtils.GenerateSine()` + `GenerateADSR()` per partial |
| **Tempo ramps** | Transform function with integrated beat-to-time mapping | Linear interpolation math -- no external DSP needed |
| **Buffer mixing** | Sample-by-sample addition with normalization | Arithmetic on float arrays |
| **Sequence overload fix** | Debug and fix type-matching logic | Bug fix in existing code |
| **Per-section gain** | Read gain from MusicalContext, multiply section buffer | 5-line addition to SongRenderer |

### Libraries Explicitly NOT Recommended for This Milestone

| Library | Why Not |
|---------|---------|
| **MathNet.Numerics** | 3MB+ dependency for functions already in `System.Math`. Only useful for matrix math, FFT, or statistical distributions -- none needed here. |
| **NWaves** | Would duplicate existing DSP stack. Last updated Oct 2021. Already rejected in prior research. |
| **NAudio / CSCore** | Windows-centric. Already rejected. |

## Feature-Specific Technical Approaches

### 1. Math Standard Library Functions

**What to build:** `sin`, `cos`, `tan`, `abs`, `sqrt`, `pow`, `log`, `log10`, `min`, `max`, `floor`, `ceil`, `round`, `pi`, `e`

**Implementation pattern:** Follow the existing registration pattern in `BuiltInFunctions.RegisterStdLib()`. Each function needs a `FunctionSignature` with typed parameters, registered via `registry.Register()`.

**Type strategy:** Register Double overloads as primary. For `abs`, `min`, `max`, also register Int overloads. The numeric widening chain (Int -> Long -> Float -> Double -> Number) means Int args will auto-resolve to Double overloads via `CanConvertTo`, but Int-specific overloads avoid unnecessary conversion and preserve Int return types.

```
// Functions taking Double, returning Double:
sin(Double) -> Double        // Math.Sin
cos(Double) -> Double        // Math.Cos
tan(Double) -> Double        // Math.Tan
sqrt(Double) -> Double       // Math.Sqrt
log(Double) -> Double        // Math.Log (natural)
log10(Double) -> Double      // Math.Log10
floor(Double) -> Double      // Math.Floor
ceil(Double) -> Double       // Math.Ceiling
round(Double) -> Double      // Math.Round

// Two-argument functions:
pow(Double, Double) -> Double  // Math.Pow
atan2(Double, Double) -> Double // Math.Atan2
mod(Int, Int) -> Int           // % operator (useful for rhythm math)
mod(Double, Double) -> Double  // Math.IEEERemainder or fmod equivalent

// Functions with Int AND Double overloads:
abs(Int) -> Int              // Math.Abs
abs(Double) -> Double        // Math.Abs
min(Int, Int) -> Int         // Math.Min
min(Double, Double) -> Double // Math.Min
max(Int, Int) -> Int         // Math.Max
max(Double, Double) -> Double // Math.Max

// Constants as zero-arg functions:
pi() -> Double               // Math.PI
e() -> Double                // Math.E
```

**Why zero-arg functions for constants:** Adding `pi` and `e` as language-level constants would require new parser rules or a constant declaration mechanism. Zero-arg functions (`(pi)` or `pi()`) avoid parser changes entirely and are consistent with Flow's functional call style. The existing codebase has no constant declaration pattern to follow.

**Where to add code:**
- New file: `StandardLibrary/MathFunctions.cs` with a static `Register(InternalFunctionRegistry)` method
- Registration: Call `MathFunctions.Register(registry)` from `BuiltInFunctions.RegisterAllImplementations()`
- Follow the pattern of `Transforms.TransformFunctions.Register()` or `Audio.EffectsFunctions.Register()`

**Confidence:** HIGH -- `System.Math` is stable, all functions exist in .NET 9, registration pattern is well-established.

---

### 2. New Synthesizer Presets: Strings, Organ, Bell

All three follow the established `INoteSynthesizer` interface. Create a new `.cs` file per synth in `StandardLibrary/Audio/Synthesizers/`, then add name mappings in `SynthesizerFactory.Create()` switch expression.

#### Strings Synthesizer (`StringsSynthesizer.cs`)

**Algorithm:** Layered detuned sawtooth waves with slow attack ADSR, simulating an ensemble string section.

- **Oscillators:** 3-4 detuned saw waves at fundamental frequency with spread of +/- 3-5 cents. The detuning creates the characteristic "chorus" shimmer of a string section. Use `SynthUtils.GenerateSaw()` which is already additive (+=).
- **Vibrato:** Low-frequency sine modulation (~5 Hz) on pitch, depth ~0.3% of frequency. Applied per-sample as a frequency offset. This requires a manual per-sample loop rather than using the batch `GenerateSaw()` -- accumulate phase manually with modulated frequency.
- **ADSR:** Slow attack (0.15-0.25s), short decay (0.1s), high sustain (0.75), moderate release (0.2s). Strings swell in -- this is their defining characteristic vs. other instruments.
- **Filtering:** `SynthUtils.OnePoleLP` at ~3000 Hz to soften the saw harshness. No biquad needed.

**Why detuned saws:** This is how virtually every synthesizer from the Roland Juno-60 to modern soft synths produces string patches. Karplus-Strong physical modeling produces "plucked string" sounds (guitar, harpsichord), which is the wrong character for orchestral strings. Detuned saws are the correct subtractive synthesis technique for ensemble strings.

#### Organ Synthesizer (`OrganSynthesizer.cs`)

**Algorithm:** Additive sine waves at harmonic drawbar positions (Hammond organ model).

- **Oscillators:** Sum of sine waves at sub-fundamental (0.5x), fundamental (1x), 2nd harmonic (1.5x), 3rd (2x), 4th (3x), 5th (4x), 6th (5x), 7th (6x), 8th (8x). These correspond to the 9 Hammond drawbar positions. Use `SynthUtils.GenerateSine()`.
- **Drawbar preset:** Model the classic "full registration" 888888888 or the more restrained 888000000 (full sub/fundamental/2nd). Start with a fixed preset -- user-configurable drawbars are a future extension.
- **ADSR:** Very fast attack (0.003s), minimal decay (0.01s), full sustain (1.0), fast release (0.01s). Organs have click-on/click-off character with no swell.
- **Key click:** Short noise burst (like PianoSynthesizer's hammer transient) for the mechanical key-click artifact. ~0.5ms burst via a separate ADSR with near-zero decay. Use `SynthUtils.GenerateWhiteNoise()` + short envelope.
- **Optional Leslie effect:** Subtle amplitude modulation at ~6 Hz to simulate Leslie speaker rotation. `amplitude *= 1.0 + 0.15 * sin(2*PI*6*t)`. Not essential for v1 but adds authenticity cheaply.

**Why additive synthesis:** Hammond organs are literally additive synthesis -- they use rotating tonewheels generating sine waves at harmonic intervals. This is the most physically accurate simple model possible. No sampling, no FM, no subtractive filtering needed.

#### Bell Synthesizer (`BellSynthesizer.cs`)

**Algorithm:** Inharmonic partials with per-partial exponential decay (Risset bell model).

- **Oscillators:** Sine waves at inharmonic frequency ratios: 1.0x, 2.0x, 2.76x, 4.07x, 5.41x, 6.58x. These ratios produce the characteristic metallic/bell timbre. Based on Jean-Claude Risset's 1969 bell synthesis research. Use `SynthUtils.GenerateSine()`.
- **Per-partial decay:** Higher partials decay faster. Apply separate ADSR envelopes per partial: fundamental has longest decay (1.5s), upper partials shorter (0.3-0.6s). Requires multiple `SynthUtils.GenerateADSR()` calls and per-partial envelope application.
- **Global ADSR:** Near-zero attack (0.001s), long decay (1.0-2.0s), zero sustain (0.0), short release (0.05s). Bells are percussive -- they ring and die, they don't sustain.
- **Pitch-dependent behavior:** Higher-octave bells should have faster decay. Scale decay time inversely with MIDI note number, similar to how PianoSynthesizer scales inharmonicity.

**Why inharmonic additive:** Bell timbres are defined by their non-harmonic partial structure -- this is well-established acoustics. FM synthesis (DX7-style) can produce great bells but requires careful modulation index tuning. Explicit frequency ratios are more predictable and use existing `GenerateSine()` infrastructure.

**Registration in factory (SynthesizerFactory.Create):**

```csharp
"strings" or "string" => new StringsSynthesizer(),
"organ" => new OrganSynthesizer(),
"bell" or "bells" or "glockenspiel" => new BellSynthesizer(),
```

**Confidence:** HIGH for all three -- standard synthesis techniques using existing infrastructure.

---

### 3. Tempo Ramps (Gradual BPM Change)

**The problem:** `MusicalContext.Tempo` is a single `double?` value. Sections render at fixed BPM via `SongRenderer.RenderSection()` which passes `bpm` as a scalar. There is no mechanism to change BPM over time within a section.

**Recommended approach: Transform function, not syntax**

Implement as `tempoRamp(Sequence, Double, Double) -> Buffer` rather than new parser syntax.

**Why a function, not `tempo 120..140 { }` syntax:**
- Parser changes are expensive -- new token types, AST nodes, MusicalContext semantics.
- A transform function is self-contained and follows existing patterns (`crescendo`, `ritardando`).
- Ships without touching the parser. Can promote to syntax later if needed.

**Why it returns Buffer, not Sequence:**
Tempo ramps affect audio timing, not musical structure. A Sequence is a collection of notes with beat-based positions. Tempo ramps change how beats map to seconds -- this only matters at render time. Returning a Buffer makes this explicit: the tempo ramp renders the sequence to audio with the ramp applied.

**Implementation:**

```
tempoRamp(Sequence, startBPM, endBPM) -> Buffer
```

Renders the sequence with linearly interpolated BPM:

1. Walk the sequence's bars/notes in beat order.
2. For each note at beat offset `b` (total beats = `B`):
   - Compute `t = b / B` (progress 0..1)
   - Compute `instantBPM = startBPM + t * (endBPM - startBPM)`
3. Convert beat positions to seconds. For linear tempo ramp, beat-to-seconds is NOT linear. The closed-form integral is:
   ```
   seconds(b) = (60 * B / (endBPM - startBPM)) * ln((startBPM + (endBPM - startBPM) * b/B) / startBPM)
   ```
   When startBPM == endBPM (degenerate case), use standard `60/bpm * b`.
4. Render each note using `SynthesizerFactory.Create()` and position in the output buffer at the computed time offset.

**Where to add:**
- New file: `StandardLibrary/Audio/TempoRampRenderer.cs` with static `Register()` method
- Or add to existing `TransformFunctions.cs` (it already has `ritardando`/`accelerando`)
- Register in `BuiltInFunctions.RegisterAllImplementations()`

**Key challenge:** The current rendering pipeline in `SequenceRenderer` assumes fixed BPM. The tempo ramp function must bypass `SequenceRenderer` and do its own per-note rendering with variable timing. This is acceptable because it's a specialized render path, not a change to the general pipeline.

**Confidence:** MEDIUM -- the math is straightforward but the bypass of `SequenceRenderer` needs care. Need to handle synthesizer selection (instrument name parameter), stereo output, and voice mixing manually within the ramp renderer.

---

### 4. Audio Buffer Mixing (`mix()`)

**What to build:** A function for layering pre-rendered audio buffers.

**Signatures:**

```
mix(Buffer, Buffer) -> Buffer           // two-buffer mix
mix(Buffer, Buffer, Double) -> Buffer   // crossfade (0.0 = all first, 1.0 = all second)
```

**Algorithm:**
1. Output length = max frames of all inputs.
2. Allocate stereo output buffer at 44100 Hz.
3. For each frame, sum all input samples. Handle mono-to-stereo promotion: mono input contributes equally to L and R channels.
4. **Normalization:** Divide by `sqrt(N)` where N is the number of inputs (constant-power normalization). This is better than `1/N` (too quiet) or no normalization (clips easily). Standard audio engineering practice.

**Crossfade variant:** When a ratio is provided, `output = buf1 * (1-ratio) + buf2 * ratio`. No normalization needed since total energy is preserved.

**Where to add:** New file `StandardLibrary/Audio/MixFunctions.cs`, registered in `BuiltInFunctions.RegisterAllImplementations()`.

**Why not reuse SongRenderer.MixVoicesToStereoBuffer:** That method operates on `Voice` objects with beat offsets, pan, and gain. A `mix()` function for raw buffers is simpler and more general -- it operates on pre-rendered audio at a different abstraction level.

**Confidence:** HIGH -- buffer mixing is trivial arithmetic. The only design decision is normalization, and sqrt(N) is the standard choice.

---

### 5. Sequence Type Overload Resolution Fix

**The bug:** `transpose` and `vary` fail when called with Sequence arguments via the flow operator.

**Root cause analysis from code reading:**

The overload resolution in `FunctionSignature.Matches()` checks bidirectional compatibility:
```csharp
argTypes[i].IsCompatibleWith(InputTypes[i])    // arg -> param
|| argTypes[i].CanConvertTo(InputTypes[i])      // arg convertible to param
|| InputTypes[i].IsCompatibleWith(argTypes[i])  // param -> arg (reverse check)
|| InputTypes[i].CanConvertTo(argTypes[i])      // param convertible to arg
```

`SequenceType` does NOT override `IsCompatibleWith` or `CanConvertTo`. It inherits from `FlowType` which does exact-type equality only (`Equals(target)`). Since `SequenceType` is a singleton (`Instance`), and `Value.Sequence()` correctly wraps with `SequenceType.Instance`, exact-type equality should work.

**The bug is therefore NOT in type compatibility itself.** Most likely root causes (investigate in this order):

1. **Ambiguous overloads between Semitone and Cent.** `transpose` has `(Sequence, Semitone)` and `(Sequence, Cent)`. If a literal like `3st` is somehow typed as something compatible with both, or if Int values match both Semitone and Cent via `CanConvertTo`, the resolver hits the ambiguity branch (line 63-70 of OverloadResolver.cs). Check `SemitoneType.GetSpecificity()` vs `CentType.GetSpecificity()` -- if they return the same value, any argument type that matches both will trigger ambiguity.

2. **Int literal interpreted as wrong type.** If the user writes `seq -> transpose(3)` instead of `seq -> transpose(3st)`, the `3` is `IntType`, not `SemitoneType`. Check whether `IntType.IsCompatibleWith(SemitoneType)` or `SemitoneType.IsCompatibleWith(IntType)` returns true -- if neither does, the overload won't match at all. If both return true, it creates ambiguity with the Cent overload.

3. **Flow operator argument insertion.** When `seq -> transpose(3st)` is parsed, it becomes `transpose(seq, 3st)`. Verify the parser correctly inserts `seq` as the first argument and preserves the `3st` literal's type.

**Fix approach:** This is a debugging task. Run a failing test case with diagnostic output to identify the actual mismatch. The fix will likely be one of:
- Add specificity differentiation between `SemitoneType` and `CentType`
- Fix type compatibility for `Int -> Semitone` conversion (make `SemitoneType.IsCompatibleWith(IntType)` return true)
- Fix how the parser handles musical literals in flow expressions

**Where to investigate:**
- `TypeSystem/SpecialTypes/SemitoneType.cs` -- check `GetSpecificity()`, `IsCompatibleWith()`, `CanConvertTo()`
- `TypeSystem/SpecialTypes/CentType.cs` -- same checks
- `Runtime/Value.cs` -- verify `Value.Semitone()` wraps with correct type
- `Parsing/Parser.cs` -- verify flow expression transformation preserves literal types

**Confidence:** MEDIUM -- diagnosis is code-reading based, not runtime-verified. The actual root cause will emerge from running failing tests.

---

### 6. Per-Section Volume/Gain in Songs

**What to build:** Allow song sections to render at different volume levels.

`MusicalContext` already has a `Velocity` property (0.0 to 1.0). `SongRenderer.RenderSection()` already reads `section.Context?.Tempo`. Extend to also read gain and apply as a buffer multiplier:

```csharp
// In RenderSection(), after MixVoicesToStereoBuffer():
double sectionGain = section.Context?.Velocity ?? 1.0;
if (Math.Abs(sectionGain - 1.0) > 0.001)
{
    for (int i = 0; i < buffer.Data.Length; i++)
        buffer.Data[i] *= (float)sectionGain;
}
```

**Where:** `SongRenderer.RenderSection()` -- a 5-line addition.

**Confidence:** HIGH -- trivial extension of existing infrastructure.

---

## Summary

| Feature | Approach | Dependencies | Complexity |
|---------|----------|-------------|------------|
| Math stdlib | `System.Math` wrappers as built-in functions | None | Low |
| Strings synth | Detuned saws + slow ADSR + vibrato | None | Medium |
| Organ synth | Additive sine harmonics (Hammond drawbar model) | None | Low |
| Bell synth | Inharmonic Risset partials + per-partial decay | None | Medium |
| Tempo ramps | Transform function with non-linear beat-to-time mapping | None | Medium-High |
| Buffer mixing | Sample addition with sqrt(N) normalization | None | Low |
| Sequence overload fix | Debug type matching (likely Semitone/Cent specificity) | None | Low-Medium |
| Per-section gain | Multiply section buffer by context velocity | None | Low |

**Total new external dependencies: 0**

## Sources

- Existing codebase: `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` -- reference synthesizer pattern
- Existing codebase: `flow-lang/StandardLibrary/Audio/SynthUtils.cs` -- oscillator and envelope primitives
- Existing codebase: `flow-lang/TypeSystem/OverloadResolver.cs` -- overload resolution logic
- Existing codebase: `flow-lang/TypeSystem/FunctionSignature.cs` -- type matching and specificity scoring
- Existing codebase: `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- section rendering and voice mixing
- Existing codebase: `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` -- transform function patterns
- .NET 9 `System.Math` API -- sin, cos, tan, sqrt, pow, log, abs, min, max, floor, ceiling, round, PI, E
- Hammond organ tonewheel frequencies -- standard additive synthesis (tonewheels at sub/fundamental/harmonic positions)
- Risset bell synthesis (Jean-Claude Risset, 1969) -- inharmonic partial ratios for metallic timbres
- Constant-power mixing normalization -- standard audio engineering practice (divide by sqrt(N))
- Constant-power panning already implemented in `SongRenderer.MixVoicesToStereoBuffer()` (verified in codebase)
