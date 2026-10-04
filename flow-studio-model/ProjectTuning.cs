namespace Flow.Studio.Model;

/// <summary>Portable tuning definition. Scala/keyboard-map text is captured data,
/// never a path; the music host parses it before evaluating a build.</summary>
public sealed record ProjectTuning
{
    public string System { get; }
    public string Key { get; }
    public string? Scala { get; }
    public string? KeyboardMap { get; }
    public ProjectTuning(string system = "EqualTemperament", string key = "Cmajor", string? scala = null, string? keyboardMap = null)
    {
        if (system is not ("EqualTemperament" or "JustIntonation" or "Pythagorean")) throw new ArgumentException("Unknown tuning system");
        string[] roots = ["C", "Csharp", "Db", "D", "Dsharp", "Eb", "E", "F", "Fsharp", "Gb", "G", "Gsharp", "Ab", "A", "Asharp", "Bb", "B"];
        string[] modes = ["major", "minor", "dorian", "phrygian", "lydian", "mixolydian", "locrian"];
        if (!roots.Any(r => modes.Any(m => r + m == key))) throw new ArgumentException("Unknown tuning key/mode");
        if (scala is { Length: > 262144 } || keyboardMap is { Length: > 262144 } ||
            scala is not null && string.IsNullOrWhiteSpace(scala) || keyboardMap is not null && (scala is null || string.IsNullOrWhiteSpace(keyboardMap)))
            throw new ArgumentException("Invalid captured Scala/keyboard-map text");
        System = system; Key = key; Scala = scala; KeyboardMap = keyboardMap;
    }
}
