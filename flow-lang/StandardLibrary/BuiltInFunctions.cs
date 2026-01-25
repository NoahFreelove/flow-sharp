using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Core built-in functions for the Flow language.
/// </summary>
public static class BuiltInFunctions
{
    /// <summary>
    /// Registers all C# implementations of internal functions.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry)
    {
        // list(Void...: items) - Create array from arguments (varargs)
        var listSignature = new FunctionSignature(
            "list",
            [VoidType.Instance],  // Varargs of any type
            IsVarArgs: true);
        registry.Register("list", listSignature, args =>
        {
            if (args.Count == 0)
                return Value.Array([], VoidType.Instance);

            var elementType = args[0].Type;
            return Value.Array(args.ToList(), elementType);
        });

        // print(String) - Output to console
        var printSignature = new FunctionSignature(
            "print",
            [StringType.Instance]);
        registry.Register("print", printSignature, args =>
        {
            Console.WriteLine(args[0].As<string>());
            return Value.Void();
        });

        // len(Array) - Get array length
        var lenSignature = new FunctionSignature(
            "len",
            [new ArrayType(VoidType.Instance)]);
        registry.Register("len", lenSignature, args =>
        {
            var arr = args[0].As<IReadOnlyList<Value>>();
            return Value.Int(arr.Count);
        });

        // str(Any) - Convert to string (simplified - accepts Int for now)
        var strSignature = new FunctionSignature(
            "str",
            [IntType.Instance]);
        registry.Register("str", strSignature, args =>
        {
            return Value.String(args[0].ToString());
        });

        // Arithmetic functions for demonstrating overloads

        // add(Int, Int) -> Int
        var addIntSignature = new FunctionSignature(
            "add",
            [IntType.Instance, IntType.Instance]);
        registry.Register("add", addIntSignature, args =>
        {
            var a = args[0].As<int>();
            var b = args[1].As<int>();
            return Value.Int(a + b);
        });

        // add(Float, Float) -> Float
        var addFloatSignature = new FunctionSignature(
            "add",
            [FloatType.Instance, FloatType.Instance]);
        registry.Register("add", addFloatSignature, args =>
        {
            var a = args[0].As<double>();
            var b = args[1].As<double>();
            return Value.Float(a + b);
        });

        // sub(Int, Int) -> Int
        var subSignature = new FunctionSignature(
            "sub",
            [IntType.Instance, IntType.Instance]);
        registry.Register("sub", subSignature, args =>
        {
            var a = args[0].As<int>();
            var b = args[1].As<int>();
            return Value.Int(a - b);
        });

        // mul(Int, Int) -> Int
        var mulSignature = new FunctionSignature(
            "mul",
            [IntType.Instance, IntType.Instance]);
        registry.Register("mul", mulSignature, args =>
        {
            var a = args[0].As<int>();
            var b = args[1].As<int>();
            return Value.Int(a * b);
        });

        // div(Int, Int) -> Int
        var divSignature = new FunctionSignature(
            "div",
            [IntType.Instance, IntType.Instance]);
        registry.Register("div", divSignature, args =>
        {
            var a = args[0].As<int>();
            var b = args[1].As<int>();
            if (b == 0) throw new DivideByZeroException();
            return Value.Int(a / b);
        });
    }
}
