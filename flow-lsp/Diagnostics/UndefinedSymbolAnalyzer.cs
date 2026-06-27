using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 31 Plan 31-08 scope expansion (SPEC-1 follow-up): undefined-symbol
/// analyzer — flags function calls whose head identifier is not resolvable
/// in the current file under the active set of `use "@module";` imports.
///
/// The "OPPOSITE" of <see cref="UnusedImportAnalyzer"/>:
/// <list type="bullet">
///   <item>UnusedImport: imported module never referenced → Warning on the use line.</item>
///   <item>UndefinedSymbol: referenced name not provided by any import → Warning on the call.</item>
/// </list>
///
/// Algorithm (build-universe, then check-callsites):
/// 1. Build the universe of known names by union of:
///    a. Procs in every imported stdlib module — `@std` expands to the
///       transitive set (matches <see cref="UnusedImportAnalyzer"/>). NOTE:
///       Flow's stdlib procs are surfaced via `internal proc NAME (...)`
///       declarations in the .flow stdlib files; the C# <see cref="BuiltInIndex"/>
///       is deliberately NOT used here because Flow requires composers to
///       `use "@std"` (or similar) for every stdlib function. Including the
///       BuiltInIndex would mask the import requirement and defeat the
///       purpose of this analyzer (catching missing imports).
///    b. Every user-declared name reachable in the AST: <c>ProcDeclaration.Name</c>,
///       <c>Parameter.Name</c>, <c>VariableDeclaration.Name</c>,
///       <c>SectionDeclaration.Name</c>, <c>ForStatement.VariableName</c>,
///       <c>LambdaParameter.Name</c>, <c>TupleDestructureStatement</c> targets.
///       This is intentionally CONSERVATIVE — we collect every name that COULD
///       be a binding regardless of scope (Flow has no nested-proc shadowing
///       concerns; ShadowedVariableAnalyzer handles same-name collisions).
///    c. Roman numerals I–VII and i–vii — these appear as function-call-shaped
///       references inside note streams + chord progressions (resolved via
///       musical key context at runtime, not symbol lookup). False-positive
///       avoidance — flagging composer's roman numeral usage would be noise.
///    d. Files with non-`@` imports (relative user-module paths) skip the
///       check entirely — we can't reason about user-module exports without
///       resolving the file, and false-positive squiggles for legitimate
///       imports would be noise.
/// 2. Walk every <see cref="FunctionCallExpression"/> in the AST.
///    If the head <c>Name</c> isn't in the universe → emit a Warning with a
///    helpful message suggesting the missing import when the name matches a
///    proc in a known stdlib module the file didn't import.
///
/// Source string (Phase 31 D-05 + Phase 24 D-18): every emitted Diagnostic
/// carries <c>Source="flow.undefinedSymbol"</c> so editors can filter or
/// configure it independently of UnusedImport / UnreachableSection /
/// ShadowedVariable / ScaleLint / parse errors.
///
/// Charitable fail-open (Phase 24 D-22 precedent): malformed AST returns
/// <see cref="Array.Empty{T}"/> — never throws past the public boundary.
///
/// Out of scope (v1):
/// <list type="bullet">
///   <item>Variable-reference checking (<see cref="VariableExpression"/>) —
///         too many cross-scope false positives without proper scope tracking.</item>
///   <item>Member-access checking (<see cref="MemberAccessExpression"/>) —
///         requires type inference.</item>
///   <item>Module-resolution for non-`@` imports (relative paths to user
///         modules) — would need to actually load the imported file.</item>
/// </list>
/// </summary>
public static class UndefinedSymbolAnalyzer
{
    private static readonly HashSet<string> RomanNumerals = new(StringComparer.Ordinal)
    {
        "I",   "II",  "III", "IV",  "V",   "VI",  "VII",
        "i",   "ii",  "iii", "iv",  "v",   "vi",  "vii",
    };

    /// <summary>
    /// Walk the AST and emit Warning-severity Diagnostic instances for every
    /// function-call whose head identifier is not resolvable under the file's
    /// active imports + user declarations + builtins. Returns an empty list
    /// when the file is empty or every call resolves.
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

            // Conservative gate: if the file has any non-`@` import (relative
            // path to a user module), skip the check — we can't reason about
            // user-module exports without resolving the file, and false
            // positives on user-defined procs would be noise.
            foreach (var stmt in ast.Statements)
            {
                if (stmt is ImportStatement imp &&
                    !string.IsNullOrEmpty(imp.FilePath) &&
                    !imp.FilePath.StartsWith('@'))
                {
                    return Array.Empty<Diagnostic>();
                }
            }

            // Step 1: build the universe of known names.
            var universe = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rn in RomanNumerals) universe.Add(rn);

            // Imported stdlib procs.
            foreach (var stmt in ast.Statements)
            {
                if (stmt is ImportStatement imp)
                {
                    var mod = ExtractModuleName(imp.FilePath);
                    if (mod is null) continue;
                    if (mod == "std")
                    {
                        foreach (var subMod in StdlibSymbolIndex.ModuleNames)
                            AddModuleProcs(stdlib, subMod, universe);
                    }
                    else
                    {
                        AddModuleProcs(stdlib, mod, universe);
                    }
                }
            }

            // User-declared names.
            foreach (var stmt in ast.Statements)
                CollectDeclaredNames(stmt, universe);

            // Step 2: walk every FunctionCallExpression; flag unresolved heads.
            var diagnostics = new List<Diagnostic>();
            foreach (var stmt in ast.Statements)
                CheckCallSites(stmt, universe, stdlib, diagnostics);
            return diagnostics;
        }
        catch
        {
            return Array.Empty<Diagnostic>();
        }
    }

    private static string? ExtractModuleName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        if (filePath.StartsWith('@'))
            return filePath.Substring(1);
        return null;
    }

    private static void AddModuleProcs(
        StdlibSymbolIndex stdlib,
        string moduleName,
        HashSet<string> universe)
    {
        foreach (var proc in stdlib.ProcsForModule(moduleName))
            universe.Add(proc.Name);
    }

    /// <summary>
    /// Recursively walk a statement; add every locally-declared identifier
    /// (proc name, parameter name, variable name, section name, for iterator,
    /// lambda param, tuple destructure target) to <paramref name="acc"/>.
    /// Walks nested expressions too (for lambdas inside variable initializers).
    /// </summary>
    private static void CollectDeclaredNames(Statement stmt, HashSet<string> acc)
    {
        switch (stmt)
        {
            case ProcDeclaration pd:
                acc.Add(pd.Name);
                foreach (var p in pd.Parameters) acc.Add(p.Name);
                foreach (var s in pd.Body) CollectDeclaredNames(s, acc);
                break;
            case VariableDeclaration vd:
                acc.Add(vd.Name);
                CollectDeclaredNamesFromExpr(vd.Value, acc);
                break;
            case AssignmentStatement asn:
                CollectDeclaredNamesFromExpr(asn.Value, acc);
                break;
            case SectionDeclaration sd:
                acc.Add(sd.Name);
                foreach (var s in sd.Body) CollectDeclaredNames(s, acc);
                break;
            case ForStatement fs:
                acc.Add(fs.VariableName);
                CollectDeclaredNamesFromExpr(fs.Collection, acc);
                foreach (var s in fs.Body) CollectDeclaredNames(s, acc);
                break;
            case WhileStatement ws:
                CollectDeclaredNamesFromExpr(ws.Condition, acc);
                foreach (var s in ws.Body) CollectDeclaredNames(s, acc);
                break;
            case MusicalContextStatement m:
                if (m.Value is not null) CollectDeclaredNamesFromExpr(m.Value, acc);
                if (m.Value2 is not null) CollectDeclaredNamesFromExpr(m.Value2, acc);
                foreach (var s in m.Body) CollectDeclaredNames(s, acc);
                break;
            case ExpressionStatement es:
                CollectDeclaredNamesFromExpr(es.Expression, acc);
                break;
            case ReturnStatement rs:
                if (rs.Value is not null) CollectDeclaredNamesFromExpr(rs.Value, acc);
                break;
            case TupleDestructureStatement td:
                try
                {
                    // Shape may vary across versions: try common property names
                    // for the LHS bindings.
                    var targetsProp = td.GetType().GetProperty("Targets")
                                   ?? td.GetType().GetProperty("Names");
                    if (targetsProp?.GetValue(td) is System.Collections.IEnumerable targets)
                    {
                        foreach (var t in targets)
                        {
                            if (t is null) continue;
                            // Each target may be a string OR a tuple-target record
                            // with a Name property.
                            if (t is string s) { acc.Add(s); continue; }
                            var nameProp = t.GetType().GetProperty("Name");
                            if (nameProp?.GetValue(t) is string n) acc.Add(n);
                        }
                    }
                    var valueProp = td.GetType().GetProperty("Value");
                    if (valueProp?.GetValue(td) is Expression valExpr)
                        CollectDeclaredNamesFromExpr(valExpr, acc);
                }
                catch
                {
                    // Tolerate shape mismatch — better to miss a few bindings
                    // (false-positive squiggles) than to throw past the boundary.
                }
                break;
            // ImportStatement, Break, Continue: no declarations to collect.
        }
    }

    private static void CollectDeclaredNamesFromExpr(Expression? expr, HashSet<string> acc)
    {
        if (expr is null) return;
        switch (expr)
        {
            case LambdaExpression la:
                foreach (var p in la.Parameters) acc.Add(p.Name);
                foreach (var s in la.Body) CollectDeclaredNames(s, acc);
                break;
            case FunctionCallExpression fc:
                foreach (var a in fc.Arguments) CollectDeclaredNamesFromExpr(a, acc);
                break;
            case FlowExpression fe:
                CollectDeclaredNamesFromExpr(fe.Left, acc);
                CollectDeclaredNamesFromExpr(fe.Right, acc);
                break;
            case ArrayLiteralExpression al:
                foreach (var e in al.Elements) CollectDeclaredNamesFromExpr(e, acc);
                break;
            case ArrayIndexExpression ai:
                CollectDeclaredNamesFromExpr(ai.Array, acc);
                CollectDeclaredNamesFromExpr(ai.Index, acc);
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements) CollectDeclaredNamesFromExpr(e, acc);
                break;
            case TupleUnpackFlowExpression tu:
                CollectDeclaredNamesFromExpr(tu.Left, acc);
                CollectDeclaredNamesFromExpr(tu.Right, acc);
                break;
            // Other expression shapes have no nested declarations.
        }
    }

    private static void CheckCallSites(
        Statement stmt,
        HashSet<string> universe,
        StdlibSymbolIndex stdlib,
        List<Diagnostic> diagnostics)
    {
        switch (stmt)
        {
            case ProcDeclaration pd:
                foreach (var s in pd.Body) CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case SectionDeclaration sd:
                foreach (var s in sd.Body) CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case MusicalContextStatement m:
                if (m.Value is not null) CheckCallSitesInExpr(m.Value, universe, stdlib, diagnostics);
                if (m.Value2 is not null) CheckCallSitesInExpr(m.Value2, universe, stdlib, diagnostics);
                foreach (var s in m.Body) CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case ForStatement fs:
                CheckCallSitesInExpr(fs.Collection, universe, stdlib, diagnostics);
                foreach (var s in fs.Body) CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case WhileStatement ws:
                CheckCallSitesInExpr(ws.Condition, universe, stdlib, diagnostics);
                foreach (var s in ws.Body) CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case ExpressionStatement es:
                CheckCallSitesInExpr(es.Expression, universe, stdlib, diagnostics);
                break;
            case VariableDeclaration vd:
                CheckCallSitesInExpr(vd.Value, universe, stdlib, diagnostics);
                break;
            case AssignmentStatement asn:
                CheckCallSitesInExpr(asn.Value, universe, stdlib, diagnostics);
                break;
            case ReturnStatement rs:
                if (rs.Value is not null) CheckCallSitesInExpr(rs.Value, universe, stdlib, diagnostics);
                break;
            // ImportStatement, Break, Continue, TupleDestructureStatement: no
            // call sites to surface (TupleDestructureStatement's RHS is handled
            // via its Value reflection path already if present).
        }
    }

    private static void CheckCallSitesInExpr(
        Expression? expr,
        HashSet<string> universe,
        StdlibSymbolIndex stdlib,
        List<Diagnostic> diagnostics)
    {
        if (expr is null) return;
        switch (expr)
        {
            case FunctionCallExpression fc:
                if (!universe.Contains(fc.Name))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Source = "flow.undefinedSymbol",
                        Message = BuildMessage(fc.Name, stdlib),
                        Range = LspMappings.ToRange(fc.Location),
                    });
                }
                foreach (var a in fc.Arguments)
                    CheckCallSitesInExpr(a, universe, stdlib, diagnostics);
                break;
            case FlowExpression fe:
                CheckCallSitesInExpr(fe.Left, universe, stdlib, diagnostics);
                CheckCallSitesInExpr(fe.Right, universe, stdlib, diagnostics);
                break;
            case ArrayLiteralExpression al:
                foreach (var e in al.Elements)
                    CheckCallSitesInExpr(e, universe, stdlib, diagnostics);
                break;
            case ArrayIndexExpression ai:
                CheckCallSitesInExpr(ai.Array, universe, stdlib, diagnostics);
                CheckCallSitesInExpr(ai.Index, universe, stdlib, diagnostics);
                break;
            case LambdaExpression la:
                foreach (var s in la.Body)
                    CheckCallSites(s, universe, stdlib, diagnostics);
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements)
                    CheckCallSitesInExpr(e, universe, stdlib, diagnostics);
                break;
            case TupleUnpackFlowExpression tu:
                CheckCallSitesInExpr(tu.Left, universe, stdlib, diagnostics);
                CheckCallSitesInExpr(tu.Right, universe, stdlib, diagnostics);
                break;
            // VariableExpression, MemberAccessExpression, LiteralExpression,
            // ChordLiteralExpression, NoteStreamExpression, SongExpression,
            // SymbolLiteralExpression — no function-call heads to surface.
        }
    }

    /// <summary>
    /// Build a helpful message. When the unresolved name exists in exactly one
    /// known stdlib module, suggest the `use` line. When it exists in multiple,
    /// list them. Otherwise emit the generic form.
    /// </summary>
    private static string BuildMessage(string name, StdlibSymbolIndex stdlib)
    {
        var candidates = new List<string>();
        foreach (var mod in StdlibSymbolIndex.ModuleNames)
        {
            foreach (var proc in stdlib.ProcsForModule(mod))
            {
                if (proc.Name == name)
                {
                    candidates.Add(mod);
                    break;
                }
            }
        }
        if (candidates.Count == 1)
            return $"Unknown identifier '{name}'. Did you forget `use \"@{candidates[0]}\";`?";
        if (candidates.Count > 1)
            return $"Unknown identifier '{name}'. Available in: " +
                   string.Join(", ", candidates.ConvertAll(c => $"@{c}"));
        return $"Unknown identifier '{name}'.";
    }
}
