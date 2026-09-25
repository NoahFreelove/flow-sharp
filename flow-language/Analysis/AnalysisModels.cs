using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.TypeSystem;

namespace FlowLang.Analysis;

public sealed record SourceDocument(string Id, string Text);

/// <summary>A coded, source-spanned diagnostic; formatting belongs to the host.</summary>
public sealed record AnalysisDiagnostic(string Code, FlowDiagnostic Detail)
{
    public DiagnosticLevel Level => Detail.Level;
    public string Message => Detail.Message;
    public Span Span => Detail.Primary;
}

public sealed record SyntaxTree(
    SourceDocument Source,
    Ast.Program Program,
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics,
    IReadOnlyList<FlowError> LegacyErrors);

/// <summary>Declaration metadata only: no implementation delegate or evaluated default.</summary>
public sealed record ProcedureDescriptor(string Name, FunctionSignature Signature,
    string SourceId, Span Span, string? Documentation, bool IsInternal);

public sealed record ModuleDescriptor(SyntaxTree Syntax,
    IReadOnlyList<ProcedureDescriptor> Procedures, IReadOnlyList<ImportStatement> Imports)
{
    public string Id => Syntax.Source.Id;
    public string? Name => Syntax.Program.Statements.OfType<ModuleDeclarationStatement>().FirstOrDefault()?.Name;
}

public sealed record ModuleDependency(string ImporterId, string SourceId, Span ImportSpan);

public sealed record AnalysisOptions(int MaxModules = 256, CancellationToken Cancellation = default);

/// <summary>
/// Syntax and top-level module discovery. Success does not prove runtime validity.
/// Conservative binding and source-procedure call warnings supplement syntax/import errors.
/// Runtime overload selection, values and effects remain unchecked.
/// </summary>
public sealed record AnalysisResult(SyntaxTree Root, IReadOnlyList<ModuleDescriptor> Modules,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics)
{
    public IReadOnlyList<ModuleDependency> Dependencies { get; init; } = [];
    public bool Success => Diagnostics.All(d => d.Level != DiagnosticLevel.Error);
    public IReadOnlyList<string> Unchecked { get; } = new[]
    {
        "Declaration order, dynamic bindings, host overloads/defaults, and non-literal type inference",
        "Conditional or nested imports and dynamic module exports",
        "Runtime values, effects, capabilities and termination",
    };
}
