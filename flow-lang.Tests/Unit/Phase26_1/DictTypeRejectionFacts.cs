using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 4 (GREEN): pins DICT-01 negative side — non-hashable
/// types (Buffer, Voice, Lazy, Function, Sequence) are rejected at the type-check
/// site (parse-time) with a "Dict key type 'X' is not hashable" error.
/// Tuple-of-hashables with a non-hashable inner element rejects at the inner site.
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class DictTypeRejectionFacts
{
    [Theory]
    [InlineData("Buffer")]
    [InlineData("Voice")]
    [InlineData("Lazy<Int>")]
    [InlineData("Function")]
    [InlineData("Sequence")]
    public void Rejects_NonHashableKey(string disallowedTypeName)
    {
        var src = $"use \"@std\"\nDict<{disallowedTypeName}, Int> bad = (dict)\n";
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errCount) = runner.RunSource(src);
        Assert.NotEqual(0, errCount);
        Assert.Contains("not hashable", stderr);
    }

    [Fact]
    public void RejectsTupleWithNonHashableElement()
    {
        const string SOURCE = "use \"@std\"\nDict<Tuple<<Buffer, Int>>, Int> bad = (dict)\n";
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errCount) = runner.RunSource(SOURCE);
        Assert.NotEqual(0, errCount);
        Assert.Contains("not hashable", stderr);
    }
}
