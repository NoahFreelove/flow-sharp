using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
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
            LambdaExpression lambda => EvaluateLambda(lambda),
            MemberAccessExpression member => EvaluateMemberAccess(member),
            LazyExpression lazy => EvaluateLazy(lazy),
            NoteStreamExpression noteStream => EvaluateNoteStream(noteStream),
            SongExpression song => EvaluateSong(song),
            ProgressionExpression progression => EvaluateProgression(progression),
            InterpolatedStringExpression interp => EvaluateInterpolatedString(interp),
            FlowExpression flowEx => EvaluateFlowExpression(flowEx),
            TupleUnpackFlowExpression unpackEx => EvaluateTupleUnpackFlow(unpackEx),
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
            string s => TryParseSpecialLiteral(s) ?? Value.String(s),
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
        try
        {
            return _context.GetVariable(var.Name);
        }
        catch (InvalidOperationException)
        {
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
    }

    private Value EvaluateFunctionCall(FunctionCallExpression call)
    {
        // Evaluate all arguments
        var argValues = call.Arguments.Select(Evaluate).ToList();
        var argTypes = argValues.Select(v => v.Type).ToList();

        // Try to resolve function overload
        var overload = _context.TryResolveFunction(call.Name, argTypes);

        // If no function found, try looking up as a variable holding a lambda
        if (overload == null)
        {
            try
            {
                var variable = _context.GetVariable(call.Name);
                if (variable.Data is FunctionOverload varOverload)
                {
                    overload = varOverload;
                }
            }
            catch (InvalidOperationException)
            {
                // Not a variable either
            }
        }

        if (overload == null)
        {
            // Report error using the full resolution path
            _context.ResolveFunction(call.Name, argTypes, call.Location);
            return Value.Void();
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
            // Call internal implementation
            return overload.Implementation!(argValues);
        }
        else
        {
            // Execute user-defined function (with closure captures if present)
            return _invoker.ExecuteUserFunctionWithCaptures(
                overload.Declaration!, argValues, overload.CapturedVariables);
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
            if (overload.IsInternal) return overload.Implementation!(args);
            else return _invoker.ExecuteUserFunctionWithCaptures(overload.Declaration!, args, overload.CapturedVariables);
        }

        _errorReporter.ReportError($"Cannot apply pipe operator -> to non-function type {rightVal.Type}", flowEx.Location);
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
        var rightVal = Evaluate(unpack.Right);

        if (rightVal.Type is not FunctionType && rightVal.Data is not FunctionOverload)
        {
            _errorReporter.ReportError(
                $"Right side of ~> must be a function, got {rightVal.Type}",
                unpack.Location);
            return Value.Void();
        }
        var overload = rightVal.Data as FunctionOverload;
        if (overload == null)
        {
            _errorReporter.ReportError(
                $"Right side of ~> resolved to non-FunctionOverload value",
                unpack.Location);
            return Value.Void();
        }

        // Tuple LHS: unpack components into positional args (CONTEXT spec).
        if (leftVal.Type is TupleType && leftVal.Data is IReadOnlyList<Value> components)
        {
            var args = components.ToList();
            return overload.IsInternal
                ? overload.Implementation!(args)
                : _invoker.ExecuteUserFunctionWithCaptures(
                    overload.Declaration!, args, overload.CapturedVariables);
        }

        // Charitable fallthrough: non-tuple LHS uses single-arg `->` semantics
        // (per ROADMAP success criterion 3, ergonomics-priority memory).
        var singleArg = new List<Value> { leftVal };
        return overload.IsInternal
            ? overload.Implementation!(singleArg)
            : _invoker.ExecuteUserFunctionWithCaptures(
                overload.Declaration!, singleArg, overload.CapturedVariables);
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

    private Value EvaluateLambda(LambdaExpression lambda)
    {
        var uniqueName = $"__lambda_{Guid.NewGuid():N}";
        var parameters = lambda.Parameters.Select(p =>
            new Parameter(p.Name, p.Type)).ToList();

        var body = lambda.Body.ToList();
        var proc = new ProcDeclaration(lambda.Location, uniqueName, parameters, body, false);

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
            if (val.Data is string s)
                sb.Append(s);
            else
                sb.Append(val.Data?.ToString() ?? "");
        }
        return Value.String(sb.ToString());
    }

    private Value EvaluateSong(SongExpression song)
    {
        var sectionRefs = new List<SongSectionRef>();

        foreach (var sectionRef in song.Sections)
        {
            if (!_context.SectionRegistry.ContainsKey(sectionRef.Name))
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

            sectionRefs.Add(new SongSectionRef(sectionRef.Name, sectionRef.RepeatCount));
        }

        var songData = new SongData(sectionRefs, new Dictionary<string, SectionData>(_context.SectionRegistry));
        return Value.Song(songData);
    }
}
