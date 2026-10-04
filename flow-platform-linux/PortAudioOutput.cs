using System.Runtime.InteropServices;

namespace Flow.Platform.Linux;

public sealed record OutputDevice(int Index, string Name, int Channels, double DefaultSampleRate, double LowLatencySeconds, int HostApiType, string HostApiName);

/// <summary>
/// Linux PortAudio v19 callback prototype, float32 interleaved stereo. Lifecycle
/// calls must be serialized on the control thread, never from the audio callback.
/// Dispose closes/joins native callbacks before releasing the rooted delegate.
/// Explicit disposal is required; there is no audio-thread finalizer cleanup.
/// </summary>
public sealed class PortAudioOutput : IPlaybackOutputStream
{
    // Serialize this adapter's process-global PortAudio lifecycle. Separate native
    // clients must coordinate externally; no lock is taken by the audio callback.
    private static readonly object Lifecycle = new();
    private static int _users;
    private readonly CallbackRenderProbe _probe;
    private readonly PortAudioNative.Callback _callback;
    private nint _stream;
    private GCHandle _callbackRoot;
    private bool _disposed;

    public OutputDevice Device { get; }
    public string NativeVersion { get; }
    public double OutputLatencySeconds { get; }
    public double ActualSampleRate { get; }
    // Audited e1b70d33 PulseAudio host passes zero callback flags even on underflow.
    // Other backends/versions are unverified, never implicitly declared reliable.
    public string UnderflowObservability => Device.HostApiType == 16 && NativeVersion.Contains("e1b70d33", StringComparison.Ordinal)
        ? "unavailable: audited PulseAudio backend does not forward underflow flags"
        : "unverified: backend-specific underflow reporting has not been validated";
    public bool CallbackFaulted => _probe.Faulted;

    public static OutputDevice[] ListDevices()
    {
        lock (Lifecycle)
        {
            Acquire();
            try
            {
                int count = PortAudioNative.Pa_GetDeviceCount();
                Check(count < 0 ? count : 0);
                var devices = new List<OutputDevice>();
                for (int i = 0; i < count; i++)
                {
                    var device = GetDevice(i);
                    if (device.Channels >= 2) devices.Add(device);
                }
                return devices.ToArray();
            }
            finally { Release(); }
        }
    }

    public PortAudioOutput(CallbackRenderProbe probe, int? deviceIndex = null, string? deviceSelector = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
        _callback = Render;
        lock (Lifecycle)
        {
            Acquire();
            try
            {
                if (deviceIndex.HasValue && deviceSelector is not null)
                    throw new ArgumentException("Choose either a device index or a host/name selector");
                int index = deviceIndex ?? PortAudioNative.Pa_GetDefaultOutputDevice();
                if (deviceSelector is not null)
                {
                    int count = PortAudioNative.Pa_GetDeviceCount();
                    Check(count < 0 ? count : 0);
                    var matches = Enumerable.Range(0, count).Select(GetDevice)
                        .Where(d => d.Channels >= 2 && $"{d.HostApiName}/{d.Name}" == deviceSelector).ToArray();
                    if (matches.Length != 1)
                        throw new ArgumentException($"Device selector must match exactly one stereo output: {deviceSelector} (found {matches.Length})");
                    index = matches[0].Index;
                }
                if (index < 0) throw new InvalidOperationException("No default PortAudio output device is available");
                Device = GetDevice(index);
                if (Device.Channels < 2) throw new InvalidOperationException("Output device does not support stereo");
                NativeVersion = Marshal.PtrToStringUTF8(PortAudioNative.Pa_GetVersionText()) ?? "unknown";
                var parameters = new PortAudioNative.StreamParameters
                {
                    Device = index, ChannelCount = 2, SampleFormat = 1, // paFloat32
                    SuggestedLatency = Device.LowLatencySeconds,
                };
                _callbackRoot = GCHandle.Alloc(_callback);
                Check(PortAudioNative.Pa_OpenStream(out _stream, 0, ref parameters,
                    probe.SampleRate, (nuint)probe.BlockFrames, 0, _callback, 0));
                var info = Marshal.PtrToStructure<PortAudioNative.StreamInfo>(PortAudioNative.Pa_GetStreamInfo(_stream));
                OutputLatencySeconds = info.OutputLatency;
                ActualSampleRate = info.SampleRate;
            }
            catch
            {
                if (_stream != 0) Check(PortAudioNative.Pa_CloseStream(_stream));
                if (_callbackRoot.IsAllocated) _callbackRoot.Free();
                Release();
                throw;
            }
        }
    }

    public void Start()
    {
        lock (Lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (CallbackFaulted) throw new InvalidOperationException("Cannot restart a faulted callback");
            Check(PortAudioNative.Pa_StartStream(_stream));
        }
    }

    public bool IsActive
    {
        get
        {
            lock (Lifecycle)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                int active = PortAudioNative.Pa_IsStreamActive(_stream);
                if (active < 0) Check(active);
                return active == 1;
            }
        }
    }

    public void Dispose()
    {
        lock (Lifecycle)
        {
            if (_disposed) return;
            // Close also aborts a running stream. Keep the delegate rooted through
            // native teardown, including an already-aborted callback stream.
            int result = PortAudioNative.Pa_CloseStream(_stream);
            if (result < 0) { Check(result); return; }
            _stream = 0;
            _disposed = true;
            if (_callbackRoot.IsAllocated) _callbackRoot.Free();
            Release();
            GC.KeepAlive(_callback);
        }
    }

    private int Render(nint input, nint output, nuint frames, nint timeInfo, nuint flags, nint userData)
        => _probe.ProcessNative(output, frames, flags, timeInfo);

    private static OutputDevice GetDevice(int index)
    {
        nint pointer = PortAudioNative.Pa_GetDeviceInfo(index);
        if (pointer == 0) throw new ArgumentOutOfRangeException(nameof(index));
        var info = Marshal.PtrToStructure<PortAudioNative.DeviceInfo>(pointer);
        var host = Marshal.PtrToStructure<PortAudioNative.HostApiInfo>(PortAudioNative.Pa_GetHostApiInfo(info.HostApi));
        return new(index, Marshal.PtrToStringUTF8(info.Name) ?? "unknown", info.MaxOutputChannels,
            info.DefaultSampleRate, info.DefaultLowOutputLatency, host.Type,
            Marshal.PtrToStringUTF8(host.Name) ?? "unknown");
    }

    private static void Acquire()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This adapter requires Linux and libportaudio.so.2");
        if (_users == 0) Check(PortAudioNative.Pa_Initialize());
        _users++;
    }
    private static void Release()
    {
        if (--_users == 0) Check(PortAudioNative.Pa_Terminate());
    }
    private static void Check(int error)
    {
        if (error < 0) throw new InvalidOperationException($"PortAudio {error}: {Marshal.PtrToStringUTF8(PortAudioNative.Pa_GetErrorText(error))}");
    }
}
