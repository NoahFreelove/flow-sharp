namespace Flow.Studio.Model;

/// <summary>Undo/Redo must be atomic: on failure leave their target unchanged.
/// Capture IDs and results at construction; never reevaluate user code during redo.</summary>
public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}

/// <summary>Single control-thread history. Entries move only after success. The
/// caller supplies conservative retained-byte costs; this is not runtime heap metering.</summary>
public sealed class ActionHistory
{
    private sealed record Entry(IUndoableAction Action, Guid Before, Guid After, long Bytes);
    private readonly List<Entry> _undo = [], _redo = [];
    private readonly int _capacity;
    private readonly long _budget;
    private Guid _revision = Guid.NewGuid(), _saved;
    private bool _busy;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public bool IsDirty => _revision != _saved;
    public long RetainedBytes { get; private set; }
    public string? UndoDescription => _undo.Count == 0 ? null : _undo[^1].Action.Description;
    public string? RedoDescription => _redo.Count == 0 ? null : _redo[^1].Action.Description;

    public ActionHistory(int capacity = 128, long budgetBytes = 64 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        _capacity = capacity;
        _budget = budgetBytes;
        _saved = _revision;
    }

    public void Execute(IUndoableAction action, long retainedBytes)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (retainedBytes <= 0 || retainedBytes > _budget)
            throw new ArgumentOutOfRangeException(nameof(retainedBytes), "Action exceeds history budget");
        Enter();
        try
        {
            // Reserve list storage before mutating the document.
            _undo.EnsureCapacity(_undo.Count + 1);
            var entry = new Entry(action, _revision, Guid.NewGuid(), retainedBytes);
            action.Redo();
            foreach (var previous in _redo) RetainedBytes -= previous.Bytes;
            _redo.Clear();
            _undo.Add(entry);
            _revision = entry.After;
            while (_undo.Count > _capacity || RetainedBytes > _budget - retainedBytes)
            {
                RetainedBytes -= _undo[0].Bytes;
                _undo.RemoveAt(0);
            }
            RetainedBytes += retainedBytes;
        }
        finally { _busy = false; }
    }

    public bool Undo() => Move(_undo, _redo, undo: true);
    public bool Redo() => Move(_redo, _undo, undo: false);
    public void MarkSaved()
    {
        if (_busy) throw new InvalidOperationException("History is already applying an action");
        _saved = _revision;
    }
    /// <summary>Recovered contents have not been saved to the working project.
    /// Mark them dirty without inventing an undo action.</summary>
    public void MarkUnsaved()
    {
        if (_busy) throw new InvalidOperationException("History is already applying an action");
        _saved = Guid.Empty;
    }

    private bool Move(List<Entry> from, List<Entry> to, bool undo)
    {
        Enter();
        try
        {
            if (from.Count == 0) return false;
            to.EnsureCapacity(to.Count + 1);
            var entry = from[^1];
            if (undo) entry.Action.Undo(); else entry.Action.Redo();
            from.RemoveAt(from.Count - 1);
            to.Add(entry);
            _revision = undo ? entry.Before : entry.After;
            return true;
        }
        finally { _busy = false; }
    }

    private void Enter()
    {
        if (_busy) throw new InvalidOperationException("History actions cannot reenter their history");
        _busy = true;
    }
}
