using System.CommandLine;

namespace FlowCli.Commands;

// `flow lsp` — starts the Flow Language Server over stdio.
//
// Thin wrapper that delegates to FlowLsp.Program.Main, which performs the
// OmniSharp LanguageServer.From(...) wiring (see flow-lsp/Program.cs).
//
// Resolves RESEARCH Pitfall 7 (binary discoverability from the JetBrains
// plugin): with `flow lsp` registered on the unified CLI, the LSP4IJ
// FlowLanguageServerFactory.kt (Plan 31-08) can spawn the server via
// GeneralCommandLine("flow", "lsp") and inherit `flow install`'s PATH wiring
// from Phase 30 for free.
internal static class LspCommand
{
    public static Command Build()
    {
        var cmd = new Command("lsp", "Start the Flow Language Server (stdio LSP)");
        cmd.SetAction(parseResult =>
        {
            // Delegate to flow-lsp's explicit Main entry — see flow-lsp/Program.cs
            // for the OmniSharp LanguageServer.From() wiring and DI setup.
            return FlowLsp.Program.Main(Array.Empty<string>()).GetAwaiter().GetResult();
        });
        return cmd;
    }
}
