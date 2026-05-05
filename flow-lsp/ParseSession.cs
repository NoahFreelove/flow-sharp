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
        // Phase 24 Wave 0 (Plan 24-00): mirror FlowEngine.Run() pragma-scan-then-parse
        // pipeline so Program.Pragmas reflects file-scope `enable <pragma>;` declarations.
        // Required precondition for D-19 activation gate (`Ast.Pragmas.Has("scaleLint")`).
        // Side-effect: closes Phase 21 latent bug — `enable hAsB;` now takes effect in
        // LSP-edited files (lexer sees pragmaSet for H→B substitution).
        //
        // Soft-failure: deliberately do NOT short-circuit on er.HasErrors between stages
        // (Phase 17 D-06 soft-failure model). Downstream stages still run on a partial
        // AST so the analyzer / completion / hover continue to work mid-edit.
        var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, path, er);
        var tokens = new SimpleLexer(transformedSource, er, path, pragmaSet).Tokenize();
        var ast = new Parser(tokens, er, pragmaSet).Parse();
        return new ParseResult(ast, tokens, er.Errors.ToList());
    }
}

public sealed record ParseResult(
    FlowProgram Ast,
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<FlowError> Errors);
