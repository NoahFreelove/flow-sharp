namespace FlowLang.Audio;

/// <summary>
/// Abstraction for audio playback backends (PipeWire, PulseAudio, etc.).
/// Implementations handle platform-specific audio output.
/// </summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>
    /// Initialize the audio backend and connect to the audio server.
    /// </summary>
    /// <param name="sampleRate">Desired sample rate in Hz (e.g., 44100, 48000).</param>
    /// <param name="channels">Number of audio channels (1 = mono, 2 = stereo).</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    bool Initialize(int sampleRate, int channels);

    /// <summary>
    /// Play a buffer of float samples. Blocks until playback completes or is cancelled.
    /// Samples should be in interleaved format for multi-channel audio.
    /// </summary>
    /// <param name="samples">Interleaved float sample data, range [-1.0, 1.0].</param>
    /// <param name="sampleRate">Sample rate of the data in Hz.</param>
    /// <param name="channels">Number of channels in the data.</param>
    /// <param name="cancellationToken">Token to cancel playback.</param>
    void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop any currently playing audio immediately.
    /// </summary>
    void Stop();

    /// <summary>
    /// List available audio output devices.
    /// </summary>
    /// <returns>List of device names. May be empty if enumeration is not supported.</returns>
    IReadOnlyList<string> GetDevices();

    /// <summary>
    /// Set the active output device by name.
    /// </summary>
    /// <param name="deviceName">Device name from <see cref="GetDevices"/>.</param>
    /// <returns>True if device was set successfully.</returns>
    bool SetDevice(string deviceName);

    /// <summary>
    /// Human-readable name of this backend (e.g., "PulseAudio", "PipeWire").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this backend is currently initialized and ready to play audio.
    /// </summary>
    bool IsInitialized { get; }
}
