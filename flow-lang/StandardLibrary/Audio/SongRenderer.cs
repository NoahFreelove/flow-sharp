using FlowLang.Audio;
using FlowLang.Runtime;
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
    /// renderSong(Song, String) -> Buffer
    /// Iterates the song arrangement, renders each section, handles repeats,
    /// and concatenates all section buffers into one stereo output.
    /// </summary>
    public static Value RenderSong(IReadOnlyList<Value> args)
    {
        var song = args[0].As<SongData>();
        string synthType = (string)args[1].Data!;

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
        double bpm = section.Context?.Tempo ?? DefaultBpm;
        var allVoices = new List<Voice>();
        double maxBeats = 0;

        foreach (var (name, sequence) in section.Sequences)
        {
            var voices = SequenceRenderer.RenderSequenceToVoices(
                sequence, synthType, DefaultSampleRate, bpm);
            allVoices.AddRange(voices);

            if (sequence.TotalBeats > maxBeats)
                maxBeats = sequence.TotalBeats;
        }

        if (allVoices.Count == 0 || maxBeats <= 0)
            return new AudioBuffer(0, StereoChannels, DefaultSampleRate);

        return MixVoicesToStereoBuffer(allVoices, bpm, DefaultSampleRate, maxBeats);
    }

    /// <summary>
    /// Positions and mixes a list of voices into a stereo AudioBuffer.
    /// </summary>
    private static AudioBuffer MixVoicesToStereoBuffer(
        List<Voice> voices, double bpm, int sampleRate, double totalBeats)
    {
        double secondsPerBeat = 60.0 / bpm;
        int totalFrames = (int)(totalBeats * secondsPerBeat * sampleRate);
        var result = new AudioBuffer(totalFrames, StereoChannels, sampleRate);

        foreach (var voice in voices)
        {
            int voiceStartFrame = (int)(voice.OffsetBeats * secondsPerBeat * sampleRate);

            for (int frame = 0; frame < voice.Buffer.Frames; frame++)
            {
                int destFrame = voiceStartFrame + frame;
                if (destFrame < 0 || destFrame >= totalFrames) continue;

                for (int ch = 0; ch < voice.Buffer.Channels && ch < StereoChannels; ch++)
                {
                    float sample = voice.Buffer.GetSample(frame, ch);
                    sample *= (float)voice.Gain;

                    float existing = result.GetSample(destFrame, ch);
                    result.SetSample(destFrame, ch, existing + sample);
                }

                // If voice is mono but output is stereo, duplicate to right channel
                if (voice.Buffer.Channels == 1 && StereoChannels == 2)
                {
                    float sample = voice.Buffer.GetSample(frame, 0);
                    sample *= (float)voice.Gain;

                    float existing = result.GetSample(destFrame, 1);
                    result.SetSample(destFrame, 1, existing + sample);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Renders a Song to an AudioBuffer and a TimelineMap for editor live highlighting.
    /// </summary>
    public static (AudioBuffer Buffer, TimelineMap Timeline) RenderSongWithTimeline(SongData song, string synthType)
    {
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
        var allVoices = new List<Voice>();
        double maxBeats = 0;
        var timelineMap = new TimelineMap();
        string scopeName = $"note:{section.Name}";

        foreach (var (name, sequence) in section.Sequences)
        {
            var voices = SequenceRenderer.RenderSequenceToVoices(
                sequence, synthType, DefaultSampleRate, bpm, timelineMap, scopeName);
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
