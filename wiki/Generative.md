# Generative Music

Flow ships a broad palette of generative primitives — from classic Euclidean rhythms and weighted random choice to Markov chains, L-systems, cellular automata, chaos maps, Tidal-style pattern combinators, and chord-aware Markov improvisation. Every stochastic primitive is deterministic when seeded, and unseeded calls route through a per-render PRNG registry so two consecutive renders of the same source produce byte-identical output.

This page is a tour of the algorithmic surface. For deterministic transforms (transpose, invert, retrograde, etc.) see [Pattern Transforms](Pattern-Transforms.md). For the standard-library module index see [Standard Library](Standard-Library.md).

## At a Glance

| Surface | Import | Highlights |
|---------|--------|------------|
| Euclidean, `vary`, `humanize`, random choice | `use "@std"` + `use "@composition"` | Always-on basics — no extra import |
| Tidal-style combinators | `use "@patterns"` | 13 combinators on `Sequence` (`every`, `fast`, `slow`, `jux`, `sometimes`, …) |
| Markov / L-system / cellular / chaos | `use "@generative"` | Algorithmic generators returning `Sequence` or `Double[]` |
| `jam` chord-aware improv + style packs | `use "@improv"` | Style-pack-driven Markov, composer-editable rule packs |

## PRNG and Determinism

All stochastic primitives in `@patterns`, `@generative`, and `@improv` thread their randomness through `Runtime/PrngRegistry`, keyed by `(source location, generator name)`. The registry is reseeded at every `renderSong` / `writeWav` boundary using a stable, platform-independent FNV-1a seed derivation. That gives composers two contracts:

- **Two consecutive runs at the same git SHA produce byte-identical output.** This is the project-wide "two-run cmp-clean" determinism contract.
- **Explicit `seed` arguments still work the way you'd expect.** Passing a seed bypasses the registry and constructs a local `Random(seed)` directly.

One important caveat: the chaos primitives (`lorenz`, `logistic`) are forward-Euler chaotic systems. Their chained floating-point arithmetic amplifies platform-specific FPU and `Math.*` quirks beyond ~50 iterations. Same-platform determinism is preserved; cross-platform reproducibility is **not** guaranteed for chaos outputs. Markov, L-system, and cellular automata all use integer arithmetic and stay cross-platform deterministic.

## Euclidean Rhythms

Distribute `k` hits evenly across `n` steps using the Bjorklund algorithm:

```flow
use "@std"

Sequence e38 = (euclidean 3 8 C4)    Note: X..X..X. — Cuban tresillo
Sequence e58 = (euclidean 5 8 E4)    Note: X.XX.XX. — Cinquillo / West African bell
```

**Signatures**

| Signature | Notes |
|-----------|-------|
| `(euclidean Int hits, Int steps, Note pitch) -> Sequence` | Plain pattern |
| `(euclidean Int, Int, Note, Double swing) -> Sequence` | On-beat / off-beat velocity accent |
| `(euclidean Int, Int, Note, Double swing, Double humanize, Int seed) -> Sequence` | Seeded uniform velocity jitter |

Common Euclidean patterns map to global rhythmic traditions:

| `hits / steps` | Feel |
|----------------|------|
| 3/8 | Cuban tresillo |
| 5/8 | West African bell pattern |
| 3/4 | Simple triplet |
| 7/16 | Afro-Cuban bembé |

## Random Choice in Note Streams

Pick a note at random from a set. This is *syntax*, not a function — it works only inside `| ... |` note streams. See [Note Streams](Note-Streams.md) for the surrounding context.

### Uniform Random — `(? ...)`

```flow
timesig 4/4 {
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |
}
```

### Weighted Random — `(? a:50 b:30 c:20)`

Weights are relative ratios, not percentages: `:50 :30 :20` is identical to `:5 :3 :2`.

### Seeded Random — `(?? ...)`

`(??)` is a second, separately seeded RNG. Use `(??set N)` to seed it and `(??reset)` to reset to the initial state.

```flow
(??set 42)
timesig 4/4 {
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |
}
```

Rests (`_`) are valid options anywhere a pitch is.

## Humanize

| Function | Signature | Notes |
|----------|-----------|-------|
| `humanize` | `(Sequence, Double) -> Sequence` | Uniform velocity jitter, non-deterministic shared RNG (frozen by design) |
| `humanizeGaussian` | `(Sequence, Double, Int seed) -> Sequence` | Box-Muller normal-distribution jitter, seeded, recurses into voice blocks |

```flow
Sequence mel = | C4q D4q E4q F4q |
Sequence loose  = (humanize mel 0.3)               Note: uniform jitter
Sequence tight  = (humanizeGaussian mel 0.15 42)   Note: tighter normal curve, reproducible
```

`humanizeGaussian` is the preferred surface for anything that needs to round-trip identically: it accepts a seed, clamps velocity to `[0.05, 1.0]`, and walks into Phase 28 voice blocks (`| {voice ...} {voice ...} |`) so polyphonic passages stay coherent.

## Sequence Mutation — `vary`

`vary` applies random mutations to a sequence. You can pick which dimension to mutate (pitch, rhythm, rest, velocity), pass a seed for reproducibility, and constrain pitch mutations to a key.

```flow
Sequence s = | C4 D4 E4 F4 G4 |

Sequence v1 = s -> vary(0.3)                          Note: random mutation type
Sequence v2 = (vary s 0.5 "pitch")
Sequence v3 = (vary s 0.5 "rhythm")
Sequence v4 = (vary s 0.5 "rest")
Sequence v5 = (vary s 0.5 "velocity")
Sequence v6 = (vary s 0.5 42)                         Note: seeded random type
Sequence v7 = (vary s 0.5 "pitch" 42)                 Note: seeded + type
Sequence v8 = (vary s 0.5 "pitch" "Cmajor")           Note: diatonic
Sequence v9 = (vary s 0.5 "pitch" "Cmajor" 42)        Note: diatonic + seed
```

**Overloads**

| Signature | Description |
|-----------|-------------|
| `(Sequence, Double) -> Sequence` | Random mutation type |
| `(Sequence, Double, String) -> Sequence` | Specific type |
| `(Sequence, Double, Int) -> Sequence` | Seeded random type |
| `(Sequence, Double, String, Int) -> Sequence` | Seeded, specific type |
| `(Sequence, Double, String, String) -> Sequence` | Diatonic (type, key) |
| `(Sequence, Double, String, String, Int) -> Sequence` | Diatonic, seeded |

**Mutation types**: `"pitch"`, `"rhythm"`, `"rest"`, `"velocity"`.

## Tidal-Style Pattern Combinators

```flow
use "@patterns"
```

Thirteen combinators that operate on `Sequence` values. They borrow their semantics from [TidalCycles](https://tidalcycles.org), with one Flow-native twist: the cycle unit is **bars**, not beats. Transform-argument combinators are **lambda-required** — you pass `(fn Sequence s => (fast s 2.0))`, not a partially-applied function name.

Every combinator is **charitable** on degenerate input: zero / negative factors, NaN offsets, empty sequences, probabilities outside `[0, 1]` all return the input unchanged with a one-shot stderr advisory. They never throw.

### Deterministic combinators

| Combinator | Signature | What it does |
|------------|-----------|--------------|
| `every` | `(Int n, Function cb, Sequence seq) -> Sequence` | Apply `cb` to bar `i` whenever `i % n == 0` |
| `fast` | `(Sequence seq, Double factor) -> Sequence` | Shorten each note by `factor` (2.0 = halve durations) |
| `slow` | `(Sequence seq, Double factor) -> Sequence` | Lengthen each note by `factor` |
| `chunk` | `(Int n, Function cb, Sequence seq) -> Sequence` | Apply `cb` to one 1/Nth chunk per call; rotates which chunk on successive invocations |
| `phase` | `(Double offset, Sequence seq) -> Sequence` | Rotate bar order by `round(offset × seq.Bars.Count)` |
| `rev` | `(Sequence seq) -> Sequence` | Reverse bar order (within-bar note order preserved — compare to `retrograde`) |
| `iter` | `(Int n, Sequence seq) -> Sequence` | Rotate note list by `totalNotes / n` positions |
| `palindrome` | `(Sequence seq) -> Sequence` | `[A B C] -> [A B C C B A]` |
| `jux` | `(Function cb, Sequence seq) -> Sequence` | Layer original with `cb(seq)` as a voice block (mono mix today; L/R stereo placement planned) |
| `superimpose` | `(Function cb, Sequence seq) -> Sequence` | Mono voice-block overlay; functionally identical to `jux` in current builds |

### Stochastic combinators (PRNG-routed)

| Combinator | Signature | What it does |
|------------|-----------|--------------|
| `sometimes` | `(Double prob, Function cb, Sequence seq) -> Sequence` | Apply `cb` to each bar with probability `prob` |
| `sometimes` | `(Function cb, Sequence seq) -> Sequence` | Convenience overload at `prob = 0.5` |
| `degrade` | `(Sequence seq) -> Sequence` | Drop each bar with fixed 50% probability (Tidal compat) |
| `sparseSeq` | `(Double prob, Sequence seq) -> Sequence` | Drop each bar with composer-controlled probability |

### Composing combinators

> **Note:** Tidal combinators take the `Sequence` as their **last** argument, so the `->` pipe operator (which inserts its left-hand value as the **first** argument) does not work with `every`, `sometimes`, `jux`, `chunk`, or `superimpose`. Use explicit nesting instead:

```flow
use "@std"
use "@patterns"

tempo 120 {
    timesig 4/4 {
        Sequence base = | C4 D4 E4 F4 |
        Sequence pat  = (jux
            (fn Sequence s => (transpose s +7st))
            (sometimes 0.3
                (fn Sequence s => (rev s))
                (every 4
                    (fn Sequence s => (fast s 2.0))
                    base)))
    }
}
```

See `examples/generative/tidal_combinators.flow` for a runnable tour.

## Markov Chains

```flow
use "@generative"
```

A Markov chain models note-to-note transition probabilities from a training corpus. Flow ships both a one-shot form and a train-once / generate-many split so you can reuse a trained model.

### One-shot

```flow
Sequence corpus = | C4 D4 E4 F4 G4 A4 G4 F4 E4 D4 C4 |
Sequence gen    = (markov corpus 2 16)            Note: unseeded — PRNG-routed
Sequence gen2   = (markov corpus 2 16 42)         Note: explicit seed
```

| Signature | Notes |
|-----------|-------|
| `(markov Sequence corpus, Int order, Int length) -> Sequence` | Unseeded; PRNG via the registry |
| `(markov Sequence corpus, Int order, Int length, Int seed) -> Sequence` | Explicit seed |

The corpus doesn't have to be a note-stream `Sequence` — a `Note[]` or an `Int[]` (MIDI / scale-degree numbers) works too, so you can train straight off a `(list ...)`:

```flow
Note[] notes = (list C4 D4 E4 F4 G4 F4 E4 D4 C4)
Sequence fromNotes = (markov notes 2 16 42)

Int[] midi = (list 60 62 64 65 67 65 64 62 60)
Sequence fromInts = (markov midi 2 16 42)
```

### Train + generate split

```flow
MarkovModel model = (markovTrain corpus 2)
Sequence run1 = (markovGenerate model 16)
Sequence run2 = (markovGenerate model 16 42)
```

| Signature | Notes |
|-----------|-------|
| `(markovTrain Sequence corpus, Int order) -> MarkovModel` | Defaults to `features=#pitch`; corpus may also be `Note[]` or `Int[]` |
| `(markovTrain ..., Symbol features) -> MarkovModel` | `features=#pitch` (default) or use named-arg form for tuple features |
| `(markovGenerate MarkovModel model, Int length) -> Sequence` | Unseeded |
| `(markovGenerate MarkovModel model, Int length, Int seed) -> Sequence` | Explicit seed |
| `(markovEqual MarkovModel a, MarkovModel b) -> Bool` | Structural compare. `(eq m1 m2)` is reference identity — independently trained models compare unequal. |

### Feature extraction

By default, each state in the chain is a raw MIDI pitch. Use the named-arg `features=` form to capture richer state:

```flow
MarkovModel pitchOnly = (markovTrain corpus 2)
MarkovModel withDur   = (markovTrain corpus 2 features=<<#pitch, #duration>>)
```

The tuple form encodes both pitch and quarter-note duration into a single state int. This gives higher fidelity at the cost of a sparser transitions table.

### Charitable interpretation

- **Order is clamped to `[1, 3]`.** Order 5 -> order 3 + advisory.
- **Empty corpus or non-positive length** -> empty sequence + advisory.
- **First `order` notes are alphabet-seeded** so the cold start is deterministic.

See `examples/generative/markov_jazz.flow` for a runnable jazz-corpus walkthrough.

## L-Systems (Lindenmayer)

Pure deterministic Symbol rewriting. Useful for fractal-like melodic structures.

```flow
use "@std"
use "@notation"
use "@generative"

Dict<Symbol, Symbol[]> rules = (dict
    #A (list #A #B)
    #B (list #A))

Symbol[] expanded = (lsystem #A rules 5)

Note: bridge to musical Sequence — mapper must return a MusicalNote (not a bare Note)
Sequence mel = (lsystemToSequence expanded
    (fn Symbol s => (if (equals s #A) (createMusicalNote C4 2) (createMusicalNote E4 2))))
```

| Signature | Notes |
|-----------|-------|
| `(lsystem Symbol axiom, Dict rules, Int iterations) -> Symbol[]` | One-shot |
| `(lsystemModel Symbol axiom, Dict rules) -> LsystemModel` | Train |
| `(lsystemGenerate LsystemModel, Int iterations) -> Symbol[]` | Generate |
| `(lsystemToSequence Symbol[], Function mapper) -> Sequence` | Map symbols to notes; mapper must return a `MusicalNote` via `(createMusicalNote pitch duration)` |
| `(lsystemEqual LsystemModel a, LsystemModel b) -> Bool` | Structural compare |

**Iteration count is clamped to `[0, 20]`** as a DoS guard — at iteration 20 the alphabet has already grown past 10^6 symbols, well beyond any musical use. Terminal symbols (symbols that aren't rule keys) pass through unchanged each iteration — standard Lindenmayer semantics.

## Cellular Automata

```flow
use "@generative"

Note: 1D Wolfram-style — Rule 30, width 16, 8 steps
Sequence rule30 = (cellular 30 16 8 0)

Note: 1D with explicit seed pattern
Bool[] initial = (list false false false true true false false false)
Sequence custom = (cellularSeeded 30 8 8 0 initial)

Note: 2D Conway's Game of Life — 8 wide, 8 tall, 16 steps, seed 1
Sequence[] life = (life 8 8 16 1)
```

| Signature | Notes |
|-----------|-------|
| `(cellular Int rule, Int width, Int steps, Int seed) -> Sequence` | 1D elementary CA, Wolfram-canonical single-1-center initial; `seed` is accepted but ignored |
| `(cellularSeeded Int rule, Int width, Int steps, Int seed, Bool[] initial) -> Sequence` | Explicit initial pattern |
| `(life Int width, Int height, Int steps, Int seed) -> Sequence[]` | 2D Conway with wrap-around; seeded fill at 30% density |

Per-dimension cap of 1024 (DoS guard). Rule values outside `[0, 255]` wrap via `(rule & 0xFF)` with an advisory. The 1D grid maps to one bar per step: live cells become C4 notes, dead cells become rests. The 2D `life` grid returns one `Sequence` per row, with higher row indices mapped to lower pitches (so visually the "top" of the grid corresponds to the top of a piano roll).

## Chaos Maps

```flow
use "@generative"

Double[] traj    = (lorenz 10.0 28.0 2.667 256 42)
Double[] series  = (logistic 3.9 256 42)

Note: bridge raw values into a Sequence
Sequence quantized1 = (quantizeToScale traj "Cmajor")
Sequence quantized2 = (quantizeToScale series (list C4 D4 E4 G4 A4))
```

| Signature | Notes |
|-----------|-------|
| `(lorenz Double σ, Double ρ, Double β, Int length, Int seed) -> Double[]` | Forward-Euler integration; returns the x-axis trajectory |
| `(logistic Double r, Int length, Int seed) -> Double[]` | `x_{n+1} = r × x_n × (1 - x_n)`, values in `[0, 1]` |
| `(quantizeToScale Double[], String scaleName) -> Sequence` | Normalize to `[0, 1]`, snap to the named scale, emit quarter notes |
| `(quantizeToScale Double[], Note[] scaleNotes) -> Sequence` | Same, with an explicit note set |

**Important determinism caveat:** as noted at the top of this page, chaos outputs are same-platform deterministic only. Don't pin cross-platform fixtures against chaos primitives. Bad params (Lorenz σ ≤ 0, logistic r outside `[0, 4]`) fall back to canonical butterfly / clamp with a one-shot advisory; lengths above 100,000 are clamped.

## Improv — `jam`

```flow
use "@improv"
```

`jam` generates a chord-aware Markov melody over a sequence of chords. Chord tones land on strong beats, scale tones on weak beats, chromatic passing tones via per-style weighted roulette.

```flow
use "@std"
use "@improv"

key Cmajor {
    Sequence chords = | Cmaj7 Am7 Dm7 G7 |

    Note: only `over` is required
    Sequence solo1 = (jam chords)

    Note: any subset of named args, in any order — including key= and seed=
    Sequence solo2 = (jam over=chords style=#blues length=16 key="Cmajor" seed=7)

    Note: the fully-positional form still works: (jam over style length key seed order)
    Sequence solo3 = (jam chords #blues 16 "Cmajor" 7)
}
```

**Signature**

```
jam(Sequence over,
    Symbol style = #jazz,
    Int length = 8,
    String key = (active key),
    Int seed = (PrngRegistry-routed),
    Int order = 2) -> Sequence
```

Only `over` is required. Every other parameter accepts a **named argument** — `style=`, `length=`, `key=`, `seed=`, `order=` — and you can pass any subset in any order; the rest fall back to their defaults. So `(jam over=chords style=#blues seed=7)` works without supplying `length` or `key`. The fully-positional form (`(jam chords #jazz 8 "Cmajor" 42)`) is still valid. The `key=` override pushes a synthetic musical-context frame for the jam, then pops — useful for chromatic pivot bars that break the surrounding key. The `order` argument is clamped to `[1, 3]` just like `markov`.

### Style packs

Style packs are **musical content**, not engine internals — they live in composer-editable `.flow` files. Flow ships three baselines:

| Style | Pack | Character |
|-------|------|-----------|
| `#jazz` | `flow-lang/improv/styles/jazz.flow` | Bebop-leaning weighting, more scale + chromatic-passing motion |
| `#blues` | `flow-lang/improv/styles/blues.flow` | Pentatonic-leaning, blues-scale chromatics |
| `#classical` | `flow-lang/improv/styles/classical.flow` | Heavier chord-tone bias, less chromatic |

User packs live at `~/.config/flow/styles/*.flow` and override shipped packs on Symbol-name collision (last-write-wins, with a one-shot stderr advisory when an override happens). Run `(listStyles)` from any Flow script to audit what's registered in the current process.

**Style registry surface**

| Function | Signature | Notes |
|----------|-----------|-------|
| `registerStyle` | `(Symbol name, Dict pack) -> Void` | Register or replace a style pack |
| `listStyles` | `() -> Symbol[]` | All currently registered style names, insertion order |

A minimal pack looks like:

```flow
use "@improv"

(registerStyle #mystyle
  (dict
    #beat_weights (dict
      #strong (dict #chord_tone 0.70 #scale_tone 0.20 #chromatic_passing 0.10)
      #weak   (dict #chord_tone 0.30 #scale_tone 0.50 #chromatic_passing 0.20))
    #interval_transitions (dict
      #step_up 0.30  #step_down 0.30
      #leap_up 0.10  #leap_down 0.15
      #chromatic 0.10  #repeat 0.05)
    #rhythmic_template <<#eighth #eighth #eighth #eighth #eighth #eighth #eighth #eighth>>
    #articulation_distribution (dict
      #downbeat   #legato
      #offbeat    #accent
      #syncopated #marcato)))
```

The full Dict-shape contract — every required field and its semantics — is documented at `flow-lang/improv/styles/README.md`.

### Charitable behaviour

Unknown style -> falls back to `#jazz` + advisory. Empty `over` or `length <= 0` -> empty Sequence + advisory. Style + key incompatibility (e.g. `#blues` over a chromatic key) is a soft advisory, not an error — Flow keeps producing music.

## Polyrhythms

Overlay two sequences with different time signatures. `polyrhythm` figures out the cycle length (LCM of time signatures) and returns a mixed buffer.

```flow
use "@std"
use "@audio"
use "@composition"

tempo 120 {
    timesig 3/4 {
        Sequence waltz = | A3 E4 E4 |
        timesig 4/4 {
            Sequence quarters = | C4 C4 C4 C4 |
            Buffer poly = (polyrhythm waltz quarters)
            (writeWav "polyrhythm.wav" poly)
        }
    }
}
```

| Signature | Notes |
|-----------|-------|
| `(polyrhythm Sequence, Sequence) -> Buffer` | Auto-align via LCM of time signatures; requires `use "@composition"` |
| `(polyrhythm Sequence, Sequence, Int) -> Buffer` | Explicit beat count override |

## Microtonal / Tuning

Cent offsets and named tunings are part of the generative toolkit when you're exploring non-12-TET soundworlds. Briefly:

- **Cent offsets in note streams**: `| C4 C4+50c C4-25c |`
- **Named-tuning pragmas** (file-scoped, last-wins): `enable justIntonation;`, `enable pythagorean;`, `enable equalTemperament;`
- **Scala `.scl` loader**: `Tuning t = (loadScala "examples/scala/22-shree.scl")`; 2-arg form `(loadScala "x.scl" "x.kbm")` overrides the keyboard mapping.
- **`tuning <expr> { ... }` musical-context block**: three composer surfaces — identifier-bound (`tuning partch { ... }`), inline call (`tuning (loadScala "x.scl") { ... }`), string-literal sugar (`tuning "x.scl" { ... }`).

See `examples/scala/intro.flow` for a runnable tutorial chapter and [Musical Context](Musical-Context.md) for context-block semantics.

## Combining Techniques

Generative primitives compose cleanly:

```flow
use "@std"
use "@audio"
use "@patterns"
use "@generative"
use "@improv"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: a Euclidean hi-hat
            Sequence hat = (euclidean 5 8 C5)

            Note: chord progression for jam — positional: (jam over style length key seed)
            Sequence chords = | Cmaj7 Am7 Dm7 G7 |
            Sequence lead = (jam chords #jazz 8 "Cmajor" 7)

            Note: roll dice on the lead each cycle — combinators take Sequence last, so use nesting
            Sequence shaped = (jux
                (fn Sequence s => (transpose s +7st))
                (sometimes 0.3
                    (fn Sequence s => (rev s))
                    lead))

            section groove {
                Sequence a = hat
                Sequence b = shaped
            }
            Song song = [groove*4]
            Buffer output = (renderSong song "piano")
            (writeWav "generative.wav" output)
        }
    }
}
```

## See Also

- [Note Streams](Note-Streams.md) — `(? ...)` / `(?? ...)` and full note-stream syntax
- [Pattern Transforms](Pattern-Transforms.md) — deterministic transforms (`transpose`, `invert`, `retrograde`, …)
- [Chords and Harmony](Chords-and-Harmony.md) — scales used by `quantizeToScale` and the diatonic `vary` overloads
- [Musical Context](Musical-Context.md) — `tuning`, `key`, `swing`, `tempo` blocks
- [Standard Library](Standard-Library.md) — module index, including `@patterns`, `@generative`, `@improv`
- [Visualization](Visualization.md) — `visualize`, `prettyBuffer`, `bufferHex` for sanity-checking generative output
- Runnable examples: `examples/generative/markov_jazz.flow`, `examples/generative/tidal_combinators.flow`, `examples/sections/parameterized.flow`, `examples/scala/intro.flow`
