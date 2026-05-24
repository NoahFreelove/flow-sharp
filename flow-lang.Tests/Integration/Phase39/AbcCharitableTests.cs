using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Notation;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase39;

/// <summary>
/// Phase 39 Plan 39-03 ABC-02 — charitable-interpretation acceptance facts
/// per D-39-15 / D-39-17 / D-v1.5-05. Verifies the ABC parser NEVER throws
/// on malformed input and emits one-shot stderr advisories where appropriate.
/// </summary>
[Collection("FlowScripts")]
public class AbcCharitableTests
{
    public AbcCharitableTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static string CaptureStderr(System.Action body)
    {
        var originalErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try { body(); } finally { Console.SetError(originalErr); }
        return sw.ToString();
    }

    [Fact]
    public void UnknownOrnamentDropped_AdvisoryFires()
    {
        var output = CaptureStderr(() =>
        {
            var section = AbcImport.ParseSingleTune("X:1\nM:4/4\nL:1/4\nK:Cmaj\n~C D ~E F |");
            Assert.NotNull(section);
            var notes = section.Sequences.Values.First().Bars[0].MusicalNotes
                .Where(n => !n.IsRest).ToList();
            Assert.Equal(4, notes.Count);  // ornaments dropped, notes survive
        });
        Assert.Contains("[abc] dropped ornament '~'", output);
    }

    [Fact]
    public void UnknownHeaderIgnored_AdvisoryFires()
    {
        var output = CaptureStderr(() =>
        {
            var section = AbcImport.ParseSingleTune("X:1\nT:Title\nZ:Bogus header\nK:Cmaj\nC D E F |");
            Assert.NotNull(section);
        });
        Assert.Contains("[abc] ignored header 'Z'", output);
    }

    [Fact]
    public void MalformedInputNeverThrows()
    {
        // Random garbage — must produce a usable SectionData
        var section = AbcImport.ParseSingleTune("X:1\nGARBAGE\nlol\n^^^^^\n");
        Assert.NotNull(section);
    }

    [Fact]
    public void InvalidQTempo_FallsBackToDefault()
    {
        var output = CaptureStderr(() =>
        {
            var section = AbcImport.ParseSingleTune("X:1\nM:4/4\nQ:lol\nK:Cmaj\nC D E F |");
            Assert.NotNull(section);
            Assert.NotNull(section.Context);
            Assert.Equal(120.0, section.Context!.Tempo ?? 0.0);
        });
        Assert.Contains("[abc] could not parse tempo 'lol'", output);
    }

    [Fact]
    public void EmptyBody_NoThrow()
    {
        var section = AbcImport.ParseSingleTune("X:1\nM:4/4\nK:Cmaj\n");
        Assert.NotNull(section);
    }

    [Fact]
    public void AdvisoryDedupPerProcess()
    {
        // First call emits the advisory; second call with the same (token, line) does NOT emit again.
        var output = CaptureStderr(() =>
        {
            AbcImport.ParseSingleTune("X:1\nK:Cmaj\n~C D |");
            AbcImport.ParseSingleTune("X:1\nK:Cmaj\n~C D |");
        });
        // Count occurrences of the specific advisory
        int count = System.Text.RegularExpressions.Regex.Matches(output,
            @"\[abc\] dropped ornament '~' at line").Count;
        Assert.Equal(1, count);
    }
}
