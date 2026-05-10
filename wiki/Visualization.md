# Visualization

Flow can print simple ASCII visualizations of sequences and buffers to stdout. These are quick debugging aids — not a replacement for a real DAW view, but useful when iterating on rhythms, melodies, and envelope shapes.

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

The output shows note pitches on the vertical axis and beat positions on the horizontal axis. Rests appear as gaps; longer notes appear as wider horizontal bars.

### Flow-Operator Style

`visualize` works as a pipe target:

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        | G4h E4h | -> visualize
    }
}
```

### Sequences With Rests

Rests are rendered as empty columns:

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        Sequence withRests = | C4q _ E4q _ |
        (visualize withRests)
    }
}
```

## Visualizing a Buffer

You can also print a simple ASCII waveform for an audio buffer:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)
(visualize tone)
```

This shows amplitude over time, downsampled to fit the terminal width. It's handy for sanity-checking envelope shapes, fades, and effect chains.

## Common Debugging Flow

Use `visualize` alongside `print` and `str` to understand what your code produces:

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

Run this once and you see the numeric representation and a rough shape side-by-side.

## Limits

- The grid is coarse: microtones, cents, and dynamics are not shown — the goal is a structural overview.
- Very long sequences wrap or truncate depending on terminal width.
- For polyphonic passages, overlapping notes may share rows.

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `visualize` | `(Sequence) -> Void` | ASCII piano-roll grid |
| `visualize` | `(Buffer) -> Void` | ASCII waveform |

## See Also

- [Note Streams](Note-Streams.md) - Creating sequences
- [Tips and Tricks](Tips-and-Tricks.md) - Other debugging idioms
