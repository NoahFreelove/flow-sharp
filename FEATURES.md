## Features at a glance

Status: **Fully** = shipped · **Partial** = caveated or limited · **Not yet** = planned

---

### Core language

| Feature | Status | Notes |
|---|---|---|
| Static typing with music-aware types | Fully | 16 primitives + 22 special types (Note, Chord, Sequence, Song, Beat, Bar, Tuning, Sfz, MarkovModel, LsystemModel, etc.) |
| Type inference | Partial | Inferred in some contexts; explicit annotations elsewhere |
| Numeric widening chain | Fully | `Int → Long → Float → Double → Number` (BigInteger); IntLiteral overflow auto-fallthrough |
| Flow operator `->` for chaining | Fully | Parse-time transform — no runtime cost |
| Tuple-unpack flow operator `~>` | Fully | `tuple ~> func` unpacks tuple into multi-arg call; non-tuple LHS falls through to `->` |
| Flow-chain naming via `as NAME` | Fully | `expr -> call as name` binds intermediate result |
| `proc` declarations with implicit returns | Fully | 0 collected → Void; 1 → that value; 2+ → array |
| Internal proc declarations | Fully | `internal proc name(...)` — signature-only forward decl for stdlib `.flow` modules |
| Lambdas (`fn x => ...`) + function types | Fully | Multi-statement body via `fn x => ( ... )`; type annotations `(Int, Int => Int)` |
| Universal named arguments | Fully | `(fn name1=val1 name2=val2)` works on any function with named params; ~150 builtin signatures backfilled |
| Pattern matching (`match`) | Fully | `(match scrutinee | pat => body | _ => default)`; WildcardPattern, BindingPattern, LiteralPattern, ConstructorPattern (music-aware), GuardPattern (`when`) |
| Tuples `<<a, b, c>>` | Fully | Per-position types; arity tracked; empty `<<>>` and singleton `<<x>>` valid; structural equality |
| Tuple destructuring assignment | Fully | `<<Type? a, Type? b>> = expr` — per-slot type annotations optional |
| `(unpack tuple func)` first-class apply | Fully | Runtime equivalent of `~>`; mirrors Lisp `(apply f args)` |
| Generic `Dict<K, V>` | Fully | Insertion-order preservation; hashable-keys checked at parse time (Int/Long/Float/String/Symbol/Note/Chord/Beat/Tuple-of-hashables); 14-op surface (`dict`, `get`, `getOr`, `set`, `remove`, `has`, `keys`, `values`, `size`, `merge`, `each`, `map`, `filter`, `dictTuple`) |
| Symbol primitive `#foo` | Fully | Interned at evaluation time; pointer-equality; strictly separate from `String` |
| Arrays, indexing (`@N`), slicing | Fully | Negative-from-end indexing; `(slice arr start end)` |
| Array literals | Fully | `[a, b, c]` — comma OR space-separated |
| `(range Int Int [Int])` | Fully | Standard half-open range with optional step |
| Lazy evaluation | Fully | `(lazy expr)`, `(eval thunk)`; generic `Lazy<T>` annotation; thunks cache values AND exceptions |
| Module imports (`use "@stdlib"` / relative) | Fully | `@` prefix → stdlib dir; idempotent; circular-import detection; per-module PragmaSet (pragmas don't leak across modules) |
| Pragma system (`enable <pragma>;`) | Fully | File-scope only, top-of-file only; unknown pragma errors include did-you-mean suggestion |
| `enable hAsB;` | Fully | `H` aliases to `B` in note streams (German notation) |
| `enable justIntonation;` / `pythagorean;` / `equalTemperament;` | Fully | 5-limit JI / 3-limit Pythagorean / 12-TET render-time tuning, rooted at active key tonic |
| `enable matchExhaustive;` | Fully | Promote non-exhaustive match warnings to errors |
| `enable scaleLint;` | Fully | Accepted (no-op since scale-lint is default-on for LSP) |
| String interpolation `$"..."` | Fully | `{expr}` segments; escapes include `\{`, `\}` |
| Loops (`for` / `while`) | Fully | `for Type x in collection { }`; `while cond { }`; `break` / `continue`; MaxIterations safety cap (10000 default) |
| `// ` line comments | Fully | |
| `Note:` / `TODO:` / `FIXME:` line-start comments | Fully | Recognized as comments to EOL |
| Column-0 `;` Lisp-style line comments | Fully | Mid-line `;` remains a statement separator |
| Line continuation `\<newline>` | Fully | Joins lines while preserving logical line numbers |
| Optional-paren function calls | Fully | Bare-identifier same-line `print x` lowers to `(print x)` |
| Optional semicolons | Fully | Used as separators when present; never required |
| Prefix-only arithmetic | Fully | `(add)` / `(sub)` / `(mul)` / `(div)` / `(idiv)` / `(neg)` / `(concat)` — no infix `+ - * /`; stray-infix error suggests prefix form |
| Variable-as-function dispatch | Fully | Bare name with zero-arg overload auto-calls (`print` works as both ref and call) |
| Reference-identity music types | Fully | `Tuning`, `Sfz`, `MarkovModel`, `LsystemModel` use reference identity; structural compare via dedicated `(markovEqual)` / `(lsystemEqual)` |
| Plural-form type shorthand | Fully | `Ints` desugars to `Int[]`; same for `Strings`, etc. |
| Varargs (`Type...`) | Fully | Ellipsis token in proc params OR plural-form |
| MaxParseDepth / MaxCallDepth guards | Fully | 500 / 1000 hard caps prevent pathological inputs |

### Notation & musical syntax

| Feature | Status | Notes |
|---|---|---|
| Musical context blocks (`tempo`, `timesig`, `key`, `swing`) | Fully | Push/pop scoped on context stack |
| `timesig C { }` common-time shorthand | Fully | Lowers to 4/4 at parse time |
| `swing 50% { }` percent-suffix shorthand | Fully | Divides by 100 at parse time |
| `pan { }`, `gain { }`, `reverbTime { }`, `dynamics { }`, `rit { }`, `accel { }` blocks | Fully | Per-section context overlays |
| `voicePool N { }` block | Fully | Range [1, 256]; default 32; steal-oldest policy when exceeded |
| `sustainPedal { }` block | Fully | Extends note buffers by 2.0s sustain tail |
| `tuning <expr> { }` block | Fully | Three composer surfaces: identifier (`tuning partch { }`), inline call (`tuning (loadScala "x.scl") { }`), string-literal sugar (`tuning "x.scl" { }`) |
| Note streams `\| C4 D4 E4 \|` | Fully | Compiled at evaluation time against active musical context |
| Duration suffixes (`w h q e s t x y`) | Fully | Whole / half / quarter / eighth / sixteenth / thirty-second / 64th / 128th |
| Dotted (`C4q.`) and tied (`C4h~`) notes | Fully | |
| Rests (`_`) | Fully | |
| Cent offsets in note streams (`C4+50c`) | Fully | Preserved through transforms, voice blocks, humanize |
| Chord brackets in streams (`[C4 E4 G4]q`) | Fully | |
| Named chord elements in streams (`Cmaj7q`, `Dmq`) | Fully | |
| Chord literals (`Cmaj7`, `Dm`, `F#dim`, `Bb7`) | Fully | Accidentals via `s` / `f` convention; `b`/`#` reserved for notes |
| Multi-letter enharmonic edges (`E↔Fb`, `B↔Cb`, etc.) | Fully | |
| Arbitrary alteration stacks (`F##4`, `Bb-+bbb`) | Fully | Net alteration = (#+ count) − (b+- count); not bounded ±2 |
| Roman numerals in `key { }` (`I`, `ii`, `V7`, `vi`) | Fully | Resolved via `ScaleDatabase` against active key |
| Variable refs in note streams | Fully | Lowercase-initial identifiers expand to bound values |
| Random choice in streams (`(? C4 E4 G4)`) | Fully | Weighted `(? C4:50 E4:30)`, seeded `(?? C4 E4 G4)` |
| Section declarations + Song expressions | Fully | `Song s = [intro verse*2 chorus]` |
| Parameterized sections | Fully | `section verse(Note root, Int reps = 2) { ... }`; named or positional call form; defaults supported |
| Section repetition operator `*N` | Fully | `[verse*3]` and `[verse(C4)*3]` |
| Section overloading | Fully | Multiple `section verse(...)` with different signatures coexist; overload resolution picks at call time |
| Pattern syntax in section signatures | Fully | Typed bindings, tuple destructure, music-aware extractors (chord literal, roman numeral, articulation symbol) |
| Tuplets `{N:M ...}q` + music21 shorthand `{N ...}q` | Fully | Locked ratios for 3=2, 5=4, 6=4, 7=4, 9=8 |
| Per-note fractional duration (`C4/12`) | Fully | |
| Per-note tuplet ratio (`C4/3:2q`) | Fully | |
| Voice-block polyphony `{voice ...}` | Fully | Multiple parallel voices share onset; same render path for audio + MIDI |
| Articulation marks | Fully | `>` accent, `stacc` staccato, `ten` tenuto, `marc` marcato, `leg` legato |
| Sticky dynamic markings | Fully | `pp p mp mf f ff fff ppp sfz fp` in note streams; velocity mapped 0.125 → 1.0 |
| Crescendo / decrescendo markers (`cresc`, `decresc`) | Fully | |
| Ghost notes `(ghost C4q)` | Fully | |
| Grace notes `(grace B3)` | Fully | |
| Pickup note streams (`pickup \| ... \|`) | Fully | |
| Progression expressions `progression voices N \| I IV V \|` | Fully | Per-element `:N` bar-count suffix supported |
| H-as-B alias (German notation) | Fully | Via `enable hAsB;`; only on `H`-prefixed identifiers with digit / accidental |
| Sequence visualization (ASCII piano roll) | Fully | `(visualize seq)`; also `(visualize buf)` for waveforms |
| Adjacent `\|` pipes collapse in multi-line bars | Fully | Charitable interpretation — no inserted rests |

### Music-typed literals

| Feature | Status | Notes |
|---|---|---|
| `+2st` / `-3st` Semitone literals | Fully | Whole-numbers-by-design; signed forms only |
| `+50c` / `-25c` / `100c` Cent literals | Fully | Microtonal precision |
| `100ms` / `2.5s` Time literals | Fully | Millisecond / Second; mutually `CanConvertTo` |
| `-12dB` / `+6dB` Decibel literals | Fully | |
| `440Hz` / `1.5kHz` Hertz literals | Fully | kHz canonicalized to Hz at lex time |
| Signed numeric literals at expression-start | Fully | `-3`, `+5`, `-2.5` parsed as single tokens after `(`, `,`, `=`, `\|`, etc. |
| BigInteger overflow fallthrough | Fully | `int → long → BigInteger` automatic on overflow |

### Harmony & transforms

| Feature | Status | Notes |
|---|---|---|
| `transpose` (Semitone / Cent) | Fully | |
| `invert`, `retrograde`, `augment`, `diminish` | Fully | |
| `up`, `down`, `repeat`, `concat` (sequences) | Fully | |
| Arpeggio with rate / direction / pattern params | Fully | `(arpeggio Chord NoteValue dir pattern)` |
| Chord inversions & voicings | Fully | `(inversion Chord n)`, `(voicing Chord name)` |
| Roman-numeral resolution from key context | Fully | `(resolveNumeral numeral keyName)` |
| Chord progression DSL | Fully | `progression [voices N] \| I IV V \|` |
| Snap-to-grid quantize | Fully | `(quantize seq resolution strength swing)` |
| Articulation transforms | Fully | `(legato seq overlap)`, `(portamento seq glideMs)` |
| Dynamics transforms | Fully | `crescendo`, `decrescendo`, `swell` |
| Tempo transforms | Fully | `ritardando`, `accelerando`, `(fermata seq noteIdx)` |
| Ornament transforms | Fully | `(trill seq interval)`, `(tremolo seq reps)` |
| Pattern variation | Fully | `(vary seq prob [mutationType seed key])` — 6 overloads |
| Scale linting (out-of-key warnings) | Fully | LSP-side; runs unconditionally on `key { ... }` blocks |
| `(polyrhythm seqA seqB [beats])` | Fully | LCM cycle alignment OR explicit beat count |

### Generative

| Feature | Status | Notes |
|---|---|---|
| Euclidean rhythms (Bjorklund) | Fully | `(euclidean hits steps note [swing] [humanize seed])` |
| Swing | Fully | `swing N { }` block; applied at note-stream compile time |
| Humanize (uniform velocity) | Fully | `(humanize seq amount)` — non-deterministic shared RNG (frozen design) |
| Humanize (Gaussian via Box-Muller) | Fully | `(humanizeGaussian seq amount seed)` — seeded; recurses into voice blocks |
| Random choice in note streams | Fully | `(? ...)` uniform, `(? a:50 b:30 c:20)` weighted, `(?? ...)` seeded |
| Markov chains | Fully | `markov` (one-shot), `markovTrain` / `markovGenerate` (split), `markovEqual`; order clamped [1, 3]; feature extraction `features=#pitch` or `<<#pitch, #duration>>` |
| L-system (Lindenmayer) | Fully | `lsystem` (one-shot), `lsystemModel` / `lsystemGenerate`, `lsystemToSequence`, `lsystemEqual`; iterations clamped [0, 20] |
| Cellular automata | Fully | `cellular` (1D Wolfram-canonical), `cellularSeeded` (explicit pattern), `life` (2D Conway with wrap) |
| Chaos maps | Fully | `(lorenz σ ρ β length seed)` and `(logistic r length seed)` returning `Array[Double]`; bridge via `(quantizeToScale series scale)` |
| Improv `jam` (chord-aware Markov) | Fully | `(jam over [style length key seed order])`; chord-tones on strong beats, scale on weak, chromatic-passing per style pack |
| Composer-editable style packs | Fully | Shipped `#jazz`, `#blues`, `#classical` at `flow-lang/improv/styles/*.flow`; user packs at `~/.config/flow/styles/*.flow` (last-write-wins) |
| Style registry surface | Fully | `(registerStyle #name pack)`, `(listStyles)` |

### Pattern combinators (Tidal-style)

Opt-in via `use "@patterns"`. Cycle unit is bars; transform-arg combinators are lambda-required; degenerate inputs return input + advisory (never throws).

| Feature | Status | Notes |
|---|---|---|
| `(every n cb seq)` | Fully | Apply `cb` to every Nth bar |
| `(fast seq factor)` | Fully | Speed bars up |
| `(slow seq factor)` | Fully | Slow bars down |
| `(chunk n cb seq)` | Fully | Rotate-apply `cb` to 1/N-th chunk per call |
| `(phase offset seq)` | Fully | Rotate bar order |
| `(rev seq)` | Fully | Reverse bar order (within-bar preserved) |
| `(iter n seq)` | Fully | Rotate note list by `totalNotes / n` |
| `(palindrome seq)` | Fully | Concat with reverse |
| `(jux cb seq)` | Fully | Layer original + lambda result as voice block; v1.5 mono mix (L/R stereo planned for v1.6) |
| `(superimpose cb seq)` | Fully | Mono voice-block overlay |
| `(sometimes [prob] cb seq)` | Fully | Probabilistic apply via `PrngRegistry`; default prob 0.5 |
| `(degrade seq)` | Fully | Fixed-50% drop (Tidal compat) |
| `(sparseSeq prob seq)` | Fully | Composer-controlled drop probability |

### Synthesis

| Feature | Status | Notes |
|---|---|---|
| Raw oscillators | Fully | `sine`, `saw`/`sawtooth`, `square`, `triangle` (aliased — no anti-alias by design) |
| Wavetable synths | Fully | `warm` (boosted-mid additive saw), `bright` (DC-removed 10% pulse), `buzz` (1/√n supersaw stack) |
| Custom wavetable registration | Fully | `(oscillator name generator [tableSize])` or `(oscillator name wavetable[])` |
| Custom oscillator definitions (user `proc` as oscillator) | Fully | `(renderSong Song Function)` with lambda contract `(MusicalNote, dur, bpm) → Buffer` |
| Sample-based piano (4 velocity layers) | Fully | U-Iowa MIS pp/mf/ff at 5 pitch points + mp synthesized at eager-load via signed-RMS interpolation (α=0.6 mf-lean) |
| `release=` named arg on `renderSong` | Fully | Sustain-pedal-sim tail length (Second-typed; default 1.5s, clamped [0.05, 10.0]) |
| Sample-based brass / sax / strings / flute / bell | Fully | Single mf-layer with linear velocity scaling; flute has G4/A4/G5 (A4 closes D5 crossover gap) |
| Sample-based percussion via SFZ | Fully | `#drums` dict-symbol routes to VSCO-CE `GM-StylePerc.sfz`; transient-preserving pitch shift via `PitchShiftEngine` |
| Drums (synthesis) | Fully | Hand-rolled multi-component per MIDI key — kick/snare/hi-hat/tom/rimshot recipes with pitch sweep + filtered noise |
| Organ (additive + formant) | Fully | 6 drawbar partials at 16'/8'/5⅓'/4'/2⅔'/2' + parallel 3-formant vowel filter bank (700/1220/2600 Hz) |
| Formant vocal synthesis | Fully | `(sing phoneme note dur)` |
| External TTS hook | Fully | `(tts text)`, `(setTtsCommand cmd)`; defaults to `espeak-ng --stdout` |
| Sample-import varispeed `loadWav` | Fully | `(loadWav path [Int semitones])` or `(loadWav path Double ratio)`; identity short-circuits at semitones=0 / ratio=1.0 |
| SFZ orchestral sampler | Fully | `use "@sfz"`, `(loadSfz #symbol)` (20-entry GM dict) or `(loadSfz "/abs/path.sfz")`; `(renderSong song "sampler:NAME")` dispatch |
| Vocaloid-style voice synthesis | Not yet | Planned |

### SFZ sampler details

| Feature | Status | Notes |
|---|---|---|
| Common-subset SFZ parser | Fully | 4 header types (`<control>`, `<global>`, `<group>`, `<region>`); max 10000 regions |
| 20-opcode whitelist | Fully | `sample`, `lokey`, `hikey`, `pitch_keycenter`, `lovel`, `hivel`, `loop_mode`, `loop_start`, `loop_end`, `ampeg_attack`, `ampeg_release`, `volume`, `pan`, `default_path`, round-robin (`seq_position`, `seq_length`), velocity crossfade (`xfin_lovel`, `xfin_hivel`, `xfout_lovel`, `xfout_hivel`) |
| Per-region sustain looping | Fully | 441-frame equal-power crossfade at loop seam |
| Round-robin sample rotation | Fully | Per-render counter; resets at `renderSong`/`writeWav` boundary; `seq_length > 100` clamped |
| Velocity-layer crossfade | Fully | Equal-power sin/cos curve; sibling-in-band 0.7071 headroom factor |
| Per-voice + per-region pan composition | Fully | Effective pan = region.Pan + voice.Pan, clamped to [-1, +1] |
| SFZ-loaded percussion routing | Fully | Dict-symbol `#drums` (not filename) drives `IsPercussion=true`; absolute-path loads stay non-percussion |
| Blessed external library: VSCO Community CE 1.1.0 | Fully | Not vendored — composer installs separately; `sfz_root` in `~/.config/flow/config.toml` |
| 20-entry GM symbol dict | Fully | 16 verified VSCO-CE: violin/viola/cello/contrabass/flute/oboe/clarinet/bassoon/trumpet/horn/trombone/tuba/piano/harp/timpani/drums; 4 placeholders (choir/guitar/harpsichord/celeste — use absolute-path overload) |

### Effects (DSP)

| Feature | Status | Notes |
|---|---|---|
| Schroeder reverb (4 parallel comb + 2 series allpass) | Fully | `(reverb buf roomSize [damping mix])` and `(reverb buf roomSize Second decay)`; Schroeder closed-form RT60 mapping |
| Biquad filters (Direct Form I, RBJ Cookbook) | Fully | `lowpass`/`highpass`/`bandpass` with Hertz-typed overloads; default Q=0.707 (Butterworth) |
| Peak-detect compressor with attack/release | Fully | `(compress buf thresholdDb ratio [attackMs releaseMs])`; music-typed `Decibel`/`Millisecond` overload |
| Sidechain compression | Fully | `(sidechain buf trigger thresh ratio [attack release])`; trigger drives envelope follower |
| Feedback delay | Fully | `(delay buf ms feedback mix)`; tempo-synced `(delay buf NoteValue ...)`; Millisecond-typed overload |
| `gain` (dB) vs `volume` (linear) | Fully | Semantic-intent split; both emit clipping warnings; `volume` rejects negative (use `gain` for dB attenuation) |
| Constant-power stereo panner | Fully | `(pan buf x)` with x ∈ [-1, 1] — `cos²(θ) + sin²(θ) = 1`; always promotes mono → stereo |
| Mix builtins | Fully | `(mix a b)` unity-gain; `(mixBuffers a b gainA gainB)`; mono-to-stereo auto-promotion |
| `(tempoRamp seq startBPM endBPM [synth])` | Fully | Linearly interpolates per-bar BPM |
| Fades | Fully | `(fadeIn buf sec)`, `(fadeOut buf sec)` |
| Granular synthesis | Fully | `(granular buf grain density jitter [windowing=#hann\|#gaussian\|#tukey])`; Hertz / Millisecond typed; PRNG-routed jitter |
| Time-stretch (without pitch change) | Fully | `(stretch buf factor [mode frameSize hopSize overlap transientThreshold pitchPeriod windowSize])`; identity fast-path at factor=1.0 |
| Pitch-shift (without time change) | Fully | `(pitchShift buf cents ...)` accepting Double / Cent / Semitone (24 overloads = 3 cents-types × 8 arities); identity fast-path at cents=0 |
| Stretch / pitch-shift modes | Fully | `#vocoder` (Laroche-Dolson 1999 phase-locked STFT vocoder), `#psola` (TD-PSOLA + YIN pitch detection), `#auto` (Fitzgerald 2010 HPS per-frame picker + one-shot summary advisory) |
| Underlying spectral utilities | Fully | Radix-2 Cooley-Tukey FFT; Hann/Gaussian/Tukey windows; Harmonic-Percussive Source separator |

### Articulation envelopes

| Feature | Status | Notes |
|---|---|---|
| Per-articulation locked ADSR shaping | Fully | Staccato 25% duration + S=0 + R×0.5; Marcato 25% duration + +0.30 velocity; Tenuto R×1.2 + 100% duration; Legato +110% duration + crossfade; Accent +0.30 velocity; Sforzando 1.5× → 1.0× spike over first 15% |
| Sample-path per-articulation multiplier overlay | Fully | Quartile-split A/D/S/R scalars on top of the locked envelope at SFZ + bundled-sample caller sites only (synth-path baselines unchanged) |
| Drum opt-out | Fully | `isPercussion: true` no-op for percussive synths |

### Voice / polyphony / mixing

| Feature | Status | Notes |
|---|---|---|
| Polyphonic voice allocation | Fully | Two policies: legacy keep-loudest-N AND steal-oldest pool with deterministic tiebreaker (original input index) |
| Voice-pool block (`voicePool N { }`) | Fully | Range [1, 256]; default 32; truncates oldest voice on overflow with 5ms fade |
| Voice-block polyphony (`{voice ...}`) | Fully | Multiple parallel voices share onset; same render path for audio + MIDI export |
| Polyrhythm | Fully | `(polyrhythm seqA seqB [beats])` |
| Per-voice pan | Fully | Constant-power; threads through SFZ + synth paths |
| Per-section context (tempo, pan, gain, reverbTime, voicePool, sustainPedal, tuning) | Fully | Per-section context overlays drive renderer choices |
| Sustain pedal | Fully | `sustainPedal { }` extends every note's buffer by 2.0s |
| `(setMaxVoices N)` | Fully | Runtime voice-pool ceiling override |
| Beat-synced live reload | Fully | Quantizes file-watch reloads to next bar with 64-sample crossfade |

### Audio I/O & playback

| Feature | Status | Notes |
|---|---|---|
| WAV export (`writeWav`) | Fully | 16 / 24 / 32-bit PCM; sample rate from buffer; auto-create parent directory |
| WAV TPDF dithering | Fully | Triangular Probability Density Function dither at 1 LSB on 16/24-bit paths; deterministic-seeded (`0xD17E2`); reseeded per export → byte-identical writes |
| WAV import (`loadWav`) | Fully | 16 / 24 / 32-bit PCM; auto-resample to 44100 Hz; chunk-walking parser; varispeed overloads (semitones / ratio) |
| Real-time playback | Fully | `play`, `loop`, `(loop buf count)`, `preview` (mono 22050 Hz), `stop`, `(stream buf)`/`(stream seq)` |
| PulseAudio backend | Fully | Also works on PipeWire via PA compatibility layer; `PA_SAMPLE_FLOAT32LE`, channels 1–8 |
| Audio backend abstraction | Fully | `IAudioBackend` ready for portability; only PulseAudio implemented |
| Audio device enumeration & selection | Partial | `(audioDevices)`, `(setAudioDevice)`, `(isAudioAvailable)` — PulseAudio Simple API returns empty / throws (use `--device` at CLI) |
| Capture mode for headless / tests | Fully | `FLOW_SUPPRESS_PLAYBACK=1` env routes playback to capture buffer |
| macOS / Windows playback backends | Not yet | `IAudioBackend` abstraction in place; LSP-only on these platforms today |

### MIDI

| Feature | Status | Notes |
|---|---|---|
| MIDI file export (`writeMidi`) | Fully | DryWetMidi 8.0.3; Standard MIDI File Format 1 (multi-track) |
| Multi-track export with GM-program routing | Fully | One track per unique sequence name + conductor track; `violin*→40`, `viola*→41`, `cello*→42`, ..., `piano*→0`, `brass*→56`, `sax*→65`, `flute*→73`, `string*→48`, `organ*→19`, `bell*→14`, `drum*→0 ch9` |
| TPQN auto-elevation | Fully | Default 480, elevated to LCM with tuplet denominators; hard cap 9600 (atomic — rejects before any disk IO) |
| MIDI velocity through to render | Fully | Velocity × 127 clamped [1, 127] |
| Microtonal MIDI export advisory | Partial | Non-12-TET tuning fires one-shot stderr advisory; pitch-bend export deferred to v1.6+ |
| MIDI file import | Partial | Standalone `flow-midi` CLI — reads `.mid`, emits `.flow` source; hand-rolled SMF Format 0/1 parser; running-status; no in-language `loadMidi` builtin |
| `flow midi2flow` CLI subcommand | Fully | `--sustain`/`--no-sustain`, `--sfz`/`--no-sfz`, `--dump`, `-o` flags |
| `flow flow2midi` CLI subcommand | Fully | Script must contain `(writeMidi ...)`; CLI forwards `-o` informationally |

### Notation IO (`use "@notation-io"`)

| Feature | Status | Notes |
|---|---|---|
| MusicXML 3.1 partwise export | Fully | `(writeMusicXML path song)`; multi-part per sequence-name; voice blocks → `<voice>N</voice>`; microtonal `<alter>` cent precision; articulations map per MuseScore convention; hand-rolled `XmlWriter` for deterministic output |
| LilyPond 2.24+ export | Fully | `(writeLilyPond path song)`; per-Sequence `\new Staff`; voice blocks → `<< { v1 } \\ { v2 } >>`; microtonal `% +Nc` cent-offset comments; Dutch pitch convention (`cis`, `bes`) |
| ABC 2.1 import | Fully | `(abc source)` returns `Section` (single tune) or `Array[Section]` (multi-tune via `X:N` headers); modal keys (Edor/Dmix/Aphr/Cmix/Glyd/Bphr/Floc); `Q:` tempo (bare BPM, `1/4=BPM`, `"Allegro" 1/4=BPM`); charitable on unknowns |
| PC-98 MML import | Fully | `(mml source)` returns `Sequence`; notes / accidentals / octave / length / tempo / loops with depth-cap 16 + expansion-cap 65536 (DoS guards); FM operator + drum bank dropped with advisory |

### Microtonal & tuning

| Feature | Status | Notes |
|---|---|---|
| Cent offsets in note streams | Fully | `C4+50c`; preserved through transforms and voice blocks |
| Named tunings via pragma | Fully | `enable justIntonation;`, `enable pythagorean;`, `enable equalTemperament;` — file-scope, last-wins with `tuning { }` blocks |
| Scala `.scl` loader | Fully | `(loadScala path)` returns `Tuning`; cents/ratio/implicit-integer step formats |
| Explicit `.kbm` keyboard mapping | Fully | `(loadScala sclPath kbmPath)` 2-arg overload; period auto-overlaid from `.scl` |
| `tuning <expr> { }` musical-context block | Fully | Three composer surfaces: identifier-bound variable, inline call, string-literal sugar |
| Period auto-adoption for non-octave scales | Fully | Carlos Alpha, Bohlen-Pierce Just Work without explicit `.kbm` |
| Unmapped-keys advisory | Fully | One-shot stderr per `Tuning.Description` when KBM leaves MIDI keys unmapped |
| `(str Tuning)` description format | Fully | `Tuning("<description>", N steps, period X.XX¢)` |

### Determinism contracts

| Feature | Status | Notes |
|---|---|---|
| Two-run cmp-clean rendering | Fully | Consecutive renders at same git SHA produce byte-identical WAV bytes |
| `PrngRegistry` single source of truth | Fully | All stochastic primitives keyed by `(SourceLocation, generator-name)`; FNV-1a stable seed derivation; reseeded at `renderSong`/`writeWav` boundary |
| Source-grep CI gate on PRNG routing | Fully | `PrngRegistryNewRandomGateTests` bans unsanctioned `new Random(` in Patterns/Generative/Improv; documented `// PRNG-SANCTIONED:` exceptions |
| Deterministic synth white-noise + WAV dither | Fully | Fixed seeds reset per `renderSong` / per export |
| SFZ round-robin counter reset | Fully | Per-render fresh `SfzRenderer` construction |
| Same-platform-only chaos primitives | Partial | `lorenz` / `logistic` preserve same-platform two-run cmp-clean only — chained FP arithmetic amplifies cross-platform quirks beyond ~50 iterations. Markov / L-system / cellular stay cross-platform deterministic |

### CLI (`flow`)

| Feature | Status | Notes |
|---|---|---|
| `flow run <script>` | Fully | `--device`, `-v`/`--verbose` flags |
| `flow eval <code>` | Fully | Same flags as `run` |
| `flow repl` | Fully | Auto-imports `@std`, `@audio`, `@collections` |
| `flow watch <script>` | Fully | Bar-boundary buffer swap + 64-sample crossfade; failed render keeps previous version |
| `flow play <script>` | Fully | Script must contain `(play ...)` (no auto-injection) |
| `flow render <script> -o out.wav` | Partial | Script must contain `(writeWav ...)` — `-o` is informational; auto-injection deferred to v1.5+ |
| `flow flow2midi <script> -o out.mid` | Partial | Script must contain `(writeMidi ...)` — `-o` is informational |
| `flow midi2flow <input.mid> [-o]` | Fully | Reverses the MIDI export — emits round-trip-friendly `.flow` source |
| `flow check <script>` | Partial | Parse-AND-execute (true parse-only mode deferred) |
| `flow new <name> [--dir]` | Fully | Scaffolds a Flow project from embedded template |
| `flow lsp` | Fully | Starts LSP server over stdio |
| `flow test [path]` | Fully | Runs `test_*.flow` files via pure-Flow framework |
| `flow version` | Fully | |
| Legacy `flow-interpreter` binary | Fully | Maintained for backward compat; `--watch`, `-e`, `--stdin`, `--device`, `--verbose` flags |

### REPL & script runner

| Feature | Status | Notes |
|---|---|---|
| `-e` / `--eval` flag | Fully | |
| `--watch` / `-w` mode | Fully | |
| stdin input (`echo ... \| flow-interpreter --stdin`) | Fully | Auto-detected on `Console.IsInputRedirected` |
| `--verbose` diagnostics on stderr | Fully | |
| REPL multi-line input | Fully | Backslash continuation OR `proc`-detection auto-multiline |
| REPL `:quit` / `:help` / `:clear` / `:stop` commands | Fully | |
| Rich Rust-style diagnostics in REPL | Fully | Selected automatically when source spans are available |
| REPL tab completion / history / syntax highlighting | Not yet | |

### Diagnostics & error display

| Feature | Status | Notes |
|---|---|---|
| Rust-style multi-line diagnostic renderer | Fully | Colored output (red/yellow/cyan), caret line, gutter, `= note:`, `= help: did you mean '...'?` |
| Did-you-mean suggestions (Levenshtein) | Fully | Threshold `max(2, len/3)` — surfaces for unknown variables, unknown pragmas, missing functions |
| Error accumulation (multiple errors per pass) | Fully | `ErrorReporter.FormatErrors()` / `FormatDiagnostics(srcMap, useColor)` |
| ANSI color toggle | Fully | Auto-off when stdout redirected; explicit `useColor: false` for golden-file tests |
| Charitable interpretation throughout | Fully | Degenerate inputs return reasonable defaults + one-shot stderr advisory; never throws |

### LSP (`flow-lsp`)

LSP 3.17 over stdio. Reachable via `flow lsp` subcommand. OmniSharp `LanguageServer` library; no audio (registers signatures-only).

| Capability | Status | Notes |
|---|---|---|
| Publish diagnostics | Fully | Six analyzer sources merged per parse cycle: parse errors, `flow.scaleLint`, `flow.unusedImport`, `flow.unreachableSection`, `flow.shadowedVariable`, `flow.undefinedSymbol` |
| Semantic tokens (full) | Fully | Hybrid with TextMate grammar; 9-entry legend (Keyword/Type/String/Number/Operator/Comment/Variable/Function/Macro) |
| Completion | Fully | Context-aware 5-source merge (builtins + stdlib + user + keywords + snippets); inside `key { }` boosts roman numerals; inside `use "..."` only stdlib paths; inside note streams only context-appropriate items |
| Hover | Fully | Markdown content; 3-way lookup (builtin docs via `BuiltInDocs.cs` / user symbol / stdlib proc) |
| Go-to-definition | Fully | User symbols (recursive AST walk) + stdlib import paths; builtins return null |
| Signature help | Fully | Comma-count active-parameter; trigger chars `(`, `,` |
| Semantic tokens delta | Not yet | Full responses only |
| Find references | Not yet | |
| Rename | Not yet | |
| Code actions | Not yet | |
| Document symbols / workspace symbols | Not yet | |
| Inlay hints | Not yet | |
| Formatter | Not yet | No formatter exists |
| Stdlib completion for newer modules | Partial | `@sfz`, `@patterns`, `@generative`, `@improv`, `@notation-io` not in `StdlibSymbolIndex.ModuleNames` yet (still hard-coded to 6 original modules) |

### Editor support

| Editor | Status | Notes |
|---|---|---|
| VSCode extension | Fully | Bundles per-platform `flow-lsp` (linux-x64 / win32-x64 / darwin-x64 / darwin-arm64); TextMate grammar (125 lines, 5 comment variants), language config, 5 snippets, `flow.server.path` + `flow.trace.server` settings |
| Marketplace + OpenVSX publish | Partial | Workflow ready; per-tag publish (`v*`); OpenVSX namespace claim is one-time manual |
| JetBrains plugin (`flow-jetbrains`) | Partial | LSP-only via LSP4IJ; IntelliJ Platform 2024.2+; manual install from `.zip` (stretch deliverable) |
| Neovim via LSP | Fully | `docs/editor-setup/neovim.md` |
| Helix via LSP | Fully | `docs/editor-setup/helix.md` |
| Emacs via LSP | Fully | `docs/editor-setup/generic-lsp.md` — `lsp-mode` + `eglot` recipes |
| Zed / Cursor / Windsurf via generic LSP | Fully | `docs/editor-setup/generic-lsp.md` |
| Debugger / DAP integration | Not yet | |

### Test framework & regression infrastructure

| Feature | Status | Notes |
|---|---|---|
| Pure-Flow test framework | Fully | `(test "name" body)`, `(assert)`, `(assertEq)`, `(assertNotesMatch)`, `(assertBytesEqual)`, `(assertWithinDb)`; bodies are `Lazy`-wrapped for hermetic isolation |
| Hermetic snapshot/restore | Fully | `TestSnapshot` captures 11+ mutable engine surfaces per-test |
| `flow test [path]` runner | Fully | Single-file OR directory mode (`test_*.flow` glob, no recursion) |
| RMS-windowed regression helpers | Fully | `AssertRmsWithinTolerance` / `AssertWavMatchesBaseline` — ±0.5 dB / 100 ms tolerance |
| Two-run determinism script | Fully | `scripts/test_two_run_determinism.sh` — renders twice and compares SHA-256s |
| Source-grep CI gates | Fully | PRNG routing, named-arg coverage, sample-bundle license audit |
| xUnit C# test project | Fully | `flow-lang.Tests/` — standard `dotnet test` |
| Legacy `.flow`-as-test scripts | Fully | 123 `test_*.flow` files in `tests/` (run-and-check-exit-code style) |

### Tooling & DX

| Feature | Status | Notes |
|---|---|---|
| Math stdlib | Fully | `sin`/`cos`/`tan`/`sqrt`/`floor`/`ceil`/`round`/`log`/`pow`/`abs`/`min`/`max` + `pi`/`tau` constants |
| Buffer pretty-printing | Fully | `(prettyBuffer buf)` (60×11 ASCII waveform), `(bufferHex buf [offset length])` |
| Documentation lookup table | Fully | `BuiltInDocs.cs` — 104 entries powering LSP hover (positioned for future `flow help <fn>`, not yet exposed) |
| Wiki | Fully | 26 markdown chapters synced via `.github/workflows/wiki-sync.yml` |
| `flow help <fn>` subcommand | Not yet | |
| `flow` CLI binary + system install | Fully | `scripts/install.sh` per-user (`~/.local/share/flow/...` + `~/.local/bin/flow` symlink) or `--system` mode |

### Configuration

| Feature | Status | Notes |
|---|---|---|
| `~/.config/flow/config.toml` | Fully | Tomlyn 2.3.2 loader; 6 keys: `install_path`, `default_audio_device`, `default_tempo`, `default_timesig`, `stdlib_search_path`, `sfz_root` |
| `~/.config/flow/styles/*.flow` user style packs | Fully | Last-write-wins over shipped packs |
| Charitable config loading | Fully | Missing file → silent default; malformed TOML → stderr warning + defaults (never aborts) |
| `$XDG_CONFIG_HOME` | Not yet | Hard-coded `~/.config/flow/` |

### Example scripts (tutorial chapters)

| Folder | Topic |
|---|---|
| `examples/dsp/` | Granular synthesis, time-stretch, pitch-shift |
| `examples/generative/` | Markov chains, Tidal-style combinators |
| `examples/improv/` (via style packs) | Chord-aware Markov improvisation |
| `examples/notation/` | MusicXML + LilyPond export, ABC + MML import |
| `examples/pragmas/` | `enable hAsB;`, `enable justIntonation;` demos |
| `examples/scala/` | Microtonal `.scl` tuning loader walkthrough |
| `examples/sections/` | Parameterized sections with defaults + `*N` repetition |

### Platform support

| Platform | Status | Notes |
|---|---|---|
| Linux x64 (primary) | Fully | PulseAudio backend; self-contained 38 MB single-file binary at GitHub Releases |
| Windows x64 / macOS x64 / macOS arm64 | Partial | VSCode extension ships per-platform `flow-lsp.exe` (LSP-only — no playback backend) |
| Cross-platform official builds | Not yet | v1.5+ backlog |
| Homebrew / AUR / apt PPA / Snap / AppImage / NuGet | Not yet | Only GitHub-release `.tar.gz` consumed by `install.sh` |
