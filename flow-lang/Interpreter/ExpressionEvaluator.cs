using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Runtime;
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
            FlowExpression flow => EvaluateFlow(flow),
            ArrayIndexExpression idx => EvaluateArrayIndex(idx),
            BinaryExpression bin => EvaluateBinary(bin),
            _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported")
        };
    }

    private Value EvaluateLiteral(LiteralExpression lit)
    {
        return lit.Value switch
        {
            int i => Value.Int(i),
            double d => Value.Double(d),
            string s => Value.String(s),
            bool b => Value.Bool(b),
            _ => throw new NotSupportedException($"Literal type {lit.Value.GetType()} not supported")
        };
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

        // Resolve function overload
        var overload = _context.ResolveFunction(call.Name, argTypes, call.Location);

        if (overload == null)
            return Value.Void();

        // Execute function
        if (overload.IsInternal)
        {
            // Call internal implementation
            return overload.Implementation!(argValues);
        }
        else
        {
            // Execute user-defined function
            return _interpreter.ExecuteUserFunction(overload.Declaration!, argValues);
        }
    }

    private Value EvaluateFlow(FlowExpression flow)
    {
        // Flow expressions should have been transformed by parser,
        // but handle them here just in case
        var left = Evaluate(flow.Left);

        if (flow.Right is FunctionCallExpression funcCall)
        {
            // Prepend left to arguments
            var args = new List<Expression> { new LiteralExpression(flow.Left.Location, left.Data!) };
            args.AddRange(funcCall.Arguments);

            var newCall = funcCall with { Arguments = args };
            return EvaluateFunctionCall(newCall);
        }

        // Fallback: just evaluate right side
        return Evaluate(flow.Right);
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
                BinaryOperator.Divide => r != 0 ? Value.Int(l / r) : throw new DivideByZeroException(),
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
                BinaryOperator.Divide => r != 0 ? Value.Double(l / r) : throw new DivideByZeroException(),
                _ => throw new NotSupportedException($"Binary operator {bin.Operator} not supported")
            };
        }

        _errorReporter.ReportError($"Cannot apply operator {bin.Operator} to {left.Type} and {right.Type}", bin.Location);
        return Value.Void();
    }
}
