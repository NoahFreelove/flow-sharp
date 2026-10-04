#if !FLOW_WEB
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Studio.Model;

namespace FlowLang.Hosting;

internal sealed record GeneratorWireRequest(int Version, Guid RequestId, GeneratorDescriptor Descriptor,
    long SourceRevision, string Source, long ContextRevision, int Seed, TempoChange[] Tempo,
    MeterChange[] Meter, Dictionary<string, double> Parameters, double TimeLimitMs, string? Plugin = null, string? AudioInput = null, string? NoteInput = null, string? Assets = null, ProjectTuning? Tuning = null)
{
    internal static GeneratorWireRequest From(GeneratorBuildRequest r, Guid id) => new(6, id, r.Descriptor,
        r.SourceRevision, r.Source, r.Context.Revision, r.Context.Seed, r.Context.Tempo.Changes.ToArray(),
        r.Context.Meter.Changes.ToArray(), r.Context.Parameters.ToDictionary(p => p.Key, p => p.Value), r.TimeLimit.TotalMilliseconds, r.Plugin?.Serialize(), r.AudioInput?.Serialize(), r.NoteInput?.Serialize(), r.Assets?.Serialize(), r.Context.Tuning);
    internal GeneratorBuildRequest ToRequest()
    {
        if (Version != 6 || RequestId == Guid.Empty || Parameters is null) throw new JsonException("Invalid request protocol");
        return new(Descriptor, SourceRevision, Source,
            new(ContextRevision, Seed, new(Tempo), new(Meter), Parameters, Tuning), TimeSpan.FromMilliseconds(TimeLimitMs), Plugin is null ? null : PluginPackage.Deserialize(Plugin), AudioInput is null ? null : PluginAudioInput.Deserialize(AudioInput), NoteInput is null ? null : PluginNoteInput.Deserialize(NoteInput), Assets is null ? null : GeneratorAssets.Deserialize(Assets));
    }
}
internal sealed record GeneratorWireResponse(int Version, Guid RequestId, Guid SourceId, long SourceRevision,
    long ContextRevision, JobStatus Status, string? Contents, string? Error);

internal static class GeneratorWorkerProtocol
{
    internal const int MaxRequestCharacters = 16 * 1024 * 1024;
    internal const int MaxResponseCharacters = 40 * 1024 * 1024;
    internal static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
    };
    internal static async Task<string> ReadBoundedAsync(TextReader reader, int limit, CancellationToken token)
    {
        var builder = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            if (count > limit - builder.Length) throw new InvalidDataException("Worker message exceeds size limit");
            builder.Append(buffer, 0, count);
        }
        return builder.ToString();
    }
}

/// <summary>One-shot generator worker: stdin EOF terminates the request; process
/// exit terminates the reply. stdout is reserved for one bounded protocol message.</summary>
public static class GeneratorWorkerServer
{
    public static async Task<int> RunAsync(TextReader input, TextWriter output)
    {
        GeneratorWireRequest? wire = null;
        GeneratorWireResponse reply;
        try
        {
            string text = await GeneratorWorkerProtocol.ReadBoundedAsync(input, GeneratorWorkerProtocol.MaxRequestCharacters, default);
            wire = JsonSerializer.Deserialize<GeneratorWireRequest>(text, GeneratorWorkerProtocol.Json)
                ?? throw new JsonException("Missing generator request");
            var result = FlowDawGenerator.Build(wire.ToRequest());
            reply = new(6, wire.RequestId, wire.Descriptor.SourceId, wire.SourceRevision, wire.ContextRevision,
                result.Status, result.Value is null ? null : GeneratedContentJson.Serialize(result.Value), result.Error);
        }
        catch (Exception error)
        {
            reply = new(6, wire?.RequestId ?? Guid.Empty, wire?.Descriptor.SourceId ?? Guid.Empty,
                wire?.SourceRevision ?? 0, wire?.ContextRevision ?? 0, JobStatus.Failed, null, error.Message);
        }
        string json = JsonSerializer.Serialize(reply, GeneratorWorkerProtocol.Json);
        if (json.Length > GeneratorWorkerProtocol.MaxResponseCharacters)
            json = JsonSerializer.Serialize(reply with { Status = JobStatus.Failed, Contents = null, Error = "Generated output exceeds worker transport budget" }, GeneratorWorkerProtocol.Json);
        await output.WriteAsync(json);
        await output.FlushAsync();
        return 0;
    }
}
#endif
