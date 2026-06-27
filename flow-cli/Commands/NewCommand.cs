using System.CommandLine;
using FlowCli.Scaffold;

namespace FlowCli.Commands;

// `flow new <name>` — scaffolds a minimum-viable Flow piece.
//
// Writes <targetDir>/<name>.flow with the embedded default.flow template,
// {{PIECE_NAME}} substituted with <name>. Default targetDir is ./<name>/
// so the project name doubles as the containing folder; --dir overrides
// for ad-hoc layouts.
internal static class NewCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name") { Description = "Piece name (becomes <name>/<name>.flow)" };
        var dirOpt = new Option<DirectoryInfo?>("--dir") { Description = "Target directory (default: ./<name>/)" };

        var cmd = new Command("new", "Scaffold a new Flow project");
        cmd.Add(nameArg);
        cmd.Add(dirOpt);
        cmd.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var dir = parseResult.GetValue(dirOpt);
            var targetDir = dir?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), name);

            if (!ScaffoldEmitter.WriteScaffold(name, targetDir, out var path, out var err))
            {
                Console.Error.WriteLine($"flow new: {err}");
                return 1;
            }

            Console.WriteLine($"Created {path}");
            Console.WriteLine($"Next: flow run {path}");
            return 0;
        });
        return cmd;
    }
}
