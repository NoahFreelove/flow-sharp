using FlowLang.Diagnostics;
using System.CommandLine;
using FlowLang.Analysis;

namespace FlowCli.Commands;

// Static frontend only: checking must never execute a script or its module bodies.
internal static class CheckCommand
{
    public static Command Build()
    {
        var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };

        var cmd = new Command("check", "Check syntax and top-level imports without executing code");
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

            bool success;
            string? errorText = null;
            try
            {
                var syntax = LanguageAnalysis.Parse(source, script.FullName);
                var result = LanguageAnalysis.Analyze(syntax,
                    new FileModuleSourceProvider(script.DirectoryName!));
                success = result.Success;
                if (result.Diagnostics.Count > 0)
                {
                    errorText = string.Join(Environment.NewLine, result.Diagnostics.Select(d =>
                        $"{d.Span.Start}: {d.Level.ToString().ToLowerInvariant()} [{d.Code}]: {d.Message}"));
                }
            }
            catch (Exception ex)
            {
                errorText = $"Error analyzing script: {ex.Message}";
                success = false;
            }

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(errorText);
                Console.ResetColor();
                return 1;
            }

            if (!string.IsNullOrEmpty(errorText)) Console.Error.WriteLine(errorText);
            Console.WriteLine($"OK: {script.FullName} (syntax and imports; runtime behavior not checked)");
            return 0;
        });
        return cmd;
    }
}
