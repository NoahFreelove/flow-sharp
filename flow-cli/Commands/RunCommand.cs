using System.CommandLine;
using FlowInterpreter;
using FlowLang.Runtime;

namespace FlowCli.Commands;

// `flow run <script.flow>` — executes a Flow script file.
//
// Thin wrapper over FlowInterpreter.ScriptRunner.RunScript: identical
// behaviour to `dotnet run --project flow-interpreter <script.flow>` so
// that REQ-1 / REQ-8 (backward-compat with the existing entrypoint) holds.
// File-not-found is reported here (stderr) rather than letting ScriptRunner
// surface a less-friendly exception so the message is uniform across run /
// play / render / flow2midi / check.
internal static class RunCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("run", "Execute a Flow script");
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
