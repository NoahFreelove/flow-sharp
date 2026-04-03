using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Composition;

/// <summary>
/// Provides the polyrhythm() built-in function that overlays sequences with
/// different time signatures, calculating LCM for cycle alignment.
/// </summary>
public static class PolyrhythmFunctions
{
    private const int DefaultSampleRate = 44100;
    private const double DefaultBpm = 120.0;

    public static void Register(InternalFunctionRegistry registry)
    {
        // polyrhythm(Sequence, Sequence) -> Buffer
        var sig2 = new FunctionSignature("polyrhythm",
            [SequenceType.Instance, SequenceType.Instance]);
        registry.Register("polyrhythm", sig2, Polyrhythm);

        // polyrhythm(Sequence, Sequence, Int) -> Buffer (beat count override)
        var sig3 = new FunctionSignature("polyrhythm",
            [SequenceType.Instance, SequenceType.Instance, IntType.Instance]);
        registry.Register("polyrhythm", sig3, Polyrhythm);
    }

    private static Value Polyrhythm(IReadOnlyList<Value> args)
    {
        var seq1 = args[0].As<SequenceData>();
        var seq2 = args[1].As<SequenceData>();

        // Determine beat count: either from LCM of time signatures or explicit override
        double totalBeats;
        if (args.Count >= 3)
        {
            // Explicit beat count override (D-11)
            totalBeats = args[2].As<int>();
        }
        else
        {
            // Calculate LCM of time signature numerators (D-10)
            int num1 = GetTimeSignatureNumerator(seq1);
            int num2 = GetTimeSignatureNumerator(seq2);
            totalBeats = Lcm(num1, num2);
        }

        double bpm = DefaultBpm;
        int sampleRate = DefaultSampleRate;

        // Render each sequence independently (D-12)
        var voices1 = SequenceRenderer.RenderSequenceToVoices(seq1, "piano", sampleRate, bpm);
        var voices2 = SequenceRenderer.RenderSequenceToVoices(seq2, "piano", sampleRate, bpm);

        // Loop voices to fill the total duration
        double seq1Beats = seq1.TotalBeats;
        double seq2Beats = seq2.TotalBeats;

        var allVoices = new List<Voice>();

        // Loop sequence 1 voices to fill totalBeats
        if (seq1Beats > 0)
            LoopVoices(allVoices, voices1, seq1Beats, totalBeats, bpm, sampleRate);

        // Loop sequence 2 voices to fill totalBeats
        if (seq2Beats > 0)
            LoopVoices(allVoices, voices2, seq2Beats, totalBeats, bpm, sampleRate);

        if (allVoices.Count == 0)
            return Value.Buffer(new AudioBuffer(0, 2, sampleRate));

        // Mix all voices into stereo buffer using SongRenderer's mixer
        var result = SongRenderer.MixVoicesToStereoBuffer(allVoices, bpm, sampleRate, totalBeats);
        return Value.Buffer(result);
    }

    /// <summary>
    /// Loops voices by duplicating them at beat offsets to fill the total duration.
    /// </summary>
    private static void LoopVoices(
        List<Voice> target, List<Voice> sourceVoices,
        double sequenceBeats, double totalBeats,
        double bpm, int sampleRate)
    {
        int fullReps = (int)Math.Ceiling(totalBeats / sequenceBeats);

        for (int rep = 0; rep < fullReps; rep++)
        {
            double offsetShift = rep * sequenceBeats;
            if (offsetShift >= totalBeats) break;

            foreach (var voice in sourceVoices)
            {
                double newOffset = voice.OffsetBeats + offsetShift;
                if (newOffset >= totalBeats) continue;

                var newVoice = new Voice(voice.Buffer, newOffset)
                {
                    Gain = voice.Gain,
                    Pan = voice.Pan
                };
                target.Add(newVoice);
            }
        }
    }

    private static int GetTimeSignatureNumerator(SequenceData seq)
    {
        if (seq.Bars.Count > 0 && seq.Bars[0].TimeSignature != null)
            return seq.Bars[0].TimeSignature.Numerator;
        return 4; // Default 4/4
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;
}
