using System.CommandLine;
using FlowCli.Commands;
using FlowCli.Config;

namespace FlowCli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // REQ-4 (Plan 30-03 Task 2): load ~/.config/flow/config.toml into
        // FlowConfig.Active before any FlowEngine is constructed. FlowEngine reads
        // FlowConfig.ConfiguredStdlibSearchPaths at ModuleLoader-seed time, so the
        // config must be active before that point. Missing file: silent fallback.
        // Malformed file: charitable warn + continue with defaults (per CLAUDE.md).
        FlowConfigLoader.LoadFromXdg();

        var root = new RootCommand("Flow — a programming language for music");
        foreach (var cmd in CommandRegistry.BuildAllCommands())
            root.Subcommands.Add(cmd);
        return await root.Parse(args).InvokeAsync();
    }
}
