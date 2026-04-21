using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

// Wave 4 bootstrap (plan 17-05). Extends the plan 17-03 wiring with the 4
// symbol indices (BuiltInIndex / StdlibSymbolIndex / KeywordIndex / UserSymbolIndex)
// and the CompletionHandler. The registry is populated via
// BuiltInFunctions.RegisterSignaturesOnly — D-07 "every built-in" completeness
// without constructing or invoking any audio output backend.
//
// DocumentManager's onParse callback also pushes the fresh AST into the
// UserSymbolIndex so per-keystroke completion reflects newly-declared procs,
// variables, and sections.
var server = await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithServices(services => services
        .AddSingleton<ParseSession>()
        .AddSingleton<DiagnosticsPublisher>()
        .AddSingleton<IDiagnosticsPublisher>(sp => sp.GetRequiredService<DiagnosticsPublisher>())
        .AddSingleton<FlowLang.StandardLibrary.InternalFunctionRegistry>(_ =>
        {
            var r = new FlowLang.StandardLibrary.InternalFunctionRegistry();
            // Option C — D-07 full coverage, audio-free (stubs throw NotSupportedException).
            FlowLang.StandardLibrary.BuiltInFunctions.RegisterSignaturesOnly(r);
            return r;
        })
        .AddSingleton<BuiltInIndex>()
        .AddSingleton<StdlibSymbolIndex>()
        .AddSingleton<KeywordIndex>()
        .AddSingleton<UserSymbolIndex>()
        .AddSingleton<DocumentManager>(sp =>
        {
            var parser = sp.GetRequiredService<ParseSession>();
            var diag = sp.GetRequiredService<IDiagnosticsPublisher>();
            var users = sp.GetRequiredService<UserSymbolIndex>();
            DocumentManager? dm = null;
            dm = new DocumentManager((uri, text, ct) =>
            {
                if (ct.IsCancellationRequested) return Task.CompletedTask;
                var result = parser.Parse(text, uri.GetFileSystemPath());
                // CLOSE-RACE GUARD: if the doc closed during the debounce window,
                // do NOT publish — that would revive cleared diagnostics.
                if (dm!.HasDocument(uri))
                {
                    users.Update(uri, result.Ast);
                    diag.Publish(uri, result.Errors);
                }
                return Task.CompletedTask;
            });
            return dm;
        }))
    .WithHandler<TextDocumentSyncHandler>()
    .WithHandler<SemanticTokensHandler>()
    .WithHandler<CompletionHandler>()
    .WithHandler<HoverHandler>()
    .WithHandler<SignatureHelpHandler>()
    .WithHandler<DefinitionHandler>());

await server.WaitForExit;
