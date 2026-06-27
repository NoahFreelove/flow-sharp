using FlowCli.Doc;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Regression tests for audit §5.15: DocExampleRunner.RunAll misattributed
/// [example failed] annotations when failures were a strict subset of examples
/// (the dense failure list was indexed by example position, so a single failure
/// on example N appeared under example 1).
///
/// Fix: RunAll now produces a per-example nullable list of the same length as
/// Examples (empty string = pass, non-null/non-empty = failure text for that
/// specific example), keyed exactly to the example that caused it.
/// </summary>
[Trait("Category", "Audit0609")]
public class DocExampleIndexAlignmentTests
{
    private static DocModel MakeModel(string name, params string[] examples) =>
        new DocModel(name, $"({name})", "summary", Array.Empty<DocParam>(),
            examples, Array.Empty<string>(), "Other", DocSource.Proc);

    /// <summary>
    /// 3 examples, only #3 (index 2) fails. The failure annotation must appear
    /// under example #3 only — examples #1 and #2 must have no annotation.
    /// </summary>
    [Fact]
    public void ThirdExampleFails_AnnotationRendersUnderThirdOnly()
    {
        var model = MakeModel("testFn",
            "(add 1 2)",            // example 0 — passes
            "(add 3 4)",            // example 1 — passes
            "(nopeDoesNotExist 5)"  // example 2 — fails
        );

        var runner = new DocExampleRunner(timeoutMs: 10_000);
        var results = runner.RunAll(new[] { model });
        var m = results[0];

        Assert.True(m.HasFailures, "Model should report failures");
        Assert.Equal(3, m.ExampleFailures.Count);

        // Examples 0 and 1 must have no annotation (empty = pass).
        Assert.True(string.IsNullOrEmpty(m.ExampleFailures[0]),
            "Example 0 (passing) must have no failure annotation");
        Assert.True(string.IsNullOrEmpty(m.ExampleFailures[1]),
            "Example 1 (passing) must have no failure annotation");

        // Example 2 must carry the [example failed] annotation.
        Assert.False(string.IsNullOrEmpty(m.ExampleFailures[2]),
            "Example 2 (failing) must have a failure annotation");
        Assert.Contains("[example failed]", m.ExampleFailures[2]);
    }

    /// <summary>
    /// 3 examples, only #1 (index 0) fails. The annotation must appear under
    /// example #1; examples #2 and #3 must be clean.
    /// </summary>
    [Fact]
    public void FirstExampleFails_AnnotationRendersUnderFirstOnly()
    {
        var model = MakeModel("testFn2",
            "(nopeDoesNotExist 1)",  // example 0 — fails
            "(add 1 2)",             // example 1 — passes
            "(add 3 4)"              // example 2 — passes
        );

        var runner = new DocExampleRunner(timeoutMs: 10_000);
        var results = runner.RunAll(new[] { model });
        var m = results[0];

        Assert.True(m.HasFailures);
        Assert.Equal(3, m.ExampleFailures.Count);

        Assert.False(string.IsNullOrEmpty(m.ExampleFailures[0]),
            "Example 0 (failing) must have a failure annotation");
        Assert.Contains("[example failed]", m.ExampleFailures[0]);

        Assert.True(string.IsNullOrEmpty(m.ExampleFailures[1]),
            "Example 1 (passing) must have no failure annotation");
        Assert.True(string.IsNullOrEmpty(m.ExampleFailures[2]),
            "Example 2 (passing) must have no failure annotation");
    }

    /// <summary>
    /// 3 examples, all pass. ExampleFailures must be empty (model unchanged).
    /// </summary>
    [Fact]
    public void AllPass_NoFailuresRecorded()
    {
        var model = MakeModel("testFn3",
            "(add 1 2)",
            "(add 3 4)",
            "(add 5 6)"
        );

        var runner = new DocExampleRunner(timeoutMs: 10_000);
        var results = runner.RunAll(new[] { model });
        var m = results[0];

        Assert.False(m.HasFailures);
        Assert.Empty(m.ExampleFailures);
    }

    /// <summary>
    /// HtmlEmitter must render [example failed] only under the failing example,
    /// not under passing ones.
    /// </summary>
    [Fact]
    public void HtmlEmitter_RendersAnnotationUnderCorrectExample()
    {
        // Build a model where example 0 passes and example 1 fails, by
        // constructing ExampleFailures directly (same-length list).
        var model = new DocModel("htmlTest", "(htmlTest)", "summary",
            Array.Empty<DocParam>(),
            new[] { "(add 1 2)", "(nopeHtml 99)" },
            new[] { "", "[example failed] undefined: nopeHtml" },
            "Other", DocSource.Proc);

        var html = new HtmlEmitter().Emit(new[] { model });

        // The first example block must NOT contain [example failed].
        // We verify by checking that the annotation is NOT associated with
        // the first code block in the source order.
        // Simple structural check: count occurrences.
        var failCount = CountOccurrences(html, "[example failed]");
        Assert.Equal(1, failCount);

        // The annotation must appear AFTER the second example's <pre>.
        var firstExPre = html.IndexOf("(add 1 2)", StringComparison.Ordinal);
        var secondExPre = html.IndexOf("(nopeHtml 99)", StringComparison.Ordinal);
        var annotationPos = html.IndexOf("[example failed]", StringComparison.Ordinal);

        Assert.True(firstExPre >= 0, "First example must appear in HTML");
        Assert.True(secondExPre >= 0, "Second example must appear in HTML");
        Assert.True(annotationPos > secondExPre,
            "Annotation must appear after the second example block");
    }

    /// <summary>
    /// MarkdownEmitter must render the blockquote annotation under the failing
    /// example only.
    /// </summary>
    [Fact]
    public void MarkdownEmitter_RendersAnnotationUnderCorrectExample()
    {
        var model = new DocModel("mdTest", "(mdTest)", "summary",
            Array.Empty<DocParam>(),
            new[] { "(add 1 2)", "(nopeMd 99)" },
            new[] { "", "[example failed] undefined: nopeMd" },
            "Other", DocSource.Proc);

        var md = new MarkdownEmitter().Emit(new[] { model });

        var failCount = CountOccurrences(md, "[example failed]");
        Assert.Equal(1, failCount);

        var firstExPos = md.IndexOf("(add 1 2)", StringComparison.Ordinal);
        var secondExPos = md.IndexOf("(nopeMd 99)", StringComparison.Ordinal);
        var annotationPos = md.IndexOf("[example failed]", StringComparison.Ordinal);

        Assert.True(firstExPos >= 0, "First example must appear in Markdown");
        Assert.True(secondExPos >= 0, "Second example must appear in Markdown");
        Assert.True(annotationPos > secondExPos,
            "Annotation must appear after the second example block");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
