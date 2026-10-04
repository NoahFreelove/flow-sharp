using Flow.Audio.Graph;

namespace Flow.Audio;

public sealed record NoteInstrumentDefinition(string Id, int Version, string InputPort, string OutputPort,
    IReadOnlyList<DeviceParameter> Parameters, int LatencyFrames, string ResetPolicy);

public static class NoteInstrumentCatalog
{
    public static NoteInstrumentDefinition Sine { get; } = new("flow.sine", 1, "tuned-notes", "stereo-audio",
        Array.AsReadOnly(new[]
        {
            new DeviceParameter("voices", "count", 1, 256, 64, 0, false),
            new DeviceParameter("attack", "milliseconds", 0, 1000, 5, 0, false),
            new DeviceParameter("release", "milliseconds", 0, 10000, 20, 0, false),
        }), 0, "Retrigger held notes at phase zero; clear released voices on seek/stop/loop");
    public static NoteInstrumentDefinition Sampler { get; } = new("flow.sampler", 1, "tuned-notes", "stereo-audio",
        Array.AsReadOnly(Sine.Parameters.Concat(new[] { new DeviceParameter("rootHz", "Hz", 1, 100000, 440, 0, false) }).ToArray()),
        0, "Retrigger held notes from sample start; clear released voices on seek/stop/loop");
}
