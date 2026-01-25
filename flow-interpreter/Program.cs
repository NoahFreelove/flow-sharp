using FlowLang.Core;

namespace FlowInterpreter;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Flow Language Interpreter v0.1");
        Console.WriteLine();

        if (args.Length == 0)
        {
            // No arguments - start REPL
            var repl = new Repl();
            repl.Run();
            return 0;
        }

        // Execute script file
        var scriptPath = args[0];

        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Error: File '{scriptPath}' not found");
            return 1;
        }

        var runner = new ScriptRunner();
        return runner.RunScript(scriptPath);
    }
}
