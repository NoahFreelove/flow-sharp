using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Model;

namespace Flow.Studio.Engine;

/// <summary>Prepared click voices using ordinary Flow graph devices. Scheduling
/// and construction happen off the audio callback; exportable graph is the same
/// definition consumed by the voice renderer.</summary>
public static class MetronomePlayback
{
    public static AudioGraphDefinition InstrumentGraph() => AudioGraphDefinition.Multiply("click",
        AudioGraphDefinition.Input("pitch", 0).Then("oscillator", "flow.sineOsc"),
        AudioGraphDefinition.Multiply("amplitude", AudioGraphDefinition.Input("velocity", 2),
            AudioGraphDefinition.Input("gate", 1).Then("envelope", "flow.adsr",
                new Dictionary<string, double> { ["attackMs"] = 1, ["decayMs"] = 20, ["sustain"] = 0, ["releaseMs"] = 0 })));

    public static PreparedNotePlayback Prepare(IEnumerable<MetronomeBeat> beats, long totalFrames,
        int sampleRate = 48000, int blockFrames = 256, double volume = .25)
        => Prepare(beats, totalFrames, InstrumentGraph(), sampleRate, blockFrames, volume);

    public static PreparedNotePlayback Prepare(IEnumerable<MetronomeBeat> beats, long totalFrames,
        AudioGraphDefinition instrument, int sampleRate = 48000, int blockFrames = 256, double volume = .25)
    {
        ArgumentNullException.ThrowIfNull(beats); ArgumentNullException.ThrowIfNull(instrument);
        if (totalFrames < 0 || sampleRate is < 1 or > 384000 || !double.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentException("Invalid metronome rendering settings");
        var captured = beats.Take(100001).ToArray();
        if (captured.Length > 100000 || captured.Any(b => b is null || b.Frame < 0 || b.Frame >= totalFrames || b.Bar < 1 || b.Beat < 1))
            throw new ArgumentException("Invalid metronome schedule");
        long gate = Math.Max(1, (long)Math.Ceiling(sampleRate * .025));
        var notes = captured.Select(b => new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), b.Frame,
            b.Frame + Math.Min(gate, totalFrames - b.Frame), b.Accent ? 1500 : 1000, volume));
        return new(notes, sampleRate, blockFrames, totalFrames, new(VoiceLimit: 16, VoiceGraph: instrument));
    }
}
