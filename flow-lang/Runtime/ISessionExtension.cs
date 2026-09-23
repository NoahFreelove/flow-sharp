namespace FlowLang.Runtime;

/// <summary>
/// Per-context state a domain layer attaches to an <see cref="ExecutionContext"/>
/// (see <see cref="ExecutionContext.GetExtension{T}"/>). Test isolation snapshots
/// the state before each pure-Flow test and restores it afterwards.
/// </summary>
public interface ISessionExtension
{
    /// <summary>Captures this extension's mutable state.</summary>
    object? Snapshot(ExecutionContext context);

    /// <summary>Reinstates state captured by <see cref="Snapshot"/>.</summary>
    void Restore(ExecutionContext context, object? snapshot);
}
