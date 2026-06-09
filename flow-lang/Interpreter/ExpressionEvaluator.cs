using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;
using FlowLang.Diagnostics;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Interpreter;

/// <summary>
/// Evaluates expressions into runtime values.
/// </summary>
public class ExpressionEvaluator
{
    private readonly RuntimeContext _context;
    private readonly ErrorReporter _errorReporter;
    private readonly IFunctionInvoker _invoker;

    public ExpressionEvaluator(RuntimeContext context, ErrorReporter errorReporter, IFunctionInvoker invoker)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    public virtual Value Evaluate(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => EvaluateLiteral(lit),
            VariableExpression var => EvaluateVariable(var),
            FunctionCallExpression call => EvaluateFunctionCall(call),
            ArrayIndexExpression idx => EvaluateArrayIndex(idx),
            ArrayLiteralExpression arrLit => EvaluateArrayLiteral(arrLit),
            TupleLiteralExpression tupLit => EvaluateTupleLiteral(tupLit),
            ChordLiteralExpression chordLit => EvaluateChordLiteral(chordLit),
            SymbolLiteralExpression symLit => EvaluateSymbolLiteral(symLit),
            BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),
            LambdaExpression lambda => EvaluateLambda(lambda),
            MemberAccessExpression member => EvaluateMemberAccess(member),
            LazyExpression lazy => EvaluateLazy(lazy),
            NoteStreamExpression noteStream => EvaluateNoteStream(noteStream),
            SongExpression song => EvaluateSong(song),
            ProgressionExpression progression => EvaluateProgression(progression),
            InterpolatedStringExpression interp => EvaluateInterpolatedString(interp),
            FlowExpression flowEx => EvaluateFlowExpression(flowEx),
            TupleUnpackFlowExpression unpackEx => EvaluateTupleUnpackFlow(unpackEx),
            MatchExpression matchEx => EvaluateMatch(matchEx),
            _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported")
        };
    }

    private Value EvaluateLiteral(LiteralExpression lit)
    {
        return lit.Value switch
        {
            int i => Value.Int(i),
            long l => Value.Long(l),                      // Phase 26: int-overflow lex path
            System.Numerics.BigInteger n => Value.Number(n),  // Phase 26: long-overflow lex path
            double d => Value.Double(d),
            bool b => Value.Bool(b),
            // Audit §2.1: only RE-TYPE a string payload as a music value when it came
            // from a music-literal token (Note/Semitone/Cent/Time/Decibel/Hertz). An
            // ordinary quoted StringLiteral (IsMusicLiteral == false) stays a String even
            // when its content happens to look like a music literal — `String s = "10s"`
            // is a String, `"a"` is a String, and a dict keyed by `"10s"` round-trips.
            string s => lit.IsMusicLiteral ? (TryParseSpecialLiteral(s) ?? Value.String(s)) : Value.String(s),
            _ => throw new NotSupportedException($"Literal type {lit.Value.GetType()} not supported")
        };
    }

    private Value? TryParseSpecialLiteral(string text)
    {
        // Try to parse as Note (A-G with optional octave and alteration)
        try
        {
            var (note, octave, alteration) = NoteType.Parse(text);
            return Value.Note(text); // Store original text
        }
        catch
        {
            // Not a note, continue
        }

        // Try to parse as Semitone (+/-Nst)
        if (text.EndsWith("st"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (int.TryParse(numberPart, out int semitoneValue))
            {
                return Value.Semitone(semitoneValue);
            }
        }

        // Try to parse as Cent (+/-Nc)
        if (text.EndsWith("c") && text.Length > 1)
        {
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, out double centValue))
            {
                return Value.Cent(centValue);
            }
        }

        // Try to parse as Time (Nms or Ns)
        if (text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, out double msValue))
            {
                return Value.Millisecond(msValue);
            }
        }
        else if (text.EndsWith("s") && !text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, out double sValue))
            {
                return Value.Second(sValue);
            }
        }

        // Try to parse as Decibel (+/-NdB or NdB)
        if (text.EndsWith("dB"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, out double dbValue))
            {
                return Value.Decibel(dbValue);
            }
        }

        // Phase 26.2 ERG-04: Try to parse as Hertz (NHz or NkHz). Check kHz BEFORE Hz
        // because EndsWith("Hz") is also true for "kHz" strings — matches HertzType.Parse ordering.
        if (text.EndsWith("kHz"))
        {
            string numberPart = text.Substring(0, text.Length - 3);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kHzValue))
            {
                return Value.Hertz(kHzValue * 1000.0);  // canonical Hz
            }
        }
        else if (text.EndsWith("Hz"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hzValue))
            {
                return Value.Hertz(hzValue);
            }
        }

        // If we can't parse it as a special literal, return null
        // and the caller will treat it as a regular string
        return null;
    }

    private Value EvaluateVariable(VariableExpression var)
    {
        // Bundle B (260524-rjm) — fast path: non-throwing TryGetVariable
        // replaces the legacy try/catch on InvalidOperationException. Every
        // bare identifier naming a function used to pay the full throw/catch
        // cost; now the miss branch is a straight-line conditional.
        if (_context.CurrentFrame.TryGetVariable(var.Name, out var v))
            return v;

        // Variable not found - check if it's a zero-argument function or a function reference
        var overloads = _context.CurrentFrame.GetFunctionOverloads(var.Name);

        if (overloads.Count > 0)
        {
            // Try resolving with 0 args first (for backwards compatibility with existing 0-arg function shortcuts)
            var zeroArgOverload = _context.TryResolveFunction(var.Name, Array.Empty<FlowType>());
            if (zeroArgOverload != null)
            {
                if (zeroArgOverload.IsInternal)
                {
                    return zeroArgOverload.Implementation!(new List<Value>());
                }
                else
                {
                    return _invoker.ExecuteUserFunction(zeroArgOverload.Declaration!, new List<Value>());
                }
            }

            // If not a 0-arg function, return it as a Function Value (the first available overload for now)
            // In Flow, Function types are structurally compatible.
            return Value.Function(overloads[0]);
        }

        // Not a variable or function — Phase 35 LANG-04 Wave 2a: emit rich
        // FlowDiagnostic with Levenshtein-derived did-you-mean suggestion
        // pulled from the union of all in-scope variable names and known
        // function names. Per RESEARCH § Pitfall 5: ONE suggestion, threshold
        // max(2, len/3). Span is the variable expression's span (post Plan
        // 35-01 migration); back-compat fallback `Span.At(var.Location)` for
        // any node still constructed without a span.
        var span = var.Span ?? Span.At(var.Location);
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in _context.CurrentFrame.GetAllAccessibleVariables().Keys)
            candidates.Add(name);
        // Internal builtins — enumerated via the registry so prefix-only
        // arithmetic / stdlib / harmony / transform names all become
        // candidate suggestions.
        foreach (var (name, _) in _context.InternalRegistry.EnumerateSignatures())
            candidates.Add(name);
        var suggestion = LevenshteinHelper.SuggestNearest(var.Name, candidates);

        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            $"unknown identifier '{var.Name}'",
            span,
            Labels: [new DiagnosticLabel(span, "not found in scope")],
            Notes: Array.Empty<string>(),
            Suggestion: suggestion);
        _errorReporter.Report(diag);
        return Value.Void();
    }

    private Value EvaluateFunctionCall(FunctionCallExpression call)
    {
        // Phase 43 Plan 43-03 D-02 — qualified-call routing: when call.Name carries a
        // dot (parser emits "mod.fn" for (mod.fn args) syntax), try the ModuleRegistry
        // first. A hit short-circuits to invoking the registered Function Value with
        // the call's argValues. A miss falls through to the normal unqualified-name
        // resolution, which will report a clean "no proc 'fn' in module 'mod'" error.
        // Pitfall 2: only qualified names (containing '.') hit this branch — all
        // existing call sites pass bare identifiers and remain unaffected.
        if (call.Name.IndexOf('.') >= 0)
        {
            var dotIdx = call.Name.IndexOf('.');
            var modName = call.Name.Substring(0, dotIdx);
            var procName = call.Name.Substring(dotIdx + 1);
            if (_context.ModuleRegistry.TryGetProc(modName, procName, out var registeredValue))
            {
                if (registeredValue!.Data is FunctionOverload registeredOverload)
                {
                    var qArgValues = call.Arguments.Select(Evaluate).ToList();
                    if (registeredOverload.IsInternal)
                    {
                        // Phase 44 Plan 44-02 D-05 + Pattern S2: snapshot the
                        // immediate-caller's strict bit alongside CurrentCallSite.
                        // Pairs the existing prevSite save/restore. Mirrors the
                        // unqualified-call branch below — qualified Phase 43
                        // (mod.fn args) dispatch is the SAME semantic event
                        // for the strict-bit snapshot.
                        var prevSite = _context.CurrentCallSite;
                        var prevCallerStrict = _context.CallerStrictMode;
                        _context.CurrentCallSite = call.Location;
                        _context.CallerStrictMode = _context.StrictMode;
                        try
                        {
                            return registeredOverload.Implementation!(qArgValues);
                        }
                        finally
                        {
                            _context.CurrentCallSite = prevSite;
                            _context.CallerStrictMode = prevCallerStrict;
                        }
                    }
                    // Phase 44 Plan 44-02 D-05: user-proc dispatch via qualified call.
                    // Snapshot CallerStrictMode BEFORE ExecuteUserFunctionWithCaptures
                    // so the call-boundary semantic is consistent with the builtin
                    // branch — the proc's own body will then re-snap as it dispatches
                    // its leaf calls (via the unqualified branch below).
                    var prevCallerStrictUser = _context.CallerStrictMode;
                    _context.CallerStrictMode = _context.StrictMode;
                    try
                    {
                        return _invoker.ExecuteUserFunctionWithCaptures(
                            registeredOverload.Declaration!, qArgValues, registeredOverload.CapturedVariables);
                    }
                    finally
                    {
                        _context.CallerStrictMode = prevCallerStrictUser;
                    }
                }
            }
            else if (_context.ModuleRegistry.Contains(modName))
            {
                // Module is registered but the proc is not. Clearer error than the generic
                // "no matching overload" — name the module + the missing proc explicitly.
                _errorReporter.ReportError(
                    $"[module] module '{modName}' has no proc '{procName}'",
                    call.Location);
                return Value.Void();
            }
            // Module not registered — fall through to the normal path so the existing
            // "Function '<mod.fn>' not found" error message fires.
        }

        // Evaluate all arguments — Bundle A (260524-r4o) Task 4: single
        // pre-sized loop builds argValues (List<Value>) + argTypes (FlowType[])
        // in one pass. FlowType[] satisfies IReadOnlyList<FlowType> at every
        // downstream consumer (TryResolveFunction / ResolveFunction), avoiding
        // the legacy double-LINQ allocation (2 iterators + 2 boxed enumerators
        // + 2 growable Lists).
        var argValues = new List<Value>(call.Arguments.Count);
        var argTypes = new FlowType[call.Arguments.Count];
        for (int i = 0; i < call.Arguments.Count; i++)
        {
            var v = Evaluate(call.Arguments[i]);
            argValues.Add(v);
            argTypes[i] = v.Type;
        }

        // Phase 36 Plan 36-02 (D-36-11): evaluate named-arg values up-front
        // so the resolver can see their Types. The dict shape mirrors the
        // AST shape (preserve composer's insertion order via Dictionary<>).
        Dictionary<string, Value>? namedArgValues = null;
        Dictionary<string, FlowType>? namedArgTypes = null;
        if (call.NamedArgs is { Count: > 0 })
        {
            namedArgValues = new Dictionary<string, Value>(call.NamedArgs.Count);
            namedArgTypes = new Dictionary<string, FlowType>(call.NamedArgs.Count);
            foreach (var (name, expr) in call.NamedArgs)
            {
                var val = Evaluate(expr);
                namedArgValues[name] = val;
                namedArgTypes[name] = val.Type;
            }
        }

        // Try to resolve function overload
        var overload = _context.TryResolveFunction(call.Name, argTypes, namedArgTypes);

        // If no function found, try looking up as a variable holding a lambda.
        // Bundle B (260524-rjm) — non-throwing TryGetVariable replaces the
        // legacy try/catch on InvalidOperationException. Ordering preserved:
        // function resolution runs first (above), variable-holding-lambda
        // fallback runs second.
        if (overload == null)
        {
            if (_context.CurrentFrame.TryGetVariable(call.Name, out var variable)
                && variable.Data is FunctionOverload varOverload)
            {
                overload = varOverload;
            }
        }

        if (overload == null)
        {
            // Report error using the full resolution path
            _context.ResolveFunction(call.Name, argTypes, call.Location, namedArgTypes);
            return Value.Void();
        }

        // Phase 36 Plan 36-02 (D-36-11): if the call uses named-args, re-order
        // argValues to match the resolved signature's positional layout. The
        // resolver already validated that ParameterNames is non-null and that
        // no slot is doubly bound, so the lookup-by-name pass is safe here.
        if (namedArgValues is { Count: > 0 } && overload.Signature.ParameterNames is { } paramNames)
        {
            var reorderedArgs = new List<Value>(overload.Signature.InputTypes.Count);
            // Positional args fill slots 0..argValues.Count-1.
            for (int i = 0; i < argValues.Count; i++)
                reorderedArgs.Add(argValues[i]);
            // Named args fill the remaining slots by parameter-name lookup.
            // Add placeholders to grow the list, then assign by index — we can't
            // assume the named-arg dictionary iteration order matches the
            // signature's slot order (it's source-text order, not declaration order).
            while (reorderedArgs.Count < overload.Signature.InputTypes.Count)
                reorderedArgs.Add(Value.Void());
            foreach (var (name, val) in namedArgValues)
            {
                int slot = -1;
                for (int i = 0; i < paramNames.Count; i++)
                {
                    if (paramNames[i] == name) { slot = i; break; }
                }
                if (slot >= 0)
                    reorderedArgs[slot] = val;
            }
            argValues = reorderedArgs;
        }

        // Execute function
        if (overload.IsInternal)
        {
            // Phase 26 D-05/D-06 (RESEARCH Pitfall 2): coerce arguments at the
            // implementation boundary. Without this, mixed-type calls that resolved
            // via OverloadResolver convertible-scoring (+100) reach the impl with
            // the caller's original CLR primitive types — e.g., `(add 5 3.0)` resolves
            // to (add Double Double) but argValues[0] is still int, throwing
            // InvalidCastException inside StdLib.AddDouble's args[0].As<double>().
            // VoidType.IsCompatibleWith returns false → CanConvertTo returns false →
            // Void-wildcard params (e.g. equals/lt/gt) short-circuit and never coerce.
            var sig = overload.Signature;
            for (int i = 0; i < argValues.Count && i < sig.InputTypes.Count; i++)
            {
                // Phase 26 fix-omissions Blocker 1: Void[] is a true wildcard array
                // parameter — typed arrays (Int[], String[], Float[], ...) pass through
                // to the impl with their original List<Value> storage. ConvertTo has no
                // Value-level path for typed-array → Void[] and would throw, so skip
                // coercion entirely when the parameter is ArrayType(Void).
                // See .planning/phases/26-op-standardization-prefix-only/.continue-here.md
                // "Blocker 1" and 26-RESEARCH.md "Pitfall 2".
                if (sig.InputTypes[i] is ArrayType { ElementType: VoidType })
                {
                    continue;
                }
                // Phase 26.1 DICT-01: Dict<Void, Void> is the wildcard registration shape used
                // by RegisterDict for (get)/(set)/(each)/(map)/(filter)/etc. — typed Dict<K, V>
                // values must pass through to the impl with their original DictData storage.
                // Value.ConvertTo has no path for Dict→Dict and would throw InvalidCastException.
                if (sig.InputTypes[i] is DictType { KeyType: VoidType, ValueType: VoidType })
                {
                    continue;
                }
                // Phase 26.1 TUP-09 / TUP-11: TupleType.AnyArity is the wildcard for (unpack)
                // and (dictTuple) — same skip-coercion rationale as DictType wildcard above.
                if (sig.InputTypes[i] is TupleType { IsAnyArity: true })
                {
                    continue;
                }
                if (!argValues[i].Type.Equals(sig.InputTypes[i])
                    && argValues[i].Type.CanConvertTo(sig.InputTypes[i]))
                {
                    argValues[i] = argValues[i].ConvertTo(sig.InputTypes[i]);
                }
            }
            // Phase 36 Plan 36-05: thread the call-site SourceLocation through
            // ExecutionContext.CurrentCallSite so Phase 36 stochastic combinators
            // (PatternFunctions.sometimes/degrade/sparseSeq) can key their PRNG
            // by (site, name) without a new lambda-signature overload. Save +
            // restore so nested builtin calls see their parent's site after the
            // inner call returns (stack-like discipline without an actual stack).
            //
            // Phase 44 Plan 44-02 D-05 + Pattern S2: adjacent save/restore for
            // CallerStrictMode — snapshots the IMMEDIATE caller's StrictMode at
            // the moment of dispatch so the builtin's body reads the caller's
            // file bit (not the stdlib module's own bit, which is always false
            // per D-03 "stdlib stays charitable internally"). Anti-Pattern 1:
            // never mutate without paired restore in try/finally.
            var prevCallSite = _context.CurrentCallSite;
            var prevCallerStrict = _context.CallerStrictMode;
            _context.CurrentCallSite = call.Location;
            _context.CallerStrictMode = _context.StrictMode;
            try
            {
                // Call internal implementation
                return overload.Implementation!(argValues);
            }
            finally
            {
                _context.CurrentCallSite = prevCallSite;
                _context.CallerStrictMode = prevCallerStrict;
            }
        }
        else
        {
            // Phase 44 Plan 44-02 D-05: user-proc dispatch — snapshot
            // CallerStrictMode BEFORE invoking so a leaf builtin called inside
            // the user proc's body sees the caller's bit. The Interpreter's
            // ExecuteUserFunctionWithCaptures will then swap StrictMode to
            // proc.IsStrict for the proc's body itself, but CallerStrictMode
            // remains pinned to the immediate caller's value until this
            // try/finally restores it on return.
            var prevCallerStrictUser = _context.CallerStrictMode;
            _context.CallerStrictMode = _context.StrictMode;
            try
            {
                // Execute user-defined function (with closure captures if present)
                return _invoker.ExecuteUserFunctionWithCaptures(
                    overload.Declaration!, argValues, overload.CapturedVariables);
            }
            finally
            {
                _context.CallerStrictMode = prevCallerStrictUser;
            }
        }
    }

    private Value EvaluateArrayIndex(ArrayIndexExpression idx)
    {
        // Phase 26.1 TUP-09 (RESEARCH § Q4): this evaluator handles BOTH array `arr@i`
        // and tuple `tup@i` indexing — tuples reuse <see cref="ArrayIndexExpression"/>
        // since their runtime storage shape is the same (<see cref="IReadOnlyList{Value}"/>).
        // Error messages branch on operand type so diagnostics match user-facing semantics.
        var operand = Evaluate(idx.Array);
        var index = Evaluate(idx.Index);

        bool isTuple = operand.Type is TupleType;

        if (operand.Data is not IReadOnlyList<Value> arr)
        {
            _errorReporter.ReportError(
                $"Cannot index non-array/non-tuple type {operand.Type}", idx.Location);
            return Value.Void();
        }

        if (index.Type is not IntType)
        {
            var label = isTuple ? "Tuple" : "Array";
            _errorReporter.ReportError($"{label} index must be Int, not {index.Type}", idx.Location);
            return Value.Void();
        }

        int indexValue = index.As<int>();

        // Support negative indices: -1 is last element, -2 is second-to-last, etc.
        if (indexValue < 0) indexValue = arr.Count + indexValue;

        // Soft-failure model: report error and return Void rather than throwing,
        // allowing the program to continue executing after an out-of-bounds access.
        if (indexValue < 0 || indexValue >= arr.Count)
        {
            var label = isTuple ? "Tuple" : "Array";
            _errorReporter.ReportError(
                $"{label} index {indexValue} out of bounds (0-{arr.Count - 1})", idx.Location);
            return Value.Void();
        }

        return arr[indexValue];
    }

    private Value EvaluateFlowExpression(FlowExpression flowEx)
    {
        // Phase 35 Plan 35-07 (LANG-03): when the parser saw `-> CALL as NAME`,
        // it wrapped the constructed FunctionCallExpression (with Left already
        // prepended to its args by ParseFlowExpression's branches 1+2) inside a
        // FlowExpression carrying IntermediateName. The result of this chain
        // step IS the evaluation of Right (the constructed call); Left is
        // preserved on the AST node for span/diagnostic reasons but is NOT
        // re-applied here. After computing the result, declare the binding in
        // the CURRENT frame per Pitfall 7 so subsequent chain steps + same-
        // block statements can read it.
        if (flowEx.IntermediateName != null)
        {
            var result = Evaluate(flowEx.Right);
            _context.DeclareVariable(flowEx.IntermediateName, result);
            return result;
        }

        var leftVal = Evaluate(flowEx.Left);
        var rightVal = Evaluate(flowEx.Right);

        if (rightVal.Type is FunctionType || rightVal.Data is FunctionOverload)
        {
            var overload = rightVal.Data as FunctionOverload;
            if (overload == null)
            {
                _errorReporter.ReportError($"Right side of -> must be a function, got {rightVal.Type}", flowEx.Location);
                return Value.Void();
            }

            var args = new List<Value> { leftVal };
            // Phase 44 review CR-02: runtime `->` (function-variable RHS) must
            // snapshot CallerStrictMode the same way EvaluateFunctionCall does
            // — without this, strict-aware builtins invoked via `x -> g` (where
            // `g` is a function-variable resolving to a FunctionOverload at
            // runtime) read whatever stale CallerStrictMode the previous
            // foreground call left behind. Mirrors the sandwich at lines
            // 437-450 / 461-472. Anti-Pattern 1: never mutate without paired
            // restore in try/finally.
            var prevCallerStrict = _context.CallerStrictMode;
            _context.CallerStrictMode = _context.StrictMode;
            try
            {
                if (overload.IsInternal) return overload.Implementation!(args);
                else return _invoker.ExecuteUserFunctionWithCaptures(overload.Declaration!, args, overload.CapturedVariables);
            }
            finally
            {
                _context.CallerStrictMode = prevCallerStrict;
            }
        }

        _errorReporter.ReportError($"Cannot apply pipe operator -> to non-function type {rightVal.Type}", flowEx.Location);
        return Value.Void();
    }

    /// <summary>
    /// Phase 35 Plan 35-05 (LANG-01) — evaluates a
    /// <c>(match scrutinee | pat => body | ... | _ => default)</c> expression.
    ///
    /// <para>
    /// Naive linear scan per D-v1.5-11: tests each arm in source order, the
    /// first match wins. On match, pushes a fresh <see cref="StackFrame"/>,
    /// declares each <see cref="BindingPattern"/>-captured value in it,
    /// evaluates the arm body in that scope, pops the frame, and returns
    /// the body Value. Per Pitfall 6 the frame lifecycle ensures bindings
    /// die with the arm body — they DO NOT leak past the match expression
    /// into enclosing scope.
    /// </para>
    ///
    /// <para>
    /// Non-exhaustive policy (Plan 35-05 cut): if no arm matches, the method
    /// silently returns <see cref="Value.Void"/>. Plan 35-06 replaces this
    /// fall-through with the <c>matchExhaustive</c> pragma lookup +
    /// WARN-vs-error policy (D-v1.5-05). The marker comment at the
    /// fall-through site flags the replacement site for Plan 35-06.
    /// </para>
    /// </summary>
    private Value EvaluateMatch(MatchExpression match)
    {
        var scrutinee = Evaluate(match.Scrutinee);

        foreach (var arm in match.Arms)
        {
            var bindings = new Dictionary<string, Value>();
            if (PatternMatcher.PatternMatches(arm.Pattern, scrutinee, bindings, this, _context))
            {
                _context.PushFrame();
                try
                {
                    foreach (var (name, value) in bindings)
                        _context.DeclareVariable(name, value);
                    return Evaluate(arm.Body);
                }
                finally
                {
                    _context.PopFrame();
                }
            }
        }

        // Phase 35 Plan 35-06 (D-v1.5-05) — non-exhaustive policy.
        //
        // Two paths, selected by the file-scope `enable matchExhaustive;`
        // pragma captured on the AST node at parse time. Per Pitfall 4 +
        // Phase 21 D-06 the pragma is PER-FILE (does NOT propagate via
        // `use` imports), so we consult match.CapturedPragmas — the
        // PragmaSet that was active when the MATCH expression itself was
        // parsed — rather than the dynamic context's pragma set.
        //
        // STRICT (pragma enabled): report a FlowDiagnostic at Error level.
        // CHARITABLE (default): emit a one-shot stderr WARN via
        //   RenderingDiagnostics.WarnOnce keyed on the match Span, then
        //   fall through to Value.Void().
        var spanForReport = match.Span ?? Span.At(match.Location);
        var pragmaSet = match.CapturedPragmas ?? _context.ProgramPragmaSet;
        if (pragmaSet is not null && pragmaSet.Has("matchExhaustive"))
        {
            _errorReporter.Report(FlowDiagnostic.Create(
                $"match expression non-exhaustive — no arm matched scrutinee of type {scrutinee.Type}",
                spanForReport));
            return Value.Void();
        }

        // Phase 44 Plan 44-06 (Axis B advisory elevation — HIGH-priority): when
        // the EXECUTING file declared `enable strict;` (StrictMode is the file's
        // pragma bit per D-02/D-03 — match is not a function call so we consult
        // StrictMode directly, not CallerStrictMode which is a per-dispatch
        // snapshot), promote the non-exhaustive advisory to a composer-facing
        // [strict] error via ErrorReporter. The existing WarnOnce sentinel +
        // body remain byte-identical on the non-strict path (Pitfall 5
        // two-run cmp-clean preserved).
        if (_context.StrictMode || _context.CallerStrictMode)
        {
            _errorReporter.ReportError(
                $"[strict] [match] non-exhaustive pattern at {spanForReport} — fell through to Void",
                _context.CurrentCallSite);
            return Value.Void();
        }
        RenderingDiagnostics.WarnOnce(
            $"match-non-exhaustive:{spanForReport}",
            $"warning: match expression at {spanForReport} non-exhaustive — fell through to Void");
        return Value.Void();
    }

    /// <summary>
    /// Phase 26.1 TUP-10: evaluates `tup ~&gt; func`. When LHS is a Tuple, components
    /// unpack into positional args; on non-tuple LHS, falls through to single-arg
    /// `-&gt;` semantics (charitable per ROADMAP success criterion 3 — ergonomics).
    /// </summary>
    private Value EvaluateTupleUnpackFlow(TupleUnpackFlowExpression unpack)
    {
        var leftVal = Evaluate(unpack.Left);

        // Audit §2.4 — build the positional argument list FIRST so we can resolve
        // the RHS function against the ACTUAL argument types. Tuple LHS unpacks into
        // positional args (CONTEXT spec); a non-tuple LHS uses single-arg `->`
        // semantics (charitable fallthrough per ROADMAP success criterion 3).
        List<Value> args =
            (leftVal.Type is TupleType && leftVal.Data is IReadOnlyList<Value> components)
                ? components.ToList()
                : new List<Value> { leftVal };

        // Audit §2.4 — when the RHS is a BARE function name, resolve the overload
        // against the unpacked arg types instead of eagerly evaluating it as a
        // value. Evaluating a bare function variable would (a) auto-invoke a 0-arg
        // overload (EvaluateVariable) — turning `t ~> f` into a call to f() — or
        // (b) blindly return overloads[0], the first registered overload regardless
        // of the piped value's type, then dispatch it with no signature match
        // (internal As<T> casts then throw). Resolving by arg type fixes both.
        FunctionOverload? overload = null;
        if (unpack.Right is VariableExpression rhsVar
            && !_context.CurrentFrame.TryGetVariable(rhsVar.Name, out _)
            && _context.CurrentFrame.GetFunctionOverloads(rhsVar.Name).Count > 0)
        {
            var argTypes = args.Select(a => a.Type).ToArray();
            overload = _context.TryResolveFunction(rhsVar.Name, argTypes);
            if (overload == null)
            {
                // Emit the rich "no matching overload" / "ambiguous" diagnostic.
                _context.ResolveFunction(rhsVar.Name, argTypes, unpack.Location);
                return Value.Void();
            }
        }
        else
        {
            // RHS is a lambda, a variable holding a function value, or any other
            // expression — evaluate it and require a FunctionOverload payload.
            var rightVal = Evaluate(unpack.Right);
            if (rightVal.Type is not FunctionType && rightVal.Data is not FunctionOverload)
            {
                _errorReporter.ReportError(
                    $"Right side of ~> must be a function, got {rightVal.Type}",
                    unpack.Location);
                return Value.Void();
            }
            overload = rightVal.Data as FunctionOverload;
            if (overload == null)
            {
                _errorReporter.ReportError(
                    $"Right side of ~> resolved to non-FunctionOverload value",
                    unpack.Location);
                return Value.Void();
            }
        }

        // Phase 44 review CR-02: snapshot CallerStrictMode around the dispatch
        // so strict-aware builtins called via `~>` see the caller's bit (not
        // whatever stale value the previous foreground call left behind).
        // Mirrors EvaluateFunctionCall lines 437-450 / 461-472. Applied to
        // both the tuple-unpack branch AND the non-tuple fall-through so all
        // `~>` paths have the same call-boundary semantics as `->` / direct
        // call dispatch.
        var prevCallerStrict = _context.CallerStrictMode;
        _context.CallerStrictMode = _context.StrictMode;
        try
        {
            if (overload.IsInternal)
            {
                // Audit §2.4 — coerce args at the impl boundary (mirrors
                // EvaluateFunctionCall) so an overload resolved via convertible
                // scoring reaches the impl with the expected CLR types instead of
                // throwing InvalidCastException inside As<T>.
                CoerceArgsForInternal(overload, args);
                return overload.Implementation!(args);
            }
            return _invoker.ExecuteUserFunctionWithCaptures(
                overload.Declaration!, args, overload.CapturedVariables);
        }
        finally
        {
            _context.CallerStrictMode = prevCallerStrict;
        }
    }

    /// <summary>
    /// Audit §2.4 — boundary coercion for internal (builtin) overloads, factored
    /// from <see cref="EvaluateFunctionCall"/>'s impl-boundary loop. Wildcard slots
    /// (Void[], Dict&lt;Void,Void&gt;, any-arity Tuple) pass through unchanged;
    /// convertible slots are converted in place.
    /// </summary>
    private static void CoerceArgsForInternal(FunctionOverload overload, List<Value> args)
    {
        var sig = overload.Signature;
        for (int i = 0; i < args.Count && i < sig.InputTypes.Count; i++)
        {
            if (sig.InputTypes[i] is ArrayType { ElementType: VoidType })
                continue;
            if (sig.InputTypes[i] is DictType { KeyType: VoidType, ValueType: VoidType })
                continue;
            if (sig.InputTypes[i] is TupleType { IsAnyArity: true })
                continue;
            if (!args[i].Type.Equals(sig.InputTypes[i])
                && args[i].Type.CanConvertTo(sig.InputTypes[i]))
            {
                args[i] = args[i].ConvertTo(sig.InputTypes[i]);
            }
        }
    }

    private Value EvaluateArrayLiteral(ArrayLiteralExpression arrLit)
    {
        var elements = arrLit.Elements.Select(Evaluate).ToList();

        if (elements.Count == 0)
            return Value.Array(elements, VoidType.Instance);

        var elementType = elements[0].Type;
        if (!elements.All(e => e.Type.Equals(elementType)))
            elementType = VoidType.Instance;

        return Value.Array(elements, elementType);
    }

    /// <summary>
    /// Phase 26.1 TUP-09: evaluates <c>&lt;&lt;a, b, c&gt;&gt;</c> by evaluating each element
    /// and recording its FlowType — per-position arity-explicit typing (unlike arrays which
    /// converge to a single element type or VoidType when heterogeneous). Empty <c>&lt;&lt;&gt;&gt;</c>
    /// produces a 0-arity Tuple value.
    /// </summary>
    private Value EvaluateTupleLiteral(TupleLiteralExpression tupLit)
    {
        var components = new List<Value>(tupLit.Elements.Count);
        var elementTypes = new List<FlowType>(tupLit.Elements.Count);
        foreach (var elem in tupLit.Elements)
        {
            var v = Evaluate(elem);
            components.Add(v);
            elementTypes.Add(v.Type);
        }
        return Value.Tuple(components, elementTypes);
    }

    /// <summary>
    /// Evaluates a lambda expression. Synthesizes a <see cref="ProcDeclaration"/>
    /// (named <c>__lambda_{GUID}</c>) and wraps it as a <see cref="FunctionOverload"/>
    /// closure with snapshot variable capture.
    ///
    /// <para>
    /// Phase 44 review WR-09 — CROSS-FILE STRICT SEMANTICS (lexical, not call-site):
    /// </para>
    ///
    /// <para>
    /// <c>proc.IsStrict</c> is captured from <c>_context.StrictMode</c> at the
    /// CREATION SITE — i.e. it preserves whichever strict bit was active in
    /// the file declaring the lambda, not the file invoking it later. This
    /// matches the file-scope <c>enable strict;</c> contract (D-03): a lambda
    /// inherits its DECLARING file's strict bit and carries that bit into
    /// every later invocation, regardless of where it gets called from.
    /// </para>
    ///
    /// <para>
    /// Practical consequences:
    /// <list type="bullet">
    /// <item>A strict-file lambda passed to a NON-strict library's higher-
    /// order function (e.g. <c>each</c> / <c>map</c>) executes its body
    /// with strict semantics. <c>(print 5)</c> inside that lambda raises
    /// <c>[strict] (print) requires String</c>.</item>
    /// <item>A non-strict-file lambda passed to a STRICT library's higher-
    /// order function executes with charitable semantics. <c>(print 5)</c>
    /// inside that lambda silently coerces.</item>
    /// <item>A lambda created in a strict file then stored in non-strict
    /// library state (e.g. <c>(registerStyle ...)</c>) and fired later
    /// from a non-strict caller still runs with strict semantics.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Mechanism: <c>ExecuteUserFunctionWithCaptures</c> pushes
    /// <c>ctx.StrictMode = proc.IsStrict</c> on entry to the lambda body
    /// (try/finally restore on exit). The caller's strict snapshot
    /// (<c>CallerStrictMode</c>) is unchanged by this push, so leaf builtins
    /// inside the lambda body still see the caller's bit at dispatch time —
    /// but <c>EvaluateFunctionCall</c> re-snapshots
    /// <c>CallerStrictMode = _context.StrictMode</c> before invoking each
    /// leaf, so a (print 5) inside the lambda body reads the strict bit of
    /// the lambda's DECLARING file, not the lambda's caller.
    /// </para>
    ///
    /// <para>
    /// This is intentional under D-03 but surprising for composers handing
    /// strict-file lambdas to charitable libraries — document at the API
    /// boundary if you accept lambdas (see <c>ProcDeclaration.IsStrict</c>).
    /// Per <c>memory/project_pre_public_no_legacy_burden.md</c>, the
    /// alternative (call-site scoped strict) could be revisited pre-traction
    /// if the cross-file semantics prove confusing in practice.
    /// </para>
    /// </summary>
    private Value EvaluateLambda(LambdaExpression lambda)
    {
        var uniqueName = $"__lambda_{Guid.NewGuid():N}";
        var parameters = lambda.Parameters.Select(p =>
            new Parameter(p.Name, p.Type)).ToList();

        var body = lambda.Body.ToList();
        // Phase 44 Plan 44-02 (Rule 2 auto-add): lambdas inherit the strict bit
        // of the surrounding lexical scope at creation time. The ProcDeclaration
        // synthesized here flows through ExecuteUserFunctionWithCaptures which
        // pushes ctx.StrictMode = proc.IsStrict — without this capture the
        // lambda body would lose the strict bit on invocation, breaking the
        // D-03 file-scope contract for inline closures (e.g. lambdas passed to
        // higher-order builtins inside a strict file).
        //
        // Phase 44 review WR-09: the doc comment above describes the
        // cross-file semantics in detail — strict bit is LEXICAL (file at
        // creation), not DYNAMIC (file at invocation).
        var proc = new ProcDeclaration(
            lambda.Location, uniqueName, parameters, body, false,
            IsStrict: _context.StrictMode,
            // Phase 45 Plan 45-06 D-04 — lambdas inherit the beat-true-to-sig bit
            // of the surrounding lexical scope at creation time (same LEXICAL-not-
            // DYNAMIC rule as IsStrict above). A lambda created in a pragma-on file
            // keeps the multiplier on (beat N) calls even when invoked from a
            // pragma-off caller; a lambda created in a pragma-off file does not.
            IsBeatTrueToSig: _context.BeatTrueToSig);

        // Snapshot capture: capture all currently visible variables at lambda creation time.
        // This ensures the lambda sees the values as they were when it was created,
        // not any later mutations (immutable-leaning semantics).
        var capturedVars = _context.CurrentFrame.GetAllAccessibleVariables();

        var inputTypes = parameters.Select(p => p.Type).ToList();
        var signature = new FunctionSignature(uniqueName, inputTypes);
        var overload = FunctionOverload.UserDefined(uniqueName, signature, proc, capturedVars);

        return Value.Function(overload);
    }

    private Value EvaluateMemberAccess(MemberAccessExpression member)
    {
        // Phase 43 Plan 43-03 D-02 — registry-first branch. When the LHS is a bare
        // identifier that matches a registered module name, return the named proc as
        // a Function Value. Falls through to the existing instance-member dispatch
        // (chord.root / song.sections / voice.Pan / track.SampleRate / etc.) on miss.
        //
        // Registry-first because:
        //   (a) Cheaper check — dict lookup vs. potentially-failing variable evaluation
        //       (a bare `math` identifier is NOT a variable; the existing code path
        //       would error with "Variable 'math' not found").
        //   (b) Clearer errors — unknown proc on a registered module says
        //       "module 'math' has no proc 'foo'" instead of "Variable 'math' not found".
        //   (c) Preserves Pitfall 2 — chord/song/voice/track LHSes evaluate to non-null
        //       values that don't have entries in ModuleRegistry; only bare
        //       VariableExpression references to REGISTERED module names hit this branch.
        //
        // The qualified-call form `(mod.fn args)` is handled at EvaluateFunctionCall
        // (where call.Name carries the dot); this branch covers the value-reference
        // form `mod.fn` (e.g., `Function f = math.sin`, `(print mod.fn)`).
        if (member.Object is VariableExpression varExpr
            && _context.ModuleRegistry.TryGetProc(varExpr.Name, member.MemberName, out var procValue))
        {
            return procValue!;
        }
        if (member.Object is VariableExpression varExpr2
            && _context.ModuleRegistry.Contains(varExpr2.Name))
        {
            // Module is registered but the member is not a proc in this module — clearer error.
            _errorReporter.ReportError(
                $"[module] module '{varExpr2.Name}' has no proc '{member.MemberName}'",
                member.Location);
            return Value.Void();
        }

        var obj = Evaluate(member.Object);

        // Handle known types with property maps
        if (obj.Data is StandardLibrary.Audio.Voice voice)
        {
            return member.MemberName switch
            {
                "OffsetBeats" => Value.Double(voice.OffsetBeats),
                "Gain" => Value.Double(voice.Gain),
                "Pan" => Value.Double(voice.Pan),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        if (obj.Data is StandardLibrary.Audio.Track track)
        {
            return member.MemberName switch
            {
                "SampleRate" => Value.Int(track.SampleRate),
                "Channels" => Value.Int(track.Channels),
                "OffsetBeats" => Value.Double(track.OffsetBeats),
                "Gain" => Value.Double(track.Gain),
                "Pan" => Value.Double(track.Pan),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        if (obj.Data is ChordData chordData)
        {
            return member.MemberName switch
            {
                "Root" => Value.String(chordData.Root),
                "Quality" => Value.String(chordData.Quality),
                "Octave" => Value.Int(chordData.Octave),
                "NoteNames" => Value.Array(
                    chordData.NoteNames.Select(n => Value.String(n)).ToArray(),
                    TypeSystem.PrimitiveTypes.StringType.Instance),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        if (obj.Data is TypeSystem.SpecialTypes.BarData barData)
        {
            return member.MemberName switch
            {
                "TimeSignature" => Value.TimeSignature(barData.TimeSignature),
                "Count" => Value.Int(barData.Notes.Count),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        if (obj.Data is SectionData sectionData)
        {
            return member.MemberName switch
            {
                "Name" => Value.String(sectionData.Name),
                "SequenceCount" => Value.Int(sectionData.Sequences.Count),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        if (obj.Data is SongData songData)
        {
            return member.MemberName switch
            {
                "SectionCount" => Value.Int(songData.Sections.Count),
                _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
            };
        }

        // Fallback: try reflection
        var prop = obj.Data?.GetType().GetProperty(member.MemberName);
        if (prop != null)
        {
            var val = prop.GetValue(obj.Data);
            return Value.From(val);
        }

        return ReportUnknownMember(obj.Type, member.MemberName, member.Location);
    }

    private Value ReportUnknownMember(FlowType type, string memberName, Core.SourceLocation location)
    {
        _errorReporter.ReportError($"Type '{type}' has no member '{memberName}'", location);
        return Value.Void();
    }

    private Value ReportDivisionByZero(Core.SourceLocation location)
    {
        _errorReporter.ReportError("Division by zero", location);
        return Value.Void();
    }

    private Value EvaluateLazy(LazyExpression lazy)
    {
        // Create a thunk that captures the expression and evaluator
        // Don't evaluate the inner expression yet!
        var thunk = new Thunk(lazy.InnerExpression, this);

        // Determine the inner type (simplified - assume Void for now, proper type inference would be better)
        // In a full implementation, you'd want to infer the type from the expression
        var innerType = lazy.InnerExpression.ResolvedType ?? VoidType.Instance;

        return Value.Lazy(thunk, innerType);
    }

    /// <summary>
    /// Evaluates a note stream expression into a Sequence value using the active musical context.
    /// </summary>
    private Value EvaluateChordLiteral(ChordLiteralExpression chordLit)
    {
        if (ChordParser.TryParse(chordLit.ChordText, out var chordData))
        {
            return Value.Chord(chordData!);
        }

        _errorReporter.ReportError($"Invalid chord symbol: '{chordLit.ChordText}'", chordLit.Location);
        return Value.Void();
    }

    /// <summary>
    /// Phase 26.1 SYM-01: evaluates a <c>#foo</c> symbol literal by interning it via
    /// <see cref="RuntimeContext.SymbolInternTable"/>. Two evaluations of <c>#foo</c> in the
    /// same context return the SAME <see cref="Value"/> instance (reference-equal).
    /// </summary>
    private Value EvaluateSymbolLiteral(SymbolLiteralExpression symLit)
    {
        return Value.Symbol(symLit.Name, _context);
    }

    /// <summary>
    /// Phase 45 D-10 — evaluates a <see cref="BeatLiteralExpression"/> (<c>Nb</c>)
    /// applying the eval-time true-to-sig multiplier:
    /// <code>final = pragma_on ? raw × (4.0 / denom) : raw</code>
    /// where <c>denom</c> is the active <see cref="MusicalContext.TimeSignature"/>
    /// denominator (defaulting to 4 — i.e. 4/4 identity — when no timesig is set,
    /// per D-02 / Pitfall 4). The pragma bit lives on
    /// <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/> (set by
    /// the declaring file's <c>enable beat-true-to-sig;</c>, file-scoped per D-04).
    /// With pragma OFF the multiplier is always 1.0 (raw passes through); with
    /// pragma ON in 4/4 (or no timesig) the multiplier is 4/4 = 1.0 (identity) —
    /// activation never corrupts scripts that never set a non-quarter meter.
    /// Internal storage stays quarter-relative (<see cref="Value.Beat(double)"/>),
    /// so every downstream Beat consumer is unaffected (construction-only desugar).
    /// </summary>
    private Value EvaluateBeatLiteral(BeatLiteralExpression beatLit)
    {
        // D-02 three-tier fallback: GetMusicalContext() resolves call-stack →
        // FlowConfig → default 4/4. TimeSignature?.Denominator ?? 4 keeps the
        // divide-by-zero-proof identity default (T-45-09 mitigation).
        int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
        double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
        return Value.Beat(beatLit.RawValue * multiplier);
    }

    private Value EvaluateNoteStream(NoteStreamExpression noteStream)
    {
        var context = _context.GetMusicalContext();
        // TUP-05: thread the engine's ErrorReporter so ValidateBarFit can emit
        // Info-severity bar-overflow diagnostics. Backward-compatible defaulted-parameter
        // pattern — Plan 19-01/19-02 unit Facts continue using the parameterless ctor.
        var compiler = new NoteStreamCompiler(_errorReporter);
        var sequence = compiler.Compile(noteStream, context, _context);
        return Value.Sequence(sequence);
    }

    private Value EvaluateProgression(ProgressionExpression progression)
    {
        var context = _context.GetMusicalContext();
        if (context.Key == null)
        {
            _errorReporter.ReportError(
                "progression requires an active key context (use `key Cmajor { ... }`)",
                progression.Location);
            return Value.Void();
        }

        var compiler = new ProgressionCompiler();
        var sequence = compiler.Compile(progression, context);
        return Value.Sequence(sequence);
    }

    private Value EvaluateInterpolatedString(InterpolatedStringExpression expr)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in expr.Parts)
        {
            var val = Evaluate(part);
            // Raw String values append verbatim (no added quotes). Everything else —
            // including Symbol, whose underlying CLR Data is also a string — renders
            // via Value.ToString so interpolation matches (str x) output.
            if (val.Type is StringType && val.Data is string s)
                sb.Append(s);
            else
                sb.Append(val.ToString());
        }
        return Value.String(sb.ToString());
    }

    private Value EvaluateSong(SongExpression song)
    {
        var sectionRefs = new List<SongSectionRef>();
        var flatRegistry = new Dictionary<string, SectionData>();

        // Phase 36 Plan 36-10 (D-36-13) — when the parser populated
        // song.Elements (mixed BareSectionElement + SectionCallElement), the
        // ELEMENT path is canonical. Each SectionCallElement materializes a
        // SectionData via OverloadResolver dispatch + synthetic-frame body
        // execution, registered under a unique synthetic name so the
        // downstream renderer sees a flat registry of zero-arg-shaped
        // entries (preserves SongRenderer / MidiExport / SfzSampleCache
        // backward compatibility).

        var elements = song.Elements;
        if (elements != null)
        {
            int callIdx = 0;
            foreach (var elem in elements)
            {
                if (elem is BareSectionElement bare)
                {
                    if (!_context.SectionRegistry.TryGetValue(bare.Name, out var existing))
                    {
                        _errorReporter.ReportError(
                            $"Undefined section '{bare.Name}' in song arrangement", song.Location);
                        return Value.Void();
                    }
                    // Bare reference dispatches to the zero-arg overload (Parameters==null)
                    // if present, else the LAST-registered overload.
                    SectionData? target = null;
                    foreach (var s in existing)
                        if (s.Parameters == null) { target = s; break; }
                    target ??= existing[existing.Count - 1];
                    if (bare.RepeatCount <= 0)
                    {
                        _errorReporter.ReportError(
                            $"Repeat count must be positive, got {bare.RepeatCount} for section '{bare.Name}'",
                            song.Location);
                        return Value.Void();
                    }
                    flatRegistry[bare.Name] = target;
                    sectionRefs.Add(new SongSectionRef(bare.Name, bare.RepeatCount));
                }
                else if (elem is SectionCallElement call)
                {
                    var materialized = EvaluateSectionCallToData(call);
                    if (materialized == null)
                        return Value.Void();  // diagnostic already emitted
                    var syntheticName = $"{call.Name}#{callIdx++}";
                    flatRegistry[syntheticName] = materialized;
                    sectionRefs.Add(new SongSectionRef(syntheticName, call.RepeatCount));
                }
            }
        }
        else
        {
            // Pre-Phase-36 path (defensive — parser should always populate Elements now)
            foreach (var sectionRef in song.Sections)
            {
                if (!_context.SectionRegistry.TryGetValue(sectionRef.Name, out var existing))
                {
                    _errorReporter.ReportError(
                        $"Undefined section '{sectionRef.Name}' in song arrangement", song.Location);
                    return Value.Void();
                }
                if (sectionRef.RepeatCount <= 0)
                {
                    _errorReporter.ReportError(
                        $"Repeat count must be positive, got {sectionRef.RepeatCount} for section '{sectionRef.Name}'",
                        song.Location);
                    return Value.Void();
                }
                SectionData? target = null;
                foreach (var s in existing)
                    if (s.Parameters == null) { target = s; break; }
                target ??= existing[existing.Count - 1];
                flatRegistry[sectionRef.Name] = target;
                sectionRefs.Add(new SongSectionRef(sectionRef.Name, sectionRef.RepeatCount));
            }
        }

        var songData = new SongData(sectionRefs, flatRegistry);
        return Value.Song(songData);
    }

    /// <summary>
    /// Phase 36 Plan 36-10 (SECT-01) — dispatches a section call through
    /// OverloadResolver, evaluates the matched section's body under a
    /// synthetic frame with bound parameter values (Pitfall 7 dynamic
    /// scope — the synthetic frame inherits the CALLSITE's MusicalContext,
    /// not the declaration's), and returns the materialized SectionData
    /// (sequences harvested from the body's local variables + bare-expr capture).
    /// Returns <c>null</c> on dispatch failure (diagnostic already emitted).
    /// </summary>
    private SectionData? EvaluateSectionCallToData(SectionCallElement call)
    {
        if (!_context.SectionRegistry.TryGetValue(call.Name, out var candidates))
        {
            _errorReporter.ReportError(
                $"Undefined section '{call.Name}' in song arrangement", call.Location);
            return null;
        }

        // Evaluate positional args
        var posValues = new List<Value>();
        foreach (var argExpr in call.PositionalArgs)
            posValues.Add(Evaluate(argExpr));

        // Evaluate named args
        Dictionary<string, Value>? namedValues = null;
        if (call.NamedArgs != null && call.NamedArgs.Count > 0)
        {
            namedValues = new Dictionary<string, Value>();
            foreach (var (n, vexpr) in call.NamedArgs)
                namedValues[n] = Evaluate(vexpr);
        }

        // OverloadResolver dispatch — scan candidates for a match
        var matched = FlowLang.Interpreter.SectionOverloadDispatch.Resolve(
            call.Name,
            candidates,
            posValues,
            namedValues,
            _context,
            _errorReporter,
            this,
            call.Location);

        if (matched == null)
            return null;  // diagnostic emitted by dispatcher

        var (section, finalArgValues, bindings) = matched.Value;

        // Synthetic-frame execution
        _context.PushFrame();
        try
        {
            foreach (var (n, v) in bindings)
                _context.DeclareVariable(n, v);

            var musicalContext = _context.GetMusicalContext();

            // Re-run the body. Same shape as Interpreter.ExecuteSectionDeclaration's
            // body-execution block — we mirror it here because the section is
            // re-evaluated per call site with different bindings.
            var bareExprSeqs = new List<SequenceData>();
            // Note: we don't have access to _activeSectionBareExpressions from
            // the ExpressionEvaluator. The section body's bare-expression
            // sequences are captured via the local-variable scan + a manual
            // post-pass.

            // Audit §2.3 — fence the body re-execution against return-flag leakage.
            // This re-execution happens during SONG evaluation, which may itself be
            // inside a user proc. Without save/restore, (a) a return flag leaked from
            // BEFORE this call would make ExecuteStatement's top guard skip the whole
            // section body, and (b) a `return` INSIDE the called section would become
            // the enclosing proc's return value. Save+clear before, restore (and
            // report any in-section return) after — mirroring
            // Interpreter.ExecuteUserFunctionWithCaptures' discipline.
            var interp = _invoker as Interpreter;
            Value? savedReturn = interp?.SaveAndClearReturnValue();
            try
            {
                if (section.Body != null)
                {
                    foreach (var stmt in section.Body)
                    {
                        // Use the parent Interpreter via _context.Invoker indirection;
                        // Since we don't have direct Interpreter ref here, fall through
                        // to a dispatched re-execution by invoking ExecuteStatement
                        // through the ExecutionContext's invoker.
                        _context.Invoker!.ExecuteStatement(stmt);

                        if (stmt is ExpressionStatement
                            && _context.Invoker.LastExpressionValue?.Data is SequenceData exprSeq)
                        {
                            bareExprSeqs.Add(exprSeq);
                        }
                    }
                }
            }
            finally
            {
                interp?.RestoreReturnValueAfterSection(savedReturn, call.Location);
            }

            var sequences = new Dictionary<string, SequenceData>();
            foreach (var (n, val) in _context.CurrentFrame.GetLocalVariables())
            {
                if (val.Data is SequenceData seq)
                    sequences[n] = seq;
            }
            for (int i = 0; i < bareExprSeqs.Count; i++)
            {
                if (!sequences.ContainsValue(bareExprSeqs[i]))
                    sequences[$"_anon_{i}"] = bareExprSeqs[i];
            }

            return new SectionData(
                section.Name,
                sequences,
                musicalContext,
                call.Location);
        }
        finally
        {
            _context.PopFrame();
        }
    }
}
