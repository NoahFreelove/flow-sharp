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

        PlaySamples(buffer.Data, buffer.SampleRate, buffer.Channels, manager);
        return Value.Void();
    }

    /// <summary>
    /// Renders a Sequence and streams it without blocking.
    /// </summary>
    private static Value StreamSequence(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var sequence = args[0].As<SequenceData>();
        if (sequence.Count == 0) return Value.Void();

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

        PlaySamples(mixedBuffer.Data, mixedBuffer.SampleRate, mixedBuffer.Channels, manager);
        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer indefinitely (non-blocking).
    /// </summary>
    private static Value LoopBufferInfiniteAsync(IReadOnlyList<Value> args, AudioPlaybackManager manager)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0) return Value.Void();

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

            var clamped = ClampSamples(buffer.Data);

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

            var clamped = ClampSamples(buffer.Data);

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
    /// Plays float samples through the audio backend with cancellation support.
    /// </summary>
    private static void PlaySamples(float[] samples, int sampleRate, int channels, AudioPlaybackManager manager)
    {
        var ct = manager.StartPlayback();
        var backend = GetBackendOrThrow(manager);

        try
        {
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
    /// Clamps samples to [-1.0, 1.0] and handles NaN/Infinity.
    /// Delegates to the shared AudioUtils implementation.
    /// </summary>
    private static float[] ClampSamples(float[] samples) => AudioUtils.ClampSamples(samples);

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
