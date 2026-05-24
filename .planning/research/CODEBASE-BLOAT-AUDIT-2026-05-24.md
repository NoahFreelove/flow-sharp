# Flow Language — Codebase Bloat & Simplification Audit
**Date:** 2026-05-24
**Scope:** Read-only sweep of `flow-lang/`, `flow-lang.Tests/`, `flow-interpreter/`, `flow-cli/`, `flow-lsp/`, and stdlib `.flow` files for implementation-level duplication, vestigial wiring, test/baseline bloat, and file-level dead weight.
**Method:** Targeted greps + selective file reads. Not a reflective audit (that was Phase 42's job — see `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md`). This pass focuses on what Phase 42 explicitly did NOT cover.
**Skipped per instructions:** per-synth delegation shells, hand-rolled DSP (Fft/Psola/PhaseVocoder/Hps/PitchShiftEngine/WindowFunctions), charitable-interpretation fallbacks, music-type singletons, Pidgin reference, `.planning/`, `flow-lang/Samples/`.

C# LOC across the four projects: **58,124 lines** (`flow-lang` + `flow-interpreter` + `flow-cli` + `flow-lsp`).

---

## 1. Top-Priority Cleanups (high-confidence, low-risk)

### 1.1 `NoteSynthesizer.cs` — four oscillators reimplement helpers that already exist in `SynthUtils`
**Files:** `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:24-182`
**What's wrong:** `SineSynthesizer`, `SawSynthesizer`, `SquareSynthesizer`, and `TriangleSynthesizer` each carry a **private** `BeatsToSeconds` + `CreateSilence` (lines 48-58, 89-99, 130-140, 171-181 — identical 11-line blocks repeated 4×) and an inline oscillator loop that duplicates `SynthUtils.GenerateSine/GenerateSaw/GenerateSquare/GenerateTriangle` (lines 38-43, 78-83, 119-124, 160-165). `WavetableSynthesizer` (sibling file) and every Phase 29 sampled synth already route through `SynthUtils.CreateSilence` + `SynthUtils.BeatsToSeconds` + `SynthUtils.ToMonoBuffer`. `SynthUtils.GenerateSine` etc. are additive (`+=`) so the inline loops would need a fresh `float[]` (which they already build via `AudioBuffer.SetSample`) — net behavior matches.
**Fix shape:** Replace each private helper with the `SynthUtils.*` call; replace the per-sample inline loops with `var samples = new float[numSamples]; SynthUtils.GenerateSine(samples, frequency, amplitude, sampleRate); return SynthUtils.ToMonoBuffer(samples, sampleRate);`. Drops ~80 LOC and removes a known divergence risk (the oscillator math has to stay byte-identical to SynthUtils because `generateSine`/etc. are also surfaced as builtins that composers can call directly).

### 1.2 Two `Fixtures/` directories differing only in case — case-insensitive-FS hazard
**Files:** `flow-lang.Tests/Fixtures/` (capital F) and `flow-lang.Tests/fixtures/` (lowercase) coexist with different contents.
- `Fixtures/Phase29/`, `Fixtures/Phase37/`, `Fixtures/midi/` — referenced by `Tools/Phase29BaselineRecorder.cs:20`, `Integration/Phase29/HarmonicRichnessTests.cs:63`, etc.
- `fixtures/Phase37/`, `fixtures/scala/`, `fixtures/sfz-smoke/` — referenced by `Helpers/Phase37Fixtures.cs:45`, `Integration/Phase33/SfzBindingTests.cs:84`, `Integration/Phase32/TutorialScriptTests.cs:27`, etc.

This silently breaks on macOS HFS+ and Windows NTFS (case-insensitive by default) — anyone cloning on those filesystems gets a single merged directory and broken tests.
**Fix shape:** Pick one casing and migrate all callsites + git move. Recommend lowercase `fixtures/` (matches the larger reference count and the new Phase 32/33/37 conventions). Six C# files need their string paths updated.

### 1.3 `audio.flow` declares `createSineTone` / `createSawTone` / `createSquareTone` / `createTriangleTone` 4× each (internal proc + proc body, ×2 for the Hertz overload)
**Files:** `flow-lang/audio.flow:224-227` (internal procs) + `flow-lang/audio.flow:352-411` (proc bodies that shadow them)
**What's wrong:** Lines 224 + 227 forward-declare the C# `createSineTone(Double, Double, Double)` and `createSineTone(Double, Hertz, Double)` builtins, which produce **mono** buffers (per inline comment at line 362). Lines 352 + 365 then define user-facing `proc createSineTone` wrappers that build **stereo** buffers — and per a code comment the wrappers fully shadow the underlying C# builtin. The pattern repeats for `createSawTone`/`createSquareTone`/`createTriangleTone` (16 total declarations covering 4 builtins × 2 type-overloads × 2 declaration-styles). The internal-proc forward-decl is dead weight when the proc wrapper unconditionally intercepts the call.
**Fix shape:** Decide: either (a) drop the `internal proc` decls (and unregister the C# mono builtins) so only the stereo Flow wrappers exist, or (b) drop the Flow wrappers and have the C# builtin emit stereo directly. Either way saves 8 dead `internal proc` decls + clears the documentation confusion.

### 1.4 `composition.flow` Track convenience layer — 11 wrapper procs called by zero composers
**Files:** `flow-lang/composition.flow:70-148`
**What's wrong:** `bpm`, `voiceAt`, `voiceAtBar`, `createStereoTrack`, `createMonoTrack`, `withVoice`, `startAt`, `startAtBar`, `withGain`, `withPan`, `render`, `renderBars`, `voiceWithGain`, `voiceWithPan` — these are user-facing DAW-style fluent wrappers around the C#-registered Track API. Composer usage across `examples/**/*.flow` + `tests/**/*.flow`: **`createStereoTrack` and `renderBars` appear only in `tests/test_full_song.flow`**, all 13 other wrappers have **zero composer usage**. The underlying `createTrack`/`addVoice`/`setTrackOffset`/`setTrackGain`/`setTrackPan`/`renderTrack` C# stack (`flow-lang/StandardLibrary/Audio/Timeline.cs`, 265 LOC) is similarly only exercised by `test_full_song.flow`. The Track abstraction predates `Song`/`Section` (which became the canonical composition primitive Phase 25+).
**Fix shape:** Mark the Track layer for deprecation (or, per Flow's "pre-traction, no deprecation discipline" rule per CLAUDE.md, just remove it). `tests/test_full_song.flow` is the only consumer and can be ported to the Song/Section path. Net removal: ~265 LOC C# + 78 LOC `.flow` + the `Track`/`Voice` cross-references in `BuiltInFunctions.cs:949-985` + the `TrackType`/`createTrack`/`addVoice`/`setTrack*`/`renderTrack` builtin registrations.

### 1.5 `TimelineMap` "editor live highlighting" API — never wired to flow-lsp
**Files:** `flow-lang/Audio/TimelineMap.cs` (75 LOC), `flow-lang/StandardLibrary/Audio/SongRenderer.cs:439-540` (overload pair `RenderSongWithTimeline` + `RenderSectionWithTimeline`), parallel `TimelineMap`-flavored overloads in `BarRenderer.cs:308-360` and `SequenceRenderer.cs:127-180`.
**What's wrong:** Confirmed zero callers in `flow-lsp`, `flow-interpreter`, `flow-cli`, or `flow-lang.Tests`. The intent ("editor live highlighting") is documented but never realized. Carries non-trivial implementation cost: every renderer keeps a parallel render path that threads a `TimelineMap` instance, and `SongRenderer.RenderSongWithTimeline` is ~100 LOC of code that's never executed.
**Fix shape:** Either implement the LSP integration (out of scope for this audit) or delete `TimelineMap.cs` + the parallel overloads. Estimated removal: ~250 LOC across 4 files.

### 1.6 `Phase35/diagnostics/` baselines — orphaned after the diagnostic snapshots were moved to `Phase35/DiagnosticRendererGoldenTests.cs` golden assertions
**Files:** `flow-lang.Tests/baselines/Phase35/diagnostics/type_mismatch.txt` (214 bytes), `unknown_identifier.txt` (267 bytes), `.gitkeep`
**What's wrong:** Grep found exactly two reference hits — both in `Phase35/DiagnosticRendererGoldenTests.cs` — but the test file itself uses inline string golden assertions, not file reads. Need a human read of the golden test to confirm whether these `.txt` files are still read at runtime or are leftover scaffolding. (Low-effort verification: `grep -r "type_mismatch.txt\|unknown_identifier.txt" flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` — if no hit, they're dead.)
**Fix shape:** Confirm and delete the `Phase35/diagnostics/` baseline subtree if golden assertions are inline.

### 1.7 `bars.flow` legacy bar API — zero composer usage after note-stream literal syntax landed
**Files:** `flow-lang/bars.flow` (31 LOC, declares `createBar`/`createBarWithNote`/`createBarFromNotes`/`addNoteToBar`/`getNoteFromBar`/`barLength`/`setTimeSignature`/`getTimeSignature`)
**What's wrong:** `createBar`, `createBarWithNote`, `createBarFromNotes`, `getNoteFromBar`, `barLength`, `setTimeSignature`, `getTimeSignature` — **zero hits** in `examples/**/*.flow` and `tests/**/*.flow`. Superseded by the `| C4 D4 E4 |` note-stream literal syntax (which lowers to `BarData` directly via `NoteStreamCompiler`). `bars.flow` is still imported by `std.flow:6` so the C# registrations stay live, but the surface is unused.
**Fix shape:** Confirm none of these are reachable through other Flow library files (a `(grep -rn 'createBar\\|barLength\\|addNoteToBar\\|getNoteFromBar' flow-lang/*.flow)` will tell), then drop both `bars.flow` and the corresponding C# registrations in `BuiltInFunctions.cs` (and `flow-lang/StandardLibrary/Bars.cs` — 88 LOC). ~120 LOC removable, plus `std.flow` no longer needs `use "@bars"`.

### 1.8 `preview` builtin — registered but zero composer use
**Files:** `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:52-55`, `flow-lang/audio.flow:528+` area (`preview` proc decl)
**What's wrong:** "Low-quality mono 22050 Hz playback" path. Grep found no composer references in `examples/**/*.flow` or `tests/**/*.flow`. Adds dead branches in `PlaybackFunctions`. Low-confidence flag because this might be a future UI/REPL feature, but at 0 callers today it qualifies as vestigial.
**Fix shape:** Either document a planned use (in CLAUDE.md or REQUIREMENTS) or remove the `preview` registration + the `PreviewBuffer` body + the `internal proc preview` decl in `audio.flow`. ~30 LOC.

---

## 2. Medium-Priority Cleanups

### 2.1 `ClampSamples` thin-wrapper forwarders in PulseAudio + PlaybackFunctions
**Files:** `flow-lang/Audio/PulseAudioSimpleBackend.cs:271`, `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:374`
Two private methods that literally `=> AudioUtils.ClampSamples(samples)`. Should be inlined to direct `AudioUtils.ClampSamples()` calls at the two callsites in each file (`PulseAudioSimpleBackend.cs:97`, `PlaybackFunctions.cs:189` + `:243`). Removes 6 lines but more importantly removes a layer of indirection that's confusing on first read.

### 2.2 `ProgressionExpression` + `ProgressionCompiler` — one composer usage, no unit tests
**Files:** `flow-lang/Ast/Expressions/ProgressionExpression.cs`, `flow-lang/Runtime/ProgressionCompiler.cs` (~340 LOC combined), parser branch at `flow-lang/Parsing/Parser.cs:1369` + `:1673-1740`, lexer token at `flow-lang/Lexing/SimpleLexer.cs:908`.
The `progression | I IV V |` keyword DSL has exactly one composer-facing usage: `examples/long_demo.flow:216`. No dedicated unit tests in `flow-lang.Tests/`. Roman numeral resolution is already available via the separate `resolveNumeral` builtin and via roman-numeral-in-key-context (`I`, `ii`, `IV` literals inside a `key Cmajor {}` block).
**Verdict:** Likely vestigial — pre-Phase-28 experimental DSL that was superseded by the simpler in-stream numeral path. Needs human call on whether to (a) remove the `progression` keyword surface entirely or (b) keep + write tests + add to the showcase examples.

### 2.3 `audio.flow` convenience layer — `createBufferStereoCustom`/`createBufferMonoCustom`/`createSilenceMono`/`createClipMono`/`isMono`/`isStereo`/`buffersCompatible`/`sampleAt`/`setSampleAt`/`bufferDuration`/`fill`/`secondsToFrames`/`framesToSeconds`
**Files:** `flow-lang/audio.flow:122-211`
13 wrapper procs around the underlying buffer primitives. Composer usage: 0 in `examples/` and only 2 hits in `tests/spike/` (`fillBuffer` + `secondsToFrames` in 2 spike files). The "convenience layer" was authored speculatively pre-traction. Could be deleted with ~90 LOC removed; if kept, should be documented or moved to a v1.6 "high-level audio.flow" module.

### 2.4 `test.flow` legacy assertion library (lines 30-138) vs. Phase 35 `@test` module (lines 1-29)
**Files:** `flow-lang/test.flow:30-138`
The pre-Phase-35 pure-Flow assertion library (`assertTrue`/`assertFalse`/`assertEqual`/`assertNotEqual`/`assertLess`/`assertGreater`/`assertLessOrEqual`/`assertGreaterOrEqual`/`assertApproxEqual`/`printResult`/`notBool`/`runTest`/`summary`) has exactly one composer consumer: `tests/test_test_library.flow`. The Phase 35 `(test "name" lazy(body))` + `(assert ...)` + `(assertEq ...)` + `(assertWithinDb ...)` is the canonical surface going forward (test.flow:23-28). Either port `test_test_library.flow` to the new surface and delete the legacy half, or document the legacy half as "pre-Phase-35 BC layer" so future readers know it's vestigial.

### 2.5 `exportWav` (legacy alias) vs `writeWav` (canonical path-first form)
**Files:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:721-733`, `flow-lang/audio.flow:38-47`
Two parallel WAV-export surfaces. `writeWav(String, Buffer)` is the canonical form (path-first, matches `writeMidi`). `exportWav(Buffer, String)` is "backwards compat" per the comment. Used by ~5 test files. Per CLAUDE.md "pre-traction, no deprecation discipline" the legacy `exportWav` can be removed in a single commit and the test files migrated. Saves 2 registration sigs + 2 Flow `internal proc` decls + the `ExportWav` and `ExportWavWithBitDepth` shim methods in `FileIO.cs`.

### 2.6 `FlowFunctionSynthesizer` interior delegation
**Files:** `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:188-208`
Used at exactly one site: `flow-lang/StandardLibrary/Audio/SongRenderer.cs:109` (the `Function`-overload of `renderSong`). This is the user-lambda-synth path — legitimately one of one usage. Not bloat per se, but worth confirming the abstraction earns its keep. Could potentially be inlined into `SongRenderer.cs` as a private class. Optional cleanup.

### 2.7 `IFunctionInvoker` — interface with 1 production impl + 1 test mock
**Files:** `flow-lang/Interpreter/IFunctionInvoker.cs` (37 LOC)
Only `Interpreter` implements it in production; `NoopInvoker` in `flow-lang.Tests/Unit/ThunkTests.cs:48` is the only other. The justification (decouple `ExpressionEvaluator` from `Interpreter` to break a circular dependency) is real. Listed here for awareness — if a future refactor folds `ExpressionEvaluator` into `Interpreter` or vice-versa, this interface can go. Not actionable today.

---

## 3. Low-Confidence Flags (needs human review)

### 3.1 `OscillatorState` / `EnvelopeType` — composer-callable but rarely used outside `audio.flow` examples
`OscillatorState` and `Envelope` are primitive types with full builtin surfaces (`createOscillatorState`, `resetPhase`, `generateSine`/`Saw`/`Square`/`Triangle` over `OscillatorState`, `createAR`/`createADSR`/`applyEnvelope`). With the Phase 29 sample-based instruments + the Song/Section abstraction, modern Flow scripts rarely touch these. **Question:** Are these still the recommended low-level path for composers building custom synths, or were they superseded by the lambda-synth + Song path?

### 3.2 `composition.flow` time-conversion procs vs `audio.flow` time-conversion procs
`composition.flow:24-28` declares `beatsToFrames` + `framesToBeats`. `audio.flow:86-97` declares `secondsToFrames` + `framesToSeconds`. Phase 43 just added `beatToSec` + `secToBeat` as new C# builtins. The trio of frames/beats/seconds conversion grew across phases. **Question:** Does the project want a unified conversion module, or do these three coexist by design (frames-aware vs seconds-aware vs beats-aware composers)?

### 3.3 `RenderSongWithTimeline` vs `RenderSong` parallel renderer paths
See §1.5 above. The TimelineMap-flavored paths in `BarRenderer`/`SequenceRenderer`/`SongRenderer` could be removed if §1.5 lands, or kept if someone has a near-term LSP wiring plan. **Question:** Is anyone planning to wire this to flow-lsp in v1.6?

### 3.4 `Phase 35/diagnostics/` baselines (overlap with §1.6)
Need a focused grep on `DiagnosticRendererGoldenTests.cs` to verify whether the .txt files are referenced at all.

### 3.5 `flow-lang/improv/styles/` directory presence
Documented in CLAUDE.md as composer-editable Flow files. Listed for awareness only — not part of the audit scope, but the location is `flow-lang/improv/` not `flow-lang/StandardLibrary/Improv/` (which is the C# side). Possible source of newcomer confusion; not bloat.

---

## 4. Quantification

Rough estimate of removable code if all top-priority cleanups land:

| Area | Removable LOC | Removable files |
|------|---:|---:|
| §1.1 NoteSynthesizer dedup | ~80 | 0 |
| §1.2 Fixtures/fixtures merge | 0 (path strings only) | 0 (dir merge) |
| §1.3 audio.flow `createSineTone` quad-declarations | ~30 | 0 |
| §1.4 Track API removal | ~265 C# + ~78 Flow + ~37 registration LOC | 2 (Timeline.cs, Track.cs) |
| §1.5 TimelineMap removal | ~250 | 1 (TimelineMap.cs) |
| §1.6 Phase35 diagnostics baselines | ~5 (2 .txt files) | 2 |
| §1.7 bars.flow + Bars.cs | ~120 | 2 (bars.flow, Bars.cs) |
| §1.8 preview builtin | ~30 | 0 |
| §2.1 ClampSamples shims | ~6 | 0 |
| §2.3 audio.flow convenience layer | ~90 | 0 |
| §2.4 test.flow legacy half | ~108 | 0 (partial file) |
| §2.5 exportWav | ~50 | 0 |
| **Aggregated upper bound** | **~1,100 LOC** | **7 files** |

Test/baseline retirement candidates: 2 `.txt` baselines (§1.6) and the redundant `Fixtures/` vs `fixtures/` directory merge (§1.2).

No actual TODO/FIXME/HACK markers in source — the `TODO:`/`FIXME:` hits in `SimpleLexer.cs` and `SemanticTokensEncoder.cs` are part of the Phase 31 line-comment lead-in feature, not stale markers.

---

## 5. Anti-Findings (considered and rejected)

These looked like bloat but ARE intentional or justified — flagging here to head off rediscovery:

- **Per-synthesizer delegation shells** (`PianoSynthesizer.cs`, `BrassSynthesizer.cs`, `SaxSynthesizer.cs`, `FluteSynthesizer.cs`, `StringsSynthesizer.cs`, `BellSynthesizer.cs`): each is ~25 lines doing nothing but `new SampledInstrumentRenderer(cache, "name", hasVelocityLayers: false).Render(...)`. Per CLAUDE.md "Sample-based tonal instruments — Each synth class is ≤25-line delegation shell" this is BY DESIGN — the class-per-instrument structure preserves Phase 28 articulation routing and lets future per-instrument tweaks land without changing call sites.
- **Hand-rolled DSP** (`Fft.cs`, `Psola.cs`, `PhaseVocoder.cs`, `Hps.cs`, `PitchShiftEngine.cs`, `WindowFunctions.cs`, `GranularEngine.cs`): per CLAUDE.md the project rejected RubberBand (GPL), NWaves (abandoned), NAudio (Windows-centric). Each file has documented justification.
- **Music-type singletons** (`DecibelType.cs`, `MillisecondType.cs`, `CentType.cs`, `HertzType.cs`, etc.): the boilerplate is intentional per `CentType.cs:24-27`.
- **Pidgin nuget reference**: documented in CLAUDE.md as referenced-but-unused.
- **Two parallel projects** (`flow-lang` vs `flow-interpreter`): intentional separation per CLAUDE.md.
- **`InterpolatedStringExpression`** (initially looked like an unused AST node): wired through Parser.cs:1620 + ExpressionEvaluator.cs:866. Real feature.
- **`ForStatement` / `WhileStatement` / `BreakStatement` / `ContinueStatement`**: full parser + interpreter wiring confirmed. Real features.
- **`VocalizationFunctions` + `FormantSynthesizer` + `ConsonantSynthesizer` + `TtsHook`**: all reached via `(sing ...)` builtin used in `tests/test_vocalization.flow`. CLAUDE.md notes "vocaloid voices planned" — keep.
- **`LambdaCaptureAuditor`** (526 LOC): the static AST walker that powers Phase 38 LIVE-03 stale-closure detection. Has full Phase 38 test coverage (`StaleClosureDetectionTests.cs`). Real Phase 38 plumbing.
- **`ImplicitReturnCollector`** (used at exactly one site, `Interpreter.cs:1171`): legitimate single-purpose helper for the `proc` body implicit-return semantics. Not bloat.
- **`SfzParseException`, `ScalaParseException`, `ScalaKbmParseException`**: each thrown by exactly one parser and caught by tests. Real diagnostic types.
- **`SectionOverloadDispatch.cs`, `PatternMatcher.cs`**: Phase 35/36 features with full wiring. Real.
- **Many similar-looking `internal proc` decls in stdlib `.flow` modules**: most are required forward-decls for the C#-registered builtins (without them, `OverloadResolver` can't see the signatures). Looks like duplication but is structurally necessary — only flagged the `createSineTone`-style cases (§1.3) where the proc body fully shadows the C# implementation.

---

## Phase routing suggestion

Most of §1 is mechanical refactor — fits a future "Phase 45 — codebase consolidation" with low risk gating (each cleanup is independently verifiable via the existing test suite + two-run cmp-clean determinism contract). §1.5 (TimelineMap) and §2.2 (Progression DSL) need a product decision before removal. §1.2 (Fixtures dir merge) should land soon — it's a latent cross-platform bug.
