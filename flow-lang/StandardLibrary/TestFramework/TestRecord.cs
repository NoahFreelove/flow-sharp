using FlowLang.Core;
using FlowLang.Runtime;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 — one entry per <c>(test "name" body)</c>
/// registration. Accumulated on <see cref="ExecutionContext.TestRegistry"/>
/// at evaluation time; consumed by <see cref="TestRunner"/> which forces
/// <see cref="BodyThunk"/> inside a Snapshot/Restore guard per test.
///
/// <para>
/// <see cref="Span"/> is currently <c>Span.Unknown</c> by default — the
/// (test ...) builtin does not yet have access to the AST node's Span at
/// the InternalFunctionRegistry call site. Plan 35-03's Diagnostics wiring
/// is the canonical path for surfacing the registering call site in
/// failure diagnostics; until then the Span field exists so the renderer
/// can pick it up the moment the wiring lands without a TestRecord shape
/// change.
/// </para>
/// </summary>
public record TestRecord(string Name, Thunk BodyThunk, Span Span);
