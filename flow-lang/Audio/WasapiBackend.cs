#if !FLOW_WEB
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FlowLang.Audio;

/// <summary>
/// Phase 41 Plan 41-04 WASAPI-01 (D-17). Windows audio backend over NAudio's
/// <see cref="WasapiOut"/>, structured after <see cref="PulseAudioSimpleBackend"/> /
/// <see cref="CoreAudioBackend"/>.
/// </summary>
/// <remarks>
/// NAudio's <c>WasapiOut</c> is a PULL model — a background <c>PlayThread</c> calls
/// <c>IWaveProvider.Read</c> on demand. <see cref="IAudioBackend"/> is a BLOCKING
/// PUSH contract (<see cref="Play"/> returns once the audio has drained). The bridge
/// is a <see cref="BufferedWaveProvider"/>: we <c>AddSamples</c> (float[] → byte[] via
/// <see cref="Buffer.BlockCopy"/>), <c>Play()</c>, then block polling
/// <c>BufferedBytes == 0</c> + <c>PlaybackState != Playing</c> until the queue drains
/// (honoring the <see cref="CancellationToken"/> to <see cref="Stop"/> early).
///
/// WASAPI is Windows-only. The <see cref="WasapiOut"/> / <see cref="MMDeviceEnumerator"/>
/// types resolve at compile time on this Linux build host (the NAudio.Wasapi
/// netstandard2.0 facade restores cross-platform), but every COM call only fires at
/// runtime on Windows. <see cref="IsAvailable"/> is the single probe entry point and
/// returns <c>false</c> on non-Windows without throwing — so DetectBackend never
/// instantiates this on Linux. Real audible playback is a HUMAN-UAT row (D-05);
/// nothing in this file claims it works on Windows hardware.
///
/// Web-stripped three ways (Pitfall 3): Desktop-only PackageReference,
/// <c>&lt;Compile Remove&gt;</c> on Web, and this <c>#if !FLOW_WEB</c> guard.
/// </remarks>
internal sealed class WasapiBackend : IAudioBackend
{
    // Shared-mode default; exclusive-mode is the opt-in (D-17). The latency hint is
    // WASAPI's minimal buffer in ms; 100 ms is NAudio's own default for shared mode.
    private const int DefaultLatencyMs = 100;

    private WasapiOut? _out;
    private BufferedWaveProvider? _provider;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;
    private readonly object _lock = new();

    public string Name => "WASAPI";
    public bool IsInitialized => _out != null;

    /// <summary>
    /// Checks whether the WASAPI backend can run on this system. WASAPI is
    /// Windows-only — returns <c>false</c> on Linux/macOS. The probe is defensive:
    /// any platform/native resolution failure is swallowed and reported as
    /// unavailable rather than thrown (mirrors the DllNotFoundException-catch
    /// convention in <see cref="PulseAudioSimpleBackend.IsAvailable"/> /
    /// <see cref="CoreAudioBackend.IsAvailable"/>). T-41-04-PINVOKE: no crash on
    /// non-Windows.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the exclusive-mode opt-in. Shared mode is the default (D-17); a future
    /// Phase 30 config.toml key can flip this to exclusive. Kept local so the
    /// SPEC-4-locked five-key <see cref="Runtime.FlowConfig"/> surface is not
    /// expanded here (a config-schema change is out of this plan's scope).
    /// </summary>
    private static AudioClientShareMode ResolveShareMode() => AudioClientShareMode.Shared;

    public bool Initialize(int sampleRate, int channels)
    {
        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channels < 1 || channels > 8)
            throw new ArgumentException("Channel count must be between 1 and 8.", nameof(channels));

        lock (_lock)
        {
            CloseOutput();

            _sampleRate = sampleRate;
            _channels = channels;

            try
            {
                // 32-bit IEEE float, interleaved — matches the float[] sample contract.
                var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

                _provider = new BufferedWaveProvider(format)
                {
                    // Generous headroom; Play()/WriteChunk feed before this fills.
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = false,
                };

                _out = new WasapiOut(ResolveShareMode(), DefaultLatencyMs);
                _out.Init(_provider);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WASAPI: initialization failed: {ex.Message}");
                CloseOutput();
                return false;
            }
        }
    }

    public void EnsureInitialized(int sampleRate, int channels)
    {
        lock (_lock)
        {
            if (IsInitialized && sampleRate == _sampleRate && channels == _channels)
                return;
        }

        if (!Initialize(sampleRate, channels))
            throw new InvalidOperationException("No audio output available. WASAPI init failed.");
    }

    public void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)
    {
        if (samples.Length == 0)
            return;

        EnsureInitialized(sampleRate, channels);

        var clamped = AudioUtils.ClampSamples(samples);
        byte[] bytes = FloatsToBytes(clamped);

        BufferedWaveProvider provider;
        WasapiOut output;
        lock (_lock)
        {
            if (_provider == null || _out == null)
                return;
            provider = _provider;
            output = _out;
        }

        // Push the whole buffer in, then let the WasapiOut PlayThread pull it out.
        // The 5 s BufferDuration covers typical render chunks; for longer buffers
        // we feed in pieces as the queue drains so AddSamples never overflows.
        int byteOffset = 0;
        int total = bytes.Length;
        // WR-01: feed in ~100 ms slices to match the WASAPI latency hint
        // (DefaultLatencyMs) rather than 1 s chunks. With 1 s chunks a single
        // Thread.Sleep oversleep under Windows scheduler jitter could let the
        // BufferedWaveProvider drain to empty (audible underrun) before the next
        // slice lands. Smaller slices + a 5 ms poll keep the feed loop tracking the
        // realtime drain rate. Guard against a degenerate 0 from integer division on
        // exotic formats.
        int chunkBytes = Math.Max(1, provider.WaveFormat.AverageBytesPerSecond / 10);

        // CR-02: a concurrent Dispose()/Stop() on another thread can Dispose the
        // `output`/`provider` we captured above while the feed + drain loops below
        // poll them OUTSIDE the lock (the Thread.Sleep waits must not hold _lock).
        // NAudio documents PlaybackState / BufferedBytes / AddSamples as throwing
        // ObjectDisposedException (and sometimes InvalidOperationException) once
        // disposed. Any such throw means "the device went away under us" — treat it
        // as a clean early return, never an unhandled crash. All naked accesses to
        // `output`/`provider` past the lock boundary go through this guard.
        try
        {
            output.Play();

            while (byteOffset < total)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Stop();
                    return;
                }

                // Wait for room in the buffered queue before adding the next slice.
                while (provider.BufferedBytes + Math.Min(chunkBytes, total - byteOffset) > provider.BufferLength)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Stop();
                        return;
                    }
                    Thread.Sleep(5); // WR-01: tighter poll tracks the realtime drain
                }

                int writeBytes = Math.Min(chunkBytes, total - byteOffset);
                provider.AddSamples(bytes, byteOffset, writeBytes);
                byteOffset += writeBytes;
            }

            // Block until the buffered audio fully drains, then stop the device.
            while (provider.BufferedBytes > 0 && output.PlaybackState == PlaybackState.Playing)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Stop();
                    return;
                }
                Thread.Sleep(5); // WR-01: tighter poll tracks the realtime drain
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed concurrently — the audio is gone; nothing left to drain.
            return;
        }
        catch (InvalidOperationException)
        {
            // WasapiOut can surface this when the device is torn down mid-poll.
            return;
        }

        lock (_lock)
        {
            try { _out?.Stop(); } catch { /* best effort — may be disposed */ }
        }
    }

    public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels)
    {
        if (count <= 0)
            return;

        EnsureInitialized(sampleRate, channels);

        // Build a clamped sub-buffer (mirrors Pulse/CoreAudio WriteChunk semantics).
        var chunk = new float[count];
        for (int i = 0; i < count; i++)
        {
            int srcIdx = offset + i;
            if (srcIdx >= samples.Length) break;
            float s = samples[srcIdx];
            chunk[i] = (float.IsNaN(s) || float.IsInfinity(s)) ? 0f : Math.Clamp(s, -1.0f, 1.0f);
        }

        byte[] bytes = FloatsToBytes(chunk);

        lock (_lock)
        {
            if (_provider == null || _out == null)
                return;
            _provider.AddSamples(bytes, 0, bytes.Length);
            if (_out.PlaybackState != PlaybackState.Playing)
                _out.Play();
        }
        // No drain — streaming caller controls the loop.
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_out != null)
            {
                try { _out.Stop(); } catch { /* best effort */ }
            }
            // Clear any queued audio so a subsequent Play starts clean.
            try { _provider?.ClearBuffer(); } catch { /* best effort */ }
        }
    }

    public IReadOnlyList<string> GetDevices()
    {
        // Best-effort render-endpoint enumeration. Returns empty on any failure
        // (matches the "may be empty" allowance in IAudioBackend.GetDevices).
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var names = new List<string>();
            foreach (var device in devices)
            {
                names.Add(device.FriendlyName);
                device.Dispose();
            }
            return names;
        }
        catch
        {
            return [];
        }
    }

    public bool SetDevice(string deviceName)
    {
        // Runtime device switching would require reconstructing WasapiOut bound to a
        // specific MMDevice. Not supported for v1.5 — match Pulse/CoreAudio: composer
        // uses Windows Sound settings to change the default output device.
        Console.Error.WriteLine(
            "WASAPI backend does not support runtime device switching. " +
            "Use Windows Sound settings to change the default output device.");
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (_lock)
        {
            CloseOutput();
        }
    }

    // --- Helpers ---

    /// <summary>
    /// Interleaved float[] → little-endian byte[] (4 bytes/sample) for
    /// BufferedWaveProvider.AddSamples. Buffer.BlockCopy is correct here because the
    /// WaveFormat is IEEE float and WASAPI consumes native-endian float frames.
    /// </summary>
    private static byte[] FloatsToBytes(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private void CloseOutput()
    {
        // Caller MUST hold _lock.
        if (_out != null)
        {
            try { _out.Stop(); } catch { /* best effort */ }
            try { _out.Dispose(); } catch { /* best effort */ }
            _out = null;
        }
        _provider = null;
    }
}
#endif
