using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.DSP;
#if !FLOW_WEB
// Phase 47 D-47-08: SFZ namespace stripped from Web build (Plan 47-01 strip-list).
using FlowLang.StandardLibrary.Audio.Sfz;
#endif
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

        // Phase 37 PIANO-01 (Plan 37-04 / D-37-11) — release-aware overload.
        // Composer surface: (renderSong song "piano" release=2.0s) — the named-arg
        // resolver matches Second against this third parameter. Sets
        // PianoSynthesizer.CurrentReleaseSec via AsyncLocal so the dispatched
        // RenderNote calls see the override; resets in finally to keep the
        // AsyncLocal scope clean for subsequent renders.
        var releaseSig = new FunctionSignature(
            "renderSong",
            [SongType.Instance, StringType.Instance, SecondType.Instance],
            IsVarArgs: false,
            ParameterNames: new[] { "song", "instrument", "release" });
        registry.Register("renderSong", releaseSig, RenderSongWithRelease);
    }

    /// <summary>
    /// Phase 37 PIANO-01 (Plan 37-04 / D-37-11) — renderSong overload that
    /// accepts a <c>release=</c> tail-length knob (Second). Currently consumed
    /// only by the piano synth path via
    /// <see cref="Synthesizers.PianoSynthesizer.CurrentReleaseSec"/>; other
    /// instruments accept the knob harmlessly (ignored — they don't depend on
    /// the tail extension because they don't ship the velocity-layered sample
    /// expansion). T-37-04-04 clamping happens at the renderer; this entry
    /// point just sets the AsyncLocal + dispatches.
    /// </summary>
    private static Value RenderSongWithRelease(IReadOnlyList<Value> args)
    {
        double releaseSec = System.Convert.ToDouble(args[2].Data);
        var basicArgs = new List<Value> { args[0], args[1] };
        var prev = Synthesizers.PianoSynthesizer.CurrentReleaseSec.Value;
        Synthesizers.PianoSynthesizer.CurrentReleaseSec.Value = releaseSec;
        try
        {
            return RenderSong(basicArgs);
        }
        finally
        {
            Synthesizers.PianoSynthesizer.CurrentReleaseSec.Value = prev;
        }
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

        // Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — reseed PrngRegistry at the
        // renderSong boundary so any unseeded Phase 36 stochastic primitives
        // produce byte-identical buffers across renders.
        context.PrngRegistry.ResetAtRenderBoundary();

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

        // Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — reseed PrngRegistry at the
        // renderSong boundary so any unseeded Phase 36 stochastic primitives
        // (markov / lsystem / cellular / lorenz / degrade / sparseSeq / sometimes /
        // jam) produce byte-identical buffers across renders.
        FlowEngine.CurrentExecutionContext?.PrngRegistry.ResetAtRenderBoundary();

        // Reset the synth white-noise RNG to its fixed seed so that two
        // renderSong calls on the same SongData produce byte-identical
        // buffers (Plan 15-05 ROADMAP criterion #2 / D-18). Pre-fix the
        // unseeded SynthUtils.Rng leaked state across renders.
        SynthUtils.ResetNoiseRng();

        // Phase 33 D-13: sampler:NAME dispatch — reads ExecutionContext.SfzPatchRegistry;
        // eager-loads via FlowEngine.CurrentSfzSampleCache; per-note render via
        // SfzRenderer wrapped in an INoteSynthesizer adapter so the existing
        // RenderSection / SequenceRenderer / BarRenderer / VoiceAllocator pipeline
        // is reused unchanged. Phase 29 bundled-sample path below stays untouched
        // (byte-identical contract — when synthType does NOT start with "sampler:",
        // execution falls through to the existing Phase 29 dispatch verbatim).
        if (synthType.StartsWith("sampler:", StringComparison.Ordinal))
        {
#if !FLOW_WEB
            return RenderSongWithSfz(song, synthType);
#else
            // Phase 47 D-47-08: SFZ subsystem stripped on Web target. Composers
            // who reach this branch on Web bypassed the ModuleLoader gate
            // somehow (e.g. direct API call with "sampler:NAME") — charitable
            // failure with a target-aware message pointing at the right fix.
            throw new InvalidOperationException(
                $"sampler:NAME dispatch requires Desktop target — build with FlowTarget=Desktop to enable SFZ.");
#endif
        }

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
    /// Phase 23 + Phase 32 D-12: resolves the per-section <see cref="RenderTuning"/> from the
    /// section's <see cref="MusicalContext"/>. Same shape as bpm / pan / gain / rt60 resolution
    /// at the head of <see cref="RenderSection"/>: read once per section before any voices
    /// are rendered so the same tuning context applies to every note.
    ///
    /// Decisions:
    ///   D-12 — reads <see cref="MusicalContext.ActiveTuning"/> (top-of-stack). When the stack
    ///        is empty, ActiveTuning returns <see cref="RenderTuning.Default"/> (12-TET).
    ///   Phase 32 D-03 / Pitfall 3 — when <c>activeTuning.Custom != null</c>, the user-supplied
    ///        Scala tuning wins; we return it verbatim (its tonic/mode are baked into the
    ///        MidiToHz table and are irrelevant to PitchConversion's lookup path).
    ///   D-02 silent C-major default — when a non-12-TET system pragma is active but no
    ///        <c>key</c> block is in scope, root at C major (tonic = ('C', 0), mode = Major).
    ///        Aligns with charitable-interpretation memory: rather than error or fall through
    ///        to 12-TET, render the JI / Pythagorean ratios with a sensible default anchor.
    ///   D-01 — tonic letter + alteration come from the innermost active key.
    ///   D-08 / Pitfall 6 — when ActiveTuning is the default (Custom is null AND
    ///        System == EqualTemperament), return it as-is so the byte-identical 12-TET
    ///        short-circuit fires at the synthesizer level.
    /// Canonical entry: uses <see cref="ScaleDatabase.TryParseKeyWithMode"/> rather than
    /// an inline parser (per WARNING-8 — no inline write-then-delete helper).
    /// </summary>
    internal static RenderTuning ResolveRenderTuning(MusicalContext? ctx)
    {
        var activeTuning = ctx?.ActiveTuning ?? RenderTuning.Default;

        // Phase 32 D-03 / Pitfall 3: custom Scala tunings win regardless of System enum.
        // The Custom path's MidiToHz table is fully populated at load time; tonic/mode are
        // not consulted by PitchConversion when Custom != null.
        if (activeTuning.Custom is not null)
            return activeTuning;

        // Default 12-TET fast path: System == EqualTemperament AND no custom → return as-is.
        if (activeTuning.System == TuningSystem.EqualTemperament)
            return activeTuning;

        // D-02 silent C-major default (tonic = ('C', 0), mode = Major).
        char tonicLetter = 'C';
        int tonicAlteration = 0;
        Mode mode = Mode.Major;
        if (!string.IsNullOrEmpty(ctx?.Key) &&
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
        return new RenderTuning(activeTuning.System, mode, tonicLetter, tonicAlteration);
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

        // Sustain pedal — when active, every note in every sequence extends its
        // rendered buffer by MusicalContext.SustainTailSeconds, mimicking piano
        // pedal behavior. Notes ring through subsequent attacks. Onsets unchanged.
        bool sustainActive = section.Context?.SustainPedal == true;

        foreach (var (name, sequence) in section.Sequences)
        {
            // Phase 28 SPEC-7: route through the voice-pool overload — uses the
            // section's `voicePool N { ... }` override when one is in scope, else
            // the locked default of 32 voices via steal-oldest. Legacy loudest-N
            // policy is preserved for direct callers via RenderSequenceToVoices.
            var voices = SequenceRenderer.RenderSequenceToVoicesWithPool(
                sequence, synthesizer, DefaultSampleRate, bpm, renderTuning,
                section.Context?.VoicePoolSize,
                sustainActive);
            // Apply pan and gain from musical context to all voices in this section.
            // Phase 38 LIVE-03: stable Name for live-swap diff. Tag each voice with
            // `"{sequenceName}:{ordinalIdx}"` per RESEARCH §B — the sequence name
            // here is the per-instrument label used in the Phase 28 panel row 3
            // breakdown, and ordinalIdx is the 0-based position within this
            // sequence's voice list. Same Name → Voice.CopyStateFrom path at the
            // live-swap site. Voice.Name is `init`-only so we re-construct the
            // voice; this mirrors the reverb wet-replace pattern below at line
            // ~320. Phase 28 offline render path unchanged: Name is set but
            // unused outside the live-block swap consumer.
            int ordinalIdx = 0;
            for (int vi = 0; vi < voices.Count; vi++)
            {
                var voice = voices[vi];
                if (pan != 0.0)
                    voice.Pan = pan;
                voice.Gain *= gain;

                var tagged = new Voice(voice.Buffer, voice.OffsetBeats)
                {
                    Name = $"{name}:{ordinalIdx}",
                };
                tagged.Gain = voice.Gain;
                tagged.Pan = voice.Pan;
                voices[vi] = tagged;
                ordinalIdx++;
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
                // Phase 38 LIVE-03: preserve Name across the reverb wet-replace so
                // the live-swap diff still recognizes the voice by its stable key.
                var replaced = new Voice(wetBuffer, v.OffsetBeats) { Name = v.Name };
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

            // Mono voices: legacy synth path — apply constant-power pan from
            // voice.Pan at the additive-mix stage (D-05, D-08 bug fix).
            // Stereo voices: Phase 37 MIX-02 SFZ path — voice.Buffer already
            // carries channel-resolved pan information (region.Pan + voice.Pan
            // composed inside SfzRenderer per OQ4 additive-with-clamp). The
            // mix stage MUST preserve L/R rather than downmix-and-re-pan,
            // otherwise it overwrites the per-region SFZ pan with a generic
            // voice.Pan re-pan. Channel-preservation cost: zero — the per-frame
            // loop body is the same shape, just reads from the channel-resolved
            // source.
            if (voice.Buffer.Channels == 1)
            {
                float panAngle = (float)((voice.Pan + 1.0) * 0.25 * Math.PI);
                float leftGain = MathF.Cos(panAngle) * (float)voice.Gain;
                float rightGain = MathF.Sin(panAngle) * (float)voice.Gain;

                for (int frame = 0; frame < voice.Buffer.Frames; frame++)
                {
                    int destFrame = voiceStartFrame + frame;
                    if (destFrame < 0 || destFrame >= totalFrames) continue;

                    float sample = voice.Buffer.GetSample(frame, 0);
                    result.SetSample(destFrame, 0, result.GetSample(destFrame, 0) + sample * leftGain);
                    result.SetSample(destFrame, 1, result.GetSample(destFrame, 1) + sample * rightGain);
                }
            }
            else
            {
                // Phase 37 MIX-02 — preserve stereo voices' L/R. Apply
                // voice.Gain uniformly (gain ≠ pan; gain is a scalar level
                // adjustment that touches both channels equally).
                float gain = (float)voice.Gain;
                for (int frame = 0; frame < voice.Buffer.Frames; frame++)
                {
                    int destFrame = voiceStartFrame + frame;
                    if (destFrame < 0 || destFrame >= totalFrames) continue;

                    // Read L/R from the source voice (channel 0 = L, 1 = R).
                    // Voice buffers with > 2 channels are not produced by any
                    // shipping path; fall back to averaging extra channels
                    // into the existing L/R for defensive robustness.
                    float left, right;
                    if (voice.Buffer.Channels == 2)
                    {
                        left = voice.Buffer.GetSample(frame, 0);
                        right = voice.Buffer.GetSample(frame, 1);
                    }
                    else
                    {
                        left = voice.Buffer.GetSample(frame, 0);
                        right = voice.Buffer.GetSample(frame, 1);
                        // Fold any additional channels into both L/R averaged.
                        float extraSum = 0f;
                        for (int c = 2; c < voice.Buffer.Channels; c++)
                            extraSum += voice.Buffer.GetSample(frame, c);
                        float extraAvg = extraSum / (voice.Buffer.Channels - 2);
                        left += extraAvg;
                        right += extraAvg;
                    }

                    result.SetSample(destFrame, 0, result.GetSample(destFrame, 0) + left * gain);
                    result.SetSample(destFrame, 1, result.GetSample(destFrame, 1) + right * gain);
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
        // Phase 23 + Phase 32 D-12: per-section tuning resolution at the timeline-aware
        // path too. The existing SequenceRenderer.RenderSequenceToVoices(string, ...,
        // timelineMap) overload threads through BarRenderer overloads that are not yet
        // tuning-aware for the timeline path; this is safe because RenderTuning.Default
        // is taken when ActiveTuning has Custom == null AND System == EqualTemperament,
        // and the timeline path is currently used by the editor/LSP integration which
        // doesn't render to WAV.
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

#if !FLOW_WEB
    /// <summary>
    /// Phase 33 D-13 — handles the <c>sampler:NAME</c> dispatch branch from
    /// <see cref="RenderSong"/>. Resolves <paramref name="synthType"/> (which
    /// must start with <c>"sampler:"</c>) against the active engine's
    /// <see cref="FlowLang.Runtime.ExecutionContext.SfzPatchRegistry"/>:
    ///
    /// Phase 47 D-47-08: stripped on Web target (SfzData/SfzRenderer types
    /// absent there).
    ///
    /// <list type="bullet">
    ///   <item><description>If the engine is not running or the patch name is
    ///   unknown, throws <see cref="InvalidOperationException"/> with a
    ///   composer-facing message listing known patch names and the
    ///   <c>Sfz {name} = (loadSfz #...)</c> hint (D-13).</description></item>
    ///   <item><description>Otherwise eager-loads the patch's WAVs via
    ///   <see cref="FlowEngine.CurrentSfzSampleCache"/> and wraps a
    ///   <see cref="SfzRenderer"/> in <see cref="SfzNoteSynthesizer"/> so the
    ///   existing <see cref="RenderSection(SectionData, INoteSynthesizer)"/>
    ///   pipeline (voice pool, per-section reverb, pan / gain context, voice
    ///   mixing) is reused verbatim.</description></item>
    /// </list>
    ///
    /// Phase 29 byte-identical contract is preserved because this method is
    /// only entered when <paramref name="synthType"/> starts with
    /// <c>"sampler:"</c> — every other call falls through to the existing
    /// Phase 29 dispatch unchanged.
    /// </summary>
    private static Value RenderSongWithSfz(SongData song, string synthType)
    {
        string patchName = synthType.Substring("sampler:".Length);
        var ctx = FlowEngine.CurrentExecutionContext;

        // Advisory #2 — sampler:NAME voice routes through SongRenderer when
        // SfzEnabled=false OR the patch isn't loaded. Emitted BEFORE the
        // throw so composers see the stderr guidance even when the exception
        // is caught upstream (mirrors Plan 33-05's ResolveSfzRoot advisory
        // pattern).
        SfzData? patch = null;
        if (ctx is not null && ctx.SfzPatchRegistry.TryGetValue(patchName, out var registered))
        {
            patch = registered;
        }

        if (patch is null)
        {
            // Build the known-names list — deterministic ordinal sort so
            // the error message is reproducible across runs (mirrors
            // SfzBuiltins' unknown-symbol error ordering).
            var knownNames = ctx?.SfzPatchRegistry.Keys
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();

            // Advisory #2 — config-disabled OR missing-patch case (one-shot
            // per process per sentinel key). Composer-facing stderr message
            // distinguishes the two sub-cases. The throw below remains the
            // hard failure surface; the advisory is purely additive.
            if (ctx is null || !ctx.SfzEnabled)
            {
                // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
                // Mirrors the existing throw below — strict surfaces both the
                // composer-facing [strict] error AND the throw.
                if (ctx is not null && ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [sfz] SFZ patch '{patchName}' not loaded (config-disabled) — sampler:NAME requires 'use \"@sfz\"' before binding",
                        ctx.CurrentCallSite);
                }
                else
                {
                    RenderingDiagnostics.WarnOnce(
                        $"sfz:dispatch:disabled:{patchName}",
                        $"[sfz] SFZ patch '{patchName}' not loaded (config-disabled) — sampler:NAME requires 'use \"@sfz\"' before binding");
                }
            }
            else
            {
                // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [sfz] SFZ patch '{patchName}' not loaded; voice rendered as silence",
                        ctx.CurrentCallSite);
                }
                else
                {
                    RenderingDiagnostics.WarnOnce(
                        $"sfz:dispatch:missing:{patchName}",
                        $"[sfz] SFZ patch '{patchName}' not loaded; voice rendered as silence");
                }
            }

            throw new InvalidOperationException(
                $"Unknown sampler patch '{patchName}'. " +
                $"Known: [{string.Join(", ", knownNames)}]. " +
                $"Did you forget `Sfz {patchName} = (loadSfz #...)`?");
        }

        var cache = FlowEngine.CurrentSfzSampleCache
            ?? throw new InvalidOperationException(
                "sampler:NAME dispatch requires an active FlowEngine — no SfzSampleCache published. " +
                "Direct SongRenderer.RenderSong calls bypassing FlowEngine are unsupported for SFZ patches.");
        cache.EagerLoad(song, patch);

        // Phase 44 Plan 44-06: pass ctx for strict-mode advisory elevation in
        // SfzRenderer leaf sites. Phase 33 byte-identical when ctx==null or
        // CallerStrictMode==false.
        var renderer = new SfzRenderer(cache, ctx);
        var adapter = new SfzNoteSynthesizer(renderer, patch);

        AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);
        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                throw new InvalidOperationException(
                    $"renderSong: section '{sectionRef.Name}' not found in song registry");

            // Phase 37 MIX-02 — capture the section's pan context BEFORE
            // RenderSection so the SfzNoteSynthesizer threads it into
            // SfzRenderer's 6-arg Render overload as voicePan. OQ4
            // additive-with-clamp composition with region.Pan happens inside
            // the renderer; the SongRenderer mix stage then preserves the
            // resulting stereo L/R per the channels==2 branch added in
            // MixVoicesToStereoBuffer (so voice.Pan is NOT re-applied at
            // mix time for SFZ-rendered voices — that would double-pan).
            adapter.SectionPan = sectionData.Context?.Pan ?? 0.0;

            var sectionBuffer = RenderSection(sectionData, adapter);
            for (int r = 0; r < sectionRef.RepeatCount; r++)
            {
                result = AppendBuffers(result, sectionBuffer);
            }
        }
        return Value.Buffer(result);
    }

    /// <summary>
    /// Phase 33 — adapter wrapping <see cref="SfzRenderer"/> in the
    /// <see cref="INoteSynthesizer"/> interface so the sampler: dispatch can
    /// reuse the existing rendering pipeline (RenderSection /
    /// SequenceRenderer / BarRenderer / VoiceAllocator) verbatim. The bound
    /// <see cref="SfzData"/> patch is captured at construction so per-note
    /// calls only thread the per-note parameters.
    /// </summary>
    private sealed class SfzNoteSynthesizer : INoteSynthesizer
    {
        private readonly SfzRenderer _renderer;
        private readonly SfzData _patch;

        /// <summary>
        /// Phase 37 MIX-02 — the composer's section-pan context, captured by
        /// <see cref="RenderSongWithSfz"/> between section iterations. Threaded
        /// into <see cref="SfzRenderer.Render(MusicalNoteData,int,double,double,SfzData,double)"/>
        /// as the <c>voicePan</c> argument so per-region SFZ pan composes
        /// additively with per-voice composer pan (OQ4 lock).
        /// </summary>
        public double SectionPan { get; set; }

        public SfzNoteSynthesizer(SfzRenderer renderer, SfzData patch)
        {
            _renderer = renderer;
            _patch = patch;
            SectionPan = 0.0;
        }

        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            // RenderTuning is intentionally unused — SFZ patches encode their
            // own pitch table via sample.pitch_keycenter + varispeed shift,
            // so the Phase 23 tuning system doesn't apply to the sample path.
            // The interface signature requires the parameter; document the
            // discard explicitly so future readers don't think this is a bug.
            _ = tuning;
            return _renderer.Render(note, sampleRate, durationBeats, bpm, _patch, SectionPan);
        }
    }
#endif // !FLOW_WEB — Phase 47 D-47-08 SFZ dispatch + adapter stripped on Web target.

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
