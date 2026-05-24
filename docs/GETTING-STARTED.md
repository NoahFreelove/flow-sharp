<!-- generated-by: gsd-doc-writer -->
# Getting Started with Flow

Welcome. Flow is a statically-typed programming language for music
production — you write `.flow` source, the interpreter renders it through a
full audio pipeline, and you hear or export the result. This guide takes
you from a blank terminal to a real piece of music in about ten minutes.

If you've never installed a CLI tool before, that's fine — every step
below is copy-pasteable.

## Prerequisites

- **Operating system:** Linux x64 is the primary supported platform.
  macOS and Windows can build and run the LSP today, but audio playback
  on those platforms is a v1.5+ backlog item (the `IAudioBackend`
  abstraction exists; just no Core Audio / WASAPI backend yet).
- **Audio server:** PulseAudio, or PipeWire with the PulseAudio
  compatibility shim (`pipewire-pulse`). Flow's playback backend calls
  `libpulse-simple` via P/Invoke — PipeWire's pulse shim works
  transparently on modern Fedora / Ubuntu / Arch desktops.
- **.NET 10 SDK** — only required if you build from source. The
  prebuilt tarball ships a self-contained ~38 MB `flow` binary with no
  runtime dependency on `dotnet`.
- **Terminal basics:** you can `cd` into a directory and run `bash`
  scripts.

Quick check that audio is alive:

```bash
# Should print one or more sinks; if it errors, install pulseaudio-utils
# or pipewire-pulse for your distro
pactl list short sinks
```

## Install

There are three install paths. Pick one.

### Path A — Prebuilt tarball (recommended)

The `scripts/install.sh` installer drops a self-contained Linux x64
binary at `~/.local/share/flow/flow-v<version>/flow` and symlinks it
into `~/.local/bin/flow`. No `sudo`, no `dotnet` SDK needed.

```bash
git clone https://github.com/NoahFreelove/flow-sharp
cd flow-sharp
bash scripts/install.sh
```

For a system-wide install at `/usr/local/share/flow/` + `/usr/local/bin/flow`:

```bash
sudo bash scripts/install.sh --system
```

The installer is idempotent — re-running upgrades in place via `ln -sfn`
and a version-stamped install dir. It never overwrites an existing
`~/.config/flow/config.toml`. To remove everything except your config,
run `scripts/uninstall.sh`.

If `~/.local/bin` is not already on your `$PATH`, the installer prints
a one-line `export PATH=...` snippet for your `~/.bashrc` or
`~/.zshrc`. Add it, then `source` the shell file or open a new terminal.

Confirm the install:

```bash
flow version
# → flow 0.1.0-phase30
```

### Path B — Build from source

If you want to hack on the interpreter, you'll need the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/NoahFreelove/flow-sharp
cd flow-sharp
dotnet build
```

Run scripts directly with:

```bash
dotnet run --project flow-cli -- run path/to/script.flow
dotnet run --project flow-cli -- render path/to/script.flow -o out.wav
```

### Path C — REPL only (no install)

To poke at Flow interactively without installing anything, run the
interpreter project directly:

```bash
dotnet run --project flow-interpreter
```

This drops you straight into the REPL with `@std`, `@audio`, and
`@collections` auto-imported. Identical behaviour to `flow repl` once
you've installed.

## Your first program — a sine tone

Create a file called `hello-tone.flow` anywhere on your filesystem:

```flow
use "@std"
use "@audio"

(print "hello")

// 1 second sine wave at A4 (440 Hz), amplitude 0.3
Buffer tone = (createSineTone 1.0 440.0 0.3)

(play tone)
```

Run it:

```bash
flow run hello-tone.flow
```

You should see `hello` printed and hear a one-second 440 Hz tone. If
nothing came out of your speakers, jump to [Common first-time
issues](#common-first-time-issues) below.

The signature is `createSineTone(duration: Double, frequency: Double,
amplitude: Double) -> Buffer`. Amplitude is a 0.0–1.0 linear scale —
0.3 is comfortably quiet. There's also a `Hertz`-typed overload that
accepts `440Hz` and `1.5kHz` literals if you prefer named units.

## Your first piece — a C major cadence to WAV

Now something recognizably musical: a four-bar I–IV–V–I in C major,
rendered through the sample-backed piano, written to disk as a WAV
file.

Create `cadence.flow`:

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section cadence {
                // Right-hand melody — quarter notes outlining each chord
                Sequence melody = | C4q E4q G4q C5q | F4q A4q C5q F5q | G4q B4q D5q G5q | C5w |

                // Left-hand chords — roman numerals resolve against the active key
                Sequence chords = | [I]w | [IV]w | [V]w | [I]w |
            }

            Song piece = [cadence]
            Buffer audio = (renderSong piece "piano")
            (writeWav "cadence.wav" audio)
            (print "wrote cadence.wav")
        }
    }
}
```

Render and play:

```bash
flow run cadence.flow          # writes cadence.wav, no playback
flow play cadence.flow         # writes cadence.wav AND plays it
```

What's happening:

- `tempo 100 { ... }` / `timesig 4/4 { ... }` / `key Cmajor { ... }`
  are **musical context blocks**. They push state on the active context
  stack — nested blocks inherit and override naturally.
- `| C4q E4q ... |` is a **note stream**. Pitches are scientific
  notation (`C4` = middle C); `q` / `h` / `w` / `e` / `s` are quarter
  / half / whole / eighth / sixteenth durations.
- `[I]w` is a **roman-numeral chord bracket** — `I` resolves to a C
  major triad because the active `key` block is `Cmajor`. `w` makes it
  a whole note.
- `section cadence { ... }` groups named `Sequence` variables. A
  `Song` (e.g. `[cadence]`) arranges sections in order — repeat with
  `*N`, e.g. `[intro verse*2 chorus]`.
- `(renderSong piece "piano")` renders the song through the sampled
  piano. Other instrument names: `"strings"`, `"organ"`, `"bell"`,
  `"brass"`, `"sax"`, `"flute"`, `"drums"`, `"wavetable"`, plus
  `"sampler:<name>"` for SFZ patches (see `use "@sfz"`).
- `(writeWav "cadence.wav" audio)` writes a 16-bit stereo WAV. Path
  comes first, buffer second.

## The REPL

Skip the file boilerplate entirely when you're experimenting:

```bash
flow repl
```

You'll see:

```
Flow REPL - Type ':quit' to exit, ':help' for help
Multi-line input: end a line with \ to continue on next line

>
```

The REPL auto-imports `@std`, `@audio`, and `@collections`, so you can
go straight into:

```
> Buffer t = (createSineTone 0.5 440.0 0.3)
> t -> play
```

REPL commands (each begins with `:`):

| Command           | Effect                                   |
|-------------------|------------------------------------------|
| `:help`, `:h`     | Show the in-REPL help                    |
| `:quit`, `:q`, `:exit` | Exit the REPL                       |
| `:clear`, `:cls`  | Clear the screen                         |
| `:stop`           | Stop any currently playing audio         |
| `Ctrl+C`          | Stop audio without exiting the REPL      |

Multi-line input has two modes:

- End any line with `\` to continue on the next (prompt changes to `...`).
- Lines starting with `proc` automatically enter multi-line mode until
  the matching `end proc`. Block-bracketed forms (`tempo 120 {` …
  `}`, `section X {` … `}`) are detected the same way.

## Where to go next

You've now seen the full pipeline — note streams, musical context,
sections, songs, instrument selection, WAV export. Here is the
recommended path through the wiki and the in-repo examples.

**Wiki chapters** (in [`wiki/`](../wiki)):

- [Quick-Start](../wiki/Quick-Start.md) — a denser version of this
  page with extra options
- [Language-Basics](../wiki/Language-Basics.md) — variables, types,
  S-expressions, scoping, tuples, dicts, pattern matching
- [Note-Streams](../wiki/Note-Streams.md) — every modifier the `| ... |`
  syntax supports: rests, dotted notes, ties, articulations, cent
  offsets, random choice, voice blocks
- [Chords-and-Harmony](../wiki/Chords-and-Harmony.md) — chord literals
  (`Cmaj7`, `F#dim`), roman numerals, scale resolution
- [Musical-Context](../wiki/Musical-Context.md) — `tempo`, `timesig`,
  `key`, `swing`, `voicePool`, `tuning`
- [Audio-and-Synthesis](../wiki/Audio-and-Synthesis.md) — the 9
  shipping synthesizers and the SFZ orchestral sampler
- [Playback-and-Export](../wiki/Playback-and-Export.md) — `play`,
  `loop`, `preview`, `writeWav`, `writeMidi`

**Runnable examples** (in [`examples/`](../examples)):

- [`examples/tutorial.flow`](../examples/tutorial.flow) — the full
  guided tour; step through every major feature with one
  `flow run examples/tutorial.flow`

When you're ready to scaffold your own piece, `flow new my-piece`
writes a minimum-viable `my-piece/my-piece.flow` you can edit and
`flow play`.

## Common first-time issues

**No audio came out.** Confirm your audio server is running and Flow
can see it:

```bash
pactl info                # should print "Server Name: pulseaudio" or "pipewire"
flow eval 'use "@audio"; (createSineTone 0.5 440.0 0.3) -> play'
```

If `pactl` is missing, install it: `sudo apt install pulseaudio-utils`
(Debian/Ubuntu), `sudo dnf install pulseaudio-utils` (Fedora), or
`sudo pacman -S libpulse` (Arch). PipeWire users need
`pipewire-pulse` from their distro packages.

**`flow: command not found`** after running `install.sh`. The
installer printed a `WARNING: ~/.local/bin is not on your PATH` line.
Add this to your `~/.bashrc` (or `~/.zshrc`):

```bash
export PATH="$HOME/.local/bin:$PATH"
```

Then `source ~/.bashrc` or open a new terminal.

**Permission denied during `install.sh --system`.** System install
writes to `/usr/local/` — you need either `sudo` or a per-user install
(omit `--system`).

**`createSineTone: not defined` or similar.** You forgot
`use "@audio"`. Script mode requires explicit imports; only the REPL
auto-imports them.

**`renderSong: no matching overload`.** Check the instrument string —
valid first-party names are `"piano"`, `"strings"`, `"organ"`,
`"bell"`, `"brass"`, `"sax"`, `"flute"`, `"drums"`, `"wavetable"`.
Sampled-instrument bundles live under
`~/.local/share/flow/flow-v<version>/Samples/`; the installer ships
them automatically.

**Build error: "SDK 'Microsoft.NET.Sdk' not found"** during
`dotnet build`. You don't have .NET 10 installed (or your `dotnet
--list-sdks` shows only older SDKs). Install the .NET 10 SDK from
<https://dotnet.microsoft.com/download/dotnet/10.0> — required for
Path B only; the prebuilt tarball does not need it.

For deeper troubleshooting of the runtime config (audio device
selection, stdlib search paths, SFZ root), see
[`docs/CONFIGURATION.md`](CONFIGURATION.md). For the architectural
tour of how the interpreter is structured, see
[`docs/ARCHITECTURE.md`](ARCHITECTURE.md).
