<!-- generated-by: gsd-doc-writer -->
# Flow Language

> **A statically-typed, interpreted programming language for music production.**
> Compose with code, render to WAV or MIDI, play through your speakers — all from one `.flow` file.

Flow is an interpreted, statically-typed language built around music. It ships with a flow operator (`->`) for chaining, music-aware types (`Note`, `Chord`, `Sequence`, `Song`, `Tuning`, `Sfz`, ...), inline note streams (`| C4 D4 E4 |`), musical context blocks (`tempo`, `key`, `timesig`, `swing`, `voicePool`, `tuning`), the Phase 33 SFZ orchestral sampler, microtonal/Scala support, generative primitives (Markov, L-systems, Tidal-style combinators), and a full audio pipeline from composition through WAV/MIDI/MusicXML/LilyPond export. The interpreter is C# on .NET 10.

**Status:** v1.4 shipped 2026-05-16 (GitHub release + two showcase pieces). Currently iterating toward v1.5. The project is pre-traction — see [Status & versioning](#status--versioning) below.

### AI disclaimer

This entire repo was vibecoded with the help of the [GSD Framework](https://github.com/gsd-build/get-shit-done) and Claude Opus. I directed the features and testing so it was mostly supervised, but expect bugs.

---

## What makes Flow different

- **Composer ergonomics over everything.** Runtime efficiency, type strictness, and generality all yield to making the easy musical case fast and the awkward case still possible.
- **Genre-agnostic by design.** Rock, jazz, classical, EDM, ragtime, death metal — no genre is privileged in the syntax or stdlib.
- **Charitable interpretation.** Out-of-range arguments get clamped with an advisory rather than thrown — `reverb(buf, 5.0, 5.0, 5.0)` Just Works because the room/damping/mix args clamp to `[0, 1]`. JavaScript-without-the-vulgarity.
- **One file, full pipeline.** From note literals to a played-back stereo WAV in the same script, no DAW round-trip.

### What Flow is *not*

- **Not AI-generated music.** You still write the notes. Flow is a notation + synthesis + DSP toolkit you drive with code.
- **Not a general-purpose language.** It computes, but features are accepted only if they serve musical use.
- **Not chasing C-level performance.** It's interpreted; the easy paths are fast, but speed isn't the headline.

---

## Install (Linux x64)

Per-user install (no sudo), from a local checkout:

```bash
bash scripts/install.sh
```

System-wide install:

```bash
sudo bash scripts/install.sh --system
```

The script copies a self-contained `flow` binary (~38 MB) to either `~/.local/share/flow/` + symlink at `~/.local/bin/flow` (per-user, default) or `/usr/local/share/flow/` + symlink at `/usr/local/bin/flow` (system-wide). Re-running upgrades in place; `scripts/uninstall.sh` removes everything except your `~/.config/flow/config.toml`.

**Platforms:** Linux x64 with PulseAudio is the primary target. macOS and Windows ship the LSP (`flow-lsp`) today; full audio playback on those platforms is on the v1.5+ backlog (the `IAudioBackend` abstraction exists, just no Core Audio / WASAPI backend yet).

---

## Hello, music

Here is a short cadence in C major — chord progression, melody over the top, rendered to WAV through the piano synthesizer:

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                // Right hand: an I–IV–V–I melody over four bars
                Sequence melody = | C4q E4q G4q C5q | A4q F4q D4q B3q | G4q B4q D5q G5q | C5w |

                // Left hand: roman numerals resolve from the active key
                Sequence chords = | [I]h [IV]h | [vi]h [V7]h | [I]w |
            }

            Song piece = [intro]
            Buffer audio = (renderSong piece "piano")
            (writeWav audio "hello.wav")
        }
    }
}
```

Run it:

```bash
flow run hello.flow      # parse + execute
flow play hello.flow     # render + play through PulseAudio
flow render hello.flow -o hello.wav
```

That is the whole program. No DAW, no plugin host, no MIDI wiring.

---

## Showcase

Two pieces shipped in v1.4 (2026-05-16), both rendered entirely from Flow source against the VSCO Community CE 1.1.0 SFZ library via the Phase 33 orchestral sampler:

- **[`examples/symphony/symphony.flow`](examples/symphony/symphony.flow)** — *In Five Voices*, a pensive ~60s ABA symphony in D minor for five orchestral instruments (violin, cello, flute, horn, timpani).
- **[`examples/ragtime/ragtime.flow`](examples/ragtime/ragtime.flow)** — *Stride & Stomp*, an upbeat ~58s solo-piano ragtime in F major.

Same interpreter, same SFZ pipeline, opposite moods — the genre-agnostic claim in one release.

**Listen:** rendered audio + reproduction docs are attached to the [v1.4.0 GitHub release](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0).

---

## Feature highlights

See **[FEATURES.md](FEATURES.md)** for the comprehensive, status-tagged inventory. A few categories worth calling out:

- **Music-first syntax** — note streams (`| C4q E4q G4q |`) with durations, rests, dotted/tied notes, cent offsets, chord brackets, articulations (`>`, `stacc`, `ten`, `marc`, `leg`), random choice (`(? C4 E4 G4)`); chord literals (`Cmaj7`, `F#dim`, `Bb7`); roman numerals (`I`, `ii`, `V7`) that resolve from the active `key` block; voice-block polyphony (`| {voice C4w}{voice E4q G4q} |`).
- **Full audio pipeline** — 9 built-in synthesizers (piano, brass, sax, drums, bell, flute, organ, strings, wavetable, the tonal six sample-backed by University of Iowa MIS); DSP effects (reverb, low/high/bandpass filters, compressor with sidechain, delay, gain in dB, volume linear, constant-power stereo pan); WAV export (16/24/32-bit) and multi-track MIDI export with GM program routing.
- **SFZ orchestral sampler** (Phase 33) — `use "@sfz"`, then `Sfz violin = (loadSfz #violin)` resolves a 20-entry GM dict against your VSCO-CE install. Render via `renderSong song "sampler:violin"`. Common-subset SFZ parser, per-region sustain looping with equal-power crossfade, round-robin and velocity-layer crossfade opcodes.
- **Microtonal / Scala** (Phase 32) — `enable justIntonation;` / `pythagorean;` / `equalTemperament;` pragmas, plus `tuning t { ... }` blocks backed by `(loadScala "x.scl")` for Carlos Alpha, Bohlen-Pierce, Partch, and friends. KBM keyboard mappings supported.
- **Generative composition** (Phase 36) — Markov chains (`markov` / `markovTrain` / `markovGenerate`), Lindenmayer systems (`lsystem`), 1D cellular automata + Conway's Life, chaos maps (Lorenz, logistic), 13 Tidal-style combinators on `Sequence` (`every`, `fast`, `slow`, `chunk`, `jux`, `sometimes`, `degrade`, ...), and `(jam over=chords style=#jazz length=8)` chord-aware improvisation with composer-editable Flow-file style packs.
- **Notation I/O** (Phase 39) — `(writeMusicXML "out.musicxml" song)` MuseScore-compatible export; `(writeLilyPond "out.ly" song)` for engraving; ABC 2.1 + abc2midi import via `(abc "...")`; PC-98 MML import via `(mml "...")`.
- **Editor support** — `flow-lsp` speaks LSP 3.17 over stdio. A bundled VSCode/Cursor/VSCodium/Windsurf extension lives in `vscode-extension/` (not yet on the marketplace). Per-editor config snippets for Neovim, Helix, Emacs are at [`docs/editor-setup/`](docs/editor-setup/README.md).

---

## CLI subcommands

| Subcommand | What it does |
|---|---|
| `flow run script.flow` | Run a Flow script |
| `flow eval "expr"` | Evaluate one expression |
| `flow repl` | Interactive REPL (auto-imports `@std @audio @collections`) |
| `flow watch script.flow` | Auto-reload on file change |
| `flow play script.flow` | Render + play via PulseAudio |
| `flow render script.flow -o out.wav` | Render to WAV |
| `flow flow2midi script.flow -o out.mid` | Render to MIDI |
| `flow midi2flow input.mid -o out.flow` | Convert MIDI → round-trippable Flow source |
| `flow check script.flow` | Parse + type-check only |
| `flow new piece-name` | Scaffold a new piece |
| `flow version` | Print version |

Config lives at `~/.config/flow/config.toml`. Optional keys: `default_tempo`, `default_timesig`, `default_audio_device`, `stdlib_search_path`, `sfz_root`. See `scripts/install.sh` for the schema.

---

## Wiki & learning

The [`wiki/`](wiki/) directory ships 26 tutorial chapters covering everything from language basics to song structure, generative composition, and audio export. Good entry points:

- **[Quick-Start.md](wiki/Quick-Start.md)** — install, build, first script
- **[Language-Basics.md](wiki/Language-Basics.md)** — variables, types, operators, scoping
- **[Note-Streams.md](wiki/Note-Streams.md)** — the `| C4 D4 E4 |` syntax in depth
- **[Musical-Context.md](wiki/Musical-Context.md)** — `tempo`, `key`, `timesig`, `swing`, ...
- **[Song-Structure.md](wiki/Song-Structure.md)** — sections, arrangements, repeats
- **[Generative.md](wiki/Generative.md)** — Markov, L-systems, Euclidean rhythms
- **[Playback-and-Export.md](wiki/Playback-and-Export.md)** — `play`, `writeWav`, `writeMidi`

A full walkthrough lives in [`examples/tutorial.flow`](examples/tutorial.flow) — run it directly to step through every major feature.

---

## Build from source

Two projects in the solution:

- **`flow-lang/`** — core language library (`FlowLang` namespace)
- **`flow-interpreter/`** — console app for REPL + script execution
- **`flow-cli/`** — the `flow` user-facing CLI
- **`flow-lsp/`** — LSP 3.17 language server
- **`flow-midi/`** — MIDI import/export

```bash
# Build the solution
dotnet build

# Run a .flow script directly
dotnet run --project flow-interpreter examples/tutorial.flow

# Run the user-facing CLI without installing
dotnet run --project flow-cli -- render examples/ragtime/ragtime.flow -o ragtime.wav

# REPL
dotnet run --project flow-interpreter

# Run all .flow tests (no unit test framework — tests are .flow scripts)
for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done
```

Architecture deep-dive, AST node reference, and contributor conventions live in **[CLAUDE.md](CLAUDE.md)**.

---

## Status & versioning

Flow shipped publicly at v1.4 on 2026-05-16 and is currently iterating toward v1.5. It is **pre-traction** — there are no known external composers writing `.flow` code yet. Per the locked decision `D-v1.5-01`, the project still operates under the no-deprecation latitude: **breaking syntax/builtin changes still ship in single commits**, with in-repo migrators, no `flow migrate` CLI subcommand yet. That contract flips when a non-author composer files an issue/PR with their own `.flow` code, when a third-party fork appears, or when Flow ships to a package registry (NuGet / Homebrew / AUR / apt PPA). See `project_pre_public_no_legacy_burden.md`.

**Two-run determinism** is preserved: consecutive runs of the same script at the same git SHA produce byte-identical WAV output (chaos primitives `lorenz` / `logistic` excepted — same-platform only).

---

## Bugs

This tool is "for fun" software, not professional production gear. Bug reports may or may not be addressed — feel free to file them, but no SLA.

---

## License & attribution

Flow itself is released under the **GNU General Public License v3.0** — see [LICENSE](LICENSE).

The bundled sampled-instrument library at `flow-lang/Samples/` (3.05 MB / 21 WAVs covering piano, brass, sax, strings, flute, bell) is sourced from the **University of Iowa Musical Instrument Samples** project under **CC-BY 4.0**. Per-instrument attribution lives at `flow-lang/Samples/{instrument}/LICENSE.md`; the bundle-wide credit is at `flow-lang/Samples/CREDITS.md`.

The optional SFZ orchestral library used by the v1.4 showcase pieces is **VSCO Community Edition 1.1.0** (CC-BY 4.0). It is **not bundled** — installation is documented at `examples/symphony/README.md`.

Sole external NuGet dependency at runtime: `Melanchall.DryWetMidi 8.0.3` (MIT) for Standard MIDI File writing.
