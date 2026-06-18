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

**Signature**: `(sing String phoneme, Note pitch, Double duration) -> Buffer`

`sing` is tuning-aware — under `enable justIntonation;` + `key Cmajor`, an E4 vocalization renders at the 5/4 ratio rather than 12-TET. Same for `enable pythagorean;`, `enable equalTemperament;`, and `tuning t { }` blocks.

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
(writeWav "vowels.wav" phrase)
```

### Consonant-Vowel Syllables

Prefix a vowel with one of three supported consonants — `s` (sibilant fricative, filtered noise), `t` (plosive), or `n` (nasal) — to get a syllable. The synthesizer renders the consonant onset, then crossfades into the vowel formants:

```flow
use "@audio"

Buffer na = (sing "na" C4 0.5)
Buffer ta = (sing "ta" E4 0.3)
Buffer sa = (sing "sa" G4 0.3)
Buffer noo = (sing "noo" C5 0.5)
```

Other consonants are not yet supported — passing an unsupported phoneme (e.g. `"la"`) raises a runtime error: `Unknown vowel phoneme: 'la'. Valid: ah, ee, eh, oh, oo`.

## Mixing Vocals with Instruments

Since `sing` returns a normal `Buffer`, you can mix and process it like any other source:

```flow
use "@std"
use "@audio"

Buffer vocal  = (sing "ah" C4 1.0)
Buffer tone   = (createSineTone 1.0 440.0 0.5)
Buffer mixed  = (mix vocal tone)

Buffer wet = mixed -> reverb 0.4 -> gain 0dB
(writeWav "vocal_mix.wav" wet)
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

Flow can delegate a string to an external TTS engine and return the generated audio as a buffer. The default command is `espeak-ng --stdout` — install `espeak-ng` (or any TTS that writes WAV to stdout) and `tts` works without further setup.

### Setting a Custom TTS Command

```flow
use "@audio"

Note: The first token is the executable; remaining tokens are base args.
Note: The text to speak is appended as a quoted last argument, so the command
Note: must write WAV data to stdout (espeak-ng's --stdout does exactly this).
(setTtsCommand "espeak-ng -v en --stdout")
```

### Running TTS

```flow
use "@audio"

Buffer greeting = (tts "Hello from Flow")
(play greeting)
```

If the TTS executable can't be found, `tts` raises an error pointing at `setTtsCommand` to change engines. The process is killed if it doesn't return within 30 seconds.

### Exporting TTS Audio

Since the result is a standard buffer, it can be processed and exported:

```flow
use "@audio"

Buffer words = (tts "welcome to the piece")
Buffer wet   = words -> reverb 0.5 -> fadeOut 0.5
(writeWav "intro_voice.wav" wet)
```

## Use Cases

- **Singing synthesis**: layer vocal buffers over instrumental parts to add a human timbre
- **Spoken intros/outros**: use TTS for ambient narration or spoken-word pieces
- **Ear training / demos**: name scale degrees or chords out loud inside longer renders
- **Phoneme play**: sequence `sing` calls to build nonsense syllable patterns over a groove

## Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `sing` | `(String, Note, Double) -> Buffer` | Formant-synthesized vowel or syllable (tuning-aware) |
| `tts` | `(String) -> Buffer` | External TTS -> buffer (defaults to `espeak-ng --stdout`) |
| `setTtsCommand` | `(String) -> Void` | Configure the TTS command template |

## See Also

- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffers and synthesizers
- [Effects](Effects.md) - Reverb, filters, compression for vocals
- [Playback and Export](Playback-and-Export.md) - Saving vocal renders
- [Tuning](Tuning.md) - How `sing` reads the active tuning context
