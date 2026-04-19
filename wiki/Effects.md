# Effects

Flow provides built-in audio effects for processing buffers. Effects return a new buffer (non-destructive) and chain cleanly with the flow operator. Most effects live in `@audio`.

## Reverb

Schroeder-style reverb with damping and mix controls:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)

Note: simple (room size only)
Buffer wet = (reverb tone 0.5)

Note: full control (roomSize, damping, mix)
Buffer wetFull = (reverb tone 0.7 0.3 0.5)
```

| Parameter | Range | Description |
|-----------|-------|-------------|
| `roomSize` | 0.0 - 1.0 | Simulated room size (0=small, 1=large) |
| `damping` | 0.0 - 1.0 | High-frequency absorption (default 0.5) |
| `mix` | 0.0 - 1.0 | Dry/wet mix (default 0.3) |

## Filters

### Low-pass / High-pass / Band-pass

```flow
Buffer lp = (lowpass  tone 800.0)           Note: cutoff at 800 Hz
Buffer hp = (highpass tone 200.0)           Note: cutoff at 200 Hz
Buffer bp = (bandpass tone 200.0 2000.0)    Note: 200 Hz - 2000 Hz
```

## Compressor

Reduces dynamic range by attenuating loud signals:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)
Double negTwelve = (sub 0.0 12.0)

Note: simple (threshold dB, ratio)
Buffer comp = (compress tone negTwelve 4.0)

Note: full (threshold, ratio, attack ms, release ms)
Buffer compFull = (compress tone negTwelve 4.0 5.0 50.0)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `threshold` | dB | Level above which compression engages (typically negative) |
| `ratio` | N:1 | Compression ratio (4.0 = 4:1) |
| `attack` | ms | How quickly the compressor reacts |
| `release` | ms | How quickly the compressor releases |

## Sidechain Compression

Compress one source using a separate control signal (e.g., duck a bass under a kick):

```flow
use "@std"
use "@audio"

Buffer kick = (createSineTone 0.1 60.0 0.9)
Buffer bass = (createSineTone 1.0 80.0 0.8)

Double negTwelve = (sub 0.0 12.0)

Note: basic sidechain (bass ducks under kick)
Buffer ducked = (sidechain bass kick negTwelve 4.0)

Note: with custom attack/release (ms)
Buffer ducked2 = (sidechain bass kick negTwelve 4.0 5.0 200.0)

Note: chainable
Buffer piped = bass -> (sidechain kick negTwelve 4.0)
```

**Signatures**

| Signature | Description |
|-----------|-------------|
| `(sidechain Buffer source, Buffer trigger, Double threshold, Double ratio) -> Buffer` | Basic ducking |
| `(sidechain ... + Double attackMs, Double releaseMs) -> Buffer` | Full control |

The output buffer matches the source's frame count — it is not truncated to the trigger's length.

## Delay

Feedback delay:

```flow
Buffer delayed = (delay tone 250.0 0.4 0.5)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `time` | ms | Delay time |
| `feedback` | 0.0 - 1.0 | Amount fed back (0 = single echo) |
| `mix` | 0.0 - 1.0 | Dry/wet mix |

## Gain

Apply gain in decibels:

```flow
Double negSix = (sub 0.0 6.0)
Buffer quieter = (gain tone negSix)     Note: -6 dB
Buffer louder  = (gain tone 6.0)        Note: +6 dB
```

## Panning

Constant-power stereo panning. Range is `-1.0` (hard left) to `+1.0` (hard right):

```flow
use "@audio"

Buffer tone   = (createSineTone 0.5 440.0 0.8)
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
Buffer fadedOut = tone -> fadeOut 0.5
```

## Effect Chaining

Effects chain naturally left-to-right with `->`:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)
Double negThree = (sub 0.0 3.0)

Buffer processed = tone
    -> lowpass 1000.0
    -> reverb 0.3
    -> (pan -0.2)
    -> gain negThree
```

Which reads: filter, then reverb, then pan, then gain.

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
                -> lowpass 2000.0
                -> reverb 0.3
                -> (pan 0.1)
                -> fadeIn 0.3
                -> fadeOut 0.5
        }
    }
}
```

## Working with Negative Values

Flow has no negative literal syntax. Use `(sub 0 N)` or `(sub 0.0 N)`:

```flow
Double negTwelve = (sub 0.0 12.0)   Note: -12.0 dB
Double negSix    = (sub 0.0 6.0)    Note: -6.0 dB
```

## Effect Reference

| Effect | Signature | Description |
|--------|-----------|-------------|
| `reverb` | `(Buffer, Double) -> Buffer` | Reverb (room size) |
| `reverb` | `(Buffer, Double, Double, Double) -> Buffer` | Reverb (room, damping, mix) |
| `lowpass` | `(Buffer, Double) -> Buffer` | Low-pass (cutoff Hz) |
| `highpass` | `(Buffer, Double) -> Buffer` | High-pass (cutoff Hz) |
| `bandpass` | `(Buffer, Double, Double) -> Buffer` | Band-pass (low, high Hz) |
| `compress` | `(Buffer, Double, Double) -> Buffer` | Compressor (threshold, ratio) |
| `compress` | `(Buffer, Double, Double, Double, Double) -> Buffer` | Full compressor |
| `sidechain` | `(Buffer, Buffer, Double, Double) -> Buffer` | Sidechain compression |
| `sidechain` | `(Buffer, Buffer, Double, Double, Double, Double) -> Buffer` | Sidechain with attack/release |
| `delay` | `(Buffer, Double, Double, Double) -> Buffer` | Delay (time, feedback, mix) |
| `gain` | `(Buffer, Double) -> Buffer` | Gain in dB |
| `pan` | `(Buffer, Double) -> Buffer` | Stereo pan (-1 .. +1) |
| `fadeIn` | `(Buffer, Double) -> Buffer` | Linear fade-in (seconds) |
| `fadeOut` | `(Buffer, Double) -> Buffer` | Linear fade-out (seconds) |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation and synthesis
- [Flow Operator](Flow-Operator.md) - Chaining with `->`
- [Playback and Export](Playback-and-Export.md) - Playing and saving processed audio
- [Musical Context](Musical-Context.md) - `pan` and `gain` context blocks
