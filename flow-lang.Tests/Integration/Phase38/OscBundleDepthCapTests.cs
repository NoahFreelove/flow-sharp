using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Network;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 — bundle nesting depth cap 8 per D-38-15
/// (mirrors Phase 36 T-36-17 + Phase 39 D-39-19 DoS-guard precedent).
/// At depth > 8, DispatchPacket aborts further recursion and emits a
/// one-shot stderr advisory per Pattern S2 with sentinel key
/// <c>osc-bundle-depth:{path}</c>.
/// </summary>
[Collection("FlowScripts")]
public class OscBundleDepthCapTests : IDisposable
{
    private readonly List<IReadOnlyList<Value>> _received = new();

    public OscBundleDepthCapTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = (_, args) =>
        {
            lock (_received) _received.Add(args);
        };
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    private static FunctionOverload StubHandler()
    {
        var sig = new FunctionSignature("test_handler",
            new FlowType[] { VoidType.Instance },
            IsVarArgs: true);
        return FunctionOverload.Internal("test_handler", sig, _ => Value.Void());
    }

    private static FlowLang.Runtime.ExecutionContext MakeContext()
    {
        var reporter = new FlowLang.Diagnostics.ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new FlowLang.Runtime.ExecutionContext(reporter, registry);
    }

    /// <summary>
    /// Build a nested bundle of depth N (each level wraps exactly one
    /// inner bundle). At the deepest level, contains a single
    /// <c>OscMessage</c> targeting <paramref name="messagePath"/>.
    /// </summary>
    private static Rug.Osc.OscBundle NestedBundle(int depth, string messagePath, int payload)
    {
        var immediate = new Rug.Osc.OscTimeTag(1UL);
        Rug.Osc.OscPacket inner = new Rug.Osc.OscMessage(messagePath, payload);
        for (int i = 0; i < depth; i++)
            inner = new Rug.Osc.OscBundle(immediate, inner);
        // Cast to OscBundle for the return signature; depth >=1 always
        // yields a bundle, but ensure the outer is bundle (depth>=1).
        return (Rug.Osc.OscBundle)inner;
    }

    /// <summary>
    /// NestedBundleDepth5_Dispatches: depth 5 < cap 8 — the inner message
    /// should fire normally.
    /// </summary>
    [Fact]
    public void NestedBundleDepth5_Dispatches()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        var bundle = NestedBundle(5, "/deep", 99);

        OscFunctions.DispatchPacketForTesting(bundle, "/deep", handler, ctx);

        Assert.Single(_received);
        Assert.Equal(99, _received[0][0].As<int>());
    }

    /// <summary>
    /// NestedBundleDepth12_ClampedAt8_EmitsAdvisory: depth 12 > cap 8 —
    /// recursion stops at depth 8 (no inner message reached); stderr
    /// contains the advisory exactly once.
    /// </summary>
    [Fact]
    public void NestedBundleDepth12_ClampedAt8_EmitsAdvisory()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        var bundle = NestedBundle(12, "/over-deep", 42);

        var oldErr = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            OscFunctions.DispatchPacketForTesting(bundle, "/over-deep", handler, ctx);
        }
        finally
        {
            Console.SetError(oldErr);
        }

        // Handler should NOT have fired — the inner message lives at depth 12
        // which is past the cap. The recursion bails out at depth 9 (one
        // step past the cap) without reaching the message.
        Assert.Empty(_received);

        var errOutput = capture.ToString();
        Assert.Contains("[osc] bundle nesting depth exceeds 8", errOutput);
        Assert.Contains("/over-deep", errOutput);
    }

    /// <summary>
    /// AdvisoryIsOneShot: dispatch two over-deep bundles in succession;
    /// the advisory fires only once per (path) sentinel per process per
    /// Pattern S2 WarnOnce dedup.
    /// </summary>
    [Fact]
    public void AdvisoryIsOneShot()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        var bundle1 = NestedBundle(12, "/dedup", 1);
        var bundle2 = NestedBundle(13, "/dedup", 2);

        var oldErr = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            OscFunctions.DispatchPacketForTesting(bundle1, "/dedup", handler, ctx);
            OscFunctions.DispatchPacketForTesting(bundle2, "/dedup", handler, ctx);
        }
        finally
        {
            Console.SetError(oldErr);
        }

        var errOutput = capture.ToString();
        // Count occurrences of the advisory header — must be exactly 1
        // for the dedup contract.
        int count = 0;
        int idx = 0;
        while ((idx = errOutput.IndexOf("[osc] bundle nesting depth exceeds 8", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }
        Assert.Equal(1, count);
    }
}
