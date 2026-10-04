using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class FlowDawGeneratorTests
{
    [Fact]
    public async Task FullMapsAndExactIdentityAreCapturedPerBuild()
    {
        var id = Guid.NewGuid();
        string code = $$"""
            use "@flowDaw"
            use "@test"
            proc generate (Dict<String, Double>: context)
                Dict<String, String> info = (dawContextInfo)
                (assertEq (get info "sourceId") "{{id}}")
                (assertEq (get info "sourceRevision") "9007199254740993")
                (assertEq (get info "contextRevision") "9007199254740995")
                (assertEq (get info "timeLimitTicks") "50000000")
                (assertEq (get (dawTempoMap) 8.0) 90.0)
                (assertEq (get (dawMeterNumerators) 3) 7)
                (assertEq (get (dawMeterDenominators) 3) 8)
                Dict<Double, Double> changed = (set (dawTempoMap) 8.0 200.0)
                (assertEq (get (dawTempoMap) 8.0) 90.0)
                section a { Sequence melody = | A4q | }
                Song song = [a]
                (dawResult song)
            end proc
            """;
        var context = new GenerationContext(9007199254740995, 7,
            new([new(0, 120), new(8, 90)]), new([new(1, 4, 4), new(3, 7, 8)]));
        var request = new GeneratorBuildRequest(new(1, id, "generate"), 9007199254740993, code, context, TimeSpan.FromSeconds(5));
        var result = FlowDawGenerator.Build(request);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
#if !FLOW_WEB
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
#else
        await Task.CompletedTask;
#endif
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var unavailable = engine.Evaluate("use \"@flowDaw\"\n(dawContextInfo)");
        Assert.False(unavailable.Succeeded);
        var next = FlowDawGenerator.Build(Request());
        Assert.True(next.Status == JobStatus.Succeeded, next.Error);
    }
    private static GeneratorBuildRequest Request(string? source = null, Guid? id = null, long revision = 1, long contextRevision = 1) =>
        new(new(1, id ?? Guid.NewGuid(), "generate"), revision, source ?? FlowDawGenerator.Template,
            new(contextRevision, 1234, new([new(0, 120)]), new([new(1, 4, 4)]),
                new Dictionary<string, double> { ["density"] = 0.5 }), TimeSpan.FromSeconds(5));

    [Fact]
    public void TemplateReturnsDetachedTunedNotesWithoutMidiOrFileWrites()
    {
        var request = Request();
        var result = FlowDawGenerator.Build(request);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        var output = Assert.IsType<GeneratedSourceOutput>(result.Value);
        Assert.Equal(request.Descriptor.SourceId, output.SourceId);
        Assert.Equal(1, output.SourceRevision);
        var layer = Assert.Single(output.ScoreLayers);
        Assert.Equal("main", layer.Id);
        var notes = layer.Composition.Placements[0].Section.Sequences[0].Notes;
        Assert.Equal(3, notes.Count);
        Assert.Equal(440, notes[0].Pitch!.FrequencyHz, 6);
        var playback = PreparedSinePlayback.Prepare(layer.Composition);
        var samples = new float[playback.MaxBlockFrames * 2];
        playback.Read(samples);
        Assert.Contains(samples, n => n != 0);
        var repeated = FlowDawGenerator.Build(request);
        Assert.Equal(notes.Select(n => n.Id), repeated.Value!.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes.Select(n => n.Id));
    }

    [Fact]
    public void ContextParametersAndNamedLayersUseNormalFlowTypes()
    {
        var result = FlowDawGenerator.Build(Request("""
            use "@flowDaw"
            use "@test"
            proc generate (Dict<String, Double>: context)
                (assertEq (get context "seed") 1234.0)
                (assertEq (get context "parameter:density") 0.5)
                section a { Sequence melody = | A4w | }
                section b { Sequence melody = | C5w | }
                Song first = [a]
                Song second = [b]
                (set (dawResult "lead" first) "bass" second)
            end proc
            """));
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.Equal(new[] { "lead", "bass" }, result.Value!.ScoreLayers.Select(l => l.Id));
    }

    [Fact]
    public async Task BadBuildPreservesLastGoodAndContextRevisionIsCarried()
    {
        var id = Guid.NewGuid();
        using var host = new FlowDawGeneratorHost(id);
        var good = await host.Submit(Request(id: id));
        Assert.True(good.Status == JobStatus.Succeeded, good.Error);
        var bad = await host.Submit(Request("this is invalid source", id, 2));
        Assert.Equal(JobStatus.Failed, bad.Status);
        Assert.Same(good.Value, host.LastGood);
        var changed = await host.Submit(Request(id: id, revision: 3, contextRevision: 42));
        Assert.Equal(JobStatus.Succeeded, changed.Status);
        Assert.Equal(42, host.LastGood!.Context.Revision);
        Assert.Equal(3, host.LastGood.SourceRevision);
    }

    [Fact]
    public async Task SupersededBuildCannotReplaceLatestSourceContext()
    {
        var id = Guid.NewGuid();
        using var host = new FlowDawGeneratorHost(id);
        var old = host.Submit(Request(id: id));
        var newer = host.Submit(Request(id: id, revision: 2, contextRevision: 2));
        await Task.WhenAll(old, newer);
        Assert.Equal(JobStatus.Succeeded, (await newer).Status);
        Assert.Equal(2, host.LastGood!.SourceRevision);
        Assert.Equal(2, host.LastGood.Context.Revision);
        Assert.Throws<ArgumentException>(() => { _ = host.Submit(Request()); });
    }

    [Fact]
    public void InvalidReturnCancellationAndDescriptorAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new GeneratorDescriptor(1, Guid.NewGuid(), "x) (play x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratorDescriptor(2, Guid.NewGuid(), "generate"));
        var invalid = FlowDawGenerator.Build(Request("use \"@flowDaw\"; proc generate (Dict<String, Double>: context) 1 end proc"));
        Assert.Equal(JobStatus.Failed, invalid.Status);
        Assert.Contains("must return Dict", invalid.Error);
        var cancelled = FlowDawGenerator.Build(Request(), new CancellationToken(true));
        Assert.Equal(JobStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void ImportAloneDoesNotInvokeGenerator()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("use \"@flowDaw\"");
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
    }
    [Fact]
    public async Task TimedOutEntryPreservesLastGoodOutput()
    {
        var id = Guid.NewGuid();
        using var host = new FlowDawGeneratorHost(id);
        var good = await host.Submit(Request(id: id));
        Assert.Equal(JobStatus.Succeeded, good.Status);
        var request = Request("""
            use "@flowDaw"
            proc generate (Dict<String, Double>: context)
                while true {
                    for Int i in (range 0 1000) { (Nothing) }
                }
            end proc
            """, id, 2) with { TimeLimit = TimeSpan.FromMilliseconds(100) };
        var timed = await host.Submit(request);
        Assert.Equal(JobStatus.TimedOut, timed.Status);
        Assert.Same(good.Value, host.LastGood);
    }
}
