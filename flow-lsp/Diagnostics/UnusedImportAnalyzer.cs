using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): unused-import analyzer.
/// Pure read-only AST + token traversal; never throws, never publishes
/// (returns <see cref="IReadOnlyList{T}"/> of <see cref="Diagnostic"/>).
/// Wave 1 wires this into <see cref="CombinedDiagnosticsPublisher"/>.
///
/// Algorithm:
/// 1. Collect every <c>ImportStatement</c> from <c>ast.Statements</c>.
/// 2. Collect every identifier referenced anywhere in the AST
///    (FunctionCallExpression.Name, VariableExpression.Name,
///     MemberAccessExpression.MemberName) into a HashSet&lt;string&gt;.
/// 3. For each import, derive the module name (strip leading <c>@</c>).
/// 4. <c>@std</c> special case: expand to <see cref="StdlibSymbolIndex.ModuleNames"/>
///    so a reference to ANY transitively-imported proc keeps <c>@std</c> alive.
/// 5. Lookup <see cref="StdlibSymbolIndex.ProcsForModule"/> for the import's module;
///    if any of those procs' Names appear in the referenced-names set, the import
///    is "used" — skip emitting. Otherwise emit one Warning diagnostic.
///
/// Source string convention (Phase 31 D-05 + Phase 24 D-18): every emitted
/// Diagnostic carries <c>Source="flow.unusedImport"</c> so editors filter
/// independently of parse errors / scale-lint / shadow / unreachable.
///
/// Charitable fail-open (Phase 24 D-22 precedent): malformed AST returns
/// <c>Array.Empty&lt;Diagnostic&gt;()</c> — never throws past the public boundary.
/// </summary>
public static class UnusedImportAnalyzer
{
    /// <summary>
    /// Walk the AST and return Warning-severity Diagnostic instances for every
    /// <c>use "@module";</c> declaration whose module's procs are never
    /// referenced in the file. Returns an empty list when no imports exist or
    /// when every import has at least one referenced proc.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        StdlibSymbolIndex stdlib)
    {
        try
        {
            if (ast?.Statements is null) return Array.Empty<Diagnostic>();

            // Step 1: collect every ImportStatement.
            var imports = new List<ImportStatement>();
            foreach (var stmt in ast.Statements)
            {
                if (stmt is ImportStatement imp)
                    imports.Add(imp);
            }
            if (imports.Count == 0) return Array.Empty<Diagnostic>();

            // Step 2: collect every identifier referenced in the AST.
            var referenced = new HashSet<string>();
            foreach (var stmt in ast.Statements)
                CollectReferencedNames(stmt, referenced);

            // Step 3-5: per-import usage check + diagnostic emission.
            var diagnostics = new List<Diagnostic>();
            foreach (var imp in imports)
            {
                var moduleName = ExtractModuleName(imp.FilePath);
                if (moduleName is null)
                {
                    // Non-@ paths (relative paths to user modules) — we can't reason
                    // about user-module exports without resolving the file, so be
                    // conservative and treat them as used (no diagnostic).
                    continue;
                }

                if (IsImportUsed(moduleName, referenced, stdlib))
                    continue;

                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Source = "flow.unusedImport",
                    Message = $"Unused import: \"{imp.FilePath}\"",
                    Range = LspMappings.ToRange(imp.Location)
                });
            }
            return diagnostics;
        }
        catch
        {
            // Charitable fail-open per Phase 24 D-22 precedent.
            return Array.Empty<Diagnostic>();
        }
    }

    /// <summary>
    /// Extract a module name from an import file path.
    /// <c>"@harmony"</c> → <c>"harmony"</c>; non-<c>@</c> paths (relative user
    /// modules) return null so the caller can treat them conservatively (kept
    /// as used; no diagnostic).
    /// </summary>
    private static string? ExtractModuleName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        if (filePath.StartsWith('@'))
            return filePath.Substring(1);
        return null;
    }

    /// <summary>
    /// True iff at least one proc from <paramref name="moduleName"/> (or, when
    /// <paramref name="moduleName"/> is <c>"std"</c>, any of its transitively
    /// imported child modules) appears in <paramref name="referenced"/>.
    /// </summary>
    private static bool IsImportUsed(
        string moduleName,
        HashSet<string> referenced,
        StdlibSymbolIndex stdlib)
    {
        // Special case: @std transitively imports the other stdlib modules.
        if (moduleName == "std")
        {
            foreach (var mod in StdlibSymbolIndex.ModuleNames)
            {
                if (HasAnyReferencedProcInModule(mod, referenced, stdlib))
                    return true;
            }
            return false;
        }
        return HasAnyReferencedProcInModule(moduleName, referenced, stdlib);
    }

    private static bool HasAnyReferencedProcInModule(
        string moduleName,
        HashSet<string> referenced,
        StdlibSymbolIndex stdlib)
    {
        foreach (var proc in stdlib.ProcsForModule(moduleName))
        {
            if (referenced.Contains(proc.Name))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Recursively walk a statement and add every referenced identifier
    /// (function-call names, variable references, member names) to
    /// <paramref name="acc"/>. Descends through block bodies, lambda bodies,
    /// and every expression sub-tree.
    /// </summary>
    private static void CollectReferencedNames(Statement stmt, HashSet<string> acc)
    {
        switch (stmt)
        {
            case MusicalContextStatement m:
                if (m.Value is not null) CollectFromExpr(m.Value, acc);
                if (m.Value2 is not null) CollectFromExpr(m.Value2, acc);
                foreach (var s in m.Body) CollectReferencedNames(s, acc);
                break;
            case SectionDeclaration sd:
                foreach (var s in sd.Body) CollectReferencedNames(s, acc);
                break;
            case ProcDeclaration pd:
                foreach (var s in pd.Body) CollectReferencedNames(s, acc);
                break;
            case ForStatement fs:
                CollectFromExpr(fs.Collection, acc);
                foreach (var s in fs.Body) CollectReferencedNames(s, acc);
                break;
            case WhileStatement ws:
                CollectFromExpr(ws.Condition, acc);
                foreach (var s in ws.Body) CollectReferencedNames(s, acc);
                break;
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
            case TupleDestructureStatement td:
                // The RHS expression is what we want — left-hand bindings are
                // declarations, not references.
                try
                {
                    var valueProp = td.GetType().GetProperty("Value");
                    if (valueProp?.GetValue(td) is Expression valExpr)
                        CollectFromExpr(valExpr, acc);
                }
                catch
                {
                    // shape may differ across versions; silently skip
                }
                break;
            // ImportStatement: nothing to collect.
            // Break/Continue: nothing to collect.
        }
    }

    private static void CollectFromExpr(Expression? expr, HashSet<string> acc)
    {
        if (expr is null) return;
        switch (expr)
        {
            case FunctionCallExpression fc:
                acc.Add(fc.Name);
                foreach (var a in fc.Arguments) CollectFromExpr(a, acc);
                break;
            case VariableExpression v:
                acc.Add(v.Name);
                break;
            case MemberAccessExpression ma:
                acc.Add(ma.MemberName);
                CollectFromExpr(ma.Object, acc);
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
            case LazyExpression lz:
                // Carries an inner expression — reflect to fetch since shape may
                // vary; ignore quietly on failure.
                try
                {
                    var innerProp = lz.GetType().GetProperty("Inner")
                                  ?? lz.GetType().GetProperty("Expression");
                    if (innerProp?.GetValue(lz) is Expression inner)
                        CollectFromExpr(inner, acc);
                }
                catch { }
                break;
            case LambdaExpression la:
                foreach (var s in la.Body) CollectReferencedNames(s, acc);
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements) CollectFromExpr(e, acc);
                break;
            case TupleUnpackFlowExpression tu:
                CollectFromExpr(tu.Left, acc);
                CollectFromExpr(tu.Right, acc);
                break;
            // LiteralExpression, ChordLiteralExpression, NoteStreamExpression,
            // SongExpression, SymbolLiteralExpression, InterpolatedStringExpression,
            // ProgressionExpression: no plain identifier-references that matter
            // for unused-import detection in v1.4 (NoteStream tokens are notes,
            // not function names; Song references section names, not procs).
        }
    }
}
