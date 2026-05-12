using System.CommandLine;
using System.Reflection;

namespace FlowCli.Commands;

// `flow version` — the only fully-wired subcommand in Plan 30-01.
// Prints the assembly's InformationalVersion (set in flow-cli.csproj to
// `0.1.0-phase30`); falls back to the AssemblyName.Version if absent and
// finally to the literal string "unknown" if both are unavailable.
internal static class VersionCommand
{
    public static Command Build()
    {
        var cmd = new Command("version", "Print the Flow CLI version");
        cmd.SetAction(parseResult =>
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var ver = info ?? asm.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"flow {ver}");
            return 0;
        });
        return cmd;
    }
}
