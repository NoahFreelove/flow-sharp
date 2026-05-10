using FlowLang.Core;

namespace FlowInterpreter;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Flow Language Interpreter v0.1");
        Console.WriteLine();

        // Parse flags from args
        var flags = ParseFlags(args);

        if (flags.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (flags.ScriptPath == null && flags.EvalCode == null && !flags.ReadStdin)
        {
            // No arguments - check if stdin has data
            if (Console.IsInputRedirected)
            {
                return RunFromStdin(flags.DeviceName);
            }

            // No input - start REPL
            var repl = new Repl();
            repl.Run();
            return 0;
        }

        if (flags.EvalCode != null)
        {
            return RunFromString(flags.EvalCode, flags.DeviceName);
        }

        if (flags.ReadStdin)
        {
            return RunFromStdin(flags.DeviceName);
        }

        // Execute script file
        if (flags.ScriptPath != null)
        {
            if (!File.Exists(flags.ScriptPath))
            {
                Console.Error.WriteLine($"Error: File '{flags.ScriptPath}' not found");
                return 1;
            }

            if (flags.Watch)
            {
                return RunWithWatch(flags.ScriptPath, flags.DeviceName);
            }

            var runner = new ScriptRunner();
            return runner.RunScript(flags.ScriptPath, flags.DeviceName);
        }

        PrintUsage();
        return 1;
    }

    static int RunFromString(string code, string? deviceName)
    {
        try
        {
            using var engine = new FlowEngine();
            ConfigureDevice(engine, deviceName);
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

    static int RunFromStdin(string? deviceName)
    {
        try
        {
            var code = Console.In.ReadToEnd();
            using var engine = new FlowEngine();
            ConfigureDevice(engine, deviceName);
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

    /// <summary>
    /// Runs a script with file watching. Re-executes on file changes.
    /// Ctrl+C stops playback and exits cleanly.
    /// </summary>
    static int RunWithWatch(string filePath, string? deviceName)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);

        using var engine = new FlowEngine();
        ConfigureDevice(engine, deviceName);

        // Handle Ctrl+C: stop audio, then exit
        var exitRequested = false;
        Console.CancelKeyPress += (_, e) =>
        {
            if (!exitRequested)
            {
                // First Ctrl+C: stop audio, stay in watch mode
                e.Cancel = true;
                engine.StopAudio();
                Console.WriteLine();
                Console.WriteLine("Audio stopped. Press Ctrl+C again to exit.");
                exitRequested = true;
            }
            // Second Ctrl+C: default behavior (exit)
        };

        // Initial execution
        ExecuteScript(engine, fullPath);

        Console.WriteLine($"Watching {fileName} for changes... (Ctrl+C to stop)");

        // Set up file watcher
        using var watcher = new FileSystemWatcher(directory, fileName);
        watcher.NotifyFilter = NotifyFilters.LastWrite;

        // Debounce: editors may trigger multiple write events
        DateTime lastChange = DateTime.MinValue;
        watcher.Changed += (_, e) =>
        {
            var now = DateTime.Now;
            if ((now - lastChange).TotalMilliseconds < 500)
                return;
            lastChange = now;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Change detected in {fileName}, re-executing...");
            Console.ResetColor();

            // Stop any current playback before re-executing
            engine.StopAudio();

            // Small delay to ensure file write is complete
            Thread.Sleep(100);

            ExecuteScript(engine, fullPath);
        };

        watcher.EnableRaisingEvents = true;

        // Block until exit is requested
        while (!exitRequested)
        {
            Thread.Sleep(200);
        }

        engine.StopAudio();
        Console.WriteLine("Watch mode ended.");
        return 0;
    }

    /// <summary>
    /// Executes a script file with error reporting.
    /// </summary>
    static void ExecuteScript(FlowEngine engine, string filePath)
    {
        try
        {
            var source = File.ReadAllText(filePath);
            var success = engine.Execute(source, filePath);

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(engine.ErrorReporter.FormatErrors());
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Configures the audio device on the engine if a device name was specified.
    /// </summary>
    static void ConfigureDevice(FlowEngine engine, string? deviceName)
    {
        if (deviceName == null) return;

        if (engine.AudioManager.IsAudioAvailable())
        {
            var backend = engine.AudioManager.GetBackend();
            if (!backend.SetDevice(deviceName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"Warning: Could not set audio device '{deviceName}'");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Parses CLI flags into a structured record.
    /// </summary>
    static CliFlags ParseFlags(string[] args)
    {
        string? scriptPath = null;
        string? evalCode = null;
        string? deviceName = null;
        bool watch = false;
        bool showHelp = false;
        bool readStdin = false;

        int i = 0;
        while (i < args.Length)
        {
            switch (args[i])
            {
                case "-e" or "--eval":
                    if (i + 1 < args.Length)
                    {
                        evalCode = args[i + 1];
                        i += 2;
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: -e/--eval requires a code string argument");
                        showHelp = true;
                        i++;
                    }
                    break;

                case "-h" or "--help":
                    showHelp = true;
                    i++;
                    break;

                case "--stdin":
                    readStdin = true;
                    i++;
                    break;

                case "--watch" or "-w":
                    watch = true;
                    i++;
                    break;

                case "--device":
                    if (i + 1 < args.Length)
                    {
                        deviceName = args[i + 1];
                        i += 2;
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --device requires a device name argument");
                        showHelp = true;
                        i++;
                    }
                    break;

                default:
                    // Assume it's a script path
                    scriptPath ??= args[i];
                    i++;
                    break;
            }
        }

        return new CliFlags(scriptPath, evalCode, deviceName, watch, showHelp, readStdin);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  flow                        Start REPL");
        Console.WriteLine("  flow <file>                 Execute a Flow script file");
        Console.WriteLine("  flow <file> --watch         Execute and re-run on file changes");
        Console.WriteLine("  flow <file> --device <name> Set audio output device");
        Console.WriteLine("  flow -e <code>              Execute Flow code from string");
        Console.WriteLine("  flow --eval <code>          Execute Flow code from string");
        Console.WriteLine("  flow --stdin                Execute Flow code from stdin");
        Console.WriteLine("  echo <code> | flow          Execute Flow code from stdin (pipe)");
        Console.WriteLine("  flow -h, --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --watch, -w     Watch script file for changes and re-execute");
        Console.WriteLine("  --device <name> Set the audio output device");
    }
}

/// <summary>
/// Parsed CLI flags.
/// </summary>
record CliFlags(
    string? ScriptPath,
    string? EvalCode,
    string? DeviceName,
    bool Watch,
    bool ShowHelp,
    bool ReadStdin
);
