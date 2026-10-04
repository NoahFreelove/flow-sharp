namespace Flow.Music.Model;

/// <summary>Captured MIDI-key frequencies. Zero denotes an unmapped, silent key.</summary>
public sealed class MidiPitchMap
{
    private readonly double[] _frequencies;
    public static MidiPitchMap EqualTemperament { get; } = new(
        Enumerable.Range(0, 128).Select(key => 440 * Math.Pow(2, (key - 69) / 12.0)));

    public MidiPitchMap(IEnumerable<double> frequencies)
    {
        ArgumentNullException.ThrowIfNull(frequencies);
        _frequencies = frequencies.Take(129).ToArray();
        if (_frequencies.Length != 128 || _frequencies.Any(value => !double.IsFinite(value) || value < 0))
            throw new ArgumentException("A MIDI pitch map requires exactly 128 finite, nonnegative frequencies", nameof(frequencies));
    }

    public double this[int key] => _frequencies[key];
}
