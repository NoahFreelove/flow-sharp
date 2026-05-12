using System.CommandLine;
using FlowInterpreter;
using FlowLang.Runtime;

namespace FlowCli.Commands;

// `flow play <script.flow>` — runs a script that is expected to invoke
// (play …) on its own audio output. Identical forwarding semantics to
// RunCommand: Phase 30 does NOT auto-inject a (play …) call. The composer's
// .flow source is the single source of truth for what audio gets played,
// matching the charitable-interpretation memory (project memory).
internal static class PlayCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("play", "Play a Flow script's audio output via PulseAudio");
        cmd.Add(scriptArg);
        cmd.Add(deviceOpt);
        cmd.Add(verboseOpt);
        cmd.SetAction(parseResult =>
        {
            var script = parseResult.GetValue(scriptArg)!;
            var device = parseResult.GetValue(deviceOpt);
            var verbose = parseResult.GetValue(verboseOpt);

            // REQ-4 (Plan 30-03 Task 4): --device wins; otherwise fall back to
            // FlowConfig.Active.DefaultAudioDevice; otherwise null (existing
            // behavior — system default audio device).
            device ??= FlowConfig.Active.DefaultAudioDevice;

            if (!File.Exists(script.FullName))
            {
                Console.Error.WriteLine($"Error: File not found: {script.FullName}");
                return 1;
            }

            return new ScriptRunner().RunScript(script.FullName, device, verbose);
        });
        return cmd;
    }
}
