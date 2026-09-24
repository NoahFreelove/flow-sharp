using FlowLang.Core;
using FlowLang.Runtime;

namespace FlowLang.Tests.Fixtures;

public sealed class FlowEngineRunner : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly FlowEngine _engine;

    /// <summary>
    /// Captures the engine's print output and advisories through its session sinks
    /// (no process-console redirection).
    /// </summary>
    public FlowEngineRunner(bool verbose = false)
    {
        _engine = new FlowEngine(new EngineOptions { Output = _stdout, Diagnostics = _stderr, Verbose = verbose });
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunFile(string path)
    {
        var source = File.ReadAllText(path);
        var success = _engine.Execute(source, path);
        FlushErrorsToStderr();
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.ErrorCount);
    }

    private bool _interactiveImported;

    /// <summary>
    /// Runs a source snippet the way <c>flow -e</c> does: <c>@std</c> is imported first
    /// (once). Scripts run through <see cref="RunFile"/> import what they use.
    /// </summary>
    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunSource(string source, string fileName = "<test>")
    {
        if (!_interactiveImported)
        {
            _engine.ImportInteractiveDefaults();
            _interactiveImported = true;
        }
        var success = _engine.Execute(source, fileName);
        FlushErrorsToStderr();
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.ErrorCount);
    }

    /// <summary>
    /// Returns the <see cref="Value"/> of a top-level variable by name from the global frame
    /// after <see cref="RunSource"/> completes. Throws if the variable is not declared.
    /// Phase 15 Plan 04: added for per-variable Fact probing (see EuclideanSwingTests,
    /// EuclideanHumanizeTests) — prior Phase 14 Facts used stdout substring assertions
    /// exclusively, but velocity observation requires structured Value access.
    /// </summary>
    public Value GetVariable(string name) => _engine.Context.GlobalFrame.GetVariable(name);

    /// <summary>
    /// Phase 36 Plan 36-11 — returns the underlying <see cref="FlowEngine"/> so
    /// tests can poke at engine-init-time state (e.g.,
    /// <c>FlowEngine.Context.StyleRegistry</c> populated at construction time
    /// from the shipped + user style packs). Other fixture consumers should
    /// prefer <see cref="GetVariable"/> / <see cref="RunSource"/> — direct
    /// engine access exists for the rare init-state probing case.
    /// </summary>
    public FlowEngine GetEngine() => _engine;

    /// <summary>
    /// Mirrors the CLI: after Execute, format the ErrorReporter contents to stderr. The interpreter entry-point does this
    /// for user feedback; our fixture does it so that Theory rows asserting
    /// stderr substrings (ExpectedErrorScripts) see the same messages.
    /// </summary>
    private void FlushErrorsToStderr()
    {
        // Both accumulators: legacy FlowErrors and rich FlowDiagnostics (unknown
        // identifiers, match exhaustiveness), exactly as the CLI prints them.
        if (_engine.ErrorReporter.Errors.Count > 0 || _engine.ErrorReporter.HasDiagnostics)
        {
            _stderr.WriteLine(_engine.ErrorReporter.FormatAll(_engine.SourceMap, useColor: false));
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
