# Playback and Export

Flow can play audio in real-time and export to WAV files. Playback functions require `use "@audio"`.

## Playing Audio

### play

Plays a buffer (blocks until playback completes):

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)
(play tone)
```

You can also play sequences directly:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    (play mel)
}
```

### loop

Loops a buffer indefinitely or a specific number of times:

```flow
use "@audio"

Note: Loop indefinitely (until stop is called)
(loop buf)

Note: Loop 4 times
(loop buf 4)
```

### preview

Low-quality preview (mono, 22050 Hz) for quick listening:

```flow
(preview buf)
```

### stop

Stops current playback:

```flow
(stop)
```

## Audio Devices

### List Devices

```flow
use "@std"
use "@audio"

Strings devices = (audioDevices)
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

## WAV Export

### Basic Export

Export a buffer to a 16-bit PCM WAV file:

```flow
use "@std"
use "@audio"

Buffer buf = (createSineTone 1.0 440.0 0.5)
(exportWav buf "output.wav")
```

### Custom Bit Depth

Specify 16, 24, or 32-bit output:

```flow
(exportWav buf "output_16.wav" 16)   Note: 16-bit PCM (default)
(exportWav buf "output_24.wav" 24)   Note: 24-bit PCM
(exportWav buf "output_32.wav" 32)   Note: 32-bit PCM
```

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

            Note: Render to buffer
            Buffer raw = (renderSong mySong "piano")

            Note: Apply effects
            Buffer processed = raw -> reverb 0.3 -> fadeIn 0.2 -> fadeOut 0.5

            Note: Export
            (exportWav processed "my_song.wav")
            (print "Exported my_song.wav!")

            Note: Report stats
            Int frames = (getFrames processed)
            Int duration = (div frames 44100)
            (print (concat "Duration: ~" (concat (str duration) "s")))
        }
    }
}
```

## Playback Architecture

Flow uses `IAudioBackend` as an abstraction for platform-specific audio playback. The current implementation uses PulseAudio via P/Invoke on Linux.

- `AudioPlaybackManager` manages the backend lifecycle
- `FlowEngine` owns the playback manager
- Audio is rendered to stereo float buffers at 44100 Hz sample rate

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` | Play buffer (blocking) |
| `play` | `(Sequence) -> Void` | Render and play sequence |
| `loop` | `(Buffer) -> Void` | Loop indefinitely |
| `loop` | `(Buffer, Int) -> Void` | Loop N times |
| `preview` | `(Buffer) -> Void` | Low-quality preview |
| `stop` | `() -> Void` | Stop playback |
| `audioDevices` | `() -> String[]` | List audio devices |
| `setAudioDevice` | `(String) -> Bool` | Set output device |
| `isAudioAvailable` | `() -> Bool` | Check audio backend |
| `exportWav` | `(Buffer, String) -> Void` | Export 16-bit WAV |
| `exportWav` | `(Buffer, String, Int) -> Void` | Export with bit depth |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation and synthesis
- [Effects](Effects.md) - Processing audio before export
- [Song Structure](Song-Structure.md) - Creating songs to render
