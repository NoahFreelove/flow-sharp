# Flow Style Packs — Composer Contract

Phase 36 Plan 36-11 / D-36-12 ships the `@improv` stdlib's chord-aware Markov
improvisation API (`jam`). Style packs are **musical content** living in
composer-editable `.flow` files — NOT engine internals. This file documents the
Dict shape contract every pack must follow.

## Load order (Pitfall 8)

At every `FlowEngine` init, `StyleRegistry.LoadAtEngineInit` scans two
directories in this fixed order:

1. **Shipped packs:** `flow-lang/improv/styles/*.flow` — ships `jazz.flow`,
   `blues.flow`, `classical.flow` in v1.5.
2. **User packs:** `~/.config/flow/styles/*.flow` — your own packs.

Last-write-wins: a user pack with the same `(registerStyle #name pack)` Symbol
as a shipped pack **overrides** the shipped version. When that happens, a
one-shot stderr advisory fires:

```
[improv] user style '#jazz' overrides shipped pack
```

The advisory is per-process — re-running the same script does NOT spam it
again. Run `(listStyles)` from any Flow script to audit which styles are
registered in the current process.

## File layout

Every style pack is a `.flow` file that:

1. Imports `@improv` (so `registerStyle` is in scope).
2. Calls `(registerStyle #name (dict ...))` with the rule-pack dict inline.

The inline-dict form sidesteps Flow's variable-declaration type annotations
(the pack's outer dict has heterogeneous values — Dict, Tuple, Dict-of-Symbol).
If you want to extract the dict to a top-level variable for inspection, use
the inline form first then split it out once you settle on a more
homogeneous shape.

Minimal skeleton:

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

## Dict shape — every field

### `#beat_weights` (required)

Outer dict maps beat-strength Symbols to inner weight dicts. The Phase 36 beat
classifier (in `JamFunctions.cs`) assigns one of three strength tags per beat
position within a bar:

| Strength      | When (4/4 time)              |
| ------------- | ---------------------------- |
| `#strong`     | beats 1, 3 (downbeats)       |
| `#weak`       | beats 2, 4 (backbeats)       |
| `#syncopated` | offbeat eighths (the "and"s) |

The inner dict assigns three probability weights summing implicitly (the
implementation normalizes — the absolute values' ratios are what matter):

| Key                 | Meaning                                                                            |
| ------------------- | ---------------------------------------------------------------------------------- |
| `#chord_tone`       | weight for picking a note from the currently-active chord's `ChordData.NoteNames`  |
| `#scale_tone`       | weight for picking a non-chord-tone note that's in the active key's scale          |
| `#chromatic_passing`| weight for picking a chromatic passing tone (any note not in the active scale)     |

If `#syncopated` is missing, the implementation falls back to `#weak`'s weights.
If a weight key is missing, it's treated as 0.0 (charitable — the others still
sum and roulette-pick).

### `#interval_transitions` (required)

Maps interval-direction Symbols to weights. When the implementation needs to
pick a note within a category (chord_tone / scale_tone / chromatic_passing), it
biases the choice by the interval from the previously-generated note:

| Key           | Direction                                        |
| ------------- | ------------------------------------------------ |
| `#step_up`    | +1 or +2 semitones                               |
| `#step_down`  | −1 or −2 semitones                               |
| `#leap_up`    | +3..+12 semitones                                |
| `#leap_down`  | −3..−12 semitones                                |
| `#chromatic`  | exactly ±1 semitone (passing tone)               |
| `#repeat`     | same pitch as the previous note                  |

Missing keys are treated as 0.0.

### `#rhythmic_template` (required)

A `Tuple<<Symbol, ...>>` of note-value symbols (`#whole`, `#half`, `#quarter`,
`#eighth`, `#sixteenth`, `#thirtysecond`) — one entry per beat position in the
bar. v1.5 locks an 8-entry eighth-note template
(`<<#eighth #eighth #eighth #eighth #eighth #eighth #eighth #eighth>>`); future
revisions may accept mixed-duration templates.

The template's length determines how many notes per bar the jam emits.

### `#articulation_distribution` (required)

Maps beat-strength Symbols to articulation Symbols. The implementation tags
each generated note with the articulation specified for its beat strength:

| Symbol Value     | Phase 28 enum  | Effect                                                          |
| ---------------- | -------------- | --------------------------------------------------------------- |
| `#legato`        | `Articulation.Legato`     | extended duration with crossfade overlap         |
| `#tenuto`        | `Articulation.Tenuto`     | held to full value with soft release             |
| `#accent`        | `Articulation.Accent`     | +0.30 velocity boost (clamped)                   |
| `#marcato`       | `Articulation.Marcato`    | 25% duration + accent velocity boost             |
| `#staccato`      | `Articulation.Staccato`   | short detached                                   |
| `#sforzando`     | `Articulation.Sforzando`  | sudden envelope spike then return                |
| `#normal`        | `Articulation.Normal`     | default envelope (no override)                   |

Missing keys default to `#normal`.

## Charitable interpretation (D-v1.5-05)

The implementation is generous about missing or malformed fields:

- **Pack with missing field** → that field's defaults apply (uniform weights,
  no articulation override) + one-shot stderr advisory per missing field.
- **Unknown `#name` to `jam`** → falls back to `#jazz` pack + advisory.
- **Empty `over` Sequence** → empty Sequence returned + advisory.
- **`length <= 0`** → empty Sequence returned + advisory.
- **`order` outside [1, 3]** → clamped to [1, 3] + advisory (matches Markov).
- **Style + key musical incompatibility** (e.g., `#blues` + chromatic key) →
  charitable advisory, NOT a hard error. The composer hears the warning on
  stderr but the jam still produces a sequence (D-36-08 Claude's Discretion
  pick — matches Flow's ergonomics-first goal).

## Audit your installed packs

```flow
use "@improv"

Array[Symbol] names = (listStyles)
(each names (fn Symbol s => (print (str s))))
```

prints all currently-registered Symbols in registration order (shipped first,
user packs second). Use this to verify your user pack actually loaded.

## Security note (T-36-27 disposition: accept)

User style packs at `~/.config/flow/styles/*.flow` are executed by FlowEngine
init like any other Flow code. Flow scripts have no network or filesystem
write surface beyond explicit composer-invoked builtins; the rule-pack
convention only requires Dict values + a `(registerStyle ...)` call. The
loader is charitable about non-`(registerStyle ...)` top-level statements —
it loads anyway. If you copy a pack from an untrusted source, audit it before
dropping it into `~/.config/flow/styles/`.

## See also

- `.planning/phases/36-sequence-algebra-generative/36-RESEARCH.md` §Pattern 8
  — the full algorithm jam runs (chord progression iteration, rule-pack
  weighting, chord-aware Markov, key= synthetic frame).
- `flow-lang/StandardLibrary/Improv/JamFunctions.cs` — the C# implementation.
- `examples/generative/markov_jazz.flow` (v1.6) — a tutorial chapter that
  combines `markovTrain` / `markovGenerate` with `jam`.
