namespace Flow.Studio.Model;

/// <summary>Atomic children apply in order and undo in reverse order. Completed
/// children are rolled back on failure. Children must support their inverse after
/// success; rollback failure is surfaced as an aggregate, never concealed.</summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly IUndoableAction[] _actions;
    public string Description { get; }
    public CompositeAction(string description, IEnumerable<IUndoableAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(actions);
        Description = description;
        _actions = actions.ToArray();
        if (_actions.Length == 0 || _actions.Any(a => a is null))
            throw new ArgumentException("A group needs non-null actions", nameof(actions));
    }

    public void Redo()
    {
        int done = 0;
        try { for (; done < _actions.Length; done++) _actions[done].Redo(); }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            for (int i = done - 1; i >= 0; i--)
                try { _actions[i].Undo(); } catch (Exception rollback) { errors.Add(rollback); }
            if (errors.Count > 1) throw new AggregateException("Composite redo rollback failed", errors);
            throw;
        }
    }

    public void Undo()
    {
        int next = _actions.Length - 1;
        try { for (; next >= 0; next--) _actions[next].Undo(); }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            for (int i = next + 1; i < _actions.Length; i++)
                try { _actions[i].Redo(); } catch (Exception rollback) { errors.Add(rollback); }
            if (errors.Count > 1) throw new AggregateException("Composite undo rollback failed", errors);
            throw;
        }
    }
}
