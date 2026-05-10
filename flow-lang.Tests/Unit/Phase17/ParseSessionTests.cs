using FlowLsp;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 01 ParseSession Facts. Validates D-01 (reuse SimpleLexer + Parser +
/// ErrorReporter) and D-02 (no FlowEngine / audio surface in the LSP).
/// </summary>
public class ParseSessionTests
{
    [Fact]
    public void ValidSource_ReturnsAstWithZeroErrors()
    {
        // Flow proc syntax: `proc name() body end proc`. Body is a single expression/statement.
        var result = LspFixtures.Parse("proc greet()\n    (print \"hi\")\nend proc");
        Assert.NotNull(result.Ast);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SyntaxError_AccumulatesInErrors()
    {
        // Incomplete proc declaration — parser soft-fails and populates Errors.
        var result = LspFixtures.Parse("proc (");
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void DoesNotExposeFlowEngineSurface()
    {
        // Defensive check for D-02: ParseSession must NOT expose audio/interpreter types.
        var props = typeof(ParseSession).GetProperties();
        foreach (var p in props)
        {
            var typeName = p.PropertyType.FullName ?? "";
            Assert.DoesNotContain("AudioPlaybackManager", typeName);
            Assert.DoesNotContain("Interpreter", typeName);
            Assert.DoesNotContain("FlowEngine", typeName);
        }

        // And actually exercising Parse on this platform doesn't throw (no PulseAudio load).
        var result = LspFixtures.Parse("(print \"ok\")");
        Assert.NotNull(result);
    }
}
