using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.TypeSystem;

namespace FlowLang.Analysis;

/// <summary>Non-executing frontend shared by hosts. Never constructs an interpreter or engine.</summary>
public static class LanguageAnalysis
{
    public static SyntaxTree Parse(string source, string sourceId = "<source>")
    {
        var reporter = new ErrorReporter();
        var diagnostics = new List<AnalysisDiagnostic>();
        int errors = 0, rich = 0;
        List<Token> tokens = new();

        void Collect(string code)
        {
            foreach (var error in reporter.Errors.Skip(errors))
            {
                var span = tokens.FirstOrDefault(t => t.Location == error.Location)?.EffectiveSpan
                    ?? Span.At(error.Location);
                diagnostics.Add(new AnalysisDiagnostic(code, new FlowDiagnostic(
                    error.Level, error.Message, span, Array.Empty<DiagnosticLabel>(), Array.Empty<string>())));
            }
            foreach (var diagnostic in reporter.Diagnostics.Skip(rich))
                diagnostics.Add(new AnalysisDiagnostic(code, diagnostic));
            errors = reporter.Errors.Count;
            rich = reporter.Diagnostics.Count;
        }

        // Retain partial syntax after errors for editor recovery, as the LSP has always done.
        var (pragmas, transformed) = PragmaScanner.Scan(source, sourceId, reporter);
        Collect("flow.syntax.pragma");
        tokens = new SimpleLexer(transformed, reporter, sourceId, pragmas).Tokenize();
        Collect("flow.syntax.lex");
        var program = new Parser(tokens, reporter, pragmas).Parse();
        Collect("flow.syntax.parse");
        return new SyntaxTree(new SourceDocument(sourceId, source), program, tokens,
            diagnostics.ToArray(), reporter.Errors.ToArray());
    }

    public static ModuleDescriptor Describe(SyntaxTree syntax) => new(syntax,
        syntax.Program.Statements.OfType<ProcDeclaration>().Select(p => new ProcedureDescriptor(
            p.Name, new FunctionSignature(p.Name, p.Parameters.Select(x => x.Type).ToArray(),
                IsVarArgs: p.Parameters.Any(x => x.IsVarArgs),
                ParameterNames: p.Parameters.Select(x => x.Name).ToArray()),
            syntax.Source.Id, p.Span ?? Span.At(p.Location), p.DocComment, p.IsInternal)).ToArray(),
        syntax.Program.Statements.OfType<ImportStatement>().ToArray());

    public static AnalysisResult Analyze(SyntaxTree root, IModuleSourceProvider sources,
        AnalysisOptions? options = null)
    {
        options ??= new AnalysisOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxModules, 1);
        var pending = new Queue<SyntaxTree>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { root.Source.Id };
        var modules = new List<ModuleDescriptor>();
        var diagnostics = new List<AnalysisDiagnostic>();
        pending.Enqueue(root);
        while (pending.TryDequeue(out var syntax))
        {
            options.Cancellation.ThrowIfCancellationRequested();
            var module = Describe(syntax);
            modules.Add(module);
            diagnostics.AddRange(syntax.Diagnostics);
            foreach (var import in module.Imports)
            {
                options.Cancellation.ThrowIfCancellationRequested();
                var span = import.Span ?? Span.At(import.Location);
                SourceDocument? source;
                try { source = sources.Resolve(import.FilePath, syntax.Source.Id); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    diagnostics.Add(new("flow.module.read", FlowDiagnostic.Create(
                        $"Cannot read module '{import.FilePath}': {ex.Message}", span)));
                    continue;
                }
                if (source is null)
                {
                    diagnostics.Add(new("flow.module.missing", FlowDiagnostic.Create(
                        $"Module '{import.FilePath}' not found", span)));
                    continue;
                }
                if (seen.Contains(source.Id)) continue; // cycles and repeated imports need no evaluation
                if (seen.Count >= options.MaxModules)
                {
                    diagnostics.Add(new("flow.module.limit", FlowDiagnostic.Create(
                        $"Analysis module limit ({options.MaxModules}) reached", span)));
                    continue;
                }
                seen.Add(source.Id);
                pending.Enqueue(Parse(source.Text, source.Id));
            }
        }
        return new AnalysisResult(root, modules.ToArray(), diagnostics.ToArray());
    }
}
