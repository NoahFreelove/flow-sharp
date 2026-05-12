using System.CommandLine;
using FlowLang.Core;

namespace FlowCli.Commands;

// `flow eval <code>` — evaluates a Flow source string in-process.
//
// Mirrors flow-interpreter/Program.cs::RunFromString: instantiates a FlowEngine,
// optionally configures the audio device, then calls Execute(code, "<eval>"). On
// failure the engine's ErrorReporter output goes to stderr in red, matching the
// legacy interpreter's behaviour byte-for-byte so REQ-8 backward-compat holds.
internal static class EvalCommand
{
    public static Command Build()
    {
        var codeArg = new Argument<string>("code") { Description = "Flow source string" };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("eval", "Evaluate a Flow expression string");
        cmd.Add(codeArg);
        cmd.Add(deviceOpt);
        cmd.Add(verboseOpt);
        cmd.SetAction(parseResult =>
        {
            var code = parseResult.GetValue(codeArg)!;
            var device = parseResult.GetValue(deviceOpt);
            var verbose = parseResult.GetValue(verboseOpt);

            try
            {
                using var engine = new FlowEngine(verbose: verbose);
                if (device != null && engine.AudioManager.IsAudioAvailable())
                {
                    var backend = engine.AudioManager.GetBackend();
                    if (!backend.SetDevice(device))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Error.WriteLine($"Warning: Could not set audio device '{device}'");
                        Console.ResetColor();
                    }
                }

                var success = engine.Execute(code, "<eval>");

                if (!success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(engine.ErrorReporter.FormatErrors());
                    Console.ResetColor();
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error executing code: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        });
        return cmd;
    }
}
