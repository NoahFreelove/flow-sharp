using System.Runtime.InteropServices;

namespace Flow.Platform.Linux;

// PortAudio v19 C ABI, Linux only (unsigned long is pointer-sized here).
// https://portaudio.com/docs/v19-doxydocs/portaudio_8h.html
internal static class PortAudioNative
{
    private const string Library = "libportaudio.so.2";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int Callback(nint input, nint output, nuint frames, nint timeInfo, nuint flags, nint userData);
    [StructLayout(LayoutKind.Sequential)]
    internal struct HostApiInfo
    {
        internal int Version, Type;
        internal nint Name;
        internal int DeviceCount, DefaultInputDevice, DefaultOutputDevice;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct CallbackTimeInfo
    {
        internal double InputAdcTime, CurrentTime, OutputDacTime;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfo
    {
        internal int Version;
        internal nint Name;
        internal int HostApi, MaxInputChannels, MaxOutputChannels;
        internal double DefaultLowInputLatency, DefaultLowOutputLatency;
        internal double DefaultHighInputLatency, DefaultHighOutputLatency, DefaultSampleRate;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct StreamParameters
    {
        internal int Device, ChannelCount;
        internal nuint SampleFormat;
        internal double SuggestedLatency;
        internal nint HostApiSpecificStreamInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct StreamInfo
    {
        internal int Version;
        internal double InputLatency, OutputLatency, SampleRate;
    }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_Initialize();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_Terminate();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_GetDefaultOutputDevice();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint Pa_GetDeviceInfo(int device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint Pa_GetHostApiInfo(int hostApi);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_GetDeviceCount();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint Pa_GetErrorText(int error);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint Pa_GetVersionText();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_OpenStream(
        out nint stream, nint input, ref StreamParameters output, double sampleRate, nuint frames,
        nuint flags, Callback callback, nint userData);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_StartStream(nint stream);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_CloseStream(nint stream);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int Pa_IsStreamActive(nint stream);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint Pa_GetStreamInfo(nint stream);
}
