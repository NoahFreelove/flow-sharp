# Phase 1: Language Foundations - Context

**Gathered:** 2026-04-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Add `for` and `while` loop constructs, string interpolation, iteration guards, and sequence visualization to the Flow language. All changes are in the interpreter pipeline (Lexer → Parser → AST → Interpreter) plus one new built-in function for visualization. No audio pipeline changes.

</domain>

<decisions>
## Implementation Decisions

### Loop Syntax
- **D-01:** Use for-each style: `for Type varName in collection { body }` — matches Flow's existing typed declaration pattern (`Int x = 5`, `proc name(Int x) { }`)
- **D-02:** While loops use: `while condition { body }` — standard syntax, condition is an expression evaluating to Bool
- **D-03:** No C-style `for(init; cond; step)` — too verbose for a music language, `while` covers the same cases
- **D-04:** Support `break` and `continue` keywords inside both loop types

### String Interpolation
- **D-05:** Use `$"...{expr}..."` syntax with explicit `$` prefix — avoids conflicts with existing `{ }` used in musical context blocks, proc bodies, and section declarations
- **D-06:** Lexer recognizes `$"` as start of interpolated string, switches to a mode that parses `{expr}` segments as embedded expressions
- **D-07:** Nested braces inside interpolated expressions are NOT supported (keep it simple — no `$"x is {if(cond, a, b)}"`)

### Sequence Visualization
- **D-08:** Piano-roll ASCII grid format: pitch on Y axis (note names), time on X axis (beats), notes shown as horizontal bars
- **D-09:** Exposed as `visualize(Sequence) -> Void` built-in function that prints to stdout
- **D-10:** Show bar lines as vertical `|` separators, rests as empty space

### Iteration Guards
- **D-11:** Default hard limit of 10,000 iterations per loop — prevents infinite loops from freezing the REPL
- **D-12:** Configurable via built-in: `setMaxIterations(Int)` to allow legitimate long loops
- **D-13:** When limit is hit, report error via ErrorReporter (soft failure model, consistent with division by zero) and break out of loop

### Claude's Discretion
- Exact ASCII art style for visualization (character choices, spacing, grid density)
- Whether `for` loops support a numeric range shorthand (e.g., `for Int i in range(0, 10)` using existing `range` function)
- Internal implementation of lexer mode stack for string interpolation

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Language Pipeline
- `flow-lang/Lexing/SimpleLexer.cs` — Hand-written lexer; new tokens needed for `for`, `while`, `break`, `continue`, `$"`
- `flow-lang/Parsing/Parser.cs` — Recursive descent parser; add `ParseForStatement`, `ParseWhileStatement`
- `flow-lang/Ast/Statements/` — Existing statement types (record types); add `ForStatement.cs`, `WhileStatement.cs`, `BreakStatement.cs`, `ContinueStatement.cs`
- `flow-lang/Interpreter/Interpreter.cs` — Statement execution; add `ExecuteForStatement`, `ExecuteWhileStatement`
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — Expression evaluation; handle `InterpolatedStringExpression`

### Runtime
- `flow-lang/Runtime/ExecutionContext.cs` — Has `MaxCallDepth` (line 17); add `MaxIterations` following same pattern
- `flow-lang/Runtime/Value.cs` — Value wrapper; `Value.String()` factory for interpolation results

### Standard Library
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Registration point for `visualize` and `setMaxIterations`
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — Signature → lambda mapping

### Existing Patterns
- `flow-lang/Ast/Statements/ProcDeclaration.cs` — Example of statement record type with body
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` — Example of block statement with body
- `flow-lang/Lexing/SimpleLexer.cs` — Token types defined in `TokenType` enum

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ExecutionContext.MaxCallDepth` pattern: exactly the model for iteration guards (counter + const limit + exception)
- `ErrorReporter`: soft failure reporting for iteration limit hits
- `MusicalContextStatement`: block statement pattern (keyword + body) — loops follow the same AST shape
- `TokenType` enum: already has keywords like `Tempo`, `Key`, `Section` — add `For`, `While`, `Break`, `Continue`
- `range` built-in function: already generates integer ranges — works directly with `for x in range(0, 10)`
- `each` built-in function: already iterates over arrays with a lambda — `for` loop is the statement-level equivalent

### Established Patterns
- AST nodes are C# `record` types (immutable, one file per type)
- Parser uses `ParseXxx()` methods with `Match(TokenType.Xxx)` for keywords
- Interpreter dispatches via `switch` on statement type
- Built-in functions registered in `BuiltInFunctions.cs` via `registry.Register(signature, lambda)`

### Integration Points
- `SimpleLexer.cs`: Add new `TokenType` values and keyword recognition
- `Parser.cs`: Add parsing methods, hook into main `ParseStatement()` dispatch
- `Interpreter.cs`: Add execution methods, hook into `ExecuteStatement()` dispatch
- `BuiltInFunctions.cs`: Register `visualize` and `setMaxIterations`

</code_context>

<specifics>
## Specific Ideas

- `for` syntax should feel natural for musicians: `for Note n in melody { (print (str n)) }` — iterate over notes in a sequence
- String interpolation enables debugging: `$"Note {n} at beat {beat}"` — much better than `(print (concat "Note " (str n) " at beat " (str beat)))`
- Visualization should be useful for quick feedback: `melody -> visualize` using the flow operator

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-language-foundations*
*Context gathered: 2026-04-01*
