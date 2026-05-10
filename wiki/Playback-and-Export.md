# Playback and Export

Flow plays audio in real time (via PulseAudio on Linux) and exports to WAV and MIDI files. All playback and export functions live in `@audio`.

## Playing Audio

### play (blocking)

Plays a buffer or sequence and blocks until playback finishes:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)
(play tone)
```

You can also pass a sequence directly (it renders with a sine synth):

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    (play mel)
}
```

### stream (non-blocking)

Plays audio asynchronously so your script continues executing:

```flow
use "@audio"

Buffer buf = (createSineTone 5.0 440.0 0.5)
(stream buf)
Note: returns immediately; audio plays in the background
```

`stream` also works with sequences.

### loop

Loops a buffer indefinitely (non-blocking), or a specific number of times:

```flow
(loop buf)          Note: forever (until (stop) is called)
(loop buf 4)        Note: 4 times
```

### preview

Low-quality preview (mono, 22050 Hz) for fast iteration:

```flow
(preview buf)
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

### Set Device

```flow
Bool success = (setAudioDevice "pulse")
```

### Check Availability

```flow
Bool available = (isAudioAvailable)
(print (str available))
```

If the audio backend is unavailable, `play` / `stream` / `loop` become no-ops with a warning — your WAV/MIDI exports still work.

## WAV Export

### Basic Export

Export a buffer to a 16-bit PCM WAV file:

```flow
use "@audio"

Buffer buf = (createSineTone 1.0 440.0 0.5)
(exportWav buf "output.wav")
```

### Custom Bit Depth

Specify 16, 24, or 32-bit output:

```flow
(exportWav buf "output_16.wav" 16)     Note: 16-bit PCM (default)
(exportWav buf "output_24.wav" 24)
(exportWav buf "output_32.wav" 32)
```

### writeWav (path-first)

A convenience variant with the path as the first argument — useful when piping:

```flow
(writeWav "output.wav" buf)
(writeWav "output.wav" buf 24)
```

## WAV Loading

Load an existing WAV back into a buffer (supports 16/24/32-bit PCM):

```flow
use "@audio"

Buffer loaded = (loadWav "sample.wav")
Int frames = (getFrames loaded)
Int channels = (getChannels loaded)
(print $"loaded {frames} frames, {channels} ch")

Note: works with effects pipeline like any other buffer
Buffer processed = loaded -> gain 0.5 -> reverb 0.2
```

## MIDI Export

Export a `Song` to a Standard MIDI File (.mid). Tempo, time signature, and key from the enclosing musical context are preserved:

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

MIDI export is useful for opening the piece in a DAW, scoring software, or another instrument.

## Complete Render-to-File Workflow

```flow
use "@std"
use "@audio"

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
            Buffer mix = raw -> reverb 0.3 -> fadeIn 0.2 -> fadeOut 0.5

            (exportWav mix "my_song.wav")
            (writeMidi "my_song.mid" mySong)

            Int frames = (getFrames mix)
            Int duration = (div frames 44100)
            (print $"duration: ~{duration}s")
        }
    }
}
```

## Playback Architecture

- Flow uses `IAudioBackend` as a platform abstraction for real-time playback.
- The Linux implementation is PulseAudio via P/Invoke (`PulseAudioSimpleBackend`).
- `AudioPlaybackManager` manages the backend lifecycle.
- Audio renders to stereo float buffers at 44100 Hz by default.

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` | Play buffer (blocking) |
| `play` | `(Sequence) -> Void` | Render and play sequence (blocking) |
| `stream` | `(Buffer) -> Void` | Play asynchronously (non-blocking) |
| `stream` | `(Sequence) -> Void` | Render and stream sequence |
| `loop` | `(Buffer) -> Void` | Loop indefinitely |
| `loop` | `(Buffer, Int) -> Void` | Loop N times |
| `preview` | `(Buffer) -> Void` | Low-quality mono preview |
| `stop` | `() -> Void` | Stop all playback |
| `audioDevices` | `() -> String[]` | List available devices |
| `setAudioDevice` | `(String) -> Bool` | Select device by name |
| `isAudioAvailable` | `() -> Bool` | Check backend availability |
| `exportWav` | `(Buffer, String) -> Void` | Export 16-bit WAV |
| `exportWav` | `(Buffer, String, Int) -> Void` | Export WAV with bit depth |
| `writeWav` | `(String, Buffer) -> Void` | Path-first WAV export |
| `writeWav` | `(String, Buffer, Int) -> Void` | Path-first with bit depth |
| `loadWav` | `(String) -> Buffer` | Load WAV file |
| `writeMidi` | `(String, Song) -> Void` | Export Song to .mid |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Creating and synthesizing audio
- [Effects](Effects.md) - Processing audio before export
- [Song Structure](Song-Structure.md) - Creating songs to render and export
- [Voices and Tracks](Voices-and-Tracks.md) - Lower-level multi-track rendering
