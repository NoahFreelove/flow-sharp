using FlowLang.Core;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowInterpreter;

/// <summary>
/// Read-Eval-Print Loop for interactive Flow execution.
/// Maintains audio backend state across evaluations.
/// Handles Ctrl+C to stop playback without exiting.
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

        // Handle Ctrl+C: stop audio playback, don't exit REPL
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Prevent process exit
            _engine.StopAudio();
            Console.WriteLine();
            Console.Write("> ");
        };

        try
        {
            while (true)
            {
                var input = ReadCompleteInput();

                if (input == null)
                    break; // EOF (e.g., Ctrl+D)

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
        }
        finally
        {
            _engine.Dispose();
        }

        Console.WriteLine("Goodbye!");
    }

    private string? ReadCompleteInput()
    {
        Console.Write("> ");
        var firstLine = Console.ReadLine();

        if (firstLine == null)
            return null; // EOF

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
            ":stop" => StopAudio(),
            _ => UnknownCommand(command)
        };
    }

    private bool StopAudio()
    {
        _engine.StopAudio();
        Console.WriteLine("Audio playback stopped.");
        return true;
    }

    private bool ShowHelp()
    {
        Console.WriteLine("Flow REPL Commands:");
        Console.WriteLine("  :quit, :q, :exit  - Exit the REPL");
        Console.WriteLine("  :help, :h         - Show this help");
        Console.WriteLine("  :clear, :cls      - Clear the screen");
        Console.WriteLine("  :stop             - Stop audio playback");
        Console.WriteLine();
        Console.WriteLine("Audio Playback:");
        Console.WriteLine("  Ctrl+C            - Stop current audio playback");
        Console.WriteLine("  (play buffer)     - Play an audio buffer");
        Console.WriteLine("  (loop buffer)     - Loop audio (Ctrl+C to stop)");
        Console.WriteLine("  (preview buffer)  - Quick low-quality preview");
        Console.WriteLine("  (stop)            - Stop playback from code");
        Console.WriteLine();
        Console.WriteLine("Multi-line Input:");
        Console.WriteLine("  Method 1: End a line with \\ to continue on the next line");
        Console.WriteLine("            The prompt changes to '...' for continuation");
        Console.WriteLine("  Method 2: Starting with 'proc' automatically enables multi-line mode");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  > use \"@std\"");
        Console.WriteLine("  > use \"@audio\"");
        Console.WriteLine("  > Buffer t = (createSineTone 0.5 440.0 0.3)");
        Console.WriteLine("  > t -> play");
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
