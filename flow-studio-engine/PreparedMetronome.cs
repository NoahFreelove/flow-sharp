using Flow.Audio;
using Flow.Music.Model;
using Flow.Studio.Model;

namespace Flow.Studio.Engine;

/// <summary>Audio-owner continuous click monitor. Captured map arrays and the
/// ordinary Flow voice graph keep ReadAt allocation-free, including beyond EOF.</summary>
public sealed class PreparedMetronome : IPreparedTimelineMonitor
{
    private readonly TempoChange[] _tempo;
    private readonly double[] _seconds, _meterQuarters;
    private readonly MeterChange[] _meter;
    private readonly PreparedLiveInstrument _voice;
    private readonly double _volume;
    private long _position = -1, _next;
    private bool _accent;
    public int SampleRate => _voice.SampleRate;
    public int MaxBlockFrames => _voice.MaxBlockFrames;

    public PreparedMetronome(ProjectTempoMap tempo, ProjectMeterMap meter, int sampleRate, int blockFrames, double volume = .25)
    {
        if (!double.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
        _tempo = tempo.Changes.ToArray(); _meter = meter.Changes.ToArray();
        _seconds = _tempo.Select(t => tempo.SecondsAt(t.Quarter)).ToArray();
        _meterQuarters = _meter.Select(m => meter.QuarterAtBar(m.Bar)).ToArray();
        if (_tempo.Max(t => t.Bpm) * _meter.Max(m => (double)m.Denominator) / 240 > sampleRate)
            throw new ArgumentException("Metronome beat interval must be at least one sample");
        var pitches = new double[128]; pitches[0] = 1000; pitches[1] = 1500;
        _voice = new(new(VoiceLimit: 16, VoiceGraph: MetronomePlayback.InstrumentGraph()), new MidiPitchMap(pitches), sampleRate, blockFrames);
        _volume = volume;
    }

    public void Reset(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        _voice.Reset(frame); _position = frame; FindNext(frame);
    }

    public void ReadAt(Span<float> output, long projectFrame, bool advancing = true)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames || projectFrame < 0 ||
            projectFrame > long.MaxValue - output.Length / 2) throw new ArgumentException("Invalid metronome block");
        output.Clear();
        if (output.IsEmpty) return;
        if (!advancing) { Reset(projectFrame); return; }
        if (_position != projectFrame) Reset(projectFrame);
        int done = 0, frames = output.Length / 2;
        while (done < frames)
        {
            if (_next == _position)
            {
                // Retrigger releases the prior same-pitch voice. The graph's decay
                // reaches silence without waiting for an external note-off.
                _voice.TryWrite(0x90, (byte)(_accent ? 1 : 0), 127);
                FindNext(_position + 1);
            }
            int count = (int)Math.Min(frames - done, Math.Max(1, _next - _position));
            var block = output.Slice(done * 2, count * 2);
            _voice.ReadAt(block, _position);
            for (int i = 0; i < block.Length; i++) block[i] = (float)(block[i] * _volume);
            done += count; _position += count;
        }
    }

    private void FindNext(long frame)
    {
        double seconds = (double)frame / SampleRate;
        int t = 0;
        while (t + 1 < _tempo.Length && _seconds[t + 1] <= seconds) t++;
        double quarter = _tempo[t].Quarter + (seconds - _seconds[t]) * (_tempo[t].Bpm / 60);
        int m = 0;
        while (m + 1 < _meter.Length && _meterQuarters[m + 1] <= quarter) m++;
        double ordinal = Math.Max(0, Math.Floor((quarter - _meterQuarters[m]) / (4.0 / _meter[m].Denominator)));
        while (true)
        {
            double q = _meterQuarters[m] + ordinal * (4.0 / _meter[m].Denominator);
            if (m + 1 < _meter.Length && q >= _meterQuarters[m + 1]) { m++; ordinal = 0; continue; }
            t = 0; while (t + 1 < _tempo.Length && _tempo[t + 1].Quarter <= q) t++;
            double at = Math.Round((_seconds[t] + (q - _tempo[t].Quarter) * (60 / _tempo[t].Bpm)) * SampleRate, MidpointRounding.AwayFromZero);
            if (!double.IsFinite(at) || at >= long.MaxValue) { _next = long.MaxValue; return; }
            if (at >= frame) { _next = (long)at; _accent = ordinal % _meter[m].Numerator == 0; return; }
            if (ordinal + 1 == ordinal) { _next = long.MaxValue; return; }
            ordinal++;
        }
    }
}
