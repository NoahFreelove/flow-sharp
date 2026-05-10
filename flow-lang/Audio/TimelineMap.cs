using FlowLang.Core;

namespace FlowLang.Audio;

/// <summary>
/// Maps a single rendered note/event to a time range in the audio output
/// and a source location in the original code.
/// </summary>
public record TimelineEntry(
    double StartSeconds,
    double EndSeconds,
    SourceLocation SourceStart,
    int SourceLength,
    string ScopeName
);

/// <summary>
/// Accumulates timeline entries during rendering, enabling the editor
/// to highlight source code regions in sync with audio playback.
/// </summary>
public class TimelineMap
{
    private readonly List<TimelineEntry> _entries = new();

    public void Add(TimelineEntry entry) => _entries.Add(entry);

    /// <summary>
    /// Returns all entries active at the given playback time.
    /// </summary>
    public IReadOnlyList<TimelineEntry> GetActiveAt(double seconds)
    {
        var result = new List<TimelineEntry>();
        foreach (var e in _entries)
        {
            if (seconds >= e.StartSeconds && seconds < e.EndSeconds)
                result.Add(e);
        }
        return result;
    }

    /// <summary>
    /// Offsets all entries by the given number of seconds.
    /// Used when concatenating section buffers in song rendering.
    /// </summary>
    public void OffsetAll(double offsetSeconds)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            _entries[i] = e with
            {
                StartSeconds = e.StartSeconds + offsetSeconds,
                EndSeconds = e.EndSeconds + offsetSeconds
            };
        }
    }

    /// <summary>
    /// Merges another timeline map's entries into this one.
    /// </summary>
    public void Merge(TimelineMap other)
    {
        _entries.AddRange(other._entries);
    }

    public IReadOnlyList<TimelineEntry> Entries => _entries;
}
