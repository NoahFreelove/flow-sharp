using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.StandardLibrary.Harmony;
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

        // Phase 29 REQ-4 — lambda-instrument calls don't reference the sample bundle
        // (custom Flow function does its own rendering), but the eager-load call is
        // harmless: SampleCache.EagerLoad no-ops for unknown instrument names
        // (lambda has no name → empty string → InstrumentManifest miss → return).
        // We keep the call for code-path uniformity across the three RenderSong* entries.
        FlowEngine.CurrentSampleCache?.EagerLoad(song, string.Empty);

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

        // Phase 29 REQ-4 — eager-load instrument samples for this song. Idempotent
        // for repeated (song, instrument) within an engine lifetime. No-op for
        // non-sampled instruments (drums/organ/wavetable) and when no FlowEngine
        // owns the active cache (e.g. direct-API SongRenderer calls bypassing
        // FlowEngine — preserves pre-Phase-29 backward compatibility).
        FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType);

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

    /// <summary>
    /// Phase 23: resolves the per-section <see cref="RenderTuning"/> from the section's
    /// <see cref="MusicalContext"/>. Same shape as bpm / pan / gain / rt60 resolution at
    /// the head of <see cref="RenderSection"/>: read once per section before any voices
    /// are rendered so the same tuning context applies to every note.
    ///
    /// Decisions:
    ///   D-02 silent C-major default — when a non-12-TET pragma is active but no
    ///        <c>key</c> block is in scope, root at C major (tonic = ('C', 0),
    ///        mode = Major). Aligns with charitable-interpretation memory: rather than
    ///        error or fall through to 12-TET, render the JI / Pythagorean ratios with
    ///        a sensible default anchor.
    ///   D-01 — tonic letter + alteration come from the innermost active key.
    ///   D-08 — when ctx.Tuning is null OR EqualTemperament, return RenderTuning.Default
    ///        so the byte-identical 12-TET short-circuit fires at the synthesizer level
    ///        (Pitfall 6).
    /// Canonical entry: uses <see cref="ScaleDatabase.TryParseKeyWithMode"/> rather than
    /// an inline parser (per WARNING-8 — no inline write-then-delete helper).
    /// </summary>
    internal static RenderTuning ResolveRenderTuning(MusicalContext? ctx)
    {
        if (ctx?.Tuning is null || ctx.Tuning == TuningSystem.EqualTemperament)
            return RenderTuning.Default;

        // D-02 silent C-major default (tonic = ('C', 0), mode = Major).
        char tonicLetter = 'C';
        int tonicAlteration = 0;
        Mode mode = Mode.Major;
        if (!string.IsNullOrEmpty(ctx.Key) &&
            ScaleDatabase.TryParseKeyWithMode(ctx.Key, out string? root, out var parsedMode) &&
            root != null)
        {
            // root is canonical-cased: e.g. "C", "Csharp", "Db".
            tonicLetter = root[0];
            if (root.Length >= 2)
            {
                if (root[1] == 'b') tonicAlteration = -1;
                else if (root.EndsWith("sharp", System.StringComparison.OrdinalIgnoreCase)) tonicAlteration = +1;
            }
            mode = parsedMode;
        }
        return new RenderTuning(ctx.Tuning.Value, mode, tonicLetter, tonicAlteration);
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
        // Phase 23 D-06/D-08: resolve once per section. Default short-circuits the
        // byte-identical 12-TET path via Pitfall 6.
        var renderTuning = ResolveRenderTuning(section.Context);
        var allVoices = new List<Voice>();
        double maxBeats = 0;

        foreach (var (name, sequence) in section.Sequences)
        {
            // Phase 28 SPEC-7: route through the voice-pool overload — uses the
            // section's `voicePool N { ... }` override when one is in scope, else
            // the locked default of 32 voices via steal-oldest. Legacy loudest-N
            // policy is preserved for direct callers via RenderSequenceToVoices.
            var voices = SequenceRenderer.RenderSequenceToVoicesWithPool(
                sequence, synthesizer, DefaultSampleRate, bpm, renderTuning,
                section.Context?.VoicePoolSize);
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

        // Phase 29 REQ-4 — eager-load samples for the timeline-aware render path too.
        // Same idempotency / no-op semantics as RenderSong.
        FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType);

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
        // Phase 23: per-section tuning resolution at the timeline-aware path too. The
        // existing SequenceRenderer.RenderSequenceToVoices(string, ..., timelineMap)
        // overload threads through BarRenderer overloads that are not yet tuning-aware
        // for the timeline path; this is safe because RenderTuning.Default is taken when
        // ctx.Tuning is null or EqualTemperament, and the timeline path is currently used
        // by the editor/LSP integration which doesn't render to WAV.
        var renderTuning = ResolveRenderTuning(section.Context);
        var allVoices = new List<Voice>();
        double maxBeats = 0;
        var timelineMap = new TimelineMap();
        string scopeName = $"note:{section.Name}";

        foreach (var (name, sequence) in section.Sequences)
        {
            // Note: timeline path keeps existing string-overload to preserve BarRenderer
            // timeline-recording behavior. Tuning is captured but not yet threaded through
            // the timeline overload chain — Wave 3 widening if user-facing audio diff
            // matters via this path. The renderTuning local is materialized so anyone
            // grep-auditing this path sees the resolution happens.
            _ = renderTuning;
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
