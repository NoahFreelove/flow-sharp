using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ProjectAcceptanceTests
{
    private static ProjectDocument Create(long budget = 256 * 1024 * 1024)
    {
        var tempo = new ProjectTempoMap([new(0, 120)]);
        var meter = new ProjectMeterMap([new(1, 4, 4)]);
        return new(new(new(Guid.NewGuid(), tempo, meter), new(0, 42, tempo, meter)), historyBudgetBytes: budget);
    }
    private static SourceBuildTicket Build(ProjectDocument doc, Guid id, string code = "draft") => doc.BeginBuild(new(1, id, "generate"), code);
    private static GeneratedSourceOutput Output(SourceBuildTicket ticket, params string[] names) =>
        new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            names.Select(n => new GeneratedAudioLayer(n, new PcmAsset([0.1f, -0.1f], 8000))));

    [Fact]
    public void AcceptanceCapturesCodeAndResultForExactUndoRedo()
    {
        var doc = Create(); var initial = doc.Snapshot; var id = Guid.NewGuid();
        var ticket = Build(doc, id, "first"); var result = Output(ticket, "main");
        Assert.True(doc.Accept(ticket, result)); var accepted = doc.Snapshot;
        Assert.Equal("first", accepted.Sources[id].Code);
        Assert.Same(result, accepted.Sources[id].Result);
        Assert.True(doc.History.Undo()); Assert.Same(initial, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(accepted, doc.Snapshot);
        Assert.False(doc.Accept(ticket, result));
    }
    [Fact]
    public void RemovedBindingsRemainUnavailableAndReappearWithSameIdentity()
    {
        var doc = Create(); var id = Guid.NewGuid(); var first = Build(doc, id);
        Assert.True(doc.Accept(first, Output(first, "main")));
        var binding = doc.Snapshot.Sources[id].Bindings.Single();
        var next = Build(doc, id); Assert.True(doc.Accept(next, Output(next, "other")));
        Assert.False(doc.Snapshot.Sources[id].Bindings.Single(b => b.Id == binding.Id).Available);
        var last = Build(doc, id); Assert.True(doc.Accept(last, Output(last, "main")));
        Assert.True(doc.Snapshot.Sources[id].Bindings.Single(b => b.Id == binding.Id).Available);
        Assert.Equal(2, doc.Snapshot.Sources[id].Bindings.Count);
    }
    [Fact]
    public void SupersededForeignAndWrongContextResultsCannotCommit()
    {
        var doc = Create(); var id = Guid.NewGuid(); var a = Build(doc, id); var b = Build(doc, id);
        Assert.False(doc.Accept(a, Output(a, "main")));
        Assert.False(Create().Accept(b, Output(b, "main")));
        var context = new GenerationContext(b.Context.Revision, 43, b.Context.Tempo, b.Context.Meter);
        var wrong = new GeneratedSourceOutput(id, b.Revision, context, [], [new("main", new PcmAsset([0, 0], 8000))]);
        Assert.False(doc.Accept(b, wrong)); Assert.Equal(0, doc.History.UndoCount);
        Assert.True(doc.Accept(b, Output(b, "main")));
    }
    [Fact]
    public void ClipMovesAndIndependentSourceAcceptancePreservePendingBuild()
    {
        var doc = Create(); var id = Guid.NewGuid(); var ticket = Build(doc, id);
        var other = Build(doc, Guid.NewGuid()); Assert.True(doc.Accept(other, Output(other, "main")));
        var clip = new ScoreClip(Guid.NewGuid(), Guid.NewGuid(), id, "main", 16, 3, 8, new(25));
        doc.Edit("Place clip", s => new(new(s.Arrangement.Id, s.Arrangement.Tempo, s.Arrangement.Meter, [clip]), s.Context, s.Sources.Values));
        Assert.True(doc.Accept(ticket, Output(ticket, "audio")));
        Assert.Same(clip, doc.Snapshot.Arrangement.ScoreClips.Single());
        Assert.Equal(16, doc.Snapshot.Arrangement.ScoreClips.Single().AnchorQuarters);
    }
    [Fact]
    public void ContextChangeAndUndoNeverReviveOldRequests()
    {
        var doc = Create(); var id = Guid.NewGuid(); var ticket = Build(doc, id);
        doc.Edit("Parameters", s => new(s.Arrangement, new(1, 43, s.Arrangement.Tempo, s.Arrangement.Meter), s.Sources.Values));
        Assert.True(doc.History.Undo());
        Assert.False(doc.Accept(ticket, Output(ticket, "main")));
        var next = Build(doc, id); Assert.True(doc.History.Redo());
        Assert.False(doc.Accept(next, Output(next, "main")));
    }
    [Fact]
    public void BudgetFailureIsAtomicAndDoesNotConsumeRequest()
    {
        var doc = Create(1024); var before = doc.Snapshot; var ticket = Build(doc, Guid.NewGuid());
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Accept(ticket, Output(ticket, "main")));
        Assert.Same(before, doc.Snapshot); Assert.Equal(0, doc.History.UndoCount); Assert.False(doc.History.IsDirty);
    }
    [Fact]
    public void AcceptedAudioRebindsPlaybackAndUndoRestoresExactSamples()
    {
        var doc = Create(); var id = Guid.NewGuid(); var track = Guid.NewGuid();
        GeneratedSourceOutput Mixed(SourceBuildTicket t, float sample, string name = "main") =>
            new(id, t.Revision, t.Context, [], [new(name, new PcmAsset([sample, sample], 8000))],
                [new("mix", AudioGraphDefinition.Input("input"))]);
        var first = Build(doc, id); Assert.True(doc.Accept(first, Mixed(first, 0.25f)));
        var bindings = doc.Snapshot.Sources[id].Bindings;
        var audio = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var graph = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var clip = new AudioClip(Guid.NewGuid(), track, audio, 0, 0, 1, 8000);
        doc.Edit("Place audio", s => new(new(s.Arrangement.Id, s.Arrangement.Tempo, s.Arrangement.Meter,
            audioClips: [clip]), s.Context, s.Sources.Values));
        float Read()
        {
            var prepared = ProjectCompiler.Prepare(doc.Snapshot, [track], graph, sampleRate: 8000, blockFrames: 8);
            var samples = new float[2]; prepared.Playback.Read(samples); return samples[0];
        }
        Assert.Equal(0.25f, Read());
        var next = Build(doc, id); Assert.True(doc.Accept(next, Mixed(next, 0.75f)));
        Assert.Equal(0.75f, Read()); Assert.Same(clip, doc.Snapshot.Arrangement.AudioClips.Single());
        Assert.True(doc.History.Undo()); Assert.Equal(0.25f, Read());
        Assert.True(doc.History.Redo()); Assert.Equal(0.75f, Read());
        var removed = Build(doc, id); Assert.True(doc.Accept(removed, Mixed(removed, 1, "new")));
        var missing = ProjectCompiler.Prepare(doc.Snapshot, [track], graph, sampleRate: 8000, blockFrames: 8);
        Assert.Contains(missing.Diagnostics, d => d.Code == "missing-audio-asset");
        Assert.Equal(0, Read());
    }

}
