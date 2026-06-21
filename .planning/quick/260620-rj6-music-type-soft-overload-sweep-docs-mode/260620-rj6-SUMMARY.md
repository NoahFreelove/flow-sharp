---
phase: quick-260620-rj6
plan: 01
subsystem: stdlib + docs
status: complete
tags: [overload-resolution, music-types, soft-overload, docs-sweep]
requires:
  - OverloadResolver +1000 exact-match tier
  - internal-proc surface bridge (Interpreter.cs:987-994)
provides:
  - "Cent trill/repeat overloads with cents-remainder folding"
  - "simple Decibel compress/sidechain overloads"
  - "Second/Hertz/Semitone/Beat ergonomic builtin + proc overloads"
  - "typed music-literal call forms across wiki/BuiltInDocs/examples"
affects:
  - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
  - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/std.flow
  - flow-lang/audio.flow
  - flow-lang/composition.flow
  - flow-lang/StandardLibrary/BuiltInDocs.cs
  - wiki/
  - examples/
tech-stack:
  added: []
  patterns:
    - "register same impl lambda under a music-typed FunctionSignature (createSineTone model)"
    - "internal proc .flow surface declaration required for each C# overload to resolve"
    - "Math.Truncate(cents/100) split + CentOffset remainder fold"
key-files:
  created: []
  modified:
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
    - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
    - flow-lang/audio.flow
    - flow-lang/composition.flow
    - flow-lang/StandardLibrary/BuiltInDocs.cs
    - wiki/ (15 pages)
    - examples/ (5 scripts)
decisions:
  - "std.flow surface declarations for the Cent transforms ship with Task 1 (C#) since they are required for the C# Cent overloads to resolve — correctness, not docs"
  - "audio.flow tone wrappers are Flow procs (createSineTone is a proc, not internal) so Task 2 adds Second/Hertz proc wrappers; the C# Second/Hertz createSineTone overloads from Task 1 are the underlying-primitive surface"
  - "duration-first teaching examples → typed (1.0s 440Hz); generic buffer examples → frequency-first canonical (440Hz 0.5 0.5)"
metrics:
  duration: ~40min
  completed: 2026-06-20
---

# Quick Task 260620-rj6: Music-Type Soft-Overload Sweep + Docs Modernization

Added the exact audit-derived set of music-type soft-overloads so typed music
literals (`+50c`, `-12dB`, `1.0s`, `440Hz`, `+5st`, `0.5b`) resolve at the +1000
exact-match tier for builtins whose params previously accepted only raw numerics,
then modernized wiki / BuiltInDocs / examples to the typed forms.

## What shipped

### Task 1 — C# soft-overloads (commit 42ee1cf)

- **Cent (correctness)** — `trill(Sequence, Cent)` + `repeat(Sequence, Int, Cent)`.
  New `TrillCent` / `RepeatTransposeCent` lambdas split via `Math.Truncate(cents/100)`
  and fold the fractional remainder into each note's `CentOffset`. Threaded a
  `centsRemainder` param (default 0.0) through `TrillBar`/`BuildTrillNote` so the
  Semitone path stays byte-identical.
- **Decibel (correctness)** — simple `compress(Buffer, Decibel, Double)` +
  `sidechain(Buffer, Buffer, Decibel, Double)` reusing `CompressSimple`/`SidechainSimple`.
- **Second (ergonomic)** — `noise` x4, `createClip`, `createAR`, `createSineTone`
  (`Second,Double,Double` + `Second,Hertz,Double`), `fadeIn`, `fadeOut`.
- **Hertz** — `createOscillatorState(Hertz, Int)`.
- **Semitone** — `loadWav(String, Semitone)`.
- **Beat** — `createVoice`/`setVoiceOffset`/`setTrackOffset`/`renderTrack`/`beatsToFrames`,
  each reading the RAW `As<double>()` with **no** beat-true-to-sig multiplier.
- std.flow internal-proc surfaces for the Cent trill + repeat overloads.

### Task 2 — .flow proc + internal-proc surfaces (commit 02c4b53)

- audio.flow: Second-duration proc forms (createBufferStereo/Mono(+Custom),
  createSilence(Mono), createSineTone/Saw/Square/Triangle), Hertz createOsc, and
  internal-proc surfaces for every Task-1 C# audio overload (loadWav Semitone,
  createClip/noise/fadeIn/fadeOut/createAR Second, createOscillatorState Hertz,
  simple Decibel compress/sidechain).
- composition.flow: Beat-typed internal-proc surfaces + Beat proc wrappers
  (voiceAt / startAt / render — forward the raw value, no multiplier math).

### Task 3 — wiki + BuiltInDocs sweep (commit 86ebc9c)

15 wiki pages + BuiltInDocs.cs modernized to typed literals. `wiki/Home.md`'s
pre-existing working-tree prose/structure edits were preserved — only the
`fadeOut 0.5` → `fadeOut 0.5s` code example was touched.

### Task 4 — examples sweep (commit d915b4c)

5 example scripts modernized; every one runs clean (exit 0).

## Verification

- `dotnet build flow-lang/flow-lang.csproj` → 0 errors (Desktop).
- `dotnet build -p:FlowTarget=Web` → 0 errors (Web).
- Eval snippets resolve:
  - `(trill (| C4 D4 |) +50c)` → ok
  - `(repeat (| C4 E4 |) 3 +50c)` → ok
  - `(compress (createSineTone 440Hz 1.0 0.5) -12dB 4.0)` → ok
  - `(createSineTone 1.0s 440Hz 0.5)` → ok
  - `(createVoice buf 0.5b)` → ok (with `use "@composition"`)
  - createOsc(Hertz), loadWav(+5st), noise(0.5s), createAR(0.1s 0.2s 44100),
    fadeIn/fadeOut(Ns), setVoiceOffset/setTrackOffset/renderTrack/beatsToFrames(Nb)
    → all ok
- DO-NOT-ADD set verifiably absent: no Decibel sig on setVoiceGain/setTrackGain/
  mixBuffers; no Second sig on createADSR's sustain slot.
- All modernized examples run exit 0; audio/transform/effect/transpose/voice test
  scripts show zero regressions.

## Deviations from Plan

### Auto-fixed / clarified

**1. [Rule 3 - Blocking] internal-proc surfaces required for resolution**
- **Found during:** Task 1 verify — `(trill seq +50c)` still threw "No matching
  overload" after the C# registration alone.
- **Cause:** builtins surface to the resolver via `internal proc` declarations in
  the `.flow` stdlib files (`Interpreter.cs:987` bridges the `.flow` signature to
  the C# impl via `TryGetImplementation`). A C# `registry.Register` with no matching
  `.flow` surface is unreachable from user code.
- **Fix:** added the matching `internal proc` surface for every new C# overload —
  std.flow (Cent trill/repeat, shipped in Task 1's commit since it pairs with the
  C# Cent correctness fix), audio.flow + composition.flow (rest, in Task 2).
- **Commits:** 42ee1cf (std.flow), 02c4b53 (audio.flow + composition.flow).

**2. [Clarification] createSineTone is a Flow proc, not an internal builtin**
- audio.flow declares `proc createSineTone(...)` (built on createBufferStereo +
  generateSine + createOsc) — this is the user-facing surface, NOT the C# builtin.
- Task 2 therefore adds Second/Hertz **proc** wrappers in audio.flow for the tone
  generators; the Task-1 C# `createSineTone` Second/Hertz overloads remain as the
  underlying-primitive surface (harmless, registered).

## Examples deliberately left unmodernized

- `examples/long_demo.flow:363` — compress threshold uses a computed `Double`
  variable (`compThresh = (sub 0.0 12.0)`); kept to illustrate the Double path.
- `examples/tutorial.flow:841` (Note comment) — `(loadWav "...kick.wav" 0)` kept to
  teach the semitones=0 byte-identical short-circuit.
- `wiki/Effects.md:103` — `(delay tone 250.0 ...)` kept as the explicit "Double"
  contrast against the `250ms` form on the next line (intentional teaching pair).
- `wiki/Tips-and-Tricks.md:365` (Note) — `(loadWav "x.wav" 0)` semitones=0 teaching note.
- reverb roomSize / damping / mix Double args left as-is (those slots are genuinely
  Double; only the decay slot is Second-typed and was already `1.5s`/`2.5s` in docs).

## Self-Check: PASSED

- All 7 modified source/stdlib files exist and compile (0 errors, both targets).
- All 4 commits present in git log (42ee1cf, 02c4b53, 86ebc9c, d915b4c).
