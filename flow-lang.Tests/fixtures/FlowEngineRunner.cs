using FlowLang.Core;
using FlowLang.Runtime;

namespace FlowLang.Tests.Fixtures;

public sealed class FlowEngineRunner : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly TextWriter _origOut;
    private readonly TextWriter _origErr;
    private readonly FlowEngine _engine;

    public FlowEngineRunner(bool verbose = false)
    {
        _origOut = Console.Out;
        _origErr = Console.Error;
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        _engine = new FlowEngine(verbose);
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunFile(string path)
    {
        var source = File.ReadAllText(path);
        var success = _engine.Execute(source, path);
        FlushErrorsToStderr();
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.Errors.Count);
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunSource(string source, string fileName = "<test>")
    {
        var success = _engine.Execute(source, fileName);
        FlushErrorsToStderr();
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.Errors.Count);
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
    /// Mirrors flow-interpreter/Program.cs:78 behavior: after Execute, format the
    /// ErrorReporter contents to stderr. The interpreter entry-point does this
    /// for user feedback; our fixture does it so that Theory rows asserting
    /// stderr substrings (ExpectedErrorScripts) see the same messages.
    /// </summary>
    private void FlushErrorsToStderr()
    {
        if (_engine.ErrorReporter.Errors.Count > 0)
        {
            _stderr.WriteLine(_engine.ErrorReporter.FormatErrors());
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        Console.SetOut(_origOut);
        Console.SetError(_origErr);
    }
}
