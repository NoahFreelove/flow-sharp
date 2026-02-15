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
    private static AudioPlaybackManager? _manager;

    /// <summary>
    /// Registers all playback-related built-in functions.
    /// </summary>
    /// <param name="registry">The function registry to register with.</param>
    /// <param name="manager">The audio playback manager (owned by FlowEngine).</param>
    public static void Register(InternalFunctionRegistry registry, AudioPlaybackManager manager)
    {
        _manager = manager;

        // play(Buffer) -> Void
        var playBufferSig = new FunctionSignature("play", [BufferType.Instance]);
        registry.Register("play", playBufferSig, PlayBuffer);

        // play(Sequence) -> Void — renders to buffer then plays
        var playSeqSig = new FunctionSignature("play", [SequenceType.Instance]);
        registry.Register("play", playSeqSig, PlaySequence);

        // loop(Buffer) -> Void — loops indefinitely until stopped
        var loopBufferSig = new FunctionSignature("loop", [BufferType.Instance]);
        registry.Register("loop", loopBufferSig, LoopBufferInfinite);

        // loop(Buffer, Int) -> Void — loops N times
        var loopBufferNSig = new FunctionSignature("loop", [BufferType.Instance, IntType.Instance]);
        registry.Register("loop", loopBufferNSig, LoopBufferN);

        // preview(Buffer) -> Void — low-quality mono 22050Hz playback
        var previewSig = new FunctionSignature("preview", [BufferType.Instance]);
        registry.Register("preview", previewSig, PreviewBuffer);

        // stop() -> Void — stop any currently playing audio
        var stopSig = new FunctionSignature("stop", []);
        registry.Register("stop", stopSig, StopPlayback);

        // audioDevices() -> String[]
        var devicesSig = new FunctionSignature("audioDevices", []);
        registry.Register("audioDevices", devicesSig, GetAudioDevices);

        // setAudioDevice(String) -> Bool
        var setDeviceSig = new FunctionSignature("setAudioDevice", [StringType.Instance]);
        registry.Register("setAudioDevice", setDeviceSig, SetAudioDevice);

        // isAudioAvailable() -> Bool
        var isAvailableSig = new FunctionSignature("isAudioAvailable", []);
        registry.Register("isAudioAvailable", isAvailableSig, IsAudioAvailable);
    }

    /// <summary>
    /// Plays an AudioBuffer through the audio backend.
    /// Empty buffers are a no-op. Blocks until playback completes.
    /// </summary>
    private static Value PlayBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();

        // Empty buffer is a no-op
        if (buffer.Frames == 0 || buffer.Data.Length == 0)
            return Value.Void();

        PlaySamples(buffer.Data, buffer.SampleRate, buffer.Channels);
        return Value.Void();
    }

    /// <summary>
    /// Renders a Sequence to audio using a sine synthesizer at 120 BPM (or current BPM),
    /// then plays the result.
    /// </summary>
    private static Value PlaySequence(IReadOnlyList<Value> args)
    {
        var sequence = args[0].As<SequenceData>();

        if (sequence.Count == 0)
            return Value.Void();

        // Render sequence to voices using sine synth
        const int sampleRate = 44100;
        const string synthType = "sine";
        double bpm = Timeline.GetBPM([]).As<double>();

        var voices = SequenceRenderer.RenderSequenceToVoices(sequence, synthType, sampleRate, bpm);

        if (voices.Count == 0)
            return Value.Void();

        // Mix voices into a single buffer
        var mixedBuffer = MixVoicesToBuffer(voices, sequence.TotalBeats, sampleRate, bpm);

        if (mixedBuffer.Frames == 0)
            return Value.Void();

        PlaySamples(mixedBuffer.Data, mixedBuffer.SampleRate, mixedBuffer.Channels);
        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer indefinitely. Stops when StopPlayback is called or Ctrl+C.
    /// </summary>
    private static Value LoopBufferInfinite(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();

        if (buffer.Frames == 0)
            return Value.Void();

        var ct = _manager!.StartPlayback();

        try
        {
            var backend = GetBackendOrThrow();
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
            // Normal cancellation — loop was stopped
        }

        return Value.Void();
    }

    /// <summary>
    /// Loops a buffer N times. N must be positive.
    /// </summary>
    private static Value LoopBufferN(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        int count = args[1].As<int>();

        if (count <= 0)
            throw new ArgumentException("Loop count must be positive.");

        if (buffer.Frames == 0)
            return Value.Void();

        var ct = _manager!.StartPlayback();

        try
        {
            var backend = GetBackendOrThrow();
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
            // Normal cancellation
        }

        return Value.Void();
    }

    /// <summary>
    /// Low-quality preview: downsamples to mono 22050Hz and plays.
    /// </summary>
    private static Value PreviewBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();

        if (buffer.Frames == 0)
            return Value.Void();

        // Downsample to mono 22050Hz for quick preview
        const int previewRate = 22050;
        double ratio = (double)buffer.SampleRate / previewRate;
        int previewFrames = (int)(buffer.Frames / ratio);

        var previewSamples = new float[previewFrames];
        for (int i = 0; i < previewFrames; i++)
        {
            int srcFrame = (int)(i * ratio);
            if (srcFrame >= buffer.Frames) break;

            // Mix all channels to mono
            float sum = 0;
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                sum += buffer.GetSample(srcFrame, ch);
            }
            previewSamples[i] = sum / buffer.Channels;
        }

        PlaySamples(previewSamples, previewRate, 1);
        return Value.Void();
    }

    /// <summary>
    /// Stops any currently playing audio.
    /// </summary>
    private static Value StopPlayback(IReadOnlyList<Value> args)
    {
        _manager?.StopPlayback();
        return Value.Void();
    }

    /// <summary>
    /// Returns available audio output devices as a string array.
    /// </summary>
    private static Value GetAudioDevices(IReadOnlyList<Value> args)
    {
        if (!_manager!.IsAudioAvailable())
            return Value.Array([], StringType.Instance);

        var backend = _manager.GetBackend();
        var devices = backend.GetDevices();
        var values = devices.Select(d => Value.String(d)).ToArray();
        return Value.Array(values, StringType.Instance);
    }

    /// <summary>
    /// Sets the active audio output device. Returns true on success.
    /// </summary>
    private static Value SetAudioDevice(IReadOnlyList<Value> args)
    {
        var deviceName = args[0].As<string>();

        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("Device name cannot be empty.");

        var backend = GetBackendOrThrow();
        bool success = backend.SetDevice(deviceName);
        return Value.Bool(success);
    }

    /// <summary>
    /// Returns whether any audio backend is available.
    /// </summary>
    private static Value IsAudioAvailable(IReadOnlyList<Value> args)
    {
        return Value.Bool(_manager?.IsAudioAvailable() ?? false);
    }

    // --- Helper methods ---

    /// <summary>
    /// Plays float samples through the audio backend with cancellation support.
    /// </summary>
    private static void PlaySamples(float[] samples, int sampleRate, int channels)
    {
        var ct = _manager!.StartPlayback();
        var backend = GetBackendOrThrow();

        try
        {
            backend.Play(samples, sampleRate, channels, ct);
        }
        catch (OperationCanceledException)
        {
            // Playback was interrupted — not an error
        }
    }

    /// <summary>
    /// Gets the audio backend or throws a clear error message.
    /// </summary>
    private static IAudioBackend GetBackendOrThrow()
    {
        try
        {
            return _manager!.GetBackend();
        }
        catch (PlatformNotSupportedException)
        {
            throw new InvalidOperationException(
                "No audio output available. Install PipeWire or PulseAudio.");
        }
    }

    /// <summary>
    /// Clamps samples to [-1.0, 1.0] and handles NaN/Infinity.
    /// </summary>
    private static float[] ClampSamples(float[] samples)
    {
        var clamped = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            if (float.IsNaN(s) || float.IsInfinity(s))
                clamped[i] = 0f;
            else
                clamped[i] = Math.Clamp(s, -1.0f, 1.0f);
        }
        return clamped;
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

        // Use mono output for simplicity
        var result = new AudioBuffer(totalFrames, 1, sampleRate);

        foreach (var voice in voices)
        {
            int voiceStartFrame = (int)(voice.OffsetBeats * secondsPerBeat * sampleRate);

            for (int frame = 0; frame < voice.Buffer.Frames; frame++)
            {
                int destFrame = voiceStartFrame + frame;
                if (destFrame < 0 || destFrame >= totalFrames) continue;

                // Mix mono — take first channel from source
                float sample = voice.Buffer.GetSample(frame, 0);
                sample *= (float)voice.Gain;

                float existing = result.GetSample(destFrame, 0);
                result.SetSample(destFrame, 0, existing + sample);
            }
        }

        return result;
    }
}
