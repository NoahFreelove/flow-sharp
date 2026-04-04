using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Renders a Sequence to a Buffer with linearly interpolated BPM across beats,
/// producing smooth ritardando/accelerando audio output.
/// </summary>
public static class TempoRampRenderer
{
    private const int DefaultSampleRate = 44100;
    private const int StereoChannels = 2;
    private const string DefaultSynthType = "piano";

    public static void Register(InternalFunctionRegistry registry)
    {
        // tempoRamp(Sequence, Double, Double) -> Buffer
        var sig3 = new FunctionSignature(
            "tempoRamp",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("tempoRamp", sig3, args => Render(args, DefaultSynthType));

        // tempoRamp(Sequence, Double, Double, String) -> Buffer
        var sig4 = new FunctionSignature(
            "tempoRamp",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance, StringType.Instance]);
        registry.Register("tempoRamp", sig4, args => Render(args, null));
    }

    private static Value Render(IReadOnlyList<Value> args, string? defaultSynth)
    {
        var sequence = args[0].As<SequenceData>();
        double startBpm = Convert.ToDouble(args[1].Data!);
        double endBpm = Convert.ToDouble(args[2].Data!);
        string synthType = defaultSynth ?? (string)args[3].Data!;

        return RenderTempoRamp(sequence, startBpm, endBpm, synthType);
    }

    private static Value RenderTempoRamp(SequenceData sequence, double startBpm, double endBpm, string synthType)
    {
        var timeline = sequence.ToTimeline();

        if (timeline.Count == 0 || sequence.TotalBeats <= 0)
            return Value.Buffer(new AudioBuffer(0, StereoChannels, DefaultSampleRate));

        AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);

        foreach (var (bar, offsetBeats) in timeline)
        {
            // Compute bar's beat count
            double barBeatsForT = bar.IsPickup
                ? bar.GetActualBeats()
                : (bar.TimeSignature?.Numerator ?? 4);

            // Use the midpoint of the bar for BPM interpolation so that even
            // a single-bar sequence gets an averaged BPM between start and end
            double midpointBeats = offsetBeats + barBeatsForT / 2.0;
            double t = sequence.TotalBeats > 0 ? midpointBeats / sequence.TotalBeats : 0.5;
            double bpm = startBpm + t * (endBpm - startBpm);

            // Ensure BPM is positive
            if (bpm <= 0) bpm = 1;

            // Render the bar at offset 0 (we position manually by concatenation)
            var voices = BarRenderer.RenderBarAtBeat(bar, 0.0, synthType, DefaultSampleRate, bpm);

            // Compute the bar's beat count for mixing
            double barBeats = bar.IsPickup
                ? bar.GetActualBeats()
                : (bar.TimeSignature?.Numerator ?? 4);

            if (voices.Count == 0 || barBeats <= 0)
                continue;

            // Mix voices into a stereo buffer at the interpolated BPM
            var barBuffer = SongRenderer.MixVoicesToStereoBuffer(voices, bpm, DefaultSampleRate, barBeats);

            // Concatenate sequentially
            result = AppendBuffers(result, barBuffer);
        }

        return Value.Buffer(result);
    }

    /// <summary>
    /// Concatenates two AudioBuffers end-to-end via Array.Copy.
    /// Duplicated from SongRenderer since that version is private.
    /// </summary>
    private static AudioBuffer AppendBuffers(AudioBuffer a, AudioBuffer b)
    {
        if (a.Frames == 0) return b;
        if (b.Frames == 0) return a;

        int totalFrames = a.Frames + b.Frames;
        var result = new AudioBuffer(totalFrames, StereoChannels, DefaultSampleRate);
        Array.Copy(a.Data, 0, result.Data, 0, a.Data.Length);
        Array.Copy(b.Data, 0, result.Data, a.Data.Length, b.Data.Length);
        return result;
    }
}
