using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Renders a Song arrangement to a single stereo AudioBuffer by walking sections,
/// rendering sequences, mixing voices, and concatenating section buffers.
/// </summary>
public static class SongRenderer
{
    private const int DefaultSampleRate = 44100;
    private const int StereoChannels = 2;
    private const double DefaultBpm = 120.0;

    public static void Register(InternalFunctionRegistry registry)
    {
        var signature = new FunctionSignature(
            "renderSong",
            [SongType.Instance, StringType.Instance]);
        registry.Register("renderSong", signature, RenderSong);
    }

    /// <summary>
    /// Registers contextual version of renderSong that supports custom lambda instruments.
    /// </summary>
    public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var lambdaSig = new FunctionSignature(
            "renderSong",
            [SongType.Instance, TypeSystem.PrimitiveTypes.FunctionType.Instance]);
        registry.Register("renderSong", lambdaSig, args => RenderSongWithLambda(args, context));
    }

    /// <summary>
    /// renderSong(Song, Function) -> Buffer
    /// Renders a song using a custom Flow lambda as the instrument.
    /// </summary>
    private static Value RenderSongWithLambda(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var song = args[0].As<SongData>();
        var lambda = args[1].As<FunctionOverload>();

        // Plan 15-05 ROADMAP #2: deterministic synth noise across renders.
        SynthUtils.ResetNoiseRng();

        // Create a wrapper for the lambda that matches the INoteSynthesizer requirement
        var synth = new FlowFunctionSynthesizer((note, duration, bpm) =>
        {
            var noteValue = Value.MusicalNote(note);
            var durValue = Value.Double(duration);
            var bpmValue = Value.Double(bpm);

            // Call the function via the context's invoker
            var resultValue = context.Invoker!.ExecuteUserFunction(lambda.Declaration!, [noteValue, durValue, bpmValue]);
            return resultValue.As<AudioBuffer>();
        });

        AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);

        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                throw new InvalidOperationException($"renderSong: section '{sectionRef.Name}' not found in song registry");

            var sectionBuffer = RenderSection(sectionData, synth);

            for (int r = 0; r < sectionRef.RepeatCount; r++)
            {
                result = AppendBuffers(result, sectionBuffer);
            }
        }

        return Value.Buffer(result);
    }

    /// <summary>
    /// renderSong(Song, String) -> Buffer
    /// Iterates the song arrangement, renders each section, handles repeats,
    /// and concatenates all section buffers into one stereo output.
    /// </summary>
    public static Value RenderSong(IReadOnlyList<Value> args)
    {
        var song = args[0].As<SongData>();
        string synthType = (string)args[1].Data!;

        // Reset the synth white-noise RNG to its fixed seed so that two
        // renderSong calls on the same SongData produce byte-identical
        // buffers (Plan 15-05 ROADMAP criterion #2 / D-18). Pre-fix the
        // unseeded SynthUtils.Rng leaked state across renders.
        SynthUtils.ResetNoiseRng();

        AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);

        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                throw new InvalidOperationException($"renderSong: section '{sectionRef.Name}' not found in song registry");

            var sectionBuffer = RenderSection(sectionData, synthType);

            // Apply repeat count
            for (int r = 0; r < sectionRef.RepeatCount; r++)
            {
                result = AppendBuffers(result, sectionBuffer);
            }
        }

        return Value.Buffer(result);
    }

    /// <summary>
    /// Renders all sequences in a section simultaneously, mixing their voices
    /// into one stereo buffer.
    /// </summary>
    private static AudioBuffer RenderSection(SectionData section, string synthType)
    {
        return RenderSection(section, SynthesizerFactory.Create(synthType));
    }

    private static AudioBuffer RenderSection(SectionData section, INoteSynthesizer synthesizer)
    {
        double bpm = section.Context?.Tempo ?? DefaultBpm;
        double pan = section.Context?.Pan ?? 0.0;
        double gain = section.Context?.Gain ?? 1.0;
        // DX-07 / D-14: per-voice reverb reads from the section's musical context.
        // null means "no reverbTime active" → dry path. Value 0.0 is the explicit
        // dry sentinel (CONTEXT D-02) — see predicate below.
        double? rt60 = section.Context?.ReverbTime;
        var allVoices = new List<Voice>();
        double maxBeats = 0;

        foreach (var (name, sequence) in section.Sequences)
        {
            var voices = SequenceRenderer.RenderSequenceToVoices(
                sequence, synthesizer, DefaultSampleRate, bpm);
            // Apply pan and gain from musical context to all voices in this section
            foreach (var voice in voices)
            {
                if (pan != 0.0)
                    voice.Pan = pan;
                voice.Gain *= gain;
            }
            allVoices.AddRange(voices);

            if (sequence.TotalBeats > maxBeats)
                maxBeats = sequence.TotalBeats;
        }

        // D-02, D-14: per-voice reverb when reverbTime context is active AND non-zero.
        // Exact `!= 0.0` comparison (no epsilon) per RESEARCH Pitfall 3 — the parser
        // produces the literal value unchanged, so `reverbTime 0` stores exactly 0.0
        // and short-circuits here, while `reverbTime 0.0001` passes through as a
        // near-dry-but-not-dry reverb (D-03 pass-through band).
        if (rt60.HasValue && rt60.Value != 0.0)
        {
            for (int i = 0; i < allVoices.Count; i++)
            {
                var v = allVoices[i];
                // Voice.Buffer is get-only (see Voice.cs:11), so we construct a new
                // Voice with the reverb-wetted buffer and copy OffsetBeats/Gain/Pan.
                var wetBuffer = Reverb.Apply(v.Buffer, rt60.Value, damping: 0.5f, mix: 0.3f);  // D-15 defaults
                var replaced = new Voice(wetBuffer, v.OffsetBeats);
                replaced.Gain = v.Gain;
                replaced.Pan = v.Pan;
                allVoices[i] = replaced;
            }
        }

        if (allVoices.Count == 0 || maxBeats <= 0)
            return new AudioBuffer(0, StereoChannels, DefaultSampleRate);

        return MixVoicesToStereoBuffer(allVoices, bpm, DefaultSampleRate, maxBeats);
    }

    /// <summary>
    /// Positions and mixes a list of voices into a stereo AudioBuffer.
    /// </summary>
    internal static AudioBuffer MixVoicesToStereoBuffer(
        List<Voice> voices, double bpm, int sampleRate, double totalBeats)
    {
        double secondsPerBeat = 60.0 / bpm;
        int totalFrames = (int)(totalBeats * secondsPerBeat * sampleRate);
        var result = new AudioBuffer(totalFrames, StereoChannels, sampleRate);

        foreach (var voice in voices)
        {
            int voiceStartFrame = (int)(voice.OffsetBeats * secondsPerBeat * sampleRate);

            // Constant-power panning using voice.Pan (D-05, D-08 bug fix)
            float panAngle = (float)((voice.Pan + 1.0) * 0.25 * Math.PI);
            float leftGain = MathF.Cos(panAngle) * (float)voice.Gain;
            float rightGain = MathF.Sin(panAngle) * (float)voice.Gain;

            for (int frame = 0; frame < voice.Buffer.Frames; frame++)
            {
                int destFrame = voiceStartFrame + frame;
                if (destFrame < 0 || destFrame >= totalFrames) continue;

                // Get mono sample from voice (downmix if stereo)
                float sample;
                if (voice.Buffer.Channels == 1)
                {
                    sample = voice.Buffer.GetSample(frame, 0);
                }
                else
                {
                    sample = 0f;
                    for (int ch = 0; ch < voice.Buffer.Channels; ch++)
                        sample += voice.Buffer.GetSample(frame, ch);
                    sample /= voice.Buffer.Channels;
                }

                result.SetSample(destFrame, 0, result.GetSample(destFrame, 0) + sample * leftGain);
                result.SetSample(destFrame, 1, result.GetSample(destFrame, 1) + sample * rightGain);
            }
        }

        return result;
    }

    /// <summary>
    /// Renders a Song to an AudioBuffer and a TimelineMap for editor live highlighting.
    /// </summary>
    public static (AudioBuffer Buffer, TimelineMap Timeline) RenderSongWithTimeline(SongData song, string synthType)
    {
        // Plan 15-05 ROADMAP #2: deterministic synth noise across renders.
        SynthUtils.ResetNoiseRng();

        var timelineMap = new TimelineMap();
        AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);
        double accumulatedSeconds = 0;

        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                throw new InvalidOperationException($"renderSong: section '{sectionRef.Name}' not found in song registry");

            var (sectionBuffer, sectionTimeline) = RenderSectionWithTimeline(sectionData, synthType);

            for (int r = 0; r < sectionRef.RepeatCount; r++)
            {
                // Offset this repeat's timeline entries
                var repeatTimeline = new TimelineMap();
                foreach (var entry in sectionTimeline.Entries)
                {
                    repeatTimeline.Add(entry with
                    {
                        StartSeconds = entry.StartSeconds + accumulatedSeconds,
                        EndSeconds = entry.EndSeconds + accumulatedSeconds
                    });
                }
                timelineMap.Merge(repeatTimeline);

                // Add section-level entry if source location is available
                if (sectionData.SourceLocation != null)
                {
                    double sectionDuration = (double)sectionBuffer.Frames / sectionBuffer.SampleRate;
                    timelineMap.Add(new TimelineEntry(
                        accumulatedSeconds,
                        accumulatedSeconds + sectionDuration,
                        sectionData.SourceLocation,
                        sectionData.Name.Length + "section ".Length,
                        $"section:{sectionData.Name}"));
                }

                accumulatedSeconds += (double)sectionBuffer.Frames / sectionBuffer.SampleRate;
                result = AppendBuffers(result, sectionBuffer);
            }
        }

        return (result, timelineMap);
    }

    /// <summary>
    /// Timeline-aware version of RenderSection.
    /// </summary>
    private static (AudioBuffer Buffer, TimelineMap Timeline) RenderSectionWithTimeline(SectionData section, string synthType)
    {
        double bpm = section.Context?.Tempo ?? DefaultBpm;
        double pan = section.Context?.Pan ?? 0.0;
        double gain = section.Context?.Gain ?? 1.0;
        var allVoices = new List<Voice>();
        double maxBeats = 0;
        var timelineMap = new TimelineMap();
        string scopeName = $"note:{section.Name}";

        foreach (var (name, sequence) in section.Sequences)
        {
            var voices = SequenceRenderer.RenderSequenceToVoices(
                sequence, synthType, DefaultSampleRate, bpm, timelineMap, scopeName);
            // Apply pan and gain from musical context to all voices in this section
            foreach (var voice in voices)
            {
                if (pan != 0.0)
                    voice.Pan = pan;
                voice.Gain *= gain;
            }
            allVoices.AddRange(voices);

            if (sequence.TotalBeats > maxBeats)
                maxBeats = sequence.TotalBeats;
        }

        if (allVoices.Count == 0 || maxBeats <= 0)
            return (new AudioBuffer(0, StereoChannels, DefaultSampleRate), timelineMap);

        return (MixVoicesToStereoBuffer(allVoices, bpm, DefaultSampleRate, maxBeats), timelineMap);
    }

    /// <summary>
    /// Concatenates two AudioBuffers end-to-end via Array.Copy.
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
