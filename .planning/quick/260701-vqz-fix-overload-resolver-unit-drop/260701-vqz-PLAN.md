---
phase: 260701-vqz
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/TypeSystem/FunctionSignature.cs
  - flow-lang/Runtime/Value.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
  - flow-lang/StandardLibrary/Audio/BufferHelpers.cs
  - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs
  - flow-lang/StandardLibrary/Audio/AudioCore.cs
  - flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs
  - flow-lang/audio.flow
  - tests/test_type_ergonomics.flow
autonomous: true
requirements: [pre-release type-ergonomics audit 2026-07-01]

must_haves:
  truths:
    - "(createSineTone 440Hz 500ms 0.5) renders 0.5 seconds of audio, not 500 seconds."
    - "(noise 2ms) / (fadeIn buf 100ms) / (createSilence 500ms) resolve to the Second overloads via ms->s conversion, not the raw-Double overloads."
    - "(delay buf 0.25s 0.3 0.4) resolves to the Millisecond overload as 250ms."
    - "(add 100ms 50ms) returns 150.0 (Double) instead of a hard ambiguity error."
    - "(volume buf +6dB) and (volume buf -6dB) both apply dB-converted linear multipliers."
    - "(up seq +2st) no longer crashes with 'Cannot convert Flow type Semitone to Int'."
    - "(adsr 10ms 100ms 0.7 200ms) produces a 10-millisecond attack, not 10-second."
    - "silence is a registered builtin (alias of createSilence)."
    - "applyEnvelope and scaleBuffer return the processed Buffer (matching :help + Standard-Library.md)."
    - "Existing .flow test suite and flow-lang.Tests dispatch behavior unchanged for raw-Double calls."
  artifacts:
    - flow-lang/TypeSystem/FunctionSignature.cs
    - flow-lang/audio.flow
    - tests/test_type_ergonomics.flow
  key_links:
    - "CalculateSpecificity unit-tiers: exact 1000 > unit-preserving conversion 700 > unit-dropping raw-numeric compat 300 (Double) / 290 (Float) > other compat 500 unchanged for non-unit args"
    - "Every new C# overload has a matching internal proc forward-decl in audio.flow (registration is unreachable without it)"
---

<objective>
Fix the verified overload-resolver unit-drop bug family: music-typed arguments silently lose their unit whenever a raw-Double sibling overload exists at the same arity, because compat (+500) outranks unit-converting conversion (+100). Add the handful of missing unit overloads for first-hour builtins, the missing Value.ConvertTo Int arm, the phantom `silence` alias, and make applyEnvelope/scaleBuffer return the buffer as documented.

Purpose: the language's core promise — music literals Just Work — currently fails on hello-world-adjacent calls ((createSineTone 440Hz 500ms 0.5) = 500 s of silence-inducing audio).
Output: unit-aware dispatch with a regression .flow test pinning every fixed call shape.
</objective>

<context>
Diagnosis complete (pre-release audit, session scratchpad type-ergonomics-gaps.md). Root cause: FunctionSignature.CalculateSpecificity tiers. Value.ConvertTo already scales ms<->s correctly (Value.cs:347-355); ExpressionEvaluator already converts post-resolution (lines 553-555, 921-923). Second's raw backing is seconds, so Second values passing raw into Double-seconds handler slots are correct — new overloads only need to exist as resolution targets.
</context>

<tasks>

<task type="auto">
  <name>Task 1: Resolver scoring tiers + Value.ConvertTo Int arm</name>
  <files>flow-lang/TypeSystem/FunctionSignature.cs, flow-lang/Runtime/Value.cs</files>
  <action>In CalculateSpecificity: introduce IsUnitQuantityType helper (Decibel/Millisecond/Second/Cent/Semitone/Hertz/Beat). Unit arg + Double param compat scores 300 (Float: 290, breaking the add(Float,Float)/add(Double,Double) tie); unit arg converting to unit param scores 700; all other tiers unchanged. In Value.ConvertTo int arm: add IntType target (fixes Semitone->Int).</action>
  <verify>dotnet build; -e probes for createSineTone/noise/delay/add/up.</verify>
  <done>Probes print correct frame counts / values; no ambiguity error on add.</done>
</task>

<task type="auto">
  <name>Task 2: Missing overloads + silence alias + documented returns</name>
  <files>flow-lang/StandardLibrary/*, flow-lang/audio.flow</files>
  <action>Add C# overloads + matching audio.flow forward-decls: createSineTone(Hertz, Second, Double) (+ Saw/Square/Triangle siblings if they have Hz-first forms), adsr(Second, Second, Double, Second) (.flow-level, delegates to createADSR), volume(Buffer, Decibel), mixBuffers(Buffer, Buffer, Decibel, Decibel), scaleBuffer(Buffer, Decibel), sing(String, Note, Second). Add .flow procs silence(Double)/silence(Second) delegating to createSilence. Make ApplyEnvelope + ScaleBuffer handlers return the buffer Value instead of Void; update audio.flow Note comments.</action>
  <verify>dotnet build; -e probes for volume dB, mixBuffers dB, sing ms, silence, Buffer b = (applyEnvelope ...).</verify>
  <done>All probes pass; volume +6dB ~= 2x linear; Buffer binding non-null.</done>
</task>

<task type="auto">
  <name>Task 3: Regression test + full suite</name>
  <files>tests/test_type_ergonomics.flow</files>
  <action>New .flow test pinning every must_have call shape with frame-count/value assertions and PASS-line output. Run the full tests/*.flow suite + flow-lang.Tests xUnit suite; fix any dispatch regressions surfaced.</action>
  <verify>for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done — all pass; dotnet test flow-lang.Tests green.</verify>
  <done>New test passes; zero regressions in existing suites.</done>
</task>

</tasks>
