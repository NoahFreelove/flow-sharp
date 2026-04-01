---
phase: 01-language-foundations
plan: 01
subsystem: language
tags: [for-loop, while-loop, break, continue, iteration-guard, ast, parser, interpreter]

requires:
  - phase: none
    provides: n/a
provides:
  - ForStatement, WhileStatement, BreakStatement, ContinueStatement AST nodes
  - for/while/break/continue/in token types and lexer keywords
  - ParseForStatement and ParseWhileStatement parser methods
  - ExecuteForStatement and ExecuteWhileStatement interpreter methods
  - BreakSignal and ContinueSignal control flow exceptions
  - MaxIterations iteration guard on ExecutionContext (default 10000)
  - setMaxIterations built-in function
affects: [01-language-foundations, string-interpolation, future-loop-dependent-features]

tech-stack:
  added: []
  patterns:
    - "Loop control flow via BreakSignal/ContinueSignal exceptions caught per-loop"
    - "Iteration guard as local counter per loop invocation (not global state)"
    - "RegisterIterationGuard pattern for post-context-creation function registration"

key-files:
  created:
    - flow-lang/Ast/Statements/ForStatement.cs
    - flow-lang/Ast/Statements/WhileStatement.cs
    - flow-lang/Ast/Statements/BreakStatement.cs
    - flow-lang/Ast/Statements/ContinueStatement.cs
    - tests/test_for_loop.flow
    - tests/test_while_loop.flow
    - tests/test_iteration_guard.flow
  modified:
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/std.flow

key-decisions:
  - "Loop control flow uses exception-based signals (BreakSignal/ContinueSignal) caught in each loop's try/catch"
  - "Iteration counter is local per loop call, not a field on ExecutionContext, preventing nested loop interference"
  - "setMaxIterations registered via separate RegisterIterationGuard method called after ExecutionContext creation"

patterns-established:
  - "Loop body parsing: save/restore _inLoop flag for nested loop support"
  - "Post-context registration pattern: RegisterIterationGuard for functions needing ExecutionContext"

requirements-completed: [LANG-01, LANG-02, LANG-04]

duration: 11min
completed: 2026-04-01
---

# Phase 01 Plan 01: Loops and Iteration Guards Summary

**For/while loops with break/continue control flow and configurable iteration guards (default 10000) to prevent runaway loops**

## Performance

- **Duration:** 11 min
- **Started:** 2026-04-01T23:24:51Z
- **Completed:** 2026-04-01T23:35:26Z
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments
- For-each loops iterate over arrays with typed variable binding and proper scope isolation
- While loops evaluate Bool conditions each iteration with break/continue support
- Iteration guard defaults to 10000, configurable via setMaxIterations, reports soft error via ErrorReporter
- break/continue validated at parse time -- rejected outside loop bodies

## Task Commits

Each task was committed atomically:

1. **Task 1: Add loop AST nodes, tokens, lexer keywords, and parser rules** - `2a33178` (feat)
2. **Task 2: Add loop execution, iteration guards, setMaxIterations, and test scripts** - `c30ce0d` (feat)

## Files Created/Modified
- `flow-lang/Ast/Statements/ForStatement.cs` - For-each loop AST record
- `flow-lang/Ast/Statements/WhileStatement.cs` - While loop AST record
- `flow-lang/Ast/Statements/BreakStatement.cs` - Break statement AST record
- `flow-lang/Ast/Statements/ContinueStatement.cs` - Continue statement AST record
- `flow-lang/Lexing/TokenType.cs` - Added For, While, Break, Continue, In token types
- `flow-lang/Lexing/SimpleLexer.cs` - Added keyword mappings for loop tokens
- `flow-lang/Parsing/Parser.cs` - Added ParseForStatement, ParseWhileStatement, _inLoop validation
- `flow-lang/Interpreter/Interpreter.cs` - Added ExecuteForStatement, ExecuteWhileStatement, BreakSignal, ContinueSignal
- `flow-lang/Runtime/ExecutionContext.cs` - Added MaxIterations property with default 10000
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - Added RegisterIterationGuard with setMaxIterations
- `flow-lang/Core/FlowEngine.cs` - Added RegisterIterationGuard calls in both constructors
- `flow-lang/std.flow` - Added internal proc declaration for setMaxIterations
- `flow-interpreter/flow-interpreter.csproj` - Fixed pre-existing erroneous Release build artifact references
- `tests/test_for_loop.flow` - For loop integration tests (basic, empty, nested, single, scoping)
- `tests/test_while_loop.flow` - While loop integration tests (basic, condition var, false, countdown, break, continue)
- `tests/test_iteration_guard.flow` - Iteration guard tests (default 10000, setMaxIterations 100)

## Decisions Made
- Loop control flow uses exception-based signals (BreakSignal/ContinueSignal) caught per-loop, following standard interpreter pattern
- Iteration counter is local per loop invocation to prevent nested loop interference
- setMaxIterations registered via separate RegisterIterationGuard method because InternalFunctionRegistry is created before ExecutionContext

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed pre-existing csproj build error**
- **Found during:** Task 1 (build verification)
- **Issue:** flow-interpreter.csproj referenced non-existent files in bin/Release/net9.0/ (std.flow, collections.flow), causing build failure in worktree
- **Fix:** Removed erroneous ItemGroup with Release build artifact references
- **Files modified:** flow-interpreter/flow-interpreter.csproj
- **Verification:** dotnet build succeeds with 0 errors
- **Committed in:** 2a33178 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed test files using wrong comment syntax**
- **Found during:** Task 2 (test execution)
- **Issue:** Test files used // comments which Flow doesn't support; Flow uses Note: prefix for comments
- **Fix:** Replaced all // comments with Note: prefix in test files
- **Files modified:** tests/test_for_loop.flow, tests/test_while_loop.flow, tests/test_iteration_guard.flow
- **Committed in:** c30ce0d (Task 2 commit)

**3. [Rule 1 - Bug] Fixed test variable name collision with built-in function**
- **Found during:** Task 2 (test execution)
- **Issue:** Variable name `empty` in test_for_loop.flow conflicted with built-in `empty` function
- **Fix:** Renamed variable to `emptyArr`
- **Files modified:** tests/test_for_loop.flow
- **Committed in:** c30ce0d (Task 2 commit)

**4. [Rule 1 - Bug] Fixed test using unsupported comparison operator syntax**
- **Found during:** Task 2 (test execution)
- **Issue:** While loop tests used `count < 5` syntax but Flow uses function call syntax `(lt count 5)` for comparisons
- **Fix:** Rewrote all comparison expressions to use `(lt ...)`, `(gt ...)` function call syntax
- **Files modified:** tests/test_while_loop.flow
- **Committed in:** c30ce0d (Task 2 commit)

**5. [Rule 3 - Blocking] Added missing internal proc declaration for setMaxIterations**
- **Found during:** Task 2 (test execution)
- **Issue:** setMaxIterations was registered in C# but not declared as internal proc in std.flow, causing "Function not found" error
- **Fix:** Added `internal proc setMaxIterations (Int: limit)` to flow-lang/std.flow
- **Files modified:** flow-lang/std.flow
- **Committed in:** c30ce0d (Task 2 commit)

**6. [Rule 1 - Bug] Used DeclareVariable instead of SetVariable for loop variable**
- **Found during:** Task 2 (test execution)
- **Issue:** SetVariable only updates existing variables; loop variable is new in each iteration's scope
- **Fix:** Changed to DeclareVariable which creates new variables in the current frame
- **Files modified:** flow-lang/Interpreter/Interpreter.cs
- **Committed in:** c30ce0d (Task 2 commit)

---

**Total deviations:** 6 auto-fixed (3 bugs, 2 blocking, 1 blocking pre-existing)
**Impact on plan:** All auto-fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all functionality is fully wired.

## Next Phase Readiness
- Loop constructs ready for use in subsequent phases
- For loops work with any array type (Int[], String[], etc.)
- Iteration guards prevent REPL hangs from infinite loops

## Self-Check: PASSED

All 7 created files verified present. Both task commits (2a33178, c30ce0d) verified in git log.

---
*Phase: 01-language-foundations*
*Completed: 2026-04-01*
