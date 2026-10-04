using Flow.Audio;
using Flow.Music.Model;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ProcessorClipCaptureTests
{
    [Fact]
    public void RepeatedWindowTrimsNotesWithStableDistinctOccurrenceIdentities()
    {
        var note = new NoteEvent(Guid.NewGuid(), "voice", 0, 2, new('A', 4, 0, 12, 69, 443),
            ExactDuration: new(2, 1), Origin: new("source", 2, 3));
        var section = new SectionSnapshot(Guid.NewGuid(), "section", new(),
            [new(Guid.NewGuid(), "sequence", 2, [note])]);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section, 3)]);
        var clip = new ScoreClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "notes", 10, 1, 4, new(23));
        var input = ProcessorClipCapture.Notes(clip, score);
        Assert.Equal(4, input.DurationQuarters);
        Assert.Equal(new double[] { 0, 1, 3 }, input.Notes.Select(n => n.OffsetQuarters));
        Assert.Equal(new double[] { 1, 2, 1 }, input.Notes.Select(n => n.DurationQuarters));
        Assert.Equal(3, input.Notes.Select(n => n.Id).Distinct().Count());
        Assert.Equal(3, input.Notes.Select(n => n.VoiceId).Distinct().Count());
        Assert.All(input.Notes, n => { Assert.Equal(note.Pitch, n.Pitch); Assert.Equal(note.Origin, n.Origin); });
        Assert.Null(input.Notes[0].ExactDuration); Assert.Equal(note.ExactDuration, input.Notes[1].ExactDuration);
        Assert.Equal(input.Serialize(), ProcessorClipCapture.Notes(clip, score).Serialize());
        Assert.Equal(input.Serialize(), ProcessorClipCapture.Notes(ClipOperations.MoveByQuarters(clip, 8), score).Serialize());
        Assert.Equal(2, note.DurationQuarters);
    }

    [Fact]
    public void EmptyAndFarRepeatedWindowsDoNotExpandEntireSource()
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "empty", new(), [new(Guid.NewGuid(), "empty", 4, [])]);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section, int.MaxValue)]);
        var clip = new ScoreClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "notes", 0, 8000000000, 4);
        Assert.Empty(ProcessorClipCapture.Notes(clip, score).Notes);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ProcessorClipCapture.Notes(clip, score, cancellation.Token));
    }

    [Fact]
    public void AudioWindowCopiesOffsetAndPadsMissingFramesWithoutMovingContent()
    {
        var asset = new PcmAsset(new float[] { 1, 2, 3, 4, 5, 6 }, 8000);
        var clip = new AudioClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12, 1, 4, 8000, new(-13));
        var input = ProcessorClipCapture.Audio(clip, asset);
        var output = new float[8]; input.Buses[0].CopyTo(output);
        Assert.Equal(new float[] { 3, 4, 5, 6, 0, 0, 0, 0 }, output);
        Assert.Equal(4, input.Frames); Assert.Equal(8000, input.SampleRate);
        Assert.Throws<ArgumentException>(() => ProcessorClipCapture.Audio(clip, new PcmAsset([], 16000)));
        var huge = new AudioClip(clip.Id, clip.TrackId, clip.SourceId, 0, 0, long.MaxValue, 8000);
        Assert.Throws<ArgumentException>(() => ProcessorClipCapture.Audio(huge, asset));
        var silence = new float[4]; asset.CopyFramesTo(long.MaxValue, silence); Assert.All(silence, s => Assert.Equal(0, s));
    }
}
