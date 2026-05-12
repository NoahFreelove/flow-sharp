using System.CommandLine;

namespace FlowCli.Commands;

// Central listing of every subcommand the `flow` binary recognizes.
// Plan 30-01 wired 10 placeholders + the real `version` handler.
// Plan 30-02 replaced the placeholders with real handlers (and one
// explicit Plan 30-09 deferral stub for `midi2flow`).
// Plan 30-09 Task 1 replaces the deferral stub with the real Midi2FlowCommand
// — all 11 subcommands now have production handlers; no stubs remain.
internal static class CommandRegistry
{
    public static Command[] BuildAllCommands()
    {
        return new[]
        {
            RunCommand.Build(),
            EvalCommand.Build(),
            ReplCommand.Build(),
            WatchCommand.Build(),
            PlayCommand.Build(),
            RenderCommand.Build(),
            Flow2MidiCommand.Build(),
            Midi2FlowCommand.Build(),
            CheckCommand.Build(),
            VersionCommand.Build(),
            NewCommand.Build(),
        };
    }
}
