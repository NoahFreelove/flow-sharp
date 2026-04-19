# Vocalization

Flow can synthesize singing and speech via formant synthesis and an optional text-to-speech hook. These live in `@audio` and produce ordinary `Buffer` values you can mix, effect, and export.

## sing

Synthesize a vowel or consonant-vowel syllable at a given pitch and duration:

```flow
use "@std"
use "@audio"

Buffer vowel = (sing "ah" C4 0.5)
(play vowel)
```

**Signature**: `(sing String syllable, Note pitch, Double seconds) -> Buffer`

### Vowels

The five core vowels are supported:

| Syllable | IPA | Example |
|----------|-----|---------|
| `"ah"` | /a/ | "father" |
| `"ee"` | /i/ | "see" |
| `"eh"` | /e/ | "bed" |
| `"oh"` | /o/ | "go" |
| `"oo"` | /u/ | "moon" |

```flow
use "@audio"

Buffer a  = (sing "ah" A4 0.3)
Buffer e  = (sing "ee" A4 0.3)
Buffer i  = (sing "eh" A4 0.3)
Buffer o  = (sing "oh" A4 0.3)
Buffer u  = (sing "oo" A4 0.3)

Buffer phrase = a -> appendBuffers e -> appendBuffers i -> appendBuffers o -> appendBuffers u
(exportWav phrase "vowels.wav")
```

### Consonant-Vowel Syllables

Prefix a vowel with a supported consonant to get a syllable:

```flow
use "@audio"

Buffer na = (sing "na" C4 0.5)
Buffer ta = (sing "ta" E4 0.3)
Buffer sa = (sing "sa" G4 0.3)
Buffer la = (sing "la" C5 0.5)
```

The synthesizer applies a short consonant attack (plosive, fricative, or nasal) followed by the vowel formants.

## Mixing Vocals with Instruments

Since `sing` returns a normal `Buffer`, you can mix and process it like any other source:

```flow
use "@std"
use "@audio"

Buffer vocal  = (sing "ah" C4 1.0)
Buffer tone   = (createSineTone 1.0 440.0 0.5)
Buffer mixed  = (mix vocal tone)

Buffer wet = mixed -> reverb 0.4 -> gain 0.0
(exportWav wet "vocal_mix.wav")
```

## Different Pitches

Formant synthesis preserves vowel character across the usable vocal range:

```flow
use "@audio"

Buffer low  = (sing "oh" C3 0.5)
Buffer mid  = (sing "oh" C4 0.5)
Buffer high = (sing "oh" C5 0.5)
```

Very low or very high pitches may become less intelligible, as with real voices.

## Text-to-Speech Hook

Flow can delegate a string to an external TTS engine (such as `espeak-ng`) and return the generated audio as a buffer.

### Setting the TTS Command

```flow
use "@audio"

(setTtsCommand "espeak-ng -v en -w {out}")
```

The command template should write WAV output to the path substituted for `{out}`. The generated file is loaded back into a buffer and the temporary file is cleaned up.

### Running TTS

```flow
use "@audio"

Buffer greeting = (tts "Hello from Flow")
(play greeting)
```

If no TTS command has been configured, or the engine is not installed, `tts` reports an error.

### Exporting TTS Audio

Since the result is a standard buffer, it can be processed and exported:

```flow
use "@audio"

Buffer words = (tts "welcome to the piece")
Buffer wet   = words -> reverb 0.5 -> fadeOut 0.5
(exportWav wet "intro_voice.wav")
```

## Use Cases

- **Singing synthesis**: layer vocal buffers over instrumental parts to add a human timbre
- **Spoken intros/outros**: use TTS for ambient narration or spoken-word pieces
- **Ear training / demos**: name scale degrees or chords out loud inside longer renders
- **Phoneme play**: sequence `sing` calls to build nonsense syllable patterns over a groove

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `sing` | `(String, Note, Double) -> Buffer` | Formant-synthesized vowel or syllable |
| `tts` | `(String) -> Buffer` | External TTS → buffer (requires `setTtsCommand`) |
| `setTtsCommand` | `(String) -> Void` | Configure the TTS command template |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffers and synthesizers
- [Effects](Effects.md) - Reverb, filters, compression for vocals
- [Playback and Export](Playback-and-Export.md) - Saving vocal renders
