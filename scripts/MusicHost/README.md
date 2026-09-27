# Native music host

```sh
dotnet run --project scripts/MusicHost -- /tmp/flow-native-host.wav [/tmp/flow-native-host.mid]
```

Constructs an immutable score with repeats and two tempos, then streams dry sine
stereo blocks into a PCM16 WAV. Only `Flow.Audio`, `Flow.Music.Model` and the BCL
are dependencies for audio; the optional MIDI output adds `Flow.Music.IO` and
DryWetMidi. The JSON report lists loaded assemblies. The output has 294,000
frames at 44,100 Hz. Ctrl+C cancels cooperatively; partial output is retained.

The renderer supports resolved pitch/tuning, authored timing, articulation,
ties/rests, overlap, sustain, voice-pool stealing and pan/gain. Nonzero reverb is
rejected. Instrument/effect routing is future work; portamento has no audible
effect on the sine path, matching the legacy sine synthesizer.

The host owns file I/O. Its small WAV encoder clips and rounds without dither,
rejects RIFF sizes over 4 GiB, and is not the existing Flow WAV export contract.
The render sink consumes borrowed blocks synchronously; copy blocks to retain
them. No complete PCM song allocation is needed.
