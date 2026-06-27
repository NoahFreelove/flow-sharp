using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 — section-query builtins.
///
/// <para>
/// Covers the two new overloads registered in
/// <c>HarmonyFunctions.RegisterHarmonyFunctions</c>:
///   - <c>(getSection Song String) -> Section</c> (single named section;
///     charitable: unknown name -> WarnOnce + empty Section).
///   - <c>(sectionSequences Song String) -> String[]</c> (sequence names of
///     ONE named section; the 1-arg <c>(sectionSequences Section)</c> form
///     already existed).
/// </para>
///
/// <para>
/// CLAUDE.md ("Harmony" builtin category) references both call forms. These
/// Facts pin the DOCUMENTED call forms working end-to-end and the charitable
/// (degenerate-input -> sane default + advisory, never throw) house style.
/// </para>
/// </summary>
public class SectionQueryBuiltinsTests
{
    private const string SongSource =
        "use \"@std\"\n" +
        "section verse { Sequence lead = | C4q D4q | }\n" +
        "section chorus { Sequence melody = | E4q F4q |\n  Sequence bass = | C2h | }\n" +
        "Song s = [verse chorus]\n";

    [Fact]
    public void GetSection_NamedSection_ReturnsThatSection()
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(SongSource + "Section v = (getSection s \"chorus\")\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var v = engine.Context.GetVariable("v");
        Assert.NotNull(v);
        var section = v!.As<SectionData>();
        Assert.Equal("chorus", section.Name);
        // chorus declares two named sequences: melody + bass.
        Assert.Equal(2, section.Sequences.Count);
        Assert.Contains("melody", section.Sequences.Keys);
        Assert.Contains("bass", section.Sequences.Keys);
    }

    [Fact]
    public void GetSection_UnknownName_ReturnsEmptySectionAndWarnsOnce()
    {
        RenderingDiagnostics.ResetForTesting();
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(SongSource + "Section missing = (getSection s \"nope\")\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var missing = engine.Context.GetVariable("missing");
        Assert.NotNull(missing);
        var section = missing!.As<SectionData>();
        Assert.Equal("nope", section.Name);
        Assert.Empty(section.Sequences);
        // Charitable: a WarnOnce advisory fired rather than an exception.
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("getSection-unknown:nope"));
        Assert.Empty(engine.ErrorReporter.Errors);
    }

    [Fact]
    public void SectionSequences_SongAndName_ReturnsSequenceNamesOfThatSection()
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(SongSource + "String[] names = (sectionSequences s \"verse\")\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var names = engine.Context.GetVariable("names");
        Assert.NotNull(names);
        var elements = names!.As<IReadOnlyList<Value>>();
        Assert.Equal(new[] { "lead" }, elements.Select(e => e.As<string>()).ToArray());
    }

    [Fact]
    public void SectionSequences_SongAndUnknownName_ReturnsEmptyArrayAndWarnsOnce()
    {
        RenderingDiagnostics.ResetForTesting();
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(SongSource + "String[] names = (sectionSequences s \"nope\")\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var names = engine.Context.GetVariable("names");
        Assert.NotNull(names);
        var elements = names!.As<IReadOnlyList<Value>>();
        Assert.Empty(elements);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("sectionSequences-unknown:nope"));
        Assert.Empty(engine.ErrorReporter.Errors);
    }

    [Fact]
    public void SectionSequences_OneArgSectionForm_StillWorks()
    {
        // Backward-compat: the pre-existing (sectionSequences Section) overload
        // must keep resolving — getSection feeds it.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            SongSource +
            "Section c = (getSection s \"chorus\")\n" +
            "String[] names = (sectionSequences c)\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var names = engine.Context.GetVariable("names")!.As<IReadOnlyList<Value>>();
        var got = names.Select(e => e.As<string>()).ToArray();
        Assert.Contains("melody", got);
        Assert.Contains("bass", got);
    }
}
