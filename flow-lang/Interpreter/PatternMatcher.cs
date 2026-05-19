using FlowLang.Ast;
using FlowLang.Ast.Patterns;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.Interpreter;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — runtime dispatch for pattern matching.
///
/// <para>
/// Implements the NAIVE LINEAR SCAN per D-v1.5-11: each pattern is tested in
/// source order against the scrutinee; the first matching arm wins. The
/// decision-tree compile (Jacobs &amp; Peterse 2008) was DEFERRED to v1.6 per
/// RESEARCH § Open Question 1 — the expected match-arm count in Flow is
/// small (3-15), so the back-end can swap to decision-tree later without any
/// composer-visible change.
/// </para>
///
/// <para>
/// Plan 35-05 ships LiteralPattern, WildcardPattern, BindingPattern, and
/// GuardPattern dispatch. ConstructorPattern's music-aware extractors
/// (chord-quality / roman-numeral / articulation-symbol per the discriminator
/// flags on <see cref="ConstructorPattern"/>) ship in Plan 35-06; in 35-05
/// the constructor branch falls through to <c>false</c> so the parent
/// MatchExpression silently returns <see cref="Value.Void"/> on no-match.
/// </para>
/// </summary>
public static class PatternMatcher
{
    /// <summary>
    /// Attempts to match <paramref name="pattern"/> against
    /// <paramref name="scrutinee"/>. On success returns true and populates
    /// <paramref name="bindings"/> with any captured identifier-to-Value pairs
    /// (from <see cref="BindingPattern"/>s). On failure returns false; the
    /// caller's accumulated bindings are left as-is (a wrapping GuardPattern
    /// or successful sibling may still consume them, but a failed pattern
    /// MUST NOT visibly mutate the caller's binding map — the
    /// <see cref="ExpressionEvaluator.EvaluateMatch"/> dispatcher discards
    /// per-arm bindings on failure by passing a fresh dictionary per arm).
    /// </summary>
    /// <param name="pattern">The Pattern AST node to test.</param>
    /// <param name="scrutinee">The Value produced by evaluating the match
    /// expression's scrutinee subexpression.</param>
    /// <param name="bindings">Accumulator for identifier-to-Value pairs
    /// produced by BindingPattern (and any sibling patterns Plan 35-06
    /// introduces).</param>
    /// <param name="evaluator">Used by <see cref="GuardPattern"/> to evaluate
    /// its guard expression in the scope extended with this pattern's
    /// accumulated bindings.</param>
    /// <param name="context">The runtime context — used by the guard
    /// evaluator to push/pop the binding frame around the guard's
    /// evaluation so the guard can read bindings made by its
    /// <see cref="GuardPattern.Inner"/>.</param>
    public static bool PatternMatches(
        Pattern pattern,
        Value scrutinee,
        Dictionary<string, Value> bindings,
        ExpressionEvaluator evaluator,
        Runtime.ExecutionContext context)
    {
        return pattern switch
        {
            WildcardPattern => true,
            BindingPattern b => Bind(b.Name, scrutinee, bindings),
            LiteralPattern lit => MatchLiteral(lit, scrutinee),
            ConstructorPattern ctor => MatchConstructor(ctor, scrutinee, bindings, evaluator, context),
            GuardPattern guard => MatchGuard(guard, scrutinee, bindings, evaluator, context),
            _ => throw new NotSupportedException($"Unknown pattern: {pattern.GetType().Name}"),
        };
    }

    private static bool Bind(string name, Value scrutinee, Dictionary<string, Value> bindings)
    {
        // Last write wins if the same name appears more than once in a single
        // pattern — current 35-05 surface only allows ONE BindingPattern per
        // arm (no nested structures), so collisions are unreachable here, but
        // the contract is documented for Plan 35-06's nested constructor work.
        bindings[name] = scrutinee;
        return true;
    }

    private static bool MatchLiteral(LiteralPattern lit, Value scrutinee)
    {
        // Wrap the embedded literal payload (int / double / bool / string)
        // in a Value so it routes through Utils.LooseEquals — which already
        // handles cross-type numeric comparison (Int vs. Double per the
        // widening chain) consistent with the rest of the language.
        var lhs = Value.From(lit.Value);
        return Utils.LooseEquals(scrutinee, lhs);
    }

    private static bool MatchConstructor(
        ConstructorPattern ctor,
        Value scrutinee,
        Dictionary<string, Value> bindings,
        ExpressionEvaluator evaluator,
        Runtime.ExecutionContext context)
    {
        // Plan 35-06: dispatch via IsChordLiteral / IsRomanNumeral /
        // IsArticulationSymbol flags into ChordParser.Parse, HarmonyFunctions
        // .resolveNumeral, and Articulation-enum compare respectively. Plan
        // 35-05's surface intentionally returns false for every ConstructorPattern
        // — combined with the silent-Void no-match fall-through in
        // EvaluateMatch, the composer sees a benign "no arm matched" result
        // until Plan 35-06 lights up the music-aware path.
        _ = ctor; _ = scrutinee; _ = bindings; _ = evaluator; _ = context;
        return false;
    }

    private static bool MatchGuard(
        GuardPattern guard,
        Value scrutinee,
        Dictionary<string, Value> bindings,
        ExpressionEvaluator evaluator,
        Runtime.ExecutionContext context)
    {
        // Step 1: inner pattern must match (and may add bindings).
        if (!PatternMatches(guard.Inner, scrutinee, bindings, evaluator, context))
            return false;

        // Step 2: evaluate the guard expression in a frame extended with the
        // accumulated bindings — so the guard can read identifiers introduced
        // by a sibling BindingPattern (e.g., `n when (greater n 0)`). The
        // frame is popped before returning so the bindings stay live ONLY
        // for the guard evaluation; EvaluateMatch sets up its own arm-body
        // frame separately for the actual arm body.
        context.PushFrame();
        try
        {
            foreach (var (name, value) in bindings)
                context.DeclareVariable(name, value);

            var guardResult = evaluator.Evaluate(guard.GuardExpression);
            return guardResult.Type is BoolType && guardResult.As<bool>();
        }
        finally
        {
            context.PopFrame();
        }
    }
}
