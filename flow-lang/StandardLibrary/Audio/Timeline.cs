using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Timeline and composition functions for DAW-like multitrack audio.
/// </summary>
public static class Timeline
{
    // ===== BPM Context (thread-local state) =====
    [ThreadStatic] private static double? _currentBPM;
    private static double CurrentBPM
    {
        get => _currentBPM ?? 120.0;
        set => _currentBPM = value;
    }

    /// <summary>
    /// Sets the global BPM (beats per minute) for timeline conversions.
    /// </summary>
    public static Value SetBPM(IReadOnlyList<Value> args)
    {
        CurrentBPM = args[0].As<double>();
        if (CurrentBPM <= 0)
            throw new ArgumentException("BPM must be positive");
        return Value.Void();
    }

    /// <summary>
    /// Gets the current global BPM.
    /// </summary>
    public static Value GetBPM(IReadOnlyList<Value> args)
    {
        return Value.Double(CurrentBPM);
    }

    // ===== Time Conversions =====

    /// <summary>
    /// Converts beats to frames based on current BPM and sample rate.
    /// </summary>
    public static Value BeatsToFrames(IReadOnlyList<Value> args)
    {
        double beats = args[0].As<double>();
        int sampleRate = args[1].As<int>();

        double secondsPerBeat = 60.0 / CurrentBPM;
        double seconds = beats * secondsPerBeat;
        int frames = (int)(seconds * sampleRate);

        return Value.Int(frames);
    }

    /// <summary>
    /// Converts frames to beats based on current BPM and sample rate.
    /// </summary>
    public static Value FramesToBeats(IReadOnlyList<Value> args)
    {
        int frames = args[0].As<int>();
        int sampleRate = args[1].As<int>();

        double seconds = (double)frames / sampleRate;
        double secondsPerBeat = 60.0 / CurrentBPM;
        double beats = seconds / secondsPerBeat;

        return Value.Double(beats);
    }

    // ===== Voice Management =====

    /// <summary>
    /// Creates a voice with a buffer positioned at a beat offset.
    /// </summary>
    public static Value CreateVoice(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        double offsetBeats = args[1].As<double>();

        var voice = new Voice(buffer, offsetBeats);
        return Value.Voice(voice);
    }

    /// <summary>
    /// Sets the gain of a voice.
    /// </summary>
    public static Value SetVoiceGain(IReadOnlyList<Value> args)
    {
        var voice = args[0].As<Voice>();
        double gain = args[1].As<double>();

        voice.Gain = gain;
        return Value.Void();
    }

    /// <summary>
    /// Sets the pan of a voice.
    /// </summary>
    public static Value SetVoicePan(IReadOnlyList<Value> args)
    {
        var voice = args[0].As<Voice>();
        double pan = args[1].As<double>();

        voice.Pan = Math.Clamp(pan, -1.0, 1.0);
        return Value.Void();
    }

    /// <summary>
    /// Sets the offset of a voice in beats.
    /// </summary>
    public static Value SetVoiceOffset(IReadOnlyList<Value> args)
    {
        var voice = args[0].As<Voice>();
        double offset = args[1].As<double>();
        voice.OffsetBeats = offset;
        return Value.Void();
    }

    // ===== Track Management =====

    /// <summary>
    /// Creates a new track with specified sample rate and channels.
    /// </summary>
    public static Value CreateTrack(IReadOnlyList<Value> args)
    {
        int sampleRate = args[0].As<int>();
        int channels = args[1].As<int>();

        var track = new Track(sampleRate, channels);
        return Value.Track(track);
    }

    /// <summary>
    /// Adds a voice to a track.
    /// </summary>
    public static Value AddVoice(IReadOnlyList<Value> args)
    {
        var track = args[0].As<Track>();
        var voice = args[1].As<Voice>();

        track.Voices.Add(voice);
        return Value.Void();
    }

    /// <summary>
    /// Sets the offset of a track in beats.
    /// </summary>
    public static Value SetTrackOffset(IReadOnlyList<Value> args)
    {
        var track = args[0].As<Track>();
        double offsetBeats = args[1].As<double>();

        track.OffsetBeats = offsetBeats;
        return Value.Void();
    }

    /// <summary>
    /// Sets the gain of a track.
    /// </summary>
    public static Value SetTrackGain(IReadOnlyList<Value> args)
    {
        var track = args[0].As<Track>();
        double gain = args[1].As<double>();

        track.Gain = gain;
        return Value.Void();
    }

    /// <summary>
    /// Sets the pan of a track.
    /// </summary>
    public static Value SetTrackPan(IReadOnlyList<Value> args)
    {
        var track = args[0].As<Track>();
        double pan = args[1].As<double>();

        track.Pan = Math.Clamp(pan, -1.0, 1.0);
        return Value.Void();
    }

    // ===== Rendering =====

    /// <summary>
    /// Renders a track to a buffer with specified duration in beats.
    /// </summary>
    public static Value RenderTrack(IReadOnlyList<Value> args)
    {
        var track = args[0].As<Track>();
        double durationBeats = args[1].As<double>();

        // Convert beats to frames
        double secondsPerBeat = 60.0 / CurrentBPM;
        double totalSeconds = durationBeats * secondsPerBeat;
        int totalFrames = (int)(totalSeconds * track.SampleRate);

        var result = new AudioBuffer(totalFrames, track.Channels, track.SampleRate);

        // Render each voice at its offset position
        foreach (var voice in track.Voices)
        {
            double voiceOffsetBeats = track.OffsetBeats + voice.OffsetBeats;
            int voiceStartFrame = (int)(voiceOffsetBeats * secondsPerBeat * track.SampleRate);

            // Mix voice into result with gain and pan
            for (int frame = 0; frame < voice.Buffer.Frames; frame++)
            {
                int destFrame = voiceStartFrame + frame;
                if (destFrame < 0 || destFrame >= totalFrames) continue;

                for (int ch = 0; ch < voice.Buffer.Channels && ch < track.Channels; ch++)
                {
                    float sample = voice.Buffer.GetSample(frame, ch);

                    // Apply voice and track gain
                    sample *= (float)(voice.Gain * track.Gain);

                    // Apply pan (stereo only)
                    if (track.Channels == 2)
                    {
                        sample *= CalculatePanGain(ch, voice.Pan, track.Pan);
                    }

                    // Mix into output
                    float existing = result.GetSample(destFrame, ch);
                    result.SetSample(destFrame, ch, existing + sample);
                }
            }
        }

        return Value.Buffer(result);
    }

    /// <summary>
    /// Calculates pan gain for a channel based on voice and track pan.
    /// </summary>
    private static float CalculatePanGain(int channel, double voicePan, double trackPan)
    {
        // Combine voice and track pan (average)
        double totalPan = (voicePan + trackPan) / 2.0;
        totalPan = Math.Clamp(totalPan, -1.0, 1.0);

        // Equal power panning
        if (channel == 0) // Left channel
        {
            return (float)Math.Cos((totalPan + 1.0) * Math.PI / 4.0);
        }
        else // Right channel
        {
            return (float)Math.Sin((totalPan + 1.0) * Math.PI / 4.0);
        }
    }
}
