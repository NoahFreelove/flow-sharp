# Flow Language — Codebase Audit

**Date:** 2026-04-18
**Scope:** Read-only analysis of `flow-lang/`, `flow-interpreter/`, `tests/`, and planning docs.
**Method:** 5 parallel exploration agents covering (1) lexer/parser/interpreter/runtime/types, (2) audio subsystem, (3) stdlib/harmony/transforms, (4) test coverage, (5) feature opportunities. Findings below are synthesized from those agents — file:line references should be spot-checked before acting on any individual item, since some agent claims were speculative.

---

## 1. Critical Bugs (blocking or data-loss)

| # | Where | Issue |
|---|-------|-------|
| C1 | `Interpreter/Interpreter.cs` ~133-289 (`ExecuteMusicalContext`) | A frame is pushed early; multiple validation paths (`tempo -1`, `timesig 0/4`, bad key, etc.) `return` before reaching the pop. After the first validation error inside a context block, the musical-context stack is left unbalanced and every subsequent statement runs in the wrong scope. |
| C2 | `Interpreter/Interpreter.cs:73-74` (`ExecuteStatement`) | `_returnValue != null` short-circuits all remaining statements. If an error path sets return-like state, every following statement is silently skipped — defeats the project's "accumulate errors" promise. |
| C3 | `StandardLibrary/Audio/EnvelopeProcessor.cs:108, 120, 150, 156, 169` | `(float)i / attackFrames` etc. divides by zero when any envelope segment duration rounds to 0 frames (very short attacks/releases or very low sample rates). Crashes the renderer. |
| C4 | `StandardLibrary/Audio/BufferHelpers.cs:130, 159` | Same div-by-zero in `FadeIn`/`FadeOut` when `durationSeconds * sampleRate < 1`. |
| C5 | `StandardLibrary/Transforms/TransformFunctions.cs:248, 269` | **`augment` and `diminish` are swapped.** Augment subtracts 1 from `NoteValueType` (WHOLE=0…THIRTYSECOND=5), so quarter → eighth (shorter). Musically backwards. Any user relying on these gets the opposite of what's documented. |
| C6 | `StandardLibrary/Collections.cs` (`init`) | `init([])` returns `[]` silently rather than erroring (LINQ `Take(-1)` returns empty). Inconsistent with `head`/`last` which error on empty. May mask logic bugs in user code. |
| C7 | `Runtime/Thunk.cs:35-45` | If the deferred expression throws, `_isEvaluated` is still set and `_cachedValue` stays null. Subsequent `Force()` calls return `null!`. Silent corruption for any `lazy (...)` that fails first time. |

---

## 2. Major Bugs (user-visible but non-blocking)

### Lexer / Parser / Interpreter

- **`TypeSystem/OverloadResolver.cs:72-79`** — Ambiguity check only compares the top 2 candidates after sort. With 3+ tied candidates, the third is silently selected.
- **`TypeSystem/FunctionSignature.cs:114-149`** — No tie-breaker between "compatible" candidates of equal score (e.g., `f(Int)` vs `f(Float)` for `f(1.5)`). Should prefer narrower / closer numeric type.
- **`TypeSystem/ArrayType.cs:28-42`** — `Void[]` returns true from `IsCompatibleWith` against any array type, which leaks the parameter-wildcard semantics into assignment checking.
- **`Lexing/SimpleLexer.cs:543-564`** — Note-vs-identifier lookahead can consume suffix characters (e.g., `B4w`) as one token, breaking note-stream syntax that depends on splitting note + duration suffix.
- **`Parsing/Parser.cs:657-661`** — Flow-arrow argument detection requires same-line check, breaking multi-line flows like `arr ->\n  map (lambda)`.
- **`Parsing/Parser.NoteStream.cs:236-237`** — On unexpected token inside a stream, parser breaks but doesn't enforce hard delimiters (`}`, EOF), risking dangling consumption into the next statement.
- **`Runtime/NoteStreamCompiler.cs:447-458`** — Roman numerals with no key in scope silently render as rests (no warning). Composer thinks the section is empty.
- **`Runtime/ExecutionContext.cs:189-205`** — `GetMusicalContext` walk has an "early break when all 7 properties set" condition that is wrong for sparse stacks (top frame sets only Tempo, parent sets only Key → Key never propagates).
- **`Interpreter/Interpreter.cs:595-694`** — `_recursionDepth` decremented twice on early return paths (line 603 + finally), causing underflow over time.

### Audio

- **`StandardLibrary/Audio/FileIO.cs:54`** — `int fileSize = 36 + dataSize` can overflow for large WAVs; RIFF size field should be range-checked.
- **`StandardLibrary/Audio/FileIO.cs:336`** — WAV loader divides chunk size by sample width without checking RIFF word-alignment padding, so odd-length data chunks read into padding bytes.
- **`StandardLibrary/Audio/DSP/Delay.cs:57`** — Read-then-write order is correct in general, but for `delaySamples == 1` reads the value just written previous frame (off-by-one ⇒ pre-echo).
- **`StandardLibrary/Audio/DSP/Delay.cs:88-89`** — `Math.Log10(feedback)` blows up as feedback → 0; bound the tail length calculation.
- **`StandardLibrary/Audio/DSP/Reverb.cs:106-111`** — No denormal flush in comb-filter feedback path; can pin a CPU core after long silence.
- **`StandardLibrary/Audio/DSP/Filter.cs`** — Bandpass derives Q from `centerHz / bw` with no upper bound; tight bandwidths produce unstable poles.
- **`StandardLibrary/Audio/VoiceAllocator.cs:49`** — `GetPeakAmplitude` only inspects the first second of a voice; long pads with quiet attacks evict short loud voices incorrectly.
- **`StandardLibrary/Audio/VoiceAllocator.cs:74`** — Fade-out applied with `frame = buffer.Frames - fadeSamples + i`; for voices shorter than `fadeSamples`, the negative-index branch silently skips the fade entirely (audible click).
- **`StandardLibrary/Audio/PlaybackFunctions.cs` (~385)** — Per-voice mix only reads channel 0; stereo voices play as mono (left channel only).

### Harmony / Transforms / Stdlib

- **`StandardLibrary/Harmony/ChordParser.cs:173-175`** — Sharp notes formatted with `+` suffix (e.g. `"C4+"`) which `NoteType.Parse` does not accept. Downstream `chordNotes` consumers break on any chord with sharps.
- **`StandardLibrary/Harmony/ScaleDatabase.cs:33-42, 182`** — Key parsing is brittle around mixed case / enharmonic spellings (`Dbmajor` parses but yields a chromatic-numbered scale, not flat-spelled).
- **`StandardLibrary/Harmony/ScaleDatabase.cs:196-214`** — `GetScaleNotes` only knows `major` and `natural minor`. No modes (dorian, lydian, etc.), no harmonic/melodic minor, no pentatonic/blues/whole-tone/octatonic — though the docs claim them.
- **`StandardLibrary/Harmony/ScaleDatabase.cs:127-140`** — Roman numeral resolution ignores upper/lowercase distinction inconsistently; minor-key vs major-key chord qualities aren't selected from case as expected.
- **`StandardLibrary/Harmony/ChordParser.cs:14-34`** — Missing common qualities: `11`, `13`, `m7b5` vs half-dim disambiguation, `mmaj7`, `7sus2`, `7sus4`, `alt`, slash-chord inversions (`C/E`).
- **`StandardLibrary/Transforms/TransformFunctions.cs:728, 738, 741`** — `Trill`/`Tremolo` `MusicalNoteData` construction may be missing/misordered required parameters (CentOffset, IsTied, Articulation, IsDotted) and the duration math is suspect. **Verify against the actual `MusicalNoteData` constructor before changing.**
- **Missing built-ins referenced by tests:** `range(Int, Int)` (used in `test_custom_oscillator.flow`), `bpm()`, `createStereoTrack`, `renderBars` (used in `test_full_song.flow`). Either implement or remove from tests.
- **`break` / `continue`** parsed but not executed in the interpreter — `test_while_loop.flow` lines 37-54 will fail.

---

## 3. Minor Issues / Code Smells

- `ExpressionEvaluator.cs:135-150` — When a name resolves as neither variable nor 0-arg function, only the variable error is reported.
- `NoteStreamCompiler.cs:206-282` — Auto-fit duration silently swallows overflow when ghost/grace notes consume more beats than the bar.
- `Interpreter/Interpreter.cs:268` vs `NoteStreamCompiler.cs:454` — Coupling between "key inherited via stack walk" and "context built per frame" is fragile; works today by coincidence.
- `TypeSystem/FlowType.cs:38-42` — Default `Equals` only compares `GetType()`, ignoring generic parameters (`Lazy<Int>` == `Lazy<Void>`). `ArrayType` overrides correctly; other generic types may not.
- `EnvelopeProcessor.cs` — Final attack-frame value is `(n-1)/n`, then sustain jumps to 1.0 — single-sample step. Clamp last attack sample to 1.0.
- `MidiExport.cs:195` — Velocity floor of 1 (vs 0) means whisper-quiet notes still trigger; consider rest threshold.
- `DSP/Panner.cs:40-42` — Stereo→mono downmix before panning produces a louder center image than mono input.
- `PulseAudioSimpleBackend.cs:71` — `Marshal.PtrToStringAnsi` on a UTF-8 PulseAudio string mangles non-ASCII error text.
- `BuiltInFunctions.cs` (string→number paths) — Likely use `int.Parse` / `double.Parse` without `CultureInfo.InvariantCulture`; non-en-US locales will misparse.
- `BuiltInFunctions.cs` (`StrArray`) — No cycle detection in recursive `str()`.
- `ChordParser.cs:85-86, 126` — Bare accidental like `"Cs"` defaults silently to major; consider warning.
- `std.flow:73-76` — `?` and `??` declared but no obvious C# registration found; double-check they're wired.

---

## 4. Test Coverage Gaps

**51 tests; ~46 expected to pass today.** Coverage of audio/harmony/transforms is solid; gaps are concentrated in error paths, advanced control flow, and a few subsystems with code but no tests.

### Tests likely to fail right now
- `test_custom_oscillator.flow` — needs `range(Int, Int)`.
- `test_full_song.flow` — needs `bpm`, `createStereoTrack`, `renderBars`.
- `test_while_loop.flow:37-54` — `break`/`continue` parsed but not interpreted.

### Subsystems with code but no test
- **Polyrhythm** — `PolyrhythmFunctions.cs` exists; no `test_polyrhythm.flow`.
- **ADSR / AR envelopes** — `createADSR`, `createAR`, `applyEnvelope` registered but never invoked in tests.
- **LiveReloadManager / watch mode** — only smoke-tested via `test_live_reload.flow`.

### Edge cases that need coverage
- Type / arity / scope errors (currently only `test_error_masking.flow` and `test_musical_context_errors.flow`).
- Parser error recovery and multi-error reporting.
- Circular module imports.
- Voice-stealing behavior at `setMaxVoices` boundary.
- Out-of-range pan values; multi-channel (>2) buffers.
- WAV loader edge cases (odd-length data chunks, 24-bit, sample rate ≠ 44100).
- `init([])`, `init([x])`, `reduce` on empty list, `range` step=0/negative, `zip` on mismatched lengths.
- Ornaments + dynamics + transforms layered on the same sequence.

---

## 5. Feature Opportunities (ranked by impact × fit ÷ effort)

All proposals reuse existing infrastructure (no new dependencies) and are consistent with stated v1.x / v2 requirements.

### Tier A — Small, high-leverage
1. **Sequence slicing & phrase-edit** (`slice(seq, start, end)`, `loopEdit(...)`) — S — `BuiltInFunctions.cs`, `audio.flow`. Fills an obvious composition-workflow gap.
2. **Note-name aliases & enharmonic helpers** (`H` = `B`, `Db` ↔ `C#`, `enharmonic()`) — S — `Lexing/SimpleLexer.cs`, `Parsing/Parser.cs`, `PitchConversion.cs`. Pedagogical and non-breaking.
3. **Per-voice reverb-time context** (`reverbTime { ... }` block) — S — `Runtime/MusicalContext.cs`, `Audio/DSP/Reverb.cs`. Mirrors existing `gain`/`pan` context pattern.
4. **MIDI velocity from dynamic transforms** (preserve `crescendo`/`decrescendo`/`swell` envelope into MIDI velocities) — S — `Audio/MidiExport.cs`, `SequenceRenderer.cs`. Closes the dynamics→export loop.
5. **Euclidean swing/humanize parameters** — S — `BuiltInFunctions.cs (euclidean)`. Modern beat design with one flag.

### Tier B — Medium, strong fit
6. **Arpeggio parameterization** (direction, speed, octave spread) — M — `Harmony/HarmonyFunctions.cs`. Backwards-compatible overload.
7. **Chord inversion & voicing control** (`I^1`, range constraints) — M — `Parsing/ProgressionCompiler.cs`, `HarmonyFunctions.cs`. Extends shipped progression DSL.
8. **Delay sync to note values + feedback control** (`delay { synced eighth feedback 0.6 }`) — M — `Runtime/MusicalContext.cs`, `Audio/DSP/Delay.cs`. Producer staple.
9. **Scale linting** (`checkScale()` warns on out-of-key notes) — M — `HarmonyFunctions.cs`, `ErrorReporter.cs`. Pure DX win.
10. **Legato / portamento** (`legato { ... }`, `portamento(duration)` transform) — M — `MusicalContext.cs`, `Synthesizers/Brass.cs`, `Synthesizers/Strings.cs`. Realistic monophonic phrasing.
11. **Snap-to-grid / `alignGrid`** — M — `NoteStreamCompiler.cs`, `Transforms/`. Live-reload polish.
12. **Microtonal / just-intonation ratios** (`C4:5/4`) — M — `Lexing/SimpleLexer.cs`, `Parsing/Parser.cs`, `PitchConversion.cs`. Niche but unique.
13. **WAV pitch-shift on load** (`loadWav("…", targetPitch)`) — M — `Audio/NoteSynthesizer.cs`, `Audio/FileIO.cs`. Even a basic resampling shim has value.

### Tier C — Larger investments
14. **Buffer feedback chains / sample-delay graphs** — L — `Runtime/ExecutionContext.cs`, `Audio/SongRenderer.cs`. Cycle detection + render-time guards required.
15. **Spectral / FFT visualization in REPL** — L — new `Audio/Visualization.cs`. Ambitious; prototype with peak-detection per voice first.

### Explicitly rejected
- **Cross-fade transform** — duplicated by `gain { ... }` + `crescendo`/`decrescendo`.
- **User-defined custom scales** — requires redesign of chord/numeral resolution; defer to v2+.
- **Real-time MIDI controller input** — out of scope (live-performance category, EXT-03 v2).

---

## 6. Recommended Next Phase

**Phase: "Stability & Correctness" (1 week)**

Bundle the critical bugs that have outsized impact relative to fix size. None are ambiguous and all have small blast radius:

- C1, C2 — fix musical-context frame leak and statement-skip-after-error in `Interpreter.cs`.
- C3, C4 — guard the envelope / fade divisions (`Math.Max(1, frames)`).
- C5 — swap `augment` / `diminish` semantics and add a regression test (rename will break user code; document the change loudly).
- C6 — decide `init([])` semantics (recommend: error, matching `head`/`last`).
- C7 — make `Thunk.Force` either re-raise or cache the exception.

Plus the test-suite-unblocking trio so the green-bar actually means something:
- Implement `range(Int, Int)`.
- Implement `break` / `continue` in the loop interpreter.
- Either implement `bpm`/`createStereoTrack`/`renderBars` or trim them from `test_full_song.flow`.

**Follow-on Phase: "Composer DX" (1-2 weeks)** — Tier A features 1-5 above. All small, all reuse existing infrastructure, all immediately visible to anyone using the tool. Together they round out the dynamics/velocity loop, the context-block pattern, and the editing workflow without expanding the language surface in any direction the project hasn't already committed to.

Defer Tier B/C until the stability phase lands and tests are green again.
