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
            // No arguments - check if stdin has data
            if (Console.IsInputRedirected)
            {
                return RunFromStdin();
            }

            // No input - start REPL
            var repl = new Repl();
            repl.Run();
            return 0;
        }

        // Handle flags
        var firstArg = args[0];

        if (firstArg == "-e" || firstArg == "--eval")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Error: -e/--eval requires a code string argument");
                PrintUsage();
                return 1;
            }
            return RunFromString(args[1]);
        }

        if (firstArg == "-h" || firstArg == "--help")
        {
            PrintUsage();
            return 0;
        }

        if (firstArg == "--stdin")
        {
            return RunFromStdin();
        }

        // Execute script file
        var scriptPath = firstArg;

        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Error: File '{scriptPath}' not found");
            return 1;
        }

        var runner = new ScriptRunner();
        return runner.RunScript(scriptPath);
    }

    static int RunFromString(string code)
    {
        try
        {
            var engine = new FlowEngine();
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
    }

    static int RunFromStdin()
    {
        try
        {
            var code = Console.In.ReadToEnd();
            var engine = new FlowEngine();
            var success = engine.Execute(code, "<stdin>");

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
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  flow                    Start REPL");
        Console.WriteLine("  flow <file>             Execute a Flow script file");
        Console.WriteLine("  flow -e <code>          Execute Flow code from string");
        Console.WriteLine("  flow --eval <code>      Execute Flow code from string");
        Console.WriteLine("  flow --stdin            Execute Flow code from stdin");
        Console.WriteLine("  echo <code> | flow      Execute Flow code from stdin (pipe)");
        Console.WriteLine("  flow -h, --help         Show this help message");
    }
}
