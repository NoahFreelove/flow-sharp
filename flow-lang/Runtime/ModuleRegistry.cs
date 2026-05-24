using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 43 Plan 43-02 (D-05 / D-02) — per-context module registry. Populated
/// by <c>ModuleLoader</c> at <c>use</c>-time when the loaded file declares
/// <c>module &lt;name&gt;</c> as its first non-comment statement. Read at
/// qualified-access dispatch time by
/// <c>ExpressionEvaluator.EvaluateMemberAccess</c> (registry-first branch per
/// D-02). Files without a <c>module</c> declaration are absent from this
/// registry per D-01 back-compat — they continue to expose procs UNQUALIFIED
/// in the caller's <see cref="ExecutionContext"/>, with no qualified-access
/// surface at all.
///
/// <para>
/// Mirrors the <see cref="LiveBlockRegistry"/> / <see cref="PrngRegistry"/> /
/// <c>StyleRegistry</c> shape — singleton-per-<see cref="ExecutionContext"/>,
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>-backed. The per-context
/// (NOT process-global) lifetime is load-bearing for Phase 35 TEST-02 hermetic
/// test isolation; a static singleton would leak module registrations across
/// FlowEngine instances. See RESEARCH §"Alternatives Considered" line 147 +
/// §Pattern 4 line 350 for the rationale.
/// </para>
///
/// <para>
/// Concurrency: <see cref="ConcurrentDictionary{TKey, TValue}"/> backing
/// matches the two-actor pattern documented at LiveBlockRegistry (background
/// re-render + audio playback thread). Per D-06, duplicate registrations
/// (two files declare the same <c>module</c> name) keep the LATEST proc set
/// (last-write-wins); the user-facing one-shot advisory is the caller's
/// responsibility — <c>ModuleLoader</c> (Plan 43-03) checks
/// <see cref="Contains"/> BEFORE calling <see cref="Register"/> and emits the
/// <c>[module] duplicate module name '...' — last load wins</c> diagnostic
/// at that hook point.
/// </para>
/// </summary>
public sealed class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, Value>> _modules = new();

    /// <summary>
    /// Returns <c>true</c> if a module with the given name has been registered
    /// in this context. Consumed by <c>ModuleLoader</c> (Plan 43-03) at use-time
    /// BEFORE the matching <see cref="Register"/> call so the duplicate-name
    /// advisory (D-06) can fire on the COLLISION, not after.
    /// </summary>
    public bool Contains(string moduleName) => _modules.ContainsKey(moduleName);

    /// <summary>
    /// Registers (or replaces) the exported procs for <paramref name="moduleName"/>.
    /// Last-write-wins per D-06 — the caller is responsible for the advisory
    /// when <see cref="Contains"/> already returned true for this name.
    /// Replacement is intentional: the second registration's proc set
    /// COMPLETELY supplants the first; procs that existed only in the prior
    /// registration are no longer reachable through this registry.
    /// </summary>
    public void Register(string moduleName, IReadOnlyDictionary<string, Value> exportedProcs)
    {
        _modules[moduleName] = exportedProcs;
    }

    /// <summary>
    /// Qualified-access lookup. Returns the <see cref="Value"/> registered as
    /// <paramref name="procName"/> inside the module
    /// <paramref name="moduleName"/>'s exported set. Returns <c>false</c>
    /// (and a null <paramref name="procValue"/>) when either the module is
    /// unregistered OR the proc name is absent from its set.
    ///
    /// <para>
    /// Consumed by <c>ExpressionEvaluator.EvaluateMemberAccess</c>'s
    /// registry-first branch (Plan 43-03 per D-02): for
    /// <c>math.sin(0.5)</c>, the dispatcher peeks at the LHS as a bare
    /// identifier and calls <see cref="TryGetProc"/> BEFORE attempting to
    /// evaluate <c>math</c> as a variable. A hit short-circuits to the
    /// returned Function Value; a miss falls through to the existing
    /// instance-member path (<c>chord.root</c>, <c>song.sections</c>, etc.).
    /// </para>
    /// </summary>
    public bool TryGetProc(string moduleName, string procName, out Value? procValue)
    {
        if (_modules.TryGetValue(moduleName, out var procs)
            && procs.TryGetValue(procName, out var value))
        {
            procValue = value;
            return true;
        }
        procValue = null;
        return false;
    }

    /// <summary>
    /// Returns an immutable snapshot of the registry — keys are module names,
    /// values are the most-recently-registered exported-procs dict per module.
    /// Mirrors <see cref="LiveBlockRegistry.Snapshot"/> — the copy lets callers
    /// iterate without seeing mid-iteration concurrent mutations from a
    /// background <c>ModuleLoader</c> registration.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Value>> Snapshot()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, Value>>(_modules);
    }

    /// <summary>
    /// Clears all registered modules. Provided for test-isolation parity with
    /// <see cref="LiveBlockRegistry.Clear"/> and
    /// <see cref="PrngRegistry.ResetAtRenderBoundary"/>; production code paths
    /// in Plan 43-03 do NOT call this (module registrations persist for the
    /// lifetime of the <see cref="ExecutionContext"/>, since <c>use</c>
    /// itself dedupes via <c>ModuleLoader._loadedModules</c>).
    /// </summary>
    public void Clear()
    {
        _modules.Clear();
    }
}
