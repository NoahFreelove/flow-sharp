using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Ast.Expressions;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using System.Numerics;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Interpreter;

/// <summary>
/// Signal exception thrown by break statements to exit the innermost loop.
/// </summary>
public class BreakSignal : Exception { }

/// <summary>
/// Signal exception thrown by continue statements to skip to the next loop iteration.
/// </summary>
public class ContinueSignal : Exception { }

/// <summary>
/// Exceptions that carry control flow or a deliberate abort through the interpreter
/// and must not be converted into accumulated diagnostics.
/// </summary>
internal static class ControlFlow
{
    public static bool IsSignal(Exception ex) =>
        ex is BreakSignal or ContinueSignal or OperationCanceledException
            or StandardLibrary.TestFramework.AssertionException;
}

/// <summary>
/// Main interpreter for executing Flow AST.
/// </summary>
public class Interpreter : IFunctionInvoker
{
    private readonly RuntimeContext _context;
    private readonly ErrorReporter _errorReporter;
    private readonly ExpressionEvaluator _evaluator;
    private readonly ModuleLoader _moduleLoader;
    private Value? _returnValue;
    private Value? _lastExpressionValue;
    private int _recursionDepth = 0;
    private const int MaxRecursionDepth = 1000;

    /// <summary>
    /// Audit §2.3 — true when we are executing inside a user-proc call (where an
    /// explicit <c>return X</c> legitimately propagates up to the proc boundary).
    /// At <c>_recursionDepth == 0</c> a <c>return</c> is at top level (or inside a
    /// definitional section/context/live block) where it must NOT silently skip the
    /// rest of the program — those sites report a charitable diagnostic and clear
    /// the flag instead of leaking it.
    /// </summary>
    public bool InsideProcCall => _recursionDepth > 0;

    /// <summary>
    /// Audit §2.3 — saves and clears the pending return flag. Used by the
    /// section-call dispatcher in <see cref="ExpressionEvaluator"/> to fence a
    /// parameterized section's body re-execution: a <c>return</c> inside the called
    /// section must NOT become the enclosing proc's return value (mirrors the
    /// save/restore discipline in <see cref="ExecuteUserFunctionWithCaptures"/>).
    /// </summary>
    public Value? SaveAndClearReturnValue()
    {
        var saved = _returnValue;
        _returnValue = null;
        return saved;
    }

    /// <summary>
    /// Audit §2.3 — restores a previously saved return flag (companion to
    /// <see cref="SaveAndClearReturnValue"/>). Also consumes (clears + reports) any
    /// return that leaked out of the fenced body: a <c>return</c> inside a called
    /// section is a composer error, not a way to abort song rendering.
    /// </summary>
    public void RestoreReturnValueAfterSection(Value? saved, Core.SourceLocation location)
    {
        if (_returnValue != null)
        {
            _errorReporter.ReportError(
                "'return' is not allowed inside a section body — a section is a definition, not a function. The return was ignored.",
                location);
        }
        _returnValue = saved;
    }

    /// <summary>
    /// Audit §2.3 — clears a return flag that leaked out of a definitional/context
    /// block body (section declaration, live block, or a top-level musical-context /
    /// tuning block) so the top-of-<see cref="ExecuteStatement"/> guard does not
    /// silently skip every subsequent statement of the program. Inside a proc call a
    /// <c>return</c> in a transparent context block must still propagate, so callers
    /// only invoke this when the return must NOT escape (definitional sites always;
    /// context/tuning blocks only at top level).
    /// </summary>
    public void ClearLeakedReturn(string blockKind, Core.SourceLocation location) =>
        DiscardPendingReturn(
            $"'return' is not allowed inside a {blockKind} block at top level — the return was ignored so the rest of the program still runs.",
            location);

    /// <summary>True while a <c>return</c> is propagating to its proc boundary.</summary>
    public bool HasPendingReturn => _returnValue != null;

    /// <summary>
    /// Reports <paramref name="message"/> and clears a pending return, for bodies a
    /// return may not leave (definitions). No-op when nothing is pending.
    /// </summary>
    public void DiscardPendingReturn(string message, Core.SourceLocation location)
    {
        if (_returnValue == null)
            return;
        _errorReporter.ReportError(message, location);
        _returnValue = null;
    }

    /// <summary>The execution context this interpreter runs in.</summary>
    public RuntimeContext Context => _context;

    /// <summary>Where execution errors are reported.</summary>
    public ErrorReporter Errors => _errorReporter;

    /// <summary>The evaluator for this interpreter's expressions.</summary>
    public ExpressionEvaluator Evaluator => _evaluator;

    public Interpreter(RuntimeContext context, ErrorReporter errorReporter, ModuleLoader? moduleLoader = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _evaluator = new ExpressionEvaluator(context, errorReporter, this);
        _moduleLoader = moduleLoader ?? new ModuleLoader(errorReporter);

        // Wire up the invoker for higher-order functions
        _context.Invoker = this;
    }

    /// <summary>
    /// Gets the last expression value from the most recent execution (for REPL).
    /// </summary>
    public Value? GetLastExpressionValue() => _lastExpressionValue;

    /// <summary>
    /// Phase 36 Plan 36-10 — IFunctionInvoker contract; exposes the last
    /// evaluated expression value to the ExpressionEvaluator's section-call
    /// dispatcher so bare-expression sequences emitted by a parameterized
    /// section body can be captured at call time.
    /// </summary>
    public Value? LastExpressionValue => _lastExpressionValue;

    /// <summary>
    /// Executes a program.
    /// </summary>
    public void Execute(Program program)
    {
        _lastExpressionValue = null;  // Clear previous value
        // Audit §2.3 — reset _returnValue at the top of every Execute. The
        // Interpreter is long-lived (one instance per FlowEngine, reused across
        // REPL evals), and a leaked return flag from a previous eval would make
        // the top-of-ExecuteStatement guard silently skip every statement of the
        // next eval. ReturnStatement sets the flag; nothing else should carry it
        // across an Execute boundary.
        _returnValue = null;

        foreach (var statement in program.Statements)
        {
            ExecuteStatement(statement);
            // sweep-0614: a bare top-level `return` (or a `return` that
            // propagated out of a top-level for/while loop) sets _returnValue
            // with no proc on the stack to consume it. Without this, the
            // top-of-ExecuteStatement guard silently skips EVERY subsequent
            // top-level statement — lost output / lost audio with no
            // diagnostic. Mirror the ClearLeakedReturn discipline already
            // applied to musical-context / tuning / live / section blocks:
            // report a charitable advisory and clear the flag so the rest of
            // the program still runs. Gated on !InsideProcCall so in-proc
            // returns are untouched (Execute only runs at top level anyway,
            // but the guard documents intent).
            if (_returnValue != null && !InsideProcCall)
            {
                _errorReporter.ReportError(
                    "'return' at top level is not allowed — 'return' only exits a proc. " +
                    "The return was ignored so the rest of the program still runs.",
                    statement.Location);
                _returnValue = null;
            }
        }
    }

    /// <summary>
    /// Executes a single statement.
    /// </summary>
    public void ExecuteStatement(Statement stmt)
    {
        if (_returnValue != null)
            return; // Already returned
        // AUDIT-VERIFIED 2026-04-18: C2 — Dismissed: _returnValue only set by ReturnStatement; guard is correct (tests/spike/c2-return-value-short-circuit.flow)

        // Error accumulation: an internal exception inside one statement is reported
        // at that statement and the program continues, instead of escaping to
        // FlowEngine's catch-all as a location-less "Unexpected error" that ends the run.
        try
        {
            ExecuteStatementCore(stmt);
        }
        catch (Exception ex) when (!ControlFlow.IsSignal(ex))
        {
            _errorReporter.ReportError(ex.Message, stmt.Location);
        }
    }

    private void ExecuteStatementCore(Statement stmt)
    {
        switch (stmt)
        {
            case ProcDeclaration proc:
                ExecuteProcDeclaration(proc);
                break;

            case VariableDeclaration varDecl:
                ExecuteVariableDeclaration(varDecl);
                break;

            case TupleDestructureStatement destruct:
                ExecuteTupleDestructure(destruct);
                break;

            case AssignmentStatement assignment:
                ExecuteAssignment(assignment);
                break;

            case ReturnStatement ret:
                ExecuteReturn(ret);
                break;

            case ImportStatement import:
                ExecuteImport(import);
                break;

            // Phase 43 Plan 43-03 D-05: ModuleDeclarationStatement is consumed by
            // ModuleLoader BEFORE Interpreter.Execute returns (the loader inspects
            // program.Statements[0] post-Execute to register the module name).
            // At execute-time the statement is metadata-only — no runtime action
            // required. We add an explicit arm so the default `NotSupportedException`
            // branch below does not fire when the program ITSELF carries a
            // `module <name>` declaration (top-level scripts or REPL evals).
            case ModuleDeclarationStatement:
                break;

            case ExpressionStatement exprStmt:
                var value = _evaluator.Evaluate(exprStmt.Expression);
                _lastExpressionValue = value;  // Store for REPL
                break;

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

            default:
                // Domain statements (sections, context/tuning/live blocks) execute
                // through the context's bindings; without one they are reported.
                if (!_context.Bindings.TryExecute(stmt, this))
                    _errorReporter.ReportError(
                        $"{DomainBindings.DescribeConstruct(stmt.GetType())} is not available: no library in this session provides it",
                        stmt.Location);
                break;
        }
    }


    private void ExecuteForStatement(ForStatement stmt)
    {
        var collectionValue = _evaluator.Evaluate(stmt.Collection);
        var items = collectionValue.Data as List<Value>;
        if (items == null)
        {
            _errorReporter.ReportError($"Cannot iterate over {collectionValue.Type.Name}; expected an array", stmt.Location);
            return;
        }
        int iterations = 0;
        // break-control (0615): track dynamic loop-nesting depth so the `(break)`
        // call-position builtin knows it is inside a loop. Balanced via finally so a
        // BreakSignal / ContinueSignal / return unwinding the body never leaks depth.
        _context.LoopDepth++;
        try
        {
            foreach (var item in items)
            {
                _context.Session.ThrowIfCancelled();
                if (++iterations > _context.MaxIterations)
                {
                    _errorReporter.ReportError($"Iteration limit of {_context.MaxIterations} exceeded in for loop", stmt.Location);
                    break;
                }
                _context.PushFrame();
                try
                {
                    _context.CurrentFrame.DeclareVariable(stmt.VariableName, item);
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
        finally { _context.LoopDepth--; }
    }

    private void ExecuteWhileStatement(WhileStatement stmt)
    {
        int iterations = 0;
        // break-control (0615): see ExecuteForStatement — same balanced depth tracking.
        _context.LoopDepth++;
        try
        {
            while (true)
            {
                _context.Session.ThrowIfCancelled();
                if (++iterations > _context.MaxIterations)
                {
                    _errorReporter.ReportError($"Iteration limit of {_context.MaxIterations} exceeded in while loop", stmt.Location);
                    break;
                }
                var condValue = _evaluator.Evaluate(stmt.Condition);
                if (condValue.Data is not bool condBool)
                {
                    _errorReporter.ReportError("While condition must evaluate to Bool", stmt.Location);
                    return;
                }
                if (!condBool) break;

                _context.PushFrame();
                try
                {
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
        finally { _context.LoopDepth--; }
    }

    private void ExecuteProcDeclaration(ProcDeclaration proc)
    {
        // Create function signature
        var inputTypes = proc.Parameters.Select(p => p.Type).ToList();
        var isVarArgs = proc.Parameters.Any(p => p.IsVarArgs);

        // sweep-0614: thread parameter NAMES through to the signature so user
        // procs (and every Flow-defined stdlib proc) honour the documented
        // universal named-arg surface (D-36-11). Previously names were dropped,
        // so `(addThem a=3 b=4)` and `(createSineTone duration=1.0 ...)` failed
        // with the misleading "does not yet support named arguments" error even
        // though the names ARE declared on the Parameter records. Safe because
        // FunctionSignature.Equals/GetHashCode intentionally exclude
        // ParameterNames (overload identity + registry de-dup unperturbed), and
        // the named-args+varargs combo is rejected earlier in OverloadResolver.
        var paramNames = proc.Parameters.Select(p => p.Name).ToList();
        var signature = new FunctionSignature(proc.Name, inputTypes, isVarArgs, paramNames);

        if (proc.IsInternal)
        {
            // Look up C# implementation for internal procedure
            if (_context.InternalRegistry.TryGetImplementation(proc.Name, signature, out var impl, out var registeredSignature))
            {
                // Use the registered signature which has the correct IsVarArgs flag
                var overload = FunctionOverload.Internal(proc.Name, registeredSignature!, impl!);
                _context.DeclareFunction(overload);
            }
            else if (BuildTarget.IsWeb)
            {
                // Phase 48 (debug wasm-boot-no-app-bundle, cycle 6): on the Web
                // target some builtin C# implementations are STRIPPED at compile
                // time (csproj `<Compile Remove>` per Phase 47 D-47-03 — e.g.
                // `micBuffer` from InputFunctions.cs, `loadSfz` from the Sfz/**
                // tree). Their `internal proc` SURFACE still ships in the embedded
                // stdlib `.flow` modules (`audio.flow`, `std.flow`), so without a
                // charitable branch here EACH such surface overload would emit a
                // hard "No C# implementation found" error. Those accumulated
                // errors make ModuleLoader fail the WHOLE import (`@audio`/`@std`),
                // taking the entire module's surface (createSineTone/play/...) down
                // with it.
                //
                // Charitable interpretation + Phase 47's established "advisory at
                // import-time for stripped features" pattern: skip the missing
                // overload and emit a one-shot advisory (keyed per proc-name so it
                // fires at most once per name in the active engine session). Calling the builtin
                // in-browser then yields a normal "function not found" — acceptable,
                // since the implementation is genuinely stripped by design.
                //
                // Gated on the compile-time FlowEngine.IsWebTarget const
                // (Phase 47 D-47-10), NOT a runtime OS check, so Desktop behavior +
                // Desktop tests are provably unchanged: under FlowTarget=Desktop
                // this branch is unreachable and the hard ReportError below still
                // fires for a genuinely-missing impl (a real bug there).
                FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                    $"target:stripped-builtin:{proc.Name}",
                    $"[target] builtin '{proc.Name}' unavailable on Web target — surface declared in stdlib but implementation stripped (Phase 47). Skipping; calls will report 'function not found'.");
            }
            else
            {
                _errorReporter.ReportError(
                    $"No C# implementation found for internal proc '{proc.Name}' with signature {signature}",
                    proc.Location);
            }
        }
        else
        {
            // User-defined function
            var overload = FunctionOverload.UserDefined(proc.Name, signature, proc);
            _context.DeclareFunction(overload);
        }
    }

    private void ExecuteVariableDeclaration(VariableDeclaration varDecl)
    {
        var value = _evaluator.Evaluate(varDecl.Value);

        // `Void` is the wildcard type: as a variable annotation it accepts any value
        // and the variable takes that value's type (like a Void parameter or Voids).
        // A declaration without an initializer carries a synthetic literal placed at the
        // variable name (an explicit initializer follows the `=`); it keeps the Void default.
        bool synthesizedDefault = varDecl.Value is LiteralExpression
            && varDecl.Span is { } declSpan && varDecl.Value.Location == declSpan.Start;
        if (varDecl.Type is VoidType && value.Type is not VoidType && !synthesizedDefault)
        {
            _context.DeclareVariable(varDecl.Name, value);
            return;
        }

        // Check if this is a default value initialization (when expression evaluates to Int 0 for non-Int types)
        // Exclude NoteValue since it's int-backed and 0 is a valid enum value (WHOLE)
        bool isDefaultInit = value.Type is IntType && value.As<int>() == 0 && varDecl.Type is not IntType;

        if (isDefaultInit)
        {
            // Create appropriate default value for the type
            value = CreateDefaultValue(varDecl.Type);
        }
        else
        {
            // Phase 26: variable initialization may need to narrow Double→Float
            // (e.g., `Float a = 1.5` where 1.5 lexes as Double). Value.ConvertTo
            // already implements Int→Long→Float→Double→Number widening AND the
            // narrowing direction Double→Float (line 114). The Type-level
            // CanConvertTo only declares the widening side to keep OverloadResolver
            // unambiguous; here at the assignment boundary we additionally try a
            // direct Value.ConvertTo for the narrow numeric cases.
            bool typeCompatible = value.Type.IsCompatibleWith(varDecl.Type)
                || value.Type.CanConvertTo(varDecl.Type);

            // Try direct Value-level coercion for numeric narrowing (Double→Float, etc.)
            string? narrowingFailure = null;
            if (!typeCompatible && IsNumericNarrowing(value.Type, varDecl.Type))
            {
                try
                {
                    var coerced = value.ConvertTo(varDecl.Type);
                    if (coerced.Type.Equals(varDecl.Type))
                    {
                        value = coerced;
                        typeCompatible = true;
                    }
                }
                catch (InvalidCastException ex) when (ex.Message.Contains("does not fit", StringComparison.Ordinal))
                {
                    narrowingFailure = ex.Message; // e.g. "value 2147483648 does not fit in Int"
                }
                catch { /* fall through to error */ }
            }

            // Type checking (simplified - just check if compatible)
            // Skip type check for function values (lambdas assigned to variables with return-type annotations)
            if (value.Type is not TypeSystem.PrimitiveTypes.FunctionType && !typeCompatible)
            {
                _errorReporter.ReportError(
                    narrowingFailure is null
                        ? $"Cannot assign {value.Type} to variable of type {varDecl.Type}"
                        : $"Cannot assign to {varDecl.Name}: {narrowingFailure}",
                    varDecl.Location);
                // Declare the name with its type default so later uses do not cascade
                // into "unknown identifier" errors.
                _context.DeclareVariable(varDecl.Name, CreateDefaultValue(varDecl.Type));
                return;
            }

            // Convert if needed
            if (!value.Type.Equals(varDecl.Type) && value.Type.CanConvertTo(varDecl.Type))
            {
                // sweep-0614: Void is the charitable report-and-continue sentinel
                // returned by handlers like ReportDivisionByZero / ReportUnknownMember
                // after they have ALREADY reported a located error. VoidType.CanConvertTo
                // returns true (wildcard), but Value.ConvertTo throws because the
                // underlying CLR value is null — and that throw would propagate to
                // FlowEngine's catch-all as a SECOND, location-less "Unexpected error"
                // on top of the good diagnostic. Bind a type-default instead so the
                // program keeps running with exactly one clean diagnostic.
                if (value.Type is TypeSystem.PrimitiveTypes.VoidType
                    && varDecl.Type is not TypeSystem.PrimitiveTypes.VoidType)
                {
                    value = CreateDefaultValue(varDecl.Type);
                }
                else
                {
                    value = value.ConvertTo(varDecl.Type);
                }
            }
        }

        _context.Bindings.OnDeclared(_context, varDecl.Name, value, varDecl.Type);

        _context.DeclareVariable(varDecl.Name, value);
    }

    /// <summary>
    /// Phase 26.1 TUP-09: executes <c>&lt;&lt;Type? name, Type? name, ...&gt;&gt; = expr</c>.
    /// Evaluates the RHS once, validates it is a Tuple, runtime-checks arity, then per-slot
    /// type-checks (when an annotation is provided) before binding each component into the
    /// current frame. Type-mismatch and arity-mismatch are soft errors (mirrors
    /// <see cref="ExecuteVariableDeclaration"/> precedent so the rest of the program continues).
    /// </summary>
    private void ExecuteTupleDestructure(TupleDestructureStatement stmt)
    {
        var rhs = _evaluator.Evaluate(stmt.Value);
        if (rhs.Type is not TupleType || rhs.Data is not IReadOnlyList<Value> tupArr)
        {
            _errorReporter.ReportError(
                $"Right-hand side of destructure must be a Tuple, got {rhs.Type}",
                stmt.Location);
            return;
        }
        if (tupArr.Count != stmt.Patterns.Count)
        {
            _errorReporter.ReportError(
                $"Tuple destructure arity mismatch: pattern has {stmt.Patterns.Count} slot(s), value has {tupArr.Count}",
                stmt.Location);
            return;
        }
        for (int i = 0; i < stmt.Patterns.Count; i++)
        {
            var pattern = stmt.Patterns[i];
            var component = tupArr[i];
            if (pattern.Type != null
                && !component.Type.IsCompatibleWith(pattern.Type)
                && !component.Type.CanConvertTo(pattern.Type))
            {
                _errorReporter.ReportError(
                    $"Cannot bind tuple component {i} of type {component.Type} to {pattern.Type} {pattern.Name}",
                    stmt.Location);
                return;
            }
            if (pattern.Type != null
                && !component.Type.Equals(pattern.Type)
                && component.Type.CanConvertTo(pattern.Type))
            {
                component = component.ConvertTo(pattern.Type);
            }
            _context.DeclareVariable(pattern.Name, component);
        }
    }

    /// <summary>
    /// Phase 26: detects numeric-narrowing initialization like `Float a = 1.5`
    /// where Value.ConvertTo can produce the narrower type but the FlowType-level
    /// CanConvertTo doesn't (intentionally — to keep OverloadResolver unambiguous).
    /// </summary>
    private static bool IsNumericNarrowing(FlowType from, FlowType to)
    {
        return (from is TypeSystem.PrimitiveTypes.DoubleType && to is TypeSystem.PrimitiveTypes.FloatType)
            || (from is TypeSystem.PrimitiveTypes.LongType && to is TypeSystem.PrimitiveTypes.IntType);
    }

    private Value CreateDefaultValue(FlowType type)
    {
        // Phase 26.1 TUP-09: Tuple default-init constructs per-position default values
        // recursively (so `Tuple<<Note, Beat>> entry` produces `<<C4, 0.0>>`).
        if (type is TupleType tt)
        {
            if (tt.IsAnyArity)
                return Value.Tuple(new List<Value>(), new List<FlowType>());
            var components = new List<Value>(tt.ElementTypes.Count);
            foreach (var et in tt.ElementTypes)
                components.Add(CreateDefaultValue(et));
            return Value.Tuple(components, tt.ElementTypes);
        }

        return type switch
        {
            IntType => Value.Int(0),
            FloatType => Value.Float(0.0),
            LongType => Value.Long(0L),
            DoubleType => Value.Double(0.0),
            StringType => Value.String(""),
            BoolType => Value.Bool(false),
            NumberType => Value.Number(System.Numerics.BigInteger.Zero),
            ArrayType arr => Value.Array(new List<Value>(), arr.ElementType),
            _ when _context.Bindings.CreateDefault(type) is { } domainDefault => domainDefault,
            _ => Value.Void()
        };
    }

    private void ExecuteAssignment(AssignmentStatement assignment)
    {
        // Evaluate new value
        var newValue = _evaluator.Evaluate(assignment.Value);

        // Get existing variable to check type compatibility.
        // Bundle B (260524-rjm) — non-throwing TryGetVariable replaces the
        // legacy try/catch on InvalidOperationException. Identical type-check
        // + conversion + SetVariable semantics on the found branch; identical
        // error wording on the not-found branch.
        if (_context.CurrentFrame.TryGetVariable(assignment.Name, out var existingValue))
        {
            var targetType = existingValue.Type;

            // Type check
            if (!newValue.Type.IsCompatibleWith(targetType) &&
                !newValue.Type.CanConvertTo(targetType))
            {
                _errorReporter.ReportError(
                    $"Cannot assign {newValue.Type} to variable of type {targetType}",
                    assignment.Location);
                return;
            }

            // Convert if needed
            if (!newValue.Type.Equals(targetType) && newValue.Type.CanConvertTo(targetType))
            {
                newValue = newValue.ConvertTo(targetType);
            }

            // Update variable
            _context.SetVariable(assignment.Name, newValue);
        }
        else
        {
            _errorReporter.ReportError(
                $"Variable '{assignment.Name}' not found",
                assignment.Location);
        }
    }

    private void ExecuteReturn(ReturnStatement ret)
    {
        _returnValue = _evaluator.Evaluate(ret.Value);
    }

    private void ExecuteImport(ImportStatement import)
    {
        // Get current file from import statement location
        string? currentFile = import.Location.FileName;

        var result = _moduleLoader.LoadModule(import.FilePath, currentFile ?? "", _context, import.Location);

        if (result == ModuleLoadResult.Error)
        {
            _errorReporter.ReportError($"Failed to import '{import.FilePath}'", import.Location);
        }
    }

    /// <summary>
    /// Executes a user-defined function.
    /// </summary>
    public Value ExecuteUserFunction(ProcDeclaration proc, IReadOnlyList<Value> args)
    {
        return ExecuteUserFunctionWithCaptures(proc, args, null);
    }

    /// <summary>
    /// Executes a user-defined function with optional captured closure variables.
    /// </summary>
    public Value ExecuteUserFunctionWithCaptures(
        ProcDeclaration proc,
        IReadOnlyList<Value> args,
        IReadOnlyDictionary<string, Value>? capturedVariables)
    {
        _context.Session.ThrowIfCancelled();
        if (++_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            _errorReporter.ReportError($"Recursion depth limit ({MaxRecursionDepth}) exceeded", proc.Location);
            return Value.Void();
        }

        // Phase 44 Plan 44-02 D-02 / D-03 (Pattern S2 + Anti-Pattern 1):
        // push/pop the declaring file's strict bit around the proc body. The
        // bit was captured on the AST node at parse time
        // (`ProcDeclaration.IsStrict` per Task 1); reading it here lets the
        // body's leaf-site dispatch see a CallerStrictMode snapshot that
        // reflects THIS proc's file (via the ExpressionEvaluator save/restore
        // at the call boundary), not whatever the outer caller's file was.
        // Order: SET BEFORE PushFrame so any future PushFrame logic reading
        // the bit gets the proc's value; RESTORE AFTER PopFrame so any
        // diagnostic reported during PopFrame also reads the proc's bit. The
        // restore lives in the SAME try/finally as the frame pop — a body
        // throw rebalances both atomically.
        var prevStrict = _context.StrictMode;
        _context.StrictMode = proc.IsStrict;

        // Phase 45 Plan 45-06 D-04 — push the declaring file's beat-true-to-sig
        // bit around the proc body, mirroring the strict-bit discipline above.
        // The bit was captured on the AST node at parse time
        // (`ProcDeclaration.IsBeatTrueToSig`); reading it here means a (beat N)
        // call inside the body sees the DECLARING file's pragma bit even when
        // the proc is invoked from a different-pragma file (Pitfall 3 /
        // cross-file boundary REQ-BEAT-TEST-04). Restored in the same finally
        // as PopFrame + the strict restore so a body throw rebalances all three.
        var prevBeatTrueToSig = _context.BeatTrueToSig;
        _context.BeatTrueToSig = proc.IsBeatTrueToSig;

        // break-control (0615) — reset the dynamic loop-nesting depth across the
        // proc-call boundary, mirroring the strict-bit discipline above. A proc
        // body must NOT be able to `(break)` a caller's loop just because the proc
        // happened to be CALLED from inside one (that would leak control flow via
        // dynamic scope). Parity with the `break` keyword, whose parse-time
        // `_inLoop` gate is already proc-body-lexical. The body re-counts its OWN
        // loops from zero; restored in the SAME finally as PopFrame so a body throw
        // (including a BreakSignal escaping a malformed body) rebalances atomically.
        var prevLoopDepth = _context.LoopDepth;
        _context.LoopDepth = 0;

        // Create new stack frame. Audit §2.5 (D5) — mark it a CALL BOUNDARY so
        // the proc/lambda body has LEXICAL variable scope: variable
        // lookup/assignment sees this frame's params + locals + injected closure
        // captures + globals, but NOT the caller's locals. This blocks the
        // dynamic-scope write-through bug (a typo'd name in the body no longer
        // silently mutates a caller's same-named local) and the dynamic read
        // (the body no longer reads caller locals). Globals stay readable +
        // writable from procs (scripts rely on top-level mutation), reached via
        // the boundary frame's GlobalScope redirect. Musical-context dynamic
        // scope is a separate mechanism (walks _callStack) and is unaffected.
        _context.PushFrame(isCallBoundary: true);

        try
        {
            // Inject captured closure variables (snapshot from lambda creation time)
            if (capturedVariables != null)
            {
                foreach (var (name, value) in capturedVariables)
                {
                    _context.DeclareVariable(name, value);
                }
            }

            // Bind parameters (may shadow captured variables, which is correct)
            for (int i = 0; i < proc.Parameters.Count; i++)
            {
                var param = proc.Parameters[i];
                Value paramValue;

                if (param.IsVarArgs)
                {
                    // Check if we're passing a single array argument that already matches the expected type
                    if (args.Count - i == 1 && args[i].Type is ArrayType arrayType && arrayType.ElementType.Equals(param.Type))
                    {
                        // Use the array directly instead of wrapping it
                        paramValue = args[i];
                    }
                    else
                    {
                        // Collect remaining arguments into an array
                        var varArgs = new List<Value>();
                        for (int j = i; j < args.Count; j++)
                        {
                            varArgs.Add(args[j]);
                        }

                        // Create array value with the parameter's base type as element type
                        paramValue = Value.Array(varArgs, param.Type);
                    }
                }
                else
                {
                    paramValue = args[i];

                    // Quick 260701-vqz: coerce SCALAR args to the declared param type at
                    // the user-proc boundary, mirroring the internal-builtin boundary in
                    // ExpressionEvaluator (Phase 26 D-05/D-06). Without this, a pure-Flow
                    // proc like createSineTone(Hertz, Second, Double) received a
                    // Millisecond value RAW in its Second slot (500ms bound as 500.0) and
                    // rendered 500 seconds. Containers/Lazy/Function/Void are excluded:
                    // their CanConvertTo is element-wise-permissive (e.g. Int[]→Double[])
                    // but Value.ConvertTo has no typed-container arms and would throw —
                    // they keep the legacy raw binding.
                    if (!paramValue.Type.Equals(param.Type)
                        && paramValue.Type is not (ArrayType or DictType or TupleType or LazyType or FunctionType or VoidType)
                        && param.Type is not (ArrayType or DictType or TupleType or LazyType or FunctionType or VoidType)
                        && paramValue.Type.CanConvertTo(param.Type))
                    {
                        paramValue = paramValue.ConvertTo(param.Type);
                    }
                }

                // Use SetVariable if the name was already declared (from captures), otherwise declare
                if (capturedVariables != null && capturedVariables.ContainsKey(param.Name))
                    _context.SetVariable(param.Name, paramValue);
                else
                    _context.DeclareVariable(param.Name, paramValue);
            }

            // Execute function body with implicit return collection
            var collector = new ImplicitReturnCollector();
            _returnValue = null;

            foreach (var statement in proc.Body)
            {
                if (_returnValue != null)
                    break; // Explicit return encountered

                ExecuteStatement(statement);

                // If statement was an expression, collect its value (already evaluated in ExecuteStatement)
                if (statement is ExpressionStatement exprStatement)
                {
                    // `(Nothing)` discards everything collected so far, so as the final
                    // statement it makes the proc return Void.
                    if (exprStatement.Expression is FunctionCallExpression { Name: "Nothing", Arguments.Count: 0 })
                        collector.Clear();
                    else
                        collector.Collect(_lastExpressionValue ?? Value.Void());
                }
            }

            // Return result
            if (_returnValue != null)
            {
                var result = _returnValue;
                _returnValue = null;
                return result;
            }

            return collector.GetResult();
        }
        finally
        {
            _context.PopFrame();
            // Phase 44 Plan 44-02 D-02 / D-03 (Anti-Pattern 1): restore the
            // strict bit AFTER PopFrame so error-reporting during pop reads
            // the proc's bit. Restore in the SAME finally as PopFrame so a
            // body throw cannot leave StrictMode mutated on unwind.
            _context.StrictMode = prevStrict;
            // Phase 45 Plan 45-06 D-04 — restore the beat-true-to-sig bit AFTER
            // PopFrame, in the SAME finally as the strict restore, so a body
            // throw cannot leak the proc's pragma bit onto unwind.
            _context.BeatTrueToSig = prevBeatTrueToSig;
            // break-control (0615) — restore the caller's loop depth AFTER PopFrame,
            // in the SAME finally as the strict/beat restores, so a body throw cannot
            // leak the proc's zeroed depth onto the unwinding caller's loop.
            _context.LoopDepth = prevLoopDepth;
            _recursionDepth--;
        }
    }
}
