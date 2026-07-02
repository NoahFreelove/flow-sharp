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

        // Quick 260701-vqz: return the (mutated) buffer — matches the documented
        // Buffer return (:help + Standard-Library.md) and enables `->` chaining;
        // previously returned Void so `Buffer b = (applyEnvelope ...)` bound null.
        return args[0];
    }

    /// <summary>
    /// Generates an envelope curve as an array of amplitude multipliers.
    /// </summary>
    public static float[] GenerateEnvelopeCurve(Envelope envelope, int totalFrames)
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

        // AUDIT-VERIFIED 2026-04-18: C3 — Dismissed: loop body only runs when frames > 0; see tests/spike/c3-envelope-short-segments.flow
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

        // QUICK-260504-v6j: when attack + decay + release exceeds totalFrames,
        // scale all three down proportionally so the envelope SHAPE is preserved
        // (just compressed in time) and the release phase is guaranteed to remain
        // > 0 for any reasonable totalFrames. The previous logic clamped release
        // to totalFrames - attack - decay, which collapsed release to 0 frames
        // for short notes (32nd-note staccato, MIDI-imported quick passages) and
        // produced an audible click at the final sample because the envelope
        // ended on the sustain level instead of ramping to 0.
        int requestedAdr = attackFrames + decayFrames + releaseFrames;
        if (requestedAdr > totalFrames)
        {
            // Whether the caller actually asked for a release ramp. This governs
            // where the floor-rounding leftover goes (see below).
            bool hasRelease = releaseFrames > 0;

            double scale = (double)totalFrames / requestedAdr;
            attackFrames = (int)(attackFrames * scale);
            decayFrames = (int)(decayFrames * scale);
            releaseFrames = (int)(releaseFrames * scale);

            // Floor-rounding can leave 1-3 frames unallocated. Where they go depends
            // on whether a release was requested:
            //   • release > 0 (synth / SFZ / drum paths — the note ends at 0 because
            //     NO tail follows): give the leftover to release so the envelope ends
            //     at exactly 0 on the final sample (QUICK-260504-v6j — no cliff).
            //   • release == 0 (sampled-instrument path — an exponential release TAIL
            //     is appended downstream that restarts at the sustain level): the
            //     leftover must STAY IN SUSTAIN so the envelope ends at the sustain
            //     level and meets that tail CONTINUOUSLY. Routing it to release here
            //     would dip the last 1-3 authored frames to ~0 while the tail jumps
            //     back to full amplitude — a per-note step discontinuity that stacks
            //     into audible per-beat static on dense short-note passages (debug
            //     session varispeed-aliasing-static, 2026-06-26).
            int leftover = totalFrames - (attackFrames + decayFrames + releaseFrames);
            if (hasRelease)
                releaseFrames += leftover;
            // else: sustainFrames (computed below) absorbs the leftover; release stays 0.
        }
        // (No clamps needed in the else branch — sustain absorbs the remainder.)

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

        // Release phase: sustain level to 0.
        // Use t = (i+1)/N so the final sample (i = N-1) writes exactly 0 instead
        // of sustain/N. Without this, even with a non-zero release frame budget
        // the curve ends on a small but non-zero value, which contradicts the
        // QUICK-260504-v6j must_have ("ends on amplitude 0.0 — no abrupt non-zero
        // cutoff") and is a half-sample-shaped residue at the end of every note.
        for (int i = 0; i < releaseFrames; i++, frame++)
        {
            float t = (float)(i + 1) / releaseFrames;
            curve[frame] = (float)sustainLevel * (1.0f - t);
        }
    }
}
