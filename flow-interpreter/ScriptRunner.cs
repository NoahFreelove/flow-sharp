using FlowLang.Core;

namespace FlowInterpreter;

/// <summary>
/// Executes Flow scripts from files.
/// </summary>
public class ScriptRunner
{
    public int RunScript(string filePath)
    {
        try
        {
            var source = File.ReadAllText(filePath);
            var engine = new FlowEngine();

            var success = engine.Execute(source, filePath);

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
            Console.Error.WriteLine($"Error executing script: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
