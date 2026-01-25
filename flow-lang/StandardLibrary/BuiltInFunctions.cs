using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Registers Flow built-in functions with their C# implementations.
/// Actual implementations are in stdlib.cs.
/// </summary>
public static class BuiltInFunctions
{
    /// <summary>
    /// Registers all C# implementations of internal functions.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry)
    {
        // ===== Array Functions =====

        var listSignature = new FunctionSignature(
            "list",
            [VoidType.Instance],
            IsVarArgs: true);
        registry.Register("list", listSignature, stdlib.List);

        var lenSignature = new FunctionSignature("len", [new ArrayType(VoidType.Instance)]);
        registry.Register("len", lenSignature, stdlib.Len);
        
        var lenStrSignature = new FunctionSignature("len", [StringType.Instance]);
        registry.Register("len", lenStrSignature, stdlib.LenString);
     

        // ===== I/O Functions =====

        var printSignature = new FunctionSignature(
            "print",
            [StringType.Instance]);
        registry.Register("print", printSignature, stdlib.Print);

        // ===== String Conversion Functions =====

        var strIntSignature = new FunctionSignature("str", [IntType.Instance]);
        registry.Register("str", strIntSignature, stdlib.StrInt);

        var strFloatSignature = new FunctionSignature("str", [FloatType.Instance]);
        registry.Register("str", strFloatSignature, stdlib.StrFloat);

        var strDoubleSignature = new FunctionSignature("str", [DoubleType.Instance]);
        registry.Register("str", strDoubleSignature, stdlib.StrDouble);

        var strStringSignature = new FunctionSignature("str", [StringType.Instance]);
        registry.Register("str", strStringSignature, stdlib.StrString);

        var strBoolSignature = new FunctionSignature("str", [BoolType.Instance]);
        registry.Register("str", strBoolSignature, stdlib.StrBool);

        var strNoteSignature = new FunctionSignature("str", [NoteType.Instance]);
        registry.Register("str", strNoteSignature, stdlib.StrNote);

        var strSemitoneSignature = new FunctionSignature("str", [SemitoneType.Instance]);
        registry.Register("str", strSemitoneSignature, stdlib.StrSemitone);

        var strCentSignature = new FunctionSignature("str", [CentType.Instance]);
        registry.Register("str", strCentSignature, stdlib.StrCent);

        var strMillisecondSignature = new FunctionSignature("str", [MillisecondType.Instance]);
        registry.Register("str", strMillisecondSignature, stdlib.StrMillisecond);

        var strSecondSignature = new FunctionSignature("str", [SecondType.Instance]);
        registry.Register("str", strSecondSignature, stdlib.StrSecond);

        var strDecibelSignature = new FunctionSignature("str", [DecibelType.Instance]);
        registry.Register("str", strDecibelSignature, stdlib.StrDecibel);

        var strArraySignature = new FunctionSignature("str", [new ArrayType(VoidType.Instance)]);
        registry.Register("str", strArraySignature, stdlib.StrArray);

        // ===== Arithmetic Functions =====

        var addIntSignature = new FunctionSignature(
            "add",
            [IntType.Instance, IntType.Instance]);
        registry.Register("add", addIntSignature, stdlib.AddInt);

        var addFloatSignature = new FunctionSignature(
            "add",
            [FloatType.Instance, FloatType.Instance]);
        registry.Register("add", addFloatSignature, stdlib.AddFloat);

        var subSignature = new FunctionSignature(
            "sub",
            [IntType.Instance, IntType.Instance]);
        registry.Register("sub", subSignature, stdlib.SubInt);

        var mulSignature = new FunctionSignature(
            "mul",
            [IntType.Instance, IntType.Instance]);
        registry.Register("mul", mulSignature, stdlib.MulInt);

        var divSignature = new FunctionSignature(
            "div",
            [IntType.Instance, IntType.Instance]);
        registry.Register("div", divSignature, stdlib.DivInt);

        // ===== Lazy Evaluation Functions =====

        // Note: eval is registered with Lazy<Void> but will work with any Lazy<T>
        // due to special handling in the implementation
        var evalSignature = new FunctionSignature(
            "eval",
            [new LazyType(VoidType.Instance)]);
        registry.Register("eval", evalSignature, stdlib.Eval);
        
        var ifSignature = new FunctionSignature(
            "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)]);
        registry.Register("if", ifSignature, stdlib.If);
        
        var andSignature = new FunctionSignature(
            "and", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)]);
        registry.Register("and", andSignature, stdlib.And);
        
        var andBoolSignature = new FunctionSignature(
            "and", [BoolType.Instance, BoolType.Instance]);
        registry.Register("and", andBoolSignature, stdlib.AndBool);
        
        var orSignature = new FunctionSignature(
            "or", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)]);
        registry.Register("or", andSignature, stdlib.Or);
        
        var orBoolSignature = new FunctionSignature(
            "or", [BoolType.Instance, BoolType.Instance]);
        registry.Register("or", orBoolSignature, stdlib.OrBool);
    }
}
