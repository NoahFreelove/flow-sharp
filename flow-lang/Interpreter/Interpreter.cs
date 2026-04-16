using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Ast.Expressions;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
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
    /// Executes a program.
    /// </summary>
    public void Execute(Program program)
    {
        _lastExpressionValue = null;  // Clear previous value

        foreach (var statement in program.Statements)
        {
            ExecuteStatement(statement);
        }
    }

    /// <summary>
    /// Executes a single statement.
    /// </summary>
    public void ExecuteStatement(Statement stmt)
    {
        if (_returnValue != null)
            return; // Already returned

        switch (stmt)
        {
            case ProcDeclaration proc:
                ExecuteProcDeclaration(proc);
                break;

            case VariableDeclaration varDecl:
                ExecuteVariableDeclaration(varDecl);
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

            case SectionDeclaration section:
                ExecuteSectionDeclaration(section);
                break;

            case MusicalContextStatement ctx:
                ExecuteMusicalContext(ctx);
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
                throw new NotSupportedException($"Statement type {stmt.GetType().Name} not supported");
        }
    }

    private void ExecuteMusicalContext(MusicalContextStatement ctx)
    {
        _context.PushFrame();
        try
        {
            var musicalCtx = new MusicalContext();

            switch (ctx.ContextType)
            {
                case MusicalContextType.Timesig:
                    var num = _evaluator.Evaluate(ctx.Value);
                    var den = _evaluator.Evaluate(ctx.Value2!);
                    int numVal = num.As<int>();
                    int denVal = den.As<int>();
                    try
                    {
                        musicalCtx.TimeSignature = new TimeSignatureData(numVal, denVal);
                    }
                    catch (ArgumentException ex)
                    {
                        _errorReporter.ReportError(ex.Message, ctx.Location);
                        return;
                    }
                    break;

                case MusicalContextType.Tempo:
                    var tempoVal = _evaluator.Evaluate(ctx.Value);
                    double tempo = tempoVal.Type is IntType
                        ? (double)tempoVal.As<int>()
                        : tempoVal.As<double>();
                    if (!MusicalContext.IsValidTempo(tempo))
                    {
                        _errorReporter.ReportError(
                            $"Tempo must be positive, got {tempo}", ctx.Location);
                        return;
                    }
                    musicalCtx.Tempo = tempo;
                    break;

                case MusicalContextType.Swing:
                    var swingVal = _evaluator.Evaluate(ctx.Value);
                    double swing = swingVal.Type is IntType
                        ? (double)swingVal.As<int>()
                        : swingVal.As<double>();
                    if (!MusicalContext.IsValidSwing(swing))
                    {
                        _errorReporter.ReportError(
                            $"Swing must be between 0.0 and 1.0, got {swing}", ctx.Location);
                        return;
                    }
                    musicalCtx.Swing = swing;
                    break;

                case MusicalContextType.Dynamics:
                    var velVal = _evaluator.Evaluate(ctx.Value);
                    double vel = velVal.Type is IntType
                        ? (double)velVal.As<int>()
                        : velVal.As<double>();
                    vel = Math.Clamp(vel, 0.0, 1.0);
                    musicalCtx.Velocity = vel;
                    break;

                case MusicalContextType.Rit:
                {
                    var targetVal = _evaluator.Evaluate(ctx.Value);
                    double targetTempo = targetVal.Type is IntType
                        ? (double)targetVal.As<int>()
                        : targetVal.As<double>();
                    // Approximate rit by averaging current tempo and target
                    double currentTempo = _context.GetMusicalContext().Tempo ?? 120.0;
                    musicalCtx.Tempo = (currentTempo + targetTempo) / 2.0;
                    break;
                }
                case MusicalContextType.Accel:
                {
                    var targetVal = _evaluator.Evaluate(ctx.Value);
                    double targetTempo = targetVal.Type is IntType
                        ? (double)targetVal.As<int>()
                        : targetVal.As<double>();
                    double currentTempo = _context.GetMusicalContext().Tempo ?? 120.0;
                    musicalCtx.Tempo = (currentTempo + targetTempo) / 2.0;
                    break;
                }

                case MusicalContextType.Pan:
                {
                    var panVal = _evaluator.Evaluate(ctx.Value);
                    double pan = panVal.Type is IntType
                        ? (double)panVal.As<int>()
                        : panVal.As<double>();
                    if (pan < -1.0 || pan > 1.0)
                    {
                        _errorReporter.ReportError(
                            $"Pan value must be between -1.0 and 1.0, got {pan}", ctx.Location);
                        return;
                    }
                    musicalCtx.Pan = pan;
                    break;
                }

                case MusicalContextType.Gain:
                {
                    var gainVal = _evaluator.Evaluate(ctx.Value);
                    double gain = gainVal.Type is IntType
                        ? (double)gainVal.As<int>()
                        : gainVal.As<double>();
                    if (gain < 0.0 || gain > 2.0)
                    {
                        _errorReporter.ReportError(
                            $"Gain must be between 0.0 and 2.0, got {gain}", ctx.Location);
                        return;
                    }
                    musicalCtx.Gain = gain;
                    break;
                }

                case MusicalContextType.Key:
                    if (ctx.Value is LiteralExpression keyExpr)
                    {
                        string keyName = (string)keyExpr.Value;
                        if (!MusicalContext.IsValidKey(keyName))
                        {
                            _errorReporter.ReportError(
                                $"Unrecognized key '{keyName}'. Valid keys include: Cmajor, Aminor, Fsharpmajor, etc.",
                                ctx.Location);
                            return;
                        }
                        musicalCtx.Key = keyName;
                    }
                    else
                    {
                        _errorReporter.ReportError(
                            "Expected a key name literal (e.g., Cmajor, Aminor)", ctx.Location);
                        return;
                    }
                    break;
            }

            _context.CurrentFrame.MusicalContext = musicalCtx;

            foreach (var stmt in ctx.Body)
            {
                ExecuteStatement(stmt);
                if (_returnValue != null) break;
            }
        }
        finally
        {
            _context.PopFrame();
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
        foreach (var item in items)
        {
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

    private void ExecuteWhileStatement(WhileStatement stmt)
    {
        int iterations = 0;
        while (true)
        {
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

    private void ExecuteSectionDeclaration(SectionDeclaration section)
    {
        // Check for duplicate section names
        if (_context.SectionRegistry.ContainsKey(section.Name))
        {
            _errorReporter.ReportError(
                $"Section '{section.Name}' is already defined", section.Location);
            return;
        }

        // Push a new scope for the section body
        _context.PushFrame();
        try
        {
            // Snapshot the musical context before executing the body
            var musicalContext = _context.GetMusicalContext();

            // Track bare expression results during body execution
            var bareExpressionSequences = new List<SequenceData>();

            // Execute the section body
            foreach (var stmt in section.Body)
            {
                ExecuteStatement(stmt);

                // Capture bare expressions that produce sequences
                if (stmt is ExpressionStatement && _lastExpressionValue?.Data is SequenceData exprSeq)
                {
                    bareExpressionSequences.Add(exprSeq);
                }

                if (_returnValue != null) break;
            }

            // Collect all Sequence variables declared in the section scope
            var sequences = new Dictionary<string, SequenceData>();
            foreach (var (name, value) in _context.CurrentFrame.GetLocalVariables())
            {
                if (value.Data is SequenceData seq)
                {
                    sequences[name] = seq;
                }
            }

            // Add bare expression sequences with auto-generated names
            for (int i = 0; i < bareExpressionSequences.Count; i++)
            {
                // Only add if not already captured as a named variable
                if (!sequences.ContainsValue(bareExpressionSequences[i]))
                {
                    sequences[$"_anon_{i}"] = bareExpressionSequences[i];
                }
            }

            var sectionData = new SectionData(section.Name, sequences, musicalContext, section.Location);
            _context.SectionRegistry[section.Name] = sectionData;
        }
        finally
        {
            _context.PopFrame();
        }
    }

    private void ExecuteProcDeclaration(ProcDeclaration proc)
    {
        // Create function signature
        var inputTypes = proc.Parameters.Select(p => p.Type).ToList();
        var isVarArgs = proc.Parameters.Any(p => p.IsVarArgs);

        var signature = new FunctionSignature(proc.Name, inputTypes, isVarArgs);

        if (proc.IsInternal)
        {
            // Look up C# implementation for internal procedure
            if (_context.InternalRegistry.TryGetImplementation(proc.Name, signature, out var impl, out var registeredSignature))
            {
                // Use the registered signature which has the correct IsVarArgs flag
                var overload = FunctionOverload.Internal(proc.Name, registeredSignature!, impl!);
                _context.DeclareFunction(overload);
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

        // Check if this is a default value initialization (when expression evaluates to Int 0 for non-Int types)
        // Exclude NoteValue since it's int-backed and 0 is a valid enum value (WHOLE)
        bool isDefaultInit = value.Type is IntType && value.As<int>() == 0 && varDecl.Type is not IntType
            && varDecl.Type is not TypeSystem.SpecialTypes.NoteValueType;

        if (isDefaultInit)
        {
            // Create appropriate default value for the type
            value = CreateDefaultValue(varDecl.Type);
        }
        else
        {
            // Type checking (simplified - just check if compatible)
            // Skip type check for function values (lambdas assigned to variables with return-type annotations)
            if (value.Type is not TypeSystem.PrimitiveTypes.FunctionType
                && !value.Type.IsCompatibleWith(varDecl.Type) && !value.Type.CanConvertTo(varDecl.Type))
            {
                _errorReporter.ReportError(
                    $"Cannot assign {value.Type} to variable of type {varDecl.Type}",
                    varDecl.Location);
                return;
            }

            // Convert if needed
            if (!value.Type.Equals(varDecl.Type) && value.Type.CanConvertTo(varDecl.Type))
            {
                value = value.ConvertTo(varDecl.Type);
            }
        }

        _context.DeclareVariable(varDecl.Name, value);
    }

    private Value CreateDefaultValue(FlowType type)
    {
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
            BufferType => Value.Buffer(null),
            NoteType => Value.Note("C4"),
            SemitoneType => Value.Semitone(0),
            CentType => Value.Cent(0.0),
            MillisecondType => Value.Millisecond(0.0),
            SecondType => Value.Second(0.0),
            DecibelType => Value.Decibel(0.0),
            BeatType => Value.Beat(0.0),
            NoteValueType => Value.NoteValue(0),
            TimeSignatureType => Value.TimeSignature(new TimeSignatureData(4, 4)),
            SequenceType => Value.Sequence(new SequenceData()),
            BarType => Value.Bar(new BarData(new List<MusicalNoteData>(), new TimeSignatureData(4, 4))),
            _ => Value.Void()
        };
    }

    private void ExecuteAssignment(AssignmentStatement assignment)
    {
        // Evaluate new value
        var newValue = _evaluator.Evaluate(assignment.Value);

        // Get existing variable to check type compatibility
        try
        {
            var existingValue = _context.GetVariable(assignment.Name);
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
        catch (InvalidOperationException)
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
        if (++_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            _errorReporter.ReportError($"Recursion depth limit ({MaxRecursionDepth}) exceeded", proc.Location);
            return Value.Void();
        }

        // Create new stack frame
        _context.PushFrame();

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
                if (statement is ExpressionStatement)
                {
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
            _recursionDepth--;
        }
    }
}
