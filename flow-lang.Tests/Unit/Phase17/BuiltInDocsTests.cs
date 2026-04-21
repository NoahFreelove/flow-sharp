using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 01 BuiltInDocs Facts. Validates CONTEXT D-12 starter set lookup.
/// </summary>
public class BuiltInDocsTests
{
    [Fact]
    public void Print_HasDoc()
    {
        var doc = BuiltInDocs.TryGet("print");
        Assert.NotNull(doc);
        Assert.Contains("print", doc!.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownKey_ReturnsNull() =>
        Assert.Null(BuiltInDocs.TryGet("nonexistent_nkjdhf"));

    /// <summary>
    /// Phase 17 Plan 05 Task 1 — D-12 starter set expansion. Every representative
    /// name below must have a Doc entry so the hover + completion handlers can
    /// surface one-line summaries for core + audio + transforms + harmony built-ins.
    /// </summary>
    [Theory]
    [InlineData("print")]
    [InlineData("str")]
    [InlineData("concat")]
    [InlineData("head")]
    [InlineData("tail")]
    [InlineData("map")]
    [InlineData("filter")]
    [InlineData("length")]
    [InlineData("sine")]
    [InlineData("writeWav")]
    [InlineData("transpose")]
    [InlineData("chordNotes")]
    [InlineData("reverb")]
    public void ExpandedSet_CoversCoreBuiltIns(string name)
        => Assert.NotNull(BuiltInDocs.TryGet(name));
}
