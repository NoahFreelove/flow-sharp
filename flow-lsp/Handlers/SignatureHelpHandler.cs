using System.Threading;
using System.Threading.Tasks;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace FlowLsp.Handlers;

/// <summary>
/// Serves <c>textDocument/signatureHelp</c> for Flow buffers. Best-effort
/// comma-count active-parameter detection for `fnName(arg1, arg2, |cursor|)`
/// surface. Flow-operator chained calls (<c>x -&gt; fn(arg)</c>) are out of
/// scope for v1 (per 17-06 plan objective).
///
/// Phase 17 (17-06).
/// </summary>
public sealed class SignatureHelpHandler : SignatureHelpHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly BuiltInIndex _builtIns;

    public SignatureHelpHandler(DocumentManager docs, BuiltInIndex builtIns)
    {
        _docs = docs;
        _builtIns = builtIns;
    }

    /// <summary>Minimal call context for the pure parser — function name + active param index.</summary>
    public sealed record CallContext(string FunctionName, int ActiveParameter);

    /// <summary>
    /// Parses the text preceding the cursor to detect `fnName(arg1, arg2, |cursor|`.
    /// Scans backward from the cursor on the current line, tracking paren depth so
    /// nested calls are correctly skipped. Returns null if no open `(` is found
    /// before the cursor.
    /// </summary>
    public static CallContext? DetectCall(string text, Position cursor)
    {
        var lines = text.Split('\n');
        if (cursor.Line >= lines.Length) return null;
        var line = lines[cursor.Line];
        if (cursor.Character > line.Length) return null;
        var prefix = line.Substring(0, cursor.Character);

        int depth = 0, commas = 0, openIdx = -1;
        for (int i = prefix.Length - 1; i >= 0; i--)
        {
            char c = prefix[i];
            if (c == ')') depth++;
            else if (c == '(')
            {
                if (depth == 0) { openIdx = i; break; }
                depth--;
            }
            else if (c == ',' && depth == 0) commas++;
        }
        if (openIdx < 0) return null;

        // Extract the function name immediately preceding the `(`.
        int ni = openIdx - 1;
        while (ni >= 0 && char.IsWhiteSpace(prefix[ni])) ni--;
        int nameEnd = ni + 1;
        while (ni >= 0 && (char.IsLetterOrDigit(prefix[ni]) || prefix[ni] == '_')) ni--;
        int nameStart = ni + 1;
        if (nameEnd <= nameStart) return null;
        return new CallContext(prefix.Substring(nameStart, nameEnd - nameStart), commas);
    }

    public override Task<SignatureHelp?> Handle(SignatureHelpParams req, CancellationToken ct)
    {
        var text = _docs.GetText(req.TextDocument.Uri) ?? string.Empty;
        var ctx = DetectCall(text, req.Position);
        if (ctx is null) return Task.FromResult<SignatureHelp?>(null);
        var b = _builtIns.Find(ctx.FunctionName);
        if (b is null) return Task.FromResult<SignatureHelp?>(null);

        var sig = new SignatureInformation
        {
            Label = b.Signatures.Count > 0 ? b.Signatures[0].ToString() : ctx.FunctionName,
            Parameters = new Container<ParameterInformation>()
        };
        return Task.FromResult<SignatureHelp?>(new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(sig),
            ActiveSignature = 0,
            ActiveParameter = ctx.ActiveParameter
        });
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("flow"),
            TriggerCharacters = new Container<string>("(", ","),
        };
}
