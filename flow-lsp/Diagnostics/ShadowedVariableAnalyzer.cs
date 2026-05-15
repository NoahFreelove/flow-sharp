using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): shadowed-variable analyzer.
/// Pure read-only AST + token traversal; never throws, never publishes
/// (returns <see cref="IReadOnlyList{T}"/> of <see cref="Diagnostic"/>).
/// Wave 1 wires this into <see cref="CombinedDiagnosticsPublisher"/>.
///
/// Algorithm (per Phase 31 PATTERNS.md lines 155-167):
/// 1. Maintain a <c>Stack&lt;Dictionary&lt;string, SourceLocation&gt;&gt;</c> scope frame.
/// 2. Push a new frame on entry to <c>MusicalContextStatement.Body</c>,
///    <c>SectionDeclaration.Body</c>, or <c>ProcDeclaration.Body</c>; pop on exit.
/// 3. For each <c>VariableDeclaration</c>: walk the stack from outer → current.
///    If <c>Name</c> matches any OUTER frame's entry, emit Warning with
///    "shadows declaration at line N, column M". Then add Name → location to the
///    CURRENT (innermost) frame.
/// 4. Same-scope re-declaration is NOT shadowing — only NESTED-scope counts
///    (the analyzer's purpose is "you may have meant the outer variable").
///
/// Source string convention (Phase 31 D-05 + Phase 24 D-18): every emitted
/// Diagnostic carries <c>Source="flow.shadowedVariable"</c>.
///
/// Charitable fail-open (Phase 24 D-22 precedent): malformed AST returns
/// <c>Array.Empty&lt;Diagnostic&gt;()</c> — never throws past the public boundary.
/// </summary>
public static class ShadowedVariableAnalyzer
{
    /// <summary>
    /// Walk the AST with a scope stack and return Warning-severity Diagnostic
    /// instances for nested-scope <c>VariableDeclaration</c> nodes whose name
    /// matches an outer-scope declaration.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source)
    {
        try
        {
            if (ast?.Statements is null) return Array.Empty<Diagnostic>();
            var diagnostics = new List<Diagnostic>();
            var scopes = new Stack<Dictionary<string, SourceLocation>>();
            // Push the top-level (file) scope.
            scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
            WalkStatements(ast.Statements, scopes, diagnostics);
            return diagnostics;
        }
        catch
        {
            return Array.Empty<Diagnostic>();
        }
    }

    private static void WalkStatements(
        IReadOnlyList<Statement> stmts,
        Stack<Dictionary<string, SourceLocation>> scopes,
        List<Diagnostic> diagnostics)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case VariableDeclaration vd:
                    HandleVariableDeclaration(vd, scopes, diagnostics);
                    break;
                case MusicalContextStatement m:
                    scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                    WalkStatements(m.Body, scopes, diagnostics);
                    scopes.Pop();
                    break;
                case SectionDeclaration sd:
                    scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                    WalkStatements(sd.Body, scopes, diagnostics);
                    scopes.Pop();
                    break;
                case ProcDeclaration pd:
                    scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                    // Proc parameters count as declarations in the proc's scope.
                    foreach (var param in pd.Parameters)
                    {
                        // Parameter shadow-checks: if the param name matches an
                        // outer-scope variable, flag it as shadowing.
                        // (Parameters don't have their own SourceLocation in the
                        // shipping AST, so we attribute to the proc's location.)
                        CheckShadow(param.Name, pd.Location, scopes, diagnostics);
                        scopes.Peek()[param.Name] = pd.Location;
                    }
                    WalkStatements(pd.Body, scopes, diagnostics);
                    scopes.Pop();
                    break;
                case ForStatement fs:
                    scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                    // The loop-variable counts as a declaration in the for-body scope.
                    CheckShadow(fs.VariableName, fs.Location, scopes, diagnostics);
                    scopes.Peek()[fs.VariableName] = fs.Location;
                    WalkStatements(fs.Body, scopes, diagnostics);
                    scopes.Pop();
                    break;
                case WhileStatement ws:
                    scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                    WalkStatements(ws.Body, scopes, diagnostics);
                    scopes.Pop();
                    break;
                case ExpressionStatement es:
                    // Descend into lambda bodies inside expressions.
                    WalkExpression(es.Expression, scopes, diagnostics);
                    break;
                // AssignmentStatement / ReturnStatement / ImportStatement /
                // Break / Continue / TupleDestructureStatement: no new
                // identifier-introduction relevant to shadow detection.
            }
        }
    }

    private static void HandleVariableDeclaration(
        VariableDeclaration vd,
        Stack<Dictionary<string, SourceLocation>> scopes,
        List<Diagnostic> diagnostics)
    {
        CheckShadow(vd.Name, vd.Location, scopes, diagnostics);
        // Record the declaration in the CURRENT (innermost) frame. Last-wins
        // on same-scope re-declaration — that's intentional; same-scope
        // re-decl is not a shadow (per Phase 31 plan task spec).
        scopes.Peek()[vd.Name] = vd.Location;

        // Descend into the initializer in case it contains lambda bodies.
        WalkExpression(vd.Value, scopes, diagnostics);
    }

    /// <summary>
    /// Walk every OUTER frame (everything except the current top-of-stack);
    /// if <paramref name="name"/> matches an entry in any outer frame, emit
    /// a Warning diagnostic against <paramref name="innerLoc"/>.
    /// </summary>
    private static void CheckShadow(
        string name,
        SourceLocation innerLoc,
        Stack<Dictionary<string, SourceLocation>> scopes,
        List<Diagnostic> diagnostics)
    {
        if (scopes.Count <= 1) return;  // only one scope = no outer scope = no shadowing possible
        // Stack.Peek is the innermost; iterate from second-to-top outward.
        // Skip the top frame (current scope) — same-scope re-decl is NOT shadowing.
        bool firstFrame = true;
        foreach (var frame in scopes)
        {
            if (firstFrame) { firstFrame = false; continue; }
            if (frame.TryGetValue(name, out var outerLoc))
            {
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Source = "flow.shadowedVariable",
                    Message = $"Variable '{name}' shadows declaration at line {outerLoc.Line}, column {outerLoc.Column}",
                    Range = LspMappings.ToRange(innerLoc)
                });
                return;  // one diagnostic per shadow — first outer match wins
            }
        }
    }

    /// <summary>
    /// Descend into an expression looking for lambda bodies (the only place
    /// where shadowing concerns arise inside expression position). Other
    /// expression kinds don't introduce new bindings.
    /// </summary>
    private static void WalkExpression(
        Expression? expr,
        Stack<Dictionary<string, SourceLocation>> scopes,
        List<Diagnostic> diagnostics)
    {
        if (expr is null) return;
        switch (expr)
        {
            case LambdaExpression la:
                scopes.Push(new Dictionary<string, SourceLocation>(StringComparer.Ordinal));
                foreach (var p in la.Parameters)
                {
                    CheckShadow(p.Name, la.Location, scopes, diagnostics);
                    scopes.Peek()[p.Name] = la.Location;
                }
                WalkStatements(la.Body, scopes, diagnostics);
                scopes.Pop();
                break;
            case FunctionCallExpression fc:
                foreach (var a in fc.Arguments) WalkExpression(a, scopes, diagnostics);
                break;
            case FlowExpression fe:
                WalkExpression(fe.Left, scopes, diagnostics);
                WalkExpression(fe.Right, scopes, diagnostics);
                break;
            case ArrayLiteralExpression al:
                foreach (var e in al.Elements) WalkExpression(e, scopes, diagnostics);
                break;
            case ArrayIndexExpression ai:
                WalkExpression(ai.Array, scopes, diagnostics);
                WalkExpression(ai.Index, scopes, diagnostics);
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements) WalkExpression(e, scopes, diagnostics);
                break;
            case TupleUnpackFlowExpression tu:
                WalkExpression(tu.Left, scopes, diagnostics);
                WalkExpression(tu.Right, scopes, diagnostics);
                break;
            case MemberAccessExpression ma:
                WalkExpression(ma.Object, scopes, diagnostics);
                break;
            // Other expressions: no nested lambda bodies relevant to shadow detection.
        }
    }
}
