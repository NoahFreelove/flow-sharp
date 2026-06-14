# Flow Language (flow-lang)
A music production language.

### AI Disclaimer
This entire repo was vibecoded with the help of the [GSD Framework](https://github.com/gsd-build/get-shit-done) and Claude Opus.
I did direct the features and testing so it was mainly supervised - but expect bugs to appear.


# What is flow-lang?
Flow-lang is a statically moderately-strongly typed functional interpreted language. The goal of flow-lang is to make a tool for code-minded folk like myself to create music in a fun way. The goal is also to not prefer one genre over another. You should be able to make rock, pop, jazz, or a symphony, all in one place - and all in the same buffer.

Flow-lang prioritizes ergonomics over almost everything. This language is interpreted, its not fast, and its not trying to be fast (though it takes the easy wins where possible). 

Many operations that would be errors in some languages are not in flow-lang because it always takes the most cheritable interpretation of your code. You could call this the JavaScript approach though I don't think we're as vulgar as JavaScript's type coercion. For example:
```flow
use "@audio"
Buffer input = (createSineTone 440Hz 1.0 0.5)
Buffer wet = (reverb input 5.0 5.0 0.3)
//                          ^    ^    ^
//                       roomSize, damping, mix. flow-lang clamps all to [0, 1.0]
```
Flow-lang will silently fix this stuff for you. So you can use variables in position of arguments pretty freely without worrying about adjusting one variable means it being out of domain for some other function where you use it.

## What ISN'T flow-lang?
Flow-lang is not AI generated music. Flow lang is just a way to generate music. You still have to place the notes and make the samples, just how you would in a standard DAW except you use code. This is completely different to how AI generated music is created.

You could use AI to create `.flow` files but this still isn't really AI generated music, its more *vibecoded music* I suppose. As much as I love claude, it cannot generate anything super pleasant sounding in flow-lang yet (sorry claude!).

Flow-lang is also not trying to do some crazy GPU accelerated parallel rendering pipeline stuff. I'm not trying to optimize the hell out of flow-lang.

I hope my direction on where I want flow-lang to go was clear. If it has to be one sentence: `Flow-lang prioritizes the development experience and the artist regardless of the performance of the program.`

## Features
See [FEATURES.md](./FEATURES.md) for a complete list of features.

## Showcase

Flow is **genre-agnostic** — the same language and the same buffer make a
classical symphony, a ragtime/jazz improvisation, or a four-on-the-floor EDM
drop. The v1.5 release ships three curated showcase pieces that prove it:

| Genre | Source | What it shows off |
|-------|--------|-------------------|
| Classical (symphony) | [`examples/showcase.flow`](./examples/showcase.flow) | Polyrhythmic tuplets, voice-led progressions, the full effects chain |
| Jazz (generative) | [`examples/generative/markov_jazz.flow`](./examples/generative/markov_jazz.flow) | Markov train/generate, chord-aware `jam`, `@patterns` combinators |
| **EDM** (v1.5 closer) | [`examples/edm/pulse.flow`](./examples/edm/pulse.flow) | The five-primitive feature checklist below |

**`examples/edm/pulse.flow`** is a ~60-second EDM piece (eight four-bar sections
at 128 BPM) that exercises the v1.5 headline surface in one file:

1. **Pattern matching** (Phase 35) — `(match idx ...)` selects the bassline motif per section.
2. **Generative rhythm** (Phase 36) — a seeded `(euclidean 7 16 ...)` kick/clap groove.
3. **Granular DSP** (Phase 37) — `(granular ...)` builds the riser texture.
4. **Live coding** (Phase 38) — a `live 1bar { ... }` hot-swap block (demo section).
5. **Real-time MIDI** (Phase 40) — `midiOut(song, port)` streams to hardware/DAW (demo section).

The file is split into a **pinned offline render** and a **live/real-time demo**.
The pinned render (`writeWav` + `writeMidi`) is fully seeded, so two consecutive
renders are byte-identical (two-run cmp-clean) and the WAV holds an RMS-windowed
regression (±0.5 dB / 100 ms, SPEC-8) against a committed baseline. The `live`
block and real-time `midiOut` opt out of that determinism contract by design, so
they live in a clearly-commented demo section that a headless render never runs —
uncomment them and run under `flow watch` / a real MIDI rig to hear them.

```bash
# Render the deterministic offline version (WAV + MIDI):
dotnet run --project flow-cli -- run examples/edm/pulse.flow
#   → /tmp/pulse.wav + /tmp/pulse.mid

# Verify two-run cmp-clean:
bash scripts/test_two_run_determinism.sh examples/edm/pulse.flow \
  --render-cmd "dotnet run --project flow-cli -- render <SCRIPT> -o <OUT>"
```

The rendered audio for all three pieces ships in the **v1.5.0 GitHub Release**
alongside the cross-platform binaries (the Release itself is a human-pushed gate,
not cut automatically).

## Install (Linux & macOS)

`scripts/install.sh` auto-detects your platform (linux-x64, linux-arm64, osx-x64, osx-arm64) and verifies the sha256 sidecar from the GitHub release.

Per-user install (no sudo), from a local checkout:

```bash
bash scripts/install.sh
```

System-wide install:

```bash
sudo bash scripts/install.sh --system
```

The script downloads the matching `flow-<rid>-v1.5.0.tar.gz` from GitHub Releases (or uses `--local-tarball` for offline), verifies the sha256 sidecar, and installs a self-contained `flow` binary to either `~/.local/share/flow/` + symlink at `~/.local/bin/flow` (per-user, default) or `/usr/local/share/flow/` + symlink at `/usr/local/bin/flow` (system-wide). Re-running upgrades in place; `scripts/uninstall.sh` removes everything except your `~/.config/flow/config.toml`.

**Windows:** download `flow-win-x64-v1.5.0.zip` from the [GitHub Releases page](../../releases) and add the extracted directory to your PATH.

## CLI subcommands

| Subcommand | What it does |
|---|---|
| `flow run script.flow` | Run a Flow script |
| `flow eval "expr"` | Evaluate one expression |
| `flow repl` | Interactive REPL (auto-imports `@std @audio @collections`) |
| `flow watch script.flow` | Auto-reload on file change |
| `flow play script.flow` | Render + play via audio backend |
| `flow render script.flow -o out.wav` | Render to WAV |
| `flow flow2midi script.flow -o out.mid` | Render to MIDI |
| `flow midi2flow input.mid -o out.flow` | Convert MIDI → round-trippable Flow source |
| `flow check script.flow` | Parse + type-check only |
| `flow new piece-name` | Scaffold a new piece |
| `flow lsp` | Start the Flow Language Server (stdio) |
| `flow test [path]` | Run `test_*.flow` files via the pure-Flow test framework |
| `flow doc [--out dir]` | Generate browsable reference docs |
| `flow version` | Print version (reports 1.5.0) |

Config lives at `~/.config/flow/config.toml`. Optional keys: `default_tempo`, `default_timesig`, `default_audio_device`, `stdlib_search_path`. See `scripts/install.sh` for the schema.

## Bugs?
This tool is just for fun, not any serious professional work. Bug reports may or may not be addressed.

## Editor support
Flow ships with a **Language Server (`flow-lsp`)**:

### VSCode / Cursor / VSCodium / Windsurf

Install the **Flow Language** extension which is bundled with this repo. Its not on the marketplace as of now.

### Emacs, Neovim, and other LSP editors

The `flow-lsp` server speaks plain LSP 3.17 over stdio, so any editor
with an LSP client can drive it. See
[`docs/editor-setup/`](./docs/editor-setup/README.md) for per-editor
config snippets (Neovim `nvim-lspconfig`, Helix `languages.toml`,
Emacs `lsp-mode`/`eglot`) and binary install guidance.
