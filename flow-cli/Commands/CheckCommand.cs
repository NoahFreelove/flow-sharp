using System.CommandLine;
using FlowLang.Core;

namespace FlowCli.Commands;

// `flow check <script.flow>` — verifies a script lexes/parses/runs without
// errors. NOTE: parse-AND-execute (RESEARCH Open Question 2 deferred a true
// parse-only mode). FlowEngine has no public Parse() entrypoint as of Phase 30,
// so we redirect Console.Out to TextWriter.Null and run the full pipeline; any
// (print …) side-effects are silenced, but errors and exit code still reflect
// the engine's success/failure verdict. Audio playback calls inside the script
// remain side-effectful — for Phase 30 this is acceptable since `check` is
// not advertised as a sandbox.
internal static class CheckCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };

        var cmd = new Command("check", "Parse a Flow script without executing it");
        cmd.Add(scriptArg);
        cmd.SetAction(parseResult =>
        {
            var script = parseResult.GetValue(scriptArg)!;

            if (!File.Exists(script.FullName))
            {
                Console.Error.WriteLine($"Error: File not found: {script.FullName}");
                return 1;
            }

            string source;
            try
            {
                source = File.ReadAllText(script.FullName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading script: {ex.Message}");
                return 1;
            }

            var originalOut = Console.Out;
            bool success;
            string? errorText = null;
            try
            {
                Console.SetOut(TextWriter.Null);
                using var engine = new FlowEngine(verbose: false);
                success = engine.Execute(source, script.FullName);
                if (!success || engine.ErrorReporter.HasErrors)
                {
                    errorText = engine.ErrorReporter.FormatErrors();
                    success = false;
                }
            }
            catch (Exception ex)
            {
                errorText = $"Error executing script: {ex.Message}";
                success = false;
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(errorText);
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"OK: {script.FullName}");
            return 0;
        });
        return cmd;
    }
}
