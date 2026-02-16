# Effects

Flow provides built-in audio effects for processing buffers. All effects return a new buffer (non-destructive) and can be chained with the flow operator. Most effects require `use "@audio"`.

## Reverb

Simulates room acoustics using a Schroeder reverb algorithm:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)

Note: Simple reverb (roomSize)
Buffer wet = (reverb tone 0.5)

Note: Full control (roomSize, damping, mix)
Buffer wetFull = (reverb tone 0.7 0.3 0.5)
```

| Parameter | Range | Description |
|-----------|-------|-------------|
| `roomSize` | 0.0-1.0 | Size of simulated room (0=small, 1=large) |
| `damping` | 0.0-1.0 | High-frequency absorption (default 0.5) |
| `mix` | 0.0-1.0 | Dry/wet mix ratio (default 0.3) |

## Filters

### Low-pass Filter

Passes frequencies below the cutoff, attenuates higher frequencies:

```flow
Buffer lp = (lowpass tone 800.0)  Note: cutoff at 800 Hz
```

### High-pass Filter

Passes frequencies above the cutoff, attenuates lower frequencies:

```flow
Buffer hp = (highpass tone 200.0)  Note: cutoff at 200 Hz
```

### Band-pass Filter

Passes frequencies within a range:

```flow
Buffer bp = (bandpass tone 200.0 2000.0)  Note: 200-2000 Hz band
```

## Compressor

Reduces dynamic range by attenuating loud signals:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)

Note: Simple compression (threshold in dB, ratio)
Double negThreshold = (sub 0.0 12.0)
Buffer comp = (compress tone negThreshold 4.0)

Note: Full control (threshold, ratio, attack ms, release ms)
Buffer compFull = (compress tone negThreshold 4.0 5.0 50.0)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `threshold` | dB | Level above which compression kicks in (typically negative) |
| `ratio` | N:1 | Compression ratio (4.0 = 4:1 compression) |
| `attack` | ms | How quickly compressor reacts (default varies) |
| `release` | ms | How quickly compressor releases (default varies) |

## Delay

Feedback delay effect:

```flow
Buffer delayed = (delay tone 250.0 0.4 0.5)
```

| Parameter | Unit | Description |
|-----------|------|-------------|
| `time` | ms | Delay time in milliseconds |
| `feedback` | 0.0-1.0 | Amount of delayed signal fed back (0=single echo) |
| `mix` | 0.0-1.0 | Dry/wet mix ratio |

## Gain

Apply gain in decibels:

```flow
Double negSix = (sub 0.0 6.0)
Buffer quieter = (gain tone negSix)    Note: -6dB (half amplitude)
Buffer louder = (gain tone 6.0)        Note: +6dB (double amplitude)
Buffer same = (gain tone 0.0)          Note: 0dB (no change)
```

## Fade In / Fade Out

Linear amplitude fades:

```flow
Buffer faded = tone -> fadeIn 0.5    Note: 0.5 second fade-in
Buffer fadedOut = tone -> fadeOut 0.5 Note: 0.5 second fade-out
```

The parameter is the fade duration in seconds.

## Effect Chaining

Effects chain naturally with the flow operator:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)

Double negThree = (sub 0.0 3.0)
Buffer processed = tone -> lowpass 1000.0 -> reverb 0.3 -> gain negThree
```

This reads left-to-right: filter first, then add reverb, then reduce gain.

### Longer Chain

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
                -> fadeIn 0.3
                -> fadeOut 0.5
        }
    }
}
```

## Working with Negative Values

Flow doesn't have negative literal syntax. Use `(sub 0 N)` or `(sub 0.0 N)` to create negative values for thresholds and gain:

```flow
use "@std"

Double negTwelve = (sub 0.0 12.0)   Note: -12.0 dB threshold
Double negSix = (sub 0.0 6.0)       Note: -6.0 dB gain
```

## Effect Reference

| Effect | Signature | Description |
|--------|-----------|-------------|
| `reverb` | `(Buffer, Double) -> Buffer` | Reverb with room size |
| `reverb` | `(Buffer, Double, Double, Double) -> Buffer` | Reverb with room/damping/mix |
| `lowpass` | `(Buffer, Double) -> Buffer` | Low-pass filter (cutoff Hz) |
| `highpass` | `(Buffer, Double) -> Buffer` | High-pass filter (cutoff Hz) |
| `bandpass` | `(Buffer, Double, Double) -> Buffer` | Band-pass filter (low/high Hz) |
| `compress` | `(Buffer, Double, Double) -> Buffer` | Compressor (threshold, ratio) |
| `compress` | `(Buffer, Double, Double, Double, Double) -> Buffer` | Full compressor |
| `delay` | `(Buffer, Double, Double, Double) -> Buffer` | Delay (time/feedback/mix) |
| `gain` | `(Buffer, Double) -> Buffer` | Gain in dB |
| `fadeIn` | `(Buffer, Double) -> Buffer` | Fade-in (duration seconds) |
| `fadeOut` | `(Buffer, Double) -> Buffer` | Fade-out (duration seconds) |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation and synthesis
- [Flow Operator](Flow-Operator.md) - Chaining with `->`
- [Playback and Export](Playback-and-Export.md) - Playing and saving processed audio
