# Feature Research — v1.3 Composer DX Tier B/C

**Domain:** Music DSL / live-coding language (Flow v1.3 — subsequent milestone)
**Researched:** 2026-04-26
**Confidence:** HIGH (lead capability + DEFER closures backed by primary docs); MEDIUM (Tier B/C DAW conventions backed by industry sources)

## Scope Note

This is a **subsequent-milestone** research pass. v1.0–v1.2 already shipped the foundational note-stream / chord / Sequence / SongRender / MIDI export / audio DSP stack. Existing surfaces touched by v1.3:

- `Lexing/SimpleLexer.cs` (883 LOC) — note literal + duration suffix tokens
- `Parsing/Parser.NoteStream.cs` (352 LOC) — `TryParseDurationSuffix` returns one of `w/h/q/e/s/t`
- `Runtime/NoteStreamCompiler.cs` (647 LOC) — `DurationSuffixMap` + `ToFraction()` + auto-fit (`FindClosestNoteValue`)
- `StandardLibrary/Harmony/HarmonyFunctions.cs` — `arpeggio(Chord, String)`, `enharmonic`, `chordNotes`, `chordRoot`
- `StandardLibrary/Audio/DSP/Delay.cs` (96 LOC) — `delay(buf, ms, fb, mix)` (ms-only API today)
- `StandardLibrary/Audio/FileIO.cs` (466 LOC) — `loadWav` already exists (no pitch-shift overload yet)
- `StandardLibrary/Transforms/TransformFunctions.cs:660-697` — `humanize(Sequence, Double)` is **uniform** today (`HumanizeRng.NextDouble() * 2.0 - 1.0`); DEFER-06 swaps in Gaussian
- `StandardLibrary/BuiltInFunctions.cs:1190-1280` — `euclidean` swing/humanize is uniform-distribution-byte-pinned (must remain so)
- `Runtime/MusicalContext.cs` — push/pop stack for tempo/timesig/key/swing (extension target for `tuplet { }` block + `enable` pragma)
- `Runtime/ExecutionContext.cs` — owns musical-context stack
- `std.flow:114` — already declares `internal proc enharmonic` (DEFER-04 closes the natural-letter edges left as TODO)

This shapes what is "table stakes" vs "differentiator" — Flow already has the surrounding pipeline. The v1.3 features are **incremental refinements** to a credible product, not v1.0 must-haves.

---

## Feature Landscape

### Table Stakes (Users Expect These for v1.3 to Be Credible)

Without these, v1.3 reads as "we shipped tuplets but couldn't be bothered with the obvious extensions."

| Feature | Why Expected | Complexity | Dependencies |
|---------|--------------|------------|--------------|
| **Tuplet brackets `(3:2 C4 D4 E4)` or `{3 C4 D4 E4}`** | Triplets are universal — can't do swing eighth-triplet feel, can't do "3 against 2", can't write any jazz/Latin/classical music without them. Lilypond `\tuplet 3/2 { c8 d e }`, ABC `(3abc`, music21 `Tuplet(3,2)` all converge on a ratio + group form. Shipping v1.3 "Tuplets" without triplet bracket syntax is incoherent. | **HIGH — lexer + parser + interpreter triple-touch.** New token paths (or reuse LParen+IntLiteral+Colon), new note-stream parser branch (recursive: tuplets contain elements), new `TupletElement` AST record, new `MusicalNoteData.TupletRatio` field (Q,P), `NoteStreamCompiler.ComputeBeats` must scale by Q/P inside tuplet, `SongRenderer` duration math (samples = seconds × sampleRate; seconds derives from beats × 60/BPM — if beats are pre-scaled, downstream is free), MIDI export must track tuplet for tick math. **Affects:** `Parser.NoteStream.cs`, `NoteStreamCompiler.cs`, `MusicalNoteData` record, `MidiExport.cs`. | None — purely additive; existing `w/h/q/e/s/t` keep working as the "outer" duration |
| **Arbitrary fractional note durations `C4/3`, `C4/5`, `C4/12`** | Users will type these the moment they see triplets work. Lilypond `c8*2/3`, music21 `Duration(1/3)`, Csound's fractional durations all support direct division. Without `/N`, users hit a cliff: "I can do 3:2 but not write a single 1/12 note in isolation." Functionally equivalent to `(N:M C4)` but syntactically pithier for one-off oddities. | **MEDIUM — lexer + parser duo-touch.** Lexer must recognize `/Int` after a note literal as a duration token (currently `/` is the divide operator only in expression context — note streams are a separate parse mode where this is unambiguous). Parser produces a `FractionalDuration { Denominator: int }` discriminator on `NoteElement`. NoteStreamCompiler converts to beats: `beats = (1.0 / N) × timeSig.Denominator`. **Affects:** `Parser.NoteStream.cs:TryParseDurationSuffix`, `NoteStreamCompiler.CompileNoteElement`, `MusicalNoteData.FractionalDenom` field. | Tuplet feature lands first — `C4/3` semantically = `(3:2 C4 [auto-fit])` of a single note; both must agree on resulting beat count |
| **`range(Int, Int) → Array[Int]`** (DEFER-01) | Every script that writes "transpose by 1, 2, 3, 4 semitones" wants `range(1,5)`. Already-deferred from v1.2; users have already filed it. Trivially addable. | **LOW — single function add.** One signature in `BuiltInFunctions.cs`, ten lines, exclusive-stop semantics following Python convention. | None |
| **`range(Int, Int, Int) → Array[Int]` (with step)** (DEFER-01) | Once `range(a,b)` exists, "I want every other note" or `range(0, 24, 3)` is the obvious follow-on. Python, JavaScript, Rust step-by all have this. | **LOW — second overload of same function.** Reject step=0 with charitable error. Negative step iterates downward (Python convention). | DEFER-01 base form |
| **DEFER-04 multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)** | The `enharmonic()` function exists; it's documented to handle these; it currently doesn't. Users in flat keys writing F major naturally want B♭ and won't blink, but anyone writing chromatic music in C# major or Cb major will hit "wait, why doesn't enharmonic(B4) round-trip in C-flat major?". Music-theory expectation: B↔Cb and E↔Fb are valid spellings (they appear in Cb major and Gb major scales); F↔E# and C↔B# only appear in deep-sharp keys (F# major, C# major). Round-trip should work in those contexts. | **LOW — bug fix in `Enharmonic`.** Currently `if (alteration == 0) return unchanged` (lines 44-47) is too aggressive. Refine: in a key context where the natural is the enharmonic of a scale tone (C# major contains B# as scale tone 7), respell to that. **Affects:** `HarmonyFunctions.cs:Enharmonic` ~30 LOC, no AST change. | None |
| **DEFER-05 slice negative-from-end** | `slice(seq, -1, 0)` for "last bar", `slice(arr, 0, -1)` for "all but last", `slice(arr, -3, 0)` for "last 3" are the **first thing every Python-trained user tries**. Existing `slice` already silent-clamps, so making negatives mean "from end" is a natural extension. Must follow Python convention: negative index = `length + index`; trailing 0 or omitted = "to end" (already the case in current behavior). | **LOW — input transform in slice helper.** Pre-process indices: `if (i < 0) i = length + i; if (i < 0) i = 0;`. **Affects:** `Collections.SliceArray`, `Collections.SliceSequence` in `BuiltInFunctions.cs`. ~8 LOC each. | Existing slice (v1.2 DX-05) |

### Differentiators (Worth Shipping in v1.3, Composer DX)

These set Flow apart from "code-as-music" peers (Sonic Pi, Tidal, Strudel, Alda) and from the textual-only (Lilypond, ABC). They are **not table stakes** — Flow without them is still credible — but each is a meaningful step up on composer DX.

| Feature | Value Proposition | Complexity | Dependencies |
|---------|-------------------|------------|--------------|
| **DEFER-02/03 pragma system + `enable` keyword for `H` alias** | The conversation that drove this was: German notation uses H for B, B for B♭. Hard-coding H as B globally breaks any user who happens to use H as a variable. The `enable germanNotation` (or `enable hAsB`) form scopes the alias to the current file/block, avoiding the trap. **Differentiator:** no other music DSL has a pragma system; Flow gains a **future-proof feature-flag mechanism** for syntax modes — preview features, deprecated-syntax escape hatches, regional notations. Direct analogues: Haskell `{-# LANGUAGE TemplateHaskell #-}`, Python `from __future__ import division`, Rust `#![feature(...)]`. | **MEDIUM — lexer + parser + ExecutionContext touch.** Add `enable` keyword (TokenType.EnableKeyword). Add `EnableStatement(name: string)` AST node. Add `Set<string> EnabledPragmas` to `ExecutionContext` (file-scoped — set on enter, cleared on file end). NoteStream parser checks `context.EnabledPragmas.Contains("hAsB")` before treating `H` as note literal. **Affects:** `TokenType.cs` (+1), `Parser.cs` (+statement), `ExecutionContext.cs` (+set field), `SimpleLexer.cs` (note-literal recognition checks active pragmas — note: lexer doesn't normally have execution context, so the cleanest implementation is **parser-level rewrite** of `H` identifiers in note-stream context when `hAsB` is active, not lexer-level). | None — orthogonal to tuplets |
| **DEFER-06 Gaussian humanize distribution** | Real human timing/velocity variation is ~normal-distributed, not uniform. Uniform humanize feels "flat" — large deviations are as likely as small ones. Gaussian (mean 0, σ=amount/3, clamped at ±amount) gives the **bell-shape**: mostly small jitter, occasional larger ones, matching how a player actually drifts. The musical difference is audible. **Cost:** the implementation is 8 lines (Box-Muller). | **LOW — isolated function change**, BUT **sensitive**: the v1.2 byte-identical-determinism contract for `euclidean(seed=...)` and `humanize` must be preserved, so this is a **new function** (`humanizeGaussian`) or a **third-arg overload** (`humanize(seq, amount, "gaussian")`), NOT an in-place change to the uniform path. Existing tests pinned to byte-identical output must continue to pass. | None — additive; uniform path stays |
| **Arpeggio parameters (rate, direction, pattern)** | Current `arpeggio(Chord, String)` accepts `"up" / "down" / "updown"` only. Hardware/plugin arpeggiators (Cthulhu, Riffer, Scaler 2, Logic Arpeggiator, Ableton Arpeggiator) all expose: rate (note value: 1/4, 1/8, 1/16, 1/16T), direction (up, down, up-down, down-up, random, as-played), pattern (linear, chord-tone-only, scale-tone), octave range (1-4 octaves). **Minimal worth-shipping set:** rate (note-value or beats), direction (add `random`, `down-up`), octave range. Pattern presets like "alberti", "chord-tone" are stretch. | **MEDIUM — overload explosion + new options bag.** Cleanest: introduce `arpeggio(Chord, String direction, String rate)` as a 3-arg overload, and `arpeggio(Chord, String direction, String rate, Int octaves)` as 4-arg, leaving the existing 2-arg in place. **Affects:** `HarmonyFunctions.cs` arpeggio body (~80 LOC currently, expand to ~150). MusicalContext aware (default rate inherits from current note-stream resolution if omitted). | Tuplet feature (rate `"1/8t"` triplet rate must compose with tuplet ratio math) |
| **Chord inversions / voicings (drop-2, drop-3, close, open, spread)** | Jazz/classical voicing is a workflow gap. Today, to get a Cmaj7 drop-2 voicing in Flow, the user has to manually write `[E4 G4 B4 C5]`. With `voicing(Cmaj7, "drop2")` they write the chord symbol once. **Reference:** drop-2 = take 4-note close-position chord, drop the 2nd-from-top down an octave; drop-3 = drop the 3rd-from-top; close = within an octave; open/spread = larger than octave. These are **textbook operations**, well-defined, every jazz pianist knows them. Strong DX win. | **MEDIUM — pure function on Chord.** New `voicing(Chord, String) → Chord` (or `→ Sequence` for an arpeggiated form). Mode strings: `"close"`, `"open"`, `"drop2"`, `"drop3"`, `"drop2-4"`, `"spread"`. Algorithm is well-defined: re-order notes, octave-shift specific positions. New `inversion(Chord, Int) → Chord` (0=root, 1=first, 2=second, 3=third) for inversions specifically — orthogonal to voicing. **Affects:** new `VoicingFunctions.cs` in `StandardLibrary/Harmony/`. | Existing `ChordData` |
| **Delay sync to note values: `delay(buf, "1/8")` / `delay(buf, "1/8d")` / `delay(buf, "1/4t")`** | Today: `delay(buf, 250.0, ...)` — user must compute 250ms = eighth-note-at-120-BPM by hand. Every DAW delay (Ableton Echo, Logic Stereo Delay, Valhalla) defaults to **note-sync mode** and exposes ms-mode as the alternative. With the active `tempo` block + `timesig` already in `MusicalContext`, Flow has everything to translate `"1/8"` → ms automatically. **Composes with tuplets:** `"1/8t"` (eighth-triplet) = (60000/BPM) × (1/2) × (2/3). | **LOW — string-parser + helper at the call site.** New `delay(Buffer, String, Float, Float)` overload that parses `"1/N"`, `"1/Nd"` (dotted), `"1/Nt"` (triplet), looks up active tempo from `MusicalContext`, computes ms, dispatches to existing `Delay.Apply`. **Affects:** `Audio/EffectsFunctions.cs` (+1 overload, ~25 LOC), no DSP change. | Tuplet ratio knowledge for `"t"` suffix |
| **Microtonal ratios — just intonation + `tuning(name) { }` block** | Today, only `+50c`-style cent offsets exist (per-note). For 5-limit just intonation (3/2, 5/4, 6/5), Pythagorean (3-limit), or quarter-comma meantone, the user wants to **declare a tuning once** and have all subsequent pitches resolved through it. **Differentiator:** Flow becomes one of a tiny set of textual languages with first-class microtonality (Csound, SuperCollider, Lilypond-with-extensions). **Path of least resistance:** support Scala `.scl` file format (industry standard since 1992) via `loadScala(path) → Tuning` + `tuning(t) { }` musical-context block. .scl format is line-oriented, simple to parse, supports both `5/4` ratios and `386.31` cents inline. Avoids reinventing a notation. | **MEDIUM-HIGH — new type + new block + freq computation rewrite.** New `Tuning` Special Type. New `loadScala(String) → Tuning` (parser is ~50 LOC: skip comments+description, read N, read N pitch lines, distinguish `.` for cents vs `/` for ratios). New `tuning t { ... }` musical-context-block in parser + interpreter. Frequency lookup at synthesis time consults active Tuning instead of standard 12-TET. **Affects:** `TypeSystem/SpecialTypes/TuningType.cs` (new), `Runtime/MusicalContext.cs` (+Tuning field), `StandardLibrary/Audio/Synthesizers/*` (frequency-from-MIDI helper consults tuning), `Parser.cs` (+block keyword), `BuiltInFunctions.cs` (+loadScala). **Risk:** synth code paths that hard-code `440 * 2^((midi-69)/12)` need a single shared helper. | Decide: ship full Scala loader (high cost, high payoff) vs. ship `justIntonation { }` / `pythagorean { }` named tunings as a v1.3 wedge with `.scl` deferred to v1.4 |
| **Scale linting (warn on out-of-key notes), opt-in via pragma** | When `key Cmajor { ... }` is active, an `F#4` is almost always either (a) intentional chromaticism or (b) a typo. A linting pass that emits a **warning** (not error — see Pitfall: charitable interpretation) when a sounded pitch is not in the active scale catches typos. **Critical design choice:** must be **opt-in** via pragma (`enable scaleLint`). Default-on lint would break every blues piece (b3, b7 are out-of-major-scale by default), every chromatic piece, every modal-mixture piece. Scope: warn only when `key` is set; warn only on accidentals (not on `(? ...)` random output, not on transformed output). | **LOW-MEDIUM — interpreter-side check.** Hook into `NoteStreamCompiler` after note resolution: if pragma is active and note is not in `ScaleDatabase.GetScaleNotes(activeKey)`, emit warning via existing `ErrorReporter` (warning level, not error). **Affects:** `NoteStreamCompiler.cs` (+10 LOC), `ScaleDatabase` (membership check helper). | DEFER-02/03 pragma system |
| **Legato / portamento articulations (note-stream marker, MIDI CC65/CC5 export)** | Existing articulations (stacc, ten, marc, accent) are stream markers that affect velocity/duration shaping. Adding `legato` and `port` (with optional time `port:200ms`) lets composers indicate connected/sliding articulation. **MIDI export:** legato → overlap consecutive notes by N% (configurable); portamento → emit CC65=127 before note-on, CC5=time, CC65=0 after. **Audio export:** for monophonic synths, render with overlap at sample level (cross-fade); polyphonic synths (most current Flow synths are voice-pool poly) treat legato as duration extension. **Differentiator:** crosses the audio/MIDI boundary cleanly. | **MEDIUM — articulation enum + duration overlap + MIDI CC emission.** Extend `Articulation` enum (+Legato, +Portamento). Parser recognizes `legato` / `port` / `port:Ndur` after note. Renderer overlaps notes when `Legato` flag set. MIDI exporter emits CC65/CC5 messages around portamento notes. **Affects:** `Articulation.cs`, `Parser.NoteStream.TryParseArticulation`, `SongRenderer.cs` (overlap logic), `MidiExport.cs` (+CC emission). | None |
| **Snap-to-grid `quantize(seq, "1/16", strength, swing)` transform** | Once humanize/euclidean produce off-grid output, the inverse — pull notes back toward a grid with strength 0..1 — closes the loop. **DAW reference:** Logic/Ableton/FL Studio all expose Strength 0–100% (0=no move, 100=full snap), Swing 50–75% (50=straight, 75=heavy swing) on a chosen Resolution. Algorithm: `newOnset = old + strength × (gridPoint - old)`. Trivially defined. | **LOW — pure transform on Sequence.** New `quantize(Sequence, String resolution, Double strength, Double swing) → Sequence`. Resolution string parses same as delay sync (`"1/8"`, `"1/16t"`). **Affects:** `Transforms/TransformFunctions.cs` (+ ~40 LOC). | Tuplet rate-string parser (shared helper with delay-sync) |
| **`loadWav(path, semitones)` simple resampling pitch-shift overload** | Existing `loadWav(path)` returns the buffer unchanged. Adding `loadWav(path, Int semitones)` for pitch-shift on load is the minimum viable sample-import-with-pitch story. **Critical:** ship the **simple** algorithm (linear-interpolation resampling, also called "varispeed" or "tape speed") — `pitchRatio = 2^(semitones/12)`, then resample at `originalRate × pitchRatio`. This **also changes duration** (length / pitchRatio), which is the **expected behavior for sample chopping/repitching workflows** (every drum machine since the SP-1200 works this way). Phase vocoder / WSOLA / PSOLA preserve duration but require FFT and are **explicitly out of scope** for v1.3 (1000+ LOC, weeks of tuning, no off-the-shelf pure-C# single-file impl found). **Document it as varispeed.** | **LOW — single function add.** Overload `loadWav(String, Int) → Buffer`: load, compute `ratio = pow(2, semitones/12.0)`, resample with linear interpolation (each output sample = lerp of two input samples at fractional position `i × ratio`), return new buffer. ~40 LOC. **Affects:** `FileIO.cs` (+overload), or `BufferHelpers.cs` if a generic resample helper is wanted. | None — varispeed is the simple algorithm |

### Anti-Features (Don't Ship in v1.3)

These look like obvious next-steps but each has a non-obvious cost in v1.3.

| Feature | Why Tempting | Why Problematic in v1.3 | Better Approach |
|---------|--------------|--------------------------|-----------------|
| **Time-preserving pitch shift (phase vocoder / WSOLA / PSOLA)** | "Pitch up without speeding up" is what every modern DAW does and what users will assume `loadWav(path, semitones)` means. | (1) No clean single-file pure-C# implementation found in 2026 research — would require porting 1000+ LOC from C++ implementations or vendoring SoundTouch (LGPL, P/Invoke complexity). (2) Phase vocoder has **infamous tuning issues** (transient smearing, formant shift, "phasey" artifacts on percussive material) — wrong setting, wrong sound. (3) Conflicts with Flow's **minimal-dependencies** principle from STACK.md. | Ship varispeed (simple resampling) in v1.3. If users ask for time-preserving, defer to v1.4 with explicit `loadWavTimePreserving(path, semitones)` — gives a clean signal it's a different (slower, lossier) operation, and lets the v1.4 milestone include the dependency-acceptance discussion. |
| **Default-on scale linting** | Catches typos! Helpful! | Breaks every chromatic, blues, jazz, modal-mixture, atonal piece — i.e. most of the music users will write. Generates noise that trains users to ignore warnings (warning fatigue). Conflicts with Flow's **charitable-interpretation** principle (CLAUDE.md MEMORY: "music > rigid correctness"). | Opt-in via `enable scaleLint`. Warning-only (not error). Document in tutorial as "uncomment the lint pragma when copy-editing your final score." |
| **`H` as global note literal in lexer** | The DEFER-02/03 conversation tempts a one-line fix: just make `H` parse as B in the lexer. | (1) Breaks every existing user variable named `H`. (2) Silent semantic change — `Int H = 5; print H` would either still print 5 (if lexer is context-aware) or now fail to parse (if not), neither obvious. (3) German notation isn't even universal in Germany — many German composers happily use B internationally. | Pragma-scoped rewrite in note-stream parser only (DEFER-02/03 design). User opts in with `enable germanNotation` per file. Outside note streams, `H` is an identifier as today. |
| **Negative `range(a, b)` defaulting to "down" automatically** | Python's `range(5, 0)` returns `[]`; users sometimes wish it defaulted to `[5,4,3,2,1]`. | Surprising vs. industry convention. Users coming from Python, JavaScript, Rust, Go, C# will all expect `range(5, 0)` to be empty. Magic auto-direction violates principle of least astonishment. | Empty array when `start ≥ stop` and step is positive (or omitted). User who wants countdown writes `range(5, 0, -1)` explicitly. |
| **VST/AU plugin hosting for "real" pitch-shift / synth swap** | Once users see varispeed is "tape-style only", they'll ask "can I just use SoundTouch / Ableton Live's algorithm?" | Out of scope per PROJECT.md (line 149): "VST/AU plugin hosting — too complex for interpreter; focus on built-in synthesis". Reaffirmed in v1.3 scope. | Document the limitation. Point to `ffmpeg` / external CLI pipeline as the escape hatch (mirrors v1.1's `tts(text)` external-process precedent). |
| **`enable` for breaking-change syntax migration** | Once you have a pragma system, the temptation is to use it for every "new vs old syntax" choice — `enable newDurationSyntax`, `enable strictTypes`, etc. | Feature-flag explosion. Each new pragma is a permanent maintenance burden (every test must consider both modes; every parser change is conditional). Haskell's GHC has 100+ language extensions — that's a warning, not an aspiration. | Reserve `enable` for **regional/preference** flags (`hAsB`, `germanNotation`, `scaleLint`) — not for syntax-mode toggles. New language features land unconditionally; old syntax stays supported indefinitely (Flow has explicit-back-compat constraint, CLAUDE.md). |
| **Fully general N:M:K ABC tuplet form `(p:q:r)`** | ABC notation supports `(p:q:r` where r is "the next r notes get this tuplet" — a counter form. Looks general. | (1) The `r` form is rarely used even in ABC (it's there for awkward edge cases). (2) Bracket form `(N:M element element element)` is unambiguous: tuplet ends at closing paren, no count needed. (3) Counter-form makes nesting harder to read and parse. | Ship bracket form `(N:M elem elem)` only. The `(N elem elem)` shorthand for triplet/quintuplet defaults `M` to "the obvious power-of-2" (3:2, 5:4, 6:4, 7:4, 9:8). The `r`-counter form is **never** worth shipping. |
| **Scale linting that warns on transformed-output notes** | "Lint everything!" | (1) `transpose(seq, 5)` will routinely move notes out of key — that's the whole point of transposition. (2) `(? ...)` random output is unconstrainable. (3) Linting transformed sequences would emit warnings the user can't fix without disabling the lint. | Lint **only the source `\| ... \|` literal** — what the user actually typed. Transformed/generated notes are exempt. |

---

## Feature Dependencies

```
Tuplet brackets (N:M ...)
    └──enables──> Arbitrary fractional duration C4/N
                       (C4/3 == (3:2 C4 [auto]))
    └──enables──> Delay sync 1/8t / 1/16t
    └──enables──> Quantize resolution 1/16t
    └──enables──> Arpeggio rate 1/8t

DEFER-02/03 pragma system (enable keyword)
    └──enables──> Scale linting (opt-in)
    └──enables──> H-as-B alias
    └──enables──> [future preview features]

Existing slice (v1.2)
    └──extended-by──> DEFER-05 negative indexing

Existing humanize (v1.2 — uniform)
    └──parallel-to──> DEFER-06 humanizeGaussian (new function, not replacement)
    └──MUST-NOT-BREAK──> v1.2 byte-identical determinism contract for euclidean+humanize seeds

Existing arpeggio(Chord, String) [v1.0]
    └──extended-by──> arpeggio(Chord, String, String rate, Int octaves)

Existing delay(buf, ms, fb, mix) [v1.0]
    └──parallel-to──> delay(buf, "1/8", fb, mix)  (new overload)

Existing loadWav(path) [v1.0]
    └──parallel-to──> loadWav(path, Int semitones)  (new overload — varispeed)

Existing enharmonic [v1.2]
    └──refined-by──> DEFER-04 multi-letter edges (in-place fix)

Tuning + Scala loader [if shipped]
    └──requires──> Centralized "midi-to-frequency" helper across all synthesizers
                       (today each synth hard-codes 440 * 2^((m-69)/12))
```

### Dependency Notes

- **Tuplets land first** — every other rhythm-aware feature (delay sync, quantize, arpeggio rate) wants to refer to "1/8t" and that string-parser is shared.
- **Pragma system lands before scale lint and `H`-as-B** — both consume `enable`.
- **DEFER-06 must not touch v1.2 byte-identical-determinism contract** — implement as new function or new overload, not in-place change to `Humanize` lambda.
- **Microtonal tuning is the heaviest single feature** — it touches every synthesizer. If shipped, it should be its own phase. If deferred, every other v1.3 feature lands cheaply.
- **Per-note overlap math (legato/portamento)** is independent of all other features but couples to `SongRenderer` mixing — schedule mid-milestone after stability is confirmed.

---

## MVP Definition

### Launch With (v1.3 must-ship)

The minimum that makes "v1.3 Composer DX Tier B/C — Tuplets + DEFER closures + DX bundle" credible:

- [x] **Tuplet brackets `(N:M element element)`** — lead capability, must work for triplets/quintuplets/septuplets, must nest, must compose with existing duration suffixes outside the bracket. AC: `\| (3:2 C4 D4 E4) F4q \|` plays as triplet eighth + quarter, total = 1 quarter + 1 quarter = 2 beats.
- [x] **Arbitrary fractional duration `C4/N`** — direct division form. AC: `C4/3` plays as triplet eighth (same MIDI ticks as one element of a `(3:2 C4 _ _)`).
- [x] **`range(Int, Int)` and `range(Int, Int, Int)`** (DEFER-01) — Python-style exclusive-stop, optional step.
- [x] **DEFER-04** multi-letter enharmonic edges — `enharmonic(B4)` in C# major returns `B#3`.
- [x] **DEFER-05** slice negative-from-end — `slice(arr, -3, 0)` returns last 3 elements.
- [x] **`enable` pragma system** — at minimum, parses and stores active pragmas in ExecutionContext per-file. Implements **one** consumer (`enable hAsB` for DEFER-02/03 H-alias). Other consumers (scaleLint, etc.) light up if their feature ships in v1.3.

### Add When Possible (v1.3 stretch — Tier B/C bundle)

Each is independently shippable. Each closes a clear DX gap. Pick based on phase budget:

- [ ] **DEFER-06 Gaussian humanize** — `humanizeGaussian(seq, amount)` or `humanize(seq, amount, "gaussian")`. Low cost, audible difference, closes a v1.2 deferred item.
- [ ] **Arpeggio params** — `arpeggio(Chord, direction, rate, octaves)`. Medium cost, big composer-flow win.
- [ ] **Chord voicings** — `voicing(Chord, mode)` + `inversion(Chord, n)`. Pure functions, low risk, big jazz/classical workflow win.
- [ ] **Delay sync to note values** — `delay(buf, "1/8", fb, mix)`. Trivially additive, high daily-use frequency.
- [ ] **Snap-to-grid quantize** — `quantize(seq, "1/16", strength, swing)`. Pure transform, low risk.
- [ ] **Legato / portamento articulations** — adds expressivity at audio + MIDI level.
- [ ] **`loadWav(path, semitones)` varispeed** — closes the "I have a sample, want it a fifth up" workflow. Document as tape-style.
- [ ] **Scale linting** — opt-in, warning-level. Cheap once pragma system is in.

### Defer to v1.4+ (out of scope for v1.3)

- [ ] **Microtonal Scala-file tuning system** — high cost (synthesizer rewrite), high payoff, deserves its own milestone or its own phase with a researcher pass on `.scl` parsing edge cases. **Wedge alternative:** ship `enable justIntonation` / `enable pythagorean` named-tuning pragmas in v1.3 if the team wants the differentiator without the full Scala loader.
- [ ] **Time-preserving pitch shift (phase vocoder)** — explicitly anti-feature in v1.3 per dependency-minimalism. Revisit when a credible single-file pure-C# implementation surfaces or the user pain is high enough to accept SoundTouch / external CLI dependency.
- [ ] **Groove templates** (DAW-style external rhythmic feels) — niche, large UX surface.
- [ ] **Per-voice effects chains** — already on PROJECT.md deferred list.
- [ ] **Pattern presets in arpeggio** (alberti, scale-tone, walking-bass) — composable from existing transforms, doesn't need a single function.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Notes |
|---------|------------|---------------------|----------|-------|
| Tuplet brackets `(N:M ...)` | HIGH | HIGH | **P1** | Lead capability — milestone is named after this |
| Fractional `C4/N` durations | HIGH | MEDIUM | **P1** | Lands free once tuplet math is in |
| `range(Int, Int)` (DEFER-01) | MEDIUM | LOW | **P1** | Tiny, deferred, expected |
| `range(Int, Int, Int)` step (DEFER-01) | MEDIUM | LOW | **P1** | Same patch as above |
| DEFER-04 enharmonic edges | LOW | LOW | **P1** | Closes deferred item; bug-fix-class |
| DEFER-05 slice negative indexing | HIGH | LOW | **P1** | Python-trained-user expectation |
| `enable` pragma system + H-alias | MEDIUM | MEDIUM | **P1** | Foundation for DEFER-02/03 and scale lint |
| DEFER-06 Gaussian humanize | MEDIUM | LOW | **P2** | Audible improvement; closes deferred |
| Chord voicings (drop-2/3/close/open) | HIGH | MEDIUM | **P2** | Big jazz workflow win, low risk |
| Delay sync to note values | HIGH | LOW | **P2** | Daily-use feature, trivial code |
| Arpeggio params (rate/dir/octaves) | MEDIUM | MEDIUM | **P2** | Existing function expands |
| Snap-to-grid quantize | MEDIUM | LOW | **P2** | Closes humanize → re-tighten loop |
| Legato/portamento articulations | MEDIUM | MEDIUM | **P2** | Audio + MIDI surface, well-scoped |
| `loadWav(path, semitones)` varispeed | MEDIUM | LOW | **P2** | Sample-chop workflow; document as tape-style |
| Scale linting (opt-in) | LOW | LOW | **P3** | Nice-to-have once pragma system exists |
| Microtonal `tuning { }` block + Scala loader | HIGH | HIGH | **P3** | Differentiator; consider deferring to v1.4 unless it's a milestone goal |

**Priority key:**
- **P1** — Must ship in v1.3. Without these, the milestone scope is unmet.
- **P2** — Should ship if phase budget allows. Each is independently valuable.
- **P3** — Stretch. Defer to v1.4 unless a phase falls short and budget reallocates.

---

## Conventions Reference (How Other DSLs / DAWs Do It)

### Tuplet syntax across notation systems

| System | Triplet syntax | Quintuplet syntax | Nesting | Notes |
|--------|---------------|-------------------|---------|-------|
| **Lilypond** (modern) | `\tuplet 3/2 { c8 d e }` | `\tuplet 5/4 { c16 d e f g }` | Yes (well-documented) | "3/2" = "3 in time of 2 of next-larger value" |
| **Lilypond** (legacy) | `\times 2/3 { c8 d e }` | `\times 4/5 { c16 d e f g }` | Yes | Inverse of modern: "scale duration by 2/3" |
| **ABC notation** | `(3abc` | `(5abcde` (defaults to 5:n:5 by time-sig) | Implicit only | Full form `(p:q:r` available; rarely used |
| **music21** (Python) | `Tuplet(3, 2)` | `Tuplet(5, 4)` | Yes via Duration objects | `numberNotesActual` / `numberNotesNormal` pair |
| **MusicXML** | `<time-modification>3/2</time-modification>` | `<time-modification>5/4</time-modification>` | Yes (tracked per note) | Verbose; designed for round-trip not authoring |
| **SuperCollider** | `Pseq([...]).stutter(...)` patterns; explicit duration arrays | Same | N/A — no rhythm grammar | SC composes durations as data; no special syntax |
| **Flow v1.3 proposal** | `(3:2 C4 D4 E4)` or `(3 C4 D4 E4)` (M defaults to 2) | `(5:4 C4 D4 E4 F4 G4)` or `(5 ...)` | Yes (recursive parse) | Bracket form, no `r`-counter — see Anti-Features |

### Pragma / language-extension systems across languages

| Language | Syntax | Scope | Use cases |
|----------|--------|-------|-----------|
| **Haskell (GHC)** | `{-# LANGUAGE TemplateHaskell #-}` at file top | File | 100+ extensions, preview features, opt-in syntax |
| **Python** | `from __future__ import division` at file top | File | Migration to forward-compat semantics |
| **Rust** | `#![feature(let_chains)]` at crate top (nightly only) | Crate | Unstable feature gates |
| **Scala** | `import scala.language.implicitConversions` or `-language:implicitConversions` | File or compiler-flag | Capability-style opt-in |
| **OCaml** | `[@@@warning ...]` attribute at file top | File or block | Warning-level toggles |
| **Flow v1.3 proposal** | `enable hAsB` at file top (or any statement position) | File (after the statement, until end) | Regional notation, opt-in lints |

### DAW humanize conventions

| DAW | Velocity humanize | Timing humanize | Distribution |
|-----|-------------------|-----------------|--------------|
| **Logic Pro** | "Humanize" parameter, ± value | Position randomization | Documentation does not specify; likely uniform |
| **Ableton Live** | Velocity randomize ± | Groove engine (sample-based, not random) | Per-note jitter typically uniform within ± range |
| **FL Studio** | Velocity randomize | Note position randomize | Uniform within ± range |
| **Native plugins (Humanizer Pro et al.)** | Programmable, often Gaussian | Programmable | Higher-end tools expose Gaussian as "natural" preset |

**Takeaway for DEFER-06:** built-in DAW humanizers are typically uniform (matching v1.2 Flow). Premium tools and academic-grade humanizers prefer Gaussian. Flow shipping **both** (uniform = current; gaussian = new) gives users the choice — the Gaussian path is the differentiator over default-DAW behavior.

### Quantize parameter conventions

All major DAWs converge on:
- **Resolution** — note value (1/4, 1/8, 1/16, 1/16T, 1/32, etc.)
- **Strength** — 0–100% (0 = no move, 100 = full snap to grid)
- **Swing** — 50–75% (50 = straight, >50 = delay every 2nd grid point)

Flow v1.3 quantize signature aligns: `quantize(seq, resolution: String, strength: Double, swing: Double)`.

### Microtonal tuning file format (Scala .scl)

- Plain ASCII, line-oriented
- Lines starting with `!` are comments
- First non-comment, non-blank line = description string
- Second = note count `N`
- Next `N` lines = pitch values, one per line:
  - Contains `.` → cents value (e.g. `386.31`)
  - Contains `/` → ratio (e.g. `5/4`)
  - Plain integer → ratio with denominator 1 (`2` → `2/1`)
- The implicit 1/1 root is **not** in the file
- Octave/repeat is the last entry (typically `2/1` for octave-based scales)

This format is **the** industry standard for microtonal tunings since 1992; supported by Csound, SuperCollider (via TuningLib), Surge XT, Pianoteq, every modern microtonal-aware tool. Flow adopting it as the v1.3 microtonal entry point inherits a 30-year ecosystem of free downloadable scales.

---

## Sources

### Tuplet syntax
- [Tuplets — LilyPond Notation Reference](https://lilypond.org/doc/v2.25/Documentation/notation/tuplets) — `\tuplet 3/2 { c8 d e }` form, nesting via `\tweak`
- [LilyPond Learning Manual: Advanced rhythmic commands](https://lilypond.org/doc/v2.23/Documentation/learning/advanced-rhythmic-commands)
- [ABC standard v2.1](https://abcnotation.com/wiki/abc:standard:v2.1) — `(p:q:r` general form
- [music21 Duration documentation, Chapter 19: Advanced Durations](https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html) — `Tuplet(numberNotesActual, numberNotesNormal)` ratio model, `tupletMultiplier()`
- [Tuplet — Wikipedia](https://en.wikipedia.org/wiki/Tuplet) — universal music-theory definition

### Microtonal tunings
- [Scala scale file (.scl) format — Huygens-Fokker](https://www.huygens-fokker.org/scala/scl_format.html) — canonical spec since 1992
- [Scala for dummies — Huygens-Fokker](https://www.huygens-fokker.org/scala/dummies.txt)
- [TuningLib/Scala.sc — SuperCollider Quarks](https://github.com/supercollider-quarks/TuningLib/blob/master/Scala.sc) — reference implementation in another music DSL

### DAW conventions (humanize, quantize)
- [Quantize parameters in Logic Pro for iPad](https://support.apple.com/guide/logicpro-ipad/quantize-parameters-lpip70c8d20d/ipados) — Q-Strength + Q-Swing percentages
- [Master Quantizing MIDI in Your DAW — DepartureMusic](https://www.departuremusic.com/master-quantizing-midi-daw-tips/) — strength/resolution/swing semantics
- [Guide: Humanizing MIDI Drums in Ableton — Production Music Live](https://www.productionmusiclive.com/blogs/news/humanizing-midi-drums)
- [How to Make Your MIDI Drums Sound Human — zZounds](https://blog.zzounds.com/2020/05/27/how-to-make-your-midi-drums-sound-human/)

### Pitch shifting algorithms
- [Phase vocoder — Wikipedia](https://en.wikipedia.org/wiki/Phase_vocoder) — algorithm overview, complexity profile
- [Time and pitch scaling in audio processing — surina.net](https://www.surina.net/article/time-and-pitch-scaling.html)
- [Vari-Speed pitch formula — HomeRecording forum](https://homerecording.com/bbs/threads/vari-speed-calculating-speed-percentage-relative-to-semi-tone.367674/) — `2^(semitones/12)` ratio derivation
- [Use Varispeed — Logic Pro Help](https://logicpro.skydocu.com/en/edit-the-timing-and-pitch-of-audio/use-varispeed-to-alter-the-speed-and-pitch-of-audio/) — DAW reference for tape-style behavior

### Chord voicings
- [Drop 2 Chords — Theory & Exercises (jazzguitar.be)](https://www.jazzguitar.be/blog/drop-2-chords/) — drop-2 algorithm
- [Drop 2 Piano Voicings: The Complete Guide — Piano With Jonny](https://pianowithjonny.com/piano-lessons/drop-2-piano-voicings-the-complete-guide/) — close vs. drop-2 vs. open
- [What To Know About Drop 2 / Drop 3 Voicings — Jazz Guitar Today](https://jazzguitartoday.com/2023/05/what-to-know-about-the-elusive-double-drop-2-drop-3-voicings/) — drop-3 + double-drop semantics

### MIDI portamento
- [How To Use Portamento (Glide) MIDI Commands — Sweetwater](https://www.sweetwater.com/insync/how-to-use-portamento-glide-midi-commands/) — CC65 / CC5 protocol
- [MIDI CC List — Nick Fever](https://nickfever.com/music/midi-cc-list)

### Pragma / language-extension systems
- [GHC Pragmas reference](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/pragmas.html) — `{-# LANGUAGE ... #-}` design
- [Rust feature gates — RFC pattern](https://rust-lang.github.io/rfcs/1192-inclusive-ranges.html) — `#![feature(...)]` semantics

### Range function semantics
- [Python range() — w3schools](https://www.w3schools.com/python/python_range.asp) — exclusive-stop, optional step
- [Rust ranges — for and range](https://doc.rust-lang.org/rust-by-example/flow_control/for.html) — `a..b` vs `a..=b` distinction

### Existing Flow code (verified)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/NoteStreamCompiler.cs` — `DurationSuffixMap`, `ToFraction`, auto-fit `FindClosestNoteValue`
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Parsing/Parser.NoteStream.cs:258-270` — `TryParseDurationSuffix` accepts `w/h/q/e/s/t` only
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:309-347` — current 2-arg `arpeggio` implementation
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/DSP/Delay.cs` — current ms-only delay API
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/FileIO.cs:290-310` — existing `loadWav` (no semitones overload)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:660-697` — current uniform-distribution `humanize`
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:37-67` — current `enharmonic` with the `alteration == 0` early-return that DEFER-04 must refine

---
*Feature research for: Flow v1.3 Composer DX Tier B/C — Tuplets + DEFER closures + DX bundle*
*Researched: 2026-04-26*
