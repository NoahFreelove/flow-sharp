using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Music.Model;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ArrangementPlaybackTests
{
    private static readonly Guid Track = Guid.NewGuid(), SourceId = Guid.NewGuid();
    private static CompositionSnapshot Source(double duration = 8, int repeats = 1)
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "notes", new(999),
            [new(Guid.NewGuid(), "lead", duration, [new(Guid.NewGuid(), "voice", 0, duration, new('A', 4, 0, null, 69, 443.5))])]);
        return new(Guid.NewGuid(), [new(Guid.NewGuid(), section, repeats)]);
    }
    private static ArrangementSnapshot Arrangement(params ScoreClip[] clips) => new(Guid.NewGuid(),
        new([new(0, 120), new(4, 60)]), new([new(1, 4, 4)]), clips);
    private static ScoreClip Clip(double anchor = 2, double offset = 1, double length = 4, double nudge = 25) =>
        new(Guid.NewGuid(), Track, SourceId, "main", anchor, offset, length, new(nudge));
    private static PreparedArrangement Prepare(ArrangementSnapshot arrangement, CompositionSnapshot? source = null) => ArrangementCompiler.Prepare(
        arrangement, new Dictionary<(Guid, string), CompositionSnapshot> { [(SourceId, "main")] = source ?? Source() },
        [Track], AudioGraphDefinition.Input("track"), 8000, 128);

    [Fact]
    public void SplitRetriggersCrossingNotesAndKeepsSourceAndProjectTiming()
    {
        var clip = Clip();
        var parts = ClipOperations.Split(clip, new(2), Guid.NewGuid());
        var prepared = Prepare(Arrangement(parts.Left, parts.Right));
        var notes = prepared.TrackNotes[Track];
        Assert.Equal(2, notes.Count);
        Assert.Equal(8200, notes[0].StartFrame); // q2 = 1 second + 25ms
        Assert.Equal(16200, notes[0].EndFrame); // q4 = 2 seconds + 25ms
        Assert.Equal(notes[0].EndFrame, notes[1].StartFrame);
        Assert.Equal(32200, notes[1].EndFrame); // q6 = 4 seconds + 25ms
        Assert.Equal(443.5, notes[0].FrequencyHz);
        Assert.Equal(notes[0].NoteId, notes[1].NoteId);
        Assert.NotEqual(notes[0].ClipId, notes[1].ClipId);
        Assert.Equal(32360, prepared.Playback.TotalFrames); // 20ms release
    }

    [Fact]
    public void NegativeNudgeReconstructsAtZeroAndShortSourceLeavesSilence()
    {
        var clip = Clip(anchor: 0, offset: 0, length: 8, nudge: -250);
        var prepared = Prepare(Arrangement(clip), Source(1));
        var note = Assert.Single(prepared.TrackNotes[Track]);
        Assert.Equal(0, note.StartFrame);
        Assert.Equal(2000, note.EndFrame);
        Assert.Contains(prepared.Diagnostics, d => d.Code == "source-window-outside");
        Assert.Equal(46000, prepared.Playback.TotalFrames); // visible q8=6s, minus 250ms
        prepared.Playback.Seek(40000);
        var block = new float[256];
        prepared.Playback.Read(block);
        Assert.All(block, value => Assert.Equal(0, value));
    }

    [Fact]
    public void CroppedLateRepeatDoesNotExpandTheEntireSource()
    {
        var clip = Clip(anchor: 0, offset: 4_000_000, length: 4, nudge: 0);
        var prepared = Prepare(Arrangement(clip), Source(4, 2_000_000));
        var note = Assert.Single(prepared.TrackNotes[Track]);
        Assert.Equal(1_000_000, note.RepeatIndex);
        Assert.Equal(0, note.StartFrame);
        Assert.Equal(16000, note.EndFrame);
    }

    [Fact]
    public void MissingLayerIsDiagnosedAndKeepsClipDuration()
    {
        var clip = Clip();
        var prepared = ArrangementCompiler.Prepare(Arrangement(clip), new Dictionary<(Guid, string), CompositionSnapshot>(),
            [Track], AudioGraphDefinition.Input("track"), 8000, 128);
        Assert.Empty(prepared.TrackNotes[Track]);
        Assert.Contains(prepared.Diagnostics, d => d.Code == "missing-source-layer");
        Assert.Equal(32200, prepared.Playback.TotalFrames);
    }

    [Fact]
    public void VoiceLimitsSeekRetriggerReleaseAndBlockPartitionAreDeterministic()
    {
        ScheduledNote[] notes = [new(Guid.NewGuid(), Guid.NewGuid(), 0, 200, 440), new(Guid.NewGuid(), Guid.NewGuid(), 20, 250, 660)];
        var settings = new SineVoiceSettings(1, 0, 10);
        var large = new PreparedNotePlayback(notes, 8000, 512, settings: settings);
        var small = new PreparedNotePlayback(notes, 8000, 512, settings: settings);
        var expected = new float[660];
        large.Read(expected);
        var actual = new float[660];
        small.Read(actual.AsSpan(0, 62)); small.Read(actual.AsSpan(62));
        Assert.Equal(expected, actual);
        Assert.Equal(1, large.StolenVoices);
        Assert.Equal(330, large.TotalFrames);
        small.Seek(50);
        var held = new float[4]; small.Read(held);
        Assert.Equal(0, held[0]); // retrigger phase zero
        Assert.NotEqual(0, held[2]);
        small.Seek(260); // no reconstruction of already-released voices
        small.Read(held);
        Assert.All(held, value => Assert.Equal(0, value));
    }

    [Fact]
    public void PreparedNotePlaybackReadAndHeldNoteSeekAllocateNothing()
    {
        var notes = Enumerable.Range(0, 1000).Select(i => new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), i, 10000, 440));
        var playback = new PreparedNotePlayback(notes, 8000, 128, settings: new(8));
        var block = new float[256];
        for (int i = 0; i < 20; i++) { playback.Seek(5000); playback.Read(block); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { playback.Seek(5000); playback.Read(block); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
#if !FLOW_WEB
    [Fact]
    public void FlowGeneratorInstrumentArrangementAndCallbackShareOneMusicalPath()
    {
        var context = new GenerationContext(1, 7, new([new(0, 120)]), new([new(1, 4, 4)]));
        var generated = FlowLang.Hosting.FlowDawGenerator.Build(new(new(1, SourceId, "generate"), 1,
            FlowLang.Hosting.FlowDawGenerator.Template, context, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
        Assert.True(generated.Status == FlowLang.Hosting.JobStatus.Succeeded, generated.Error);
        using var engine = new FlowLang.Core.FlowEngine(new FlowLang.Core.EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var evaluated = engine.Evaluate("use \"@flowDaw\"; DawInstrument tone = (dawSine 16 2ms 30ms); tone");
        Assert.True(evaluated.Succeeded, string.Join("\n", evaluated.Errors));
        var instrument = evaluated.LastValue!.As<SineVoiceSettings>();
        var arrangement = Arrangement(Clip(anchor: 0, offset: 0, length: 4, nudge: 0));
        var sources = new Dictionary<(Guid, string), CompositionSnapshot> { [(SourceId, "main")] = generated.Value!.ScoreLayers[0].Composition };
        var routing = AudioGraphDefinition.Input("track").Then("master", "flow.gain", new Dictionary<string, double> { ["gain"] = 0.5 });
        PreparedArrangement Make() => ArrangementCompiler.Prepare(arrangement, sources, [Track], routing, 8000, 128,
            new Dictionary<Guid, SineVoiceSettings> { [Track] = instrument });
        var live = Make(); var offline = Make();
        var queue = new QueuedSinePlayback(live.Playback);
        var callback = new Flow.Platform.Linux.CallbackRenderProbe(queue, 256, mute: false);
        queue.TryPlay();
        var a = new float[256]; var b = new float[256];
        bool audible = false;
        while (offline.Playback.PositionFrames < offline.Playback.TotalFrames)
        {
            offline.Playback.Read(a); callback.Process(b);
            Assert.Equal(a, b);
            audible |= a.Any(sample => sample != 0);
        }
        Assert.True(audible);
        Assert.Equal(TransportState.Stopped, queue.State);
    }

#endif
    [Fact]
    public void PreparationRejectsUnsupportedControlsAndBudgetsWithoutPublishing()
    {
        var arrangement = Arrangement(Clip(anchor: 0, offset: 0, length: 8, nudge: 0));
        var sources = new Dictionary<(Guid, string), CompositionSnapshot> { [(SourceId, "main")] = Source(1, 8) };
        Assert.Throws<ArgumentException>(() => ArrangementCompiler.Prepare(arrangement, sources, [Track],
            AudioGraphDefinition.Input("track"), maxOccurrences: 1));
        Assert.Throws<OperationCanceledException>(() => ArrangementCompiler.Prepare(arrangement, sources, [Track],
            AudioGraphDefinition.Input("track"), cancellation: new CancellationToken(true)));
        var note = new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), 0, 10, 440);
        Assert.Throws<ArgumentException>(() => new PreparedNotePlayback([note, note], 8000, 128, maxOnsetsPerFrame: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreparedNotePlayback([note], 8000, 128, settings: new(0)));
    }
    [Fact]
    public void TieExtensionStaysInSecondsAcrossProjectTempoChange()
    {
        var note = new NoteEvent(Guid.NewGuid(), "voice", 3, 2, new('A', 4, 0, null, 69, 440), IsTied: true);
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "lead", 8, [note]);
        var section = new SectionSnapshot(Guid.NewGuid(), "source", new(999), [sequence]);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section)]);
        var prepared = Prepare(Arrangement(Clip(anchor: 0, offset: 0, length: 8, nudge: 0)), score);
        var scheduled = Assert.Single(prepared.TrackNotes[Track]);
        Assert.Equal(12000, scheduled.StartFrame); // q3 at 120bpm
        Assert.Equal(24800, scheduled.EndFrame); // q5 at 3 seconds plus fixed 100ms
        var withReverb = new SectionSnapshot(Guid.NewGuid(), "unsupported", new(ReverbSeconds: 1), [sequence]);
        var unsupported = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), withReverb)]);
        Assert.Throws<NotSupportedException>(() => Prepare(Arrangement(Clip()), unsupported));
    }
}
