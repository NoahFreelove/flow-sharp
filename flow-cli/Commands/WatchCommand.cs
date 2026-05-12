using System.CommandLine;
using FlowInterpreter;
using FlowLang.Runtime;

namespace FlowCli.Commands;

// `flow watch <script.flow>` — runs a script in live-reload mode.
//
// Mirrors flow-interpreter/Program.cs::RunWithWatch: resolves the path
// to an absolute form, constructs a LiveReloadManager, and runs until
// the user terminates the process. LiveReloadManager owns its own
// streaming AudioPlaybackManager so we do NOT call ConfigureDevice
// against a separate engine — the device name is passed straight in.
internal static class WatchCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("watch", "Run a Flow script in watch (auto-reload) mode");
        cmd.Add(scriptArg);
        cmd.Add(deviceOpt);
        cmd.Add(verboseOpt);
        cmd.SetAction(parseResult =>
        {
            var script = parseResult.GetValue(scriptArg)!;
            var device = parseResult.GetValue(deviceOpt);
            // verbose flag accepted for parity; LiveReloadManager has its own diagnostics

            // REQ-4 (Plan 30-03 Task 4): --device wins; otherwise fall back to
            // FlowConfig.Active.DefaultAudioDevice; otherwise null (existing
            // behavior — system default audio device).
            device ??= FlowConfig.Active.DefaultAudioDevice;

            if (!File.Exists(script.FullName))
            {
                Console.Error.WriteLine($"Error: File not found: {script.FullName}");
                return 1;
            }

            var fullPath = Path.GetFullPath(script.FullName);
            using var manager = new LiveReloadManager(fullPath, device);
            manager.Run();
            return 0;
        });
        return cmd;
    }
}
