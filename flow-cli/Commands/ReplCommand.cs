using System.CommandLine;
using FlowInterpreter;

namespace FlowCli.Commands;

// `flow repl` — starts the interactive Flow REPL.
//
// Thin wrapper over FlowInterpreter.Repl.Run(); identical behaviour to
// `dotnet run --project flow-interpreter` with no script argument.
internal static class ReplCommand
{
    public static Command Build()
    {
        var cmd = new Command("repl", "Start the interactive Flow REPL");
        cmd.SetAction(parseResult =>
        {
            new Repl().Run();
            return 0;
        });
        return cmd;
    }
}
