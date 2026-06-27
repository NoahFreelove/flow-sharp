using FlowLang.Core;

namespace FlowInterpreter;

/// <summary>
/// Executes Flow scripts from files.
/// </summary>
public class ScriptRunner
{
    public int RunScript(string filePath, string? deviceName = null, bool verbose = false)
    {
        try
        {
            var source = File.ReadAllText(filePath);
            using var engine = new FlowEngine(verbose: verbose);

            // Configure audio device if specified
            if (deviceName != null && engine.AudioManager.IsAudioAvailable())
            {
                var backend = engine.AudioManager.GetBackend();
                backend.SetDevice(deviceName);
            }

            var success = engine.Execute(source, filePath);

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                // Phase 35 LANG-04 Wave 2a: picks rich Rust-style format when
                // the engine has accumulated any FlowDiagnostic, otherwise
                // falls back to the legacy FormatErrors output.
                Console.Error.WriteLine(Program.FormatErrorsForEmit(engine));
                Console.ResetColor();
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error executing script: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
