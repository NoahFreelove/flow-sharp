using System.Linq;
using FlowLang.Core;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowInterpreter;

/// <summary>
/// Read-Eval-Print Loop for interactive Flow execution.
/// Maintains audio backend state across evaluations.
/// Handles Ctrl+C to stop playback without exiting.
///
/// Phase 38 Plan 38-04 (REPL-01..04): wires <see cref="ReplLineEditor"/> for
/// PrettyPrompt-backed line editing (Tab completion via in-process flow-lsp
/// CompletionHandler per D-38-12; Ctrl+R history search via PrettyPrompt
/// built-in; multi-line continuation preserved through
/// <see cref="ReplInputCompleteness"/>). Extends <see cref="HandleCommand"/>
/// with <c>:help &lt;name&gt;</c> per D-38-09.
/// </summary>
public class Repl
{
    private readonly FlowEngine _engine;
    private ReplLineEditor? _lineEditor;

    /// <summary>
    /// Phase 44 Plan 44-10 D-16 — sticky REPL strict-mode session flag.
    /// Mirrors the Phase 32 D-08 tuning-pragma persistence pattern at REPL
    /// scope: once the composer flips strict ON (via <c>:strict on</c> meta-
    /// command OR by typing <c>enable strict;</c> at the prompt), the bit
    /// persists across subsequent <see cref="FlowEngine.Execute"/> calls
    /// even though each call's <c>ApplyStrictPragma</c> would otherwise
    /// reset <c>engine.Context.StrictMode</c> to false (Plan 44-01 unconditional-
    /// overwrite design — necessary for fresh script-mode runs, defeated here
    /// for REPL stickiness by the per-line sync sandwich in
    /// <see cref="ExecuteUserLine"/>).
    /// </summary>
    private bool _sessionStrict = false;

    public Repl()
    {
        _engine = new FlowEngine();
    }

    public void Run()
    {
        Console.WriteLine("Flow REPL - Type ':quit' to exit, ':help' for help");
        Console.WriteLine("Multi-line input: end a line with \\ to continue on next line");
        Console.WriteLine();

        // Auto-import standard modules for REPL convenience
        // Script mode requires explicit imports for reproducibility
        AutoImportStandardModules();

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

                // Execute input and get result.
                //
                // Phase 44 Plan 44-10 D-16 — sticky-strict via pragma injection:
                //
                // Plan 44-01's ApplyStrictPragma is an UNCONDITIONAL overwrite
                // between parse and interpret (so script-mode files without
                // `enable strict;` always run charitable). Setting
                // engine.Context.StrictMode=true BEFORE Execute is therefore
                // defeated by the overwrite. To honour the D-16 sticky-session
                // contract WITHOUT touching the Plan 44-01 design lock, we
                // inject `enable strict;` at the front of the input line when
                // the session is sticky-strict — the per-line PragmaScanner
                // observes it and ApplyStrictPragma flips StrictMode=true as
                // a natural consequence.
                //
                // Symmetric direction (typing `enable strict;` flips the
                // sticky flag too): after Execute returns, copy the per-line
                // post-ApplyStrictPragma context.StrictMode back into
                // _sessionStrict so the next line inherits it. This is the
                // RESEARCH §Pattern 8 sticky-from-pragma sync requirement.
                var lineToExecute = _sessionStrict ? "enable strict;\n" + input : input;
                var result = _engine.ExecuteScriptAndGetResult(lineToExecute, "<repl>");
                if (_engine.Context.StrictMode != _sessionStrict)
                    _sessionStrict = _engine.Context.StrictMode;

                if (_engine.ErrorReporter.HasErrors)
                {
                    // Phase 35 LANG-04 Wave 2a: picks rich Rust-style format
                    // when the engine has accumulated any FlowDiagnostic; the
                    // REPL `<repl>` sentinel source registered by
                    // FlowEngine.Execute lets the renderer quote the offending
                    // line back to the composer.
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(Program.FormatErrorsForEmit(_engine));
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

    private void AutoImportStandardModules()
    {
        var imports = new[]
        {
            "use \"@std\"",
            "use \"@audio\"",
            "use \"@collections\""
        };

        foreach (var import in imports)
        {
            _engine.Execute(import, "<repl-init>");
            // Clear any errors from auto-import (e.g., if audio not available)
            _engine.ErrorReporter.Clear();
        }
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
        return !IsInputComplete(line);
    }

    private bool IsInputComplete(string input)
    {
        // Phase 38 Plan 38-04: delegate to the shared helper so the legacy
        // Console.ReadLine path AND the PrettyPrompt path call the same logic.
        return ReplInputCompleteness.IsInputComplete(input);
    }

    private bool HandleCommand(string command)
    {
        // Phase 38 Plan 38-04 (D-38-09): `:help <name>` extension — look up the
        // identifier in BuiltInDocs and render header + body + Example per UI-SPEC
        // lines 263-280. The bare-:help arms below remain unchanged.
        var trimmed = command.TrimEnd();
        if (trimmed.StartsWith(":help ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(":h ", StringComparison.OrdinalIgnoreCase))
        {
            int spaceIdx = trimmed.IndexOf(' ');
            var name = trimmed.Substring(spaceIdx + 1).Trim();
            if (!string.IsNullOrEmpty(name))
            {
                return ShowHelpForName(name);
            }
        }

        return command.ToLower() switch
        {
            ":quit" or ":q" or ":exit" => false,
            ":help" or ":h" => ShowHelp(),
            ":clear" or ":cls" => ClearScreen(),
            ":stop" => StopAudio(),
            // Phase 44 Plan 44-10 D-16 — REPL sticky-strict meta-commands.
            // Mirror the `:help` / `:quit` / `:clear` / `:stop` family.
            ":strict on" => SetStrict(true),
            ":strict off" => SetStrict(false),
            _ => UnknownCommand(command)
        };
    }

    /// <summary>
    /// Phase 44 Plan 44-10 D-16 — flips the sticky <see cref="_sessionStrict"/>
    /// session flag AND mutates <c>_engine.Context.StrictMode</c> immediately
    /// (no wait for next Execute). Prints <c>[strict] on</c> / <c>[strict] off</c>
    /// to stdout per ReplStrictMetaCommandTests Fact 7. Returns <c>true</c> to
    /// keep the REPL alive (matches the meta-command-family convention).
    /// </summary>
    private bool SetStrict(bool on)
    {
        _sessionStrict = on;
        _engine.Context.StrictMode = on;
        Console.WriteLine($"[strict] {(on ? "on" : "off")}");
        return true;
    }

    /// <summary>
    /// Test seam — exposes <see cref="HandleCommand"/> to xUnit per Phase 38 Plan 38-04
    /// ReplHelpMetaCommandTests. Production callers go through <see cref="Run"/>.
    /// Returns the same bool the dispatch returns: <c>true</c> = continue REPL,
    /// <c>false</c> = exit (matches the `:quit` arm contract).
    /// </summary>
    public bool HandleCommandForTesting(string command) => HandleCommand(command);

    /// <summary>
    /// Phase 44 Plan 44-10 D-16 test seam — runs a single non-meta REPL line
    /// through the same sticky-strict sync sandwich the production
    /// <see cref="Run"/> loop uses (pragma-injection BEFORE Execute when
    /// <c>_sessionStrict==true</c>, plus the symmetric sticky-from-pragma
    /// sync AFTER Execute). Mirrors the
    /// <see cref="HandleCommandForTesting"/> pattern: xUnit cannot drive
    /// the interactive Console.ReadLine loop deterministically, so the test
    /// seam invokes the per-line contract directly. Returns the underlying
    /// <see cref="FlowEngine"/>'s post-Execute <see cref="ErrorReporter"/>
    /// success bit (<c>true</c> = no errors).
    /// </summary>
    public bool ExecuteLineForTesting(string input)
    {
        var lineToExecute = _sessionStrict ? "enable strict;\n" + input : input;
        _engine.Execute(lineToExecute, "<repl>");
        if (_engine.Context.StrictMode != _sessionStrict)
            _sessionStrict = _engine.Context.StrictMode;
        return !_engine.ErrorReporter.HasErrors;
    }

    /// <summary>
    /// Phase 38 Plan 38-04 D-38-09 — `:help &lt;name&gt;` arm. Looks up <paramref name="name"/>
    /// in <see cref="BuiltInDocs"/>; on hit renders the 3-block layout per UI-SPEC
    /// lines 263-280 (bold+green header, dim signature, default body, dim Example);
    /// on miss emits the locked yellow advisory per line 289.
    /// Mirrors the HoverHandler.BuildHover consumer pattern at flow-lsp:46-65.
    /// </summary>
    private bool ShowHelpForName(string name)
    {
        var entry = BuiltInDocs.TryGet(name);
        if (entry is null)
        {
            // UI-SPEC line 289: locked yellow advisory wording (composer-interactive).
            var prevFg = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[help] no documentation entry for '{name}' — try ':help' for the meta-command list");
            }
            finally
            {
                Console.ForegroundColor = prevFg;
            }
            return true;
        }

        // Header: proc-name bold + green per UI-SPEC line 268+281
        var prev = Console.ForegroundColor;
        try
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            // ANSI bold escape — terminals that support both colour and SGR honour the bold.
            Console.WriteLine($"\x1b[1m{name}\x1b[0m");
            Console.ForegroundColor = prev;

            // Signature line — dim per UI-SPEC line 269+282. Params come from BuiltInDocs entry.
            Console.WriteLine();
            var sigDisplay = entry.Params.Count > 0
                ? $"({name} {string.Join(' ', entry.Params.Select(p => p.Name))})"
                : $"({name})";
            Console.WriteLine($"  \x1b[2m{sigDisplay}\x1b[0m");

            // Body — default attribute per UI-SPEC line 272+283.
            Console.WriteLine();
            Console.WriteLine($"  {entry.Summary}");

            // Per-param lines (if any) — composer-useful detail.
            foreach (var p in entry.Params)
            {
                if (!string.IsNullOrEmpty(p.Description))
                    Console.WriteLine($"    \x1b[2m{p.Name}\x1b[0m: {p.Description}");
            }

            // Example label — dim per UI-SPEC line 276.
            Console.WriteLine();
            Console.WriteLine($"  \x1b[2mExample:\x1b[0m");
            // BuiltInDocs.Doc has no Example field today — render a generic one-liner
            // sourced from the param names per UI-SPEC line 277 example pattern.
            var exampleArgs = entry.Params.Count > 0
                ? string.Join(' ', entry.Params.Select(p => p.Name))
                : string.Empty;
            Console.WriteLine($"    ({name}{(exampleArgs.Length > 0 ? " " + exampleArgs : string.Empty)})");
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
        return true;
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
        Console.WriteLine("  :help <name>      - Show docs for a builtin (e.g. ':help transpose')"); // Phase 38 Plan 38-04 D-38-09 + UI-SPEC line 362
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
