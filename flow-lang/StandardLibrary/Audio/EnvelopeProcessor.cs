using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Envelope creation and processing functions.
/// </summary>
public static class EnvelopeProcessor
{
    /// <summary>
    /// Creates an AR (Attack-Release) envelope.
    /// </summary>
    public static Value CreateAR(IReadOnlyList<Value> args)
    {
        double attackSec = args[0].As<double>();
        double releaseSec = args[1].As<double>();
        int sampleRate = args[2].As<int>();

        var parameters = new double[] { attackSec, releaseSec };
        var envelope = new Envelope(EnvelopeKind.AR, parameters, sampleRate);

        return Value.Envelope(envelope);
    }

    /// <summary>
    /// Creates an ADSR (Attack-Decay-Sustain-Release) envelope.
    /// </summary>
    public static Value CreateADSR(IReadOnlyList<Value> args)
    {
        double attack = args[0].As<double>();
        double decay = args[1].As<double>();
        double sustain = args[2].As<double>();
        double release = args[3].As<double>();
        int sampleRate = args[4].As<int>();

        var parameters = new double[] { attack, decay, sustain, release };
        var envelope = new Envelope(EnvelopeKind.ADSR, parameters, sampleRate);

        return Value.Envelope(envelope);
    }

    /// <summary>
    /// Applies an envelope curve to a buffer (modifies in-place).
    /// </summary>
    public static Value ApplyEnvelope(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var envelope = args[1].As<Envelope>();

        float[] curve = GenerateEnvelopeCurve(envelope, buffer.Frames);

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                float sample = buffer.GetSample(frame, ch);
                buffer.SetSample(frame, ch, sample * curve[frame]);
            }
        }

        return Value.Void();
    }

    /// <summary>
    /// Generates an envelope curve as an array of amplitude multipliers.
    /// </summary>
    private static float[] GenerateEnvelopeCurve(Envelope envelope, int totalFrames)
    {
        float[] curve = new float[totalFrames];

        switch (envelope.Kind)
        {
            case EnvelopeKind.AR:
                GenerateARCurve(curve, envelope, totalFrames);
                break;

            case EnvelopeKind.ADSR:
                GenerateADSRCurve(curve, envelope, totalFrames);
                break;
        }

        return curve;
    }

    /// <summary>
    /// Generates an AR envelope curve.
    /// </summary>
    private static void GenerateARCurve(float[] curve, Envelope envelope, int totalFrames)
    {
        double attackSec = envelope.Parameters[0];
        double releaseSec = envelope.Parameters[1];

        int attackFrames = (int)(attackSec * envelope.SampleRate);
        int releaseFrames = (int)(releaseSec * envelope.SampleRate);

        // Ensure attack and release fit within buffer
        attackFrames = Math.Min(attackFrames, totalFrames);
        releaseFrames = Math.Min(releaseFrames, totalFrames - attackFrames);

        int sustainFrames = totalFrames - attackFrames - releaseFrames;

        int frame = 0;

        // Attack phase: 0 to 1
        for (int i = 0; i < attackFrames; i++, frame++)
        {
            curve[frame] = (float)i / attackFrames;
        }

        // Sustain phase: 1
        for (int i = 0; i < sustainFrames; i++, frame++)
        {
            curve[frame] = 1.0f;
        }

        // Release phase: 1 to 0
        for (int i = 0; i < releaseFrames; i++, frame++)
        {
            curve[frame] = 1.0f - (float)i / releaseFrames;
        }
    }

    /// <summary>
    /// Generates an ADSR envelope curve.
    /// </summary>
    private static void GenerateADSRCurve(float[] curve, Envelope envelope, int totalFrames)
    {
        double attackSec = envelope.Parameters[0];
        double decaySec = envelope.Parameters[1];
        double sustainLevel = envelope.Parameters[2];
        double releaseSec = envelope.Parameters[3];

        int attackFrames = (int)(attackSec * envelope.SampleRate);
        int decayFrames = (int)(decaySec * envelope.SampleRate);
        int releaseFrames = (int)(releaseSec * envelope.SampleRate);

        // Ensure phases fit within buffer
        attackFrames = Math.Min(attackFrames, totalFrames);
        decayFrames = Math.Min(decayFrames, totalFrames - attackFrames);
        releaseFrames = Math.Min(releaseFrames, totalFrames - attackFrames - decayFrames);

        int sustainFrames = totalFrames - attackFrames - decayFrames - releaseFrames;

        int frame = 0;

        // Attack phase: 0 to 1
        for (int i = 0; i < attackFrames; i++, frame++)
        {
            curve[frame] = (float)i / attackFrames;
        }

        // Decay phase: 1 to sustain level
        for (int i = 0; i < decayFrames; i++, frame++)
        {
            float t = (float)i / decayFrames;
            curve[frame] = 1.0f - t * (1.0f - (float)sustainLevel);
        }

        // Sustain phase: sustain level
        for (int i = 0; i < sustainFrames; i++, frame++)
        {
            curve[frame] = (float)sustainLevel;
        }

        // Release phase: sustain level to 0
        for (int i = 0; i < releaseFrames; i++, frame++)
        {
            float t = (float)i / releaseFrames;
            curve[frame] = (float)sustainLevel * (1.0f - t);
        }
    }
}
