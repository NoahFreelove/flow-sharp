using FlowLang.Runtime;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;
#if !FLOW_WEB
using FlowLang.StandardLibrary.Audio.Sfz;
#endif

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Render state owned by one engine session: sample caches, the evaluation context
/// the renderer reads (SFZ patches, PRNG registry), render random sources, custom
/// wavetables, the <c>setBPM</c> timeline tempo and the in-memory MIDI sink.
/// Replaces the former process statics (<c>FlowEngine.CurrentSampleCache</c>,
/// <c>SynthUtils.Rng</c>, <c>FileIO.Random</c>, ...), so concurrent engines render
/// independently and deterministically.
/// </summary>
public sealed class RenderServices
{
    public const int SynthNoiseSeed = 0x55EED;
    public const int DitherSeed = 0xD17E2;

    public RenderServices(SampleCache sampleCache, ExecutionContext context
#if !FLOW_WEB
        , SfzSampleCache sfzSampleCache
#endif
        )
    {
        SampleCache = sampleCache;
        Context = context;
#if !FLOW_WEB
        SfzSampleCache = sfzSampleCache;
#endif
    }

    /// <summary>
    /// Cancellation checkpoint for long render/DSP loops: throws when the current
    /// session's evaluation was cancelled or ran out of time.
    /// </summary>
    public static void Checkpoint() => SessionServices.Current?.ThrowIfCancelled();

    /// <summary>Render services of the current session, if any.</summary>
    public static RenderServices? Current => SessionServices.Current?.Get<RenderServices>();

    public SampleCache SampleCache { get; }

#if !FLOW_WEB
    public SfzSampleCache SfzSampleCache { get; }
#endif

    public ExecutionContext Context { get; }

    /// <summary>Noise oscillator source; reseeded at every render boundary.</summary>
    public Random NoiseRng { get; private set; } = new(SynthNoiseSeed);

    /// <summary>WAV dither source; reseeded at every WAV write.</summary>
    public Random DitherRng { get; private set; } = new(DitherSeed);

    public void ResetNoiseRng() => NoiseRng = new Random(SynthNoiseSeed);

    public void ResetDitherRng() => DitherRng = new Random(DitherSeed);

    /// <summary>Wavetables registered with <c>registerWavetable</c> in this session.</summary>
    public Dictionary<string, float[]> CustomWavetables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tempo set with <c>setBPM</c>; null means the 120 BPM default.</summary>
    public double? TimelineBpm { get; set; }

    /// <summary>Bytes of the last MIDI file written to the in-memory sink (browser runtime).</summary>
    public byte[]? LastMidiBytes { get; set; }
}
