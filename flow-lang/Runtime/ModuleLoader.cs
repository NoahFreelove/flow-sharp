using FlowLang.Ast.Statements;
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

    /// <summary>
    /// REQ-4 (Plan 30-03): additional search paths for module resolution. Consulted
    /// AFTER the <c>@</c>-prefix stdlib branch and BEFORE the relative-resolution
    /// fallback in <see cref="ResolvePath"/>. Populated by flow-cli's
    /// <c>FlowConfigLoader.LoadFromXdg()</c> at process startup (via
    /// <see cref="FlowEngine"/> reading <c>FlowConfig.ConfiguredStdlibSearchPaths</c>).
    /// ModuleLoader stays unaware of the config source — paths are externally seeded
    /// so flow-lang has zero new package dependencies.
    /// </summary>
    public List<string> AdditionalSearchPaths { get; } = new();

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

            // Phase 43 Plan 43-03 D-05 / D-06 — ModuleRegistry registration hook.
            // Runs ONCE per resolvedPath because the _loadedModules.Contains short-circuit
            // at line 53 prevents a second load (Pitfall 7). Walks program.Statements
            // looking for the leading ModuleDeclarationStatement (RESEARCH A2: preferred
            // over snapshot-and-diff because Flow has no dynamic proc declarations).
            // If found:
            //   - Iterates remaining ProcDeclarations to build the exportedProcs dict
            //     (proc.Name → Value.Function looked up from the global frame).
            //   - Emits D-06 one-shot dup-module advisory if Contains() is already true.
            //   - Emits D-04 last-import-wins shadow advisory at the proc-shadow site
            //     (per-proc-name; for procs already owned by a DIFFERENT prior module).
            //   - Registers the module name → exportedProcs into context.ModuleRegistry.
            // Files WITHOUT a `module` declaration register nothing and inherit the
            // back-compat path (procs are already in context.GlobalFrame from Execute).
            if (program.Statements.Count > 0
                && program.Statements[0] is ModuleDeclarationStatement modDecl)
            {
                var exportedProcs = new Dictionary<string, Value>();
                foreach (var stmt in program.Statements)
                {
                    if (stmt is Ast.Statements.ProcDeclaration proc)
                    {
                        var overloads = context.GlobalFrame.GetFunctionOverloads(proc.Name);
                        if (overloads.Count > 0)
                        {
                            // Last-declared overload wins for the qualified-access surface.
                            // OverloadResolver handles multi-overload resolution downstream when
                            // the registry returns a Function Value the caller wraps in a call.
                            exportedProcs[proc.Name] = Value.Function(overloads[overloads.Count - 1]);
                        }
                    }
                }

                // D-06 advisory: duplicate-module name detected BEFORE re-registering.
                // Per-name dedup (NOT per-name-and-path) so hot-reload re-registration
                // of the same module name doesn't flood stderr.
                if (context.ModuleRegistry.Contains(modDecl.Name))
                {
                    RenderingDiagnostics.WarnOnce(
                        $"module-dup:{modDecl.Name}",
                        $"[module] duplicate module name '{modDecl.Name}' — last load wins");
                }

                // D-04 last-import-wins shadow advisory — for each proc this module exports
                // that was previously owned by a DIFFERENT module, emit one-shot advisory
                // keyed by (priorOwner, newOwner, procName). Update ownership last-write-wins
                // so the next-import's shadow check sees this module as the prior owner.
                foreach (var procName in exportedProcs.Keys)
                {
                    if (context.ProcOwnership.TryGetValue(procName, out var priorOwner)
                        && priorOwner != modDecl.Name)
                    {
                        RenderingDiagnostics.WarnOnce(
                            $"module-shadow:{priorOwner}:{modDecl.Name}:{procName}",
                            $"[module] '{procName}' from '{modDecl.Name}' shadows '{procName}' from '{priorOwner}' — qualify with '{priorOwner}.{procName}' or '{modDecl.Name}.{procName}' to disambiguate");
                    }
                    context.ProcOwnership[procName] = modDecl.Name;
                }

                context.ModuleRegistry.Register(modDecl.Name, exportedProcs);
            }

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
        // Use AppContext.BaseDirectory rather than Assembly.Location: under
        // single-file publish (Phase 30 REQ-2), Assembly.Location returns "" and
        // resolution falls back to the user's cwd, which breaks 'use "@audio"'
        // when 'flow' is invoked from a non-publish directory. AppContext.BaseDirectory
        // always returns the directory of the executing binary (or the
        // extraction directory for self-extracting single-file apps), so the
        // stdlib .flow files copied alongside the binary via
        // CopyToPublishDirectory=PreserveNewest are found correctly.
        var assemblyDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(assemblyDir))
        {
            // Defensive fallback — AppContext.BaseDirectory is documented to
            // always be non-empty for a managed app, but keep the same
            // last-resort behavior as before in case a host scenario differs.
            assemblyDir = Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location) ?? Environment.CurrentDirectory;
        }
        return Path.GetFullPath(Path.Combine(assemblyDir, libraryName));
    }

    private string ResolvePath(string path, string? currentFile)
    {
        // Handle internal library imports (e.g., "@std" or "@std.flow")
        if (path.StartsWith("@"))
        {
            return ResolveStdlibPath(path);
        }

        // REQ-4 stdlib_search_path: try each configured path before falling back to
        // relative resolution. Lets composer-installed custom modules live outside
        // the bundled stdlib directory. Paths are externally seeded so ModuleLoader
        // does NOT import FlowConfig (one-way dependency: flow-cli -> FlowEngine ->
        // ModuleLoader). Empty list by default => zero-cost no-op for existing scripts.
        foreach (var searchPath in AdditionalSearchPaths)
        {
            var candidate = Path.GetFullPath(
                Path.Combine(searchPath, path.EndsWith(".flow") ? path : path + ".flow"));
            if (File.Exists(candidate)) return candidate;
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
