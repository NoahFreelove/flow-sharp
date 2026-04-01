# Phase 1: Language Foundations - Research

**Researched:** 2026-03-29
**Domain:** Interpreter pipeline extension (Lexer, Parser, AST, Interpreter, built-in functions)
**Confidence:** HIGH

## Summary

This phase adds four tightly-scoped features to the Flow interpreter: `for` and `while` loop constructs, string interpolation with `$"...{expr}..."` syntax, iteration guards, and ASCII piano-roll visualization. All changes follow the existing interpreter pipeline pattern (Lexer -> Parser -> AST -> Interpreter) and require no new external dependencies.

The codebase has strong, consistent patterns for each pipeline stage. New keywords go through `TokenType` enum + lexer keyword map + parser dispatch + AST record + interpreter switch case. The `MusicalContextStatement` (block with body) is the closest pattern analog for loops. The `ErrorReporter` soft failure model and `MaxCallDepth` constant in `ExecutionContext` provide the exact template for iteration guards. String interpolation is the most technically complex feature because it requires a new lexer mode to parse embedded expressions within strings.

**Primary recommendation:** Follow existing pipeline patterns exactly. Each feature touches the same 5-6 files in the same way. The visualization built-in is a standalone function registration with no pipeline changes.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Use for-each style: `for Type varName in collection { body }` -- matches Flow's existing typed declaration pattern
- **D-02:** While loops use: `while condition { body }` -- standard syntax, condition evaluates to Bool
- **D-03:** No C-style `for(init; cond; step)` -- `while` covers the same cases
- **D-04:** Support `break` and `continue` keywords inside both loop types
- **D-05:** Use `$"...{expr}..."` syntax with explicit `$` prefix -- avoids conflicts with existing `{ }` usage
- **D-06:** Lexer recognizes `$"` as start of interpolated string, switches to a mode that parses `{expr}` segments
- **D-07:** Nested braces inside interpolated expressions are NOT supported (keep it simple)
- **D-08:** Piano-roll ASCII grid format: pitch on Y axis, time on X axis, notes as horizontal bars
- **D-09:** Exposed as `visualize(Sequence) -> Void` built-in function that prints to stdout
- **D-10:** Show bar lines as vertical `|` separators, rests as empty space
- **D-11:** Default hard limit of 10,000 iterations per loop
- **D-12:** Configurable via built-in: `setMaxIterations(Int)`
- **D-13:** When limit is hit, report error via ErrorReporter (soft failure) and break out of loop

### Claude's Discretion
- Exact ASCII art style for visualization (character choices, spacing, grid density)
- Whether `for` loops support a numeric range shorthand (e.g., `for Int i in range(0, 10)` using existing `range` function)
- Internal implementation of lexer mode stack for string interpolation

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LANG-01 | User can iterate over collections with `for` loop construct | New AST node `ForStatement`, parser rule, interpreter execution; follows `MusicalContextStatement` block pattern |
| LANG-02 | User can write conditional loops with `while` construct | New AST node `WhileStatement`, same pipeline as `for`; iteration guard applies here |
| LANG-03 | User can use string interpolation to embed expressions in strings | New `InterpolatedStringExpression` AST node, lexer mode for `$"...{expr}..."`, evaluator concatenates parts |
| LANG-04 | User can add iteration guards to prevent infinite loops in REPL | `MaxIterations` field on `ExecutionContext` following `MaxCallDepth` pattern; `setMaxIterations` built-in |
| VIS-01 | User can visualize sequences as piano-roll ASCII art | `visualize` built-in function registered in `BuiltInFunctions.cs`; uses `SequenceData.ToTimeline()` + `MusicalNoteData` pitch/duration + `TransformFunctions.ToMidi()` |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Runtime | Already in use; no changes needed |
| C# 13 | Latest | Language | Record types, pattern matching already used throughout |

### Supporting
No new libraries needed. All features are hand-rolled interpreter extensions.

### Alternatives Considered
None -- all features are standard interpreter work with no library candidates.

## Architecture Patterns

### Pipeline Extension Pattern (used by ALL features in this phase)

Every new language feature follows this exact sequence:

```
1. TokenType.cs       -- Add enum values (e.g., For, While, Break, Continue)
2. SimpleLexer.cs     -- Add keyword recognition in ScanIdentifierOrKeyword switch
3. Parser.cs          -- Add ParseXxxStatement() method, hook into ParseStatement()
4. Ast/Statements/    -- Add XxxStatement.cs record type
5. Interpreter.cs     -- Add ExecuteXxx() method, hook into ExecuteStatement() switch
```

For expression-level features (string interpolation):
```
1. TokenType.cs       -- Add InterpolatedStringStart (or similar)
2. SimpleLexer.cs     -- Detect $" and produce token sequence
3. Ast/Expressions/   -- Add InterpolatedStringExpression.cs
4. ExpressionEvaluator.cs -- Add evaluation case in Evaluate() switch
```

### Recommended New Files
```
flow-lang/
  Ast/
    Statements/
      ForStatement.cs              # for Type var in collection { body }
      WhileStatement.cs            # while condition { body }
      BreakStatement.cs            # break
      ContinueStatement.cs         # continue
    Expressions/
      InterpolatedStringExpression.cs  # $"text {expr} text"
  StandardLibrary/
    VisualizationFunctions.cs      # visualize(Sequence) built-in
```

### Pattern 1: Block Statement with Body (for/while loops)

**What:** AST record with a body (list of statements) executed in a scoped frame.
**When to use:** Any construct that has `keyword ... { body }` structure.
**Example (existing pattern):**
```csharp
// Source: flow-lang/Ast/Statements/MusicalContextStatement.cs
public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,
    Expression? Value2,
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

**ForStatement analog:**
```csharp
// New file: flow-lang/Ast/Statements/ForStatement.cs
public record ForStatement(
    SourceLocation Location,
    FlowType ElementType,       // The declared type (e.g., Int, Note)
    string VariableName,        // The loop variable name
    Expression Collection,      // The iterable expression
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

**WhileStatement analog:**
```csharp
// New file: flow-lang/Ast/Statements/WhileStatement.cs
public record WhileStatement(
    SourceLocation Location,
    Expression Condition,       // Must evaluate to Bool
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

### Pattern 2: Loop Control Flow (break/continue)

**What:** `break` and `continue` need a signaling mechanism from deep in the statement execution stack back to the loop executor.
**When to use:** Any control flow that exits a block non-locally.
**Recommended approach:** Use C# exceptions as control flow signals (same pattern as `_returnValue` but for loops). This is the standard approach for tree-walking interpreters.

```csharp
// Signal exceptions (not error conditions -- control flow)
public class BreakSignal : Exception { }
public class ContinueSignal : Exception { }
```

The loop execution methods catch these:
```csharp
private void ExecuteForStatement(ForStatement stmt)
{
    var collection = _evaluator.Evaluate(stmt.Collection);
    var items = collection.As<List<Value>>();
    int iterations = 0;

    foreach (var item in items)
    {
        if (++iterations > _context.MaxIterations)
        {
            _errorReporter.ReportError($"Iteration limit of {_context.MaxIterations} exceeded");
            break;
        }
        _context.PushFrame();
        try
        {
            _context.CurrentFrame.SetVariable(stmt.VariableName, item);
            foreach (var bodyStmt in stmt.Body)
            {
                ExecuteStatement(bodyStmt);
                if (_returnValue != null) return;
            }
        }
        catch (BreakSignal) { break; }
        catch (ContinueSignal) { continue; }
        finally { _context.PopFrame(); }
    }
}
```

**Alternative considered:** Using a boolean flag (`_breakRequested`). This requires checking after every statement in every body, which is more invasive. Exception-based control flow is cleaner for tree-walking interpreters because it unwinds naturally through nested blocks.

### Pattern 3: Lexer Mode for String Interpolation

**What:** The lexer needs to emit a sequence of tokens for `$"text {expr} more text"` that the parser can assemble into an `InterpolatedStringExpression`.
**When to use:** When the lexer needs context-dependent behavior.

**Approach (two viable options):**

**Option A -- Lexer emits multi-token sequence:**
When `$"` is encountered, the lexer produces:
- `InterpolatedStringStart` token
- `StringLiteral` for text segments
- Expression tokens for `{...}` segments (re-entering normal lexing)
- `InterpolatedStringEnd` token

This is the cleanest approach but requires the lexer to track a mode stack.

**Option B -- Lexer produces a single compound token:**
The lexer scans the entire `$"..."`, splits into parts (string segments + expression source strings), stores them as the token's `Value`. The parser then sub-lexes/sub-parses the expression parts.

**Recommendation:** Option A (multi-token) is more consistent with how the parser works. The lexer already handles mode-like behavior (e.g., note stream detection). The key implementation detail: when inside an interpolated string, `{` switches to normal expression lexing and `}` returns to string scanning. Since D-07 forbids nested braces, the lexer just needs to track one level.

```csharp
// In NextToken(), before falling through to identifiers:
if (c == '$' && PeekNext() == '"')
{
    return ScanInterpolatedString(start);
}
```

**InterpolatedStringExpression:**
```csharp
public record InterpolatedStringExpression(
    SourceLocation Location,
    IReadOnlyList<Expression> Parts  // Alternating StringLiterals and expressions
) : Expression(Location);
```

### Pattern 4: Built-in Function Registration

**What:** Registering `visualize` and `setMaxIterations` as built-in functions.
**Example (existing pattern):**
```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs
var printSignature = new FunctionSignature("print", [StringType.Instance]);
registry.Register("print", printSignature, stdlib.Print);
```

**For visualize:**
```csharp
var visualizeSignature = new FunctionSignature("visualize", [SequenceType.Instance]);
registry.Register("visualize", visualizeSignature, VisualizationFunctions.Visualize);
```

### Anti-Patterns to Avoid
- **Do NOT add a new FlowType for loops** -- loops are statements, not values. They produce no type.
- **Do NOT modify the existing `ScanString` method** for interpolation -- add a separate `ScanInterpolatedString` to keep concerns separated.
- **Do NOT use `_returnValue` for break/continue signaling** -- return is function-level, break/continue are loop-level. Mixing them creates subtle bugs with nested loops inside procs.
- **Do NOT iterate by index over arrays** -- the `for` loop iterates over a Value that wraps a `List<Value>`. Use the existing array unpacking pattern.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MIDI number from note name | Pitch calculation for visualizer | `TransformFunctions.ToMidi()` | Already exists at line 34 of TransformFunctions.cs; handles alterations correctly |
| Sequence timeline | Bar offset calculation for visualizer | `SequenceData.ToTimeline()` | Already exists; returns `List<(BarData bar, double offsetBeats)>` |
| Note duration in beats | Duration calculation for visualizer | `MusicalNoteData.GetBeats(timeSigDenominator)` | Already exists; handles dotted notes |
| Array iteration | Collection traversal for `for` loop | Existing `List<Value>` in Value.Data | Arrays are already stored as `List<Value>` |

## Common Pitfalls

### Pitfall 1: Break/Continue Outside Loops
**What goes wrong:** User writes `break` or `continue` outside a loop body; exception propagates to top level and crashes.
**Why it happens:** `BreakSignal`/`ContinueSignal` exceptions are only caught by loop executors.
**How to avoid:** Parser should track whether it's inside a loop body (bool flag `_inLoop`). Report a parse error if `break`/`continue` appears outside a loop.
**Warning signs:** Unhandled exception in REPL after typing `break`.

### Pitfall 2: Iteration Guard Not Resetting Between Loops
**What goes wrong:** Sequential loops share a single counter; second loop gets fewer iterations.
**Why it happens:** Counter placed at wrong scope (ExecutionContext level instead of per-loop).
**How to avoid:** The iteration counter MUST be local to each `ExecuteForStatement`/`ExecuteWhileStatement` call, not a field on ExecutionContext. Only the `MaxIterations` limit is on ExecutionContext.
**Warning signs:** `for` loop after a long `while` loop terminates early.

### Pitfall 3: String Interpolation vs Musical Context Braces
**What goes wrong:** `$"key is {key}"` -- the lexer might confuse `{key}` with the start of a musical context block.
**Why it happens:** `{` and `}` are already meaningful in many contexts (proc bodies, musical context blocks, section bodies).
**How to avoid:** The `$"` prefix is the explicit disambiguator (D-05). The lexer only enters interpolation mode when it sees `$"` -- regular `"strings"` remain unchanged. Inside interpolation mode, `{` always means "start expression" and `}` always means "end expression".
**Warning signs:** Parse errors on strings containing key names like "key" or "tempo".

### Pitfall 4: For-Loop Variable Scoping
**What goes wrong:** Loop variable leaks into outer scope or mutations inside the body persist across iterations.
**Why it happens:** Variable set in the wrong frame.
**How to avoid:** Push a new frame for EACH iteration (not just once for the entire loop). Set the loop variable in the new frame. Pop after each iteration. This matches the existing `PushFrame`/`PopFrame` pattern used by `ExecuteMusicalContext`.
**Warning signs:** Variable defined in loop body is accessible after loop ends.

### Pitfall 5: Visualizer Assumes Fixed Time Signature
**What goes wrong:** Sequences with changing time signatures render incorrectly.
**Why it happens:** Using a fixed beat grid instead of reading per-bar time signatures.
**How to avoid:** Use `SequenceData.ToTimeline()` which already handles per-bar offsets. Read each bar's `TimeSignature` property for bar line placement. The `BarData.MusicalNotes` list + `MusicalNoteData.GetBeats(timeSigDenominator)` gives correct note durations per bar.
**Warning signs:** Notes extending past bar lines in the ASCII output.

### Pitfall 6: Empty Collection in For Loop
**What goes wrong:** For loop over empty array causes error instead of silently skipping.
**Why it happens:** Not handling empty `List<Value>`.
**How to avoid:** If the collection evaluates to an empty list, the loop body simply never executes. No special case needed -- `foreach` over empty list naturally does nothing.

### Pitfall 7: While Loop Condition Type
**What goes wrong:** Non-boolean condition silently treated as truthy/falsy.
**Why it happens:** Flow is statically typed but the while condition might not be checked.
**How to avoid:** The while loop executor should verify the condition evaluates to a `Bool` typed Value. If not, report a type error via `ErrorReporter`.
**Warning signs:** `while 1 { ... }` running instead of erroring.

## Code Examples

### Keyword Registration in Lexer (verified pattern)
```csharp
// Source: flow-lang/Lexing/SimpleLexer.cs lines 459-489
// Add to the switch expression:
"for" => TokenType.For,
"while" => TokenType.While,
"break" => TokenType.Break,
"continue" => TokenType.Continue,
"in" => TokenType.In,
```

### Parser Statement Dispatch (verified pattern)
```csharp
// Source: flow-lang/Parsing/Parser.cs lines 68-136
// Add before variable declaration check:
if (Match(TokenType.For))
    return ParseForStatement();
if (Match(TokenType.While))
    return ParseWhileStatement();
if (Match(TokenType.Break))
    return new BreakStatement(PreviousToken.Location);
if (Match(TokenType.Continue))
    return new ContinueStatement(PreviousToken.Location);
```

### Interpreter Statement Dispatch (verified pattern)
```csharp
// Source: flow-lang/Interpreter/Interpreter.cs lines 70-107
// Add cases:
case ForStatement forStmt:
    ExecuteForStatement(forStmt);
    break;
case WhileStatement whileStmt:
    ExecuteWhileStatement(whileStmt);
    break;
case BreakStatement:
    throw new BreakSignal();
case ContinueStatement:
    throw new ContinueSignal();
```

### MaxCallDepth Pattern (template for MaxIterations)
```csharp
// Source: flow-lang/Runtime/ExecutionContext.cs lines 16-17
private int _callDepth = 0;
private const int MaxCallDepth = 1000;

// Analog for iterations:
private int _maxIterations = 10000;
public int MaxIterations
{
    get => _maxIterations;
    set => _maxIterations = value > 0 ? value : throw new ArgumentException("MaxIterations must be positive");
}
```

### Visualization: MIDI Pitch Conversion (existing utility)
```csharp
// Source: flow-lang/StandardLibrary/Transforms/TransformFunctions.cs line 34
private static int ToMidi(char noteName, int octave, int alteration)
// Converts note name (C-B), octave (0-9), alteration (-2 to +2) to MIDI number (0-127)
```

### Visualization: Sequence Timeline (existing utility)
```csharp
// Source: flow-lang/TypeSystem/SpecialTypes/SequenceType.cs lines 46-61
public List<(BarData bar, double offsetBeats)> ToTimeline()
// Returns each bar with its beat offset -- use for X-axis positioning
```

### ASCII Piano Roll Visualization (recommended design)
```csharp
// Recommended approach for visualize(Sequence)
public static Value Visualize(IReadOnlyList<Value> args)
{
    var sequence = args[0].As<SequenceData>();
    var timeline = sequence.ToTimeline();

    // 1. Collect all notes with (midiPitch, startBeat, durationBeats)
    // 2. Find min/max pitch for Y-axis range
    // 3. Build grid: rows = pitches (high to low), columns = beat subdivisions
    // 4. Fill grid: '#' or block chars for note durations
    // 5. Add Y-axis labels (note names), bar lines ('|')
    // 6. Print to stdout

    // Example output:
    // E4 |####    |        |
    // D4 |    ####|        |
    // C4 |        |########|
    //    Beat 1   Beat 5   Beat 9

    Console.WriteLine(grid.ToString());
    return Value.Void();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Functional-only iteration (`each`, `map`, `reduce`) | Adding imperative loops (`for`, `while`) | This phase | Users get familiar iteration patterns alongside functional style |
| Manual string concatenation (`concat("a", str(x), "b")`) | String interpolation (`$"a {x} b"`) | This phase | Dramatically simpler debugging output |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow script execution (no unit test framework) |
| Config file | none -- tests are .flow scripts in `tests/` |
| Quick run command | `dotnet run --project flow-interpreter tests/test_FILE.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LANG-01 | for loop iterates over collection | integration | `dotnet run --project flow-interpreter tests/test_for_loop.flow` | No -- Wave 0 |
| LANG-02 | while loop with condition + termination | integration | `dotnet run --project flow-interpreter tests/test_while_loop.flow` | No -- Wave 0 |
| LANG-03 | string interpolation with embedded expressions | integration | `dotnet run --project flow-interpreter tests/test_string_interpolation.flow` | No -- Wave 0 |
| LANG-04 | iteration guard halts runaway loop | integration | `dotnet run --project flow-interpreter tests/test_iteration_guard.flow` | No -- Wave 0 |
| VIS-01 | visualize prints ASCII piano roll | integration | `dotnet run --project flow-interpreter tests/test_visualization.flow` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet run --project flow-interpreter tests/test_FEATURE.flow`
- **Per wave merge:** Full test suite (all `tests/test_*.flow` files)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/test_for_loop.flow` -- covers LANG-01 (for-each over arrays, notes, nested loops, break/continue)
- [ ] `tests/test_while_loop.flow` -- covers LANG-02 (conditional loop, counter, break/continue)
- [ ] `tests/test_string_interpolation.flow` -- covers LANG-03 (expressions in strings, edge cases)
- [ ] `tests/test_iteration_guard.flow` -- covers LANG-04 (default limit, setMaxIterations, soft error)
- [ ] `tests/test_visualization.flow` -- covers VIS-01 (visualize a sequence, verify no crash)

## Project Constraints (from CLAUDE.md)

- **.NET 9 targeting net9.0** -- all new code must target this
- **C# conventions:** File-scoped namespaces, nullable reference types, `record` types for AST nodes, pattern matching for dispatch
- **Namespace conventions:** `FlowLang.*` for library code
- **AST immutability:** All AST nodes must be `record` types
- **No new external dependencies** for this phase
- **Existing test suite must pass:** All 70+ existing `.flow` test scripts must continue to work
- **Error accumulation:** Use `ErrorReporter` for soft failures, not exceptions
- **Adding built-in functions:** Follow the registration pattern in `BuiltInFunctions.cs` with `FunctionSignature` + `registry.Register()`

## Sources

### Primary (HIGH confidence)
- `flow-lang/Lexing/SimpleLexer.cs` -- Verified lexer keyword registration pattern, string scanning, token boundary chars
- `flow-lang/Lexing/TokenType.cs` -- Verified full enum (82 values), no existing For/While/Break/Continue
- `flow-lang/Parsing/Parser.cs` -- Verified ParseStatement dispatch, ParseMusicalContextStatement block pattern
- `flow-lang/Interpreter/Interpreter.cs` -- Verified ExecuteStatement switch, ExecuteMusicalContext frame push/pop
- `flow-lang/Runtime/ExecutionContext.cs` -- Verified MaxCallDepth pattern (line 17), PushFrame/PopFrame
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` -- Verified block statement record pattern
- `flow-lang/Ast/Statements/ProcDeclaration.cs` -- Verified statement record with body
- `flow-lang/TypeSystem/SpecialTypes/SequenceType.cs` -- Verified SequenceData.ToTimeline() for visualization
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` -- Verified MusicalNoteData fields (pitch, duration, rest)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` -- Verified ToMidi() at line 34
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` -- Verified function registration pattern
- `flow-lang/TypeSystem/FunctionSignature.cs` -- Verified signature record structure

### Secondary (MEDIUM confidence)
- CLAUDE.md project conventions and architecture documentation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, fully understood existing codebase
- Architecture: HIGH -- all patterns verified by reading source files directly
- Pitfalls: HIGH -- based on verified code structure and common interpreter implementation issues

**Research date:** 2026-03-29
**Valid until:** 2026-04-29 (stable -- no external dependency churn)
