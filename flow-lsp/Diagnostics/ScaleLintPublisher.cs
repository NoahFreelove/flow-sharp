using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-04: concrete <see cref="IScaleLintPublisher"/>. Delegates to the
/// static <see cref="ScaleLintAnalyzer"/>. The D-19 short-circuit
/// (<c>Ast.Pragmas.Has("scaleLint")</c>) lives inside <c>ScaleLintAnalyzer.Analyze</c>
/// — this class is a thin DI-mockable adapter and adds no decision logic.
/// </summary>
public sealed class ScaleLintPublisher : IScaleLintPublisher
{
    public IReadOnlyList<Diagnostic> Analyze(ParseResult result, string source) =>
        ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
}
