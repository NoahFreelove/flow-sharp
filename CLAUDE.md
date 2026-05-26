# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## What This Is

Flow is an interpreted, statically-typed language for music production. Flow operator (`->`) for chaining, music types (Note/Chord/Song/etc.), inline note streams (`| C4 D4 E4 |`), musical-context blocks (tempo/key/timesig), and a full pipeline from composition → WAV export → real-time playback. Written in C# targeting .NET 10.

## Goals & Non-Goals

**Goals**
- **Ergonomics first** — composer ergonomics override runtime efficiency, type strictness, and generality.
- **Genre-agnostic** — classical, EDM, jazz, pop, metal, all in one language. MIDI + instrument generation today; vocaloid voices planned.
- **Make easy cases fast** — common well-typed paths should be reasonably fast.

**Non-Goals**
- General-purpose computation (Flow *can* compute, but don't bend it to non-musical use cases).
- Maximum runtime efficiency (it's interpreted).
- Type strictness for its own sake (Float × Double is fine; flexibility is the point).
- A language for one kind of music.

> **Pre-traction (v1.4+ public, no deprecation discipline yet).** Per D-v1.5-01 (locked 2026-05-17), breaking syntax/builtin changes still ship in single commits, in-repo migrators only, no `flow migrate` subcommand. Flips when a non-author composer files a `.flow` issue/PR, a third-party fork appears, the user observes traction and explicitly opts in, or Flow ships to a package registry. See external memory `project_pre_public_no_legacy_burden.md`.

## Build & Run Commands

```bash
# Build the solution
dotnet build

# Run a .flow script
dotnet run --project flow-interpreter path/to/script.flow

# REPL
dotnet run --project flow-interpreter

# REPL in watch mode
dotnet run --project flow-interpreter -- --watch path/to/script.flow

# Eval a string (the `--` is REQUIRED — dotnet run consumes flags before it)
dotnet run --project flow-interpreter -- -e 'Int x = 5; (print (str x))'

# Run one test
dotnet run --project flow-interpreter tests/test_comprehensive.flow

# Run all tests
for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done
```

No unit-test framework — tests are `.flow` scripts in `tests/` verified by console output (70+ files: features, pipes, audio, musical context, note streams, chords, song structure, instruments, effects, transforms, generative, lambdas, imports).

## Compile-Target Flavors

Phase 47 (shipped 2026-05-25) introduced `FlowTarget=Desktop|Web` MSBuild conditioning so `flow-lang.dll` compiles cleanly under WASM. Desktop is the default; Web is opt-in.

```bash
# Default — Desktop (all P/Invoke / SFZ / OSC / live coding / mic input intact)
dotnet build flow-lang/flow-lang.csproj

# Explicit Desktop
dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop

# Web — strips features that cannot run in a browser sandbox
dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web
```

**Stripped on Web** (per D-47-01..14): `Audio/PulseAudio*Backend.cs` + `CoreAudioBackend.cs` (P/Invoke); `StandardLibrary/Audio/Sfz/**/*.cs` + `sfz.flow` + `@sfz` module; `StandardLibrary/Network/OscFunctions.cs` + `OscHandleData.cs` + `osc.flow` + Rug.Osc PackageReference; `StandardLibrary/Audio/InputFunctions.cs` (`micBuffer`); `flow-lang/Samples/**` (U-Iowa MIS bundle — Phase 29 sampled instruments fall back to synthesis); future Phase 40 RtMidi.Core.

**Stays on Web** (~85% of language surface): full core (lexer/parser/AST/evaluator/type system), pattern matching (Phase 35), all music types, all synthesis + DSP, Phase 36 stdlibs (`@patterns`/`@generative`/`@improv`), Phase 39 notation IO export (`@notation-io` — hand-rolled XmlWriter), MIDI file write via DryWetMidi 8.0.3.

**Composer-facing UX on Web target:**

- `use "@sfz"` / `use "@osc"` under FlowTarget=Web → ModuleLoader emits `[target] module '@X' unavailable on Web target — line N. Build with FlowTarget=Desktop to enable.` + ModuleLoadResult.Error.
- `live <quantize> { ... }` → Parser throws Rust-style ParseException pointing at the line; live coding requires FileSystemWatcher.
- `(micBuffer N)` → function not found (InputFunctions stripped).
- Audio playback → `WebAudioBackend` stub throws PlatformNotSupportedException with `"WebAudioBackend stub — Phase 48 will implement via [JSImport]"` (Phase 48 fills the JSImport bodies).

**Guard locations** (future-contributor reference):

- `flow-lang/Core/FlowEngine.cs:185,202` — `#if !FLOW_WEB` wraps SfzBuiltins.Register + OscFunctions.Register
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1027` — `#if !FLOW_WEB` wraps InputFunctions.RegisterContextDependent
- `flow-lang/Audio/AudioPlaybackManager.cs` — `WebAudioBackend.IsAvailable()` probe FIRST in `DetectBackend`, existing branches wrapped in `#if !FLOW_WEB`
- `flow-lang/Runtime/ModuleLoader.cs` — `IsStrippedOnWeb` gate at top of `LoadModule`
- `flow-lang/Parsing/Parser.cs:220` — `TokenType.Live` gate inside the dispatch branch
- `flow-lang/Core/FlowEngine.cs` — `FlowEngine.IsWebTarget` + `FlowEngine.SupportsLiveBlocks` static properties (compile-time constants via `#if FLOW_WEB` initializer)
- `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` — Mono.Cecil reflective invariant gate

When adding new audio/network/IO features that may not work in the browser: (1) add a `#if !FLOW_WEB` guard at the actual call site (NOT a wrapper method); (2) if composer-invoked at parse-time or import-time, add a charitable advisory at Parser or ModuleLoader; (3) tag exercising tests with `[FlowTargetFact("Desktop")]`; (4) if a new package is pulled, add it to `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` so Web build drift is caught.

## Project Structure

Two projects: **flow-lang** (library, namespace `FlowLang`) and **flow-interpreter** (REPL/CLI, namespace `FlowInterpreter`).

```
flow-lang/
  Core/                 # FlowEngine orchestrator
  Lexing/               # SimpleLexer (manual, music-literal aware)
  Parsing/              # Parser, TypeParser (recursive descent)
  Ast/{Expressions,Statements}/   # immutable record nodes
  Interpreter/          # ExpressionEvaluator, Interpreter
  Runtime/              # ExecutionContext, StackFrame, Value, ModuleLoader,
                        #   MusicalContext, NoteStreamCompiler, Thunk, PrngRegistry
  TypeSystem/{PrimitiveTypes,SpecialTypes}/ + OverloadResolver.cs + ArrayType.cs
  StandardLibrary/
    BuiltInFunctions.cs           # main registration site
    InternalFunctionRegistry.cs   # signature → lambda
    Audio/{DSP,Synthesizers,Sfz}/ # effects + instruments + SFZ surface
    Harmony/                      # ChordParser, scales, roman numerals
    Transforms/                   # transpose/invert/retrograde/etc.
    {Patterns,Generative,Improv}/ # Phase 36 stdlibs
    Notation/                     # Phase 39 MusicXML/LilyPond/ABC/MML
  Audio/                # Playback infrastructure
    AudioPlaybackManager.cs       # backend lifecycle + auto-detect
    IAudioBackend.cs              # abstraction
    PulseAudioSimpleBackend.cs    # Linux (libpulse-simple)
    CoreAudioBackend.cs           # macOS (AudioToolbox/AudioQueue P/Invoke)
  Samples/              # CC-BY 4.0 University-of-Iowa MIS bundle (≤5 MB)
  improv/styles/        # composer-editable Flow rule packs
  *.flow                # stdlib modules (std, audio, patterns, sfz, ...)
```

## Execution Pipeline

`Source → SimpleLexer → Token[] → Parser → AST (records) → Interpreter → Value`

**FlowEngine** (`Core/FlowEngine.cs`) wires `InternalFunctionRegistry` + `ExecutionContext` + `Interpreter` and owns `AudioPlaybackManager`.

| Stage | File | Role |
|-------|------|------|
| Lexing | `Lexing/SimpleLexer.cs` | Manual tokenizer with music-literal detection (notes, chords, durations) |
| Parsing | `Parsing/Parser.cs` | Recursive descent; `->` → nested calls at parse time; note streams, chords, songs, musical context, lambdas, sections |
| Type parsing | `Parsing/TypeParser.cs` | Annotations: arrays, music types, function types |
| Evaluation | `Interpreter/ExpressionEvaluator.cs` | Switch dispatch over AST → `Value` |
| Execution | `Interpreter/Interpreter.cs` | Statements, function calls, implicit returns, musical context, sections |
| Runtime | `Runtime/ExecutionContext.cs` | Call stack, scoping, musical context stack |
| Scope | `Runtime/StackFrame.cs` | Variables/functions with parent chain |
| Values | `Runtime/Value.cs` | CLR wrapper + Flow type info |
| Musical ctx | `Runtime/MusicalContext.cs` | Tempo / timesig / key / swing (push/pop) |
| Note streams | `Runtime/NoteStreamCompiler.cs` | `\| ... \|` → Sequence using active context |
| Overloads | `TypeSystem/OverloadResolver.cs` | Specificity-scored dispatch |
| Built-ins | `StandardLibrary/InternalFunctionRegistry.cs` + `BuiltInFunctions.cs` | Registration |
| Imports | `Runtime/ModuleLoader.cs` | `use "@x"` = stdlib, else relative |
| Lazy | `Runtime/Thunk.cs` | Memoizing thunk for `if`/`and`/`or` |
| Audio | `Audio/AudioPlaybackManager.cs` | Real-time playback via `IAudioBackend` |
| Synthesis | `StandardLibrary/Audio/Synthesizers/` | Per-instrument note rendering |
| DSP | `StandardLibrary/Audio/DSP/` | Reverb, filters, compressor, delay, granular, stretch, pitchShift |
| Harmony | `StandardLibrary/Harmony/` | Chord parsing, scale DB, roman numerals |

## Key Design Decisions

- **`->` is a parse-time transform.** `x -> f(arg)` → `f(x, arg)` as `FunctionCallExpression`. No runtime flow concept.
- **Overload resolution.** `OverloadResolver` scores: exact +1000, compatible +500, convertible +100. `Void` params act as wildcards.
- **Implicit returns.** `ImplicitReturnCollector` collects every non-void expression. 0 → `Void`; 1 → that value; 2+ → array. Explicit `return X` clears and short-circuits. No `Void`/`null`/`()` literal; bare `return` is a parse error. To return `Void` implicitly end with a void expression (e.g. `(print)` or a declaration).
- **Module imports execute in caller's context** (no namespace isolation). Circular imports detected.
- **Error accumulation.** `ErrorReporter` collects errors instead of throwing.
- **Musical context is scoped.** `tempo` / `timesig` / `key` / `swing` / `voicePool` / `tuning` push/pop on a stack.
- **Note streams compile at eval time** using active context.
- **Chord parsing at compile time** via `ChordParser.Parse()`.
- **Roman numerals** resolve from key context via `HarmonyFunctions`.
- **Song rendering is multi-pass.** Sections → sequences → voices → buffers → final mix.
- **Audio backend abstraction.** `IAudioBackend` chooses per platform: CoreAudio on macOS (AudioToolbox P/Invoke), PulseAudio on Linux (libpulse-simple; PipeWire compat).

## Language Features

### Core
- Static typing + type inference in some contexts
- `proc` declarations with implicit returns
- Lambdas: `fn Int x => (mul x 2)`, `fn Int a, Int b => (add a b)`; function types: `(Int => Int)`, `(Int, Int => Int)`
- Array literals + indexing (`arr@0`)
- Lazy evaluation, module imports via `use`
- Prefix-only arithmetic: `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(idiv)`/`(concat)` — no infix `+ - * /`
- **Symbol** (Phase 26.1): `#foo` interned, pointer-equality, strict-separate from String
- **Tuple** (Phase 26.1): `<<a, b, c>>` with empty + singleton arities; `tup@N` index; `<<a, b>> = expr` destructure; structural equality
- **`~>`** (Phase 26.1): tuple-unpack flow op; falls through to `->` on non-tuples
- **`Dict<K, V>`** (Phase 26.1): insertion-ordered; keys ∈ {Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables}; `(dict K V K V ...)` / `(dictTuple <<K,V>> ...)`; 14 ops (`get`/`getOr`/`set`/`remove`/`has`/`keys`/`values`/`size`/`merge`/`each`/`map`/`filter`)
- **`(unpack tuple func)`** — Lisp-style `(apply f args)`
- **Universal named args** (Phase 36 D-36-11): `(fn name1=val1 ...)` at language level; ~150 builtins backfilled; positional remains valid

### Music Types Quick Reference

Single source of truth for music-typed literals, their CLR type, numeric coercions, and call sites:

| Literal | Type | IsCompatibleWith | Accepted at |
|---|---|---|---|
| `-12dB` | `Decibel` | `Double`, `Float` | `gain`, `compress`/`sidechain` threshold |
| `100ms` | `Millisecond` | `Double`, `Float` | `delay`, `compress`/`sidechain` attack/release; → `Second` |
| `2.5s` | `Second` | `Double`, `Float` | `reverb` decay; → `Millisecond` |
| `+50c` | `Cent` | `Double`, `Float` | `transpose` cent-precision |
| `+2st` | `Semitone` | `Int` (whole-numbers-by-design) | `transpose` semitone-precision |
| `1.5` (Beat-tagged) | `Beat` | `Double`, `Float` | beat-position arithmetic |
| `440Hz` / `1.5kHz` | `Hertz` | `Double`, `Float` | filters, `createSineTone`/etc. (kHz → canonical Hz at lex time) |
| `#foo` | `Symbol` | strict (no Double/Float) | `Dict<Symbol, V>`, identity equality |
| `(loadScala "x.scl")` | `Tuning` | strict (reference identity) | `tuning t { ... }` block (Phase 32) |
| `(loadSfz #violin)` | `Sfz` | strict (reference identity) | `renderSong song "sampler:NAME"` (Phase 33) |
| `(markovTrain ...)` | `MarkovModel` | strict (reference identity) | `markovGenerate`, `markovEqual` (Phase 36) |
| `(lsystemModel ...)` | `LsystemModel` | strict (reference identity) | `lsystemGenerate`, `lsystemEqual` (Phase 36) |

Notes: Decibel/ms/Second/Cent/Semitone follow the `CentType.cs:24-27` pattern (sealed singleton + `IsCompatibleWith`). Hertz canonicalizes to Hz at lex time. Symbol is strictly separate from String — `(equals #foo "foo")` is `false` (SYM-01). Music literals at expression-start (after `(`, `=`, `,`) lex as single tokens (ERG-05). Reference-identity types: `(eq a b)` compares by reference; structural-equal helpers (`markovEqual`/`lsystemEqual`) exist where needed.

### Music-Specific
- **Musical context blocks**: `tempo 120 { }`, `timesig 4/4 { }` (also `timesig C { }` = 4/4 shorthand), `key Cmajor { }`, `swing 0.6 { }`, `voicePool 32 { }` (Phase 28), `tuning t { }` (Phase 32). All six keywords (`tempo`/`timesig`/`key`/`swing`/`voicePool`/`tuning`) are reserved.
- **`tuning <expr> { }`** (Phase 32): applies a `Tuning` value scoped to the block. Surface forms (D-15): identifier (`tuning partch { }`), inline call (`tuning (loadScala "x.scl") { }`), string sugar (`tuning "x.scl" { }`, parser-desugared). Last-wins with file-scope `enable justIntonation;`/`pythagorean;`/`equalTemperament;` pragmas. Tutorial: `examples/scala/intro.flow`.
- **Note streams**: `| C4 D4 E4 F4 |` with durations (`q`/`h`/`w`/`e`/`s`), rests (`_`), dotted (`C4q.`), tied (`C4h~`), cent offset (`C4+50c`), chord bracket (`[C4 E4 G4]q`)
- **Chord literals**: `Cmaj7`, `Dm`, `F#dim`, `Bb7`
- **Roman numerals** (in key context): `I`, `ii`, `IV`, `V7`, `vi`
- **Random choice in streams**: `(? C4 E4 G4)`, weighted `(? C4:50 E4:30 G4:20)`, seeded `(?? C4 E4 G4)`
- **Sections + Songs**: `section verse { }` / `Song s = [intro verse*2 chorus bridge outro]`
- **Parameterized sections** (Phase 36 SECT-01): `section verse(Note root, Int repeats = 2) { }`, called `[verse(C4, 2) chorus]`; `verse(C4)*3` repetition operator; full Phase 35 pattern syntax in signatures (typed bindings, tuple destructure, chord/numeral/articulation extractors); section overloading (D-36-18) via OverloadResolver specificity at call time. CALLSITE MusicalContext inherited at call time (Pitfall 7 dynamic scope). Tutorial: `examples/sections/parameterized.flow`.

### Audio + Articulation
- **`gain` vs `volume`** (Phase 26.2): `gain(Buffer, Double|Decibel)` reads 2nd arg as dB; `volume(Buffer, Double)` reads it as linear multiplier. Negative `volume` rejected; both warn to stderr when post-mult samples exceed 1.0.
- **Voice-block polyphony** (Phase 28): `| {voice C4w} {voice C5q D5q E5q F5q} |` — parallel voices share the bar's onset; `BarData.ParallelVoices` mixed additively. Same render path drives WAV + MIDI.
- **Articulations** (Phase 28). `leg`/`stacc`/`ten`/`marc`/`>` in note streams → `Articulation.*` enum. Locked rules: Staccato 25% dur + sustain=0 + release×0.5; Marcato 25% dur + Accent's velocity boost; Tenuto 100% dur + release×1.2; Legato 110% dur + crossfade overlap; Accent +0.30 velocity (clamped); Sforzando 100% dur + 1.5→1.0 envelope spike over first 15% of frames. All 9 synths route through `SynthUtils.GenerateArticulationADSR`; drums opt out via `isPercussion: true`. Phase 22's `legato(Sequence, Double)` transform composes with the enum (e.g. `Articulation.Legato` + `DurationOverlap=0.5` → 1.65× authored duration).
- **Voice-pool allocation** (Phase 28): `voicePool N { }` range 1..256, SPEC-7 default 32. Steal-oldest by earliest onset; tiebreaker = input index → deterministic, preserves two-run cmp-clean.
- **Sample-based tonal instruments** (Phase 29): Piano/Brass/Sax/Strings/Flute/Bell via `SampledInstrumentRenderer` backed by CC-BY 4.0 U-Iowa MIS bundle at `flow-lang/Samples/` (3.05 MB / 21 WAVs / 44.1 kHz 16-bit mono, ≤5 MB cap via `RepoSizeTests`). Each synth class is ≤25-line delegation shell; renderer picks nearest-pitch sample, linear-interpolation varispeed, layers Phase 28 envelope. Piano has 2 velocity layers (pp/ff cross-faded). Drums/Organ/Wavetable stay synthesis-based with ≥20% harmonic-richness floor (`HarmonicRichnessTests`). Eager-load on `renderSong` via per-FlowEngine `SampleCache`. SPEC-2 license set: CC0 / Public Domain / CC-BY 3.0 / CC-BY 4.0 (SA + NC rejected). Audited by `LicenseAuditTests`. Attribution: `Samples/CREDITS.md` + per-instrument `LICENSE.md`.
- **SFZ orchestral sampler** (Phase 33, opt-in `use "@sfz"`): `(loadSfz #violin)` resolves a 19-entry GM symbol dict against `sfz_root` from `~/.config/flow/config.toml` (Phase 30 config); `(loadSfz "/abs/path.sfz")` bypasses dict. Bind `Sfz violin = (loadSfz #violin)`; render via `renderSong song "sampler:violin"`. Common-subset parser (13 opcodes + `<region>`/`<group>`/`<global>`/`<control>`); per-region sustain loop with 441-frame equal-power crossfade; Phase 28 envelope on top. Phase 29 path stays byte-identical. Blessed library: VSCO Community CE 1.1.0 (CC-BY 4.0).
- **Multi-track MIDI export** (Phase 28): `writeMidi` emits one track per unique sequence name + conductor track. Prefix-match GM program routing (`piano*`→0, `brass*`/`horn*`→56, `sax*`→65, `flute*`→73, `string*`→48, `organ*`→19, `bell*`→14, `drum*`→ch 9 percussion). Cross-section same-name sequences concatenate in tick order.

### DSP (Phase 37)
- **Granular** (DSP-01): `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)`. Windowing: `#hann` (default) / `#gaussian` (σ=0.4, A2 LOCK) / `#tukey` (α=0.5, A3 LOCK). Jitter PRNG via `PrngRegistry` (`granular_offset` + `granular_timing`). Unknown windowing → Hann + one-shot advisory. Tutorial: `examples/dsp/granular.flow`.
- **Time-stretch + pitch-shift** (DSP-02 + DSP-03): `(stretch buf factor mode=#auto)`, `(pitchShift buf cents mode=#auto)`. Modes: `#vocoder` (Laroche-Dolson 1999 phase-locked STFT, harmonic), `#psola` (TD-PSOLA + YIN, percussive), `#auto` (Fitzgerald 2010 HPS per-frame + one-shot advisory `[stretch] mode=#auto picked: X% vocoder / Y% psola across N frames` per D-37-06). 6 LOCK knobs via prefix-ladder arity: `frameSize=2048` / `hopSize=512` / `overlap=4` (CCRMA Hann COLA min) / `transientThreshold=0.3` / `pitchPeriod` (PSOLA YIN override) / `windowSize` (PSOLA grain override). `pitchShift` accepts Double / Cent / Semitone (24 overloads = 3 × 8). Identity fast-paths (factor=1.0, cents=0) preserve two-run cmp-clean. Hand-rolled per D-v1.5-03 (RubberBand rejected: GPL). `loadWav` varispeed unaffected. Tutorial: `examples/dsp/stretch_pitchshift.flow`.
- **Stereo pan retrofit + SFZ polish** (MIX-01/02 + SAMP-01/02/03): per-voice `Pan` ∈ [-1, +1] on `Voice` shipped at synth path (D-37-15), pinned by `flow-lang.Tests/baselines/Phase37/mix_synth_path_pan.wav`. SFZ path: `effectivePan = clamp(region.Pan + voice.Pan, -1, +1)` (additive-with-clamp, OQ4 LOCK). **B2 LOCK** (Pitfall 12): SFZ voices ALWAYS promote to stereo via `ToStereoBufferWithPan` — `voice.Pan=0` means "center", not "unset". Centered pan → equal L/R at √0.5 (constant-power). SFZ opcode whitelist grew 14 → 20: `seq_position`/`seq_length` (round-robin, modulo `seq_length`, deterministic via fresh-per-render `SfzRenderer` + `ResetAtRenderBoundary`; `seq_length > 100` clamps + WarnOnce) + `xfin_lovel`/`xfin_hivel`/`xfout_lovel`/`xfout_hivel` (equal-power velocity crossfade, sin/cos, 0.7071 headroom; hard-switch fallback at sentinels = -1 preserves Phase 33 byte-identical). Per-articulation env-multiplier table (SAMP-03, A8 Option A LOCK): Staccato (0.5, 1.2, 1.0, 0.8) brightens decay; Marcato/Tenuto/Legato/Accent/Sforzando/Normal distinct scalars. Stacks multiplicatively on Phase 28 envelope, **SAMPLE-path caller site only** (Phase 28 `SynthUtils.GenerateArticulationADSR` unchanged → synth-path RMS regression preserved).
- **Piano warmth** (PIANO-01): SampleCache pp/ff → pp/mp/mf/ff at 5 pitch points (15 disk + 5 synth mp = 20 layers). mp via signed-RMS interp `mp[n] = sign(heavier) × sqrt(pp²·(1-α) + mf²·α)` with α=0.6 (A5 LOCK, mf-leaning). Deterministic + two-run cmp-clean preserved. `RmsInterpolateTruncated` tolerates length-mismatched pp/mf. `renderSong(..., release=Second)` exposes sustain-tail length (default 1.5s, D-37-11; clamped [0.05, 10.0]). Decay τ scales releaseSec × 0.3. Threaded via `PianoSynthesizer.CurrentReleaseSec` `AsyncLocal<double?>` + `SongRenderer.RenderSongWithRelease` (mirrors Phase 28 `VoiceAllocator._lastPoolSizeUsedForTests`). 4-way crossfade delegates to 2-way `LoudnessNormalizedCrossfade`; charitable 2-way fallback if mp/mf absent.
- **Flute D5 crossover** (FLUTE-01): manifest grew G4/G5 → G4/A4/G5. A4 (MIDI 69) chosen over D5 per RESEARCH §Pattern 10 — closes low-register varispeed gap (D5 now 5-semitone via A4 vs. 7-semitone via G4, better formant preservation).
- **Sampled drums** (DRUM-01): VSCO-CE `GM-StylePerc.sfz` via Phase 33 SFZ surface; `sfz.flow` GM dict 19 → 20 with `#drums "GM-StylePerc.sfz"` (D-37-13). **W7 LOCK**: `#drums` dict-symbol is source of truth for percussion detection — `SfzData.IsPercussion` set at `SfzBuiltins.LoadSfzSymbol` LOAD TIME when `symbolName == "drums"`, NOT by filename inspection. String-overload `loadSfz "/path/X.sfz"` bypasses dict and inherits `IsPercussion=false`. Drum pitch-shift via `PitchShiftEngine.Process(raw, semitones*100, StretchMode.Auto)` (D-37-14, transient-preserving); sample-center notes via Phase 33 varispeed (byte-identical at shift=0). Shifts > 12 st emit one-shot advisory.
- **Known sampled-instrument status**: flute D5 timbre gap CLOSED (FLUTE-01); sampled staccato thinness CLOSED (SAMP-03); sampled drums CLOSED (DRUM-01). v1.6 backlog: piano EQ + sympathetic-string resonance (D-37-09 narrow-scope, "Sound Design 2.0"); SAMP-03 Option B per-frame curve overlay (reserved escalation); sparse-named-arg call ergonomics (OverloadResolver relaxation); Auto-mode HPS rendering cost (renders both engines today).

### Generative + Improv (Phase 36)
- **`@patterns`** (PAT-01/02): 13 Tidal-style combinators on `Sequence` — `every n cb seq`, `fast seq factor`, `slow seq factor`, `chunk n cb seq`, `phase offset seq`, `rev seq`, `iter n seq`, `palindrome seq`, `jux cb seq` (v1.6 will add L/R stereo), `superimpose cb seq`, `sometimes prob cb seq`, `degrade seq` (fixed 50%), `sparseSeq prob seq`. Transform-arg combinators lambda-required (D-36-03). Cycle unit = bars (D-36-04). Charitable interpretation throughout (D-v1.5-05 + PAT-02) — degenerate inputs → input + advisory, never throws. Tutorial: `examples/generative/tidal_combinators.flow`.
- **`@generative`** (GEN-01..04 + D-36-06..09):
  - **Markov**: `(markov corpus order length seed)` one-shot OR split via `(markovTrain corpus order) → MarkovModel` + `(markovGenerate model length seed)` (D-36-06). Order clamped to [1, 3]. First `order` states alphabet-seeded. Named-arg features: `features=#pitch` (default) or `features=<<#pitch, #duration>>` (D-36-07).
  - **L-system**: `(lsystem axiom rules iterations)` one-shot OR split via `lsystemModel` + `lsystemGenerate`. Symbol alphabet (D-36-08). Iterations clamped [0, 20] (T-36-17 DoS guard). `(lsystemToSequence symbols mapper)` bridges to notes.
  - **Cellular**: `(cellular rule width steps seed)` (1D elementary, Wolfram single-1-center init); `(cellularSeeded rule width steps seed initialPattern)` escape hatch; `(life w h steps seed)` 2D Conway (30% density init). Per-dim cap 1024 (T-36-19).
  - **Chaos maps**: `(lorenz σ ρ β length seed)` (forward-Euler 3-state ODE, returns `Array[Double]` x-trajectory); `(logistic r length seed)`; bridge via `(quantizeToScale series scale)` (String name or `Array[Note]`). Degenerate params fall back charitably. **D-36-09 CAVEAT**: chained chaotic FP arithmetic diverges across platforms; same-platform two-run cmp-clean preserved, cross-platform reproducibility NOT guaranteed for chaos primitives.
- **PRNG routing** (D-v1.5-06 / GEN-05): all stochastic primitives route through `Runtime/PrngRegistry`, keyed by `(SourceLocation, generator-name)`. Unseeded calls reseed at `renderSong` / `writeWav` boundary preserving two-run cmp-clean. CI gate `PrngRegistryNewRandomGateTests` forbids unsanctioned `new Random(` in `StandardLibrary/{Patterns,Generative,Improv}/`; explicit-seed exceptions marked `// PRNG-SANCTIONED:`. Tutorial: `examples/generative/markov_jazz.flow`.
- **`@improv`** (IMPROV-01 + D-36-10..12): `(jam over=chords style=#jazz length=8 key="Cmajor" seed=N order=2)` returns chord-aware melodic `Sequence` — chord tones on strong beats, scale tones on weak, chromatic passing per style-weighted roulette. Only `over` required; defaults: `style=#jazz`, `length=8`, `key=active context`, `seed=PrngRegistry`, `order=2`. `key=` pushes synthetic MusicalContext frame (out-of-key pivot bars OK). **Style packs are MUSICAL CONTENT** (D-36-12): composer-editable Flow files at `flow-lang/improv/styles/*.flow` (shipped: `#jazz`/`#blues`/`#classical`) + `~/.config/flow/styles/*.flow` (user packs override via last-write-wins). Pack Dict shape documented at `flow-lang/improv/styles/README.md`. Charitable on degenerate inputs (empty over, unknown style, key/style mismatch) — always returns usable Sequence.

### Notation IO (Phase 39, opt-in `use "@notation-io"`)
- `(writeMusicXML "file.musicxml" song)` — MusicXML 3.1 partwise, MuseScore-compatible per D-v1.5-08 articulation table (Accent→`<accent/>`, Marcato→`<strong-accent/>`, Staccato→`<staccato/>`, Tenuto→`<tenuto/>`, Sforzando→`<dynamics><sfz/></dynamics>`, Legato→`<slur>` span per D-39-07). Multi-track Song → multi-`<part>` (D-39-09); voice blocks → per-note `<voice>N</voice>`; microtonal as decimal `<alter>` cents (D-39-06). Hand-rolled `XmlWriter` with `NewLineChars = "\n"` for two-run cmp-clean (Pitfall 6).
- `(writeLilyPond "file.ly" song)` — LilyPond 2.24+ text. Per-Sequence `\new Staff` (D-39-13); voice blocks → `\new Voice` siblings in `<< { } \\ { } >>`; microtonal as `% +Nc` comments (D-39-12); Dutch pitch convention `cis`/`bes`/etc. (Pitfall 2); `\layout { }` + `\midi { }` blocks kept.
- `(abc "...")` — ABC 2.1 + abc2midi subset import. Modal keys (Edor/Dmix/Aphr/Cmix/Glyd/Bphr/Floc, D-39-15); `Q:` tempo handles bare BPM + `1/4=BPM` + "Allegro" `1/4=BPM` (D-39-16); multi-tune `X:1`/`X:2` → `Array[Section]`. Charitable (D-39-17) — unknown ornaments dropped + `[abc]` advisory.
- `(mml "...")` — PC-98 MML common core (notes/accidentals/octave/length/tempo/loops with depth cap 16 per D-39-19). FM operator routing + drum-bank opcodes ignored + `[mml]` advisory (D-39-18).
- XML-02 round-trip CI gate (`mscore --convert-to mxl`) charitable-skips when binary absent (D-39-08). `flow-lang/Vendor/README.md` documents the Plan 39-01/03 decisions NOT to vendor `sightreader/musicxml-schemas` / `matthewcpp/ABCSharp` (XDocument structural diff + hand-rolled ABC parser fit better; both MIT-licensed if v1.6 reconsiders).

### Live Coding 2.0 (Phase 38)
- **`live <quantize> { body }` block** (LIVE-01): composer wraps hot-swappable code; on file save the block re-evaluates and swaps at the next quantize boundary with a 64-sample equal-power crossfade (D-38-06 preserved unchanged). Quantize accepts `Int` + `bar/bars` suffix (`1bar`, `2bar`), `NoteValue` (`q`/`h`/`w`/`e`/`s`), or omitted (defaults to `1bar`). Multiple `live` blocks per file swap on independent quantize timelines per D-38-02 — each block keyed by a stable FNV-1a `BlockId(SourceLocation)` routed via `Runtime/LiveBlockRegistry`. D-v1.5-07 stderr advisory on every entry — `[live] entering live block at line N — opts OUT of two-run cmp-clean determinism` (dedup'd per `(line, process)`). Offline render paths (`writeWav` / `writeMidi`) STAY deterministic.
- **Modernized `flow watch`** (LIVE-02): 4-row ANSI live status panel (UI-SPEC §"ANSI Live Status Panel") in `flow-interpreter/LiveStatusPanel.cs` (429 LOC) — Row 1 Tempo/TimeSig/Bar, Row 2 active live blocks, Row 3 Voices N/M + per-instrument breakdown, Row 4 sticky advisory (8s auto-clear). Plain-line fallback when `Console.IsOutputRedirected` / `NO_COLOR` / `--no-color` / `TERM=dumb`. 200ms file-watch debounce (down from legacy 500ms); 30s wall-clock evaluation cap via `Task.Run + Wait(TimeSpan.FromSeconds(30))` per RESEARCH §E Option A; 2 Hz heartbeat off the audio thread per Pitfall #21. LiveReloadManager preserves `CheckBarBoundary` + `ApplyCrossfade` + `RenderScript` body byte-identical per D-38-06.
- **State preservation across live reload** (LIVE-03): `Voice.Name` init property + `Voice.CopyStateFrom(prev)` transfers `OffsetBeats`; `VoiceAllocator.DiffByVoiceName(prev, next) → (Preserved, Dropped, Added)`; SongRenderer tags every voice `{sequenceName}:{ordinal}` at allocation. `flow-lang/Interpreter/LambdaCaptureAuditor.cs` (526 LOC) static AST walker covers every Phase 35/36/38 expression/statement/pattern record type with charitable D-v1.5-05 defaults. `LiveReloadManager.StagePendingBuffers` per-block stale-closure gate fires `[live] stale closure: references removed binding '<name>' at line N — keeping previous version`; per-swap `PrngRegistry.ResetAtRenderBoundary` exactly once; `ApplyFadeOut` on dropped voices. D-38-04 file-scope-edit detection emits `[live] file-scope edit detected outside live blocks at line N — restart 'flow watch' to apply` (yellow advisory; no auto-restart per Pitfall #12 "live session never dies mid-set").
- **REPL polish** (REPL-01..04): PrettyPrompt 4.1.1 (MPL-2.0, .NET 6+) replaces `Console.ReadLine`; Tab completion via in-process `flow-lsp` `CompletionHandler.BuildItems()` static helper per D-38-12 SIMPLIFICATION (no MemoryStream LanguageServer plumbing); **`:help fn` meta-command** per D-38-09 (overrides REQUIREMENTS.md REPL-02 `?fn` wording per D-v1.5-01 single-commit migration latitude — consistency with the existing `:quit`/`:help`/`:clear`/`:stop` family) prints bold+green header + dim signature + body + dim Example block from `BuiltInDocs.TryGet(identifier)`; Ctrl+R reverse history search via `persistentHistoryFilepath` pointing at `~/.config/flow/history` (10k cap, 0600 mode on Linux/macOS, XDG-compatible per Phase 30 precedent); multi-line continuation extends brace+proc-depth to LParen/RParen + LBracket/RBracket nesting (Rule 2 auto-add). **`(inspect seq)` / `(visualize seq)` alias pair** per D-38-10 (overrides solo `(inspect seq)` wording per D-v1.5-01) backed by one `VisualizationFunctions.cs` implementation; renders ASCII piano-roll with Phase 28 articulation glyphs at note onsets (`>` Accent / `.` Staccato / `^` Marcato / `_` Tenuto / `!` Sforzando / `~` Legato gap-fill per UI-SPEC §"Glyph Inventory") + new tick-mark row above first pitch row (`+` at bar columns / `-` elsewhere); collision rules: bar-line `|` wins over sustain `#`; Legato `~` renders in the gap cell BETWEEN connected notes on the LATER note's row.
- **Audio input** (AUDIO-IN-01..02): `(micBuffer Second)` / `(micBuffer Double)` overloads in `flow-lang/StandardLibrary/Audio/InputFunctions.cs` (244 LOC) backed by `flow-lang/Audio/PulseAudioCaptureBackend.cs` (272 LOC) sibling class to `PulseAudioSimpleBackend` — `PA_STREAM_RECORD = 2` + `pa_simple_read` P/Invoke binding. -20 dB feedback-guard attenuation scalar unconditional on every micBuffer open per Pitfall #24; linear-interp resample to 44.1 kHz at capture-side; charitable null-fallback to silent buffer when libpulse load fails; one-shot WarnOnce advisories `[audio-in] mic stream attenuated -20 dB on open` + `[audio-in] resampling capture stream from <N> Hz to 44100 Hz (linear interpolation)`; `CaptureOverride` + `NativeRateForTesting` test seam lets xUnit Facts exercise the full pipeline without real PulseAudio.
- **OSC surface** (opt-in via `use "@osc"`; OSC-01..02): 5 surface builtins + 1 marker — `oscSend(host, port, path, ...args)`, `oscListen(port, path, handler) → OscHandle`, `oscStop(handle)`, `oscBundle(...packets)`, `oscSendBundle(host, port, bundle)`, plus internal `__enableOscModule` flipping `ExecutionContext.OscEnabled`. **D-38-13 charitable smallest-tag-that-fits inference** (overrides REQUIREMENTS.md OSC-02 strict-tag-by-arg wording per D-v1.5-05 + D-v1.5-01): Int→`,i` Long→`,h` Float→`,f` Double→`,d` String|Symbol→`,s` Bool→`,T`/`,F` Buffer→`,b` (blob — 4-byte LE IEEE-754 flatten). Composer escape hatch via explicit cast at call site (`(toLong 1)`, `1.5d`). D-38-14 per-path drop-newest sample-and-hold at 5ms (= 1/200Hz) via `ConcurrentDictionary<string, long> _lastFireTimeMs`; D-38-15 bundle dispatch both directions with timetag honored on receive + nesting depth cap 8 (mirrors Phase 36 T-36-17 / Phase 39 D-39-19 DoS guard); D-38-16 reference-identity `OscHandle` Value (specificity 151) with dual-role discriminator (listener `Receiver` non-null vs pending-packet `PendingPacket` non-null); Pitfall #5 `Cts.Token.Register(() => receiver.Dispose())` forces blocked `Receive()` to throw `ObjectDisposedException`. Backed by Rug.Osc 1.2.5 (MIT, .NET Standard 2.0, zero transitive deps).

## AST Node Types

**Expressions (15 in `Ast/Expressions/`)**: `Literal`, `Variable`, `FunctionCall`, `Flow` (`->`), `ArrayLiteral`, `ArrayIndex` (`arr@N`), `Lazy`, `Lambda` (`fn x => ...`), `MemberAccess`, `ChordLiteral`, `NoteStream` (`| ... |`), `Song` (`[intro ...]`), `SymbolLiteral` (`#foo`, interned in `ExecutionContext.SymbolInternTable`), `TupleLiteral` (`<<a, b, c>>`), `TupleUnpackFlow` (`~>`).

**Statements (9 in `Ast/Statements/`)**: `VariableDeclaration`, `AssignmentStatement`, `ProcDeclaration`, `ReturnStatement`, `ExpressionStatement`, `ImportStatement`, `MusicalContextStatement`, `SectionDeclaration`, `TupleDestructureStatement`.

Arithmetic uses prefix builtins (no `BinaryExpression` AST node); parser emits `FunctionCallExpression`.

## Type System

In `TypeSystem/`. Each type extends `FlowType` and implements `IsCompatibleWith()`, `CanConvertTo()`, `GetSpecificity()`. Numeric widening: `Int → Long → Float → Double → Number`.

- **Primitive** (`PrimitiveTypes/`): Void, Int, Float, Long, Double, String, Bool, Number, Buffer, Lazy, Function, Envelope, OscillatorState, Voice, Track
- **Special** (`SpecialTypes/`): Note, Semitone, Cent, Millisecond, Second, Decibel, Beat, Hertz, Bar, TimeSignature, NoteValue, Sequence, MusicalNote, Chord, Section, Song, Tuning (Phase 32, ref-id), Sfz (Phase 33, ref-id), MarkovModel (Phase 36, ref-id, specificity 148), LsystemModel (Phase 36, ref-id, specificity 149)
- **Array**: `ArrayType` — generic with element-type tracking

## Built-in Function Categories

- **Core** (`stdlib.cs`): I/O (`print`, `input`), `str`, arithmetic, conversions, comparisons, `and`/`or`/`not`, `if`, random (`random`, `randomInt`, `choose`)
- **Collections** (`BuiltInFunctions.cs`): `list`, `head`, `tail`, `last`, `init`, `empty`, `reverse`, `take`, `drop`, `append`, `prepend`, `concat`, `contains`, `map`, `filter`, `reduce`, `each`, `length`, `range`, `zip`
- **Audio core** (`Audio/`): `buffer`, `silence`, `createSineTone`/`createSawTone`/`createSquareTone`/`createTriangleTone`, `noise`, `adsr`, `applyEnvelope`, `writeWav`
- **Audio effects** (`Audio/DSP/`): `reverb`, `lowpass`, `highpass`, `bandpass`, `compress`, `delay`, `gain` (dB), `volume` (linear); Phase 37: `granular`, `stretch`, `pitchShift`. All ride shared `WindowFunctions` (Hann/Gaussian/Tukey) / `Fft` (radix-2 Cooley-Tukey) / `Hps` (Fitzgerald 2010) + `PhaseVocoder` (Laroche-Dolson 1999) / `Psola` (TD-PSOLA + YIN).
- **Audio input** (`StandardLibrary/Audio/InputFunctions.cs`, Phase 38 AUDIO-IN-01/02): `(micBuffer Second)` / `(micBuffer Double)` reads from the default PulseAudio input device via `PA_STREAM_RECORD` P/Invoke; -20 dB feedback-guard attenuation on open; linear-interp resample to 44.1 kHz at capture-side; charitable null-fallback to silent buffer. Composable with every existing Audio effect / playback / `writeWav` builtin via the shared `AudioBuffer` value type.
- **Playback** (`Audio/PlaybackFunctions.cs`): `play`, `loop`, `preview`, `stop`, `audioDevices`, `setAudioDevice`, `isAudioAvailable`
- **Musical notation** (`Audio/ClassicalComposition.cs`): `musicalNote`, `rest`, bars, sequences, `renderSequence`, `renderSequences`
- **Song rendering** (`Audio/SongRenderer.cs`): `renderSong` (sections → sequences → voices → buffer, with instrument routing)
- **Harmony**: `chordNotes`, `chordRoot`, `chordQuality`, `arpeggio`, `scaleNotes`, `resolveNumeral`, `getSections`, `sectionSequences`
- **Transforms**: `transpose`, `invert`, `retrograde`, `augment`, `diminish`, `up`, `down`, `repeat`, `concat`
- **Tuning** (Phase 32): `(loadScala "x.scl")` (synthesizes default linear KBM, period auto-adopts scale's period via D-07 — Carlos Alpha / Bohlen-Pierce Just Work without explicit `.kbm`); `(loadScala "scl" "kbm")` 2-arg overload (SPEC-4) with `.kbm` period overlaid; `(str t)` returns D-04 description `Tuning("desc", N steps, period X.XX¢)`. Apply via `tuning t { }` block. One-shot stderr advisory `[tuning] unmapped MIDI keys under '<desc>' — rendered as rest` per process per `Tuning.Description` (D-08).
- **Generative** (Phase 36): `euclidean`; note-stream `(? ...)`/`(?? ...)`; `@patterns` (13 combinators); `@generative` (`markov`/`markovTrain`/`markovGenerate`/`markovEqual`, `lsystem`/`lsystemModel`/`lsystemGenerate`/`lsystemToSequence`/`lsystemEqual`, `cellular`/`cellularSeeded`/`life`, `lorenz`/`logistic`/`quantizeToScale`); `@improv` (`jam`, `registerStyle`, `listStyles`)
- **Notation IO** (Phase 39, `@notation-io`): `writeMusicXML`, `writeLilyPond`, `abc`, `mml`
- **Network** (Phase 38, `@osc`): `oscSend(host, port, path, ...args)`, `oscListen(port, path, handler) → OscHandle`, `oscStop(handle)`, `oscBundle(...packets)`, `oscSendBundle(host, port, bundle)`. D-38-13 charitable smallest-tag-that-fits type-tag inference; D-38-14 per-path 200 Hz rate limit (drop-newest sample-and-hold); D-38-15 bundle dispatch both directions with depth cap 8; D-38-16 reference-identity `OscHandle` Value. Rug.Osc 1.2.5 (MIT, zero transitive deps).

## Standard Library Modules (`flow-lang/*.flow`)

Loaded via `use "@name"`:
- `std.flow` — imports `@collections` + `@bars`
- `collections.flow`, `bars.flow`, `notation.flow`, `composition.flow`
- `audio.flow` — buffer/signal/effects/playback convenience; Phase 37 adds `granular` (3 overloads) + `stretch` (8-arity prefix-ladder) + `pitchShift` (24 overloads) + `renderSong(Song, String, Second)` for PIANO-01 `release=`
- `sfz.flow` — SFZ surface (Phase 33; opt-in `@sfz`); Phase 37 DRUM-01 grew GM dict 19 → 20 with `#drums "GM-StylePerc.sfz"` (W7 LOCK)
- `patterns.flow`, `generative.flow`, `improv.flow` — Phase 36 (opt-in `@patterns` / `@generative` / `@improv`)
- `notation-io.flow` — Phase 39 export/import (opt-in `@notation-io`). Distinct from `notation.flow`. Hand-rolled emit/import, no vendored sources, zero new NuGets.
- `osc.flow` — OSC client + server (Phase 38 OSC-01/02; opt-in via `use "@osc"`). Rug.Osc 1.2.5 backed (MIT, zero transitive deps). 5 surface builtins + 1 init marker; D-38-13/14/15/16 locked behaviors (charitable type-tag inference / 200 Hz per-path rate limit / bundle depth cap 8 / reference-identity OscHandle).

## Adding Built-ins

1. Define in `StandardLibrary/BuiltInFunctions.cs` (or relevant subdir: `Audio/`, `Harmony/`, `Transforms/`)
2. Create a `FunctionSignature` (name, param types, varargs flag, parameter names per Phase 36 D-36-11)
3. `registry.Register(signature, args => { ... })` in the appropriate `Register*` method
4. For a new module, call its registration from `FlowEngine.cs`

## Adding Synthesizers

1. New class in `StandardLibrary/Audio/Synthesizers/` (see `PianoSynthesizer.cs`)
2. Register instrument-name → class mapping in `SongRenderer.cs`

## C# Conventions

- .NET 10, C# 13, nullable on, implicit usings, file-scoped namespaces
- All namespaces under `FlowLang.*` (library) or `FlowInterpreter` (CLI)
- AST nodes are `record` types
- Switch expressions for node dispatch (not visitor pattern)
- External deps: only Pidgin (referenced but unused — SimpleLexer/Parser are manual) + Melanchall.DryWetMidi 8.0.3 (only library justified; MIDI SMF encoding is error-prone). NOT recommended: NAudio/CSCore (Windows-centric), NWaves (would duplicate hand-built DSP, abandoned at v0.9.6), managed-midi (past project), SIMD (premature)

<!-- GSD:project-start source:PROJECT.md -->
## Project

**Flow Language** — interpreted, statically-typed language for music production. C# / .NET 10. Flow operator, music types, note streams, musical-context blocks, full audio pipeline (composition → WAV → real-time playback), MIDI import. For composers, producers, and creative coders.

**Core value:** users write musical ideas as code and hear them immediately — the language must faithfully translate notation into correct, playable audio.

**Constraints:** target `net10.0`; Linux + macOS playback via `IAudioBackend` (PulseAudio + CoreAudio); minimal external deps (Pidgin reference + DryWetMidi 8.0.3); real-time audio = efficient buffer ops with no GC pressure in hot paths; existing `.flow` scripts and test suite must keep working.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->
## Technology Stack

**Runtime:** .NET 10 / C# 13 (record types, pattern matching, file-scoped namespaces).

**Audio backends (P/Invoke):** PulseAudio (`libpulse-simple.so.0` on Linux, also covers PipeWire); CoreAudio AudioQueue (`AudioToolbox.framework` on macOS). Both expose stereo. `IAudioBackend.IsAvailable()` probe gates per-platform selection.

**External library — only one:** `Melanchall.DryWetMidi 8.0.3` for MIDI SMF write/read. .NET Standard 2.0, .NET 10-compatible, actively maintained. Hand-rolling delta times / VLE / tempo maps / channel assignment is error-prone — the library earns its keep.

**Hand-rolled in flow-lang (no library):** voice allocation (Phase 28), custom oscillators (`OscillatorState` + lambda callbacks), sidechain compression (extend `Compressor.cs` with second input), spatial panning (constant-power `cos`/`sin`), WAV loading (reverse of `FileIO.cs` writer), pattern variation (`NoteStreamCompiler` random-choice extension), polyrhythm (parallel `MusicalContext` per voice), beat-synced live reload (`FileSystemWatcher` + bar-quantized reload), loop constructs (new AST + parser + interpreter), string interpolation (lexer + AST node), chord-progression DSL (extends `ChordParser` + `HarmonyFunctions`), sequence visualization (ASCII piano roll from `MusicalNoteData`).

**Rejected libraries:** NAudio / CSCore (Windows-centric, would duplicate pipeline); NWaves (would duplicate DSP, abandoned at v0.9.6); managed-midi (past project — DryWetMidi wins); SIMD / `System.Numerics.Tensors` (premature optimization — sample-by-sample is clear and correct).
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

- **Pre-Phase-28 byte-identical determinism for `tutorial.flow` / `showcase.flow` is dropped.** Phase 28's articulation rewrite changes rendered bytes legitimately. Two-run determinism IS preserved (same SHA → byte-identical) — contract in shape, not pinned bytes.
- **RMS-windowed regression tests** for behavior that legitimately changes bytes but should preserve perceptual fidelity: `flow-lang.Tests/Helpers/RmsRegressionTests.AssertRmsWithinTolerance` (or `AssertWavMatchesBaseline`), SPEC-8 locked ±0.5 dB / 100 ms. Baselines under `flow-lang.Tests/baselines/Phase28/` — committed because the dither RNG is seeded deterministically (Phase 15 Plan 05).
- **Phase 36 chaos primitives — same-platform determinism only** (D-36-09). `lorenz` + `logistic` are forward-Euler-integrated chaotic systems; chained FP arithmetic diverges exponentially across platforms after ~50 iterations. Same-platform two-run cmp-clean preserved; cross-platform NOT guaranteed for chaos. Cross-platform CI gates MUST exclude Lorenz/logistic fixtures (e.g. `examples/generative/markov_jazz.flow`) from shared-baseline comparison. Markov / L-system / cellular are integer-arithmetic and stay cross-platform deterministic. Other Phase 36 stochastic primitives (`sometimes`/`degrade`/`sparseSeq`/`jam`) route via `PrngRegistry` and inherit the Phase 28/29/33 two-run cmp-clean contract.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

Not yet mapped. Follow existing patterns in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit/Write/etc., start work through a GSD command so planning artifacts and execution context stay in sync.

- `/gsd:quick` — small fixes, doc updates, ad-hoc tasks
- `/gsd:debug` — investigation, bug fixing
- `/gsd:execute-phase` — planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate.
> This section is managed by `generate-claude-profile` — do not edit manually.
<!-- GSD:profile-end -->
