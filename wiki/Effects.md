# Effects

Flow provides built-in audio effects for processing buffers. Effects return a new buffer (non-destructive) and chain cleanly with the flow operator. Most effects live in `@audio`.

## Reverb

Schroeder-style reverb (4 parallel comb filters + 2 series allpass filters) with damping and mix controls:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)

Note: simple (room size only)
Buffer wet = (reverb tone 0.5)

Note: full control (roomSize, damping, mix)
Buffer wetFull = (reverb tone 0.7 0.3 0.5)

Note: decay-time form — Second-typed RT60 instead of damping/mix tuple
Buffer hall = (reverb tone 0.7 2.5s)
```

| Parameter | Range | Description |
|-----------|-------|-------------|
| `roomSize` | 0.0 - 1.0 | Simulated room size (0=small, 1=large) |
| `damping` | 0.0 - 1.0 | High-frequency absorption (default 0.5) |
| `mix` | 0.0 - 1.0 | Dry/wet mix (default 0.3) |
| `decay` | `Second` | Schroeder closed-form RT60 -> damping mapping |

**Reverb renders its decay tail past the input end.** The returned buffer is longer than the input so the room sound decays naturally (tail capped at 10 s). Known exception: a section's final bar clips at the section boundary (v1.6 fix pending).

## Filters

Biquad filters (Direct Form I, RBJ Cookbook coefficients) with `Q=0.707` (Butterworth) by default. Cutoffs accept either bare `Double` (Hz) or a `Hertz`-typed literal:

```flow
Buffer lp  = (lowpass  tone 800.0)        Note: cutoff at 800 Hz (Double)
Buffer lp2 = (lowpass  tone 1.5kHz)       Note: Hertz literal — same overload
Buffer hp  = (highpass tone 200Hz)
Buffer bp  = (bandpass tone 200Hz 2.0kHz) Note: band edges
```

## Compressor

Peak-detect compressor with attack/release envelopes. Reduces dynamic range by attenuating loud signals:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)

Note: simple (threshold dB, ratio)
Buffer comp = (compress tone -12dB 4.0)

Note: full (threshold, ratio, attack ms, release ms)
Buffer compFull = (compress tone -12dB 4.0 5ms 50ms)

Note: bare Double / Double also works
Double negTwelve = (sub 0.0 12.0)
Buffer compNum  = (compress tone negTwelve 4.0)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `threshold` | `Decibel` or `Double` | Level above which compression engages (typically negative) |
| `ratio` | N:1 | Compression ratio (4.0 = 4:1) |
| `attack` | `Millisecond` or `Double` | How quickly the compressor reacts |
| `release` | `Millisecond` or `Double` | How quickly the compressor releases |

## Sidechain Compression

Compress one source using a separate control signal (e.g., duck a bass under a kick):

```flow
use "@std"
use "@audio"

Buffer kick = (createSineTone 60Hz 0.1 0.9)
Buffer bass = (createSineTone 80Hz 1.0 0.8)

Note: basic sidechain (bass ducks under kick)
Buffer ducked = (sidechain bass kick -12dB 4.0)

Note: with custom attack/release (Millisecond-typed)
Buffer ducked2 = (sidechain bass kick -12dB 4.0 5ms 200ms)

Note: chainable
Buffer piped = bass -> (sidechain kick -12dB 4.0)
```

The output buffer matches the source's frame count — it is not truncated to the trigger's length.

## Delay

Feedback delay with three input forms — bare milliseconds, `Millisecond` literal, or tempo-synced `NoteValue`. NoteValue constants (`EIGHTH`, `QUARTER`, `HALF`, `WHOLE`, `SIXTEENTH`) live in `@notation`:

```flow
use "@notation"   Note: brings EIGHTH/QUARTER/HALF/WHOLE/SIXTEENTH into scope

Buffer delayed   = (delay tone 250.0 0.4 0.5)        Note: 250 ms (Double)
Buffer delayedMs = (delay tone 250ms 0.4 0.5)        Note: Millisecond literal
tempo 120 {
    Buffer synced = (delay tone EIGHTH 0.4 0.5)      Note: tempo-synced — reads active BPM
}
```

> **Snippet** — `tone` must be defined before this block (e.g. `Buffer tone = (createSineTone 440Hz 0.5 0.5)`).

| Parameter | Unit | Description |
|-----------|------|-------------|
| `time` | `Double` ms, `Millisecond`, or `NoteValue` | Delay time |
| `feedback` | 0.0 - 1.0 | Amount fed back (0 = single echo) |
| `mix` | 0.0 - 1.0 | Dry/wet mix |

## Gain (dB) vs Volume (linear)

Flow splits dB-attenuation and linear-multiplier into two named builtins so the unit is unambiguous at the call site:

```flow
Buffer quieter = (gain tone -6dB)        Note: dB attenuation
Buffer louder  = (gain tone +6dB)
Buffer half    = (volume tone 0.5)       Note: linear multiplier — 0.5 = half-amplitude
Buffer doubled = (volume tone 2.0)       Note: 2.0 = double-amplitude (clipping warning if it crests 1.0)
```

`volume` rejects negative arguments — use `gain` with a `-dB` value for attenuation. Both emit a one-shot stderr clipping warning when post-multiplication samples exceed 1.0.

## Panning

Constant-power stereo panning (`cos²(θ) + sin²(θ) = 1`). Range is `-1.0` (hard left) to `+1.0` (hard right):

```flow
use "@audio"

Buffer tone   = (createSineTone 440Hz 0.5 0.8)
Buffer center = (pan tone 0.0)
Buffer left   = (pan tone -1.0)
Buffer right  = (pan tone 1.0)

Note: chainable
Buffer piped = tone -> (pan 0.3)
```

Mono inputs are promoted to stereo automatically.

`pan` is also a [musical context block](Musical-Context.md) that applies to everything rendered inside it:

```flow
pan -0.5 {
    section leftPart {
        Sequence mel = | C4 E4 G4 |
    }
}
```

## Fade In / Fade Out

Linear amplitude fades (duration in seconds):

```flow
Buffer faded    = tone -> fadeIn  0.5
Buffer fadedOut = tone -> fadeOut 0.5s
```

## Tempo Ramp

Render a sequence with linearly-interpolated per-bar BPM — useful for ritardando / accelerando passages baked into a single buffer:

```flow
Sequence build = | C4 E4 G4 C5 E5 G5 |
Buffer ramp = (tempoRamp build 100.0 160.0)         Note: 100 -> 160 BPM over the sequence
Buffer rampOrgan = (tempoRamp build 100.0 160.0 "organ")
```

## Granular Synthesis

Slice a buffer into short overlapping grains and resynthesize with controllable density, randomized timing, and a choice of window function:

```flow
use "@audio"

Buffer src = (loadWav "pad.wav")
Buffer dust = (granular src 50ms 20Hz 0.3)                   Note: defaults to Hann window
Buffer soft = (granular src 80ms 15Hz 0.2 windowing=#gaussian)
Buffer tight = (granular src 30ms 40Hz 0.1 windowing=#tukey)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `grain` | `Millisecond` | Grain length |
| `density` | `Hertz` | Grains per second |
| `jitter` | 0.0 - 1.0 | Random offset amount (PRNG-routed for reproducibility) |
| `windowing` | `#hann` (default), `#gaussian`, `#tukey` | Per-grain envelope shape |

Unknown windowing symbols fall back to Hann with a one-shot stderr advisory.

## Time-Stretch

Change a buffer's duration without changing its pitch. Three modes pick the algorithm: `#vocoder` (Laroche-Dolson phase-locked STFT — best for harmonic content), `#psola` (TD-PSOLA + YIN pitch detection — best for percussive / monophonic content), and `#auto` (Fitzgerald HPS per-frame source separator — picks per frame, emits a one-shot summary advisory).

```flow
use "@audio"

Buffer src   = (loadWav "loop.wav")
Buffer half  = (stretch src 2.0)                       Note: twice as long
Buffer tight = (stretch src 0.5 mode=#vocoder)         Note: half as long
Buffer perc  = (stretch src 1.5 mode=#psola)
```

`factor=1.0` short-circuits to identity (byte-identical to input). Six knobs thread through end-to-end via prefix-ladder arity overloads: `frameSize` (default 2048), `hopSize` (default 512), `overlap` (default 4 — CCRMA Hann COLA minimum), `transientThreshold` (default 0.3), `pitchPeriod` (PSOLA YIN override), and `windowSize` (PSOLA grain override).

## Pitch-Shift

Change a buffer's pitch without changing its duration. Same three modes as `stretch`; cents arg accepts `Double` (cents), `Cent`, or `Semitone`:

```flow
Buffer up5    = (pitchShift src 500.0)            Note: +500 cents (Double)
Buffer up5c   = (pitchShift src +500c)            Note: Cent literal
Buffer up5st  = (pitchShift src +5st)             Note: Semitone literal — 5st = 500c internally
Buffer downOct = (pitchShift src -1200.0 mode=#psola)
```

`cents=0` short-circuits to identity. The full overload matrix is 24 signatures (3 cents-types × 8 arity steps for the prefix-ladder knobs).

## Effect Chaining

Effects chain naturally left-to-right with `->`:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)
Double panLeft = (sub 0.0 0.2)     Note: negative bare literals cannot start a parenthesised pipe arg; bind first

Buffer processed = tone
    -> lowpass 1kHz
    -> reverb 0.3
    -> (pan panLeft)
    -> gain -3dB
```

Which reads: filter, then reverb, then pan, then gain.

> **Note on negative literals in pipe calls:** `-> (pan -0.2)` does not parse because `-0.2` is not valid as the first argument of a parenthesised pipe call. Bind the value to a variable first, or use `(sub 0.0 0.2)` inline.

### A Longer Chain

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence mel = | C4 E4 G4 C5 |
            }
            Song song = [intro]
            Buffer raw = (renderSong song "piano")

            Buffer final = raw
                -> lowpass 2.0kHz
                -> reverb 0.3
                -> (pan 0.1)
                -> fadeIn 0.3s
                -> fadeOut 0.5s
        }
    }
}
```

## Working with Negative Values

Music-typed literals like `-12dB`, `-50c`, `-2st`, `-250ms` carry their sign natively. For bare numeric arguments, signed numeric literals (`-3`, `-2.5`) parse as single tokens at expression-start; `(sub 0 N)` / `(sub 0.0 N)` also works:

```flow
Double negTwelve = (sub 0.0 12.0)
Buffer trim      = (compress tone -12dB 4.0)
```

## Effect Reference

| Effect | Signature | Description |
|--------|-----------|-------------|
| `reverb` | `(Buffer, Double) -> Buffer` | Reverb (room size) |
| `reverb` | `(Buffer, Double, Double, Double) -> Buffer` | Reverb (room, damping, mix) |
| `reverb` | `(Buffer, Double, Second) -> Buffer` | Reverb (room, RT60 decay) |
| `lowpass` | `(Buffer, Double\|Hertz) -> Buffer` | Low-pass (cutoff Hz) |
| `highpass` | `(Buffer, Double\|Hertz) -> Buffer` | High-pass (cutoff Hz) |
| `bandpass` | `(Buffer, Double\|Hertz, Double\|Hertz) -> Buffer` | Band-pass (low, high Hz) |
| `compress` | `(Buffer, Double\|Decibel, Double) -> Buffer` | Compressor (threshold, ratio) |
| `compress` | `(Buffer, Double\|Decibel, Double, Double\|Millisecond, Double\|Millisecond) -> Buffer` | Full compressor |
| `sidechain` | `(Buffer, Buffer, Double\|Decibel, Double[, attack, release]) -> Buffer` | Sidechain compression |
| `delay` | `(Buffer, Double\|Millisecond, Double, Double) -> Buffer` | Delay (time, feedback, mix) |
| `delay` | `(Buffer, NoteValue, Double, Double) -> Buffer` | Tempo-synced delay (reads active BPM) |
| `gain` | `(Buffer, Double\|Decibel) -> Buffer` | Gain in dB |
| `volume` | `(Buffer, Double) -> Buffer` | Linear-multiplier amplitude (rejects negatives) |
| `pan` | `(Buffer, Double) -> Buffer` | Stereo pan (-1 .. +1) — auto-promotes mono to stereo |
| `fadeIn` | `(Buffer, Double) -> Buffer` | Linear fade-in (seconds) |
| `fadeOut` | `(Buffer, Double) -> Buffer` | Linear fade-out (seconds) |
| `tempoRamp` | `(Sequence, Double, Double[, String]) -> Buffer` | Per-bar BPM-interpolated render |
| `granular` | `(Buffer, Millisecond, Hertz, Double[, windowing=#sym]) -> Buffer` | Granular synthesis |
| `stretch` | `(Buffer, Double[, mode=#sym, knobs...]) -> Buffer` | Time-stretch (no pitch change) |
| `pitchShift` | `(Buffer, Double\|Cent\|Semitone[, mode=#sym, knobs...]) -> Buffer` | Pitch-shift (no time change) |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation, synthesizers, SFZ sampler
- [Flow Operator](Flow-Operator.md) - Chaining with `->`
- [Playback and Export](Playback-and-Export.md) - Playing and saving processed audio
- [Musical Context](Musical-Context.md) - `pan`, `gain`, `reverbTime`, `dynamics` context blocks
