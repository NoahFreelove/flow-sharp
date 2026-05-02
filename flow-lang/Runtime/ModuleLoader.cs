using FlowLang.Diagnostics;

namespace FlowLang.Runtime;

/// <summary>
/// Result of a module load operation.
/// </summary>
public enum ModuleLoadResult
{
    Loaded,
    AlreadyLoaded,
    Error
}

/// <summary>
/// Handles loading and executing imported modules.
/// </summary>
public class ModuleLoader
{
    private readonly ErrorReporter _errorReporter;
    private readonly TextWriter? _diagnosticOutput;
    private readonly HashSet<string> _loadedModules = new();
    private readonly HashSet<string> _currentlyLoading = new();

    public Interpreter.Interpreter? ParentInterpreter { get; set; }

    public ModuleLoader(ErrorReporter errorReporter, TextWriter? diagnosticOutput = null)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _diagnosticOutput = diagnosticOutput;
    }

    /// <summary>
    /// Loads a module from the given path.
    /// Returns Loaded on success, AlreadyLoaded if previously imported, Error on failure.
    /// </summary>
    public ModuleLoadResult LoadModule(string path, string currentFile, ExecutionContext context, Core.SourceLocation? importLocation = null)
    {
        var resolvedPath = ResolvePath(path, currentFile);
        var errorLocation = importLocation ?? Core.SourceLocation.Unknown;

        if (_loadedModules.Contains(resolvedPath))
            return ModuleLoadResult.AlreadyLoaded;

        if (_currentlyLoading.Contains(resolvedPath))
        {
            _errorReporter.ReportError($"Circular import detected: {resolvedPath}", errorLocation);
            return ModuleLoadResult.Error;
        }

        _currentlyLoading.Add(resolvedPath);

        try
        {
            // 1. Check file exists
            if (!File.Exists(resolvedPath))
            {
                _diagnosticOutput?.WriteLine($"[verbose] Failed to load module: {resolvedPath} - file not found");
                _errorReporter.ReportError($"Import file not found: {resolvedPath}", errorLocation);
                return ModuleLoadResult.Error;
            }

            // 2. Read file contents
            var source = File.ReadAllText(resolvedPath);

            // Phase 21 D-06: each imported file gets its OWN PragmaSet computed
            // from THIS file's source. Pragmas declared inside the module do NOT
            // leak into the importer's parse session — PRAG-02 isolation is
            // enforced structurally by lexical scoping (pragmaSet is a local
            // variable, only passed to the lexer + parser of THIS module).
            var localReporter = new Diagnostics.ErrorReporter();
            var (pragmaSet, transformedSource) =
                Lexing.PragmaScanner.Scan(source, resolvedPath, localReporter);
            if (localReporter.HasErrors)
            {
                _diagnosticOutput?.WriteLine($"[verbose] Failed to pre-scan module: {resolvedPath}");
                _errorReporter.ReportError(
                    $"Module '{resolvedPath}' has invalid pragma declarations.", errorLocation);
                return ModuleLoadResult.Error;
            }

            // 3. Lex and parse with the module's own pragmaSet + isolated reporter.
            var lexer = new Lexing.SimpleLexer(transformedSource, localReporter, resolvedPath, pragmaSet);
            var tokens = lexer.Tokenize();

            if (localReporter.HasErrors)
            {
                _diagnosticOutput?.WriteLine($"[verbose] Failed to lex module: {resolvedPath}");
                _errorReporter.ReportError($"Module '{resolvedPath}' failed to parse due to syntax errors.", errorLocation);
                return ModuleLoadResult.Error;
            }

            var parser = new Parsing.Parser(tokens, localReporter, pragmaSet);
            var program = parser.Parse();

            if (localReporter.HasErrors)
            {
                _diagnosticOutput?.WriteLine($"[verbose] Failed to parse module: {resolvedPath}");
                _errorReporter.ReportError($"Module '{resolvedPath}' contains structural syntax errors and cannot be imported.", errorLocation);
                return ModuleLoadResult.Error;
            }

            // 4. Execute in current context (no new frame - imports add to current scope)
            var interpreter = ParentInterpreter ?? new Interpreter.Interpreter(context, _errorReporter, this);
            interpreter.Execute(program);

            _loadedModules.Add(resolvedPath);
            if (_errorReporter.HasErrors)
            {
                _diagnosticOutput?.WriteLine($"[verbose] Failed to load module: {resolvedPath} - errors during execution");
                return ModuleLoadResult.Error;
            }
            _diagnosticOutput?.WriteLine($"[verbose] Loaded module: {resolvedPath}");
            return ModuleLoadResult.Loaded;
        }
        catch (Exception ex)
        {
            _diagnosticOutput?.WriteLine($"[verbose] Failed to load module: {resolvedPath} - {ex.Message}");
            _errorReporter.ReportError($"Error loading module {resolvedPath}: {ex.Message}", errorLocation);
            return ModuleLoadResult.Error;
        }
        finally
        {
            _currentlyLoading.Remove(resolvedPath);
        }
    }

    /// <summary>
    /// Resolve a stdlib module name ("@std" / "@audio" / bare "std" / "audio.flow") to an absolute file path.
    /// Mirrors the `@`-prefix branch of the private <see cref="ResolvePath"/>; exposed so the LSP
    /// (flow-lsp) and future callers can share one resolver. Phase 17 (17-05).
    /// </summary>
    public static string ResolveStdlibPath(string moduleName)
    {
        var libraryName = moduleName.StartsWith("@") ? moduleName.Substring(1) : moduleName;
        if (!libraryName.EndsWith(".flow")) libraryName += ".flow";
        var assemblyDir = Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(assemblyDir, libraryName));
    }

    private string ResolvePath(string path, string? currentFile)
    {
        // Handle internal library imports (e.g., "@std" or "@std.flow")
        if (path.StartsWith("@"))
        {
            return ResolveStdlibPath(path);
        }

        // If path is absolute, return as-is
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        // If path is relative, resolve relative to current file
        if (currentFile != null)
        {
            var currentDir = Path.GetDirectoryName(currentFile) ?? Environment.CurrentDirectory;
            return Path.GetFullPath(Path.Combine(currentDir, path));
        }

        // Otherwise resolve relative to current directory
        return Path.GetFullPath(path);
    }
}
