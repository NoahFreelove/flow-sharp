using FlowLang.Ast;
using FlowLang.Ast.Patterns;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

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
        // sweep-0614: a Note-literal pattern (`| C4 => ...`) carries the raw
        // note text "C4" as a string payload. Value.From would wrap it as a
        // String-typed Value, so LooseEquals(Note, String) takes its cross-type
        // fallthrough and returns false unconditionally — a note pattern could
        // never match a Note scrutinee. Build a Note-typed comparison value
        // when the scrutinee is a Note so both sides hit the same-type
        // StrictEquals branch (verbatim note-text compare). Both the scrutinee
        // (Value.Note(text)) and the pattern payload store the raw note text,
        // so a direct text compare fires for the common case.
        if (scrutinee.Type is NoteType && lit.Value is string noteText)
        {
            return Utils.LooseEquals(scrutinee, Value.Note(noteText));
        }

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
        // Phase 35 Plan 35-06 (LANG-02) — music-aware constructor dispatch.
        // Three discriminator flags route to three specialized helpers:
        //   - IsChordLiteral: MatchChordQuality (Root + Quality compare)
        //   - IsRomanNumeral: MatchRomanNumeral (resolved against active key)
        //   - IsArticulationSymbol: MatchArticulation (Note.Articulation compare)
        // Non-music ConstructorPatterns (none in v1.5 surface) fall through
        // to false — the silent-Void behavior from Plan 35-05 stays in place
        // until v1.6 introduces nested constructor patterns.
        _ = bindings; _ = evaluator;

        if (ctor.IsChordLiteral)
            return MatchChordQuality(ctor.Name, scrutinee);
        if (ctor.IsRomanNumeral)
            return MatchRomanNumeral(ctor.Name, scrutinee, context);

        // sweep-0614: a `#symbol` pattern may carry BOTH flags (an articulation
        // keyword like `#staccato`). Dispatch on the scrutinee's runtime type:
        //   - Symbol scrutinee → symbol-name equality (covers `#kick`, `#jazz`,
        //     and an articulation-keyword symbol used as a plain Symbol value);
        //   - MusicalNote scrutinee → articulation extractor (only when the
        //     symbol names an Articulation enum member).
        if (ctor.IsSymbolLiteral && scrutinee.Type is SymbolType)
            return MatchSymbol(ctor.Name, scrutinee);
        if (ctor.IsArticulationSymbol)
            return MatchArticulation(ctor.Name, scrutinee);
        if (ctor.IsSymbolLiteral)
            return MatchSymbol(ctor.Name, scrutinee);

        return false;
    }

    /// <summary>
    /// sweep-0614 — matches a general symbol-literal pattern (e.g. <c>#kick</c>,
    /// <c>#jazz</c>) against a Symbol scrutinee. Requires the scrutinee to
    /// actually be a Symbol value and compares the interned symbol name for
    /// ordinal equality, matching <see cref="Value.Symbol"/> /
    /// <c>SymbolInternTable</c> semantics (SYM-01 pointer-equality is exactly
    /// name-equality for interned symbols). A non-Symbol scrutinee (e.g. a Note
    /// carrying an articulation) misses charitably — this cannot collide with
    /// articulation matching, which is gated on a separate flag.
    /// </summary>
    private static bool MatchSymbol(string symbolName, Value scrutinee)
    {
        return scrutinee.Type is SymbolType
            && scrutinee.Data is string scrutineeName
            && string.Equals(scrutineeName, symbolName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches a chord-literal pattern (e.g.,
    /// <c>Cmaj7</c>, <c>Dm7</c>) against a Chord scrutinee. The canonical
    /// equality per RESEARCH §Example 2 is Root + Quality match — different
    /// roots miss, different qualities miss, only structural equality on
    /// both fields produces a hit. Octave is intentionally ignored so a
    /// composer matching <c>Cmaj7</c> hits any Cmaj7 chord value regardless
    /// of the octave the scrutinee was rendered at.
    /// </summary>
    private static bool MatchChordQuality(string chordText, Value scrutinee)
    {
        if (scrutinee.Data is not ChordData scrutineeChord)
            return false;

        if (!ChordParser.TryParse(chordText, out var expected) || expected == null)
            return false;

        return string.Equals(scrutineeChord.Root, expected.Root, StringComparison.Ordinal)
            && string.Equals(scrutineeChord.Quality, expected.Quality, StringComparison.Ordinal);
    }

    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches a roman-numeral pattern (e.g.,
    /// <c>V7</c>, <c>I</c>, <c>vi</c>) against a Chord scrutinee. The numeral
    /// is resolved against the active key context (read from
    /// <see cref="MusicalContext.Key"/>) via
    /// <see cref="ScaleDatabase.ResolveRomanNumeral"/>, then compared to the
    /// scrutinee by Root + Quality (mirroring MatchChordQuality). When no
    /// key is active or the resolution fails, the match misses charitably
    /// rather than throwing — the composer is expected to scope the match
    /// inside a <c>key X { ... }</c> block, but a missing key is a
    /// composer-error condition, not a runtime crash.
    /// </summary>
    private static bool MatchRomanNumeral(
        string numeral,
        Value scrutinee,
        Runtime.ExecutionContext context)
    {
        if (scrutinee.Data is not ChordData scrutineeChord)
            return false;

        var musical = context.GetMusicalContext();
        if (musical.Key is null)
            return false;

        var resolved = ScaleDatabase.ResolveRomanNumeral(numeral, musical.Key);
        if (resolved is null)
            return false;

        return string.Equals(scrutineeChord.Root, resolved.Root, StringComparison.Ordinal)
            && string.Equals(scrutineeChord.Quality, resolved.Quality, StringComparison.Ordinal);
    }

    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches an articulation-symbol pattern
    /// (e.g., <c>#staccato</c>, <c>#legato</c>, <c>#accent</c>) against a
    /// MusicalNote scrutinee by comparing the symbol body (case-insensitive
    /// per Phase 28's lex-time normalization) to the note's
    /// <see cref="Articulation"/> enum value. Unknown symbol names produce
    /// a charitable miss rather than throwing.
    /// </summary>
    private static bool MatchArticulation(string symbolName, Value scrutinee)
    {
        if (scrutinee.Data is not MusicalNoteData musicalNote)
            return false;

        if (!Enum.TryParse<Articulation>(symbolName, ignoreCase: true, out var expected))
            return false;

        return musicalNote.Articulation == expected;
    }

    /// <summary>
    /// Phase 36 Plan 36-10 (D-36-17 SECT-01) — section-overload dispatch
    /// helper. Tries to match a list of patterns (the section's parameter
    /// signature) against a list of runtime arg Values. Returns
    /// <c>(matched: false, ...)</c> on length mismatch OR any individual
    /// pattern miss; otherwise aggregates per-position bindings + sums
    /// per-pattern specificity scores per RESEARCH §Pattern 7:
    ///
    /// <list type="bullet">
    ///   <item><description>LiteralPattern → +1000</description></item>
    ///   <item><description>ConstructorPattern with music-aware extractor → +800</description></item>
    ///   <item><description>ConstructorPattern (tuple destructure) → +600</description></item>
    ///   <item><description>BindingPattern (typed) → +500</description></item>
    ///   <item><description>BindingPattern (untyped) → +200</description></item>
    ///   <item><description>WildcardPattern → +100</description></item>
    ///   <item><description>GuardPattern → +inner specificity (guard expr
    ///     evaluated as part of the match)</description></item>
    /// </list>
    ///
    /// <para>
    /// Typed BindingPattern (D-36-17): when <c>TypeAnnotation</c> is set, the
    /// arg Value's Type must be compatible (per FlowType.IsCompatibleWith) —
    /// otherwise the match misses. Untyped BindingPattern accepts any arg.
    /// </para>
    /// </summary>
    public static (bool matched, Dictionary<string, Value> bindings, int specificity)
        TryMatchAll(
            IReadOnlyList<Pattern> patterns,
            IReadOnlyList<Value> args,
            ExpressionEvaluator evaluator,
            Runtime.ExecutionContext context)
    {
        var bindings = new Dictionary<string, Value>();

        if (patterns.Count != args.Count)
            return (false, bindings, 0);

        int totalSpecificity = 0;
        for (int i = 0; i < patterns.Count; i++)
        {
            var pat = patterns[i];
            var arg = args[i];

            // Typed-binding type-compatibility gate (D-36-17).
            if (pat is BindingPattern bp && bp.TypeAnnotation != null)
            {
                if (!IsTypeCompatible(arg, bp.TypeAnnotation))
                    return (false, bindings, 0);
            }

            // Tuple destructure: ConstructorPattern with Name="Tuple".
            // Tuples are stored as IReadOnlyList<Value> tagged with TupleType.
            if (pat is ConstructorPattern cp && cp.Name == "Tuple"
                && !cp.IsChordLiteral && !cp.IsRomanNumeral && !cp.IsArticulationSymbol
                && !cp.IsSymbolLiteral)
            {
                if (arg.Type is not TypeSystem.SpecialTypes.TupleType
                    || arg.Data is not IReadOnlyList<Value> tupleElements)
                    return (false, bindings, 0);
                if (tupleElements.Count != cp.SubPatterns.Count)
                    return (false, bindings, 0);

                // Recursive per-slot match.
                var inner = TryMatchAll(cp.SubPatterns, tupleElements, evaluator, context);
                if (!inner.matched)
                    return (false, bindings, 0);
                foreach (var (n, v) in inner.bindings)
                    bindings[n] = v;
                totalSpecificity += 600;
                continue;
            }

            // Music-aware ConstructorPattern (chord literal / roman numeral /
            // articulation / general symbol literal, sweep-0614):
            if (pat is ConstructorPattern cpMusic && (cpMusic.IsChordLiteral || cpMusic.IsRomanNumeral || cpMusic.IsArticulationSymbol || cpMusic.IsSymbolLiteral))
            {
                if (!PatternMatches(cpMusic, arg, bindings, evaluator, context))
                    return (false, bindings, 0);
                totalSpecificity += cpMusic.IsSymbolLiteral ? 1000 : 800;
                continue;
            }

            // Guard pattern — delegate to PatternMatches; on success add inner specificity.
            if (pat is GuardPattern gp)
            {
                if (!PatternMatches(gp, arg, bindings, evaluator, context))
                    return (false, bindings, 0);
                totalSpecificity += SpecificityOf(gp.Inner);
                continue;
            }

            // Default: delegate to PatternMatches.
            if (!PatternMatches(pat, arg, bindings, evaluator, context))
                return (false, bindings, 0);
            totalSpecificity += SpecificityOf(pat);
        }

        return (true, bindings, totalSpecificity);
    }

    private static int SpecificityOf(Pattern pattern) => pattern switch
    {
        LiteralPattern => 1000,
        // sweep-0614: a general symbol literal is an exact-value match like any
        // other literal, so it scores at the literal level (above articulation /
        // roman-numeral extractors, which are looser by-property matches).
        ConstructorPattern cp when cp.IsSymbolLiteral => 1000,
        ConstructorPattern cp when cp.IsChordLiteral || cp.IsRomanNumeral || cp.IsArticulationSymbol => 800,
        ConstructorPattern cp when cp.Name == "Tuple" => 600,
        BindingPattern bp when bp.TypeAnnotation != null => 500,
        BindingPattern => 200,
        WildcardPattern => 100,
        GuardPattern gp => SpecificityOf(gp.Inner),
        _ => 100,
    };

    private static bool IsTypeCompatible(Value arg, TypeSystem.FlowType expected)
    {
        if (arg.Type == null) return true;
        return expected.IsCompatibleWith(arg.Type);
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
