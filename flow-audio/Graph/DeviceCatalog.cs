namespace Flow.Audio.Graph;

public enum SignalKind { StereoAudio }
public sealed record DeviceParameter(string Id, string Unit, double Minimum, double Maximum,
    double Default, double SmoothingMilliseconds, bool Automatable = true)
{
    public void Validate(double value)
    {
        if (!double.IsFinite(value) || value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(Id, $"{Id} must be finite and within [{Minimum}, {Maximum}]");
    }
}

/// <summary>Versioned processor contract used by authoring, UI and preparation.
/// Kernels have zero scheduling latency; finite delay declares its maximum tail. Reset snaps smoothing to targets.
/// Bypass effects pass their input unchanged; input/sum cannot be bypassed.</summary>
public sealed record DeviceDefinition(string Id, int Version, int MinimumInputs, int MaximumInputs,
    IReadOnlyList<DeviceParameter> Parameters, bool CanBypass,
    SignalKind Signal = SignalKind.StereoAudio, int LatencyFrames = 0, double TailSeconds = 0);

public static class DeviceCatalog
{
    private static DeviceDefinition Device(string id, int min, int max, bool bypass, params DeviceParameter[] parameters) =>
        new(id, 1, min, max, Array.AsReadOnly(parameters), bypass);
    public static IReadOnlyList<DeviceDefinition> Devices { get; } = Array.AsReadOnly(new[]
    {
        Device("flow.input", 0, 0, false, new DeviceParameter("bus", "index", 0, 63, 0, 0, false)),
        Device("flow.value", 0, 0, false, new DeviceParameter("value", "linear", -1000000, 1000000, 0, 5)),
        Device("flow.multiply", 2, 2, false),
        Device("flow.tanh", 1, 1, false),
        Device("flow.sample", 2, 2, false,
            new DeviceParameter("asset", "index", 0, 255, 0, 0, false),
            new DeviceParameter("rootHz", "Hz", 1, 100000, 440, 0, false)),
        Device("flow.sineOsc", 1, 1, false),
        Device("flow.sawOsc", 1, 1, false),
        Device("flow.squareOsc", 1, 1, false),
        Device("flow.lowPass", 2, 2, false) with { TailSeconds = 2 },
        Device("flow.adsr", 1, 1, false,
            new DeviceParameter("attackMs", "ms", 0, 10000, 5, 0, false),
            new DeviceParameter("decayMs", "ms", 0, 10000, 100, 0, false),
            new DeviceParameter("sustain", "linear", 0, 1, .7, 0, false),
            new DeviceParameter("releaseMs", "ms", 0, 10000, 100, 0, false)) with { TailSeconds = 10 },
        Device("flow.gain", 1, 1, true, new DeviceParameter("gain", "linear", 0, 16, 1, 5)),
        // Same mono-downmix constant-power law as the legacy Flow pan buffer API.
        Device("flow.pan", 1, 1, true, new DeviceParameter("pan", "normalized", -1, 1, 0, 5)),
        Device("flow.drive", 1, 1, true, new DeviceParameter("drive", "linear", 1, 32, 1, 5)),
        // Finite echo train: repeat count bounds both callback cost and tail duration.
        Device("flow.delay", 1, 1, true,
            new DeviceParameter("timeMs", "ms", 1, 2000, 250, 0, false),
            new DeviceParameter("repeats", "count", 1, 16, 4, 0, false),
            new DeviceParameter("feedback", "linear", 0, 0.95, 0.5, 5),
            new DeviceParameter("wet", "linear", 0, 1, 0.5, 5)) with { TailSeconds = 32 },
        Device("flow.sum", 2, 64, false),
    });
    public static DeviceDefinition Get(string id, int version = 1) =>
        Devices.FirstOrDefault(d => d.Id == id && d.Version == version)
        ?? throw new ArgumentException($"Unknown device/version {id}@{version}");
}
