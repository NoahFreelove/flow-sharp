# Architecture: v1.1 Integration Analysis

**Project:** Flow Language v1.1
**Researched:** 2026-04-02
**Focus:** How each bug fix and new feature integrates with existing architecture

## Classification: Additive vs Modification

| Feature | Classification | Files Touched | Risk |
|---------|---------------|---------------|------|
| Sequence overload resolution | **MODIFICATION** -- core type system | `SequenceType.cs`, `ExpressionEvaluator.cs`, `OverloadResolver.cs` | HIGH -- affects all overload resolution |
| Section bare expressions | **MODIFICATION** -- interpreter core | `Interpreter.cs` (`ExecuteSectionDeclaration`) | MEDIUM -- section-specific, testable |
| `//` line comments | **MODIFICATION** -- lexer | `SimpleLexer.cs` (`SkipWhitespaceAndComments`) | LOW -- isolated lexer change |
| Math functions | **ADDITIVE** -- new built-ins | New `MathFunctions.cs`, `BuiltInFunctions.cs` (one call) | LOW -- pure addition |
| `exportWav` rename to `writeWav` | **MODIFICATION** -- rename + alias | `BuiltInFunctions.cs`, test files, `.flow` stdlib | LOW -- mechanical |
| `mix()` buffer layering | **ADDITIVE** -- new built-in | New function in `Audio/` area | LOW -- pure addition |
| Per-section volume/gain | **MODIFICATION** -- song renderer | `SongRenderer.cs` | LOW -- leverages existing `MusicalContext.Velocity` |
| Synth presets (strings, organ, bell) | **ADDITIVE** -- new synthesizers | New files in `Audio/Synthesizers/`, `SynthesizerFactory.Create()` | LOW -- follows established pattern |
| Tempo ramps | **MODIFICATION** -- musical context + renderer | `MusicalContext.cs`, `SongRenderer.cs`, `SequenceRenderer`, parser | HIGH -- fundamental timing change |
| REPL auto-imports | **MODIFICATION** -- REPL startup | `Repl.cs` or `FlowEngine.cs` | LOW -- one-time init |
| `--verbose` flag | **ADDITIVE** -- CLI + diagnostics | `Program.cs`, `FlowEngine` | LOW -- opt-in output |
| Better error reporting | **MODIFICATION** -- diagnostics | `ErrorReporter.cs`, scattered call sites | MEDIUM -- many small changes |

## Detailed Integration Analysis

### 1. Sequence Overload Resolution Bug (CRITICAL)

**Problem:** `transpose(Sequence, Semitone)` and `vary(Sequence, Double)` fail at overload resolution. The functions are registered with correct signatures but the resolver cannot match them.

**Root Cause Analysis:**
- `SequenceType` is a sealed singleton extending `FlowType`
- `FlowType.IsCompatibleWith()` defaults to `Equals(target)` -- exact reference equality
- Since `SequenceType.Instance` is a singleton, `Equals()` should always succeed when both sides are `SequenceType`
- The transform functions register with `SequenceType.Instance` as their first parameter
- `FunctionSignature.Matches()` checks 4-way compatibility: `argType.IsCompatibleWith(paramType)`, `argType.CanConvertTo(paramType)`, `paramType.IsCompatibleWith(argType)`, `paramType.CanConvertTo(argType)`

**Likely actual root cause options (investigate in order):**
1. The `Value` produced by `NoteStreamCompiler` or pipe expression carries a wrong type tag (e.g., wrapping SequenceData in a Value with ArrayType instead of SequenceType)
2. The second argument (e.g., `+2st` for Semitone) is being typed incorrectly by the lexer/evaluator, causing the 2-arg signature to not match
3. The pipe transform `seq -> transpose(+2st)` is producing `transpose(seq, +2st)` at parse time but the arguments evaluate in unexpected order

**What Changes:**
- **Diagnosis first**: Add verbose logging to `OverloadResolver.Resolve()` to print candidate signatures vs actual arg types
- `ExpressionEvaluator.cs` -- verify Value types at pipe resolution boundaries
- `NoteStreamCompiler.cs` -- verify the Value type tag on compiled sequences
- Possibly `SequenceType.cs` or the Value factory -- fix the type tag

**Integration Points:**
- `FunctionSignature.Matches()` lines 93-111 -- the 4-way compatibility check
- `OverloadResolver.Resolve()` lines 39-45 -- the "no matching overload" error path
- All transform registrations in `TransformFunctions.cs` and `VariationFunctions.cs`

**Risk:** If the fix touches `FlowType.IsCompatibleWith`, it affects every function call. Must be surgical -- likely a fix to a specific Value factory or type tag, not the compatibility logic itself.

### 2. Section Bare Expressions Bug (CRITICAL)

**Problem:** Sections silently drop bare expressions, producing 0-frame renders:

```flow
section intro {
    | C4 D4 E4 F4 |
}
```

**Root Cause:** `ExecuteSectionDeclaration` (Interpreter.cs:336-377) only collects Sequence values from *named variables* declared in the section scope. It iterates `_context.CurrentFrame.GetLocalVariables()` and checks for `SequenceData`. A bare note stream expression evaluates via `ExpressionStatement` and stores the result in `_lastExpressionValue`, but it is never assigned to a variable, so the collection loop at lines 360-368 never sees it.

**What Changes:**
- `Interpreter.cs` `ExecuteSectionDeclaration` -- after executing each statement, check if the result is a SequenceData and collect it:
  - Track a counter for auto-naming: `"_expr_0"`, `"_expr_1"`, etc.
  - After the execution loop, also scan expression results (not just local variables)
  - **Recommended approach:** Modify the loop to intercept `ExpressionStatement` results whose value is SequenceData and auto-assign them to the current frame before the collection pass

**Integration Points:**
- `ExecuteSectionDeclaration` collection loop (lines 360-368)
- `SectionData` constructor -- already accepts `Dictionary<string, SequenceData>`, auto-named keys fit
- `SongRenderer.RenderSection` -- iterates `section.Sequences`, picks up auto-named entries automatically
- No parser changes needed

**Risk:** Medium. Must ensure: (a) auto-named keys don't collide with user variable names (use `_` prefix), (b) non-Sequence bare expressions are harmlessly ignored, (c) ordering of auto-collected sequences is deterministic.

### 3. `//` Line Comments (LOW RISK)

**Current State:** `SkipWhitespaceAndComments()` in SimpleLexer handles whitespace, line continuations (`\` + newline), and `Note:` comments (start-of-line only). No `//` support exists.

**What Changes:**
- `SimpleLexer.cs` line ~809, add before the `else break`:
  ```csharp
  else if (c == '/' && PeekNext() == '/')
  {
      while (!IsAtEnd() && Peek() != '\n')
          Advance();
  }
  ```

**Conflict Check:** The `/` division operator is handled in `NextToken()` as a single-character operator. Since `SkipWhitespaceAndComments()` runs BEFORE `NextToken()`, the two-char `//` is consumed before the single `/` path is reached. The `->` arrow uses the same pre-check pattern successfully. No conflict.

### 4. Math Functions (ADDITIVE)

**What to Add:** `sin`, `cos`, `abs`, `sqrt`, `min`, `max`, `floor`, `ceil`, `round`, `pow`

**Where:** New file `StandardLibrary/MathFunctions.cs` following `TransformFunctions.cs` pattern:
- Static class with `Register(InternalFunctionRegistry registry)` method
- Multiple overloads per function (Int and Float/Double)
- Called from `BuiltInFunctions.RegisterAllImplementations()`

**Overload Strategy:**
- `sin(Float) -> Float`, `cos(Float) -> Float`, `sqrt(Float) -> Float` -- single overload each
- `abs(Int) -> Int`, `abs(Float) -> Float` -- two overloads
- `min(Int, Int) -> Int`, `min(Float, Float) -> Float` -- two overloads each
- `pow(Float, Float) -> Float`

**Name Collision Check:** None of these names are currently used as function names or keywords.

### 5. `exportWav` Rename to `writeWav`

**Current State:** `exportWav` registered in `BuiltInFunctions.cs` lines 387-397 with two overloads (default 16-bit and custom bit depth).

**Approach:** Register both `"writeWav"` (primary) and `"exportWav"` (deprecated alias) pointing to the same lambda implementations. Update `.flow` stdlib files and tests.

**Files:** `BuiltInFunctions.cs`, `audio.flow`, test files referencing `exportWav`.

### 6. `mix()` Buffer Layering (ADDITIVE)

**What:** Additive mixing of multiple audio buffers.

**Signature options:**
- `mix(Buffer, Buffer) -> Buffer` -- two-buffer mix
- `mix(Buffer, Buffer, Float) -> Buffer` -- with gain
- `mix(Buffer...) -> Buffer` -- varargs for N buffers

**Algorithm:** Sample-by-sample addition with soft clipping or normalization. Handle mismatched lengths (pad shorter) and channel counts (upmix mono to stereo).

**Where:** Register in `BuiltInFunctions.RegisterAudio()` or new `MixingFunctions.cs`. Uses existing `AudioBuffer` API.

**Note:** `SongRenderer.MixVoicesToStereoBuffer` already does voice mixing internally. `mix()` is the user-facing equivalent for raw buffers.

### 7. Per-Section Volume/Gain in Songs

**Simplest path:** `MusicalContext` already has a `Velocity` field (0.0 to 1.0). Sections already snapshot musical context via `_context.GetMusicalContext()`. `SongRenderer.RenderSection` already reads `section.Context?.Tempo`.

**What Changes:** In `SongRenderer.RenderSection()`, after `MixVoicesToStereoBuffer`, apply `section.Context?.Velocity` as a gain multiplier to the buffer samples. This is a 5-line addition.

**User syntax (already works):**
```flow
section verse {
    velocity 0.6 {
        Sequence melody = | C4 D4 E4 |
    }
}
```

The existing velocity context block already sets `MusicalContext.Velocity`. The only missing piece is that `SongRenderer` doesn't read it for overall section gain.

### 8. Synth Presets: Strings, Organ, Bell (ADDITIVE)

**Pattern:** Follow existing `PianoSynthesizer.cs`, `BrassSynthesizer.cs`. Each implements `INoteSynthesizer`.

**New files:**
- `Audio/Synthesizers/StringsSynthesizer.cs` -- multi-oscillator with slow attack, vibrato, Karplus-Strong or additive
- `Audio/Synthesizers/OrganSynthesizer.cs` -- additive synthesis (drawbar model: fundamental + harmonics at octave intervals)
- `Audio/Synthesizers/BellSynthesizer.cs` -- FM synthesis (carrier + modulator with inharmonic frequency ratio)

**Integration:** Add to `SynthesizerFactory.Create()` switch:
```csharp
"strings" or "string" => new StringsSynthesizer(),
"organ" => new OrganSynthesizer(),
"bell" or "bells" => new BellSynthesizer(),
```

No other changes. Existing `BarRenderer`, `SequenceRenderer`, and `SongRenderer` all use `SynthesizerFactory.Create(synthType)`.

### 9. Tempo Ramps (HIGH COMPLEXITY)

**Problem:** The entire rendering pipeline assumes constant tempo. `MusicalContext.Tempo` is a single `double`. The conversion `60.0 / bpm` appears throughout renderers.

**What Must Change:**

1. **MusicalContext.cs** -- add tempo curve support:
   - New field: `TempoEnd` (double?) for ramp target
   - Or a `TempoMap` class that maps beat positions to BPM values

2. **New concept: TempoMap** -- converts beat positions to time (seconds):
   ```
   double BeatsToSeconds(double beatPosition)  // integrates tempo curve
   double SecondsTotalForBeats(double totalBeats)  // total duration
   ```

3. **Parser** -- new syntax, options:
   - `temporamp 120 140 { ... }` -- ramp from start to end BPM
   - Or extend existing `tempo` block: `tempo 120..140 { ... }`

4. **SequenceRenderer / BarRenderer** -- replace `double secondsPerBeat = 60.0 / bpm` with TempoMap queries

5. **SongRenderer.RenderSection** -- pass TempoMap instead of flat `bpm`

6. **SongRenderer.MixVoicesToStereoBuffer** -- voice positioning must use TempoMap for beat-to-sample conversion

**Note on existing accelerando/ritardando:** These exist in `DynamicsFunctions` but operate on note velocity/duration as discrete per-note transforms, not as continuous tempo changes. Tempo ramps are a fundamentally different feature.

**Risk:** HIGH. Touches the core timing model across 6+ files. Every audio rendering path must be updated.

### 10. REPL Auto-Imports (LOW RISK)

**Implementation:** In `Repl.cs`, after creating the FlowEngine, execute a bootstrap:
```csharp
_engine.Execute("use \"@std\"", "<repl-init>");
```

This loads `std.flow` which already imports `@collections` and `@bars`. Single-line change.

### 11. `--verbose` Flag (ADDITIVE)

**Implementation:**
- `flow-interpreter/Program.cs` -- parse `--verbose` from command-line args
- Thread a `verbose` flag through to `FlowEngine` or `ErrorReporter`
- Add optional diagnostic output at key points: after lexing (token count), after parsing (AST size), during overload resolution (candidates and scores)

**Especially valuable for:** Diagnosing the Sequence overload bug (feature 1).

### 12. Better Error Reporting

**Scattered changes:**
- Ensure all error paths include `SourceLocation` from AST nodes
- Add warnings (not just errors) to `ErrorReporter`
- Specifically: when overload resolution fails, show the actual arg types vs expected parameter types (currently only shows function name)
- When sections produce 0 sequences, warn rather than silently rendering nothing

## Component Boundary Map

```
+------------------+     +------------------+     +------------------+
|    SimpleLexer   |---->|     Parser       |---->|   Interpreter    |
| // comments [3]  |     | tempo ramp [9]   |     | section fix [2]  |
+------------------+     +------------------+     +------------------+
                                                         |
                    +------------------------------------+
                    |                    |                |
            +------v------+    +--------v------+  +------v--------+
            | MusicalCtx  |    | ExprEvaluator |  | StdLib/Audio  |
            | tempo ramp  |    | overload [1]  |  | mix() [6]     |
            | [9]         |    +---------------+  | writeWav [5]  |
            +-------------+           |           | math [4]      |
                                      v           +---------------+
                              +---------------+          |
                              | OverloadRes.  |   +------v--------+
                              | seq fix [1]   |   | Synthesizers  |
                              | verbose [11]  |   | strings [8]   |
                              +---------------+   | organ [8]     |
                                                  | bell [8]      |
                                                  +---------------+
                                                         |
                                                  +------v--------+
                                                  | SongRenderer  |
                                                  | section gain  |
                                                  |   [7]         |
                                                  | tempo ramp [9]|
                                                  +---------------+
```

## Recommended Build Order

Based on dependency analysis and risk (fix bugs first, then additive, then complex):

### Wave 1: Bug Fixes + Diagnostics (unblocks everything)

1. **`--verbose` flag** -- implement first, aids debugging all other changes
2. **Sequence overload resolution** -- CRITICAL bug blocking transforms. Verbose logging helps diagnose
3. **Section bare expressions** -- CRITICAL bug blocking simple section usage
4. **Better error reporting** -- improves DX for all subsequent work

**Rationale:** Verbose logging aids Sequence bug diagnosis. Both bugs block real user scripts. Error reporting improves the feedback loop for all subsequent features.

### Wave 2: Quick Wins (additive, no core changes)

5. **`//` line comments** -- ~5-line lexer change, huge DX improvement
6. **Math functions** -- new file, pure addition, no risk
7. **`exportWav` rename** -- mechanical, alias preserves backward compat
8. **REPL auto-imports** -- ~2-line change

**Rationale:** All isolated, additive, fast to implement, immediately useful. Can be done in any order or parallel.

### Wave 3: Audio Features (additive, moderate scope)

9. **`mix()` buffer layering** -- new function using existing AudioBuffer API
10. **Synth presets** -- new files following INoteSynthesizer pattern, independent of each other
11. **Per-section volume/gain** -- small SongRenderer modification using existing Velocity context

**Rationale:** All use existing audio infrastructure. Synth presets are independent and parallelizable.

### Wave 4: Complex Feature (last, highest risk)

12. **Tempo ramps** -- touches timing model across 6+ files. Do last when everything else is stable.

**Rationale:** Only feature requiring a core assumption change (constant tempo). If it destabilizes the audio pipeline, all other features are already locked in and testable.

## Anti-Patterns to Avoid

### Anti-Pattern: Fixing Overloads by Widening Type Compatibility
**What:** Making SequenceType "compatible with" other types to force matches
**Why bad:** Breaks specificity scoring for ALL Sequence functions, could cause wrong overload selection
**Instead:** Find the actual type mismatch -- likely a Value factory producing the wrong type tag

### Anti-Pattern: Section Fix via Forced Variable Assignment
**What:** Requiring users to assign bare expressions to variables in sections
**Why bad:** Changes the language semantics; bare expressions should work like they do everywhere else
**Instead:** Fix the interpreter's collection logic to capture expression results alongside variables

### Anti-Pattern: Tempo Ramps via Post-Render Time-Stretching
**What:** Render at constant tempo, then stretch the audio buffer
**Why bad:** Introduces pitch artifacts or requires complex phase vocoder
**Instead:** Use a TempoMap that converts beat positions to sample positions during rendering

### Anti-Pattern: Special-Casing Functions in OverloadResolver
**What:** Adding `if (name == "transpose")` branches in the resolver
**Why bad:** Hides the real type system issue; other functions will hit the same bug
**Instead:** Fix at the type/Value level so all functions benefit

## Sources

- `flow-lang/TypeSystem/OverloadResolver.cs` -- overload resolution logic
- `flow-lang/TypeSystem/FunctionSignature.cs` -- Matches() and CalculateSpecificity()
- `flow-lang/TypeSystem/FlowType.cs` -- IsCompatibleWith/CanConvertTo defaults
- `flow-lang/Interpreter/Interpreter.cs:336-377` -- ExecuteSectionDeclaration
- `flow-lang/Interpreter/ExpressionEvaluator.cs:160` -- EvaluateFunctionCall
- `flow-lang/Lexing/SimpleLexer.cs:777-813` -- SkipWhitespaceAndComments
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:182-216` -- SynthesizerFactory
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- rendering pipeline
- `flow-lang/Runtime/MusicalContext.cs` -- context fields
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` -- transpose registration
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` -- vary registration
