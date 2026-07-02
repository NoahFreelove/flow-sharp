# Design Philosophy

Flow makes a handful of opinionated choices that show up in every corner of the language. If a design decision ever seems surprising, it is probably one of these. This page collects them in one place, including the honest limitations that follow from them.

## Ergonomics First

The overriding goal is **composer ergonomics** — deliberately above runtime efficiency, type strictness, and generality. Flow is interpreted and unapologetically so. Where a choice trades implementation complexity for a lower-friction composer experience, Flow takes the friction off the composer. "Easy cases fast, flexible cases flexible."

This is why note streams compile against the ambient musical context, why nearly every builtin accepts named arguments, why music-typed units interoperate with plain numbers, and why the flow operator exists at all.

## Charitable Interpretation

**Degenerate input gets a reasonable default plus a one-shot advisory — it does not throw.** A wrong note is better than a crash mid-piece. This propagates through the entire stdlib:

- Out-of-range values are clamped
- Pattern combinators return their input untouched on degenerate arguments (`(every 0 cb seq)` → input + advisory).
- Notation import (ABC, MML) drops tokens it doesn't understand rather than failing.
- Config loading, module loading on the wrong target, and unknown windowing symbols all degrade gracefully.

Advisories follow a **one-shot `[prefix]` convention**: each distinct advisory fires once per process (or per key), prefixed by its subsystem — `[tuning]`, `[live]`, `[stretch]`, `[abc]`, `[mml]`, `[osc]`, `[jack]`, `[audio-in]`, `[midi]`, `[strict]`. Composer `print` output goes to stdout; engine advisories go to stderr.

When you *want* the opposite — a hard perimeter that rejects sloppy input — opt into [Strict Mode](Strict-Mode.md) per file. Charitable stays the default everywhere else.

## The Determinism Contract

Rendering the **same source at the same version produces byte-identical WAV and MIDI on repeated runs** ("two-run cmp-clean"). This is a contract in *shape*, not pinned bytes — legitimate changes (like the articulation rewrite) change the bytes, but two runs of the current code always agree. It holds on the desktop and in the browser.

How it is kept:

- **Seeded dither.** The TPDF dither on WAV export is deterministically seeded and reset per export.
- **`PrngRegistry`.** Every stochastic builtin routes its randomness through a single registry keyed by `(source location, generator name)`, reseeded at the render boundary. Unseeded calls still reproduce, because the seed derives from where the call lives in the source. A CI grep-gate bans stray `new Random(` in the generative stdlibs.
- **Deterministic voice-steal.** Voice-pool overflow steals the oldest voice, with the original input index as a tiebreaker.
- **Deterministic emitters.** SFZ round-robin counters reset per render; the notation `XmlWriter` pins its newline handling.

### Opt-Outs

Two places deliberately step outside the contract, and both say so out loud:

- **Live blocks.** Entering a `live { }` block emits `[live] ... opts OUT of two-run cmp-clean determinism`. Live coding is about editing mid-set, so the contract would only get in the way. **Offline renders stay deterministic** even during a live session — the opt-out is scoped to the `play` path. See [Live Coding](Live-Coding.md).
- **Chaos maps.** `lorenz` and `logistic` preserve *same-platform* two-run determinism only. Chained floating-point arithmetic in a chaotic system amplifies platform-specific quirks beyond ~50 iterations, so cross-platform reproducibility is not guaranteed for those two primitives. Markov, L-systems, and cellular automata are integer arithmetic and stay cross-platform deterministic.

## Prefix-Only S-Expression Arithmetic

Flow has **no infix `+ - * /`** — you write `(add a b)`, `(mul 3.0 4.5)`, `(sub 100 37)`. This is a deliberate, Haskell/Lisp-lineage choice, not an oversight. A stray infix expression is a parse error, and the parser suggests the prefix form. Signed numeric literals (`-3`, `+5`, `-2.5`) still lex as single tokens at expression-start positions, so negative constants read naturally.

The flow operator `->` is the concession to readability: `x -> f arg` rewrites to `(f x arg)` at parse time, with zero runtime machinery.

## Genre-Agnosticism

Flow treats **all genres as equal** — classical, EDM, jazz, pop, metal, chiptune. There is no privileged idiom baked into the language. The corollary is a scope rule: a feature justified *only* by non-musical use is rejected. Flow can compute, but it is not a general-purpose language, and it won't be bent into one.

## Hand-Rolled DSP, Minimal Dependencies

Almost the entire audio and notation stack is **hand-written in this repo** rather than pulled from libraries:

- Radix-2 Cooley-Tukey **FFT**, Hann/Gaussian/Tukey windows, harmonic-percussive separation.
- **Phase vocoder** (Laroche-Dolson 1999) and **TD-PSOLA + YIN** for time-stretch and pitch-shift.
- **Schroeder reverb**, RBJ-cookbook biquad filters, a peak-detect compressor, constant-power panning.
- A common-subset **SFZ parser**, and four notation parsers/writers: **MusicXML** writer, **LilyPond** writer, **ABC** importer, **PC-98 MML** importer.
- Voice allocation, custom oscillators, WAV load/save, the note-stream compiler.

The only external libraries are DryWetMidi (MIDI file encoding — genuinely error-prone to hand-roll), Rug.Osc (OSC, desktop-only), NAudio.Wasapi (Windows output, desktop-only), plus PrettyPrompt and Tomlyn for the CLI. GPL-licensed options were rejected on principle (RubberBand for stretch, Ableton Link for sync) to keep the MIT license clean.

## Honest Known Caveats

Flow ships partial features labeled honestly rather than oversold. The ones worth knowing:

- **Sampled "piano" notes are ~1.2 s long.** Held notes go silent past that, and `release=` / `sustainPedal` / `legato` can't extend a sample beyond its length. For sustained passages, use the synthesized `"organ"` instead.
- **Reverb tail clips at a section boundary.** A section's final bar truncates the decay tail (v1.6 fix pending).
- **Web playground sampled instruments are silent** — only synthesis voices sound in the browser. See [Playground](Playground.md).
- **Audio device enumeration is empty under PulseAudio Simple.** `(audioDevices)` returns nothing; select a device with the `--device` CLI flag instead.
- **`flow check` executes** the script (it is not parse-only yet).
- **`flow midi2flow` clamps dense-bar polyphony** — trailing notes in very dense bars can be dropped by the bar-fit pass (per-track polyphony split is the planned fix).
- **Realtime MIDI / JACK are Linux-only**, best-effort, and ms-aligned (not sample-accurate). See [OSC and MIDI](OSC-and-MIDI.md).
- **Ableton Link is not shipped and not planned** (licensing).
- **Vocaloid-style voice synthesis is planned**, not present. Formant `sing` covers 5 vowels + `s`/`t`/`n` onsets today.

For the complete status matrix (Fully / Partial / Not yet), see [FEATURES.md](https://github.com/NoahFreelove/flow-sharp/blob/main/FEATURES.md) at the repo root.

## See Also

- [Strict Mode](Strict-Mode.md) — Opting into a hard input perimeter
- [Live Coding](Live-Coding.md) — The determinism opt-out in practice
- [Generative Music](Generative.md) — `PrngRegistry` and the chaos-map caveat
- [Tips and Tricks](Tips-and-Tricks.md) — Charitable interpretation and PRNG determinism idioms
- [Playground](Playground.md) — What the browser build can and can't do
