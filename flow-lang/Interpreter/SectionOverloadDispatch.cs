using FlowLang.Ast;
using FlowLang.Ast.Patterns;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Interpreter;

/// <summary>
/// Phase 36 Plan 36-10 (D-36-18 SECT-01) — runtime dispatcher that picks
/// a SectionData overload from a candidate list based on Phase 35 pattern
/// matching + specificity scoring.
///
/// <para>
/// Three-stage flow:
/// <list type="number">
///   <item><description>Fold positional + named args + default values into a
///   final positional <c>Value[]</c> matching each candidate's parameter
///   shape (D-36-15 default-value support).</description></item>
///   <item><description>Run <see cref="PatternMatcher.TryMatchAll"/> against
///   each candidate; drop misses.</description></item>
///   <item><description>Pick the survivor with the highest specificity sum;
///   ties raise an Ambiguous-overload diagnostic.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Failure modes (all routed through the supplied <see cref="ErrorReporter"/>):
/// <list type="bullet">
///   <item><description>0 candidates → "no overload of section &lt;name&gt;
///   matches arguments" (D-36-16).</description></item>
///   <item><description>Ambiguous → "Ambiguous section overload" + the two
///   tied candidate source locations.</description></item>
/// </list>
/// </para>
/// </summary>
public static class SectionOverloadDispatch
{
    public static (SectionData section, IReadOnlyList<Value> finalArgs, Dictionary<string, Value> bindings)?
        Resolve(
            string sectionName,
            IReadOnlyList<SectionData> candidates,
            IReadOnlyList<Value> positionalArgs,
            IReadOnlyDictionary<string, Value>? namedArgs,
            Runtime.ExecutionContext context,
            ErrorReporter errorReporter,
            ExpressionEvaluator evaluator,
            SourceLocation? location)
    {
        if (candidates.Count == 0)
        {
            errorReporter.ReportError(
                $"no section '{sectionName}' is registered", location);
            return null;
        }

        // Collect successful matches with their specificity
        var matches = new List<(SectionData section, IReadOnlyList<Value> finalArgs, Dictionary<string, Value> bindings, int specificity)>();

        foreach (var candidate in candidates)
        {
            // Skip the zero-arg legacy candidate when args are present
            var paramCount = candidate.Parameters?.Count ?? 0;
            if (candidate.Parameters == null)
            {
                // zero-arg legacy: only matches when no args supplied
                if (positionalArgs.Count == 0 && (namedArgs == null || namedArgs.Count == 0))
                {
                    matches.Add((candidate, new List<Value>(), new Dictionary<string, Value>(), 0));
                }
                continue;
            }

            // Build the final positional arg list by folding named args + defaults
            var (finalArgs, bindFailed) = BuildFinalArgs(
                candidate, positionalArgs, namedArgs, evaluator);
            if (bindFailed) continue;

            // Pattern-match the candidate's parameter list against the final args
            var (matched, bindings, specificity) = PatternMatcher.TryMatchAll(
                candidate.Parameters, finalArgs, evaluator, context);
            if (!matched) continue;

            matches.Add((candidate, finalArgs, bindings, specificity));
        }

        if (matches.Count == 0)
        {
            errorReporter.ReportError(
                $"no overload of section '{sectionName}' matches the supplied arguments",
                location);
            return null;
        }
        if (matches.Count == 1)
            return (matches[0].section, matches[0].finalArgs, matches[0].bindings);

        // Rank by specificity descending
        matches.Sort((a, b) => b.specificity.CompareTo(a.specificity));
        if (matches[0].specificity == matches[1].specificity)
        {
            var loc1 = matches[0].section.SourceLocation?.ToString() ?? "<unknown>";
            var loc2 = matches[1].section.SourceLocation?.ToString() ?? "<unknown>";
            errorReporter.ReportError(
                $"Ambiguous section overload — section '{sectionName}' has two equally-specific overloads " +
                $"matching the supplied arguments (at {loc1} and {loc2})",
                location);
            return null;
        }

        var pick = matches[0];
        return (pick.section, pick.finalArgs, pick.bindings);
    }

    /// <summary>
    /// Builds the final positional arg list for a candidate, folding named
    /// args into slot positions and applying default-value expressions for
    /// un-supplied slots (D-36-15). Returns <c>bindFailed=true</c> (a SILENT
    /// per-candidate disqualification — mirrors OverloadResolver, never a hard
    /// error, so sibling overloads stay eligible) when:
    /// - more positional args than this candidate has parameters,
    /// - a named arg targets an unknown slot (no matching BindingPattern name),
    /// - a named arg collides with a positional slot,
    /// - no default exists for an un-supplied slot.
    /// The aggregate "no overload matches" diagnostic in <see cref="Resolve"/>
    /// fires only when EVERY candidate is disqualified.
    /// </summary>
    private static (IReadOnlyList<Value> finalArgs, bool bindFailed) BuildFinalArgs(
        SectionData candidate,
        IReadOnlyList<Value> positionalArgs,
        IReadOnlyDictionary<string, Value>? namedArgs,
        ExpressionEvaluator evaluator)
    {
        var paramCount = candidate.Parameters!.Count;
        var finalArgs = new Value[paramCount];
        var bound = new bool[paramCount];

        // 1. Place positional args left-to-right.
        //    Too many positional args for THIS candidate is NOT a hard error:
        //    a sibling overload with more parameters may accept them. Disqualify
        //    this candidate silently so OverloadResolver-style dispatch can pick
        //    the arity-correct overload (the "no overload matches" diagnostic at
        //    Resolve() fires only when EVERY candidate is disqualified).
        if (positionalArgs.Count > paramCount)
        {
            return (Array.Empty<Value>(), true);
        }
        for (int i = 0; i < positionalArgs.Count; i++)
        {
            finalArgs[i] = positionalArgs[i];
            bound[i] = true;
        }

        // 2. Place named args by looking up the parameter name. Named args
        //    only resolve against BindingPattern slots — patterns like a
        //    ConstructorPattern (Cmaj7) or tuple destructure don't carry
        //    a slot name to bind by.
        if (namedArgs != null)
        {
            foreach (var (name, value) in namedArgs)
            {
                int slot = -1;
                for (int i = 0; i < paramCount; i++)
                {
                    var p = candidate.Parameters[i];
                    string? slotName = p switch
                    {
                        BindingPattern bp => bp.Name,
                        GuardPattern { Inner: BindingPattern gpb } => gpb.Name,
                        _ => null,
                    };
                    if (slotName == name) { slot = i; break; }
                }
                if (slot < 0)
                {
                    // Unknown slot for THIS candidate — a sibling overload may
                    // carry the named slot. Disqualify silently (aggregate
                    // "no overload matches" fires if every candidate fails).
                    return (Array.Empty<Value>(), true);
                }
                if (bound[slot])
                {
                    // Named arg collides with a positional slot for THIS
                    // candidate — disqualify silently; a sibling overload with a
                    // different parameter shape may still accept the call.
                    return (Array.Empty<Value>(), true);
                }
                finalArgs[slot] = value;
                bound[slot] = true;
            }
        }

        // 3. Fill un-supplied slots with default values.
        for (int i = 0; i < paramCount; i++)
        {
            if (bound[i]) continue;
            Expression? defaultExpr = candidate.DefaultValues != null && i < candidate.DefaultValues.Count
                ? candidate.DefaultValues[i]
                : null;
            if (defaultExpr == null)
            {
                // Slot has no default and was not supplied — this candidate
                // can't be satisfied. NOT a hard error: just disqualify the
                // candidate by returning bindFailed (sibling overloads may
                // accept the arg shape).
                return (Array.Empty<Value>(), true);
            }
            finalArgs[i] = evaluator.Evaluate(defaultExpr);
            bound[i] = true;
        }

        return (finalArgs, false);
    }
}
