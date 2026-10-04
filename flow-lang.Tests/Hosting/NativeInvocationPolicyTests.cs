using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;
namespace FlowLang.Tests.Hosting;
public class NativeInvocationPolicyTests
{
    [Fact]
    public void CapturedNativeDelegatesAndReplacementsCannotBypassPolicy()
    {
        var signature = new FunctionSignature("external", [StringType.Instance]); int calls = 0;
        var registry = new InternalFunctionRegistry(_ => false);
        Value Body(IReadOnlyList<Value> args) { calls++; return args[0]; }
        registry.Register("external", signature, Body);
        Assert.True(registry.TryGetImplementation("external", signature, out var captured, out _));
        Assert.Throws<InvalidOperationException>(() => captured!([new Value("x", StringType.Instance)]));
        registry.ReplaceAll("external", signature, Body);
        Assert.True(registry.TryGetImplementation("external", signature, out var replacement, out _));
        Assert.Throws<InvalidOperationException>(() => replacement!([new Value("x", StringType.Instance)]));
        Assert.Equal(0, calls);
    }
    [Fact]
    public void DeniedInvocationDoesNotBreakImportsOrAnIndependentUnrestrictedEngine()
    {
        using var sink = new StringWriter();
        using var restricted = new FlowEngine(new EngineOptions { Output = sink, Diagnostics = TextWriter.Null, NativeFunctionPolicy = s => s.Name != "print" });
        Assert.True(restricted.Evaluate("use \"@core\"").Succeeded);
        var denied = restricted.Evaluate("(print \"must not print\")");
        Assert.False(denied.Succeeded); Assert.Equal("", sink.ToString());
        using var ordinary = new FlowEngine(new EngineOptions { Output = sink, Diagnostics = TextWriter.Null });
        Assert.True(ordinary.Evaluate("use \"@core\"\n(print \"allowed\")").Succeeded);
        Assert.Contains("allowed", sink.ToString());
    }
}
