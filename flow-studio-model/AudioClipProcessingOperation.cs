using Flow.Audio;

namespace Flow.Studio.Model;

/// <summary>One captured offline audio operation, owned by one document/control
/// thread. Processing occurs elsewhere; acceptance never evaluates Flow code.</summary>
public sealed class AudioClipProcessingOperation : IClipProcessingOperation
{
    private readonly ProjectDocument _document;
    private readonly long _version;
    private readonly AudioClip _clip;
    private bool _finished;
    public GeneratorDescriptor Descriptor { get; }
    public PluginPackage Package { get; }
    public GenerationContext Context { get; }
    public PluginAudioInput Input { get; }
    public bool IsCurrent => !_finished && _document.ChangeVersion == _version;

    private AudioClipProcessingOperation(ProjectDocument document, AudioClip clip,
        PluginPackage package, PluginAudioInput input, IReadOnlyDictionary<string, double>? values)
    {
        _document = document; _version = document.ChangeVersion; _clip = clip;
        Package = package; Input = input;
        Descriptor = new(1, Guid.NewGuid(), package.Manifest.Builder);
        var parameters = package.Manifest.Parameters.ToDictionary(p => p.Id, p => p.Default, StringComparer.Ordinal);
        foreach (var pair in values ?? new Dictionary<string, double>())
        {
            var parameter = package.Manifest.Parameters.SingleOrDefault(p => p.Id == pair.Key)
                ?? throw new ArgumentException("Unknown processor parameter");
            _ = parameter.ToNormalized(pair.Value);
            parameters[pair.Key] = pair.Value;
        }
        var context = document.Snapshot.Context;
        Context = new(context.Revision, context.Seed, context.Tempo, context.Meter, parameters, context.Tuning);
    }

    /// <summary>Single selected clip supplies one input bus. External assets must
    /// already be decoded and hash-verified by the host's project asset resolver.</summary>
    public static AudioClipProcessingOperation Capture(ProjectDocument document, Guid clipId,
        PluginPackage package, IReadOnlyDictionary<string, double>? values = null,
        IReadOnlyDictionary<Guid, PcmAsset>? externalAssets = null)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(package);
        if (package.Manifest.Kind != FlowPluginKind.OfflineAudio || package.Manifest.AudioInputs != 1)
            throw new ArgumentException("Selected audio clip processing requires a one-input offline audio package");
        var snapshot = document.Snapshot;
        var clip = snapshot.Arrangement.AudioClips.SingleOrDefault(c => c.Id == clipId)
            ?? throw new ArgumentException("Unknown audio clip");
        package.Manifest.ValidateProcessing(clip.SampleRate, 256);
        PcmAsset? asset = null;
        foreach (var source in snapshot.Sources.Values)
        {
            var binding = source.Bindings.SingleOrDefault(b => b.Id == clip.SourceId && b.Available && b.Output.Role == GeneratedRole.Audio);
            if (binding is not null) { asset = source.Result.AudioLayers.Single(l => l.Id == binding.Output.LayerId).Asset; break; }
        }
        if (asset is null)
        {
            var reference = snapshot.Assets.SingleOrDefault(a => a.Id == clip.SourceId);
            if (reference is null || externalAssets is null || !externalAssets.TryGetValue(reference.Id, out asset))
                throw new ArgumentException("Audio source is unavailable");
            if (asset.SampleRate != reference.SampleRate || asset.Frames != reference.Frames)
                throw new ArgumentException("Resolved audio format differs from project metadata");
        }
        return new(document, clip, package, ProcessorClipCapture.Audio(clip, asset), values);
    }

    public void Cancel() => _finished = true;

    /// <summary>Replace only the captured clip, retaining placement/nudge and using
    /// the result's duration (including processor tails). Undo restores the exact
    /// previous snapshot; redo reuses the captured result and binding identities.</summary>
    public bool Accept(GeneratedSourceOutput result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsCurrent) return false;
        if (result.SourceId != Descriptor.SourceId || result.SourceRevision != 0 || !ReferenceEquals(result.Context, Context))
            throw new ArgumentException("Processor result identity or context does not match capture");
        Package.ValidateOutput(result, Input.SampleRate, 256);
        var asset = result.AudioLayers.Single().Asset;
        if (asset.Frames == 0) throw new ArgumentException("A replacement audio clip must have positive duration");
        var source = new ProjectSource(Descriptor, Package.Source, result, null, plugin: Package);
        var replacement = new AudioClip(_clip.Id, _clip.TrackId, source.Bindings.Single().Id,
            _clip.AnchorQuarters, 0, asset.Frames, asset.SampleRate, _clip.Nudge);
        _document.Edit("Process audio clip", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
            p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Select(c => c.Id == _clip.Id ? replacement : c)),
            p.Context, p.Sources.Values.Append(source), p.Routing, p.Assets, p.Automation, p.RenderSettings));
        _finished = true;
        return true;
    }
}
