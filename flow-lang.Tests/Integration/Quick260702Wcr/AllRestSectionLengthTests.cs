using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Quick260702Wcr;

/// <summary>
/// quick-260702-wcr — all-rest Song section renders as notated-length SILENCE.
///
/// <para>Before this task <see cref="SongRenderer.RenderSection(SectionData, string)"/>'s
/// terminal guard collapsed an all-rest section to ZERO frames: rests produce no
/// <c>Voice</c> objects, so the section reached
/// <c>allVoices.Count == 0 || maxBeats &lt;= 0</c> with <c>maxBeats &gt; 0</c>
/// (bars are capacity-based, so <c>SequenceData.TotalBeats</c> still reports the
/// notated bar-grid length) and returned a zero-length buffer. In a multi-section
/// Song that shifted every later section early — a `[tacet melody]` rendered
/// byte-identical to `[melody]` alone.</para>
///
/// <para>Fix: split the guard. <c>maxBeats &lt;= 0</c> (genuinely empty section)
/// still returns zero-length; <c>allVoices.Count == 0</c> with <c>maxBeats &gt; 0</c>
/// (all-rest) now returns a zero-filled stereo buffer of the notated length,
/// tempo-scaled off the resolved section bpm. Silence is the only musically-correct
/// reading of a rest section (charitable interpretation).</para>
///
/// <para>Buffers are captured WITHOUT a WAV round-trip (WAV write applies seeded
/// dither, so an "all samples exactly zero" assertion would fail). The
/// <c>organ</c> synth is used for note energy — full sustain, no sample-length cap.</para>
/// </summary>
[Collection("FlowScripts")]
public class AllRestSectionLengthTests : IDisposable
{
    private const int SampleRate = 44100;

    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    public AllRestSectionLengthTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>Max abs sample magnitude (across channels) over frames [start, end).</summary>
    private static float MaxAbs(AudioBuffer buf, int startFrame, int endFrame)
    {
        int ch = buf.Channels;
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(buf.Frames, endFrame);
        float maxAbs = 0f;
        for (int f = startFrame; f < endFrame; f++)
            for (int c = 0; c < ch; c++)
            {
                float v = MathF.Abs(buf.Data[f * ch + c]);
                if (v > maxAbs) maxAbs = v;
            }
        return maxAbs;
    }

    // ----- 1. Note content after an all-rest section starts at the rest duration -

    [Fact]
    public void AllRestSection_ThenNoteSection_NoteContentStartsAtRestDuration()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(Prelude + @"
tempo 100 {
    section tacet { Sequence s = | _ _ _ _ | _ _ _ _ | }
    section melody { Sequence s = | C4q D4q E4q F4q | }
}
Song withRest = [tacet melody]
Song noRest = [melody]
Buffer renderedWith = (renderSong withRest ""organ"")
Buffer renderedNo = (renderSong noRest ""organ"")
");
        Assert.Equal(0, errorCount);

        var withRest = runner.GetVariable("renderedWith").As<AudioBuffer>();
        var noRest = runner.GetVariable("renderedNo").As<AudioBuffer>();

        // 8 beats of rest @ 100 BPM in 4/4 = 8 * 0.6s = 4.8s.
        int restFrames = (int)(8.0 * (60.0 / 100.0) * SampleRate);

        // Total length = rest silence + note content (frame-accurate proof that
        // later sections no longer start early).
        Assert.InRange(withRest.Frames, noRest.Frames + restFrames - 2,
            noRest.Frames + restFrames + 2);

        // The lead of withRest (the rest section) is silent.
        float leadPeak = MaxAbs(withRest, 0, restFrames);
        Assert.True(leadPeak < 1e-6f,
            $"rest-section lead must be silent, peak was {leadPeak:E3}");

        // Audible energy exists AFTER the rest — the note content starts there.
        float tailPeak = MaxAbs(withRest, restFrames, withRest.Frames);
        Assert.True(tailPeak > 1e-3f,
            $"note content after the rest must carry energy, peak was {tailPeak:E3}");
    }

    // ----- 2. All-rest section alone = silent buffer of notated length ----------

    [Fact]
    public void AllRestSection_Alone_IsSilentBufferOfNotatedLength()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
tempo 100 {
    section tacet { Sequence s = | _ _ _ _ | _ _ _ _ | }
}
Song s = [tacet]
Buffer rendered = (renderSong s ""organ"")
");
        Assert.Equal(0, errorCount);

        var buf = runner.GetVariable("rendered").As<AudioBuffer>();

        int expectedFrames = (int)(8.0 * (60.0 / 100.0) * SampleRate);
        Assert.InRange(buf.Frames, expectedFrames - 2, expectedFrames + 2);
        Assert.True(buf.Frames > 0, "all-rest section must NOT collapse to zero frames");

        // Every sample is exactly 0.0 — a fresh zero-filled silent buffer.
        for (int i = 0; i < buf.Data.Length; i++)
            Assert.Equal(0f, buf.Data[i]);
    }

    // ----- 3. Genuinely-empty section (no sequences) stays zero-length ----------

    [Fact]
    public void EmptySection_NoSequences_StaysZeroLength()
    {
        // Direct-construction path: a section with an empty Sequences dictionary
        // has maxBeats == 0 → the preserved zero-length return.
        var empty = new SectionData("empty", new Dictionary<string, SequenceData>(), null);
        var registry = new Dictionary<string, SectionData> { ["empty"] = empty };
        var song = new SongData(new List<SongSectionRef> { new("empty") }, registry);

        var buf = SongRenderer
            .RenderSong(new List<Value> { Value.Song(song), Value.String("organ") })
            .As<AudioBuffer>();

        Assert.Equal(0, buf.Frames);
    }

    // ----- 4. Silent length scales with the resolved section bpm ----------------

    [Fact]
    public void AllRestSection_HalfTempo_DoublesFrames()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
tempo 60 {
    section tacet60 { Sequence s = | _ _ _ _ | _ _ _ _ | }
}
tempo 120 {
    section tacet120 { Sequence s = | _ _ _ _ | _ _ _ _ | }
}
Song slow = [tacet60]
Song fast = [tacet120]
Buffer renderedSlow = (renderSong slow ""organ"")
Buffer renderedFast = (renderSong fast ""organ"")
");
        Assert.Equal(0, errorCount);

        var slow = runner.GetVariable("renderedSlow").As<AudioBuffer>();
        var fast = runner.GetVariable("renderedFast").As<AudioBuffer>();

        // Half the tempo → twice the wall-clock duration → twice the frames.
        Assert.InRange(slow.Frames, fast.Frames * 2 - 2, fast.Frames * 2 + 2);
    }
}
