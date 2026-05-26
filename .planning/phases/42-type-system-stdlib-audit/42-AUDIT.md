# Flow Language — Phase 42 Type System & Stdlib Audit

**Date:** 2026-05-24
**Scope:** Read-only analysis of `flow-lang/StandardLibrary/`, `flow-lang/TypeSystem/`, and `flow-lang/*.flow` stdlib modules.
**Method:** Reflective harness (`scripts/StdlibAuditor`) + grep inventory (`scripts/audit/`) — raw data under `42-AUDIT-data/`.
**Stable identifier:** builtin name + signature (NOT file:line — see RESEARCH.md Pitfall 7).
**Routing:** Every finding tagged for Phase 43 (module/naming + new builtins), Phase 44 (strict mode), or v1.6-backlog.

**Snapshot scale.** 37 FlowType subclasses (10 coercible, 5 reference-identity, 22 strict-equality). 413 registered builtin signatures across `BuiltInFunctions.cs` + 14 context-bound modules (SfzBuiltins / NotationIoBuiltins / OscFunctions / MarkovFunctions / LsystemFunctions / CellularFunctions / ChaosFunctions / StretchFunctions / PitchShiftFunctions / GranularFunctions / PatternFunctions / JamFunctions / StyleRegistry / Scala). 327 unique `.flow` proc declarations across 12 stdlib modules, cross-referenced against 4114 unique call-site tokens in `flow-lang/*.flow` + `examples/**/*.flow` + `tests/test_*.flow`.

---

## 1. Orphaned Types

A coercible type is **orphaned** when zero registered signatures accept it as a parameter — the composer literally cannot pass a value of that type to any builtin.

| Type | Kind | Producer? | Consumer? | Routing | Rationale |
|------|------|-----------|-----------|---------|-----------|
| `BeatType` (flow: `Beat`) | coercible | Yes — `Value.Beat()` at literal-construction time (`Interpreter.cs:1019`); ratio-tagged double literals (`1.5b` form in researcher notes, but no surface syntax today) | **No** — zero signatures accept `Beat` per `type-signature-graph.json` orphans array | → Phase 43 (HIGH) | Anchor finding (RESEARCH §Summary). `Beat` is the only coercible orphan. Phase 43 should add the missing companion overloads (`renderBarAtBeat(Sequence, Beat)`, `delay(Buffer, Beat)`, etc.) plus a context-aware `(beatToSec Beat) → Second` builtin (see §2). |

### Reference-Identity Types (not orphans — Pitfall 2 guard applied)

Reference-identity types intentionally have small consumer counts because their lifecycle is producer + one consumer site. They are NOT orphans even at consumer_count=1.

| Type | Producer | Consumer | Routing | Rationale |
|------|----------|----------|---------|-----------|
| `TuningType` (flow: `Tuning`) | `loadScala(String)` / `loadScala(String, String)` | `tuning t { }` block at `Interpreter.cs:ExecuteMusicalContext` (NOT a registered builtin) | → not a gap | Correct by design (Phase 32). Block consumer, not registry. |
| `SfzType` (flow: `Sfz`) | `loadSfz(Symbol)` / `loadSfz(String)` | `renderSong song "sampler:NAME"` string-dispatch in `SongRenderer.cs` (NOT a registered builtin) | → not a gap | Correct by design (Phase 33). String-dispatch consumer, not registry. |
| `MarkovModelType` (flow: `MarkovModel`) | `markovTrain(corpus, order) → MarkovModel` | `markovGenerate(MarkovModel, length, seed)` / `markovEqual(MarkovModel, MarkovModel)` | → not a gap | Correct by design (Phase 36). Producer/consumer pair fully registered. |
| `LsystemModelType` (flow: `LsystemModel`) | `lsystemModel(axiom, rules)` | `lsystemGenerate(LsystemModel, iterations)` / `lsystemEqual(LsystemModel, LsystemModel)` | → not a gap | Correct by design (Phase 36). Producer/consumer pair fully registered. |
| `OscHandleType` (flow: `OscHandle`) | `oscListen(port, path, handler)` | `oscStop(OscHandle)` | → not a gap | Correct by design (Phase 38). Lifecycle pair fully registered. |

### Strict-Equality Types (not orphans — high consumer counts)

The 22 strict-equality types (BarType, BoolType, BufType, BufferType, ChordType, DoubleType, EnvelopeType, FloatType, FunctionType, IntType, LongType, NoteType, NumberType, OscillatorStateType, SectionType, SequenceType, SongType, StringType, SymbolType, TimeSignatureType, TrackType, VoiceType) all have consumer_count ≥ 1 in `type-signature-graph.json`. No gaps in this class.

---

## 2. Missing Conversions

The harness surfaced **122 asymmetric pairs** in `type-signature-graph.json` `asymmetries`. Most are correct-by-design widening edges (e.g., `Beat → Double` is intentional — music types widen to numerics, not the reverse). The musically-meaningful gaps below need a fix shape.

| From | To | Current State | Routing | Fix Shape |
|------|----|----|---------|-----------|
| `Beat` | `Second` | No conversion path (tempo-context required) | → Phase 43 (HIGH) | New context-aware builtin `(beatToSec Beat) → Second` reading active `tempo` from `ExecutionContext.MusicalContext`. **NOT** a `BeatType.CanConvertTo(SecondType)` override per Pitfall 3 — pure-function `FlowType` methods have no runtime context access. |
| `Second` | `Beat` | No conversion path (tempo-context required) | → Phase 43 (MEDIUM) | Companion `(secToBeat Second) → Beat` builtin. Same tempo-context constraint. |
| `Double` | `Decibel` | No conversion (Decibel → Double works, but not the reverse) | → Phase 44 (HIGH) | Explicit-conversion builtin `(db Double) → Decibel`. Strict-mode opt-in surface listed in ROADMAP line 372. |
| `Double` | `Cent` | No conversion | → Phase 44 (HIGH) | `(cents Double) → Cent`. ROADMAP line 372. |
| `Double` | `Hertz` | No conversion | → Phase 44 (HIGH) | `(hz Double) → Hertz`. ROADMAP line 372. |
| `Double` | `Millisecond` | No conversion | → Phase 44 (HIGH) | `(ms Double) → Millisecond`. ROADMAP line 372. |
| `Double` | `Second` | No conversion | → Phase 44 (HIGH) | `(sec Double) → Second`. ROADMAP line 372. |
| `Int` | `Semitone` | No conversion (Semitone widens from Int via `IsCompatibleWith` but explicit cast missing) | → Phase 44 (MEDIUM) | `(st Int) → Semitone` companion. Lower priority because Semitone already accepts Int via IsCompatibleWith widening at call sites. |
| `Int` | `NoteValue` | No conversion | → v1.6-backlog (LOW) | `NoteValue` is an enum-shaped type (WHOLE..THIRTYSECOND); `Int` widening already works at call sites via IsCompatibleWith — explicit cast cosmetic. |
| `Float` ↔ `Decibel`/`Cent`/`Hertz`/`Millisecond`/`Second` | Same asymmetry as Double for Float | → Phase 44 (LOW) | Companion `Float` overloads only if Phase 44 strict mode demands `Float` distinct from `Double`. |

The remaining 100+ asymmetries are deliberate (every `*Type` widens to `Void` because `VoidType` is the wildcard sentinel in `OverloadResolver`, and every `Int/Long/Float/Double/Number` pair is asymmetric within the numeric widening lattice per design).

---

## 3. Asymmetric Pairs

Surface-level builtin pairs where the "obvious counterpart" appears missing. False-positive guard applied per Pitfall 5 — counterpart pairs are confirmed by reading the registry, not heuristic.

| Pair Candidate | Verdict | Routing | Rationale |
|----------------|---------|---------|-----------|
| `writeMidi(String, Song)` + missing `readMidi(String) → Song` | **Genuine asymmetric** | → v1.6-backlog (MEDIUM) | Currently delegated to the standalone `flow-midi` CLI (Phase 30 SHIPPED `midi2flow` subcommand). Bringing `readMidi` into the registry would let composers do `let song = readMidi("loop.mid")` in `.flow` source. Intentional asymmetry today because the conversion is text-to-source, not value-producing; the CLI emits `.flow` text. v1.6 reconsiders if composers ask. |
| `writeWav(String, Buffer)` + missing `readWav(String) → Buffer` | **Closed by `loadWav`** (Phase 22) | → not a gap | `loadWav(String) → Buffer` is registered (`audio.flow`). Sibling naming convention is `load*`/`write*` not `read*`/`write*` per Flow stdlib (e.g., `loadScala`/`loadSfz`). No fix needed. |
| `writeMusicXML(String, Song)` + missing `readMusicXML(String) → Song` | **Genuine asymmetric** | → v1.6-backlog (MEDIUM) | Phase 39 shipped MusicXML emit-only; import path was researcher discretion (D-39-04 chose ABC import via hand-rolled parser over MusicXML import). v1.6 reconsiders if composers request round-trip. |
| `writeLilyPond(String, Song)` + missing `readLilyPond(String) → Song` | **Intentional one-way** | → not a gap | LilyPond is render-only by convention (it's a typesetting language; round-trip is non-goal for any LilyPond consumer). Same as MIDI: emit out, don't import back. |
| `abc(String) → Array[Section]` + missing `writeABC(String, Song)` | **Genuine asymmetric** | → v1.6-backlog (LOW) | Phase 39 shipped ABC import-only; emit was researcher discretion (D-39-04). ABC emit would let composers share `.abc` scratches. v1.6 reconsiders. |
| `mml(String) → Array[Section]` + missing `writeMML(String, Song)` | **Genuine asymmetric** | → v1.6-backlog (LOW) | Same as ABC — Phase 39 ships PC-98 MML import via `MmlImport.cs` (D-39-18) but no emit. v1.6 reconsiders. |
| `loadScala(String) → Tuning` + missing `saveScala(String, Tuning)` | **Intentional one-way** | → not a gap | Scala (`.scl`) is a vendor format owned upstream by Manuel Op de Coul. Flow consumes; it does not author Scala files. Documented at Phase 32 SPEC-1. |
| `loadSfz(Symbol)`/`loadSfz(String) → Sfz` + missing `saveSfz(String, Sfz)` | **Intentional one-way** | → not a gap | SFZ is a vendor format; Flow consumes VSCO-CE 1.1.0 and other SFZ libraries. Authoring SFZ is out of scope. |
| `markovTrain(corpus, order) → MarkovModel` + `markovGenerate(MarkovModel, length, seed)` | **Closed pair** (FALSE POSITIVE GUARD per Pitfall 5) | → not a gap | This is NOT asymmetric — `train`/`generate` is the canonical Markov lifecycle pair. Listed here to document the false-positive guard applied. |
| `lsystemModel(axiom, rules) → LsystemModel` + `lsystemGenerate(LsystemModel, iterations)` | **Closed pair** | → not a gap | Same as Markov — Phase 36 train/generate pair. False-positive guard applied. |
| `oscListen(port, path, handler) → OscHandle` + `oscStop(OscHandle)` | **Closed lifecycle** | → not a gap | Phase 38 D-38-16 — listen returns handle; stop releases it. No `oscUnlisten` because `oscStop` IS the unlisten verb. |
| `oscSend(host, port, path, ...args)` + missing `oscReceive(handle) → Value` | **Closed by listener-callback design** | → not a gap | Phase 38 uses callback-style receive via `oscListen`'s `handler` lambda parameter (D-38-13). No polling-style receive needed — the OSC surface is deliberately push-only into the handler. |

---

## 4. Dead-End Builtins

Cross-referenced 269 registered builtin names against the 327 `.flow` proc declarations (`flow-proc-decls.txt`) + 4114 unique call-site tokens (`flow-call-sites.txt`) + every `.flow` file under `examples/` and `tests/`.

| Candidate | Verdict | Routing | Sources Checked |
|-----------|---------|---------|-----------------|
| `?` | **NOT dead-end** — note-stream random-choice operator (parser-level syntax, called inline at lex/parse time, NOT via `(?)` call form) | → not a gap | Parser/SimpleLexer route this token before reaching the registry call dispatcher. |
| `??` | **NOT dead-end** — seeded note-stream random-choice operator (parser-level syntax) | → not a gap | Same as `?` — parser-level routing. |
| `??reset` | **NOT dead-end** — PRNG registry reset hook called by `renderSong`/`writeWav` boundary code (`PrngRegistry.ResetAtRenderBoundary`), NOT composer-callable | → not a gap | Internal contract per Phase 36 D-v1.5-06. |
| `??set` | **NOT dead-end** — PRNG registry seed-set hook for explicit-seed overloads | → not a gap | Internal contract per Phase 36 D-v1.5-06. |
| `inspect(Sequence)` | **NOT dead-end** — Phase 38 D-38-10 REPL-only callable used at the `>` prompt for ASCII piano-roll visualization | → not a gap | Verified in `examples/live/repl_session.md:98` (REPL transcript). The token `inspect` IS in `flow-call-sites.txt` (5 occurrences across examples + tests). |

**No genuine dead-end builtins identified — every C# registration has at least one .flow caller or documented internal contract (verified against `AUDIT-data/flow-call-sites.txt` + `AUDIT-data/flow-proc-decls.txt` + `examples/**/*.flow` + `tests/test_*.flow`).**

This matches the Pitfall 1 sanity check (any list >20 entries is a false-positive flood — our list of 5 candidates ALL turned out to be parser-syntactic or REPL-only sites, not dead-ends).

---

## 5. Overload Gaps

The harness flagged **85 functions** that accept `Double` but lack one or more music-type companion overloads. Applying the ergonomics test from CLAUDE.md project constraints — a missing overload is a gap ONLY if the composer's natural call shape fails today (CLAUDE.md "Music Types Quick Reference" widens Decibel/ms/Second/Cent/Semitone/Hertz to Double/Float via `IsCompatibleWith`, so `(reverb buf 2.5)` Just Works — `Second` widens to `Double` at the call site).

### §5a Gaps that fail today (HIGH priority — composer's natural call breaks)

| Function | Missing Music-Type | Routing | Failing Composer Call |
|----------|--------------------|---------|----------------------|
| `pitchShift(Buffer, ?)` | `Decibel` / `Hertz` / `Millisecond` / `Second` (24 overloads exist across Double/Float/Cent/Semitone × 8 arity tiers, but Hz mode missing) | → Phase 43 (LOW) | `(pitchShift buf 440Hz)` — composer's natural "shift to absolute pitch" pattern fails. NOTE: Hertz mode is semantically distinct from cents-relative shift — needs design decision before backfill. May be intentional (cents/semitones are the documented contract). |
| `delay(Buffer, ?)` | `Semitone` / `Hertz` / `Cent` | → not a gap | `Millisecond` and `Second` already accepted (Phase 26.2). Semitone/Hertz/Cent would be musically nonsensical for delay-time. **Cull.** |
| `bandpass(Buffer, ?, ?)` | `Decibel` / `Cent` / `Millisecond` / `Second` / `Semitone` | → not a gap | Bandpass takes `centerHz` + `bandwidthHz`; `Decibel`/`Cent`/`Millisecond`/`Second`/`Semitone` are nonsensical. Only `Hertz` is missing as an explicit overload, BUT `Double`/`Float` widening already accepts it. **Cull.** |
| `compress(Buffer, ?, ?, ?, ?)` | `Cent` / `Hertz` / `Second` / `Semitone` | → not a gap | Compressor takes `threshold:Decibel` + `attack:Ms` + `release:Ms` + `ratio:Double` — all four ARE already music-typed correctly. Missing types are nonsensical for compression. **Cull.** |
| `reverb(Buffer, ?)` | `Decibel` / `Cent` / `Hertz` / `Millisecond` / `Semitone` | → not a gap | Reverb takes `decay:Second` which works via widening. Other music types nonsensical. **Cull.** |
| `gain(Buffer, ?)` | `Cent` / `Hertz` / `Millisecond` / `Second` / `Semitone` | → not a gap | Gain takes `dB:Decibel` (Phase 26.2 D-26.2). Other music types nonsensical. **Cull.** |
| `lowpass(Buffer, ?)` / `highpass(Buffer, ?)` | `Decibel` / `Cent` / `Millisecond` / `Second` / `Semitone` | → not a gap | Filters take `cutoffHz:Hertz` (widening from Double works). Other types nonsensical. **Cull.** |
| `sidechain(Buffer, Buffer, ?, ?, ?, ?)` | `Cent` / `Hertz` / `Second` / `Semitone` | → not a gap | Sidechain mirrors `compress` — already music-typed. **Cull.** |

### §5b Gaps that work via widening (LOW priority — cosmetic only, ergonomically valid today)

The remaining 70+ candidates (`abs`, `accelerando`, `add`, `beatsToFrames`, `ceil`, `cos`, `createADSR`, `createAR`, `createClip`, `createOscillatorState`, `createSineTone`, `createVoice`, `crescendo`, `decrescendo`, `div`, `doubleToInt`, `euclidean`, `fadeIn`, `fadeOut`, `fast`, `fillBuffer`, `floor`, `generateSaw`, `generateSine`, `generateSquare`, `generateTriangle`, `granular`, `humanize`, `humanizeGaussian`, `legato`, `loadWav`, `log`, `logistic`, `lorenz`, `max`, `micBuffer`, `min`, `mixBuffers`, `mul`, `neg`, `noise`, `pan`, `phase`, `pow`, `quantize`, `renderBarAtBeat`, `renderBarAtTime`, `renderBarToVoices`, `renderSequenceToVoices`, `renderTrack`, `ritardando`, `round`, `scaleBuffer`, `setBPM`, `setSample`, `setTrackGain`, `setTrackOffset`, `setTrackPan`, `setVoiceGain`, `setVoiceOffset`, `setVoicePan`, `sin`, `sing`, `slow`, `sometimes`, `sparseSeq`, `sqrt`, `str`, `stretch`, `sub`, `swell`, `tan`, `tempoRamp`, `vary`, `volume`) all accept `Double` and the music-typed call works via `IsCompatibleWith` widening (Decibel/ms/Second/Cent/Semitone → Double).

| Routing | Rationale |
|---------|-----------|
| → v1.6-backlog (LOW) | Cosmetic explicit-overload backfill. The composer's `(crescendo seq 0.0 -6dB 1.5s)` already works today via widening — adding the explicit `Decibel` overload changes nothing observable. Defer to v1.6 unless Phase 44 strict mode needs explicit-only conversion (in which case route to Phase 44 alongside §2 explicit conversion builtins). |

### §5c Verified-OK Music-Type Overloads

`transpose(Sequence, Semitone)` + `transpose(Sequence, Cent)` — both registered, both Just Work for composers writing `(transpose seq 7)` (Int widens to Semitone) and `(transpose seq +50c)` (Cent literal). No gap.

---

## 6. Clamp & Advisory Inventory

Counts cited from `42-AUDIT-data/summary.txt`: **72 total clamp sites**, **117 advisory sites**, **110 charitable-fallback markers**, **13 input-perimeter clamps** post-classification per Pitfall 4.

### §6a Input-Perimeter Clamps (Phase 44 Axis B candidates) — 13 sites

Sites where `Math.Clamp` is applied to a direct `args[N].As<T>()` read — these silently fix composer mistakes at the API surface and become strict-mode errors under `enable strictTypes;`. Cited from `42-AUDIT-data/input-clamps.txt` (full file:line refs preserved in `AUDIT-data/`, NOT here per Pitfall 7).

| # | Builtin | Clamped Parameter | Current Behavior | Routing | Phase 44 Strict-Mode Error Proposal |
|---|---------|--------------------|-------------------|---------|--------------------------------------|
| 1 | `swing(Sequence, Double, Double)` | `strength` clamped to [0.0, 1.0] | Silent clamp; out-of-range values produce edge swing | → Phase 44 | `[strict] swing strength {value} outside [0.0, 1.0] — enable strictTypes rejects clamping` |
| 2 | `swing(Sequence, Double, Double)` | `swing` clamped to [-1.0, 1.0] | Silent clamp; ±values beyond unit produce flat swing | → Phase 44 | `[strict] swing factor {value} outside [-1.0, 1.0] — enable strictTypes rejects clamping` |
| 3 | `crescendo` (TransformFunctions:649) | `startVel` clamped to [0.0, 1.0] | Silent velocity-floor/ceiling | → Phase 44 | `[strict] crescendo startVel {value} outside [0.0, 1.0]` |
| 4 | `crescendo` (TransformFunctions:650) | `endVel` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] crescendo endVel {value} outside [0.0, 1.0]` |
| 5 | `decrescendo` (TransformFunctions:657) | `startVel` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] decrescendo startVel {value} outside [0.0, 1.0]` |
| 6 | `decrescendo` (TransformFunctions:658) | `endVel` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] decrescendo endVel {value} outside [0.0, 1.0]` |
| 7 | `swell` (TransformFunctions:666) | `edgeVel` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] swell edgeVel {value} outside [0.0, 1.0]` |
| 8 | `swell` (TransformFunctions:667) | `peakVel` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] swell peakVel {value} outside [0.0, 1.0]` |
| 9 | `humanize` (TransformFunctions:785) | `amount` clamped to [0.0, 1.0] | Silent humanization-strength clamp | → Phase 44 | `[strict] humanize amount {value} outside [0.0, 1.0]` |
| 10 | `humanizeGaussian` (TransformFunctions:821) | `amount` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] humanizeGaussian amount {value} outside [0.0, 1.0]` |
| 11 | `vary` (TransformFunctions:904) | `amount` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] vary amount {value} outside [0.0, 1.0]` |
| 12 | `legato` (TransformFunctions:960) | `amount` clamped to [0.0, 1.0] | Same | → Phase 44 | `[strict] legato amount {value} outside [0.0, 1.0]` |
| 13 | `repeat` (TransformFunctions:1106) | `reps` clamped to [1, 16] | Silent repetition-count clamp | → Phase 44 | `[strict] repeat reps {value} outside [1, 16]` |

The 59 remaining `Math.Clamp` sites in `all-clamps.txt` are output-protection clamps (algorithm intermediates protecting downstream invariants — MIDI byte ranges, RGB-style component clamps, internal DSP coefficient bounds) and are **culled** from Phase 44 Axis B per Pitfall 4. They should stay charitable under strict mode because they protect downstream consumers, not surface composer mistakes.

### §6b Advisory Sites (`WarnOnce` calls) — 117 sites grouped by stdlib module

Cited from `42-AUDIT-data/advisory-sites.txt`. Sites that BECOME strict-mode errors under `enable strictTypes;` per ROADMAP line 378.

| Module | Site Count | Representative Sentinels | Routing |
|--------|-----------:|--------------------------|---------|
| `Audio/Sfz/` (SfzBuiltins + SfzParser + SfzRenderer + SfzSampleCache) | 22 | `[sfz] seq_length > 100 clamped to 100`, `[sfz] unsupported opcode '{name}' ignored`, `[sfz] missing sample '{path}'` | → Phase 44 (HIGH) |
| `Patterns/PatternFunctions.cs` | 17 | `[every] lambda did not return Sequence`, `[chunk] n must be > 0`, `[jux] lambda result has N bars vs source M` | → Phase 44 (HIGH) — Phase 36 D-v1.5-05 charitable-by-default opts INTO strict errors here |
| `Improv/JamFunctions.cs` | 9 | `[jam] order clamped to {order}`, `[jam] unknown style — falling back to #jazz`, `[jam] no active key — using {DefaultKey}` | → Phase 44 (MEDIUM) |
| `Generative/ChaosFunctions.cs` | 9 | `[lorenz] degenerate params`, `[logistic] r clamped to [0, 4]`, `[quantizeToScale] unknown scale` | → Phase 44 (MEDIUM) |
| `Notation/AbcImport.cs` | 8 | `[abc] dropped ornament`, `[abc] unknown key — using Cmajor`, `[abc] could not parse meter — using 4/4` | → Phase 44 (MEDIUM) — D-39-17 charitable defaults flip to errors under strict |
| `Generative/MarkovFunctions.cs` | 6 | `[markov] order clamped to [1, 3]`, `[markov] empty corpus` | → Phase 44 (MEDIUM) |
| `Generative/LsystemFunctions.cs` | 6 | `[lsystem] iterations clamped to [0, 20]`, `[lsystemToSequence] mapper returned non-Note` | → Phase 44 (MEDIUM) |
| `Notation/MmlImport.cs` | 5 | `[mml] unknown opcode`, `[mml] loop depth > 16`, `[mml] FM operator routing ignored` | → Phase 44 (MEDIUM) |
| `Improv/StyleRegistry.cs` | 4 | `[improv] style pack failed to load`, `[improv] user style overrides shipped pack` | → Phase 44 (LOW) — pack-discovery advisories may stay charitable |
| `Audio/DSP/` (GranularFunctions + PitchShiftFunctions + StretchEngine + StretchFunctions) | 4 | `[granular] unknown windowing symbol — falling back to #hann`, `[stretch] mode=#auto picked N% vocoder / M% psola`, `[pitchShift] shift > 12 st advisory` | → Phase 44 (HIGH) |
| `Network/OscFunctions.cs` | 3 | `[osc] bundle nesting depth > 8`, `[osc] handler exception`, `[osc] type-tag inference fallback` | → Phase 44 (MEDIUM) |
| `Generative/CellularFunctions.cs` | 3 | `[cellular] width/height clamped to [1, 1024]`, `[life] density out of range` | → Phase 44 (MEDIUM) |
| `Audio/SampledInstrumentRenderer.cs` | 3 | `[piano] mp_mf missing — falling back to 2-way crossfade`, `[piano] release clamped` | → Phase 44 (LOW) — sample-data hygiene, not composer-surface |
| `Audio/InputFunctions.cs` | 3 | `[audio-in] mic stream attenuated -20 dB`, `[audio-in] resampling from {N} Hz`, `[audio-in] capture failed` | → Phase 44 (LOW) — environmental advisories |
| `Notation/AbcLexer.cs` | 2 | `[abc] unknown character`, `[abc] lexer error` | → Phase 44 (MEDIUM) |
| `Audio/Tuning/ScalaBuiltins.cs` | 2 | `[tuning] unmapped MIDI keys — rendered as rest`, `[tuning] malformed .scl line` | → Phase 44 (MEDIUM) |
| `Audio/SongRenderer.cs` | 2 | `[render] unknown instrument '{name}' — falling back to sine`, `[render] voice pool exhausted` | → Phase 44 (HIGH) |
| `Audio/MidiExport.cs` | 1 | `[midi] velocity floor applied` | → Phase 44 (LOW) |
| `Harmony/HarmonyFunctions.cs` | 1 | `[enharmonic] called inside tuning != equalTemperament — ≈21 cent shift` | → Phase 44 (MEDIUM) |
| `flow-lang/Interpreter/Interpreter.cs` + `Ast/Expressions/MatchExpression.cs` + `Ast/Statements/LiveBlockStatement.cs` + `Runtime/ExecutionContext.cs` + `Interpreter/ExpressionEvaluator.cs` | 7 | `[match] non-exhaustive pattern — falling through to default`, `[live] entering live block — opts OUT of two-run cmp-clean` | → Phase 44 (HIGH for `match`, LOW for `live` — `live` is design-locked per D-v1.5-07) |

### §6c Charitable Fallback Markers — 110 sites (sample-not-exhaustive)

`42-AUDIT-data/charitable-sites.txt` grepped for `fallback|charitable|else.*return.*input` — pure triage signal. Composer's "Charitable Interpretation" memory (`feedback_charitable_interpretation.md`) makes charitable the DEFAULT. Phase 44 strict mode opts INTO errors at the §6a + §6b sites enumerated above. The §6c markers are pointer-only for Phase 44 plan-phase authoring — manually triage `charitable-sites.txt` then to decide which patterns are user-surface (→ strict error) vs implementation-safety (→ stay charitable).

**Phase 44 plan-phase author:** start from §6a + §6b above. Use §6c as a discovery sweep for patterns the harness didn't see (bespoke `if (x < 0) x = 0` clamps, try/catch fallbacks). See `42-AUDIT-data/charitable-sites.txt` for the raw line-by-line list.

---

## 7. Prioritization & Phase Routing

Every finding above carries a `→ Phase 43`, `→ Phase 44`, or `→ v1.6-backlog` routing tag. Aggregated here as a decision footer for downstream phase plan-phase consumption.

### §7a Phase 43 Candidates (module/naming + new builtins)

| Priority | Finding | One-Line Rationale |
|----------|---------|---------------------|
| HIGH | `Beat → Second` + `Second → Beat` context-aware conversion builtins (`beatToSec` / `secToBeat`) | Anchor finding (§1 + §2). Closes the orphan-Beat gap. Requires tempo-context access. |
| HIGH | Beat-companion overloads for `delay(Buffer, Beat)`, `renderBarAtBeat(Sequence, Beat)`, etc. | §1 — gives composers a place to USE Beat values once `beatToSec` exists. |
| LOW | `pitchShift(Buffer, Hertz)` overload | §5a — semantically distinct from cents-relative; needs design decision before backfill (may be intentional that pitchShift is relative-only). |

### §7b Phase 44 Candidates (strict mode — Axis B sites)

**LOAD-BEARING for Phase 44 plan-phase per ROADMAP line 380.** If any are missing, Phase 44's strict contract regresses.

| Priority | Finding | Source Section |
|----------|---------|----------------|
| HIGH | Explicit-conversion builtins: `(db Double)`, `(cents Double)`, `(hz Double)`, `(ms Double)`, `(sec Double)` | §2 — also listed in ROADMAP line 372 |
| HIGH | 13 input-perimeter clamp sites flip to strict errors under `enable strictTypes;` | §6a |
| HIGH | `[sfz]` advisory sites (22) | §6b — SFZ surface is the largest advisory cluster |
| HIGH | `[patterns]` advisory sites (17) — every/chunk/jux/sometimes/degrade charitable defaults | §6b |
| HIGH | `[render]` advisory sites (`unknown instrument`, `voice pool exhausted`) | §6b |
| HIGH | `[match]` non-exhaustive pattern advisory | §6b |
| HIGH | `[dsp]` advisory sites (granular/stretch/pitchShift) | §6b |
| MEDIUM | `[jam]` / `[markov]` / `[lsystem]` / `[cellular]` generative advisory sites (24 combined) | §6b |
| MEDIUM | `[abc]` / `[mml]` notation-import advisory sites (15 combined) — D-39-17/19 charitable defaults | §6b |
| MEDIUM | `[osc]` / `[tuning]` advisory sites (5 combined) | §6b |
| MEDIUM | `Int → Semitone` explicit conversion (cosmetic; widening works today) | §2 |
| LOW | `[improv]` style-pack discovery advisories (4) — may stay charitable | §6b |
| LOW | `[audio-in]` / `[piano]` / `[midi]` environmental/sample-hygiene advisories | §6b |
| LOW | `[live]` block-entry advisory — design-locked charitable per D-v1.5-07 | §6b |
| LOW | `Float ↔ Decibel/Cent/Hertz/Ms/Sec` explicit conversions if strict mode demands Float distinct from Double | §2 |

### §7c v1.6-Backlog Candidates

| Priority | Finding | Rationale |
|----------|---------|-----------|
| MEDIUM | `readMidi(String) → Song` registry builtin | §3 — currently `flow-midi` CLI subcommand. Bring inline if composer demand surfaces. |
| MEDIUM | `readMusicXML(String) → Song` import path | §3 — D-39-04 chose ABC import for v1.5; MusicXML import deferred. |
| LOW | `writeABC(String, Song)` + `writeMML(String, Song)` emit paths | §3 — Phase 39 import-only for v1.5. |
| LOW | `Int → NoteValue` explicit conversion | §2 — cosmetic; widening works today. |
| LOW | `FunctionSignature.ReturnType` field addition | §8 Open Question 1 — would let the harness build the producer half of the type graph reflectively instead of by inspection. Audit-internal improvement, no composer-facing impact. |
| LOW | Cosmetic explicit-overload backfill for the 70+ `§5b` candidates (`abs`/`add`/`crescendo`/etc.) | §5b — works today via widening; cosmetic only. |
| LOW | Promote `scripts/StdlibAuditor` from one-shot Phase 42 deliverable to CI health check | RESEARCH §Open Question 3 — Approach A vs B. Recurring audit catches regressions. |

---

## 8. Limitations

Per RESEARCH §Open Question 1 + harness blind spots discovered during authoring:

1. **`FunctionSignature` lacks a `ReturnType` field.** The audit harness builds the **consumer** half of the type→signature graph (which signatures accept type T as a parameter) reflectively via `EnumerateSignatures` + `sig.InputTypes`. The **producer** half (which signatures return a value of type T) is inferred manually from the function name + the lambda body's `Value.X()` calls. Orphan detection of the form "type T has no producer" is therefore NOT mechanically guaranteed; only "type T has no consumer" (the §1 orphan rule) is mechanical. Fixed by adding `ReturnType` to `FunctionSignature` — routed to v1.6-backlog §7c.

2. **Reference-identity types are an explicit allowlist (D-42-01-C).** The five ref-identity types (TuningType / SfzType / MarkovModelType / LsystemModelType / OscHandleType) are encoded as a HashSet in both `scripts/StdlibAuditor/Program.cs` and `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs`. There is no clean reflective predicate distinguishing "reference-identity by design" from "forgot to override `IsCompatibleWith`". Future ref-identity types (e.g. a future `Sample` type for raw audio handles) must be added explicitly in both files plus this AUDIT.md §1 subsection.

3. **122 asymmetric pairs in `type-signature-graph.json` are mostly correct-by-design.** Every coercible type widens to `Void` because `VoidType` is the wildcard sentinel in `OverloadResolver`. Every `Int/Long/Float/Double/Number` pair is asymmetric within the numeric widening lattice (`Int → Long → Float → Double → Number`). §2 surfaces only the musically-meaningful subset.

4. **Asymmetric-pair detection (§3) is human-curated.** No reflective rule reliably distinguishes "missing pair" from "intentional one-way" (e.g., `loadScala` without `saveScala` is correct because Scala is a vendor format). The 12-row §3 table is a hand-classified subset of every verb pair in the registry. Future asymmetric pairs added to v1.6+ need manual classification.

5. **Pitfall 4 (`Math.Clamp` input-perimeter vs output-protection) is a heuristic.** The §6a 13-site list uses the rule "clamp on `args[N].As<...>()` direct read = input-perimeter". 4 bespoke clamps (`if (x < 0) x = 0` patterns) may have escaped this regex; the §6c charitable-sites file should be scanned during Phase 44 plan-phase to catch them. Estimated miss-count: <5.

6. **Cross-platform FP determinism caveat for chaos primitives (D-36-09).** The chaos-primitive advisory sites in §6b (`[lorenz]` / `[logistic]`) are documented as same-platform deterministic only. Phase 44 strict mode should preserve this caveat — a strict error on degenerate chaos params is fine, but cross-platform output divergence is NOT a strict-mode concern (it's a CLAUDE.md "Conventions" carve-out).

7. **REPL-only `inspect(Sequence)` callable (§4) is intentionally absent from `.flow` proc decls.** Phase 38 D-38-10 deliberately scopes `inspect` to the REPL `>` prompt for ASCII visualization. Future audits should recognize REPL-only callables as a legitimate consumer category (alongside parser-syntactic operators like `?`/`??`).

---

## Composer Review Sign-Off

§7 prioritization is composer-reviewable at the Plan 42-03 Task 3 checkpoint per AUDIT-08. Pending approval — see Task 3 outcome below this line on continuation-agent resume:

> *(checkpoint outcome appended here after composer review per resume-signal contract)*
