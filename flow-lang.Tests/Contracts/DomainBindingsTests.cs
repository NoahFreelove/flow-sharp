using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;
using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// The language runtime evaluates domain constructs only through
/// <see cref="Interpreter.DomainBindings"/>. A context without bindings (a
/// language-only host) runs ordinary code and reports each music construct at its
/// location instead of evaluating it; the music host's bindings are what give the
/// music grammar its meaning.
/// </summary>
public class DomainBindingsTests
{
    private static (ExecutionContext Context, ErrorReporter Errors) Run(string source, Interpreter.DomainBindings? bindings = null)
    {
        var errors = new ErrorReporter();
        var context = new ExecutionContext(errors, new InternalFunctionRegistry())
        {
            Bindings = bindings ?? new Interpreter.DomainBindings(),
        };
        var tokens = new SimpleLexer(source, errors, "<bindings>").Tokenize();
        var program = new Parser(tokens, errors).Parse();
        new Interpreter.Interpreter(context, errors).Execute(program);
        return (context, errors);
    }

    private static string Messages(ErrorReporter errors) =>
        string.Join("\n", errors.Errors.Select(e => e.Message).Concat(errors.Diagnostics.Select(d => d.Message)));

    [Fact]
    public void LanguageOnlyContextRunsPlainCode()
    {
        var (context, errors) = Run("Int x = 5\nString s = \"text\"\nBool b = true\nfn Int n => n");
        Assert.Equal(0, errors.ErrorCount);
        Assert.Equal(5, context.GlobalFrame.GetVariable("x").As<int>());
        Assert.Equal("text", context.GlobalFrame.GetVariable("s").As<string>());
    }

    [Theory]
    [InlineData("Sequence q = | C4 D4 |", "note stream is not available")]
    [InlineData("tempo 120 { Int y = 1 }", "musical context is not available")]
    [InlineData("section verse { Int y = 1 }", "section is not available")]
    [InlineData("Void c = Cmaj7", "chord literal is not available")]
    public void LanguageOnlyContextReportsMusicConstructs(string source, string expected)
    {
        var (_, errors) = Run(source);
        Assert.Contains(expected, Messages(errors));
    }

    [Fact]
    public void LanguageOnlyContextHasNoMusicLiteralsOrConstants()
    {
        // A unit/pitch token is its text without a literal parser; duration names
        // are ordinary unknown identifiers without the music constants.
        var (context, errors) = Run("Void n = C4\nVoid d = q");
        Assert.Equal("C4", context.GlobalFrame.GetVariable("n").As<string>());
        Assert.Contains("unknown identifier 'q'", Messages(errors));
    }

    [Fact]
    public void MusicBindingsGiveTheGrammarItsMeaning()
    {
        var (context, errors) = Run("Void n = C4\nVoid d = q\nVoid c = Cmaj7", Music.MusicBindings.Create());
        Assert.Equal(0, errors.ErrorCount);
        Assert.Equal("Note", context.GlobalFrame.GetVariable("n").Type.Name);
        Assert.Equal("NoteValue", context.GlobalFrame.GetVariable("d").Type.Name);
        Assert.Equal("Chord", context.GlobalFrame.GetVariable("c").Type.Name);
    }
}
