using System.CommandLine;
using FlowInterpreter;

namespace FlowCli.Commands;

// `flow render <script.flow> -o out.wav` — executes the script; the script is
// expected to contain a (writeWav …) call that writes the WAV file.
//
// Charitable-interpretation tradeoff (project memory, RESEARCH.md SPEC-3
// Background): Phase 30 does NOT auto-inject the writeWav target — composers'
// .flow sources already encode their preferred output paths. We honour what
// the script does, then emit a stderr warning if --output doesn't match the
// actual emitted path so the user knows the CLI flag was effectively ignored.
// Real auto-injection is deferred to v1.5+ ROADMAP work.
internal static class RenderCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var outputOpt = new Option<FileInfo>("--output", "-o")
        {
            Description = "Expected WAV output path (must match what the script writes)",
            Required = true,
        };
        var deviceOpt = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Diagnostic output" };

        var cmd = new Command("render", "Render a Flow script to a WAV file");
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
                    "For Phase 30, the .flow source must contain (writeWav \"...\") — " +
                    "auto-injection deferred (ROADMAP v1.5+).");
                Console.ResetColor();
            }

            return exit;
        });
        return cmd;
    }
}
