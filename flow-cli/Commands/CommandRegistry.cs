using System.CommandLine;

namespace FlowCli.Commands;

// Central listing of every subcommand the `flow` binary recognizes.
// Plan 30-01 wired 10 placeholders + the real `version` handler.
// Plan 30-02 replaced the placeholders with real handlers (and one
// explicit Plan 30-09 deferral stub for `midi2flow`).
// Plan 30-09 Task 1 replaces the deferral stub with the real Midi2FlowCommand.
// Plan 31-01 Task 1 adds LspCommand — 12 subcommands total; Phase 31 added
// LspCommand for JetBrains plugin binary-discoverability per RESEARCH Pitfall 7.
// Plan 35-04 Task 3 adds TestCommand — 13 subcommands total; Phase 35 TEST-01
// wires the pure-Flow test framework into `flow test [path]`.
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
            LspCommand.Build(),     // Phase 31 REQ-7 support
            TestCommand.Build(),    // NEW — Phase 35 Plan 35-04 TEST-01 (pure-Flow test framework)
        };
    }
}
