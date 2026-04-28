# Stack Research — Flow v1.3 Composer DX Tier B/C

**Domain:** Brownfield language-runtime + music DSL features (additive milestone)
**Researched:** 2026-04-26
**Confidence:** HIGH (existing stack documented in PROJECT.md / CLAUDE.md / csproj inspection; new feature math verified against existing primitives)

## TL;DR

**Zero new external dependencies are warranted for v1.3.** Every Tier B/C feature plus the six DEFER closures plus tuplets/arbitrary-fraction durations is a hand-roll candidate that fits cleanly inside Flow's existing pipeline (lexer → parser → interpreter → renderer → DryWetMidi/PulseAudio sink). The strict "minimal dependencies" stance from PROJECT.md remains the right answer for v1.3.

The only borderline cases — rational arithmetic for tuplet duration math, and pitch-shift on WAV load — both have hand-roll implementations that are simpler than integrating an external library given Flow's existing primitives (`MusicalNoteData.DurationValue` is already an `int` enum, `PitchConversion.NoteToFrequency` already does the cents→frequency math via `Math.Pow(2, x/12)`).

This document maps each v1.3 feature to its existing-stack integration point and flags the two places where a "look again" decision is justified later.

## Recommended Stack — v1.3 Delta

### Core Technologies (Existing — No Changes)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET 10 | net10.0 | Runtime | Already locked in `flow-lang.csproj`; C# 13 record types + pattern matching already used pervasively |
| C# 13 | Latest | Language | File-scoped namespaces, switch expressions, records — already idiomatic in flow-lang |
| Melanchall.DryWetMidi | 8.0.3 | MIDI file write/read | Already integrated for v1.2 velocity regression (`Audio/MidiExport.cs`); covers MIDI output for tuplets and microtonal export via per-channel pitch-bend |
| PulseAudio (P/Invoke) | System | Real-time playback | Already in `Audio/PulseAudioSimpleBackend.cs`; `IAudioBackend` abstraction unchanged |
| OmniSharp.Extensions.LanguageServer | 0.19.9 | LSP host (flow-lsp) | Already shipped in v1.2; scale-linting plugs in as a new diagnostic provider, no version bump needed |

### Supporting Libraries (NEW — None Recommended)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| *(none)* | — | — | All v1.3 features fit existing primitives |

### Development Tools (Existing)

| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet build` / `dotnet run --project flow-interpreter` | Build & test loop | `.flow` test scripts continue to act as the regression suite |
| flow-lsp + VSCode extension | Author-time validation | Add scale-lint diagnostic; no new tools |

## Per-Feature Stack Decisions

For every v1.3 feature: which file/module owns the change, whether existing stack suffices, and what (if anything) would justify a new dependency.

### 1. Tuplets — `(3:2 C4 D4 E4)q` syntax

**Owner:** `Lexing/SimpleLexer.cs` + `Parsing/Parser.cs` + `Ast/Expressions/NoteStreamExpression.cs` + `Runtime/NoteStreamCompiler.cs`

**Existing stack covers:** YES.
- Lexer already emits paren-prefixed groups (`(? ...)`, `(?? ...)`, `(ghost ...)`, `(grace ...)`); add a `TUPLET_OPEN` form that recognizes `(N:M ...)`.
- Parser already has a `NoteStreamElement` discriminated-union pattern (`NoteElement`, `RestElement`, `ChordElement`, `RandomChoiceElement`, `GhostNoteElement`, `GraceNoteElement`); add `TupletElement(int Numerator, int Denominator, IReadOnlyList<NoteStreamElement> Inner, string? DurationSuffix, bool IsDotted)`.
- `NoteStreamCompiler.CompileBar` already auto-fits durations against `TimeSignatureData` — extend `CalculateAutoFitDuration` to multiply tuplet inner durations by `Denominator/Numerator`.

**No external library.** Tuplet ratios are small integers (2:3, 3:2, 5:4, 7:8) — no arbitrary-precision rational math is required. See "Rational Arithmetic" below for the explicit rationale.

### 2. Arbitrary fractional note durations — `C4/12`, `C4/5`

**Owner:** `Lexing/SimpleLexer.cs` (duration-suffix tokenizer) + `Runtime/NoteStreamCompiler.cs` + new `MusicalNoteData.DurationValue` representation

**Existing stack covers:** YES, with one schema decision to make.
- Today `MusicalNoteData.DurationValue` is `int?` interpreted as a `NoteValueType.Value` enum (WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4, THIRTYSECOND=5).
- Adding fractional durations requires either (a) extending the enum to encode `1/N` for arbitrary N, or (b) replacing `int?` with a new struct `NoteDuration(int Numerator, int Denominator)` while keeping the enum-int convention as a fast path.
- The renderer (`SequenceRenderer`, `BarRenderer`, `Audio/MidiExport.cs`) ultimately converts duration to seconds via `60/BPM × beatsPerNote` — that math is identical for `1/4` and `1/12`; only the source-of-truth representation needs widening.

**No external library.** Standard integer division + GCD for normalization; ~30 lines of C#.

### 3. DEFER-01 — `range(Int, Int) → Array[Int]`

**Owner:** `StandardLibrary/BuiltInFunctions.cs` + `StandardLibrary/InternalFunctionRegistry.cs`

**Existing stack covers:** YES — pure stdlib registration. Use `Enumerable.Range(start, count).ToList()` wrapped in `Value.FromArray` against `ArrayType(IntType.Instance)`. Zero new dependencies.

### 4. DEFER-02/03 — pragma system + `enable` keyword for `H` alias

**Owner:** `Lexing/SimpleLexer.cs` (top-of-file pragma scan) + `Parsing/Parser.cs` (`enable H_AS_B` statement) + `Runtime/ExecutionContext.cs` (pragma flag set on the module's stack frame)

**Existing stack covers:** YES.
- Pragma syntax is a parser-level feature; no runtime cost, no external lib.
- Note-stream lexing is already a custom path (it already distinguishes flat-letter `Bb4` from regular `B4`); adding a single conditional remap `H` → `B` when the pragma flag is on is trivial.
- Mirror precedent: existing `swing 0.6 { ... }` block pushes flag-state on `MusicalContext` stack — pragma can use the same scoping primitive (or be module-scoped, lighter).

**No external library.**

### 5. DEFER-04 — Multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)

**Owner:** `TypeSystem/SpecialTypes/NoteType.cs` (Parse) + `StandardLibrary/Harmony/ChordParser.cs` (formatting) + `enharmonic()` builtin

**Existing stack covers:** YES — pure note-name table extension. v1.2 Phase 14 already shipped `Db4/Eb4/Gb4/Ab4/Bb4/Cb4/Fb4` Parse + lexer dispatch reorder; this DEFER closes the inverse direction (E# = F natural, B# = C natural one octave up, Fb = E natural, Cb = B natural one octave down). Add the four edge-case rows to the existing alteration table; ~12 lines.

**No external library.**

### 6. DEFER-05 — Slice negative-from-end indexing

**Owner:** `StandardLibrary/BuiltInFunctions.cs` `slice()` (already exists for `Sequence` and `Array[T]`)

**Existing stack covers:** YES. Negative indexing is a lambda over Length: `if (idx < 0) idx = length + idx`. Existing slice already does silent two-sided clamping (per v1.2 charitable-interpretation pattern); adding negative-from-end is a one-line wrap before clamping.

**No external library.**

### 7. DEFER-06 — Gaussian humanize distribution

**Owner:** `StandardLibrary/BuiltInFunctions.cs` `humanize()` overload + RNG path through `ExecutionContext.GetRand`

**Existing stack covers:** YES.
- `Random.Shared.NextSingle()` (uniform) already used; Gaussian is the **Box–Muller transform**: `z = sqrt(-2 ln u1) × cos(2π u2)`. Two uniform draws → one Gaussian sample. Standard textbook ~6 lines.
- Determinism contract from v1.2 is preserved because the RNG is reseeded at `renderSong/writeWav` boundaries — Box–Muller consumes pairs of uniforms deterministically.

**No external library.** `MathNet.Numerics` would provide `Normal.Sample` but is overkill for a single distribution and contradicts the minimal-deps stance.

### 8. Arpeggio parameters (rate, direction, pattern)

**Owner:** `StandardLibrary/Harmony/HarmonyFunctions.cs` `arpeggio()` overload

**Existing stack covers:** YES. Existing `arpeggio(Chord) → Sequence` is upgraded to `arpeggio(Chord, NoteValue rate, ArpeggioDirection dir, ArpeggioPattern pattern) → Sequence` — pure data manipulation over `ChordData.NoteNames` (reverse for `down`, alternate-then-shift for `updown`, reorder by interval-class for patterns like `1-3-5-7`/`1-5-3-7`). New enum types fit in `TypeSystem/SpecialTypes/`.

**No external library.**

### 9. Chord inversions / voicings

**Owner:** `StandardLibrary/Harmony/ChordParser.cs` + `StandardLibrary/Harmony/HarmonyFunctions.cs`

**Existing stack covers:** YES. `ChordData.NoteNames` is already an ordered list. Inversion = octave-up the bottom N notes (`invert(Chord, Int n)`), close-voicing = collapse all notes into a one-octave window, drop-2 / drop-3 = octave-down the 2nd/3rd-from-top voice. All operations are integer arithmetic on octave numbers + reorder.

**No external library.**

### 10. Delay sync to note values (`delay(buf, 1/8)` vs `delay(buf, 250ms)`)

**Owner:** `StandardLibrary/Audio/DSP/Delay.cs` + new overload `delay(Buffer, NoteValue, Double feedback)` that reads `MusicalContext.Tempo`

**Existing stack covers:** YES. `Delay.cs` already takes ms; new overload converts `NoteValue` × tempo to ms via `(60.0 / bpm) × beatsPerNote × 1000`. The same musical-context stack (`MusicalContext.Tempo`) used by `tempoRamp` and `renderSong` is the source of truth.

**No external library.**

### 11. Microtonal ratios — just intonation, custom temperaments

**Owner:** `Runtime/MusicalContext.cs` (new `Tuning` field) + `StandardLibrary/Audio/PitchConversion.cs` (replace `NoteToFrequency` with tuning-aware version) + new `tuning { ... }` musical-context block

**Existing stack covers:** YES.
- `PitchConversion.NoteToFrequency` currently hard-codes 12-TET via `440 × 2^((midi-69)/12)`. Generalize to `referenceFreq × tuningTable[scaleDegree] × 2^octaveOffset`, where `tuningTable` is a `double[N]` of frequency ratios (just intonation: `{1, 16/15, 9/8, 6/5, 5/4, 4/3, 45/32, 3/2, 8/5, 5/3, 9/5, 15/8}`; Pythagorean / meantone / user-supplied: same shape).
- The `CentType` already exists for cent offsets — reuse for per-note microtonal nudges. A custom temperament is **N cent values** keyed off the tonic from `MusicalContext.Key`. Pure C# math; no library.
- **Scala (.scl) file format support** (optional, post-v1.3): plain ASCII text, one tuning per file, lines = ratios (`3/2`) or cents (`701.955`), comments start with `!`. ~50 lines of parser code; no library exists for C# anyway (only C++ libscala-file). Hand-rolling is the correct call given the format's simplicity and the Huygens-Fokker spec.

**No external library.** The existing `Cent` and `Semitone` types plus `Math.Pow` cover everything microtonal Flow has expressed interest in.

### 12. Scale linting (compile-time warning for out-of-key notes)

**Owner:** `flow-lang/Core/ErrorReporter.cs` (new `Diagnostic` severity = warning) + `flow-lsp` diagnostic publisher

**Existing stack covers:** YES.
- `ScaleDatabase.cs` already enumerates the active key's diatonic pitch-classes (used for roman-numeral resolution).
- `NoteStreamCompiler` already walks every `NoteElement` with full key context.
- Add a single pass after note-stream compilation: for each non-rest note, check `ScaleDatabase.IsInKey(note, MusicalContext.Key)` — if not, emit a `Diagnostic(Severity.Warning, "Note {X} is outside key {Y}", sourceLocation)`.
- LSP plumbing is identical to existing diagnostics in flow-lsp (publishes via `TextDocumentPublishDiagnostics` already wired to `ErrorReporter`). The `OmniSharp.Extensions.LanguageServer 0.19.9` package already shipped in v1.2 has full diagnostic-severity support — no version bump.

**No external library.** This is purely an additional pass over data the compiler already has.

### 13. Legato / portamento articulations

**Owner:** `Ast/Expressions/NoteStreamExpression.cs` (`Articulation` enum already has `Normal`, `Accent`, `Marcato`, `Sforzando`, `Staccato` — add `Legato`, `Portamento`) + `StandardLibrary/Audio/SequenceRenderer.cs` (envelope/release-overlap behavior) + per-instrument synthesizer release behavior

**Existing stack covers:** YES.
- Legato = note-overlap rendering: extend each note's release into the next note's attack window. `EnvelopeProcessor.cs` already supports adjustable ADSR per-note; legato is "release time → 0, attack overlaps previous". Pure parameter tweak in render path.
- Portamento = pitch-glide between notes: linear (or exponential) ramp of the carrier frequency over the first 10–30ms of the second note. Existing per-sample synth loop (`PianoSynthesizer`, `BrassSynthesizer`, etc.) already computes frequency per-sample — add a one-pole pitch lerp at note boundary.

**No external library.**

### 14. Snap-to-grid quantize

**Owner:** New `StandardLibrary/Transforms/QuantizeFunctions.cs` + `quantize(Sequence, NoteValue grid) → Sequence`

**Existing stack covers:** YES. Existing transforms (`transpose`, `invert`, `retrograde`, `augment`, `diminish`) all walk `SequenceData.Bars[].Notes[]` and produce a new sequence — `quantize` is the same shape. Round each `note.StartBeat` to nearest multiple of `grid` (in beats) using `Math.Round`. Strength parameter (0..1) lerps between original and snapped position.

**No external library.**

### 15. WAV pitch-shift on load — sample-rate conversion + pitch transposition

**Owner:** `StandardLibrary/Audio/FileIO.cs` (`loadWav` exists since v1.0 Phase 2) + new helper in `Audio/`

**Existing stack covers:** YES — but this is the only feature where a library deserves a serious second look. See "Pitch-Shift Decision" below.

Recommended hand-roll path:
- **Sample-rate conversion**: linear interpolation (or windowed-sinc with a precomputed Lanczos kernel — ~40 lines) between source-rate samples and target-rate sample positions. Quality is sufficient for a music DSL where users are loading short samples (drum hits, vocal stabs), not mastering audio.
- **Pitch-shift via resample-then-stretch**: shift pitch by reading the buffer faster/slower (factor = `2^(semitones/12)`), then time-stretch back to original length using overlap-add (OLA) with a Hann window — classic 200-line implementation, no library required.
- **Combined `loadWav(path, semitones)` overload**: do the resample-and-stretch in one pass; no intermediate temp buffer.

**Why not SoundTouch.Net 2.3.2:** It's LGPL — even though static linking with .NET assemblies is a less-clear-cut LGPL concern than native code, it conflicts with Flow's existing all-permissive dependency stance (Pidgin: MIT, DryWetMidi: MIT, OmniSharp.Extensions.LanguageServer: MIT). Adding LGPL also forecloses future commercial relicensing without notice. Hand-rolled OLA is well within the project's demonstrated DSP capability (existing `Reverb.cs`, `Compressor.cs`, `Delay.cs` are all hand-written).

**Why not NAudio's WDL resampler:** NAudio is Windows-centric; pulling it in for one resampling routine drags in a Windows-targeted dependency surface that conflicts with the existing PulseAudio Linux-first stance and the `IAudioBackend` abstraction philosophy.

**No external library** — but mark this feature for re-evaluation if user feedback shows audible artifacts in production-grade pitch shifts.

## Rational Arithmetic for Tuplet / Fraction Math

**Question:** `BigRational`, `Fractions 8.3.2`, `Rationals 2.3.0`, hand-rolled, or built-in?

**Answer: Hand-rolled struct.**

```csharp
public readonly record struct NoteFraction(int Numerator, int Denominator)
{
    public static NoteFraction Reduce(int n, int d) { /* Euclid GCD */ }
    public static NoteFraction operator *(NoteFraction a, NoteFraction b) =>
        Reduce(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    public double ToDouble() => (double)Numerator / Denominator;
}
```

**Why a library is overkill:**
1. **Tuplet ratios are small integers.** A 7:8 quintuplet inside a 64th-note triplet inside a 4/4 bar produces denominators in the low thousands at worst — `int` (or `long` for paranoia) handles this without overflow concerns. `BigInteger`-backed rationals (Fractions 8.3.2, Rationals 2.3.0, ExtendedNumerics.BigRational) are arbitrary-precision; we don't need that.
2. **Renderer ultimately converts to `double` seconds.** `MusicalNoteData.DurationValue` flows into `60.0 / bpm × beatsPerNote × sampleRate` to compute sample counts — at that point we're in `double` anyway. Carrying exact rationals through the entire pipeline gains nothing audible.
3. **Existing convention.** `MusicalContext.TimeSignature.Numerator`/`Denominator` are already `int` pairs — a `(int, int)` record is the natural extension and keeps the interpreter's value model homogeneous.
4. **Minimal-deps stance.** Adding `Fractions` for ~3 operations (×, ÷, equality) when the GCD-based reduce is one Euclid loop violates the explicit "all other features: hand-rolled" rule from PROJECT.md.

**When to revisit:** If/when Flow grows a polyrhythm-vs-polymetric DSL with deeply nested compound tuplets producing pathological denominators, reassess. Not v1.3.

## Microtonal Tuning APIs

**Question:** External tuning libraries vs hand-rolled cents-to-frequency math?

**Answer: Hand-rolled, leveraging existing `CentType` and `PitchConversion`.**

**Why a library is overkill:**
1. **`PitchConversion.NoteToFrequency` already does the math.** It's six lines: MIDI-note + alteration → `440 × 2^((midi - 69)/12)`. Generalizing to `freq = referenceFreq × tuningRatio[scaleDegree] × 2^octaveOffset` is two extra fields and a table lookup.
2. **`CentType` already parses `+50c`/`-25c` literals.** Cents-to-ratio is `2^(cents/1200)` — one `Math.Pow` call. The existing pipeline that handles `C4+50c` already routes the cent offset through to the synthesizer; tuning tables piggyback on this exact path.
3. **No mainstream C# tuning library exists.** Searches for "Scala .scl C# library" surface only C++ implementations (libscala-file). The closest .NET-adjacent option is hand-rolling against the published Huygens-Fokker spec — which is so simple (text format, one ratio or cents value per line, `!` comments) that even a "library" version would be ~50 lines.
4. **MIDI export already supports microtonality.** DryWetMidi 8.0.3 (already integrated) handles per-channel pitch-bend events, which is the standard MIDI mechanism for non-12-TET pitches. No additional package needed for round-trip microtonal MIDI.

**When to revisit:** If Flow ever wants AnaMark `.tun` (binary) or MTS-ESP runtime tuning protocol support, those are heavier formats and might justify a library. v1.3 needs neither.

## Pitch-Shift / Sample Rate Conversion

**Question:** Resampling library vs hand-rolled SoundTouch-like?

**Answer: Hand-rolled — but flag for re-evaluation post-v1.3.**

| Option | Verdict | Rationale |
|--------|---------|-----------|
| SoundTouch.Net 2.3.2 | **NO** | LGPL — license inconsistent with existing all-permissive deps; conflicts with implicit "no commercial-foreclosing license" stance |
| NAudio WDL resampler | **NO** | Windows-centric, pulls in a Windows-targeted dependency surface; contradicts Linux-first PulseAudio stance |
| r8brain-free-src | **NO** | C++ only; would require P/Invoke wrapper; complexity > benefit for the audio quality target |
| NWaves | **NO** | Already explicitly NOT recommended by PROJECT.md (abandoned since 2021, would create parallel DSP stack) |
| Hand-rolled linear interpolation + OLA | **YES** | ~200 LOC; matches existing DSP-authoring style (Reverb.cs, Compressor.cs are similar in scope); good-enough quality for a music DSL's sample-load use case |

**Quality budget:** For a music DSL where users load drum samples / vocal stabs / instrument hits and pitch-shift them by ±12 semitones, linear interpolation produces audible artifacts only on extreme stretches; windowed-sinc with a Hann-windowed Lanczos kernel (precomputed table of 64 taps) closes that gap. Both are textbook DSP that fits in `StandardLibrary/Audio/DSP/`.

**Re-evaluation trigger:** If post-v1.3 user feedback specifically calls out audible artifacts in pitch-shifted samples — *and only then* — re-evaluate against SoundTouch.Net (with explicit license review) or a hand-rolled phase vocoder.

## Scale-Linting / Static-Analysis Infrastructure

**Question:** Does flow-lsp already have what's needed?

**Answer: YES — full plumbing exists.**

What v1.2 already shipped (per MILESTONES.md Phase 17):
- `flow-lsp` project references `flow-lang` directly (no shadow language model)
- Live diagnostics via `OmniSharp.Extensions.LanguageServer` 0.19.9
- `ErrorReporter` with severity levels
- Per-platform self-contained VSIX with bundled stdlib
- Roman-numeral context inside note streams (proves that the LSP already understands the active musical context — exactly what scale-linting needs)

Adding scale linting is therefore **one new diagnostic publisher** that:
1. Walks the AST after parse.
2. For each `NoteStreamExpression`, traverses bars × elements.
3. Looks up the active `Key` from the surrounding `MusicalContextStatement` (or the nearest enclosing one — the LSP already does this for roman numerals).
4. Calls `ScaleDatabase.IsInKey(noteName, octave, alteration, key)` (a new method, ~10 lines, on existing class).
5. Emits `Diagnostic(Warning, "Note D# is outside Cmajor", sourceLocation)` for each non-conforming note.

`MusicalNoteData` already has `SourceLocation` and `SourceLength` (per `NoteStreamCompiler.cs:124-135`) — diagnostic squiggles render correctly out of the box.

**No external library, no LSP version bump, no VSIX rebuild infrastructure changes.**

## Installation

```bash
# No new packages — existing flow-lang.csproj is sufficient:
#   <PackageReference Include="Melanchall.DryWetMidi" Version="8.0.3" />
#   <PackageReference Include="Pidgin" Version="3.5.1" />        # still unused; candidate for removal
# flow-lsp.csproj likewise unchanged:
#   <PackageReference Include="OmniSharp.Extensions.LanguageServer" Version="0.19.9" />
```

**Optional housekeeping during v1.3:** Pidgin 3.5.1 is still referenced but unused (per PROJECT.md "Libraries Explicitly NOT Recommended" and the deferred candidates list). If a quick task is desired, dropping it is one csproj edit + a `dotnet restore`. Not required by any v1.3 feature, but the milestone is a natural moment to do it.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Hand-rolled `NoteFraction(int, int)` struct | `Fractions 8.3.2` NuGet | Only if Flow grows arbitrary-precision tuplet math (not v1.3 scope) |
| Hand-rolled tuning table in `PitchConversion` | `MathNet.Numerics` + a tuning library | If Flow ever needs spectral analysis / FFT (separate problem) |
| Hand-rolled OLA pitch-shift | `SoundTouch.Net 2.3.2` | Only after explicit license review and audible-artifact user feedback |
| Hand-rolled linear/sinc resampler | `NAudio` WDL | Never — NAudio's Windows-centric dependency surface is wrong for Flow |
| Existing `OmniSharp.Extensions.LanguageServer 0.19.9` | Roslyn-based analyzers | Roslyn analyzers target C# source — irrelevant for `.flow` files |
| Hand-rolled Box–Muller for Gaussian humanize | `MathNet.Numerics.Distributions.Normal.Sample` | If Flow grows ≥3 distributions (Poisson, Pareto, etc.) — not v1.3 |
| Hand-rolled `.scl` parser (post-v1.3, optional) | None on NuGet | n/a — no C# .scl library exists; hand-roll is the only option |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `SoundTouch.Net 2.3.2` | LGPL; inconsistent with existing all-permissive deps | Hand-rolled OLA pitch-shift in `Audio/DSP/` |
| `NAudio` (any version) | Windows-centric; would pull in WASAPI/MME COM dependencies | Existing PulseAudio backend + hand-rolled resampler |
| `NWaves` (any version) | Abandoned since Oct 2021 (per PROJECT.md); parallel DSP stack | Existing hand-rolled DSP in `Audio/DSP/` |
| `Fractions 8.3.2` for tuplet math | Arbitrary-precision overkill; `int`-pair denominators max out in low thousands | Hand-rolled `NoteFraction(int, int)` record struct |
| `MathNet.Numerics` for one Gaussian | One distribution doesn't justify a 5MB+ scientific-computing dep | Box–Muller transform (~6 lines) |
| `managed-midi` | Marked "past project" on GitHub | DryWetMidi 8.0.3 (already integrated) |
| `Pidgin 3.5.1` | Referenced in csproj but unused by hand-written parser | Remove during v1.3 housekeeping (optional) |

## Stack Patterns by Variant

**If a v1.3 feature needs to walk the AST (tuplets, scale linting, quantize):**
- Use the existing `NoteStreamElement` discriminated union and the `switch (element)` dispatch pattern in `NoteStreamCompiler.CompileBar`.
- Add new element types as records in `Ast/Expressions/`.

**If a v1.3 feature needs to read musical context (delay sync, microtonal, scale lint):**
- Read from `MusicalContext.Tempo / TimeSignature / Key / Tuning` in the compiler/renderer.
- Push/pop on the context stack via existing `ExecuteMusicalContext` pattern (matches v1.1 audit-fix pattern).

**If a v1.3 feature needs to emit diagnostics (scale linting):**
- Use `ErrorReporter.Add(Diagnostic)` with severity = warning.
- LSP picks it up automatically via the existing publish pipeline.

**If a v1.3 feature needs RNG (Gaussian humanize):**
- Route through `ExecutionContext.GetRand(isSeeded)` — preserves the v1.2 byte-identical determinism contract.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Melanchall.DryWetMidi 8.0.3 | net10.0 | Targets .NET Standard 2.0 — works on net10.0; v9.0.0-prerelease1 exists but not needed for v1.3 features |
| OmniSharp.Extensions.LanguageServer 0.19.9 | net10.0 | Reflection-heavy — trimming must stay disabled (per flow-lsp.csproj comment) |
| Pidgin 3.5.1 | net10.0 (unused) | Candidate for removal during v1.3 — already flagged in PROJECT.md deferred list |

## Confidence Assessment

| Decision Area | Confidence | Reason |
|---------------|-----------|--------|
| No new deps for tuplets / fractions | HIGH | Existing `NoteValueType` + parser dispatch shown in `NoteStreamCompiler.cs`; tuplet math is small-integer GCD |
| No new deps for microtonal | HIGH | `PitchConversion.NoteToFrequency` already does `Math.Pow(2, x/12)`; CentType already exists; no C# Scala library exists anyway |
| No new deps for scale lint | HIGH | flow-lsp 0.19.9 already has full diagnostic plumbing; ScaleDatabase already enumerates key pitches |
| Hand-roll over SoundTouch | MEDIUM | License + dep-philosophy match well; quality budget for a music DSL's sample-load is met by linear/sinc resample, but real audio quality ultimately depends on user feedback — flagged for re-evaluation |
| Hand-roll Gaussian humanize | HIGH | Box–Muller is textbook; no library needed for one distribution |
| DEFER-01..06 are pure stdlib | HIGH | All map to existing builtin-registration or NoteType/Parser extensions |

## Sources

- /home/noah/Desktop/projects/flow-sharp/.planning/PROJECT.md — minimal-deps stance, "Libraries Explicitly NOT Recommended" table, current package list
- /home/noah/Desktop/projects/flow-sharp/.planning/MILESTONES.md — v1.2 close inventory (LSP, DryWetMidi, Schroeder reverb, determinism contract, DEFER-01..06)
- /home/noah/Desktop/projects/flow-sharp/CLAUDE.md — architecture map, file ownership for each module
- /home/noah/Desktop/projects/flow-sharp/flow-lang/flow-lang.csproj — current packages: DryWetMidi 8.0.3, Pidgin 3.5.1; net10.0
- /home/noah/Desktop/projects/flow-sharp/flow-lsp/flow-lsp.csproj — OmniSharp.Extensions.LanguageServer 0.19.9; net10.0
- /home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/NoteStreamCompiler.cs — existing element-dispatch pattern; auto-fit duration calculation; SourceLocation propagation
- /home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Audio/PitchConversion.cs — existing 12-TET frequency conversion; generalization point for microtonal tuning
- /home/noah/Desktop/projects/flow-sharp/flow-lang/TypeSystem/SpecialTypes/CentType.cs — existing cent literal parser
- /home/noah/Desktop/projects/flow-sharp/flow-lang/TypeSystem/SpecialTypes/NoteValueType.cs — current power-of-2 enum representation; extension point for fractional durations
- [Fractions 8.3.2 on NuGet](https://www.nuget.org/packages/fractions/) — verified current version (Apr 2026); HIGH confidence: not needed for v1.3 scope
- [Rationals 2.3.0 on NuGet](https://www.nuget.org/packages/Rationals/) — alternative considered; same conclusion — overkill
- [SoundTouch.Net 2.3.2 on NuGet](https://www.nuget.org/packages/SoundTouch.Net) — verified current version, LGPL license confirmed; rejected for license/philosophy mismatch
- [Scala .scl format spec](https://www.huygens-fokker.org/scala/scl_format.html) — confirms format simplicity; no C# library exists, hand-roll is correct call
- [DryWetMidi NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) — confirms 8.0.3 still current; v9.0.0-prerelease1 noted but not required
- [r8brain-free-src](https://github.com/avaneev/r8brain-free-src) — MIT C++ resampler; rejected for P/Invoke complexity vs benefit

---
*Stack research for: Flow v1.3 Composer DX Tier B/C — tuplets, fractional durations, DEFER-01..06, Tier B/C bundle*
*Researched: 2026-04-26*
