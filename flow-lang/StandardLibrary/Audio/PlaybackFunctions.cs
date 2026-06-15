using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Built-in functions for audio playback: play, loop, preview, audioDevices, setAudioDevice.
/// Uses <see cref="AudioPlaybackManager"/> to manage backend lifecycle.
/// </summary>
public static class PlaybackFunctions
{
    /// <summary>
    /// Registers all playback-related built-in functions.
    /// </summary>
    /// <param name="registry">The function registry to register with.</param>
    /// <param name="manager">The audio playback manager (owned by FlowEngine).</param>
    public static void Register(InternalFunctionRegistry registry, AudioPlaybackManager manager)
    {
        // play(Buffer) -> Void
        var playBufferSig = new FunctionSignature("play", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("play", playBufferSig, args => PlayBuffer(args, manager));

        // play(Sequence) -> Void — renders to buffer then plays
        var playSeqSig = new FunctionSignature("play", [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("play", playSeqSig, args => PlaySequence(args, manager));

        // play-song (#9): play(Song) -> Void — renders the whole arrangement with
        // per-sequence instrument routing (each named sequence picks its own
        // timbre; unknown names fall back to piano + advisory) then plays the mix.
        // So (play song) Just Works without (play (renderSong song "piano")).
        var playSongSig = new FunctionSignature("play", [SongType.Instance],
            ParameterNames: ["song"]);
        registry.Register("play", playSongSig, args => PlaySong(args, manager));

        // play-song (#9): play(Song, String) -> Void — forces ONE synth for every
        // sequence in the song (e.g. (play song "sine")) then plays the mix.
        var playSongSynthSig = new FunctionSignature("play", [SongType.Instance, StringType.Instance],
            ParameterNames: ["song", "synthType"]);
        registry.Register("play", playSongSynthSig, args => PlaySongWithSynth(args, manager));

        // loop(Buffer) -> Void — loops indefinitely (non-blocking)
        var loopBufferSig = new FunctionSignature("loop", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("loop", loopBufferSig, args => LoopBufferInfiniteAsync(args, manager));

        // loop(Buffer, Int) -> Void — loops N times (non-blocking)
        var loopBufferNSig = new FunctionSignature("loop", [BufferType.Instance, IntType.Instance],
            ParameterNames: ["buf", "count"]);
        registry.Register("loop", loopBufferNSig, args => LoopBufferNAsync(args, manager));

        // stream(Buffer) -> Void — plays without blocking the interpreter
        var streamBufferSig = new FunctionSignature("stream", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("stream", streamBufferSig, args => StreamBuffer(args, manager));

        // stream(Sequence) -> Void — renders and streams
        var streamSeqSig = new FunctionSignature("stream", [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("stream", streamSeqSig, args => StreamSequence(args, manager));

        // preview(Buffer) -> Void — low-quality mono 22050Hz playback
        var previewSig = new FunctionSignature("preview", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("preview", previewSig, args => PreviewBuffer(args, manager));

        // stop() -> Void — stop any currently playing audio
        var stopSig = new FunctionSignature("stop", [], ParameterNames: []);
        registry.Register("stop", stopSig, args => StopPlayback(args, manager));

        // audioDevices() -> String[]
        var devicesSig = new FunctionSignature("audioDevices", [], ParameterNames: []);
        registry.Register("audioDevices", devicesSig, args => GetAudioDevices(args, manager));

        // setAudioDevice(String) -> Bool
        var setDeviceSig = new FunctionSignature("setAudioDevice", [StringType.Instance],
            ParameterNames: ["device"]);
        registry.Register("setAudioDevice", setDeviceSig, args => SetAudioDevice(args, manager));

        // isAudioAvailable() -> Bool
        var isAvailableSig = new FunctionSignature("isAudioAvailable", [], ParameterNames: []);
        registry.Register("isAudioAvailable", isAvailableSig, args => IsAudioAvailable(args, manager));
    }

    /// <summary>
    /// Plays an AudioBuffer through the audio backend.
    /// Empty buffers are a no-op. Blocks until playback completes.
    /// </summary>
    private static Value PlayBuffer(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();

        if (buffer.Frames == 0 || buffer.Data.Length == 0)
            return Value.Void();

        if (manager.CaptureMode)
        {
            manager.SetCapturedBuffer(buffer);
            return Value.Void();
        }

        PlaySamples(buffer.Data, buffer.SampleRate, buffer.Channels, manager, buffer);
        return Value.Void();
    }

    /// <summary>
    /// Renders a Sequence and streams it without blocking.
    /// </summary>
    private static Value StreamSequence(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var sequence = args[0].As<SequenceData>();
        if (sequence.Count == 0) return Value.Void();

        // sweep-0614 wasm-web: Mono-WASM is single-threaded; Task.Run queues to
        // the one main thread that RunFromJs already blocks synchronously, so the
        // body NEVER runs (silent no audio). Fall back to a synchronous single
        // play (WebAudio.Play is fire-and-forget — returns immediately) + an
        // advisory, so composers get audio + a diagnostic instead of silence.
        if (WebPlaybackFallbackUsed())
            return PlaySequence(args, manager);

        Task.Run(() => PlaySequence(args, manager));
        return Value.Void();
    }

    /// <summary>
    /// Streams a buffer without blocking.
    /// </summary>
    private static Value StreamBuffer(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0) return Value.Void();

        // sweep-0614 wasm-web: see StreamSequence — synchronous fallback on Web.
        if (WebPlaybackFallbackUsed())
            return PlayBuffer(args, manager);

        Task.Run(() => PlayBuffer(args, manager));
        return Value.Void();
    }

    /// <summary>
    /// Renders a Sequence to audio using a sine synthesizer at 120 BPM (or current BPM),
    /// then plays the result.
    /// </summary>
    private static Value PlaySequence(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var sequence = args[0].As<SequenceData>();

        if (sequence.Count == 0)
            return Value.Void();

        const int sampleRate = 44100;
        const string synthType = "sine";
        double bpm = Timeline.GetBPM([]).As<double>(); // Wait, Timeline.GetBPM needs to work without _manager, it handles current BPM via ThreadStatic

        var voices = SequenceRenderer.RenderSequenceToVoices(sequence, synthType, sampleRate, bpm, manager.MaxVoices);

        if (voices.Count == 0)
            return Value.Void();

        var mixedBuffer = MixVoicesToBuffer(voices, sequence.TotalBeats, sampleRate, bpm);

        if (mixedBuffer.Frames == 0)
            return Value.Void();

        if (manager.CaptureMode)
        {
            manager.SetCapturedBuffer(mixedBuffer);
            return Value.Void();
        }

        PlaySamples(mixedBuffer.Data, mixedBuffer.SampleRate, mixedBuffer.Channels, manager, mixedBuffer);
        return Value.Void();
    }

    /// <summary>
    /// play-song (#9): renders a Song with per-sequence instrument routing
    /// (via <see cref="SongRenderer.RenderSongAuto"/>) and plays the mixed buffer.
    /// Empty / silent songs are a no-op. Web-target safe — RenderSongAuto +
    /// PlaySamples both run synchronously on the single Mono-WASM thread and the
    /// audible path routes through WebAudioBackend.
    /// </summary>
    private static Value PlaySong(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var song = args[0].As<SongData>();
        var buffer = SongRenderer.RenderSongAuto(song);
        return PlayRenderedBuffer(buffer, manager);
    }

    /// <summary>
    /// play-song (#9): renders a Song forcing ONE synth for every sequence, then
    /// plays the mixed buffer.
    /// </summary>
    private static Value PlaySongWithSynth(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var renderArgs = new List<Value> { args[0], args[1] };
        var buffer = SongRenderer.RenderSong(renderArgs).As<AudioBuffer>();
        return PlayRenderedBuffer(buffer, manager);
    }

    /// <summary>
    /// Shared tail for the play(Song...) overloads: honors capture mode and
    /// no-ops on empty buffers, otherwise routes through the backend.
    /// </summary>
    private static Value PlayRenderedBuffer(AudioBuffer buffer, AudioPlaybackManager manager)
    {
        if (buffer.Frames == 0 || buffer.Data.Length == 0)
            return Value.Void();

        if (manager.CaptureMode)
        {
            manager.SetCapturedBuffer(buffer);
            return Value.Void();
        }

        PlaySamples(buffer.Data, buffer.SampleRate, buffer.Channels, manager, buffer);
        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer indefinitely (non-blocking).
    /// </summary>
    private static Value LoopBufferInfiniteAsync(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0) return Value.Void();

        // sweep-0614 wasm-web: on the single-threaded Web target the Task.Run
        // body never runs (silent), and LoopBufferInfinite's tight
        // while(!ct.IsCancellationRequested) over a fire-and-forget Play would
        // either no-op or spawn unbounded overlapping source nodes / hang the
        // tab. Fall back to a single synchronous play + advisory (audio +
        // diagnostic instead of silence). True looping is v1.6 (native
        // source.loop=true via a new JSImport).
        if (WebPlaybackFallbackUsed())
            return PlayBuffer(args, manager);

        Task.Run(() => LoopBufferInfinite(args, manager));
        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer indefinitely. Blocks until cancelled.
    /// </summary>
    private static Value LoopBufferInfinite(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0) return Value.Void();

        if (manager.CaptureMode)
        {
            manager.SetCapturedBuffer(buffer);
            return Value.Void();
        }

        var ct = manager.StartPlayback();

        try
        {
            var backend = GetBackendOrThrow(manager);
            if (!backend.IsInitialized)
                backend.Initialize(buffer.SampleRate, buffer.Channels);

            var clamped = AudioUtils.ClampSamples(buffer.Data);

            while (!ct.IsCancellationRequested)
            {
                backend.Play(clamped, buffer.SampleRate, buffer.Channels, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer N times (non-blocking).
    /// </summary>
    private static Value LoopBufferNAsync(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0) return Value.Void();

        // sweep-0614 wasm-web: synchronous single-play fallback on Web (see
        // LoopBufferInfiniteAsync). Validate the count arg the same way
        // LoopBufferN does so degenerate input behaves identically across targets.
        if (WebPlaybackFallbackUsed())
        {
            int requested = args[1].As<int>();
            if (requested <= 0)
                throw new ArgumentException("Loop count must be positive.");
            return PlayBuffer(args, manager);
        }

        Task.Run(() => LoopBufferN(args, manager));
        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer N times. N must be positive.
    /// </summary>
    private static Value LoopBufferN(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        int count = args[1].As<int>();

        if (count <= 0)
            throw new ArgumentException("Loop count must be positive.");

        if (buffer.Frames == 0)
            return Value.Void();

        if (manager.CaptureMode)
        {
            manager.SetCapturedBuffer(buffer);
            return Value.Void();
        }

        var ct = manager.StartPlayback();

        try
        {
            var backend = GetBackendOrThrow(manager);
            if (!backend.IsInitialized)
                backend.Initialize(buffer.SampleRate, buffer.Channels);

            var clamped = AudioUtils.ClampSamples(buffer.Data);

            for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
            {
                backend.Play(clamped, buffer.SampleRate, buffer.Channels, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return Value.Void();
    }

    /// <summary>
    /// Low-quality preview: downsamples to mono 22050Hz and plays.
    /// </summary>
    private static Value PreviewBuffer(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();

        if (buffer.Frames == 0)
            return Value.Void();

        const int previewRate = 22050;
        double ratio = (double)buffer.SampleRate / previewRate;
        int previewFrames = (int)(buffer.Frames / ratio);

        var previewSamples = new float[previewFrames];
        for (int i = 0; i < previewFrames; i++)
        {
            int srcFrame = (int)(i * ratio);
            if (srcFrame >= buffer.Frames) break;

            float sum = 0;
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                sum += buffer.GetSample(srcFrame, ch);
            }
            previewSamples[i] = sum / buffer.Channels;
        }

        PlaySamples(previewSamples, previewRate, 1, manager);
        return Value.Void();
    }

    /// <summary>
    /// Stops any currently playing audio.
    /// </summary>
    private static Value StopPlayback(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        manager.StopPlayback();
        return Value.Void();
    }

    /// <summary>
    /// Returns available audio output devices as a string array.
    /// </summary>
    private static Value GetAudioDevices(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        if (!manager.IsAudioAvailable())
            return Value.Array([], StringType.Instance);

        var backend = manager.GetBackend();
        var devices = backend.GetDevices();
        var values = devices.Select(d => Value.String(d)).ToArray();
        return Value.Array(values, StringType.Instance);
    }

    /// <summary>
    /// Sets the active audio output device. Returns true on success.
    /// </summary>
    private static Value SetAudioDevice(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var deviceName = args[0].As<string>();

        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("Device name cannot be empty.");

        var backend = GetBackendOrThrow(manager);
        bool success = backend.SetDevice(deviceName);
        return Value.Bool(success);
    }

    /// <summary>
    /// Returns whether any audio backend is available.
    /// </summary>
    private static Value IsAudioAvailable(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        return Value.Bool(manager.IsAudioAvailable());
    }

    // --- Helper methods ---

    /// <summary>
    /// sweep-0614 wasm-web: true on the browser (single-threaded Mono-WASM)
    /// target, where the non-blocking <c>loop</c>/<c>stream</c> builtins cannot
    /// use <c>Task.Run</c> (no background thread — the body would never run, or a
    /// blocking loop would hang the tab). When true the caller routes to a
    /// synchronous single play; this helper emits the one-shot advisory once.
    /// Returns false on Desktop (constant-folded), so the existing Task.Run path
    /// is byte-identical there.
    /// </summary>
    private static bool WebPlaybackFallbackUsed()
    {
        if (!OperatingSystem.IsBrowser())
            return false;

        FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
            "web-loop-stream-sync-fallback",
            "[target] loop/stream fall back to a single synchronous play under " +
            "FlowTarget=Web (no background thread); native looping is v1.6");
        return true;
    }

    /// <summary>
    /// Plays float samples through the audio backend with cancellation support.
    /// </summary>
    /// <param name="originBuffer">Phase 40 MIDI-RT-04 alignment seam. When non-null,
    /// its <see cref="AudioBuffer.PlaybackStartTime"/> is stamped with the
    /// <see cref="System.Diagnostics.Stopwatch"/> tick origin the instant before
    /// <c>backend.Play</c> begins. A real-time MIDI scheduler keys note dispatch
    /// off this origin (buffer-relative ms alignment — NOT sample-accurate;
    /// 40-RESEARCH Pitfall 5). Null on synthesized/preview paths with no buffer.</param>
    private static void PlaySamples(float[] samples, int sampleRate, int channels, AudioPlaybackManager manager, AudioBuffer? originBuffer = null)
    {
        var ct = manager.StartPlayback();
        var backend = GetBackendOrThrow(manager);

        try
        {
            // Phase 40 MIDI-RT-04: record the alignment origin the instant before
            // playback begins, so any midiOut scheduler dispatches events relative
            // to the true audio start (NOT queue/enqueue time). Honest scope:
            // buffer-relative ms accuracy on the blocking PulseAudio Simple push
            // API — there is no pull-model callback for sample accuracy.
            if (originBuffer != null)
                originBuffer.PlaybackStartTime = System.Diagnostics.Stopwatch.GetTimestamp();

            backend.Play(samples, sampleRate, channels, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Gets the audio backend or throws a clear error message.
    /// </summary>
    private static IAudioBackend GetBackendOrThrow(AudioPlaybackManager manager)
    {
        try
        {
            return manager.GetBackend();
        }
        catch (PlatformNotSupportedException)
        {
            throw new InvalidOperationException(
                "No audio output available. Install PipeWire or PulseAudio.");
        }
    }

    /// <summary>
    /// Mixes a list of voices into a single AudioBuffer.
    /// </summary>
    private static AudioBuffer MixVoicesToBuffer(
        List<Voice> voices, double totalBeats, int sampleRate, double bpm)
    {
        double secondsPerBeat = 60.0 / bpm;
        double totalSeconds = totalBeats * secondsPerBeat;
        int totalFrames = (int)(totalSeconds * sampleRate);

        if (totalFrames <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var result = new AudioBuffer(totalFrames, 1, sampleRate);

        foreach (var voice in voices)
        {
            int voiceStartFrame = (int)(voice.OffsetBeats * secondsPerBeat * sampleRate);

            for (int frame = 0; frame < voice.Buffer.Frames; frame++)
            {
                int destFrame = voiceStartFrame + frame;
                if (destFrame < 0 || destFrame >= totalFrames) continue;

                float sample = voice.Buffer.GetSample(frame, 0);
                sample *= (float)voice.Gain;

                float existing = result.GetSample(destFrame, 0);
                result.SetSample(destFrame, 0, existing + sample);
            }
        }

        return result;
    }
}
