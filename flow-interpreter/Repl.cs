using FlowLang.Core;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowInterpreter;

/// <summary>
/// Read-Eval-Print Loop for interactive Flow execution.
/// </summary>
public class Repl
{
    private readonly FlowEngine _engine;

    public Repl()
    {
        _engine = new FlowEngine();
    }

    public void Run()
    {
        Console.WriteLine("Flow REPL - Type ':quit' to exit, ':help' for help");
        Console.WriteLine("Multi-line input: end a line with \\ to continue on next line");
        Console.WriteLine();

        while (true)
        {
            var input = ReadCompleteInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Handle special commands
            if (input.StartsWith(':'))
            {
                if (!HandleCommand(input))
                    break;
                continue;
            }

            // Execute input and get result
            var result = _engine.ExecuteExpression(input, "<repl>");

            if (_engine.ErrorReporter.HasErrors)
            {
                // Print errors
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(_engine.ErrorReporter.FormatErrors());
                Console.ResetColor();
            }
            else if (result != null && result.Type is not VoidType)
            {
                // Print the result (if it's not Void)
                Console.WriteLine(result.ToString());
            }
        }

        Console.WriteLine("Goodbye!");
    }

    private string ReadCompleteInput()
    {
        Console.Write("> ");
        var firstLine = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(firstLine))
            return string.Empty;

        // Check if line ends with backslash continuation
        if (firstLine.TrimEnd().EndsWith("\\"))
        {
            return ReadBackslashContinuation(firstLine);
        }

        // Check if this might be a multi-line statement
        var trimmed = firstLine.TrimStart();
        if (!NeedsMoreLines(trimmed))
            return firstLine;

        // Collect multiple lines
        var lines = new List<string> { firstLine };

        while (true)
        {
            Console.Write("... ");
            var line = Console.ReadLine();

            if (line == null)
                break;

            lines.Add(line);

            // Check if input is complete
            var combined = string.Join("\n", lines);
            if (IsInputComplete(combined))
                break;
        }

        return string.Join("\n", lines);
    }

    private string ReadBackslashContinuation(string firstLine)
    {
        var lines = new List<string>();
        var currentLine = firstLine;

        while (currentLine.TrimEnd().EndsWith("\\"))
        {
            // Remove the trailing backslash
            lines.Add(currentLine.TrimEnd().TrimEnd('\\'));

            // Read next line with continuation prompt
            Console.Write("... ");
            currentLine = Console.ReadLine();

            if (currentLine == null)
                break;
        }

        // Add the final line (without backslash)
        if (currentLine != null)
        {
            lines.Add(currentLine);
        }

        // Join all lines with newline to preserve lexer behavior
        return string.Join("\n", lines);
    }

    private bool NeedsMoreLines(string line)
    {
        // Start multi-line mode for these keywords
        return line.StartsWith("proc ") ||
               line.StartsWith("internal proc");
    }

    private bool IsInputComplete(string input)
    {
        // For internal procs, they don't have bodies
        if (input.TrimStart().StartsWith("internal proc"))
            return true;

        // Count proc declarations vs end markers
        // We need to be smarter about this - count actual tokens, not substrings
        var lines = input.Split('\n');
        int procCount = 0;
        int endCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Count proc declarations (but not "end proc")
            if (trimmed.StartsWith("proc ") && !trimmed.StartsWith("end proc"))
            {
                procCount++;
            }
            else if (trimmed.StartsWith("internal proc"))
            {
                procCount++;
            }

            // Count end markers (both "end proc" and "end" alone)
            if (trimmed == "end" || trimmed == "end proc" || trimmed.StartsWith("end proc "))
            {
                endCount++;
            }
        }

        return procCount <= endCount;
    }

    private bool HandleCommand(string command)
    {
        return command.ToLower() switch
        {
            ":quit" or ":q" or ":exit" => false,
            ":help" or ":h" => ShowHelp(),
            ":clear" or ":cls" => ClearScreen(),
            _ => UnknownCommand(command)
        };
    }

    private bool ShowHelp()
    {
        Console.WriteLine("Flow REPL Commands:");
        Console.WriteLine("  :quit, :q, :exit  - Exit the REPL");
        Console.WriteLine("  :help, :h         - Show this help");
        Console.WriteLine("  :clear, :cls      - Clear the screen");
        Console.WriteLine();
        Console.WriteLine("Multi-line Input:");
        Console.WriteLine("  Method 1: End a line with \\ to continue on the next line");
        Console.WriteLine("            The prompt changes to '...' for continuation");
        Console.WriteLine("  Method 2: Starting with 'proc' automatically enables multi-line mode");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  > use \"@std\"");
        Console.WriteLine("  > Int x = 5");
        Console.WriteLine("  > x");
        Console.WriteLine("  5");
        Console.WriteLine();
        Console.WriteLine("  Backslash continuation:");
        Console.WriteLine("  > (list 1 \\");
        Console.WriteLine("  ...     2 \\");
        Console.WriteLine("  ...     3)");
        Console.WriteLine("  [1, 2, 3]");
        Console.WriteLine();
        Console.WriteLine("  Proc definition:");
        Console.WriteLine("  > proc double (Int: n)");
        Console.WriteLine("  ...     n * 2");
        Console.WriteLine("  ... end proc");
        Console.WriteLine("  > (double 21)");
        Console.WriteLine("  42");
        Console.WriteLine();
        return true;
    }

    private bool ClearScreen()
    {
        Console.Clear();
        Console.WriteLine("Flow REPL - Type ':quit' to exit, ':help' for help");
        Console.WriteLine();
        return true;
    }

    private bool UnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Type ':help' for available commands");
        return true;
    }
}
