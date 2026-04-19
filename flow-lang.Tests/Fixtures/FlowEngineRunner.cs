using FlowLang.Core;

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
