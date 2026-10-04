using Flow.Audio;
using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

[Collection("FlowScripts")]
public class BackendLifecycleTests
{
    private static float[] Render(ProjectSnapshot p)
    {
        var playback = ProjectCompiler.Prepare(p, p.RenderSettings.SampleRate, p.RenderSettings.BlockFrames).Playback;
        var data = new float[checked((int)playback.TotalFrames * 2)];
        int block = playback.MaxBlockFrames * 2;
        for (int i = 0; i < data.Length; i += block) playback.Read(data.AsSpan(i, Math.Min(block, data.Length - i)));
        return data;
    }
    private static float[] Wave(string path)
    {
        using var stream = File.OpenRead(path); var asset = WaveAssetReader.Read(stream);
        var data = new float[asset.Frames * 2]; asset.CopyTo(data); return data;
    }
    [Fact]
    public async Task BuildEditAutomateUndoRecoverAndExportRetainOneMusicalProject()
    {
        var (doc, clip) = NoteClipProcessingTests.Create();
        ProjectRenderCommands.Set(doc, new(8000, 32));
        var editableId = Guid.NewGuid(); ProjectNoteCommands.MakeEditable(doc, clip.Id, editableId);
        var section = doc.Snapshot.Sources[editableId].Result.ScoreLayers[0].Composition.Placements[0].Section;
        var sequence = section.Sequences[0];
        ProjectNoteCommands.EditSequence(doc, editableId, section.Id, sequence.Id, "Edit velocity",
            s => NoteEditing.SetVelocity(s, [s.Notes[0].Id], .4));
        ProjectClipCommands.TrimScore(doc, clip.Id, .25, 3);
        ProjectClipCommands.Repeat(doc, clip.Id, [Guid.NewGuid()]);
        ProjectClipCommands.RelativeOffset(doc, [clip.Id], new(12));
        ProjectTimingCommands.Set(doc, new([new(0, 120), new(4, 90)]), new([new(1, 4, 4), new(3, 3, 4)]));
        var package = PluginBuildTests.Package(); var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "build"), package.Source);
        await using (var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")))
        {
            var built = await worker.BuildAsync(new(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context,
                TimeSpan.FromSeconds(10), package), TestContext.Current.CancellationToken);
            Assert.True(built.Status == JobStatus.Succeeded, built.Error);
            Assert.True(doc.Accept(ticket, built.Value!, "Build effect", p => p, plugin: package));
        }
        var binding = doc.Snapshot.Sources[ticket.Descriptor.SourceId].Bindings.Single().Id;
        ProjectEffectCommands.Set(doc, [new(binding, clip.TrackId)]);
        var beforeAutomation = doc.Snapshot;
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "", "gain", [new(0, .2), new(6, .8)], targetKind: AutomationTargetKind.PluginParameter));
        var expectedSnapshot = doc.Snapshot; var expected = Render(expectedSnapshot);
        Assert.Contains(expected, sample => sample != 0);
        Assert.True(doc.History.Undo()); Assert.Same(beforeAutomation, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(expectedSnapshot, doc.Snapshot); Assert.Equal(expected, Render(doc.Snapshot));
        string root = Path.Combine(Path.GetTempPath(), "flow-lifecycle-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string savedPath = Path.Combine(root, "song.flowproject"), recoveryPath = Path.Combine(root, "recovery");
            ProjectFile.Save(savedPath, doc); Assert.False(doc.History.IsDirty);
            ProjectTrackCommands.Rename(doc, clip.TrackId, "Recovered lead");
            await using (var autosave = new ProjectAutosaveHost(doc, recoveryPath, savedPath)) { }
            var inspection = ProjectRecovery.Inspect(savedPath, recoveryPath); Assert.True(inspection.DiffersFromSaved);
            var recovered = ProjectRecovery.Restore(inspection); Assert.True(recovered.History.IsDirty);
            Assert.Equal(expected, Render(recovered.Snapshot));
            ProjectFile.Save(savedPath, recovered); var reopened = ProjectFile.Load(savedPath);
            Assert.Equal(ProjectJson.Serialize(recovered.Snapshot), ProjectJson.Serialize(reopened.Snapshot));
            using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
            var reconstructed = engine.Evaluate(FlowProjectExporter.Export(reopened.Snapshot));
            Assert.True(reconstructed.Succeeded, string.Join("\n", reconstructed.Errors));
            Assert.Equal(expected, Render(reconstructed.LastValue!.As<ProjectSnapshot>()));
            var mix = await ProjectBounceFile.ExportAsync(reopened.Snapshot, root, Path.Combine(root, "mix.wav"), cancellation: TestContext.Current.CancellationToken);
            Assert.Equal(expected, Wave(mix.Path));
            var stems = await ProjectStemFiles.ExportAsync(reopened.Snapshot, root, Path.Combine(root, "stems"), cancellation: TestContext.Current.CancellationToken);
            Assert.Equal(expected, Wave(Path.Combine(stems.Directory, Assert.Single(stems.Stems).FileName)));
            var midi = await ProjectMidiExport.ExportAsync(reopened.Snapshot, root, Path.Combine(root, "notes.mid"), cancellation: TestContext.Current.CancellationToken);
            Assert.True(midi.Notes > 0); Assert.True(File.Exists(midi.Path)); Assert.False(reopened.History.IsDirty);
        }
        finally { Directory.Delete(root, true); }
    }
}
