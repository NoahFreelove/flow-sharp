using FlowLang.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace FlowLsp.Handlers;

/// <summary>
/// Interface over PublishDiagnostics so tests can substitute a recording implementation
/// without standing up an OmniSharp <c>ILanguageServerFacade</c>. The real publisher's
/// ctor takes ILanguageServerFacade; tests inject a mock that implements IDiagnosticsPublisher.
/// </summary>
public interface IDiagnosticsPublisher
{
    void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors);
}

/// <summary>
/// Transforms FlowError[] into LSP Diagnostic[] and publishes via the
/// server's TextDocument facade. Empty diagnostic arrays MUST still publish —
/// that is how LSP clears prior squiggles (RESEARCH §Pattern 2 caveat).
/// </summary>
public sealed class DiagnosticsPublisher : IDiagnosticsPublisher
{
    private readonly ILanguageServerFacade _server;

    public DiagnosticsPublisher(ILanguageServerFacade server) => _server = server;

    /// <summary>
    /// Pure: transform FlowError list to LSP Diagnostic list. Exposed static so
    /// unit tests can exercise the mapping without constructing an ILanguageServerFacade.
    /// </summary>
    public static IReadOnlyList<Diagnostic> BuildDiagnostics(IReadOnlyList<FlowError> errors)
    {
        var list = new List<Diagnostic>(errors.Count);
        foreach (var e in errors)
        {
            list.Add(new Diagnostic
            {
                Severity = LspMappings.ToSeverity(e.Level),
                Source = "flow",
                Message = e.Message,
                Range = LspMappings.ToRange(e.Location)
            });
        }
        return list;
    }

    public void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors)
    {
        // MUST publish even when empty — that is how LSP clears prior markers.
        var diags = BuildDiagnostics(errors);
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(diags)
        });
    }
}
