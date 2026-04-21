using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 05 Task 2 Facts pinning the RegisterSignaturesOnly contract.
/// D-07 completeness: every built-in (core + audio + transforms + harmony + …) must
/// surface via EnumerateSignatures. Invoking any stub must throw NotSupportedException.
/// </summary>
public class BuiltInFunctionsTests
{
    [Fact]
    public void RegisterSignaturesOnly_CoversCoreAudioTransformsHarmony()
    {
        var r = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(r);
        var names = r.EnumerateSignatures().Select(kv => kv.Key).ToHashSet();

        // Representative probes across every major category (D-07 full coverage):
        Assert.Contains("print", names);       // core I/O
        Assert.Contains("concat", names);      // string / collection
        Assert.Contains("map", names);         // higher-order collection
        Assert.Contains("sin", names);         // math
        Assert.Contains("mix", names);         // audio core
        Assert.Contains("reverb", names);      // audio effect
        Assert.Contains("pan", names);         // audio panning
        Assert.Contains("compress", names);    // audio dynamics
        Assert.Contains("transpose", names);   // transform
        Assert.Contains("invert", names);      // transform
        Assert.Contains("chordNotes", names);  // harmony
        Assert.Contains("arpeggio", names);    // harmony
        Assert.Contains("visualize", names);   // visualization
        Assert.Contains("euclidean", names);   // generative / musical notation
        Assert.Contains("play", names);        // playback (signature only — delegate is a stub)
        Assert.Contains("writeWav", names);    // audio file IO
    }

    [Fact]
    public void RegisterSignaturesOnly_StubThrowsWhenInvoked()
    {
        var r = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(r);

        // Pick 'print' — a 1-arg String signature we know is registered.
        var printSig = new FunctionSignature("print", [StringType.Instance]);
        Assert.True(r.TryGetImplementation("print", printSig, out var impl, out _));
        Assert.NotNull(impl);

        // Invoking any stub is always a bug in the LSP (it introspects, never executes).
        var args = new List<Value> { Value.String("x") };
        Assert.Throws<NotSupportedException>(() => impl!(args));
    }

    [Fact]
    public void RegisterSignaturesOnly_DoesNotRegressRegisterAllImplementations()
    {
        // Sanity check: calling RegisterAllImplementations on a fresh registry still
        // works (the signatures-only path is additive, not a replacement).
        var r1 = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(r1);
        Assert.True(r1.HasImplementation("print"));
        Assert.True(r1.HasImplementation("reverb"));
    }
}
