using Flow.Audio;
namespace Flow.Studio.Model;

/// <summary>Graph instrument package contract. Live targets use catalog smoothing
/// and shared voice-pool groups; structural targets require preparation.</summary>
public sealed class PluginInstrumentContract
{
    private readonly PluginEffectContract _parameters;
    public SineVoiceSettings Instrument { get; }
    public PluginInstrumentContract(PluginManifest manifest, SineVoiceSettings instrument,
        IEnumerable<PluginParameterTarget> targets, int sampleRate, int blockFrames)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        instrument.Validate();
        var graph = instrument.VoiceGraph ?? throw new ArgumentException("Instrument plugins require a prepared voice graph");
        _parameters = new(manifest, graph, targets, sampleRate, blockFrames, true);
        Instrument = instrument;
    }
    public SineVoiceSettings ApplyValues(IReadOnlyDictionary<string, double> values)
        => Instrument with { VoiceGraph = _parameters.ApplyValues(values) };
}
