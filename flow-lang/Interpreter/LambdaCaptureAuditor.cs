using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;

namespace FlowLang.Interpreter;

/// <summary>
/// Phase 38 Plan 38-03 LIVE-03 — best-effort AST walker that collects every
/// VARIABLE name referenced from inside a body (or any lambda nested within
/// it) that is NOT shadowed by a local declaration. The live-block swap path
/// (<see cref="FlowInterpreter.LiveReloadManager.StagePendingBuffers"/>) uses
/// the returned set to detect closures whose previously-captured file-scope
/// bindings have been removed since the last render — RESEARCH §C lines
/// 698-765.
///
/// <para>
/// <b>Charitable defaults (D-v1.5-05):</b> the walker handles every AST node
/// type produced by the parser today (Phase 35/36/38 inclusive); unknown
/// future node types fall through to the switch default and silently
/// contribute zero references. The stale-closure detection is a best-effort
/// composer-aid guard, NOT a soundness gate — Threat T-38-CLO accepts the
/// false-negative risk per the plan's threat register.
/// </para>
///
/// <para>
/// <b>Scope tracking:</b> the walker maintains a <c>Stack&lt;HashSet&lt;string&gt;&gt;</c>
/// of locally-declared names. A new scope is pushed for each
/// <see cref="LambdaExpression"/> (pre-populated with its parameter names)
/// and for the top-level statement list. <see cref="VariableDeclaration"/> /
/// <see cref="ProcDeclaration"/> / <see cref="SectionDeclaration"/> /
/// <see cref="ForStatement"/> / <see cref="TupleDestructureStatement"/>
/// contribute names into the current scope at declaration order. A
/// <see cref="VariableExpression"/> reference is reported as "file-scope"
/// (added to the result) ONLY when its name appears in NONE of the active
/// scopes — i.e. it must be resolved from outside this body.
/// </para>
///
/// <para>
/// <b>Why pattern-matching switch dispatch (per CLAUDE.md C# Conventions):</b>
/// the AST nodes are immutable records; the visitor pattern is rejected in
/// favor of <c>switch</c> expressions that pattern-match on node type. Same
/// idiom as <see cref="ExpressionEvaluator"/> + <see cref="Interpreter"/>.
/// </para>
/// </summary>
public static class LambdaCaptureAuditor
{
    /// <summary>
    /// Walks <paramref name="body"/> and returns the set of variable names
    /// referenced from inside the body (or any nested lambda) that are NOT
    /// locally declared. The set is intended for membership-testing against
    /// the GlobalFrame's variable + function tables at the live-swap site —
    /// any name NOT present in either is a stale-closure indicator.
    /// </summary>
    public static HashSet<string> CollectFileScopeReferences(IReadOnlyList<Statement> body)
    {
        var result = new HashSet<string>();
        var scopes = new Stack<HashSet<string>>();

        // Top-level body scope — VariableDeclaration / ProcDeclaration /
        // SectionDeclaration at this level shadow file-scope names per
        // typical block-scope rules.
        scopes.Push(new HashSet<string>());

        if (body != null)
        {
            for (int i = 0; i < body.Count; i++)
            {
                WalkStatement(body[i], result, scopes);
            }
        }

        scopes.Pop();
        return result;
    }

    private static bool IsDeclaredInScopes(Stack<HashSet<string>> scopes, string name)
    {
        foreach (var s in scopes)
        {
            if (s.Contains(name)) return true;
        }
        return false;
    }

    private static void AddToCurrentScope(Stack<HashSet<string>> scopes, string name)
    {
        if (scopes.Count > 0)
        {
            scopes.Peek().Add(name);
        }
    }

    private static void WalkStatement(Statement statement, HashSet<string> result, Stack<HashSet<string>> scopes)
    {
        if (statement == null) return;

        switch (statement)
        {
            case VariableDeclaration vd:
                // RHS is evaluated BEFORE the binding becomes visible — so
                // walk the value first, then add the name to the current
                // scope. (Self-reference in initializer is a parse-time
                // error anyway; this ordering doesn't matter for live-swap
                // detection.)
                WalkExpression(vd.Value, result, scopes);
                AddToCurrentScope(scopes, vd.Name);
                break;

            case AssignmentStatement asg:
                // The LHS name MUST already exist somewhere — either in a
                // local scope or at file scope. If not in any local scope,
                // it's a file-scope reference (the assignment target).
                if (!IsDeclaredInScopes(scopes, asg.Name))
                {
                    result.Add(asg.Name);
                }
                WalkExpression(asg.Value, result, scopes);
                break;

            case ProcDeclaration pd:
                // The proc name becomes visible in the current scope; its
                // parameters introduce a new nested scope for the body.
                AddToCurrentScope(scopes, pd.Name);
                scopes.Push(new HashSet<string>());
                if (pd.Parameters != null)
                {
                    for (int i = 0; i < pd.Parameters.Count; i++)
                    {
                        AddToCurrentScope(scopes, pd.Parameters[i].Name);
                    }
                }
                if (pd.Body != null)
                {
                    for (int i = 0; i < pd.Body.Count; i++)
                    {
                        WalkStatement(pd.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case ReturnStatement rs:
                WalkExpression(rs.Value, result, scopes);
                break;

            case ExpressionStatement es:
                WalkExpression(es.Expression, result, scopes);
                break;

            case ImportStatement:
                // Imports don't introduce stale-closure risk by themselves —
                // any imported bindings would surface as file-scope variable
                // references through the normal name-resolution path.
                break;

            case MusicalContextStatement mcs:
                WalkExpression(mcs.Value, result, scopes);
                if (mcs.Value2 != null) WalkExpression(mcs.Value2, result, scopes);
                scopes.Push(new HashSet<string>());
                if (mcs.Body != null)
                {
                    for (int i = 0; i < mcs.Body.Count; i++)
                    {
                        WalkStatement(mcs.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case TuningContextStatement tcs:
                WalkExpression(tcs.TuningExpr, result, scopes);
                scopes.Push(new HashSet<string>());
                if (tcs.Body != null)
                {
                    for (int i = 0; i < tcs.Body.Count; i++)
                    {
                        WalkStatement(tcs.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case SectionDeclaration sd:
                // Section name is bound at file scope; its body has its own
                // scope. Section parameters (Phase 36 SECT-01) introduce
                // bindings into the body scope; pattern parameters are
                // walked for any binding names.
                AddToCurrentScope(scopes, sd.Name);
                scopes.Push(new HashSet<string>());
                if (sd.Parameters != null)
                {
                    for (int i = 0; i < sd.Parameters.Count; i++)
                    {
                        WalkPattern(sd.Parameters[i], result, scopes, declareBindings: true);
                    }
                }
                if (sd.DefaultValues != null)
                {
                    for (int i = 0; i < sd.DefaultValues.Count; i++)
                    {
                        if (sd.DefaultValues[i] != null)
                            WalkExpression(sd.DefaultValues[i]!, result, scopes);
                    }
                }
                if (sd.Body != null)
                {
                    for (int i = 0; i < sd.Body.Count; i++)
                    {
                        WalkStatement(sd.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case TupleDestructureStatement tds:
                WalkExpression(tds.Value, result, scopes);
                if (tds.Patterns != null)
                {
                    for (int i = 0; i < tds.Patterns.Count; i++)
                    {
                        AddToCurrentScope(scopes, tds.Patterns[i].Name);
                    }
                }
                break;

            case LiveBlockStatement lbs:
                // Live blocks nested inside the current body — walk the
                // quantize expression in the outer scope, push a new scope
                // for the body. Per D-38-04 a live block introduces no new
                // bindings beyond its own body.
                WalkExpression(lbs.QuantizeValue, result, scopes);
                scopes.Push(new HashSet<string>());
                if (lbs.Body != null)
                {
                    for (int i = 0; i < lbs.Body.Count; i++)
                    {
                        WalkStatement(lbs.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case ForStatement fs:
                // The collection expression is evaluated in the outer scope.
                // The loop variable name is bound for the body scope.
                WalkExpression(fs.Collection, result, scopes);
                scopes.Push(new HashSet<string>());
                AddToCurrentScope(scopes, fs.VariableName);
                if (fs.Body != null)
                {
                    for (int i = 0; i < fs.Body.Count; i++)
                    {
                        WalkStatement(fs.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case WhileStatement ws:
                WalkExpression(ws.Condition, result, scopes);
                scopes.Push(new HashSet<string>());
                if (ws.Body != null)
                {
                    for (int i = 0; i < ws.Body.Count; i++)
                    {
                        WalkStatement(ws.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case BreakStatement:
            case ContinueStatement:
                break;

            default:
                // Charitable per D-v1.5-05 — unknown statement types
                // contribute zero references. Future AST nodes won't crash
                // the auditor; the stale-closure detection is a best-effort
                // guard, not a soundness gate (Threat T-38-CLO accepted).
                break;
        }
    }

    private static void WalkExpression(Expression expression, HashSet<string> result, Stack<HashSet<string>> scopes)
    {
        if (expression == null) return;

        switch (expression)
        {
            case LiteralExpression:
            case SymbolLiteralExpression:
            case ChordLiteralExpression:
                break;

            case VariableExpression ve:
                if (!IsDeclaredInScopes(scopes, ve.Name))
                {
                    result.Add(ve.Name);
                }
                break;

            case FunctionCallExpression fc:
                // Function name itself is a callable reference — escape-check it
                // just like a VariableExpression so misspelled / removed builtins
                // surface as stale-closure candidates.
                if (!IsDeclaredInScopes(scopes, fc.Name))
                {
                    result.Add(fc.Name);
                }
                if (fc.Arguments != null)
                {
                    for (int i = 0; i < fc.Arguments.Count; i++)
                    {
                        WalkExpression(fc.Arguments[i], result, scopes);
                    }
                }
                if (fc.NamedArgs != null)
                {
                    foreach (var kv in fc.NamedArgs)
                    {
                        WalkExpression(kv.Value, result, scopes);
                    }
                }
                break;

            case FlowExpression fe:
                WalkExpression(fe.Left, result, scopes);
                WalkExpression(fe.Right, result, scopes);
                break;

            case ArrayLiteralExpression al:
                if (al.Elements != null)
                {
                    for (int i = 0; i < al.Elements.Count; i++)
                    {
                        WalkExpression(al.Elements[i], result, scopes);
                    }
                }
                break;

            case ArrayIndexExpression ai:
                WalkExpression(ai.Array, result, scopes);
                WalkExpression(ai.Index, result, scopes);
                break;

            case LazyExpression lz:
                WalkExpression(lz.InnerExpression, result, scopes);
                break;

            case LambdaExpression lam:
                // Push a fresh scope pre-populated with parameter names.
                scopes.Push(new HashSet<string>());
                if (lam.Parameters != null)
                {
                    for (int i = 0; i < lam.Parameters.Count; i++)
                    {
                        AddToCurrentScope(scopes, lam.Parameters[i].Name);
                    }
                }
                if (lam.Body != null)
                {
                    for (int i = 0; i < lam.Body.Count; i++)
                    {
                        WalkStatement(lam.Body[i], result, scopes);
                    }
                }
                scopes.Pop();
                break;

            case MemberAccessExpression ma:
                WalkExpression(ma.Object, result, scopes);
                break;

            case TupleLiteralExpression tl:
                if (tl.Elements != null)
                {
                    for (int i = 0; i < tl.Elements.Count; i++)
                    {
                        WalkExpression(tl.Elements[i], result, scopes);
                    }
                }
                break;

            case TupleUnpackFlowExpression tuf:
                WalkExpression(tuf.Left, result, scopes);
                WalkExpression(tuf.Right, result, scopes);
                break;

            case NoteStreamExpression ns:
                // Note streams compile at evaluation time using the active
                // musical context — they don't carry sub-expressions that
                // bind variable references at parse time. No-op.
                break;

            case SongExpression se:
                // SongExpression's bare-section references are name lookups
                // against the SectionRegistry, not variable references —
                // skip. Parameterized SectionCallElement carries
                // PositionalArgs / NamedArgs expressions that DO reference
                // variables.
                if (se.Elements != null)
                {
                    for (int i = 0; i < se.Elements.Count; i++)
                    {
                        var elem = se.Elements[i];
                        if (elem is SectionCallElement sce)
                        {
                            // Section name itself — file-scope binding check.
                            if (!IsDeclaredInScopes(scopes, sce.Name))
                            {
                                result.Add(sce.Name);
                            }
                            if (sce.PositionalArgs != null)
                            {
                                for (int j = 0; j < sce.PositionalArgs.Count; j++)
                                {
                                    WalkExpression(sce.PositionalArgs[j], result, scopes);
                                }
                            }
                            if (sce.NamedArgs != null)
                            {
                                foreach (var kv in sce.NamedArgs)
                                {
                                    WalkExpression(kv.Value, result, scopes);
                                }
                            }
                        }
                        else if (elem is BareSectionElement bse)
                        {
                            if (!IsDeclaredInScopes(scopes, bse.Name))
                            {
                                result.Add(bse.Name);
                            }
                        }
                    }
                }
                break;

            case InterpolatedStringExpression ise:
                if (ise.Parts != null)
                {
                    for (int i = 0; i < ise.Parts.Count; i++)
                    {
                        WalkExpression(ise.Parts[i], result, scopes);
                    }
                }
                break;

            case MatchExpression me:
                WalkExpression(me.Scrutinee, result, scopes);
                if (me.Arms != null)
                {
                    for (int i = 0; i < me.Arms.Count; i++)
                    {
                        var arm = me.Arms[i];
                        // The pattern may introduce bindings (BindingPattern,
                        // ConstructorPattern.SubPatterns) for the arm body.
                        scopes.Push(new HashSet<string>());
                        WalkPattern(arm.Pattern, result, scopes, declareBindings: true);
                        WalkExpression(arm.Body, result, scopes);
                        scopes.Pop();
                    }
                }
                break;

            case ProgressionExpression:
                // Chord progressions resolve at evaluation time against the
                // musical-context key — no variable references.
                break;

            default:
                // Charitable per D-v1.5-05 — unknown expression types
                // contribute zero references.
                break;
        }
    }

    /// <summary>
    /// Walks a pattern node. When <paramref name="declareBindings"/> is true,
    /// <see cref="BindingPattern"/> names are added to the current scope (for
    /// match arm bodies and section parameter lists). Guard expressions are
    /// walked in the outer-or-arm scope.
    /// </summary>
    private static void WalkPattern(Pattern pattern, HashSet<string> result, Stack<HashSet<string>> scopes, bool declareBindings)
    {
        if (pattern == null) return;

        switch (pattern)
        {
            case BindingPattern bp:
                if (declareBindings) AddToCurrentScope(scopes, bp.Name);
                break;

            case WildcardPattern:
            case LiteralPattern:
                break;

            case ConstructorPattern cp:
                // Music-aware constructor patterns (ChordLiteral / RomanNumeral
                // / ArticulationSymbol) are name-resolved at PatternMatcher
                // time against the active musical context — not stale-closure
                // candidates. Walk sub-patterns for their bindings only.
                if (cp.SubPatterns != null)
                {
                    for (int i = 0; i < cp.SubPatterns.Count; i++)
                    {
                        WalkPattern(cp.SubPatterns[i], result, scopes, declareBindings);
                    }
                }
                break;

            case GuardPattern gp:
                WalkPattern(gp.Inner, result, scopes, declareBindings);
                WalkExpression(gp.GuardExpression, result, scopes);
                break;

            default:
                // Charitable default — unknown pattern types contribute zero.
                break;
        }
    }
}
