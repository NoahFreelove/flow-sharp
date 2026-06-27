# Visualization

Flow can print ASCII visualizations of sequences and buffers to stdout. These are quick debugging aids — not a replacement for a real DAW view, but useful when iterating on rhythms, melodies, envelope shapes, and DSP behaviour.

Flow ships three side-effect printers:

| Function | Purpose |
|----------|---------|
| `visualize` | ASCII piano-roll (for `Sequence`) or 80×20 ASCII waveform (for `Buffer`) |
| `prettyBuffer` | Multi-line buffer summary (frames, channels, sample rate, duration, peak, RMS) plus a compact 60×11 waveform |
| `bufferHex` | Classic xxd-style hex dump of the buffer's underlying float samples (little-endian IEEE-754) |

All three return `Void` and never throw — empty inputs print `(empty sequence)` / `(empty buffer)` and continue.

## Visualizing a Sequence

Render an ASCII piano-roll-style grid:

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence melody = | C4q D4q E4q F4q |
            (visualize melody)
        }
    }
}
```

The output shows note pitches on the vertical axis and beat positions on the horizontal axis. Notes render as `#` runs; longer notes span more columns; rests appear as gaps; bar boundaries appear as `|` separators.

### Flow-operator style

`visualize` works as a pipe target:

```flow
tempo 120 {
    timesig 4/4 {
        | G4h E4h | -> visualize
    }
}
```

### Sequences with rests

Rests are rendered as empty columns:

```flow
tempo 120 {
    timesig 4/4 {
        Sequence withRests = | C4q _ E4q _ |
        (visualize withRests)
    }
}
```

## Visualizing a Buffer

`visualize` on a buffer prints an 80-column, 20-row ASCII waveform with `±1.0` row labels on the left:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)
(visualize tone)
```

Stereo buffers are downmixed to mono for the display. This is handy for sanity-checking envelope shapes, fades, and effect chains.

## Pretty Buffer Summary — `prettyBuffer`

`prettyBuffer` is `visualize`'s more detailed cousin: it prints a metadata header plus a compact 60×11 waveform that fits comfortably in a terminal alongside the header.

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)
(prettyBuffer tone)
```

Sample output:

```
Buffer:
  frames      : 22050
  channels    : 2 (stereo)
  sample rate : 44100 Hz
  duration    : 0.500 s
  peak        : 0.5000 (-6.02 dBFS)
  rms         : 0.3536  (-9.03 dBFS)
|                                                            |
|************************************************************|
|************************************************************|
|************************************************************|
|                                                            |
```

> Note: `use "@audio"` shadows the C# builtin `createSineTone` with a Flow proc that promotes to stereo, so `prettyBuffer` reports `channels: 2 (stereo)`. If you import only the C# builtins (no `use "@audio"`), the output would show `channels: 1 (mono)`.

Peak and RMS are reported both in linear amplitude and in dBFS, with `-inf` shown for silent buffers (floor at 1e-12 ≈ -240 dBFS).

## Hex Dump — `bufferHex`

`bufferHex` reinterprets the buffer's float samples as little-endian IEEE-754 32-bit bytes and prints them in classic xxd format: 8-digit offset, 16 bytes per row split into two columns of 8, ASCII gutter at the right, and a trailing end-offset line for easy length read-off.

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.01 0.5)
(bufferHex tone)
```

Use the slice overload to focus on a region:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.01 0.5)
Note: dump 64 bytes starting at byte offset 256
(bufferHex tone 256 64)
```

Out-of-range or negative `offset` / `length` values are silently clamped (per Flow's charitable-interpretation philosophy) — `bufferHex` never throws on bad slice arguments. An offset past the end of the buffer prints `(empty slice)`.

## Common Debugging Flow

Use the printers alongside `print` and `str` to understand what your code produces:

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence mel = | C4 D4 E4 F4 | -> humanize 0.2
            (print (str mel))
            (visualize mel)
        }
    }
}
```

For audio diagnosis, `prettyBuffer` is usually the first stop:

```flow
use "@std"
use "@audio"

Buffer raw  = (createSineTone 440Hz 1.0 0.8)
Buffer wet  = raw -> (reverb 2.5) -> (gain -3dB)
(prettyBuffer wet)         Note: header + waveform
(bufferHex wet 0 128)      Note: first 128 bytes if something looks wrong
```

## Limits

- The piano-roll grid is coarse: microtones, cents, and dynamics are not shown — the goal is a structural overview.
- Very long sequences wrap or truncate depending on terminal width.
- For polyphonic passages (Phase 28 voice blocks), overlapping notes may share rows.
- `visualize` (buffer) downmixes stereo to mono; `prettyBuffer` does the same for its waveform but reports the true channel count in the header.

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `visualize` | `(Sequence) -> Void` | ASCII piano-roll grid |
| `visualize` | `(Buffer) -> Void` | 80×20 ASCII waveform |
| `prettyBuffer` | `(Buffer) -> Void` | Header (frames / channels / sample rate / duration / peak dBFS / RMS dBFS) + 60×11 waveform |
| `bufferHex` | `(Buffer) -> Void` | Full hex dump of underlying float samples |
| `bufferHex` | `(Buffer, Int offset, Int length) -> Void` | Slice (negative / out-of-range values silently clamped) |

## See Also

- [Note Streams](Note-Streams.md) — creating sequences
- [Audio and Synthesis](Audio-and-Synthesis.md) — creating buffers
- [Tips and Tricks](Tips-and-Tricks.md) — other debugging idioms
