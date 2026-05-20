using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-07 Wave 0 — `-> CALL as name` type carry-through gates (LANG-03).
///
/// Pins the trivial-but-load-bearing claim that the Value bound under an
/// `as` clause carries the RHS function's return-type as its
/// <see cref="Value.Type"/>. No new type-inference machinery is needed —
/// the Value's Type field already records its type at evaluation time;
/// the binding step just stashes it under a name.
///
/// RED state: same as AsBindingScopeTests — the binding doesn't yet
/// happen until Task 4.
/// </summary>
public class AsBindingTypeTests
{
    [Fact]
    public void IntermediateNameValueTypeMatchesRhsReturnType()
    {
        // 5 -> (mul 2) as doubled — `(mul 2 5)` returns an Int; the bound
        // `doubled` Value must carry IntType.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "5 -> (mul 2) as doubled\n" +
            "Int captured = doubled\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var doubledValue = engine.Context.GetVariable("doubled");
        Assert.IsType<IntType>(doubledValue.Type);
        Assert.Equal(10, doubledValue.As<int>());
    }
}
