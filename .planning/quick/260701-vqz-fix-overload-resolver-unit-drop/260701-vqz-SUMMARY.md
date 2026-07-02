---
phase: 260701-vqz
plan: 01
subsystem: flow-lang
status: complete
tags: [type-system, overload-resolution, ergonomics, music-types, bugfix]
requires: []
provides: [unit-aware-overload-resolution, user-proc-scalar-coercion, decibel-overloads, silence-alias]
affects:
  - flow-lang/TypeSystem/FunctionSignature.cs
  - flow-lang/Runtime/Value.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
  - flow-lang/StandardLibrary/Audio/BufferHelpers.cs
  - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs
  - flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs
  - flow-lang/audio.flow
key-files:
  created:
    - tests/test_type_ergonomics.flow
  modified:
    - flow-lang/TypeSystem/FunctionSignature.cs
    - flow-lang/Runtime/Value.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
    - flow-lang/StandardLibrary/Audio/BufferHelpers.cs
    - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs
    - flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs
    - flow-lang/audio.flow
    - flow-lang.Tests/FlowScriptData.cs
commits: [7962c61, aa25f7a]
---

# Quick Task 260701-vqz: Fix the OverloadResolver unit-drop bug family

Pre-release type-ergonomics audit (2026-07-01) verified ~20 broken unit-slots
across 16 builtin families, nearly all one root cause: in
`FunctionSignature.CalculateSpecificity`, a music-typed arg's raw-Double
compatibility (+500) always outranked its unit-preserving conversion (+100),
so any raw-Double sibling overload won and the unit was silently dropped —
`(createSineTone 440Hz 500ms 0.5)` rendered 500 seconds.

## What shipped

1. **Unit-aware resolver tiers** (`FunctionSignature.cs`): exact 1000 >
   unit-preserving music-type conversion 700 (ms→s scales in Value.ConvertTo) >
   non-unit compat 500 (unchanged) > unit-dropping raw-numeric landing 300
   (Double) / 290 (Float). The Double/Float split breaks the permanent
   `(add 100ms 50ms)` ambiguity tie. Unit types: Decibel/Millisecond/Second/
   Cent/Semitone/Hertz/Beat. Raw-number calls untouched (Double→Double still
   exact 1000).
2. **`Value.ConvertTo` IntType arm** — int-backed music types (Semitone) can
   now convert to Int; `(up seq +2st)` crashed before.
3. **User-proc bind-time scalar coercion** (`Interpreter.cs`): pure-Flow procs
   (createSineTone, silence, adsr...) previously bound args RAW — only C#
   builtins coerced. Scalar args now convert to the declared param type at
   binding; containers/Lazy/Function/Void keep legacy raw binding (their
   CanConvertTo is permissive but Value.ConvertTo has no container arms).
4. **Missing unit overloads + surfaces**: tone generators ×4 gain
   (Hertz, Second, Double); `adsr(Second, Second, Double, Second)`;
   `volume/mixBuffers/scaleBuffer` Decibel (10^(dB/20));
   `sing(String, Note, Second)`. Every C# registration has its audio.flow
   forward decl (unreachable otherwise).
5. **`silence(Double|Second)`** registered as createSilence aliases (was a
   documented phantom).
6. **`applyEnvelope`/`scaleBuffer` return the processed Buffer** as :help and
   Standard-Library.md always claimed (was Void; `Buffer b = (...)` bound null).

## Verification

- New `tests/test_type_ergonomics.flow`: 25 sentinel checks (frame-count +
  sample-value assertions), registered in FlowScriptData for CI.
- Full .flow sweep: only the 4 intentional error-behavior scripts print
  error lines (unchanged).
- xUnit: **2738 passed / 0 failed / 19 skipped** — all WAV baselines and
  determinism gates intact, so no existing dispatch shifted.
- `FlowTarget=Web` rebuild green.

## Deviations / notes

- Bind-time coercion is a deliberate behavior change for user procs: an Int
  arg into a `Double:` param now arrives as a real Double (declared types are
  honored). Suite-wide green; sanctioned by pre-traction breaking-change
  latitude (D-v1.5-01).
- Mixed raw+unit calls in the SAME time family (e.g. `(adsr 0.01 100ms ...)`)
  still resolve to the raw-Double sig and pass ms raw — use one style per
  call. Homogeneous calls (all units or all raw) are correct.
- Beat durations still pass raw into Double slots (tempo-dependent conversion
  needs MusicalContext, which Value.ConvertTo can't reach) — `beatToSec`
  remains the explicit bridge; `delay` keeps its dedicated Beat overload.
- `createClip` is a non-constant test signal despite its audio.flow Note
  claiming "constant value" (C# SignalGeneration.CreateClip shadows the .flow
  proc) — discovered while writing assertions, not addressed here.
