using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 — serializes every test class that invokes
/// <see cref="FlowLang.Runtime.WasmEntry.RunFromJs"/>.
///
/// <para><b>Why:</b> <c>RunFromJs</c> redirects the PROCESS-WIDE
/// <see cref="System.Console.Out"/> / <see cref="System.Console.Error"/> to
/// per-call <c>StringWriter</c> sinks (D-48-15 stdout/stderr capture) and
/// restores them in a <c>finally</c>. xUnit runs distinct test CLASSES in
/// parallel by default, so two classes each calling <c>RunFromJs</c> can
/// interleave their <c>Console.SetOut</c> redirection — one class's captured
/// stdout then leaks into the other's <c>RunResult.Stdout</c>, breaking the
/// two-run byte-identical determinism assertion non-deterministically.</para>
///
/// <para>Placing every <c>RunFromJs</c>-calling class in this single collection
/// forces them to run SERIALLY (xUnit never parallelizes within one collection),
/// eliminating the Console-redirection race without weakening any assertion.
/// Tests WITHIN a class already run serially, so a class alone was never at
/// risk; the hazard is strictly cross-class parallelism.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class WasmEntryConsoleCollection
{
    public const string Name = "WasmEntry Console-redirection (serial)";
}
