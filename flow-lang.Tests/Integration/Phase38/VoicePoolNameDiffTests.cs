using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-03 LIVE-03 — Wave 0 voice-pool name-diff tests.
///
/// Asserts the new Voice.Name + Voice.CopyStateFrom + VoiceAllocator.DiffByVoiceName
/// surface that the live-block swap path uses to PRESERVE overlapping voices
/// across re-renders (no envelope retrigger / click) and FADE OUT voices that
/// have been dropped from the new render. See RESEARCH §B lines 652-697.
///
/// Tests are RED until Task 1 lands Voice.Name + Voice.CopyStateFrom +
/// VoiceAllocator.DiffByVoiceName.
/// </summary>
[Collection("FlowScripts")]
public class VoicePoolNameDiffTests : IDisposable
{
    public VoicePoolNameDiffTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Helper — build a stub Voice with a 1-frame mono buffer and the given Name.
    /// </summary>
    private static Voice MakeVoice(string name, double offsetBeats = 0.0)
    {
        var buf = new AudioBuffer(frames: 1, channels: 1, sampleRate: 44100);
        return new Voice(buf, offsetBeats) { Name = name };
    }

    /// <summary>
    /// DiffByVoiceName partitions prev + next into (Preserved, Dropped, Added):
    /// - Preserved = next-side voices whose Name appears in BOTH prev and next
    /// - Dropped   = prev-side voices whose Name is no longer in next
    /// - Added     = next-side voices whose Name was not in prev
    ///
    /// Preserved uses the NEW voice instances (RESEARCH §B line 675
    /// "preserved.Add(newVoice)") so the swap consumer's CopyStateFrom call
    /// mutates the freshly rendered voices in place.
    /// </summary>
    [Fact]
    public void DiffByVoiceName_DistinguishesPreservedDroppedAdded()
    {
        var prev = new List<Voice>
        {
            MakeVoice("piano:0"),
            MakeVoice("piano:1"),
            MakeVoice("brass:0"),
        };
        var next = new List<Voice>
        {
            MakeVoice("piano:0"),
            MakeVoice("brass:0"),
            MakeVoice("drums:0"),
        };

        var (preserved, dropped, added) = VoiceAllocator.DiffByVoiceName(prev, next);

        // Preserved == { piano:0, brass:0 } (the NEW voice instances per RESEARCH §B).
        Assert.Equal(2, preserved.Count);
        Assert.Contains(preserved, v => v.Name == "piano:0" && ReferenceEquals(v, next[0]));
        Assert.Contains(preserved, v => v.Name == "brass:0" && ReferenceEquals(v, next[1]));

        // Dropped == { piano:1 } (prev-side reference, the voice that's leaving the mix).
        Assert.Single(dropped);
        Assert.Equal("piano:1", dropped[0].Name);
        Assert.Same(prev[1], dropped[0]);

        // Added == { drums:0 } (next-side reference).
        Assert.Single(added);
        Assert.Equal("drums:0", added[0].Name);
        Assert.Same(next[2], added[0]);
    }

    /// <summary>
    /// CopyStateFrom transfers the previous voice's playback cursor (OffsetBeats)
    /// onto the freshly rendered next voice so the live-swap path doesn't restart
    /// the voice from its onset. Envelope-phase transfer is documented as a no-op
    /// in v1.5 per the plan's behavior section — Voice.cs does not currently
    /// expose an explicit envelope cursor; the OffsetBeats transfer alone is
    /// sufficient.
    /// </summary>
    [Fact]
    public void CopyStateFrom_TransfersOffsetBeats()
    {
        var prev = MakeVoice("piano:0", offsetBeats: 1.5);
        var next = MakeVoice("piano:0", offsetBeats: 0.0);

        next.CopyStateFrom(prev);

        Assert.Equal(1.5, next.OffsetBeats);
    }

    /// <summary>
    /// Empty prev list → all next voices are Added; nothing Preserved / Dropped.
    /// Charitable edge case (D-v1.5-05) — composer's first render with no prior
    /// state must Just Work.
    /// </summary>
    [Fact]
    public void DiffByVoiceName_EmptyPrev_AllAdded()
    {
        var prev = new List<Voice>();
        var next = new List<Voice> { MakeVoice("piano:0"), MakeVoice("drums:0") };

        var (preserved, dropped, added) = VoiceAllocator.DiffByVoiceName(prev, next);

        Assert.Empty(preserved);
        Assert.Empty(dropped);
        Assert.Equal(2, added.Count);
    }

    /// <summary>
    /// Empty next list → all prev voices are Dropped (fade-out path will fire
    /// for each in PreserveVoiceState).
    /// </summary>
    [Fact]
    public void DiffByVoiceName_EmptyNext_AllDropped()
    {
        var prev = new List<Voice> { MakeVoice("piano:0"), MakeVoice("brass:0") };
        var next = new List<Voice>();

        var (preserved, dropped, added) = VoiceAllocator.DiffByVoiceName(prev, next);

        Assert.Empty(preserved);
        Assert.Equal(2, dropped.Count);
        Assert.Empty(added);
    }
}
