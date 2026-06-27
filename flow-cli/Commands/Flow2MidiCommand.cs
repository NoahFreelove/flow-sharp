using System.CommandLine;
using FlowInterpreter;

namespace FlowCli.Commands;

// `flow flow2midi <script.flow> -o out.mid` — executes the script; the
// script is expected to contain a (writeMidi …) call. Mirrors RenderCommand's
// charitable-interpretation tradeoff: --output is informational in Phase 30;
// CLI-driven auto-injection of writeMidi is deferred to v1.5+.
internal static class Flow2MidiCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var outputOpt = new Option<FileInfo>("--output", "-o")
        {
            Description = "Expected MIDI output path (must match what the script writes)",
            Required = true,
        };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name (unused for export)" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("flow2midi", "Export a Flow script to a MIDI file");
        cmd.Add(scriptArg);
        cmd.Add(outputOpt);
        cmd.Add(deviceOpt);
        cmd.Add(verboseOpt);
        cmd.SetAction(parseResult =>
        {
            var script = parseResult.GetValue(scriptArg)!;
            var output = parseResult.GetValue(outputOpt)!;
            var device = parseResult.GetValue(deviceOpt);
            var verbose = parseResult.GetValue(verboseOpt);

            if (!File.Exists(script.FullName))
            {
                Console.Error.WriteLine($"Error: File not found: {script.FullName}");
                return 1;
            }

            var exit = new ScriptRunner().RunScript(script.FullName, device, verbose);

            if (!File.Exists(output.FullName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine(
                    $"Warning: --output={output.FullName} but the script did not write to that path. " +
                    "For Phase 30, the .flow source must contain (writeMidi \"...\") — " +
                    "auto-injection deferred (ROADMAP v1.5+).");
                Console.ResetColor();
            }

            return exit;
        });
        return cmd;
    }
}
