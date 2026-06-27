using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 45 D-05 — pragma-aware (beat Double) → Beat constructor.
/// Migrates the plain Register call at BuiltInFunctions.cs:547-555 to
/// RegisterContextDependent so the lambda has access to
/// ExecutionContext.BeatTrueToSig + active MusicalContext.TimeSignature
/// at call time. Multiplier formula matches
/// ExpressionEvaluator.EvaluateBeatLiteral exactly:
/// final = pragma_on ? raw × (4.0 / denom) : raw.
/// Preserves Phase 26.1 DICT-01 acceptance (Tuple-of-hashables Dict key).
/// </summary>
public static class BeatConstructorFunctions
{
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var sig = new FunctionSignature("beat", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("beat", sig, args =>
        {
            double raw = args[0].As<double>();
            int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
            double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
            return Value.Beat(raw * multiplier);
        });
    }
}
