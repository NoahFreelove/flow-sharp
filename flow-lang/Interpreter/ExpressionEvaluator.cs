using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
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
    private readonly Interpreter _interpreter;

    public ExpressionEvaluator(RuntimeContext context, ErrorReporter errorReporter, Interpreter interpreter)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
    }

    public Value Evaluate(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => EvaluateLiteral(lit),
            VariableExpression var => EvaluateVariable(var),
            FunctionCallExpression call => EvaluateFunctionCall(call),
            ArrayIndexExpression idx => EvaluateArrayIndex(idx),
            BinaryExpression bin => EvaluateBinary(bin),
            ArrayLiteralExpression arrLit => EvaluateArrayLiteral(arrLit),
            ChordLiteralExpression chordLit => EvaluateChordLiteral(chordLit),
            LambdaExpression lambda => EvaluateLambda(lambda),
            MemberAccessExpression member => EvaluateMemberAccess(member),
            LazyExpression lazy => EvaluateLazy(lazy),
            NoteStreamExpression noteStream => EvaluateNoteStream(noteStream),
            SongExpression song => EvaluateSong(song),
            _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported")
        };
    }

    private Value EvaluateLiteral(LiteralExpression lit)
    {
        return lit.Value switch
        {
            int i => Value.Int(i),
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
            // Variable not found - check if it's a zero-argument function
            var overload = _context.TryResolveFunction(var.Name, Array.Empty<FlowType>());

            if (overload != null)
            {
                // Call the zero-argument function
                if (overload.IsInternal)
                {
                    return overload.Implementation!(new List<Value>());
                }
                else
                {
                    return _interpreter.ExecuteUserFunction(overload.Declaration!, new List<Value>());
                }
            }

            // Not a variable or function
            _errorReporter.ReportError($"Variable '{var.Name}' not found", var.Location);
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
            // Call internal implementation
            return overload.Implementation!(argValues);
        }
        else
        {
            // Execute user-defined function (with closure captures if present)
            return _interpreter.ExecuteUserFunctionWithCaptures(
                overload.Declaration!, argValues, overload.CapturedVariables);
        }
    }

    private Value EvaluateArrayIndex(ArrayIndexExpression idx)
    {
        var array = Evaluate(idx.Array);
        var index = Evaluate(idx.Index);

        if (array.Data is not IReadOnlyList<Value> arr)
        {
            _errorReporter.ReportError($"Cannot index non-array type {array.Type}", idx.Location);
            return Value.Void();
        }

        if (index.Type is not IntType)
        {
            _errorReporter.ReportError($"Array index must be Int, not {index.Type}", idx.Location);
            return Value.Void();
        }

        int indexValue = index.As<int>();

        // Support negative indices: -1 is last element, -2 is second-to-last, etc.
        if (indexValue < 0) indexValue = arr.Count + indexValue;

        // Soft-failure model: report error and return Void rather than throwing,
        // allowing the program to continue executing after an out-of-bounds access.
        if (indexValue < 0 || indexValue >= arr.Count)
        {
            _errorReporter.ReportError($"Array index {indexValue} out of bounds (0-{arr.Count - 1})", idx.Location);
            return Value.Void();
        }

        return arr[indexValue];
    }

    private Value EvaluateBinary(BinaryExpression bin)
    {
        var left = Evaluate(bin.Left);
        var right = Evaluate(bin.Right);

        // Handle integer arithmetic
        if (left.Type is IntType && right.Type is IntType)
        {
            int l = left.As<int>();
            int r = right.As<int>();

            return bin.Operator switch
            {
                BinaryOperator.Add => Value.Int(l + r),
                BinaryOperator.Subtract => Value.Int(l - r),
                BinaryOperator.Multiply => Value.Int(l * r),
                BinaryOperator.Divide => r != 0 ? Value.Int(l / r) : ReportDivisionByZero(bin.Location),
                _ => throw new NotSupportedException($"Binary operator {bin.Operator} not supported")
            };
        }

        // Handle float/double arithmetic
        if ((left.Type is FloatType or DoubleType) && (right.Type is FloatType or DoubleType))
        {
            double l = left.As<double>();
            double r = right.As<double>();

            return bin.Operator switch
            {
                BinaryOperator.Add => Value.Double(l + r),
                BinaryOperator.Subtract => Value.Double(l - r),
                BinaryOperator.Multiply => Value.Double(l * r),
                BinaryOperator.Divide => r != 0 ? Value.Double(l / r) : ReportDivisionByZero(bin.Location),
                _ => throw new NotSupportedException($"Binary operator {bin.Operator} not supported")
            };
        }

        _errorReporter.ReportError($"Cannot apply operator {bin.Operator} to {left.Type} and {right.Type}", bin.Location);
        return Value.Void();
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

    private Value EvaluateNoteStream(NoteStreamExpression noteStream)
    {
        var context = _context.GetMusicalContext();
        var compiler = new NoteStreamCompiler();
        var sequence = compiler.Compile(noteStream, context);
        return Value.Sequence(sequence);
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
