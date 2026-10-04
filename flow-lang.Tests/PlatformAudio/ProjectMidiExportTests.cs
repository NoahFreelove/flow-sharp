using Flow.Music.IO;
using Flow.Music.Model;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectMidiExportTests
{
    [Fact]
    public async Task ExportedGatesMatchSharedSchedulingAcrossTempoChangesCutsRepeatsAndNudge()
    {
        var (doc, _) = NoteClipProcessingTests.Create();
        doc.Edit("Timing", p =>
        {
            var tempo = new ProjectTempoMap([new(0, 120), new(1, 90), new(3, 150)]);
            var meter = new ProjectMeterMap([new(1, 4, 4), new(2, 3, 4)]);
            return new(new(p.Arrangement.Id, tempo, meter, p.Arrangement.ScoreClips, p.Arrangement.AudioClips),
                new(1, p.Context.Seed, tempo, meter), p.Sources.Values, p.Routing);
        });
        var before = doc.Snapshot;
        string root = Path.Combine(Path.GetTempPath(), $"flow-midi-export-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "song.mid");
            var result = await ProjectMidiExport.ExportAsync(before, root, path, 8000, 16, cancellation: TestContext.Current.CancellationToken);
            var file = MidiFile.Read(path); var notes = file.GetTrackChunks().Skip(1).SelectMany(t => t.GetNotes()).OrderBy(n => n.Time).ToArray();
            var expected = ProjectCompiler.Prepare(before, 8000, 16).TrackNotes.Values.SelectMany(n => n).OrderBy(n => n.StartFrame).ToArray();
            Assert.Equal(expected.Length, result.Notes); Assert.Equal(expected.Length, notes.Length); Assert.Same(before, doc.Snapshot);
            for (int i = 0; i < notes.Length; i++)
            {
                double on = before.Arrangement.Tempo.SecondsAt((double)notes[i].Time / result.TicksPerQuarter);
                double off = before.Arrangement.Tempo.SecondsAt((double)notes[i].EndTime / result.TicksPerQuarter);
                Assert.InRange(Math.Abs(on - expected[i].StartFrame / 8000.0), 0, .0001);
                Assert.InRange(Math.Abs(off - expected[i].EndFrame / 8000.0), 0, .0001);
                Assert.Equal(69, (int)notes[i].NoteNumber);
            }
            var imported = MidiNoteImporter.Read(File.ReadAllBytes(path));
            Assert.Equal(new double[] { 0, 1, 3 }, imported.Tempo.Select(t => t.Quarter));
            Assert.Equal(new double[] { 0, 4 }, imported.Meter.Select(m => m.Quarter));
            Assert.Contains(result.Diagnostics, d => d.Contains("not Flow instruments"));
            string second = Path.Combine(root, "again.mid");
            await ProjectMidiExport.ExportAsync(before, root, second, 8000, 16, cancellation: TestContext.Current.CancellationToken);
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(second));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task SameKeyOverlapsUseIndependentChannelsAndTuningLossIsReported()
    {
        var (doc, clip) = AudioClipProcessingTests.Create();
        var pitch = new NotePitch('A', 4, 0, 15, 69, 444);
        ProjectNoteCommands.CreateFromNotes(doc, Guid.NewGuid(), Guid.NewGuid(), clip.TrackId, 0, 2,
            [new(Guid.NewGuid(), "a", 0, 1, pitch), new(Guid.NewGuid(), "b", .25, 1, pitch)]);
        string root = Path.Combine(Path.GetTempPath(), $"flow-midi-export-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "overlap.mid");
            var result = await ProjectMidiExport.ExportAsync(doc.Snapshot, root, path, 8000, 16, cancellation: TestContext.Current.CancellationToken);
            var notes = MidiFile.Read(path).GetNotes().OrderBy(n => n.Time).ToArray();
            Assert.Equal(2, notes.Length); Assert.NotEqual(notes[0].Channel, notes[1].Channel);
            Assert.True(notes[0].EndTime > notes[1].Time); Assert.Contains(result.Diagnostics, d => d.Contains("Rounded 2 tuned pitches"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CancellationAndOverwriteRefusalPreserveDestination()
    {
        var (doc, _) = NoteClipProcessingTests.Create();
        string root = Path.Combine(Path.GetTempPath(), $"flow-midi-export-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "song.mid"); File.WriteAllText(path, "previous");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => ProjectMidiExport.ExportAsync(doc.Snapshot, root, path, cancellation: TestContext.Current.CancellationToken));
            using var cancellation = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProjectMidiExport.ExportAsync(doc.Snapshot, root, path,
                8000, 16, true, cancellation.Token, (_, _) => cancellation.Cancel()));
            Assert.Equal("previous", File.ReadAllText(path)); Assert.Single(Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
