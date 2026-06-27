using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): unreachable-section analyzer.
/// Pure read-only AST + token traversal; never throws, never publishes
/// (returns <see cref="IReadOnlyList{T}"/> of <see cref="Diagnostic"/>).
/// Wave 1 wires this into <see cref="CombinedDiagnosticsPublisher"/>.
///
/// Algorithm (two-pass per Phase 31 PATTERNS.md lines 146-151):
/// 1. Pass 1: walk every <c>SectionDeclaration</c> (descending into nested
///    <c>MusicalContextStatement</c> / <c>SectionDeclaration</c> / <c>ProcDeclaration</c>
///    bodies) and collect <c>(Name → Location)</c>.
/// 2. Pass 2: walk every <c>SongExpression</c> in the AST and collect referenced
///    section names (handles <c>name*N</c> repeat syntax via
///    <see cref="SongSectionReference.Name"/>).
/// 3. Emit one Information-severity Diagnostic per defined-but-unreferenced section.
///
/// Source string convention (Phase 31 D-05 + Phase 24 D-18): every emitted
/// Diagnostic carries <c>Source="flow.unreachableSection"</c>.
///
/// Charitable fail-open (Phase 24 D-22 precedent): malformed AST returns
/// <c>Array.Empty&lt;Diagnostic&gt;()</c> — never throws past the public boundary.
/// </summary>
public static class UnreachableSectionAnalyzer
{
    /// <summary>
    /// Walk the AST and return Information-severity Diagnostic instances for
    /// every <c>section name { ... }</c> definition whose name is not referenced
    /// in any <c>Song</c> arrangement.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source)
    {
        try
        {
            if (ast?.Statements is null) return Array.Empty<Diagnostic>();

            // Pass 1: collect defined sections.
            var defined = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);
            CollectDefinedSections(ast.Statements, defined);
            if (defined.Count == 0) return Array.Empty<Diagnostic>();

            // Pass 2: collect referenced section names.
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            CollectReferencedSections(ast.Statements, referenced);

            // Emit one Information per defined-but-not-referenced section.
            var diagnostics = new List<Diagnostic>();
            foreach (var (name, loc) in defined)
            {
                if (referenced.Contains(name)) continue;
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Information,
                    Source = "flow.unreachableSection",
                    Message = $"Section '{name}' is defined but never referenced in any Song",
                    Range = LspMappings.ToRange(loc)
                });
            }
            return diagnostics;
        }
        catch
        {
            return Array.Empty<Diagnostic>();
        }
    }

    private static void CollectDefinedSections(
        IReadOnlyList<Statement> stmts,
        Dictionary<string, SourceLocation> acc)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case SectionDeclaration sd:
                    // Last-wins semantics on duplicate-name redefinition is fine
                    // here — the second declaration is the one composers see in
                    // the editor most recently.
                    acc[sd.Name] = sd.Location;
                    CollectDefinedSections(sd.Body, acc);
                    break;
                case MusicalContextStatement m:
                    CollectDefinedSections(m.Body, acc);
                    break;
                case ProcDeclaration pd:
                    CollectDefinedSections(pd.Body, acc);
                    break;
                case ForStatement fs:
                    CollectDefinedSections(fs.Body, acc);
                    break;
                case WhileStatement ws:
                    CollectDefinedSections(ws.Body, acc);
                    break;
            }
        }
    }

    private static void CollectReferencedSections(
        IReadOnlyList<Statement> stmts,
        HashSet<string> acc)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case ExpressionStatement es:
                    CollectFromExpr(es.Expression, acc);
                    break;
                case VariableDeclaration vd:
                    CollectFromExpr(vd.Value, acc);
                    break;
                case AssignmentStatement asn:
                    CollectFromExpr(asn.Value, acc);
                    break;
                case ReturnStatement rs:
                    if (rs.Value is not null) CollectFromExpr(rs.Value, acc);
                    break;
                case MusicalContextStatement m:
                    if (m.Value is not null) CollectFromExpr(m.Value, acc);
                    if (m.Value2 is not null) CollectFromExpr(m.Value2, acc);
                    CollectReferencedSections(m.Body, acc);
                    break;
                case SectionDeclaration sd:
                    CollectReferencedSections(sd.Body, acc);
                    break;
                case ProcDeclaration pd:
                    CollectReferencedSections(pd.Body, acc);
                    break;
                case ForStatement fs:
                    CollectFromExpr(fs.Collection, acc);
                    CollectReferencedSections(fs.Body, acc);
                    break;
                case WhileStatement ws:
                    CollectFromExpr(ws.Condition, acc);
                    CollectReferencedSections(ws.Body, acc);
                    break;
            }
        }
    }

    private static void CollectFromExpr(Expression? expr, HashSet<string> acc)
    {
        if (expr is null) return;
        switch (expr)
        {
            case SongExpression song:
                foreach (var section in song.Sections)
                    acc.Add(section.Name);
                break;
            case FunctionCallExpression fc:
                foreach (var a in fc.Arguments) CollectFromExpr(a, acc);
                break;
            case FlowExpression fe:
                CollectFromExpr(fe.Left, acc);
                CollectFromExpr(fe.Right, acc);
                break;
            case ArrayLiteralExpression al:
                foreach (var e in al.Elements) CollectFromExpr(e, acc);
                break;
            case ArrayIndexExpression ai:
                CollectFromExpr(ai.Array, acc);
                CollectFromExpr(ai.Index, acc);
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements) CollectFromExpr(e, acc);
                break;
            case TupleUnpackFlowExpression tu:
                CollectFromExpr(tu.Left, acc);
                CollectFromExpr(tu.Right, acc);
                break;
            case LambdaExpression la:
                CollectReferencedSections(la.Body, acc);
                break;
            case MemberAccessExpression ma:
                CollectFromExpr(ma.Object, acc);
                break;
            // VariableExpression / LiteralExpression / NoteStreamExpression /
            // ChordLiteralExpression / SymbolLiteralExpression: no nested
            // SongExpression possible.
        }
    }
}
