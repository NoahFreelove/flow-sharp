# Project Research Summary

**Project:** Flow Language v1.1 -- Polish & Foundations
**Domain:** Music programming language interpreter -- bug fixes, DX polish, and audio feature additions
**Researched:** 2026-04-02
**Confidence:** HIGH

## Executive Summary

Flow v1.1 targets 12 discrete items across two critical bug fixes, four DX improvements, two CLI enhancements, and four audio/music features. Every item is implementable with zero new external dependencies -- all features use .NET 9's `System.Math`, the existing `INoteSynthesizer` pattern, or arithmetic on the `AudioBuffer` type already in the codebase. The recommended stack remains exactly as-is, and this milestone should be treated as a hardening-and-completeness pass rather than an architectural expansion.

The two bugs are the highest-priority items because they block real user scripts silently. The Sequence overload bug causes `transpose` and `vary` to fail at runtime with no useful error, and the section bare-expressions bug renders sections containing unas signed note streams as silence. Both have known fix points in the interpreter and type system. The ten remaining features are either purely additive (new files, no changes to existing code paths) or low-risk modifications to isolated subsystems. The single high-risk feature -- true tempo ramps -- touches the core timing model across six or more files and is recommended to be implemented as a transform function returning a Buffer rather than as new parser syntax or a pipeline rewrite.

Build order matters and was consistent across all four research files: implement verbose diagnostics first (aids debugging everything else), then fix the two critical bugs (unblocks user scripts that validate all subsequent work), then add the quick-win DX features (independent, fast, parallelizable), then the audio additions (established patterns), and tempo ramps last (highest blast radius if anything goes wrong).

## Key Findings

### Recommended Stack

No new dependencies are introduced in this milestone. All features are implemented using the existing .NET 9 standard library and the codebase's own audio infrastructure.

**Core technologies:**
- **.NET 9 / C# 13**: Existing runtime. Record types, pattern matching, and file-scoped namespaces already used throughout -- continue those conventions.
- **`System.Math`**: Provides all math stdlib functions (`sin`, `cos`, `abs`, `sqrt`, `pow`, `log`, `floor`, `ceil`, `round`, `min`, `max`, `PI`, `E`) with no additional packages.
- **`INoteSynthesizer` interface**: Existing synthesizer contract in `StandardLibrary/Audio/Synthesizers/`. Three new synths (strings, organ, bell) slot in by creating new `.cs` files and adding entries to `SynthesizerFactory.Create()`.
- **`AudioBuffer` API**: Existing type. `mix()` is arithmetic on float arrays using the max-length output pattern and sqrt(N) normalization.
- **PulseAudio P/Invoke**: Existing audio backend. No changes in this milestone.

The only previously-considered external dependency (Melanchall.DryWetMidi for MIDI export) is out of scope for v1.1. It remains the right choice for a future MIDI export milestone.

### Expected Features

**Must have (table stakes -- users expect these; absence makes the product feel broken):**
- `//` line comments -- Flow currently has zero comment support, the single most glaring language omission
- Math stdlib (`sin`, `cos`, `abs`, `sqrt`, `min`, `max`, `pow`, `floor`, `ceil`, `round`, `pi`, `tau`) -- required for custom oscillators and generative patterns
- `exportWav` -> `writeWav` rename with deprecated alias -- codebase inconsistency that confuses every new user
- `mix(Buffer, Buffer)` convenience function -- two-buffer layering in a simpler form than the existing four-argument `mixBuffers`
- REPL auto-imports -- REPL is unusable for quick experiments without pre-loading `@std` and `@audio`

**Should have (differentiators for a music production language):**
- Per-section volume/gain in songs (`section@0.8` syntax or via `velocity` block) -- mix-level control within arrangements
- Three new synth presets (strings, organ, bell) -- expands the instrument palette with acoustically correct algorithms

**Defer to later milestone:**
- True continuous tempo ramps -- architectural complexity high relative to v1.1 scope; the discrete-step approximation (pre-render as N constant-BPM sub-sections) is the acceptable v1.1 path
- Block comments (`/* ... */`) -- adds lexer complexity for minimal gain; `//` covers 99% of use cases
- Variadic `mix()` with N buffers -- simple two-buffer version ships now; varargs is a later extension

**Explicit anti-features (do not build):**
- Dynamic tempo via variable binding (`tempo myVar { ... }`) -- requires lazy evaluation across the full pipeline
- Auto-normalization in `mix()` -- silently changes gain staging, making levels impossible to reason about
- Math as a loadable module (`use "@math"`) -- math functions belong in core like `print` and `str`, always available

### Architecture Approach

Of the 12 items, 8 are purely additive (new files following established patterns) and 4 are targeted modifications to isolated subsystems. The component boundary map is clean: the lexer is touched only for `//` comments; the interpreter only for the section bare-expressions fix; the type system only for the Sequence overload bug; and `SongRenderer` only for per-section gain. No change crosses multiple architecture layers except tempo ramps, which is exactly why tempo ramps carry HIGH risk.

**Major components touched in v1.1:**
1. `SimpleLexer.SkipWhitespaceAndComments` -- add `//` line comment detection before the `else break` path
2. `Interpreter.ExecuteSectionDeclaration` -- collect `_lastExpressionValue` as an implicit sequence, mirroring how `proc` handles implicit returns
3. `OverloadResolver` / `FunctionSignature.Matches` / `Value` factory -- fix type tag mismatch causing Sequence overload failure (diagnosis required at runtime before fix)
4. `StandardLibrary/MathFunctions.cs` (new) -- `System.Math` wrappers registered via `RegisterMath()`
5. `StandardLibrary/Audio/Synthesizers/` (3 new files) -- `StringsSynthesizer`, `OrganSynthesizer`, `BellSynthesizer` each implementing `INoteSynthesizer`
6. `SongRenderer.RenderSection` -- 5-line addition to apply `section.Context?.Velocity` as a gain multiplier after `MixVoicesToStereoBuffer`
7. `flow-interpreter/Program.cs` + `FlowEngine` -- `--verbose` flag threaded through to `OverloadResolver` and `ModuleLoader` diagnostic output
8. `Repl.cs` -- execute `use "@std"` + `use "@audio"` before the input loop

### Critical Pitfalls

1. **Fixing Sequence overloads at the wrong layer** -- Do not widen `SequenceType.IsCompatibleWith()` or make it more permissive. That breaks all Sequence overload resolution globally. The root cause is almost certainly a type tag mismatch at the `Value` factory or `NoteStreamCompiler` level. Diagnose by printing `argTypes[i].GetType().Name` at the resolver call site before writing any fix. Run all 70+ test files after any change to `FunctionSignature.Matches` or `OverloadResolver`.

2. **Sections silently dropping bare expressions** -- The fix is to collect `_lastExpressionValue` as an implicit sequence after the section body executes, not to require users to assign note streams to variables. Silent wrong behavior (renders 0 frames with no error) is the worst kind of bug. Also add a warning when a section produces no sequences at all.

3. **Static `PlaybackFunctions._manager` clobbering** -- Watch mode creates a new `FlowEngine` on file change, which overwrites the static `_manager`. Every code path that creates a background engine must save/restore via `PlaybackFunctions.GetManager()` / `SetManager()`. Do not refactor the static out in v1.1 -- that is a major architectural change.

4. **Tempo ramps breaking the constant-BPM rendering pipeline** -- The entire rendering pipeline reads `section.Context?.Tempo` once as a scalar. True continuous tempo ramps require a `TempoMap` concept replacing every `60.0 / bpm` calculation in `SequenceRenderer` and `SongRenderer`. Do not attempt this as a pipeline rewrite in v1.1. Instead, implement as a transform function (`tempoRamp(Sequence, startBPM, endBPM) -> Buffer`) that renders with a discrete-step approximation (1-beat granularity), bypassing the constant-BPM pipeline.

5. **Error accumulator masking real failures** -- `ErrorReporter` collects errors without throwing. Every call site after `ReportError` continues executing into potentially invalid state. When fixing bugs, add early returns after error reports at the failure site. Use `--verbose` to surface the accumulated error list before the final displayed error.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Diagnostics and Bug Fixes
**Rationale:** Verbose logging aids diagnosis of the Sequence overload bug; both bugs silently break user scripts and block validation of all subsequent features; error masking makes debugging harder across the entire milestone.
**Delivers:** A working interpreter where transforms and sections behave as documented.
**Addresses:** `--verbose` flag (CLI), Sequence overload resolution bug (type system), section bare expressions bug (interpreter), better error messages (ErrorReporter).
**Avoids:** Pitfall 1 (fixing overloads at wrong layer), Pitfall 2 (silent section failures), Pitfall 3 (error accumulator masking), Pitfall 4 (static manager clobbering in watch mode).
**Research flag:** The Sequence overload root cause is hypothesized from code reading but not confirmed by runtime tracing. This phase requires diagnostic-first development -- add verbose output, run a failing test, read the output, then write the fix.

### Phase 2: Quick Wins
**Rationale:** All four features are fully independent, touch no shared state, and can be implemented in any order or in parallel. Each is either a new file or a small targeted addition to one function. Combined they dramatically improve the daily-use experience.
**Delivers:** A language that is comfortable to write (`//` comments, `writeWav` consistency, math functions everywhere) and experiment with (REPL auto-imports).
**Addresses:** `//` line comments (lexer), math stdlib (new `MathFunctions.cs`), `exportWav` -> `writeWav` rename with deprecated alias, REPL auto-imports.
**Avoids:** Pitfall 5 (token boundary collision for `//`), Pitfall 6 (breaking existing scripts on rename), Pitfall 7 (math overload type matching for Int vs Double args).
**Research flag:** None needed. All four follow well-established patterns in this codebase and have clear implementation paths.

### Phase 3: Audio Features
**Rationale:** All three audio additions use the existing `INoteSynthesizer` and `AudioBuffer` infrastructure with no core changes. They are parallelizable and follow the same established pattern. Per-section gain is a five-line addition to `SongRenderer`.
**Delivers:** Expanded instrument palette (strings, organ, bell), buffer-level audio layering (`mix()`), and mix-level volume control per section.
**Addresses:** `mix(Buffer, Buffer)` function (audio stdlib), strings/organ/bell synth presets (new `Synthesizers/` files), per-section volume/gain (SongRenderer).
**Avoids:** Pitfall 10 (buffer length/channel mismatch in mix), Pitfall 11 (double gain application), Pitfall 12 (new synths changing default instrument behavior).
**Research flag:** Synth implementations (strings: detuned-saw vibrato; organ: Hammond drawbar additive; bell: Risset inharmonic partials) are standard algorithms. No additional research needed.

### Phase 4: Tempo Ramps
**Rationale:** This is the only feature requiring a change to a core architectural assumption (constant BPM). Done last, it is isolated -- if it destabilizes the audio pipeline, all preceding features are already stable and tested.
**Delivers:** Gradual BPM transitions within rendered sequences.
**Addresses:** `tempoRamp(Sequence, startBPM, endBPM) -> Buffer` transform function.
**Avoids:** Pitfall 8 (pipeline rewrite for continuous tempo) -- implement as discrete-step approximation (pre-render as N constant-BPM sub-sections at 1-beat granularity), not as parser syntax or full TempoMap rewrite.
**Research flag:** The beat-to-seconds integral for linear ramps has a closed-form solution (`time(b) = B * 60 * ln(T1 + (T2-T1)*b/B) / (T2-T1)` when T1 != T2) that should be validated against a known test case before shipping.

### Phase Ordering Rationale

- Verbose logging enables the root-cause trace for the Sequence overload bug -- this is why diagnostics come before the bug fixes, not after.
- Both bugs in Phase 1 must be fixed before audio features in Phase 3 can be reliably tested (sections and transforms are used in every non-trivial audio script).
- Phase 2 features are fully decoupled and could be developed concurrently with Phase 1 by a separate contributor, but ordered here for clarity.
- Tempo ramps are isolated when done last. If a `TempoMap` rewrite becomes necessary during implementation, it does not cascade into any Phase 1-3 work.

### Research Flags

Phases needing diagnostic or runtime validation during execution:
- **Phase 1 (Sequence overload):** Root cause not confirmed by code reading alone. Must trace actual `FlowType` at `OverloadResolver.Resolve()` before writing the fix. The `--verbose` flag from the same phase makes this tractable.
- **Phase 4 (Tempo ramps):** Discrete-step approximation at 1-beat granularity should be measured against the closed-form integral to verify perceptual smoothness. If a 120->60 BPM ramp over 4 beats produces audible staircase artifacts, increase granularity to 0.25 beats.

Phases with standard patterns (skip additional research):
- **Phase 2:** All four features have clear, minimal implementation paths established in the codebase. `//` comments follow the existing `Note:` comment precedent; math wraps `System.Math`; rename is register-both-names; REPL auto-imports is a single `Execute` call.
- **Phase 3 (Synths):** Strings (detuned saw), organ (additive Hammond), and bell (Risset inharmonic partials) are textbook synthesis algorithms. `PianoSynthesizer.cs` is the implementation template.
- **Phase 3 (mix, per-section gain):** Pure arithmetic operations on existing types with well-understood normalization conventions.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All features confirmed implementable from direct codebase inspection; zero new dependencies; all .NET 9 APIs verified available |
| Features | HIGH | Implementation paths are clear from codebase analysis; complexity tiers are validated; anti-features are explicitly justified |
| Architecture | HIGH | All integration points identified by reading specific file and line numbers; component boundaries are clean and non-overlapping except for tempo ramps |
| Pitfalls | MEDIUM | Critical pitfalls 1-3 are verified from code reading; Pitfall 1 (Sequence overload root cause) is still a hypothesis until a failing test is traced at runtime |

**Overall confidence:** HIGH

### Gaps to Address

- **Sequence overload root cause (Phase 1):** Three candidate root causes are identified (type tag mismatch in Value factory, Semitone/Cent specificity ambiguity, parser flow-operator injection). The actual cause requires runtime diagnostic output to confirm. Implement verbose mode, run a failing test, read the output, then write the targeted fix.
- **Tempo ramp perceptual smoothness (Phase 4):** The 1-beat granularity assumption for discrete-step approximation is based on general music production intuition, not a measurement on this specific renderer. Validate with a test render and human listening before shipping.
- **`rit`/`accel` interaction with tempo ramps (Phase 4):** Existing `ritardando`/`accelerando` in `TransformFunctions.cs` are note-level duration transforms, not BPM ramps. They should not conflict with `tempoRamp()`, but verify they are not applied to the same sequences in user scripts, as combining them would double-apply the effect.

## Sources

### Primary (HIGH confidence -- direct codebase inspection)
- `flow-lang/TypeSystem/OverloadResolver.cs` -- overload resolution logic and 4-way compatibility check
- `flow-lang/TypeSystem/FunctionSignature.cs` -- `Matches()` and `CalculateSpecificity()` implementation
- `flow-lang/Interpreter/Interpreter.cs:336-377` -- `ExecuteSectionDeclaration` collection loop
- `flow-lang/Lexing/SimpleLexer.cs:777-813` -- `SkipWhitespaceAndComments` current implementation
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` -- reference synthesizer pattern
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` -- oscillator and envelope primitives
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- rendering pipeline and constant-BPM assumption
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` -- transform registration patterns
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:387-397` -- `exportWav` registration (no math functions)
- `flow-lang/Audio/PlaybackFunctions.cs:15` -- static `_manager` field

### Secondary (MEDIUM confidence -- established synthesis algorithms)
- Hammond organ tonewheel model -- additive sine harmonics at drawbar positions (sub, fundamental, harmonics)
- Jean-Claude Risset bell synthesis (1969) -- inharmonic partial ratios (1.0x, 2.0x, 2.76x, 4.07x, 5.41x, 6.58x) for metallic timbres
- Constant-power panning law -- already implemented in `SongRenderer.MixVoicesToStereoBuffer` (verified in codebase)
- Constant-power mixing normalization (divide by sqrt(N)) -- standard audio engineering practice
- Linear tempo ramp closed-form integral -- standard calculus for variable-rate time mapping

### Tertiary (LOW confidence -- inferred, needs runtime validation)
- Sequence overload root cause -- hypothesized from reading `Value.cs`, `NoteStreamCompiler.cs`, and `FunctionSignature.Matches()`; not confirmed by runtime tracing

---
*Research completed: 2026-04-02*
*Ready for roadmap: yes*
