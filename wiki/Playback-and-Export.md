# Playback and Export

Flow plays audio in real time and exports to WAV, MIDI, MusicXML, and LilyPond files. Real-time playback and WAV/MIDI export live in `@audio`; notation export/import is opt-in via `use "@notation-io"`.

Real-time playback uses `IAudioBackend`, which probe-selects the best available platform backend:
- **macOS** — CoreAudio (`AudioToolbox.framework` P/Invoke, `CoreAudioBackend`). `play` blocks until the full buffer has been rendered by the device (drain fixed 2026-06-10).
- **Windows** — WASAPI (`NAudio.Wasapi`, `WasapiBackend`). Audible end-to-end; HUMAN-UAT pending.
- **Linux** — PulseAudio (`libpulse-simple` P/Invoke, `PulseAudioSimpleBackend`). Also works on PipeWire via PA's compatibility layer.

## Playing Audio

### play (blocking)

Plays a buffer or sequence and blocks until playback finishes:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)
(play tone)
```

You can also pass a sequence directly (it renders with a sine synth):

```flow
use "@audio"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    (play mel)
}
```

### stream (non-blocking)

Plays audio asynchronously so your script continues executing:

```flow
use "@audio"

Buffer tone = (createSineTone 440Hz 5.0 0.5)
(stream tone)
Note: returns immediately; audio plays in the background
```

`stream` also works with sequences.

### Playing a Song directly

`play` also accepts a `Song` — it renders the song to a buffer (defaulting to the piano synth) and plays it, so you don't have to call `renderSong` first. An optional second argument picks the synth by name:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section verse { Sequence mel = | C4 E4 G4 C5 | }
            Song song = [verse]
            (play song)            Note: render + play with the default piano
            (play song "sine")     Note: render + play with an explicit synth
        }
    }
}
```

### loop

Loops a buffer indefinitely (non-blocking), or a specific number of times:

```flow
use "@audio"

Buffer tone = (createSineTone 440Hz 1.0 0.5)
(loop tone)          Note: forever (until (stop) is called)
(loop tone 4)        Note: 4 times
```

### preview

Low-quality preview (mono, 22050 Hz) for fast iteration:

```flow
use "@audio"

Buffer tone = (createSineTone 440Hz 1.0 0.5)
(preview tone)
```

### stop

Stops any currently playing audio:

```flow
(stop)
```

## Audio Devices

### List Devices

```flow
use "@std"
use "@audio"

String[] devices = (audioDevices)
(print (str devices))
```

The PulseAudio Simple API doesn't enumerate sinks, so `audioDevices()` currently returns an empty list. For now, select an output device with the `--device` CLI flag on `flow run` / `flow play`.

### Set Device

```flow
Bool success = (setAudioDevice "pulse")
```

### Check Availability

```flow
Bool available = (isAudioAvailable)
(print (str available))
```

If the audio backend is unavailable, `play` / `stream` / `loop` become no-ops with a warning — your WAV/MIDI exports still work. For headless renders and CI, set `FLOW_SUPPRESS_PLAYBACK=1` to route playback to a capture buffer instead of the audio device.

## WAV Export

`writeWav` writes 16/24/32-bit PCM at the buffer's sample rate. Parent directories are auto-created. The 16/24-bit paths apply TPDF (Triangular Probability Density Function) dither at 1 LSB — the dither RNG is seeded deterministically per export, so consecutive writes of the same buffer produce byte-identical WAVs.

### Basic Export

```flow
use "@audio"

Buffer tone = (createSineTone 440Hz 1.0 0.5)
(writeWav "output.wav" tone)
```

### Custom Bit Depth

```flow
use "@audio"

Buffer tone = (createSineTone 440Hz 1.0 0.5)
(writeWav "output_16.wav" tone 16)        Note: default
(writeWav "output_24.wav" tone 24)
(writeWav "output_32.wav" tone 32)
```

### Exporting a Song Directly

`writeWav` also takes a `Song` in the buffer position — it renders the song (defaulting to the piano synth) and writes the WAV in one step, so you can skip the explicit `renderSong`. An optional third argument names the synth:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section verse { Sequence mel = | C4 E4 G4 C5 | }
            Song song = [verse]
            (writeWav "song.wav" song)            Note: render + export, piano default
            (writeWav "song_sine.wav" song "sine") Note: explicit synth
        }
    }
}
```

Reach for the explicit `renderSong` + effects-chain form when you need to post-process the buffer (reverb, fades, gain) before writing.

## WAV Loading

Load an existing WAV back into a buffer (supports 16/24/32-bit PCM; auto-resamples to 44100 Hz). Two optional overloads apply varispeed pitch-shift at load time — identity short-circuits at `semitones=0` / `ratio=1.0`:

```flow
use "@audio"

Note: replace "sample.wav" with the path to an existing WAV file on disk
Buffer loaded   = (loadWav "sample.wav")
Buffer up5      = (loadWav "sample.wav" +5st)      Note: +5 semitones (Semitone literal)
Buffer halfRate = (loadWav "sample.wav" 0.5)       Note: half-speed = down one octave (Double ratio)
Int frames      = (getFrames loaded)
Int channels    = (getChannels loaded)

Note: works with effects pipeline like any other buffer
Buffer processed = loaded -> (gain -6dB) -> (reverb 0.2)
```

## MIDI Export

Export a `Song` to a Standard MIDI File (Format 1, multi-track) via DryWetMidi. Tempo, time signature, and key from the enclosing musical context are preserved. Each unique sequence name in the song becomes its own track (plus a conductor track for tempo / timesig); track names route to General MIDI program numbers by prefix:

| Sequence name prefix | GM program | Channel |
|----------------------|------------|---------|
| `violin*` / `viola*` / `cello*` / `contrabass*` | 40 / 41 / 42 / 43 | per-track |
| `piano*` | 0 | per-track |
| `brass*` / `horn*` | 56 | per-track |
| `sax*` | 65 | per-track |
| `flute*` | 73 | per-track |
| `string*` (synth) | 48 | per-track |
| `organ*` | 19 | per-track |
| `bell*` | 14 | per-track |
| `drum*` | 0 | channel 9 (GM percussion) |

```flow
use "@std"
use "@audio"

tempo 140 {
    timesig 3/4 {
        key Gmajor {
            section waltz {
                | G4q B4q D5q |
                | D5h G4q |
            }
            section ending { | G4h. | }

            Song song = [waltz waltz ending]
            (writeMidi "my_waltz.mid" song)
        }
    }
}
```

TPQN auto-elevates to the LCM of any tuplet denominators (default 480, hard cap 9600). Voice-block polyphony exports as overlapping NoteOn events at the parent's tick. Non-12-TET tunings fire a one-shot stderr advisory; per-note pitch-bend export is on the v1.6+ backlog.

## MIDI Import (CLI)

MIDI import is a CLI subcommand rather than an in-language builtin — it emits round-trip-friendly `.flow` source from a `.mid`:

```bash
flow midi2flow input.mid                 # writes input.flow next to source
flow midi2flow input.mid -o tune.flow    # explicit output path
flow midi2flow input.mid --no-sustain    # omit sustain-pedal blocks
flow midi2flow input.mid --sfz           # prefer SFZ instruments for orchestral GM
flow midi2flow input.mid --dump          # also write a diagnostic dump
```

## Notation Export & Import

Opt in to MusicXML / LilyPond / ABC / MML with `use "@notation-io"`. All four builtins write or read text formats; none ship audio dependencies.

```flow
use "@std"
use "@audio"
use "@notation-io"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section verse {
                Sequence mel = | C4 E4 G4 C5 |
            }
            Song song = [verse]

            (writeMusicXML "verse.musicxml" song)   Note: MusicXML 3.1 partwise (MuseScore-compatible)
            (writeLilyPond "verse.ly"       song)   Note: LilyPond 2.24+
        }
    }
}

Note: ABC import — single tune returns Section, multi-tune (X:1/X:2/...) returns Array[Section]
Section tune = (abc "X:1\nT:Demo\nM:4/4\nK:Cmaj\nC D E F |")

Note: PC-98 MML import — returns a single Sequence
Sequence riff = (mml "T120 L4 O4 cdefga>c")
```

Both exports preserve articulations, microtonal cent offsets (as `<alter>` decimals in MusicXML; as `% +Nc` comments in LilyPond), voice-block polyphony (per-note `<voice>N</voice>` tags / sibling `<< { } \\ { } >>` voices), and dynamics. Imports are charitable — unknown ornaments / opcodes drop with a one-shot stderr advisory rather than erroring.

## Beat-Synced Live Reload

`flow watch <script>` quantizes file-watch reloads to the next bar boundary and applies a 64-sample crossfade between the old and new render. Failed renders keep the previous version playing — your speakers don't go silent when you typo.

```bash
flow watch piece.flow
```

## Complete Render-to-File Workflow

```flow
use "@std"
use "@audio"
use "@notation-io"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4 E4 G4 C5 |
            }
            section verse {
                Sequence lead = | E4 E4 F4 G4 |
            }
            section chorus {
                Sequence lead = | I IV V I |
            }

            Song mySong = [intro verse*2 chorus]

            Buffer raw = (renderSong mySong "piano")
            Buffer mix = raw -> reverb 0.3 -> fadeIn 0.2s -> fadeOut 0.5s

            (writeWav       "my_song.wav"       mix)
            (writeMidi      "my_song.mid"       mySong)
            (writeMusicXML  "my_song.musicxml"  mySong)
            (writeLilyPond  "my_song.ly"        mySong)

            Int frames = (getFrames mix)
            Int duration = (idiv frames 44100)
            (print $"duration: ~{duration}s")
        }
    }
}
```

## Playback Architecture

- Flow uses `IAudioBackend` as a platform abstraction for real-time playback.
- `AudioPlaybackManager` detects and instantiates the best available backend at startup (probe order: WebAudio on WASM, CoreAudio on macOS, WASAPI on Windows, PulseAudio on Linux).
- The Linux backend (`PulseAudioSimpleBackend`) uses `PA_SAMPLE_FLOAT32LE` and supports 1–8 channels. Also works on PipeWire via PA's compatibility layer.
- Audio renders to stereo float buffers at 44100 Hz by default.

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` | Play buffer (blocking) |
| `play` | `(Sequence) -> Void` | Render and play sequence (blocking) |
| `play` | `(Song) -> Void` | Render (piano default) and play song (blocking) |
| `play` | `(Song, String) -> Void` | Render with named synth and play song |
| `stream` | `(Buffer) -> Void` | Play asynchronously (non-blocking) |
| `stream` | `(Sequence) -> Void` | Render and stream sequence |
| `loop` | `(Buffer) -> Void` | Loop indefinitely |
| `loop` | `(Buffer, Int) -> Void` | Loop N times |
| `preview` | `(Buffer) -> Void` | Low-quality mono preview (22050 Hz) |
| `stop` | `() -> Void` | Stop all playback |
| `audioDevices` | `() -> String[]` | List available devices (empty under PulseAudio Simple API) |
| `setAudioDevice` | `(String) -> Bool` | Select device by name |
| `isAudioAvailable` | `() -> Bool` | Check backend availability |
| `writeWav` | `(String, Buffer) -> Void` | Path-first WAV export (16-bit) |
| `writeWav` | `(String, Buffer, Int) -> Void` | Path-first with bit depth (16/24/32) |
| `writeWav` | `(String, Song) -> Void` | Render (piano default) and export song to WAV |
| `writeWav` | `(String, Song, String) -> Void` | Render with named synth and export song to WAV |
| `loadWav` | `(String) -> Buffer` | Load WAV file (auto-resample to 44100 Hz) |
| `loadWav` | `(String, Int) -> Buffer` | Load with semitone varispeed |
| `loadWav` | `(String, Double) -> Buffer` | Load with ratio varispeed |
| `writeMidi` | `(String, Song) -> Void` | Export Song to .mid (SMF Format 1, multi-track) |
| `writeMusicXML` | `(String, Song) -> Void` | Export Song to MusicXML 3.1 (requires `@notation-io`) |
| `writeLilyPond` | `(String, Song) -> Void` | Export Song to LilyPond 2.24+ (requires `@notation-io`) |
| `abc` | `(String) -> Section\|Array[Section]` | Import ABC 2.1 source (requires `@notation-io`) |
| `mml` | `(String) -> Sequence` | Import PC-98 MML source (requires `@notation-io`) |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Creating and synthesizing audio
- [Effects](Effects.md) - Processing audio before export
- [Song Structure](Song-Structure.md) - Creating songs to render and export
- [Voices and Tracks](Voices-and-Tracks.md) - Lower-level multi-track rendering
