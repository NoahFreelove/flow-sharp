using FlowCli.Doc;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 DOC-02 — code examples inside <c>///</c> doc-comments execute via the
/// Phase 35 hermetic in-process runner (D-10): a passing example produces NO
/// annotation; a failing example is annotated <c>[example failed]</c> in the
/// generated docs AND doubles as a regression test. Audio/MIDI examples are
/// checked for successful render, not byte output (portable).
///
/// 41-03: live — exercises <see cref="DocExampleRunner"/> directly + the
/// doc-comment fence parser in <see cref="DocCollector"/>.
/// </summary>
[Trait("Category", "Phase41")]
public class DocExampleExecTests
{
    [Fact]
    public void PassingExample_NoAnnotation()
    {
        var runner = new DocExampleRunner();
        // Bare expression that runs without error → no failure annotation.
        var failure = runner.RunOne("(print (str (add 1 2)))");
        Assert.Null(failure);
    }

    [Fact]
    public void FailingExample_AnnotatedExampleFailed()
    {
        var runner = new DocExampleRunner();
        // References an undefined function → engine accumulates an error.
        var failure = runner.RunOne("(thisFunctionDoesNotExist 1 2 3)");
        Assert.NotNull(failure);
        Assert.Contains("[example failed]", failure);
    }

    [Fact]
    public void AudioExample_ChecksRenderNotBytes()
    {
        var runner = new DocExampleRunner();
        // Per D-10: an audio render that completes without error is a PASS —
        // we never compare bytes (platform-portable).
        var failure = runner.RunOne("use \"@audio\"\n(createSineTone 440Hz 0.1 0.5)");
        Assert.Null(failure);
    }

    [Fact]
    public void RunAll_PopulatesFailuresPerModel()
    {
        var passing = new DocModel(
            "ok", "(ok)", "summary", System.Array.Empty<DocParam>(),
            new[] { "(add 1 2)" }, System.Array.Empty<string>(), "Other", DocSource.Proc);
        var failing = new DocModel(
            "bad", "(bad)", "summary", System.Array.Empty<DocParam>(),
            new[] { "(nopeNotAFunction)" }, System.Array.Empty<string>(), "Other", DocSource.Proc);

        var runner = new DocExampleRunner();
        var result = runner.RunAll(new[] { passing, failing });

        Assert.False(result[0].HasFailures);
        Assert.True(result[1].HasFailures);
        Assert.Contains("[example failed]", result[1].ExampleFailures[0]);
    }

    [Fact]
    public void DocComment_FenceParsing_SplitsSummaryAndExamples()
    {
        var doc = "Doubles its input.\n```\n(dbl 21)\n```";
        var (summary, examples) = DocCollector.ParseDocComment(doc);
        Assert.Equal("Doubles its input.", summary);
        Assert.Single(examples);
        Assert.Equal("(dbl 21)", examples[0]);
    }
}
