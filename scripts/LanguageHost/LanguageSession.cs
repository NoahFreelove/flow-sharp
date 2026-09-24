using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Interpreter;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace LanguageHost;

/// <summary>Minimal embedding example: only the language assembly and its core library.</summary>
public sealed class LanguageSession : IDisposable
{
    private readonly SessionServices _session;
    private readonly ErrorReporter _reporter = new();
    private readonly RuntimeContext _context;
    private readonly Interpreter _interpreter;
    private readonly SourceMap _sources = new();

    public LanguageSession(TextWriter output, TextWriter diagnostics)
    {
        _session = new SessionServices(output, diagnostics);
        var registry = new InternalFunctionRegistry();
        _context = new RuntimeContext(_reporter, registry) { Session = _session };
        CoreLibrary.Register(registry, _context);
        var loader = new ModuleLoader(_reporter);
        _interpreter = new Interpreter(_context, _reporter, loader);
        loader.ParentInterpreter = _interpreter;
    }

    public bool Execute(string source, string fileName = "<language-host>")
    {
        using var scope = _session.Enter();
        _reporter.Clear();
        _sources.Register(fileName, source);
        try
        {
            var (pragmas, transformed) = PragmaScanner.Scan(source, fileName, _reporter);
            if (!_reporter.HasErrors)
            {
                var tokens = new SimpleLexer(transformed, _reporter, fileName, pragmas).Tokenize();
                if (!_reporter.HasErrors)
                {
                    var program = new Parser(tokens, _reporter, pragmas).Parse();
                    if (!_reporter.HasErrors)
                    {
                        _context.StrictMode = program.Pragmas.Has("strict");
                        _interpreter.Execute(program);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _reporter.ReportError(ex.Message);
        }
        if (_reporter.HasErrors || _reporter.HasDiagnostics)
            _session.Diagnostics.Write(_reporter.FormatAll(_sources, useColor: false));
        return !_reporter.HasErrors;
    }

    public void Dispose() => _session.Dispose();
}
