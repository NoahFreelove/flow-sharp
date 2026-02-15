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
    private readonly HashSet<string> _loadedModules = new();
    private readonly HashSet<string> _currentlyLoading = new();

    public ModuleLoader(ErrorReporter errorReporter)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
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
                _errorReporter.ReportError($"Import file not found: {resolvedPath}", errorLocation);
                return ModuleLoadResult.Error;
            }

            // 2. Read file contents
            var source = File.ReadAllText(resolvedPath);

            // 3. Lex and parse
            var lexer = new Lexing.SimpleLexer(source, _errorReporter, resolvedPath);
            var tokens = lexer.Tokenize();

            if (_errorReporter.HasErrors)
                return ModuleLoadResult.Error;

            var parser = new Parsing.Parser(tokens, _errorReporter);
            var program = parser.Parse();

            if (_errorReporter.HasErrors)
                return ModuleLoadResult.Error;

            // 4. Execute in current context (no new frame - imports add to current scope)
            var interpreter = new Interpreter.Interpreter(context, _errorReporter);
            interpreter.Execute(program);

            _loadedModules.Add(resolvedPath);
            return _errorReporter.HasErrors ? ModuleLoadResult.Error : ModuleLoadResult.Loaded;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError($"Error loading module {resolvedPath}: {ex.Message}", errorLocation);
            return ModuleLoadResult.Error;
        }
        finally
        {
            _currentlyLoading.Remove(resolvedPath);
        }
    }

    private string ResolvePath(string path, string? currentFile)
    {
        // Handle internal library imports (e.g., "@std" or "@std.flow")
        if (path.StartsWith("@"))
        {
            var libraryName = path.Substring(1); // Remove '@' prefix

            // Add .flow extension if not present
            if (!libraryName.EndsWith(".flow"))
                libraryName += ".flow";

            // Resolve to the standard library directory (same directory as the executing assembly)
            var assemblyDir = Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location) ?? Environment.CurrentDirectory;
            return Path.GetFullPath(Path.Combine(assemblyDir, libraryName));
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
