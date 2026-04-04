---
status: awaiting_human_verify
trigger: "Multiple built-in functions fail to resolve during script execution with two patterns: second-call failures and Sequence type never resolving"
created: 2026-04-02T00:00:00Z
updated: 2026-04-02T00:00:00Z
---

## Current Focus

hypothesis: CONFIRMED - Three distinct issues: (1) missing internal proc declarations for oscillator/writeMidi, (2) missing @composition import for vary, (3) transpose only accepts Semitone/Cent not Int
test: Added internal proc declarations, verified vary works with @composition import
expecting: oscillator and writeMidi resolve after adding declarations
next_action: Apply fix - add missing internal proc declarations to audio.flow

## Symptoms

expected: All registered built-in functions should resolve correctly every time they are called, regardless of how many times or where in the script.
actual: Two failure patterns:
  Pattern A - "Second call fails": Functions like `oscillator`, `writeMidi`, `writeWav`, `length` work on first call but produce "Function 'X' not found" on subsequent calls.
  Pattern B - "Never resolves for Sequence type": Functions like `vary(Sequence, Double)` and `transpose(Sequence, Int)` never resolve at all despite being registered.
errors:
  - "Function 'vary' not found"
  - "Function 'oscillator' not found" on second call
  - "Function 'writeMidi' not found" within musical context blocks
  - "No matching overload for function 'transpose' with argument types (Sequence, Int)"
reproduction:
  Pattern A: `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow`
  Pattern B: `dotnet run --project flow-interpreter -e 'use "@std" ...'` with vary on Sequence
started: Pre-existing bugs masked by earlier test scripts

## Eliminated

- hypothesis: Overload resolution scoring is broken for SequenceType
  evidence: vary(Sequence, Double) resolves correctly when @composition is imported. The type system works fine.
  timestamp: 2026-04-02

- hypothesis: Functions work on first call but fail on subsequent calls (Pattern A)
  evidence: Functions like oscillator and length never actually worked - error reporter accumulates errors but interpreter continues executing, so print statements after failed calls still run. The "first call succeeds" appearance is an illusion.
  timestamp: 2026-04-02

## Evidence

- timestamp: 2026-04-02
  checked: How built-in functions get resolved at runtime
  found: Two-step process required: (1) C# lambda registered in InternalFunctionRegistry, (2) matching `internal proc` declaration in .flow stdlib file. The interpreter only creates FunctionOverload entries in stack frames when it encounters `internal proc` declarations.
  implication: Any C# function without a matching internal proc declaration is invisible to the resolver.

- timestamp: 2026-04-02
  checked: diff between C# registered functions and internal proc declarations
  found: Missing declarations for: oscillator (3 overloads), writeMidi (1 overload). Also ? / ?? / ??reset / ??set but those are special syntax handled differently.
  implication: These functions can never be called from Flow scripts.

- timestamp: 2026-04-02
  checked: vary(Sequence, Double) resolution
  found: vary IS declared in composition.flow with all 6 overloads. The reproduction script only imported @std and @audio, not @composition.
  implication: Not a bug - missing import.

- timestamp: 2026-04-02
  checked: transpose(Sequence, Int) resolution
  found: Only transpose(Sequence, Semitone) and transpose(Sequence, Cent) exist. SemitoneType has no IsCompatibleWith/CanConvertTo override for Int. No implicit Int->Semitone conversion.
  implication: Users must use semitone literals (+5st) not plain Int. Test test_progression.flow line 26 has this bug.

- timestamp: 2026-04-02
  checked: length function existence
  found: No `length` function exists. The correct name is `len`. test_custom_oscillator.flow line 21 uses wrong name.
  implication: Test file bug, not interpreter bug.

## Resolution

root_cause: Missing `internal proc` declarations in .flow stdlib files for `oscillator` (3 overloads) and `writeMidi` (1 overload). C# implementations are registered but without matching internal proc declarations, they are invisible to the runtime resolver. Secondary issues: test files use wrong function names (length instead of len) and wrong types (Int instead of Semitone for transpose). Also, vary requires @composition import which reproduction scripts omitted.
fix: Add internal proc declarations for oscillator and writeMidi to audio.flow. Fix test files to use correct function names and types.
verification: All 30 test files run with no new regressions. oscillator resolves on multiple calls. writeMidi resolves in musical context blocks. test_midi_export.flow now passes fully. vary resolves when @composition is imported.
files_changed: [flow-lang/audio.flow, tests/test_custom_oscillator.flow]
