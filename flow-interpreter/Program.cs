using System.Reflection;
using FlowLang.Core;
using FlowLang.Runtime;

namespace FlowInterpreter;

class Program
{
    static int Main(string[] args)
    {
        // Load ~/.config/flow/config.toml into FlowConfig.Active BEFORE any
        // FlowEngine is constructed. FlowEngine reads
        // FlowConfig.ConfiguredStdlibSearchPaths at ModuleLoader-seed time and
        // loadSfz reads FlowConfig.Active.SfzRoot, so the config must be active
        // by the time any engine is built (REPL / ScriptRunner / LiveReloadManager).
        // Missing file: silent fallback. Malformed: charitable warn + continue.
        // Mirrors flow-cli/Program.cs:16 so `dotnet run --project flow-interpreter`
        // honors the same config as the `flow` CLI binary.
        FlowConfigLoader.LoadFromXdg();

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
                return RunFromStdin(flags.DeviceName, flags.Verbose);
            }

            // No input - start REPL. Emit the banner ONLY here (interactive
            // session) and on STDERR so -e/--eval/--stdin stdout stays clean for
            // tools capturing program output. The version is read from the real
            // assembly metadata (no stale hardcoded literal).
            Console.Error.WriteLine($"Flow Language Interpreter v{ResolveVersion()}");
            Console.Error.WriteLine();
            var repl = new Repl();
            repl.Run();
            return 0;
        }

        if (flags.EvalCode != null)
        {
            return RunFromString(flags.EvalCode, flags.DeviceName, flags.Verbose);
        }

        if (flags.ReadStdin)
        {
            return RunFromStdin(flags.DeviceName, flags.Verbose);
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
                return RunWithWatch(flags.ScriptPath, flags.DeviceName, flags.Verbose);
            }

            var runner = new ScriptRunner();
            return runner.RunScript(flags.ScriptPath, flags.DeviceName, flags.Verbose);
        }

        PrintUsage();
        return 1;
    }

    static int RunFromString(string code, string? deviceName, bool verbose = false)
    {
        try
        {
            using var engine = new FlowEngine(verbose: verbose);
            ConfigureDevice(engine, deviceName);
            var success = engine.Execute(code, "<eval>");

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(FormatErrorsForEmit(engine));
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

    static int RunFromStdin(string? deviceName, bool verbose = false)
    {
        try
        {
            var code = Console.In.ReadToEnd();
            using var engine = new FlowEngine(verbose: verbose);
            ConfigureDevice(engine, deviceName);
            var success = engine.Execute(code, "<stdin>");

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(FormatErrorsForEmit(engine));
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
    /// Phase 35 LANG-04 Wave 2a — picks the rich Rust-style format
    /// (<see cref="FlowLang.Diagnostics.ErrorReporter.FormatDiagnostics"/>)
    /// when the engine has accumulated any <see cref="FlowLang.Diagnostics.FlowDiagnostic"/>,
    /// falling back to the legacy single-line <see cref="FlowLang.Diagnostics.ErrorReporter.FormatErrors"/>
    /// otherwise. Concatenates both when present so emit sites mid-Span-migration
    /// don't drop legacy FlowError accumulator output.
    ///
    /// <para>
    /// Color is emitted unconditionally (the wrapping
    /// <c>Console.ForegroundColor = Red</c> remains the existing precedent);
    /// .NET auto-suppresses ANSI when stderr is redirected. The
    /// <c>useColor:false</c> path is reserved for the golden-file tests.
    /// </para>
    /// </summary>
    internal static string FormatErrorsForEmit(FlowEngine engine)
    {
        var hasRich = engine.ErrorReporter.HasDiagnostics;
        var hasLegacy = engine.ErrorReporter.Errors.Count > 0;
        if (hasRich && hasLegacy)
        {
            return engine.ErrorReporter.FormatDiagnostics(engine.SourceMap, useColor: true)
                + "\n\n"
                + engine.ErrorReporter.FormatErrors();
        }
        if (hasRich)
            return engine.ErrorReporter.FormatDiagnostics(engine.SourceMap, useColor: true);
        return engine.ErrorReporter.FormatErrors();
    }

    /// <summary>
    /// Runs a script with file watching and live-coding support.
    /// Delegates to LiveReloadManager for streaming playback with bar-boundary buffer swapping.
    /// </summary>
    static int RunWithWatch(string filePath, string? deviceName, bool verbose = false)
    {
        var fullPath = Path.GetFullPath(filePath);
        using var manager = new LiveReloadManager(fullPath, deviceName);
        manager.Run();
        return 0;
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
        bool verbose = false;

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

                case "--verbose" or "-v":
                    verbose = true;
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

        return new CliFlags(scriptPath, evalCode, deviceName, watch, showHelp, readStdin, verbose);
    }

    /// <summary>
    /// Resolves the interpreter version for the interactive REPL banner. Reads
    /// the assembly's <see cref="AssemblyInformationalVersionAttribute"/> (set
    /// via the build), stripping any SDK-appended <c>+&lt;commit&gt;</c> suffix,
    /// then falls back to the AssemblyName.Version and finally "unknown".
    /// Mirrors flow-cli/Commands/VersionCommand.cs so the two binaries report
    /// the same version surface.
    /// </summary>
    static string ResolveVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString() ?? "unknown";
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
        Console.WriteLine("  --verbose, -v   Show diagnostic output (module loads, resolution failures)");
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
    bool ReadStdin,
    bool Verbose
);
