namespace Flow.Audio.Graph;

/// <summary>One public instrument instance used by multiple track voice pools.
/// The parent playback consumes a complete update before reading any track.
/// Use exclusively instead of individual pool setters. One producer, one audio owner.</summary>
public sealed class PreparedInstrumentControlGroup
{
    private readonly PreparedNotePlayback[] _pools;
    private Dictionary<(string, string), GraphParameterValue> _values = new();
    private PreparedAudioGraph.ParameterCommand[][]? _pending;
    public PreparedInstrumentControlGroup(IEnumerable<PreparedNotePlayback> pools, IEnumerable<PreparedLiveInstrument>? monitors = null)
    {
        var live = (monitors ?? []).Take(129).ToArray();
        if (live.Any(m => m is null)) throw new ArgumentException("Monitor pools cannot be null");
        _pools = pools.Take(129).Concat(live.Select(m => m.VoicePool)).Take(129).ToArray();
        if (_pools.Length is < 1 or > 128 || _pools.Any(p => p is null) || _pools.Distinct().Count() != _pools.Length)
            throw new ArgumentException("Provide 1–128 independent scheduled/live voice pools");
    }
    public void SetLatestParameters(IEnumerable<GraphParameterValue> values)
    {
        var updates = values.Take(4097).ToArray();
        if (updates.Length is < 1 or > 4096 || updates.Select(v => (v.NodeId, v.ParameterId)).Distinct().Count() != updates.Length)
            throw new ArgumentException("Invalid instrument parameter batch");
        var next = new Dictionary<(string, string), GraphParameterValue>(_values);
        foreach (var value in updates) next[(value.NodeId, value.ParameterId)] = value;
        if (next.Count > 4096) throw new ArgumentException("Instrument parameter budget exceeded");
        // Validate every pool before publishing anything; graphs may have different indices.
        var commands = _pools.Select(p => p.PrepareVoiceParameters(next.Values)).ToArray();
        _values = next;
        Volatile.Write(ref _pending, commands);
    }
    internal void Apply()
    {
        var commands = Interlocked.Exchange(ref _pending, null);
        if (commands is null) return;
        for (int i = 0; i < _pools.Length; i++) _pools[i].ApplyVoiceParameterBatch(commands[i]);
    }
}
