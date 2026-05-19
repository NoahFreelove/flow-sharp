# Stack Research — Flow v1.5 Stage, Studio, Web

**Domain:** Music-production language interpreter (interpreted, statically-typed, .NET 10 C#) — adding live-coding revamp, notation interop, real-time MIDI / transport sync, WASM playground, and cross-platform distribution
**Researched:** 2026-05-18
**Confidence:** HIGH for most surfaces; MEDIUM on WASM-AOT viability (rapidly evolving) and on improvisation-API library choice (no obvious .NET-native option)
**Researcher orientation:** Flow already ships ~83K LOC C# + 312 .flow files at v1.4 close. The guiding principle inherited from v1.0/v1.4 research is **minimal dependencies — hand-roll wherever the surface fits in a phase scope**. Every recommendation below has been weighed against that bar.

---

## TL;DR

**v1.5 adds 4–6 new external dependencies, all scoped behind interface seams (`IAudioBackend`, new `IMidiBackend`, new `OscModule`).** The majority of phases (35, 36, 37, plus most of 39) ship with hand-rolled C# only — the same rhythm as v1.3. New deps appear at Phase 38 (OSC), Phase 40 (real-time MIDI + JACK), and Phase 41 (Windows/macOS backends + WASM host).

| Phase | New NuGet deps | Hand-rolled surfaces |
|-------|----------------|---------------------|
| 35 (language foundation) | none | Pattern matching, `-> as name`, Rust-style diagnostics, pure-Flow test framework |
| 36 (sequence algebra + generative + improv) | none | Tidal algebra, Markov/L-system/CA/Lorenz, parameterized sections, chord-aware Markov improv |
| 37 (sound design) | none (optional `FftFlat`) | Granular synthesis, phase vocoder, stereo pan, sampler polish |
| 38 (live coding 2.0) | `Rug.Osc` 1.2.5 | `live { ... }`, modernized watch, REPL polish, audio input via PulseAudio capture |
| 39 (notation citizenship) | none (vendor `ABCSharp` + `musicxml-schemas` source) | MusicXML emit, LilyPond emit, ABC import, MML import |
| 40 (studio sync) | `RtMidi.Core` 1.0.53, `JackSharp` 0.4.0 (optional) | MIDI clock; Ableton Link via P/Invoke if license cleared |
| 41 (reach + closer) | `NAudio.Wasapi` 2.3.0, `OwnAudioSharp` 1.0.68, `KristofferStrube.Blazor.WebAudio` | `flow doc`, third-genre showcase, JetBrains publish workflow |

**Key reality-check findings from this research:**
- **DryWetMidi 8.0.3 does NOT support Linux for real-time MIDI device I/O** — only file I/O. The official "Supported OS" docs confirm Windows + macOS only. This forces `RtMidi.Core` for Phase 40.
- **Magenta is archived read-only since 2026-01-06.** Improvisation API ships as hand-rolled chord-aware Markov; ML-backed improv is post-v1.5.
- **.NET 10's Native AOT for WASM landed in 2025** — Blazor WASM viability for the Phase 41 playground is materially higher than it was even one year ago (76% reduction in bootstrap JS, AOT compilation to WASM).
- **RubberBand and Ableton Link are dual-license GPL/commercial** — both flagged. Hand-roll phase vocoder; defer Ableton Link until legal review.

---

## Guiding Principles (inherited from v1.0 / v1.4 stack research)

1. **Minimal dependencies.** Each new NuGet package is a license/maintenance/.NET-10-compat liability. Add one only when hand-rolling would dominate a phase.
2. **No duplicate stacks.** Flow already has hand-rolled DSP (reverb / filters / compressor / delay / panning), WAV I/O, PulseAudio backend, MIDI export (DryWetMidi 8.0.3), and a custom recursive-descent parser. Do not pull in libraries that duplicate these.
3. **`IAudioBackend` and the new `IMidiBackend` are abstraction seams.** Platform-specific code lives behind them — the rest of FlowLang remains platform-agnostic.
4. **License lean = MIT / Apache-2.0 / BSD-3 / public domain.** GPL/LGPL native libs are usable through P/Invoke (we are not statically linking them) but flagged. Reject CC-BY-SA and CC-BY-NC outright (per Phase 29 SPEC-2 precedent).
5. **Two-run cmp-clean determinism contract holds.** Every new dependency that touches the render path must be auditable for determinism. PRNG-using libraries (Magenta etc.) are extra-suspect.

---

## v1.5 New Dependency Verdict — Summary Table

| # | Surface | Phase | Decision | Library / Approach | License |
|---|---------|-------|----------|--------------------|---------|
| 1 | Pattern matching (language) | 35 | **Hand-roll** — AST nodes + decision-tree compile | n/a | n/a |
| 2 | `-> as name` chain naming | 35 | **Hand-roll** — parser sugar | n/a | n/a |
| 3 | Rust-style diagnostics | 35 | **Hand-roll** — extend `ErrorReporter` | n/a | n/a |
| 4 | Pure-Flow test framework | 35 | **Hand-roll** — `.flow` convention + CLI runner subcommand | n/a | n/a |
| 5 | Tidal pattern algebra (every/fast/slow/chunk/phase/rev) | 36 | **Hand-roll** — extends `Sequence` + new transforms | n/a | n/a |
| 6 | Markov / L-system / cellular automata / Lorenz | 36 | **Hand-roll** — all small, no library justifies | n/a | n/a |
| 7 | Parameterized sections | 36 | **Hand-roll** — extend `SectionDeclaration` AST | n/a | n/a |
| 8 | Improvisation API | 36 | **Hand-roll** baseline (Markov + chord-aware) — defer ML model | n/a | n/a |
| 9 | Granular synthesis | 37 | **Hand-roll** — new `Granulator` DSP module | n/a | n/a |
| 10 | Time-stretch + pitch-shift (independent) | 37 | **Hand-roll** phase vocoder, with `FftFlat` as optional FFT helper | MIT (FftFlat) |
| 11 | Stereo pan across instruments | 37 | **Hand-roll** — constant-power panning already specced in v1.0 stack | n/a | n/a |
| 12 | Sampler polish (piano warmth, VSCO velocity layers, SFZ `seq_position`/`seq_length`, per-articulation envelope multipliers, flute samples, sampled drums) | 37 | **Hand-roll** — extends existing `SampledInstrumentRenderer` / `SfzRenderer` / `SfzParser` | n/a |
| 13 | `live { ... }` block + modernized watch mode (cue-quantized swap, ANSI live status, structured stderr) | 38 | **Hand-roll** — extends existing watch-mode + `MusicalContext` | n/a |
| 14 | REPL LSP-backed tab completion | 38 | **In-process reuse** of existing `flow-lsp` — no IPC | (already in use) |
| 15 | Inline `?fn` help, pretty piano-roll | 38 | **Hand-roll** — `BuiltInDocs` table + Unicode block-char piano roll | n/a |
| 16 | Audio input (mic / line-in) | 38 | **Hand-roll** — extend `PulseAudioSimpleBackend` with `PA_STREAM_RECORD` direction | n/a |
| 17 | OSC server/client | 38 | **NEW DEP** — `Rug.Osc` v1.2.5 | MIT-style |
| 18 | MusicXML export | 39 | **Hand-roll** with `sightreader/musicxml-schemas` XSD-generated POCOs as scaffolding | (schemas: vendor, verify license) |
| 19 | LilyPond export | 39 | **Hand-roll** — text emit, no library | n/a |
| 20 | ABC notation import | 39 | **Vendor** `ABCSharp` source (single-file) — no NuGet package exists | MIT (verify) |
| 21 | MML notation import | 39 | **Hand-roll** — target one dialect (PMD subset) | n/a |
| 22 | Real-time MIDI output (`IMidiBackend`) | 40 | **NEW DEP** — `RtMidi.Core` v1.0.53 + native libs for cross-platform | MIT (binding) + MIT (libRtMidi) |
| 23 | MIDI clock + Ableton Link + JACK transport | 40 | **Mixed**: MIDI clock = hand-roll; Ableton Link = **P/Invoke wrapper** of `libabletonlink` (defer/skip if GPL conflict); JACK = `JackSharp` v0.4.0 (optional Linux-only) | Link: GPLv2+/commercial; JackSharp: LGPL/MIT |
| 24 | WASM playground | 41 | **NEW APPROACH** — Blazor WebAssembly host (.NET 10) + JSInterop → Web Audio API; consider `KristofferStrube.Blazor.WebAudio` wrapper | MIT |
| 25 | Cross-platform binaries — Windows WASAPI | 41 | **NEW DEP** — `NAudio.Wasapi` v2.3.0 (scoped to new `WasapiBackend` only) | Microsoft Public License |
| 26 | Cross-platform binaries — macOS CoreAudio | 41 | **P/Invoke** AudioToolbox/AudioUnit directly; or `OwnAudioSharp` (miniaudio binding) as one-shot cross-platform shortcut | OwnAudioSharp: MIT |
| 27 | `flow doc` documentation generator | 41 | **Hand-roll** — extract `BuiltInDocs` + proc signatures + `//` comments → Markdown + HTML | n/a |
| 28 | JetBrains Marketplace publish | 41 | **Workflow only** — Gradle `publishPlugin` task + signing keys; no new code dep | n/a |

**Net new external dependencies introduced in v1.5: 4-6 packages.** All behind interface seams or scoped to new top-level modules.

---

## Recommended Stack — Detailed

### Core Runtime (unchanged from v1.4)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 10 | net10.0 | Runtime | Already in use; .NET 10 added Native AOT for WASM (Phase 41 unlock) |
| C# 13 | Latest | Language | Records / pattern matching / file-scoped namespaces / source generators all in active use |
| PulseAudio (P/Invoke) | System | Linux audio playback + new capture | `PulseAudioSimpleBackend` extends for Phase 38 audio input |
| DryWetMidi | 8.0.3 (current) | MIDI file R/W (export + flow2midi/midi2flow round-trip) | Confirmed working at v1.4; multi-track export + `Quantizer` already wired in. **Cannot use for real-time MIDI I/O — Windows + macOS only, no ALSA support** (verified at the DryWetMidi "Supported OS" doc page). v1.5 keeps DryWetMidi for *file* work and adds `RtMidi.Core` for *device* work. |

### NEW: Real-time MIDI Output (Phase 40)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `RtMidi.Core` | 1.0.53 | Cross-platform real-time MIDI device I/O | Wraps `librtmidi` (which has ALSA + CoreMIDI + WinMM/WinUWP backends). DryWetMidi 8.0.3 **explicitly does not support Linux** for device I/O — confirmed. RtMidi.Core advertises Windows + macOS officially; Linux ALSA works via the bundled native lib but needs an integration test. Maintenance state: latest release on NuGet is 1.0.53; the only active community-maintained .NET binding for librtmidi. Fits behind the new `IMidiBackend` abstraction so it can be swapped without touching FlowLang core. |

**Alternatives rejected:**
- `managed-midi` — explicitly marked "Past project" on its GitHub README; same conclusion as v1.4 stack research.
- DryWetMidi for device I/O — **technically unavailable on Linux**, which is our primary platform.
- Hand-roll ALSA / CoreMIDI / WinMM — three platform-specific P/Invoke surfaces is more than one phase's scope; RtMidi.Core gives us all three under one API for one dependency.

**Compatibility note:** `RtMidi.Core` targets .NET Standard 2.0, fully compatible with .NET 10. Native librtmidi binaries for linux-x64, win-x64, osx-x64, osx-arm64 ship in the NuGet package.

### NEW: OSC Server/Client (Phase 38)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `Rug.Osc` | 1.2.5 | OSC 1.0 protocol — bundles, addresses, UDP send/recv, all argument types | Complete OSC 1.0 implementation; supports the full type tag set including Osc-MIDI and arrays. .NET 2.0 baseline = .NET 10 compatible. Zero dependencies. The package has been stable since 2017 — for a protocol that itself hasn't changed since 2002, "no recent commits" is a feature, not a bug. |

**Alternatives rejected:**
- `OscCore` — exists but smaller surface area; Rug.Osc covers more of the spec out-of-the-box.
- Hand-roll — OSC's TimeTag NTP-fixed-point, address-pattern matching, and bundle nesting add up to a non-trivial spec; not worth a phase's worth of work when a stable BCL-only library exists.

### NEW: Windows WASAPI Backend (Phase 41)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `NAudio.Wasapi` | 2.3.0 | Windows audio output backend | The single .NET-native, mature, MS-Public-License WASAPI wrapper. Confirmed .NET 10 compatible (NAudio v2.3.0 added `#:package` directive support which is a .NET 10 preview-4 feature). **Important constraint:** isolate to a new `WasapiBackend.cs` implementing `IAudioBackend` — do NOT pull NAudio.Core into the rest of FlowLang. We are using one specific class (`WasapiOut`) and the MMDeviceEnumerator, not duplicating the existing DSP pipeline. |

**Alternatives rejected:**
- `CSCore` — Windows-only same as NAudio; no compelling advantage and less active.
- Hand-roll WASAPI via P/Invoke — possible (the existing PulseAudio backend is hand-rolled P/Invoke) but WASAPI's COM-interface surface is materially more complex than PulseAudio Simple; this would dominate Phase 41 scope.

### NEW: macOS CoreAudio Backend (Phase 41)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| **Option A (preferred)**: `OwnAudioSharp` | 1.0.68 | Cross-platform audio I/O backed by miniaudio | Single binding covers Win/macOS/Linux — same API everywhere. If integration testing on macOS shows acceptable latency, this is the simplest path. miniaudio is public domain, OwnAudioSharp is MIT. |
| **Option B (fallback)**: Hand-roll P/Invoke of AudioToolbox/AudioUnit | n/a | macOS-only audio output backend | Mirrors the existing PulseAudio pattern. More work but zero new deps and full control. .NET MAUI / Xamarin bindings (CoreMidi namespace) exist but only target iOS — they don't ship in a non-MAUI .NET 10 console app. |

**Recommendation:** start with Option A's `OwnAudioSharp` smoke test in Phase 41 Plan 1. If it cleanly handles the existing stereo `IAudioBackend` interface, ship it as the macOS backend AND keep PulseAudio for Linux (don't migrate Linux). If miniaudio's quirks surface, fall back to Option B.

**Alternatives rejected:**
- `libsoundio-sharp` — atsushieno's binding, less maintained than `OwnAudioSharp`.
- NAudio's macOS plans — NAudio is Windows-only; CoreAudio support has been on its roadmap for years without landing.

### NEW: Ableton Link (Phase 40, optional)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `Ableton/link` (native C++ lib via P/Invoke) | latest from https://github.com/Ableton/link | Cross-application beat/tempo/phase/start-stop sync | No active .NET wrapper exists. **Licensing alert:** Ableton Link is dual-licensed GPLv2+ AND proprietary. Flow is open-source — GPL compatibility check needed before shipping. Existing C# wrapper precedent: `UnityAbletonLink` (P/Invoke pattern) — useful as scaffolding reference. |

**Recommendation:** P/Invoke the C++ libabletonlink as the lowest-risk path. Treat as a stretch feature in Phase 40 — if the GPL licensing investigation flags a conflict with Flow's distribution license, downgrade to "Ableton Link compatibility = NOT shipped; document the API surface for community contribution."

### NEW: JACK Transport Sync (Phase 40, optional, Linux-only)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `JackSharp` | 0.4.0 | C# wrapper around libjack | LGPL/MIT mixed; provides `JackSharp.Processor` and `JackSharp.Controller`. The standard .NET binding for JACK; no real competitor. |

**Recommendation:** Gate JACK transport behind a runtime probe (`isJackAvailable()`). Linux composers who don't have a JACK server get a graceful no-op. Don't make JACK a hard dep of the Linux build.

### NEW: WASM Playground (Phase 41)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| **Blazor WebAssembly** | .NET 10 SDK | Host the FlowEngine in the browser | .NET 10's WASM/AOT improvements are substantial: blazor.web.js dropped from 183 KB → 43 KB; core assemblies compressed 50–70%; startup feels competitive. The FlowEngine is pure managed C# (modulo PulseAudio P/Invoke, which we exclude from the WASM build via conditional compilation) — should compile cleanly to WASM. |
| `KristofferStrube.Blazor.WebAudio` | latest | Blazor wrapper around the Web Audio API | Used for playback (replaces PulseAudio in the WASM build). The Phase 38 `live { ... }` block is the headline browser demo — composer edits Flow in the browser, audio plays via Web Audio. Optional: could JSInterop the Web Audio API directly if we want zero deps. |

**Critical constraint for Phase 41:** the FlowEngine's audio path must be retargettable. The existing `IAudioBackend` abstraction was built exactly for this — add a `WebAudioBackend.cs` and the rest of the engine doesn't know it's running in a browser.

**Bundle size estimate:** the full Flow stdlib + interpreter + 21 sample WAVs is currently ~40 MB self-contained. For the browser, ship only the interpreter + a curated stdlib subset; samples can lazy-load on first `renderSong`. Target: < 15 MB compressed initial payload.

**Alternatives rejected:**
- Uno Platform's WASM head — heavier than Blazor for our use case (we don't need XAML).
- NativeAOT-LLVM standalone (running .NET in browser without Blazor) — actively researched approach but documented as experimental; Blazor WASM is the well-trodden path.

### NEW (scaffolding only): MusicXML Export (Phase 39)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `sightreader/musicxml-schemas` (vendored) | latest commit | XSD-generated C# POCOs for MusicXML 2.0/3.0/3.1 schema | We don't pull this as a NuGet (no package). Vendor the generated POCOs once, treat them as a code dependency. Then hand-roll the Flow → POCO mapper for the minimum viable export subset. |

**Why not `MusicXml.NET` v3.1.0:** it's a *parser*, not a *writer*. The repo only does file-in. We need file-out, so the parser's API doesn't help.

**Minimum viable subset for MuseScore round-trip:** `score-partwise` root, `part-list` with `score-part`, `part` containing `measure`s, each with `attributes` (divisions / key / time / clef) + `note` elements (pitch / duration / tied / chord / lyric / dynamics). This is ~30 elements out of MusicXML's ~400+. Hand-roll using the vendored POCOs as types; serialize via `System.Xml.Serialization` or just `StringBuilder`.

**Integration with existing pipeline:** reuse the same `BarData` / `MusicalNoteData` / `Sequence` structures that feed `MidiExport`. The existing multi-track MIDI export's tick math gives us measure boundaries for free. Lift the per-instrument routing logic from `MidiExport.cs` for the `score-part` instrument names.

### NEW: ABC Notation Import (Phase 39)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `matthewcpp/ABCSharp` (vendored as source) | latest from GitHub | ABC notation parser | No NuGet package exists; vendor the source. ABC is a tiny grammar (notes, durations, key, meter, chord symbols) — `ABCSharp` is the only actively-updated C# implementation (last commit 2024). |

**Alternatives rejected:**
- `danwatford/abc` — Java/Kotlin, not C#.
- Hand-roll — feasible (ABC is small) but ABCSharp already exists and works; vendoring 1-2 source files is cheaper than re-deriving the grammar.

### Hand-Roll: LilyPond Export (Phase 39)

**No library. LilyPond input is plain text — write a `LilyPondWriter` class that walks the same `Section`/`Sequence`/`Bar` structures the WAV+MIDI exporters use.**

Minimum viable emit:
```lilypond
\version "2.24.0"
\score {
  \new Staff {
    \time 4/4
    \key c \major
    \tempo 4 = 120
    c'4 d'4 e'4 f'4 |
    g'2 c''2 |
  }
  \layout { }
  \midi { }
}
```

Map: `Sequence` → `\new Staff`; `MusicalContext.TimeSignature` → `\time`; key → `\key`; tempo → `\tempo`; note pitch+duration → LilyPond pitch syntax with `'` octave marks. Chords → `<c' e' g'>4`. Ties → `~`. Slurs/articulations → existing Phase 28 tokens.

**Why no library:** lilypond.NET, lily-export-sharp etc. don't exist; the LilyPond ecosystem assumes its own CLI as the toolchain, not third-party emitters. Pure string building is the right level.

### Hand-Roll: MML Import (Phase 39)

**Target one MML dialect: a PMD/MUCOM88 subset (the most common "modern chiptune" baseline).** Reject everything else with a parser error — composers who want NRTDRV or NES-style FT can preprocess.

Minimal grammar:
- Octave: `O<n>` and `<` / `>` shift
- Notes: `[a-g][#+\-]?<duration>` with `.` for dot
- Rest: `r<duration>`
- Length: `L<n>` default duration
- Tempo: `T<bpm>`
- Volume: `V<0-15>`
- Voice/instrument: `@<n>`
- Loop: `[ ... ]<n>`

Hand-roll a recursive-descent parser following the existing `Parser.cs` pattern; emit Flow AST nodes directly (or compile straight to `Sequence` values).

**No library exists for C#** — `mugene-ng` is Kotlin, `mml_parser` is C, `MML-Parser` is JS. Worth keeping the surface small.

### Hand-Roll: Phase Vocoder (Time-Stretch + Pitch-Shift) (Phase 37)

**No P/Invoke RubberBand. No libsamplerate.NET.** Hand-roll the phase vocoder.

| Helper | Optional NuGet | Why |
|--------|----------------|-----|
| `FftFlat` 1.0.1 | If profiling shows FFT as bottleneck | Pure C# FFT, 4× faster than Math.NET; MIT; zero native deps |

**Rationale:**
- RubberBand has a CC0/GPL dual license — same legal hazard as Ableton Link. The `breakfastquay/rubberband` repo ships an official .NET interface but it requires C++ build infrastructure. `spoiledtechie/RubberBand.Net` is a community wrapper but appears unmaintained.
- libsamplerate.NET — last touched many years ago.
- Phase vocoder algorithm is well-documented (Bernsee, Laroche-Dolson) and fits in ~300 lines of C#. Determinism is preservable (no PRNG).

**Algorithm sketch:** STFT (Hann window, 75% overlap, 1024–2048 FFT) → phase-unwrap per bin → time-scale by adjusting hop size → optional resample for pitch shift OR scale bin phases for pitch-shift-without-stretch → inverse STFT with overlap-add. Independence of time and pitch comes from doing the two operations in series.

**Acceptance bar:** match Sound on Sound's "pitch shifter perceptual tolerance" baseline at modest stretch factors (0.5×–2.0×). Heroic stretches (10×) are not in scope.

### Hand-Roll: Granular Synthesis (Phase 37)

Standard surface (from the Output / Native Instruments / Sound on Sound consensus):
- `grainDuration` (Ms, typical 5–500ms)
- `density` (grains/sec, 1–1000)
- `position` (offset into source buffer, 0–1 or absolute samples)
- `positionJitter` (random spread around position)
- `pitch` (semitones or ratio)
- `pitchJitter`
- `pan` + `panJitter`
- `windowShape` (`hann` / `triangle` / `gaussian` / `rectangular`)
- `seed` (deterministic randomness — required for Flow's determinism contract)

Surface in Flow:
```
Buffer cloud = (granulate sourceBuffer
                  grainDuration: 50ms
                  density: 100
                  position: 0.3
                  positionJitter: 0.1
                  pitch: 0
                  pitchJitter: 50
                  pan: 0.0
                  panJitter: 0.5
                  windowShape: #hann
                  seed: 42)
```

Implementation: `Granulator.cs` in `StandardLibrary/Audio/DSP/` — schedule grain events on a deterministic timeline, render each by windowing+resampling the source buffer, accumulate into an output `AudioBuffer`. Reuse existing stereo handling.

### Hand-Roll: Markov / L-system / CA / Lorenz (Phase 36)

All four fit in ~50–150 lines of C# each. None justify a library.

| Generator | Surface |
|-----------|---------|
| **Markov chain** | `(markovTrain corpusSequence order: 2)` returns a `MarkovModel`; `(markovGenerate model lengthBars: 4 seed: 42)` returns a `Sequence` |
| **L-system** | `(lsystem axiom: "F" rules: <<"F" "F+F-F">> iterations: 4)` returns a `String` of tokens; companion `(lsystemToSequence tokens scale: cmajor)` maps tokens → notes (Wikipedia/McCormack mapping, well-documented) |
| **Cellular automata** | `(elementaryCa rule: 30 width: 16 generations: 32 seed: 42)` returns an `Array[Bool]` grid; map onto pitches in a scale via `(caToSequence grid scale: cmajor)` |
| **Lorenz attractor** | `(lorenzAttractor sigma: 10.0 rho: 28.0 beta: 2.667 steps: 200 dt: 0.01)` returns three `Array[Double]` axes; `(lorenzToSequence x scale: cmajor)` quantizes to scale |

All four route their RNGs through the existing `ExecutionContext.SeededRandom` infrastructure (the same one that backs `(?? ...)` seeded random in note streams) — preserves the determinism contract automatically.

### Hand-Roll: Improvisation API (Phase 36)

**Recommendation: ship a chord-aware Markov baseline. Defer ML model integration to v1.6+ if at all.**

Why no Magenta:
- **Magenta is Python TensorFlow** — the GitHub repo (https://github.com/magenta/magenta) was **archived read-only on 2026-01-06**. It is no longer maintained.
- Magenta.js exists for the browser but adds a heavyweight JS dep that conflicts with Flow's "minimal deps" principle.
- Magenta RealTime (2025) is impressive but requires a TPU/Colab runtime — orthogonal to a desktop CLI.

The chord-aware Markov baseline:
- Input: a chord progression (`<<Cmaj7, Am7, Dm7, G7>>`), a scale (`cmajor`), a Markov order (1 or 2), a rhythm profile (Tidal pattern), a seed
- Output: a `Sequence` of melodic notes where transitions respect (a) chord tones on strong beats, (b) scale tones elsewhere, (c) trained transition probabilities from optional corpus

Bias toward chord tones on downbeats is the cheap way to get "musical" output without ML. This is `BachBot`-style on a budget — solves 80% of "I want a passable lead line over my chord progression" use cases.

Surface:
```
Sequence solo = (improvise chordProgression: progression
                            scale: cmajor
                            rhythm: tidalPattern
                            order: 2
                            corpus: trainingSeq
                            seed: 42)
```

ML-backed improvisation can be added later as a separate `(improviseML ...)` function that shells out to a Python service (`flow-improv-server`) — the same pattern as the existing TTS hook.

### Hand-Roll: Pattern Matching (Phase 35)

**Compile to a decision tree.** Yorick Peterse's Rust implementation (https://github.com/yorickpeterse/pattern-matching-in-rust) is the reference for the algorithm; Jules Jacobs' 2021 paper ("How to compile pattern matching") is the textbook source.

For Flow's interpreter:
- Add `MatchExpression` and `PatternExpression` AST nodes
- Patterns: literal, variable binding, wildcard `_`, tuple `<<a, b>>`, list `[head, ...tail]`, chord/note destructure, guards (`when <bool-expr>`)
- Compiler: at parse time, build a decision tree (test most-discriminating column first); interpreter walks the tree against the scrutinee
- Exhaustiveness checking: nice-to-have, defer if scope-bloating
- No library — every part is < 500 LOC

**Syntax to specify** in Phase 35 plan (sketch):
```
match note {
  C4 => "do",
  D4 => "re",
  E4 => "mi",
  _  => "other"
}

match seq {
  | C4 E4 G4 | => "C major arpeggio",
  | <<head, ...tail>> => (str "starts with " head)
}
```

### Hand-Roll: Pure-Flow Test Framework (Phase 35)

**File-convention discovery, not attribute-based.** Rationale: Flow already has implicit-return semantics and no concept of attributes/decorators; the path of least friction is `tests/test_*.flow` convention + a new CLI subcommand.

| Surface | Behavior |
|---------|----------|
| `proc testFoo() { ... }` in any `.flow` file | Discovered as a test if name starts with `test` and proc takes 0 args |
| `(assert <bool> "message")` | New stdlib builtin |
| `(assertEqual <a> <b>)` | Builtin |
| `(assertNear <a> <b> tolerance)` | Builtin (already need for RMS regression) |
| `flow test [path]` | New CLI subcommand; runs every `testXxx` proc, prints pass/fail summary |

No new external dep. Builds on the existing CLI binary's 11-subcommand surface (becomes 12).

### Hand-Roll: Documentation Generator `flow doc` (Phase 41)

**Reject DocFX.** DocFX is for C# triple-slash comments + assemblies — it's designed for documenting the *Flow interpreter source*, not Flow programs. We want a tool that documents `.flow` files.

| Component | Behavior |
|-----------|----------|
| Source: `BuiltInDocs` table (already exists in `flow-lsp` for hover/SignatureHelp) | Built-in reference page |
| Source: `proc` declarations in user `.flow` files with optional `// doc:` comments above | User-defined function reference |
| Source: `examples/*.flow` | Tutorial pages with rendered audio links |
| Output: Markdown by default; HTML via a templated converter (use `Markdig` if any rendering needed) | |

Surface:
```
flow doc <path> [--format html|md] [--output <dir>]
```

This is a hand-rolled walker over the same AST the interpreter uses. ~500-1000 LOC. Markdig is the only candidate library and only if HTML rendering is in scope — pure Markdown emit needs nothing.

---

## Existing Stack (No Changes)

| Technology | Version | Purpose | Status |
|------------|---------|---------|--------|
| .NET 10 | net10.0 | Runtime | Unchanged |
| C# 13 | Latest | Language | Unchanged |
| PulseAudio (P/Invoke) | System | Linux audio playback | Will be extended in Phase 38 for capture (`PA_STREAM_RECORD` direction) |
| Melanchall.DryWetMidi | 8.0.3 | MIDI file R/W | Unchanged; remains the file-export library |
| OmniSharp.Extensions.LanguageServer | 0.19.x (existing in flow-lsp) | LSP server framework | Reused in Phase 38 for REPL completion via in-process embedding |
| Pidgin | (referenced, unused) | Parser combinator | Should be removed as a v1.5 housekeeping item (it's flagged in v1.0 stack research as removable) |

---

## Alternatives Considered

| Recommended | Alternative | When Alternative Wins |
|-------------|-------------|------------------------|
| `RtMidi.Core` 1.0.53 | `managed-midi` | Never — archived as "past project" |
| `RtMidi.Core` 1.0.53 | Hand-roll ALSA + CoreMIDI + WinMM P/Invoke | If we only needed one platform; we need three |
| Hand-roll phase vocoder | RubberBand via P/Invoke | If a Flow user files an RFC for studio-grade ±12-st pitch shift quality |
| Hand-roll Markov improv | Magenta RealTime | Never — Python-only, archived, no offline runtime |
| Vendored ABCSharp source | Hand-roll ABC parser | If ABCSharp turns out to be broken on a fixture (low risk; small grammar) |
| `OwnAudioSharp` (miniaudio) for macOS | Hand-roll CoreAudio P/Invoke | If miniaudio has unacceptable latency on macOS hardware (run Phase 41 Plan 1 smoke test first) |
| `NAudio.Wasapi` for Windows | Hand-roll WASAPI P/Invoke | If we want zero deps and have Phase 41 budget to burn — unlikely |
| `Rug.Osc` 1.2.5 | `OscCore` | If a feature gap in Rug.Osc emerges (unlikely; it covers full 1.0 spec) |
| `JackSharp` 0.4.0 | Hand-roll libjack P/Invoke | If JackSharp's API model conflicts with Flow's render path (low risk; the surface we need is small) |
| Blazor WASM | Uno Platform WASM | If we need cross-target XAML (we don't) |
| Hand-roll MusicXML emit (using vendored schemas) | A full `MusicXML.Writer` NuGet | None exists |

---

## What NOT to Use

| Avoid | Specific Problem | Use Instead |
|-------|------------------|-------------|
| **NAudio (full, not just NAudio.Wasapi)** | Windows-centric, would duplicate the hand-built audio pipeline — same conclusion as v1.0 stack research | NAudio.Wasapi only (one class, scoped to a single backend file) |
| **CSCore** | Windows-only; same overlap problem as NAudio | NAudio.Wasapi |
| **NWaves** | At v0.9.6 (Oct 2021), abandonment-flagged; would duplicate existing hand-rolled DSP | Hand-roll the phase vocoder + granulator on top of existing DSP primitives |
| **managed-midi** | Marked "Past project" on GitHub | RtMidi.Core 1.0.53 |
| **DryWetMidi for real-time MIDI device I/O** | Confirmed: Windows + macOS only, NO Linux/ALSA support | RtMidi.Core |
| **Magenta (Python)** | Archived read-only since 2026-01-06; Python-only | Hand-rolled chord-aware Markov baseline; reconsider Magenta.js or a successor in v1.6 if browser-side ML is desired |
| **Pidgin parser combinator** | Already in csproj, not used by the actual parser | Remove during a v1.5 housekeeping cleanup |
| **DocFX for `flow doc`** | Documents C# source, not Flow source | Hand-rolled Markdown emitter |
| **MusicXml.NET** | Parser only — does not write MusicXML | Vendored `sightreader/musicxml-schemas` POCOs + hand-rolled writer |
| **Ableton Link in proprietary distribution context** | GPLv2+ / commercial dual-license — needs legal review if Flow ships under MIT/Apache | Document the API surface; defer if license cleared with the Ableton team |
| **CC-BY-SA / CC-BY-NC native libraries** | Rejected per Phase 29 SPEC-2 precedent | MIT / Apache / BSD / CC-BY 4.0 only |

---

## Stack Patterns by Variant

**If shipping only the Linux build first (Phase 41 partial):**
- Use PulseAudio (existing); no Windows/macOS backends yet
- Use JackSharp (optional) for transport
- Use RtMidi.Core's ALSA backend for MIDI I/O
- Skip OwnAudioSharp + NAudio.Wasapi entirely

**If shipping a "Linux-only studio profile" forever:**
- All v1.5 phases ship; Phase 41 narrows to "Linux self-contained binary refresh + WASM playground only"
- Save ~3-5 days vs. cross-platform binaries

**If the WASM playground proves harder than budgeted:**
- Move it to v1.6
- Phase 41 still closes the milestone with cross-platform binaries + `flow doc` + JetBrains publish + third-genre showcase

**If `RtMidi.Core` Linux ALSA support has bugs (untested by upstream):**
- Build a thin ALSA-direct backend behind `IMidiBackend`
- Use RtMidi.Core for Windows + macOS only
- Mirrors the IAudioBackend pattern exactly

---

## Version Compatibility Matrix

| Package | Version | .NET 10 Compatible | Notes |
|---------|---------|---------------------|-------|
| Melanchall.DryWetMidi | 8.0.3 | YES | Confirmed in production at v1.4; v9.0.0-prerelease exists |
| RtMidi.Core | 1.0.53 | YES (via .NET Standard 2.0) | Native libs bundled |
| Rug.Osc | 1.2.5 | YES (via .NET Standard 2.0) | Zero deps |
| NAudio.Wasapi | 2.3.0 | YES | NAudio v2.3 explicitly added .NET 10 preview-4 feature support |
| JackSharp | 0.4.0 | YES (via .NET Standard 2.0) | Native libjack required at runtime |
| OwnAudioSharp | 1.0.68 | YES | Bundles miniaudio natives |
| KristofferStrube.Blazor.WebAudio | latest | YES | Blazor WASM target |
| FftFlat | 1.0.1 (if used) | YES | Pure C# |
| ABCSharp | vendored from GitHub | YES | Single .cs file dependency |
| sightreader/musicxml-schemas | vendored | YES | Pure POCO classes |

---

## Installation

```bash
# Already in flow-lang.csproj — no change
# <PackageReference Include="Melanchall.DryWetMidi" Version="8.0.3" />

# Phase 38 (OSC + audio input)
dotnet add flow-lang package Rug.Osc --version 1.2.5

# Phase 40 (real-time MIDI; JACK optional)
dotnet add flow-lang package RtMidi.Core --version 1.0.53
dotnet add flow-lang package JackSharp --version 0.4.0      # optional, Linux-only feature

# Phase 41 (Windows backend; macOS backend; WASM playground)
dotnet add flow-lang package NAudio.Wasapi --version 2.3.0  # Windows backend only
dotnet add flow-lang package OwnAudioSharp --version 1.0.68 # macOS (and optionally cross-platform)
dotnet add flow-wasm package KristofferStrube.Blazor.WebAudio  # new flow-wasm project

# Optional (only if profiling demands)
dotnet add flow-lang package FftFlat --version 1.0.1        # Phase 37 phase vocoder

# Vendored sources (no NuGet)
# Drop into flow-lang/External/:
#   - musicxml-schemas/*.cs (Phase 39 MusicXML export scaffolding)
#   - ABCSharp/*.cs (Phase 39 ABC import)
```

---

## Phase-by-Phase Dependency Introduction Plan

| Phase | New NuGet | New native dep | New vendored source | Removed deps |
|-------|-----------|----------------|---------------------|---------------|
| 35 (language foundation) | — | — | — | (optional) Pidgin cleanup |
| 36 (sequence algebra + generative + improv) | — | — | — | — |
| 37 (sound design) | — | — | — | — |
| 38 (live coding 2.0) | `Rug.Osc` | (extends existing PulseAudio P/Invoke for capture) | — | — |
| 39 (notation citizenship) | — | — | `ABCSharp/*.cs`, `musicxml-schemas/*.cs` | — |
| 40 (studio sync) | `RtMidi.Core`, `JackSharp` (optional) | `librtmidi` (bundled by NuGet); `libabletonlink` (optional P/Invoke) | — | — |
| 41 (reach + closer) | `NAudio.Wasapi`, `OwnAudioSharp`, `KristofferStrube.Blazor.WebAudio` | `miniaudio` (bundled by OwnAudioSharp) | — | (optional) Pidgin cleanup if not done in 35 |

**Phase 35–37 add zero new dependencies.** Phase 38 onward begins the dep-add cycle, each scoped behind an interface seam (`Rug.Osc` behind a new `OscModule`; `RtMidi.Core` behind `IMidiBackend`; `NAudio.Wasapi` / `OwnAudioSharp` behind `IAudioBackend`; Blazor.WebAudio inside a new `flow-wasm` project that doesn't compile on desktop).

---

## Integration Points with Existing Codebase

### Reuses existing `BarData` / `MusicalNoteData` / `Sequence` / `Section` / `Song`
- MusicXML emit (Phase 39)
- LilyPond emit (Phase 39)
- ABC import (Phase 39) → produces these structures
- MML import (Phase 39) → produces these structures
- Multi-track real-time MIDI output (Phase 40) reuses the same per-instrument routing logic as the existing `MidiExport`

### Reuses existing `IAudioBackend` abstraction
- `WasapiBackend.cs` (Phase 41 — Windows)
- `CoreAudioBackend.cs` or `MiniAudioBackend.cs` (Phase 41 — macOS)
- `WebAudioBackend.cs` (Phase 41 — Blazor WASM)
- `PulseAudioCaptureBackend` extension (Phase 38 — audio input via `PA_STREAM_RECORD`)

### Adds new `IMidiBackend` abstraction (mirrors `IAudioBackend` pattern)
- `RtMidiBackend.cs` (Phase 40 — primary cross-platform impl)
- Future: `AlsaMidiBackend.cs` if RtMidi.Core's ALSA proves flaky

### Adds new `ITransportSync` abstraction
- `MidiClockSync.cs` (Phase 40 — hand-rolled)
- `AbletonLinkSync.cs` (Phase 40 optional — P/Invoke wrapper)
- `JackTransportSync.cs` (Phase 40 optional — `JackSharp`)

### Reuses existing `MusicalContext` stack
- `live { ... }` block (Phase 38) pushes/pops onto the same stack — same lifecycle pattern as `tempo` / `key` / `tuning`
- New `voicePool` interaction with the live-mode hot-swap

### Reuses existing `BuiltInDocs` lookup table
- REPL `?fn` inline help (Phase 38)
- `flow doc` documentation generator (Phase 41)
- LSP-backed REPL completion (Phase 38)

### Reuses existing seeded-RNG infrastructure (`ExecutionContext.SeededRandom`)
- Markov / L-system / CA / Lorenz / Granulator / improvise — all route through the same seeded RNG path to preserve the two-run cmp-clean determinism contract

---

## Licensing Summary

| Dependency | License | Compatible with Flow's distribution? |
|------------|---------|--------------------------------------|
| .NET 10, C# 13 | MIT | YES |
| DryWetMidi 8.0.3 | MIT | YES (already in v1.4) |
| RtMidi.Core 1.0.53 | MIT (wrapper) + MIT (librtmidi) | YES |
| Rug.Osc 1.2.5 | MIT-style permissive | YES |
| NAudio.Wasapi 2.3.0 | Microsoft Public License (Ms-PL) | YES (permissive; commercial-friendly) |
| JackSharp 0.4.0 | LGPL/MIT (wrapper); libjack itself LGPL | YES (dynamic linking) |
| OwnAudioSharp 1.0.68 | MIT (wrapper); miniaudio public domain | YES |
| Blazor.WebAudio | MIT | YES |
| FftFlat | MIT | YES |
| ABCSharp (vendored) | License needs verification — flag for Phase 39 plan | TBD |
| musicxml-schemas (vendored) | License needs verification — flag for Phase 39 plan | TBD |
| **Ableton Link** | GPLv2+ OR proprietary | **NEEDS LEGAL REVIEW** — gating dep for Phase 40 stretch |
| **RubberBand** (rejected) | GPL OR commercial | Rejection avoids the legal hazard |
| **Magenta** (rejected) | Apache-2.0 — but Python-only and archived | Moot |

**Action item for v1.5 kickoff:** verify ABCSharp + musicxml-schemas licenses before Phase 39; verify Ableton Link's license posture before Phase 40 (consider treating Link as a "post-v1.5 community-contributable stretch").

---

## Sources

- Existing v1.0 / v1.4 Flow stack research (`.planning/research/STACK.md` superseded sections; `flow-lang/flow-lang.csproj` for current deps)
- [Melanchall.DryWetMidi 8.0.3 NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) — confirmed current; .NET 10 compatible
- [DryWetMidi Supported OS doc](https://melanchall.github.io/drywetmidi/articles/dev/Supported-OS.html) — **confirmed: Windows + macOS only for device I/O; NO Linux/ALSA**
- [DryWetMidi Output device doc](https://melanchall.github.io/drywetmidi/articles/devices/Output-device.html)
- [RtMidi.Core 1.0.53 NuGet](https://www.nuget.org/packages/RtMidi.Core) — cross-platform MIDI; .NET Standard 2.0
- [RtMidi.Core GitHub](https://github.com/micdah/RtMidi.Core) — last GitHub release v1.0.51 was Oct 2020 (NuGet shows 1.0.53 published since)
- [Rug.Osc 1.2.5 NuGet](https://www.nuget.org/packages/Rug.Osc) — stable, zero deps, OSC 1.0 complete
- [NAudio.Wasapi 2.3.0 NuGet](https://www.nuget.org/packages/NAudio.Wasapi/) — .NET 10 preview-4 supported
- [NAudio GitHub](https://github.com/naudio/NAudio) — Windows audio library
- [NAudio WasapiOut docs](https://github.com/naudio/NAudio/blob/master/Docs/WasapiOut.md)
- [JackSharp 0.4.0 NuGet](https://www.nuget.org/packages/JackSharp) — .NET binding for libjack
- [JackSharp GitHub](https://github.com/residuum/JackSharp) — provides Processor + Controller
- [OwnAudioSharp 1.0.68 NuGet](https://www.nuget.org/packages/OwnAudioSharp/1.0.68) — miniaudio C# binding
- [libsoundio GitHub](https://github.com/andrewrk/libsoundio) — alternative cross-platform audio
- [Blazor.WebAudio GitHub](https://github.com/KristofferStrube/Blazor.WebAudio) — Blazor Web Audio API wrapper
- [Blazor in .NET 10 release notes](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0) — Native AOT for WASM compilation
- [.NET 10 WebAssembly improvements](https://darthpedro.net/2025/10/02/blazor-wasm-in-net-10-has-faster-startup/) — 76% js bundle reduction; AOT for WASM
- [FftFlat NuGet](https://www.nuget.org/packages/FftFlat) — fast pure-C# FFT (4× Math.NET)
- [Magenta GitHub archive notice](https://github.com/magenta/magenta) — archived read-only as of 2026-01-06
- [Magenta RealTime](https://magenta.withgoogle.com/magenta-realtime) — 2025 model; requires TPU
- [MusicXml.NET 3.1.0 NuGet](https://www.nuget.org/packages/MusicXml.NET) — parser only; insufficient for export
- [sightreader/musicxml-schemas](https://github.com/sightreader/musicxml-schemas) — XSD-generated C# POCOs (MusicXML 2.0/3.0/3.1)
- [matthewcpp/ABCSharp](https://github.com/matthewcpp/ABCSharp) — C# ABC notation parser; vendor as source
- [Wikipedia: L-system](https://en.wikipedia.org/wiki/L-system) — algorithm reference
- [Manousakis "Musical L-Systems" 2006 thesis](https://modularbrains.net/wp-content/uploads/Stelios-Manousakis-Musical-L-systems.pdf) — musical L-system mapping reference
- [Tidal Cycles `every` / `fast` / `slow` / `chunk` docs](https://tidalcycles.org/docs/reference/alteration/) — pattern algebra reference for Phase 36
- [Granular synthesis parameter consensus (Output)](https://output.com/blog/granular-synthesis) — grain / density / jitter / windowing surface
- [Granular synthesis (Native Instruments)](https://blog.native-instruments.com/granular-synthesis/) — supporting reference
- [Sound on Sound: Granular Synthesis](https://www.soundonsound.com/techniques/granular-synthesis) — definitive parameter set
- [Phase vocoder tutorial (CMU)](https://www.cs.cmu.edu/~music/nyquist/extensions/pvoc/phasevocoder.html) — algorithm reference
- [Laroche-Dolson 1999 phase vocoder paper](https://www.ee.columbia.edu/~dpwe/papers/LaroD99-pvoc.pdf) — pitch shift technique
- [Jules Jacobs "How to compile pattern matching" 2021](https://julesjacobs.com/notes/patternmatching/patternmatching.pdf) — decision tree compilation
- [yorickpeterse/pattern-matching-in-rust](https://github.com/yorickpeterse/pattern-matching-in-rust) — reference implementation
- [JetBrains Marketplace publishing docs](https://plugins.jetbrains.com/docs/intellij/publishing-plugin.html) — Gradle publishPlugin workflow
- [JetBrains plugin signing docs](https://plugins.jetbrains.com/docs/intellij/plugin-signing.html) — required since 2021.2
- [PulseAudio Simple API](https://freedesktop.org/software/pulseaudio/doxygen/simple.html) — `pa_simple_new` with `PA_STREAM_RECORD` for capture
- [Ableton Link GitHub](https://github.com/Ableton/link) — GPLv2+ / proprietary dual license
- [RubberBand library](https://breakfastquay.com/rubberband/) — GPL / commercial dual license (rejected)
- [DocFX docs](https://dotnet.github.io/docfx/) — rejected for `flow doc`; documents C# not Flow

---
*Stack research for: Flow Language v1.5 Stage, Studio, Web*
*Researched: 2026-05-18*
*Confidence: HIGH (4-6 new deps, all scoped behind interface seams; majority of phases ship with hand-rolled C# only)*
