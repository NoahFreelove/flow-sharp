# Flow Documentation-Example Verification — 2026-06-14

**Date:** 2026-06-14
**Method:** Every fenced Flow code block in the wiki + top-level docs was extracted and run on the real desktop Flow interpreter (`dotnet run --project flow-interpreter`), one verification agent per page. Each block was classified as **pass**, **broken** (genuine parse/eval/runtime error reproducible standalone), **desktop-only** (works on Desktop but fails on the Web playground because of a Web-stripped module), or **fragment** (illustrative snippet that errors standalone because it depends on unstated context or an external file). Prose claims were checked against actual engine behavior and source. Three issues were confirmed against the engine ahead of synthesis (the CONFIRMED SEED) and are carried into the prose section.

**Counts (28 files):**

- Total Flow examples examined: **482**
- Broken examples (must fix): **94**
- Desktop-only examples: **3**
- Fragments: **39**
- Prose / wording issues: **63**

The most problematic pages are **Tips-and-Tricks.md** (13 broken), **Examples.md** (12 broken), **Audio-and-Synthesis.md** (9 broken), and **Playback-and-Export.md** (9 broken).

The single most pervasive defect across the whole corpus is the use of **`buf` as a variable name** — `buf` is a reserved lexer token (`TokenType.Buf`) and produces `Expected variable name. Got Buf 'buf'` everywhere it appears. The second most common is the **removed `exportWav` function** (deleted in Phase 46 D-06; use `writeWav` path-first). Other recurring root causes: infix arithmetic (`+ - * /`) in a prefix-only language, the `length` alias that is not registered (only `len` exists), wrong module imports (`@audio` instead of `@composition`), bare NoteValue letters (`q`/`e`/`s`) used outside note streams, `Array[T]` instead of `T[]`, bare `<<T, T>>` instead of `Tuple<<T, T>>`, `return`/`break` inside `lazy(...)`, and the `jam` `seed=`/`key=` named-arg overload trap.

---

## Summary table

| File | total | broken | desktop-only | fragment | prose |
|---|---|---|---|---|---|
| wiki/Audio-and-Synthesis.md | 20 | 9 | 1 | 6 | 6 |
| wiki/Chord-Progressions.md | 8 | 1 | 0 | 0 | 0 |
| wiki/Chords-and-Harmony.md | 16 | 4 | 0 | 1 | 2 |
| wiki/Collections.md | 18 | 3 | 0 | 0 | 3 |
| wiki/Dynamics-and-Expression.md | 20 | 1 | 0 | 1 | 1 |
| wiki/Effects.md | 16 | 2 | 0 | 8 | 2 |
| wiki/Examples.md | 40 | 12 | 1 | 2 | 5 |
| wiki/Flow-Operator.md | 13 | 2 | 0 | 2 | 1 |
| wiki/Functions.md | 19 | 3 | 0 | 0 | 2 |
| wiki/Generative.md | 20 | 7 | 0 | 1 | 5 |
| wiki/Home.md | 1 | 1 | 0 | 0 | 0 |
| wiki/Imports-and-Modules.md | 10 | 0 | 0 | 3 | 0 |
| wiki/Language-Basics.md | 26 | 5 | 0 | 1 | 3 |
| wiki/Loops.md | 15 | 2 | 0 | 0 | 2 |
| wiki/Musical-Context.md | 17 | 1 | 0 | 1 | 1 |
| wiki/Note-Streams.md | 29 | 2 | 0 | 0 | 1 |
| wiki/Pattern-Transforms.md | 23 | 3 | 0 | 0 | 4 |
| wiki/Playback-and-Export.md | 16 | 9 | 0 | 0 | 3 |
| wiki/Quick-Start.md | 5 | 1 | 0 | 0 | 1 |
| wiki/Song-Structure.md | 16 | 4 | 0 | 3 | 4 |
| wiki/Standard-Library.md | 2 | 0 | 0 | 0 | 4 |
| wiki/String-Interpolation.md | 15 | 2 | 0 | 1 | 1 |
| wiki/Tips-and-Tricks.md | 38 | 13 | 0 | 5 | 6 |
| wiki/Visualization.md | 9 | 1 | 0 | 1 | 1 |
| wiki/Vocalization.md | 8 | 0 | 0 | 0 | 1 |
| wiki/Voices-and-Tracks.md | 10 | 5 | 0 | 0 | 3 |
| README.md | 4 | 1 | 0 | 3 | 1 |
| FEATURES.md | 0 | 0 | 0 | 0 | 1 |
| **TOTAL** | **482** | **94** | **3** | **39** | **63** |

---

## 1. Broken examples (must fix)

### wiki/Audio-and-Synthesis.md

- **Creating Buffers (lines 11-27)** — `Expected variable name. Got Buf 'buf'`. `buf` is reserved. Fix: rename every `buf` to `mybuf`, e.g. `Buffer mybuf = (createBuffer 44100 2 44100)`.
- **Buffer Manipulation (lines 47-56)** — (1) `Buffer buf = buf1` uses reserved `buf`; (2) `Buffer scaled = (scaleBuffer buf 0.5)` fails: `Cannot convert Flow type 'Void' to Flow target type 'Buffer'` (`scaleBuffer` mutates in place, returns Void). Fix: rename `buf`→`mybuf`; `(scaleBuffer mybuf 0.5)` with no assignment; copy first if preserving the original (`Buffer toScale = (copyBuffer mybuf)`).
- **Oscillators (lines 75-87)** — reserved `buf`. Fix: `Buffer mybuf = (createBuffer 44100 1 44100)` and use `mybuf` in the generate calls.
- **Custom Oscillator — From an Array (lines 97-113)** — infix arithmetic: `Unexpected token Slash '/'`. Bad: `(id / tableSizeD) * 2.0 - 1.0`. Fix: `Double sample = (sub (mul (div id tableSizeD) 2.0) 1.0)`.
- **Custom Oscillator — From a Lambda (lines 117-122)** — infix arithmetic: `Expected ')' after expression. Got Slash '/'`. Bad: `(idx -> intToDouble) / (sz -> intToDouble) * 4.0 - 2.0`. Fix: `(sub (mul (div (idx -> intToDouble) (sz -> intToDouble)) 4.0) 2.0)`.
- **Custom Oscillator — Using a Custom Oscillator (lines 134-137)** — embeds the broken infix lambda and uses reserved `buf`. Fix: prefix arithmetic in the lambda + rename `buf`→`mybuf`.
- **Custom Instrument Lambdas (lines 258-262)** — engine bug: `Cannot convert Flow type 'MusicalNote' ... to Flow target type 'Note'`. The lambda receives a `MusicalNote`; `noteToFrequency` is registered with `NoteType.Instance` and the overload is selected (`MusicalNoteType.CanConvertTo(NoteType)==true`) but `Value.ConvertTo` has no MusicalNote→Note case and throws. Fix: add a `MusicalNote→Note` case in `flow-lang/Runtime/Value.cs` (+ a `MusicalNoteType.Instance` overload of `noteToFrequency`), or document the limitation and use a hardcoded frequency in the lambda.
- **BPM and Timeline (lines 293-301)** — `Function 'setBPM' not found` / `getBPM` not found. These live in `@composition`. Fix: change `use "@audio"` to `use "@composition"`.
- **Voice and Track System (lines 307-315)** — `Function 'createVoice' not found`. `createVoice`/`setVoiceGain`/`setVoicePan`/`createTrack`/`addVoice`/`renderTrack` are in `@composition`. Fix: add `use "@composition"`.

### wiki/Chord-Progressions.md

- **Rendering a Progression (ex7)** — `Function 'exportWav' not found`. Bad: `(exportWav final "ivvi_iv_v.wav")`. Fix: `(writeWav "ivvi_iv_v.wav" final)` (path-first).

### wiki/Chords-and-Harmony.md

- **Chord Functions — chordRoot/chordQuality/chordNotes (block 3)** — `Function 'length' not found`. Bad: `notes -> length`. Fix: `notes -> len`.
- **Rhythmic Chord Streams with Duration Suffixes (block 5)** — `Empty note stream`. The lexer only splits note+duration for note literals (`C4q`), not chord literals (`Cmaj7q`). Fix: add a space: `| Cmaj7 q Am7 q Dm7 h G7 h. Cmaj7 q |`.
- **Arpeggios — 4-arg form with NoteValue rate (block 10)** — `unknown identifier 'q'`. Bare `q`/`h`/`e`/`s` are only valid as duration suffixes inside note streams. Fix: `use "@notation"`, use `QUARTER`/`SIXTEENTH`: `(arpeggio Cmaj7 QUARTER "updown" "linear")`.
- **Section Query Functions — getSections / sectionSequences (block 16)** — `unknown identifier 'intro'`. Section names live in an internal SectionRegistry, not variable scope; `sectionSequences` is unreachable from composer code. Fix: drop the `sectionSequences` call (keep the working `getSections mySong`) until the engine exposes `getSection(Song, String) -> Section`.

### wiki/Collections.md

- **Array Indexing (negative index)** — `Unexpected token Minus '-'`. Bad: `nums@-1`. Fix: `nums@(neg 1)`.
- **Inspection (length alias)** — `Function 'length' not found`. Fix: `(len nums)`.
- **Zip example** — `Int[][]` is not valid type syntax, and `zip` is never registered (`Function 'zip' not found`, tracked as BUG-3). Fix: remove the Zip section or mark it unimplemented; manual workaround via `map`+`range`.

### wiki/Dynamics-and-Expression.md

- **Combining Expression Techniques (block 20)** — reserved `buf`: `Expected variable name. Got Buf 'buf'`. Bad: `Buffer buf = (renderSong piece "piano")`. Fix: `Buffer result = (renderSong piece "piano")`.

### wiki/Effects.md

- **Delay (EIGHTH NoteValue) (block 5)** — `unknown identifier 'EIGHTH'`. NoteValue constants are in `@notation`. Fix: add `use "@notation"` and define `tone` first.
- **Effect Chaining (block 14)** — `Unexpected token Minus '-'`. A negative bare literal as the first arg of a parenthesized pipe call (`-> (pan -0.2)`) does not parse. Fix: bind it first: `Double panLeft = (sub 0.0 0.2)` then `-> (pan panLeft)`.

### wiki/Examples.md

- **String Interpolation** — reserved `key`: `Expected variable name. Got Key 'key'`. Fix: rename `key`→`keyName`.
- **While Loop with `break`** — `Unexpected token Break 'break'`. `break` cannot live inside `lazy(...)`. Fix: encode the exit in the loop condition with a Bool flag.
- **Tuples and `~>` Unpack** — `Cannot assign Double to variable of type Int`. The last line references an undefined `double` proc; `double` lexes as the type `Double`, so `5 ~> double` becomes a cast. Fix: define the `double` proc first or remove the line.
- **Simple Melody** — reserved `buf`. Fix: rename `buf`→`audio`.
- **Chord Progression** — reserved `progression`: `Expected section name. Got Progression`. Fix: rename the section `progression`→`chordProg`.
- **Full Song with Sections** — `Cannot assign Double to variable of type Int`. `(div frames 44100)` returns Double. Fix: `(idiv frames 44100)`.
- **Voice-Block Polyphony** — reserved `buf`. Fix: rename `buf`→`audio`.
- **L-Systems** — `Function 'createMusicalNote' not found` (it is `internal`). Fix: `use "@notation"` and use `(quarter C4)` / `(quarter E4)`.
- **Chord-Aware Improvisation with `jam`** — `unknown parameter 'seed'`; `key` reserved so `key=` cannot be a named-arg label. Fix: positional form `(jam chords #jazz 4 "Cmajor" 42 2)`, or 3-named-arg form `(jam over=chords style=#jazz length=4)`.
- **Granular Synthesis** — `Unexpected token '-'`. Bad: `-> pan -0.3` parses as infix subtraction. Fix: `-> pan (neg 0.3)`.
- **Audio Synthesis from Scratch** — reserved `buf`. Fix: rename `buf`→`audio` throughout.
- **Waltz in 3/4** — reserved `buf`. Fix: rename `buf`→`audio`.

### wiki/Flow-Operator.md

- **Tuple-Unpack `~>` example (block 11)** — `Unexpected token LessThan '<'`. Bad: `<<Note, Note>> entry = <<C4, D4>>`. Fix: `Tuple<<Note, Note>> entry = <<C4, D4>>` (and `Tuple<<Int, Int, Int>> trip = <<1, 2, 3>>`).
- **`(unpack ...)` example (block 12)** — `Expected identifier in destructure pattern`. Bad: `<<Int, Int, Int>> trip = <<1, 2, 3>>`. Fix: `Tuple<<Int, Int, Int>> trip = <<1, 2, 3>>`.

### wiki/Functions.md

- **proc add (block 2)** — `Recursion depth limit (1000) exceeded`. The proc named `add` shadows the builtin, so `(add a b)` recurses. Fix: rename to `myAdd`.
- **abs with return inside lazy (block 3)** — `Unexpected token Return 'return'`. `return` is not valid inside `lazy((...))`. Fix (implicit return): `(if (lt x 0) lazy ((sub 0 x)) lazy (x))`.
- **named args with markov (block 7)** — `positional argument after named argument is not allowed`; `features=` is only on `markovTrain`. Fix: all-named `(markov corpus=base order=2 length=16 seed=42)` or all-positional `(markov base 2 16 42)`.

### wiki/Generative.md

- **Composing combinators (lines 164-177)** — `No matching overload for function 'every' with argument types (Sequence, Int, Function)`. `->` inserts the value first but `every`/`sometimes`/`jux` take Sequence last; also `7st` needs a sign. Fix: explicit nesting `(every 4 (fn ...) base)` ... and `+7st`.
- **L-system (lines 241-253)** — cascading: `Dict<Symbol, Tuple>`→`Dict<Symbol, Symbol[]>`; rule values need `(list #A #B)` not `<<#A #B>>`; `eq`→`equals`; mapper must return MusicalNote (`createMusicalNote`/`quarter`); missing `use "@std"`/`use "@notation"`; `Array[Symbol]`→`Symbol[]`.
- **Cellular automata (lines 267-279)** — `Array[Bool]`/`Array[Sequence]` invalid. Fix: `Bool[]` / `Sequence[]`.
- **Chaos maps (lines 291-300)** — `Array[Double]` invalid. Fix: `Double[]`.
- **jam example (lines 319-332)** — `seed=` requires `key=` (no seed-without-key overload). Fix: drop `seed=` or add `key="Cmajor"` before it.
- **Polyrhythm (lines 397-411)** — `Function 'polyrhythm' not found`. Fix: add `use "@composition"`.
- **Combining techniques (lines 433-465)** — combinator pipe arg-order + reserved `buf` + seed-without-key jam. Fix: explicit nesting + rename `buf`→`output` + drop `seed=`.

### wiki/Home.md

- **Quick Example — full song render pipeline** — `Function 'exportWav' not found`. Bad: `(exportWav final "my_song.wav")`. Fix: `(writeWav "my_song.wav" final)`.

### wiki/Language-Basics.md

- **Array indexing with `nums@-1` (block 10)** — `Unexpected token Minus '-'`. Fix: bind first: `Int negOne = (sub 0 1)` then `nums@negOne`.
- **Tuple type annotations `<<Int, Int>>` (block 11)** — `Expected identifier in destructure pattern`. Fix: `Tuple<<Int, Int>> point = <<3, 4>>` (and the other three declarations).
- **Tuple-unpack `~>` with `<<Note, Note>>` decl (block 13)** — `Unexpected token LessThan '<'`. Fix: `Tuple<<Note, Note>> entry = <<C4, D4>>`.
- **Named arguments jam (block 24)** — `unknown parameter 'seed'`; `key` reserved. Fix: 3-named-arg `(jam over=chords style=#jazz length=8)` or positional with seed `(jam chords #jazz 8 "Cmajor" 42)`.
- **Pragmas with trailing `Note:` comments (block 26)** — `Function 'enable' not found` / `unknown identifier 'hAsB'`. The PragmaScanner only accepts `//` after `enable NAME;`. Fix: use `//` for trailing comments on pragma lines.

### wiki/Loops.md

- **Infinite Loop + break (example 8)** — `Unexpected token Break 'break'`. Fix: rewrite as `while (lt i 5) { i = (add i 1) }`.
- **break section example (example 10)** — `Unexpected token Break 'break'`. Fix: convert the for loop to a while loop with the termination test in the guard.

### wiki/Musical-Context.md

- **renderSong release= inline snippet (line 204)** — reserved `buf`. Bad: `Buffer buf = (renderSong song "piano" release=3.0s)`. Fix: `Buffer result = (renderSong song "piano" release=3.0s)`.

### wiki/Note-Streams.md

- **Rests — explicit duration suffix `_q`** — `Empty note stream`. The lexer merges `_q` into a single identifier; only bare `_` works. Fix: `| C4q _ E4q F4q |`.
- **Voice Blocks — inline dynamics inside `{voice ...}`** — `Unexpected token 'mf' inside voice block`. `ParseVoiceBlockChildren` has no dynamic-marking branch. Fix: remove inline dynamics from voice blocks (articulations like `stacc`/`>` work); set a sticky dynamic before the outer `|` stream.

### wiki/Pattern-Transforms.md

- **quantize (example 15)** — `unknown identifier 'e'`. Bare `e`/`s` are not NoteValue tokens in expression position. Fix: `use "@notation"`, use `EIGHTH`/`SIXTEENTH`.
- **polyrhythm (example 20)** — `Function 'polyrhythm' not found` + `exportWav` does not exist. Fix: `use "@composition"` and `(writeWav "poly.wav" mixed)`.
- **Tidal-style combinators / every (example 21)** — `No matching overload for function 'every' with argument types (Sequence, Int, Function)`. Fix: pass the sequence last: `(every 4 (fn Sequence s => (fast s 2.0)) base)`.

### wiki/Playback-and-Export.md

- **play (blocking) — sequence (lines 27-31)** — `Function 'play' not found` (no `use "@audio"`). Fix: add `use "@audio"`.
- **stream (lines 38-43)** — reserved `buf`. Fix: rename `buf`→`tone`.
- **loop (lines 52-54)** — reserved `buf`. Fix: complete the snippet and rename `buf`→`tone`.
- **preview (lines 62-63)** — reserved `buf`. Fix: complete the snippet and rename `buf`→`tone`.
- **WAV Export basic (lines 107-112)** — reserved `buf`. Fix: rename `buf`→`tone`.
- **WAV Export custom bit depth (lines 117-120)** — reserved `buf`. Fix: rename `buf`→`tone`.
- **exportWav (lines 126-129)** — `Function 'exportWav' not found` (removed Phase 46 D-06) plus reserved `buf`. Fix: remove the section; use `writeWav` path-first.
- **loadWav (lines 136-146)** — reserved `buf`. Fix: rename `buf`→`src`/`loaded` and supply a real WAV path (or write one first).
- **Complete Render-to-File Workflow (lines 241-274)** — `Cannot assign Double to variable of type Int`. `(div frames 44100)` returns Double. Fix: `(idiv frames 44100)`.

### wiki/Quick-Start.md

- **melody.flow (Your First Melody)** — reserved `buf` + `Function 'exportWav' not found`. Fix: rename `buf`→`audioBuf`, replace `exportWav` with `writeWav` and flip args: `(writeWav "melody.wav" audioBuf)`.

### wiki/Song-Structure.md

- **Section Overloading (lines 103-113)** — `Empty note stream` (`| (root) |` is not valid; bare `| root |` is) plus a spurious arity-dispatch error when both 1-arg and 2-arg `verse` overloads are registered. Fix: use `| root |`; the dispatch error is an engine bug (`SectionOverloadDispatch.BuildFinalArgs` should skip to the next candidate instead of erroring).
- **sectionSequences snippet (lines 135-145)** — `unknown identifier 'mySection'`. Section declarations are not first-class Section values. Fix: remove the example or use `getSections` (which works) until `getSection(Song, String) -> Section` exists.
- **renderSong example (lines 161-181)** — reserved `buf` + `exportWav` removed. Fix: `Buffer audio = (renderSong song "piano")` then `(writeWav "song.wav" audio)`.
- **Complete Example (lines 203-239)** — `Function 'exportWav' not found`. Fix: `(writeWav "full_song.wav" final)`.

### wiki/String-Interpolation.md

- **Expressions Inside Braces — `items -> length`** — `Function 'length' not found`. Fix: `items -> len`.
- **Debug Prints — `buf` variable** — reserved `buf`. Fix: rename `buf`→`myBuf`.

### wiki/Tips-and-Tricks.md

- **Optional Parentheses (lines 19-27)** — `unknown identifier 'x'`; `Int s` declared twice. Fix: define `x` and rename to `s1`/`s2`.
- **Debugging snippet (lines 104-112)** — reserved `buf` + undefined `song`. Fix: rename `buf`→`audio`, build `song` from a section.
- **as-binding idiom (lines 150-156)** — undefined `dry`, `gain`/`mix` not found (no `@audio`). Fix: split the runnable first example from the illustrative second, or make the second self-contained.
- **Named arguments example (lines 182-191)** — `Empty note stream` (chords in stream need key context), `key=` reserved as named-arg label, `@generative` not imported. Fix: bracket chords in a `key` block, drop `key=`, import the right module.
- **Match expressions (lines 199-210)** — `unknown identifier 'n'`. Fix: define `n` before the second match or split the blocks.
- **gain vs volume example (lines 253-256)** — `Unexpected token Buf 'buf'`. Fix: rename `buf`→`src` (and provide a real buffer).
- **PRNG determinism snippet (lines 295-301)** — `Function 'sometimes' not found` / `markov` not found / undefined `seq`,`corpus`. Fix: `use "@patterns"` + `use "@generative"` and define inputs.
- **Identity DSP fast-paths (lines 311-315)** — `Unexpected token Buf 'buf'`. Fix: rename `buf`→`audio`.
- **Section patterns (lines 323-343)** — `Unexpected token Ellipsis '...'` (not valid Flow) + undefined `chorus` + empty note stream. Fix: replace `...` with real bodies or `Note:` placeholders; define referenced sections; add key context.
- **Scala microtuning (lines 362-370)** — `Could not find a part of the path '.../tunings/partch_43.scl'`. Fix: use an absolute path / note the reader must supply a real `.scl`.
- **Pitfall 8 — Division by zero (lines 492-495)** — `Unexpected error: Division by zero` (exits 1). The comment claims it returns Void; it throws. Fix: correct the prose and the example.
- **Array indexing with @ (lines 506-511)** — `Unexpected token Minus '-'`. Bad: `nums@-1`. Fix: `nums@(-1)` (parentheses required).
- **Full rendering pattern (lines 517-543)** — reserved `buf`. Fix: rename `buf`→`audio`.

### wiki/Visualization.md

- **Audio diagnosis — `(gain -3.0)` (lines 143-151)** — `Unexpected token Minus '-'`. A bare `-3.0` in arg position fails (no infix arithmetic). Fix: use the Decibel literal `-3dB` (single token), or bind `Double g = (neg 3.0)` first.

### wiki/Voices-and-Tracks.md

- **Creating Voices (line 19)** — `Function 'createVoice' not found`. Fix: add `use "@composition"`.
- **Creating Tracks (line 45)** — `Function 'createTrack' not found`. Fix: add `use "@composition"` (and define `v` first).
- **Rendering a Track (line 68)** — `renderTrack` is in `@composition`; also depends on undefined `t`. Fix: add `use "@composition"` and the preceding setup.
- **BPM and Beat Conversion (line 80)** — `Function 'setBPM' not found` / `getBPM` not found. Fix: add `use "@composition"`.
- **Polyrhythm (line 181)** — no import (`@composition`), `polyrhythm` returns `Buffer` not `Sequence`, and the 3-arg overload wants `Int` not `Double`. Fix: `use "@composition"`, `Buffer poly = (polyrhythm three four)`, `Buffer poly8 = (polyrhythm three four 8)`.
- **Multi-Track Example (line 191)** — `Function 'setBPM' not found` / `createTrack` not found. Fix: add `use "@composition"`.

### README.md

- **Block 1 (line 15): reverb usage illustration** — `Unexpected token Comma ','`. Bad: `reverb(input, 5.0, 5.0, 5.0)` (C-style). Fix: prefix S-expression `(reverb input 5.0 5.0 0.3)`.

---

## 2. Web-playground gotchas (desktop-only)

These run on the Desktop interpreter but fail in the Web playground because they use modules stripped under `FlowTarget=Web`. They should carry a desktop-only callout.

- **wiki/Audio-and-Synthesis.md — SFZ Orchestral Sampler (lines 268-287):** uses `use "@sfz"` (Web-stripped). Also uses reserved `buf` (broken on all targets — rename to `mybuf`) and requires the Desktop build with `sfz_root` configured in `~/.config/flow/config.toml` pointing to a VSCO-CE install. Needs: Desktop build + `@sfz` + `sfz_root` config.
- **wiki/Examples.md — SFZ Orchestral Sampler:** `use "@sfz"` is Web-stripped; `(loadSfz #violin)` additionally requires `sfz_root` in `~/.config/flow/config.toml`. Needs: Desktop build + `@sfz` + `sfz_root` config. Add a note: "Requires FlowTarget=Desktop and `sfz_root` configured; stripped on the web playground."

> Reminder for the fix wave: should any corrected example be expanded to call `@sfz`/`@osc`/`@midi`/`@jack` or `micBuffer`/`live { }`, mark it desktop-only — those modules/constructs are stripped or parse-rejected on `FlowTarget=Web` and the playground surfaces a charitable ModuleLoader advisory or "function not found".

---

## 3. Prose / wording fixes

### Seeded items (confirmed against the engine — MUST appear)

1. **abs proc using `return` inside `lazy` (wiki/Functions.md, also wiki/Language-Basics.md):** `return` inside `lazy((...))` is a parse error on BOTH desktop and the web playground. **Fix:** use implicit return — the body becomes `(if (lt x 0) lazy ((sub 0 x)) lazy (x))` with no `return` statement.
2. **Comments "Note: must be at start of line" (wiki/Language-Basics.md; also FEATURES.md, wiki/Tips-and-Tricks.md):** STALE — inline/trailing `Note:` comments now work. **Fix:** state that `Note:`/`note` comments work both at line start AND as trailing inline comments (only `TODO:`/`FIXME:` remain line-start-only).
3. **Implicit returns "end with a void expression to return Void" (wiki/Functions.md / wiki/Language-Basics.md):** MISLEADING — verified: a body that collected `(add 1 2)` and `(add 3 4)` then ends with `(print)` returns `[3, 7]`, NOT `Void`. **Fix:** clarify that a trailing void expression returns `Void` ONLY when no prior non-void expression was collected; otherwise the collected value(s) are returned (1 → the value, 2+ → an array). To force `Void`, ensure nothing non-void is collected or use `(Nothing)`.

### wiki/Audio-and-Synthesis.md

- `applyEnvelope` documented as returning a new buffer (line 161); it mutates in place and returns `Void` (`EnvelopeProcessor.cs:62`). **Fix:** "`applyEnvelope` modifies the buffer in place and returns Void; copy first via `(copyBuffer mybuf)` if needed."
- The custom-oscillator examples present infix arithmetic (`/ * -`) as valid. **Fix:** all arithmetic is prefix-only; rewrite with `(sub (mul (div ...) ...) ...)`.
- `buf` used as the canonical Buffer variable name. **Fix:** `buf` is a reserved lexer token; use `mybuf`/`audioBuf`/`result`/`output`.
- "BPM and Timeline" shows `use "@audio"` for `setBPM`/`getBPM`/`beatsToFrames`/`framesToBeats`. **Fix:** these are in `@composition`.
- The Voice/Track section implies `@audio` provides `createVoice`/`createTrack`/etc. **Fix:** they require `use "@composition"`.
- Custom Instrument Lambda docs say `(noteToFrequency pitch)` is a valid call inside the lambda. **Fix:** the lambda receives a `MusicalNote` and `noteToFrequency` cannot accept it; document the limitation/workaround or fix the engine.

### wiki/Chords-and-Harmony.md

- `chordNotes` inline comment claims bare names `["C","E","G","B"]`. **Fix:** the engine returns octave-qualified `["C4","E4","G4","B4"]` (only `scaleNotes` returns bare names).
- The `G7` blockquote implies `G7` can be a dominant-7th chord "in a context where a chord is explicit, like inside a note stream." **Fix:** `G7` is always `NoteLiteral(G, octave 7)` in ALL contexts; use `Gdom7`.

### wiki/Collections.md

- The reference table lists `length / len`. **Fix:** only `len` is registered; remove `length`.
- The reference table and Zip section list `zip`. **Fix:** `zip` is never registered (BUG-3); remove it or mark unimplemented.
- The Array Indexing example omits `use "@std"` while the intro says most collection functions need it. **Fix (low):** add `use "@std"` for consistency or clarify that `@` indexing is core.

### wiki/Dynamics-and-Expression.md

- The `sfz` dynamic-marking row and the `sfz` articulation-table row together imply `| sfz C4q |` triggers both velocity 0.95 and the envelope spike. **Fix:** in note streams `sfz` only sets velocity (0.95) via `TryParseDynamicMarking`; the envelope spike requires the `Articulation.Sforzando` enum path, not exposed as an in-stream prefix keyword.

### wiki/Effects.md

- The Delay section assumes `EIGHTH` is in scope after `@std`/`@audio`. **Fix:** NoteValue constants are in `@notation`; add `use "@notation"`.
- The Effect Chaining section teaches `-> (pan -0.2)`. **Fix:** a negative bare literal cannot be the first arg of a parenthesized pipe call; use a variable or `(sub 0.0 N)`.

### wiki/Examples.md

- "Every snippet on this page parses and runs against the current Flow build." **Fix:** 12 examples fail; either fix them or remove the guarantee.
- The `jam` named-arg line `(jam ... seed=42)` is "usually clearer." **Fix:** it fails at parse time; show the valid 3-named-arg form and the positional seeded form.
- L-Systems uses `(createMusicalNote C4 4)`. **Fix:** `createMusicalNote` is `internal`; use `(quarter C4)` after `use "@notation"`.
- `buf` used as a variable in four examples. **Fix:** reserved token; rename to `audio`.
- `progression` used as a section name. **Fix:** reserved token; rename to `chordProg`.

### wiki/Flow-Operator.md

- The Tuple-Unpack section writes `<<Note, Note>> entry = ...` as a type annotation. **Fix:** typed tuple declarations require the `Tuple<<...>>` prefix; bare `<<...>>` on the LHS is a destructure pattern.

### wiki/Functions.md

- "To return Void implicitly, end with a void expression." **Fix:** see seeded item 3 — a trailing void does not clear collected values; ensure nothing non-void is collected, or use `(return (Nothing))`.
- "For zero-arg builtin/proc calls used as statements, the parens are optional" while showing `print "hello"`. **Fix (low):** optional parens also work for simple literal/identifier args, not just zero-arg calls.

### wiki/Generative.md

- The "Composing combinators" section shows `base -> (every 4 fn) -> ...`. **Fix:** Tidal combinators take Sequence LAST, so `->` (which inserts first) does not work; use explicit nesting.
- The `jam` signature lists `seed` as independently optional and uses `seed=` without `key=`. **Fix:** `seed` requires `key` (no seed-without-key overload).
- The Polyrhythm section shows `@std`/`@audio` as sufficient imports. **Fix:** `polyrhythm` requires `use "@composition"`.
- The L-system section uses `Dict<Symbol, Tuple>` with `<<#A #B>>` values. **Fix:** must be `Dict<Symbol, Symbol[]>` with `(list #A #B)` values.
- Cellular/Chaos sections use `Array[Bool]`/`Array[Double]`/`Array[Sequence]`. **Fix:** use postfix `T[]` syntax.

### wiki/Language-Basics.md

- Comment rules say `Note:` must start at the beginning of a line's content. **Fix:** see seeded item 2 — `Note:` works anywhere; only `TODO:`/`FIXME:` are line-start-only.
- "Array indexing uses @ (supports negative-from-end)" with `nums@-1`. **Fix:** `nums@-1` does not parse; bind via `(sub 0 N)` first.
- Tuple variable declaration uses `<<Type, Type>>`. **Fix:** typed tuple declarations require the `Tuple<<...>>` prefix.

### wiki/Loops.md

- The `while true` + `(if cond lazy (break) lazy ((Nothing)))` idiom is presented as canonical. **Fix:** `break`/`continue` are statements and cannot live in `lazy()`; encode the exit in the `while` guard.
- The for-loop early-exit example uses the same broken `lazy (break)` pattern. **Fix:** convert to a while loop with the termination condition in the guard.

### wiki/Musical-Context.md

- Scoping Rules: "Note streams require a `timesig` context to determine bar duration." **Fix:** the default 4/4 applies silently if none is set; note streams do not fail without `timesig`.

### wiki/Note-Streams.md

- The Voice Blocks section claims voice blocks "can carry their own articulation and dynamics." **Fix:** dynamics are not implemented inside voice blocks (parse error); only articulations work — set a sticky dynamic before the outer stream.

### wiki/Pattern-Transforms.md

- The Quantize example shows bare `e`/`s` as NoteValue arguments. **Fix:** use `EIGHTH`/`SIXTEENTH` from `use "@notation"`.
- The polyrhythm example shows `@std`/`@audio` imports. **Fix:** `polyrhythm` requires `@composition`.
- The polyrhythm example calls `(exportWav ...)`. **Fix:** `exportWav` does not exist; use `(writeWav "poly.wav" mixed)`.
- The Tidal section shows `base -> (every 4 ...)`. **Fix:** `every`/`chunk`/`phase` take Sequence last; the `->` pipe cannot be used directly.

### wiki/Playback-and-Export.md

- The WAV Export section documents `exportWav` (buffer-first) with code + a reference-table row. **Fix:** `exportWav` was removed in Phase 46 D-06; remove all references; `writeWav` (path-first) is the only WAV writer.
- Multiple examples use `buf` as a variable. **Fix:** reserved token; rename across stream/loop/preview/WAV/loadWav sections.
- The `play mel` timesig snippet omits `use "@audio"`. **Fix:** add `use "@audio"`.

### wiki/Quick-Start.md

- "A desktop GUI ... is included in the repo as `flow-editor`." **Fix:** no `flow-editor` project exists; remove the section or mark it planned/community.

### wiki/Song-Structure.md

- The `sectionSequences` example passes a bare section identifier. **Fix:** sections are not first-class values outside `Song [...]`; document the limitation or use `getSections`.
- The renderSong and Complete Example sections use `(exportWav ...)`. **Fix:** removed in Phase 46; use `(writeWav "path" buf)`.
- The renderSong example uses `Buffer buf = ...`. **Fix:** `buf` is reserved; rename.
- The Section Overloading example uses `| (root) |`. **Fix:** only `(ghost ...)`/`(grace ...)`/`(? ...)`/`(?? ...)` are valid parenthesized stream elements; use bare `| root |`.

### wiki/Standard-Library.md

- The Buffer-creation table lists `silence`. **Fix:** the real function is `createSilence` (no `silence` is registered).
- The WAV/MIDI I/O table lists `exportWav` (buffer-first). **Fix:** removed in Phase 46 D-06; only `writeWav` (path-first) remains.
- The `@notation` table lists `noteToFrequency`. **Fix:** `noteToFrequency` is provided by `@audio`, not `@notation`.
- The `@test` section says legacy `assertTrue`/`assertEqual`/`runTest`/`summary` remain available. **Fix:** removed in Phase 46 D-07; canonical API is `(test name lazy(...))` + `assert`/`assertEq`/`assertNotesMatch`/`assertBytesEqual`/`assertWithinDb`.

### wiki/String-Interpolation.md

- "The expression is automatically stringified via `str`." **Fix:** inaccurate for `Note` — `$"{C4}"` produces `"C4"` (with quotes) but `(str C4)` produces `C4`; qualify the claim.

### wiki/Tips-and-Tricks.md

- Comments block "must start at column 0." **Fix:** `Note:` works inline/trailing anywhere; remove the restriction.
- Pitfall 8: "Division by zero returns Void rather than crashing." **Fix:** `(div 10 0)` throws and exits 1; correct the prose.
- Charitable Interpretation: "`(stretch buf 0.0)` returns input + advisory." **Fix:** it throws `stretch factor must be positive; got 0`; remove or correct (also `buf` is reserved).
- Pitfall 4: "May not work correctly without timesig" for note streams. **Fix (low):** note streams default to 4/4; the real requirement is a key context for roman numerals.
- Pitfall 1: "ERROR: print is not defined (without @std)." **Fix (low):** `print`/`str`/arithmetic are global; only collection functions need `@std`.
- Named Arguments: `(jam ... key="Cmajor")` shows `key=` as a named arg. **Fix:** `key` is reserved and cannot be a named-arg label; use a `key Cmajor { }` block.

### wiki/Visualization.md

- The prettyBuffer sample output shows `channels: 1 (mono)`. **Fix:** with `use "@audio"` the Flow `createSineTone` proc shadows the C# builtin and produces stereo (`channels: 2 (stereo)`); update the sample output or drop `@audio`.

### wiki/Vocalization.md

- "Other consonants are not yet supported — passing e.g. `"la"` will be treated as an unknown phoneme." **Fix:** the engine throws a hard runtime error (`Unknown vowel phoneme: 'la'. Valid: ah, ee, eh, oh, oo`); it is not a charitable no-op.

### wiki/Voices-and-Tracks.md

- "Most voice/track functions live in `@audio` (some helpers in `@composition`)." **Fix:** reversed — `createVoice`/`createTrack`/`setBPM`/`getBPM`/`beatsToFrames`/`framesToBeats`/`setVoice*`/`addVoice`/`setTrack*`/`renderTrack`/`polyrhythm` are all in `@composition`; only `renderSequenceToVoices`/`renderBar*`/`setMaxVoices` are in `@audio`.
- Polyrhythm: `Sequence poly = (polyrhythm ...)`. **Fix:** `polyrhythm` returns `Buffer`, not `Sequence`.
- Polyrhythm: `(polyrhythm three four 8.0)`. **Fix:** the 3-arg overload takes `Int`, not `Double`; use `8`.

### README.md

- The clamping illustration uses `reverb(input, 5.0, 5.0, 5.0)` (C-style). **Fix:** the clamping prose is correct but the syntax is not Flow; use `(reverb input 5.0 5.0 0.3)`.

### FEATURES.md

- The table row on line 40 groups `Note:`/`TODO:`/`FIXME:` as "line-start comments." **Fix:** `Note:` works inline or at line start; only `TODO:`/`FIXME:` are line-start-only.

---

## 4. Fragments

Illustrative snippets that error standalone (undefined variables from prior blocks, or external files). Not "broken" in the language sense, but a reader copy-pasting one block in isolation will hit an error. Each should get either a minimal self-contained preamble or a prose note that it continues a previous block / needs an external file.

- **wiki/Audio-and-Synthesis.md** — Buffer Properties (31-35), Sample Access (39-43), Loading WAV Files (62-67, needs `sample.wav`), Envelope AR (145-150), Envelope ADSR (154-159), Custom Oscillator Custom Table Size (126-128). Several also reference the reserved `buf`.
- **wiki/Chords-and-Harmony.md** — Scale Variation `vary` (block 14, references undefined `mel`).
- **wiki/Dynamics-and-Expression.md** — Gain vs. Volume (block 18, references undefined `rendered`).
- **wiki/Effects.md** — Filters, Gain/Volume, Fade In/Out, Tempo Ramp, Granular (needs `pad.wav`), Time-Stretch (needs `loop.wav`), Pitch-Shift, Negative Values (all reference undefined `tone`/`src`).
- **wiki/Examples.md** — Loading a WAV Sample (needs `kick.wav`), Scala `.scl` Tuning (needs `.scl` files).
- **wiki/Flow-Operator.md** — Block 1 (syntax diagram `value -> function`), Block 2 (syntax diagram `(function value)`). Intentional pseudo-code; consider non-runnable fences.
- **wiki/Imports-and-Modules.md** — Basic import syntax (7-10, placeholder `mylib.flow`), Local file import paths (87-90), Execution in caller's scope (121-125). All reference placeholder `mylib.flow`.
- **wiki/Language-Basics.md** — Numeric widening diagram (94-96, untagged type-hierarchy diagram, not runnable Flow).
- **wiki/Musical-Context.md** — Tuning block with `loadScala` (212-230, placeholder `.scl` filenames).
- **wiki/Song-Structure.md** — getSections (124-129, undefined `mySong`), str snippet (151-154, undefined `mySong`), writeMidi snippet (251-253, undefined `fullSong` + missing `use "@audio"`).
- **wiki/String-Interpolation.md** — Render Status (undefined `song`).
- **wiki/Tips-and-Tricks.md** — Effect chain idiom (line 131, undefined `raw`), Transform chain idiom (line 137, undefined `mel`), Piano sustain pedal (264-266, undefined `song`), PRNG style pack override (351-353, comment-only), Import name conflicts (453-457, placeholder files).
- **wiki/Visualization.md** — bufferHex slice (116-119, undefined `tone`).
- **README.md** — three bash blocks (render `pulse.flow`, install per-user, install system-wide). Shell commands, not Flow; mark non-runnable.
