namespace Flow.Music.IO;

/// <summary>
/// The single sequence-name → General MIDI routing table for MIDI, MusicXML and
/// LilyPond output. Matching is case-insensitive by prefix; order is significant:
/// specific names precede generic ones (<c>horn</c> before <c>brass</c>,
/// <c>bassoon</c> before <c>bass</c>, <c>harpsichord</c> before <c>harp</c>). <c>sampler:NAME</c> routes like <c>NAME</c>.
/// Unrecognized names use program 0 on channel 0; drums and timpani use channel 9.
/// </summary>
public static class InstrumentRouting
{
    private static readonly (string Prefix, int Program, int Channel)[] Table =
    [
        ("violin", 40, 0), ("viola", 41, 0), ("cello", 42, 0), ("contrabass", 43, 0),
        ("oboe", 68, 0), ("clarinet", 71, 0), ("bassoon", 70, 0), ("horn", 60, 0),
        ("trombone", 57, 0), ("tuba", 58, 0), ("timpani", 47, 9), ("choir", 52, 0),
        ("harpsichord", 6, 0), ("harp", 46, 0), ("guitar", 24, 0), ("celeste", 8, 0),
        ("piano", 0, 0), ("brass", 56, 0), ("bass", 32, 0), ("sax", 65, 0),
        ("flute", 73, 0), ("string", 48, 0), ("organ", 19, 0), ("bell", 14, 0),
        ("drum", 0, 9),
    ];

    /// <summary>Removes a leading <c>sampler:</c> (any case) so names display canonically.</summary>
    public static string StripSamplerPrefix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.StartsWith("sampler:", StringComparison.OrdinalIgnoreCase) ? name["sampler:".Length..] : name;
    }

    public static (int gmProgram, int channel) ResolveGmProgram(string sequenceName)
    {
        if (string.IsNullOrEmpty(sequenceName)) return (0, 0);
        string name = StripSamplerPrefix(sequenceName).ToLowerInvariant();
        foreach (var (prefix, program, channel) in Table)
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return (program, channel);
        return (0, 0);
    }
}
