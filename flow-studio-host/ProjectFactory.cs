using Flow.Studio.Model;
using FlowLang.Hosting;
namespace Flow.Studio.Host;

/// <summary>Startup/worker-only creation of a clean, routed project. The default
/// devices are evaluated from saved Flow source through the normal generator path.</summary>
public static class ProjectFactory
{
    public const string DefaultDeviceCode = """
        use "@flowDaw"
        proc generate (Dict<String, Double>: context)
            AudioGraph bus = (dawInput "trackInput" 0)
            AudioGraph level = (dawGain "trackGain" bus 1.0)
            AudioGraph output = (dawGain "masterGain" level 1.0)
            DawInstrument instrument = (dawSine 64 5ms 20ms)
            (dawCombine (dawResult "master" output) (dawResult "instrument" instrument))
        end proc
        """;

    public static ProjectDocument Create(double bpm = 120, int seed = 1, string trackName = "Track 1",
        CancellationToken cancellation = default)
    {
        // Validate user options before entering the interpreter.
        var trackId = Guid.NewGuid(); _ = new ProjectRouting([new(trackId, trackName)], null);
        var tempo = new ProjectTempoMap([new(0, bpm)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var context = new GenerationContext(0, seed, tempo, meter);
        var document = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), context));
        var ticket = document.BeginBuild(new(1, Guid.NewGuid(), "generate"), DefaultDeviceCode);
        var built = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, ticket.Code, context, TimeSpan.FromSeconds(20)), cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (built.Status != JobStatus.Succeeded || built.Value is null)
            throw new InvalidOperationException(built.Error ?? "Default Flow devices could not be generated");
        if (!document.Accept(ticket, built.Value)) throw new InvalidOperationException("Default Flow devices were not accepted");
        var source = document.Snapshot.Sources[ticket.Descriptor.SourceId];
        var graph = source.Bindings.Single(b => b.Output == new OutputKey(GeneratedRole.Graph, "master")).Id;
        var instrument = source.Bindings.Single(b => b.Output == new OutputKey(GeneratedRole.Instrument, "instrument")).Id;
        // Startup is initial state, not a gesture in the user's undo history.
        return new(new(document.Snapshot.Arrangement, context, document.Snapshot.Sources.Values,
            new([new(trackId, trackName, instrument)], graph)));
    }
}
