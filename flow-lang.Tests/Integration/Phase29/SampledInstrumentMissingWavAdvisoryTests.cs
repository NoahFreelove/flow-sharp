using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// sweep-0614 regression — bundled sampled instruments (brass/sax/strings/
/// flute/bell/piano) used to render TOTAL SILENCE with NO advisory when the
/// WAV bundle was absent (Web target strips Samples/**; a fresh clone may not
/// have fetched it). SampleCache.HasInstrument returned true purely from the
/// static manifest, so the synth shells proceeded into the renderer, which
/// returned diagnostic-free silence for every note.
///
/// <para>This test points a SampleCache at a non-existent samples root so
/// EagerLoad loads ZERO WAVs, then asserts:
///   1. HasInstrument stays true (manifest-only) — documents the trap.
///   2. HasLoadedSamples is FALSE (reflects real loaded state — the fix).
///   3. SampledInstrumentRenderer.Render emits the one-shot
///      <c>sample:missing:&lt;instrument&gt;</c> advisory instead of silently
///      returning silence.</para>
/// </summary>
[Collection("FlowScripts")]
public class SampledInstrumentMissingWavAdvisoryTests : IDisposable
{
    public SampledInstrumentMissingWavAdvisoryTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static SongData OneNoteC4Song()
    {
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(
            new List<MusicalNoteData>
            {
                new('C', 4, 0, durationValue: 4, isRest: false, velocity: 0.7),
            },
            ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var section = new SectionData(
            "tmp",
            new Dictionary<string, SequenceData> { ["s"] = seq },
            context: null);
        var registry = new Dictionary<string, SectionData> { ["tmp"] = section };
        return new SongData(new List<SongSectionRef> { new("tmp", 1) }, registry);
    }

    [Fact]
    public void MissingBundle_HasInstrumentTrue_ButHasLoadedSamplesFalse()
    {
        // Point at a guaranteed-absent samples root.
        var cache = new SampleCache(Path.Combine(Path.GetTempPath(),
            "flow_no_samples_" + Guid.NewGuid().ToString("N")));
        cache.EagerLoad(OneNoteC4Song(), "brass");

        // The static manifest still claims coverage (the trap)...
        Assert.True(cache.HasInstrument("brass"));
        // ...but nothing actually loaded — the fix lets callers detect this.
        Assert.False(cache.HasLoadedSamples("brass"));
        Assert.Equal(0, cache.RawSampleCount);
    }

    [Fact]
    public void MissingBundle_Render_EmitsAdvisory_NotSilentlySilent()
    {
        var cache = new SampleCache(Path.Combine(Path.GetTempPath(),
            "flow_no_samples_" + Guid.NewGuid().ToString("N")));
        cache.EagerLoad(OneNoteC4Song(), "brass");

        var origErr = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        AudioBuffer buf;
        try
        {
            // brass is a single-velocity-layer instrument.
            var renderer = new SampledInstrumentRenderer(cache, "brass", hasVelocityLayers: false);
            var note = new MusicalNoteData(
                noteName: 'C', octave: 4, alteration: 0,
                durationValue: 4, isRest: false, velocity: 0.7,
                articulation: Articulation.Normal);
            buf = renderer.Render(note, sampleRate: 44100, durationBeats: 1.0, bpm: 120.0,
                RenderTuning.Default);
        }
        finally
        {
            Console.SetError(origErr);
        }

        // The render still returns a duration-correct (silent) buffer — charitable,
        // never throws — but now the silence is ADVISED, not diagnostic-free.
        Assert.True(buf.Frames > 0);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("sample:missing:brass"),
            "missing-WAV render must emit the one-shot 'sample:missing:brass' advisory");
        var stderr = capture.ToString();
        Assert.Contains("no WAV loaded for 'brass'", stderr);
    }
}
