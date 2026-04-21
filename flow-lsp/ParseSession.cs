using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp;

/// <summary>
/// Stripped-down parse pipeline for the LSP server. Wires SimpleLexer + Parser +
/// ErrorReporter only. Intentionally omits the full evaluator pipeline and the audio
/// playback layer (which would P/Invoke <c>libpulse-simple.so.0</c> on construction).
///
/// Per D-02 (no audio in flow-lsp) and RESEARCH Pitfall 3. Each Parse call allocates
/// a fresh ErrorReporter so instances are safe to share across concurrent LSP requests.
/// </summary>
public sealed class ParseSession
{
    public ParseResult Parse(string source, string? path)
    {
        var er = new ErrorReporter();
        var tokens = new SimpleLexer(source, er, path).Tokenize();
        var ast = new Parser(tokens, er).Parse();
        return new ParseResult(ast, tokens, er.Errors.ToList());
    }
}

public sealed record ParseResult(
    FlowProgram Ast,
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<FlowError> Errors);
