using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-03 Task 1 — Facts pinning the Axis A strict-tier filter
/// in <see cref="FunctionSignature.Matches"/> + <see cref="OverloadResolver"/>.
///
/// <para>
/// RESEARCH Pitfall 1 (the most dangerous landmine of the phase): the
/// "+100 convertible" tier in non-strict spans TWO clauses in the
/// <c>Matches</c> for-loop:
/// </para>
/// <list type="bullet">
///   <item>Clause (a) — <c>argTypes[i].CanConvertTo(InputTypes[i])</c>
///         covers numeric widening (Int → Long → Float → Double).</item>
///   <item>Clause (b) — <c>InputTypes[i].IsCompatibleWith(argTypes[i])</c>
///         covers inverse-direction music-type widening (e.g.
///         <c>Semitone.IsCompatibleWith(Int) = true</c> makes
///         <c>(transpose seq 2)</c> match <c>transpose(Sequence, Semitone)</c>).</item>
/// </list>
///
/// <para>
/// Strict mode drops BOTH clauses; only the exact (+1000) and compatible
/// (+500) tiers remain. Default-false <c>strictMode</c> parameter
/// preserves byte-identical non-strict behavior at every existing call site.
/// </para>
///
/// <para>
/// Plan 44-03 §interfaces named <c>(gain buf -12.0)</c> as the canonical
/// clause-(b) example, but the production registry already ships BOTH
/// <c>gain(Buffer, Double)</c> AND <c>gain(Buffer, Decibel)</c> — so a raw
/// Double argument hits the exact-match <c>gain(Buffer, Double)</c>
/// overload regardless of strict mode. The actual single-overload Pitfall-1
/// clause-(b) sites in the production codebase are
/// <c>transpose(Sequence, Semitone)</c> (matched by raw Int via
/// <c>Semitone.IsCompatibleWith(Int)</c>) and the 3-arg
/// <c>reverb(Buffer, Double, Second)</c> (matched by 3-arg raw Double via
/// <c>Second.IsCompatibleWith(Double)</c>). These are used here; see SUMMARY
/// "Deviations" §1 for the rationale.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class OverloadResolverStrictTierTests
{
    // =====================================================================
    // Helpers
    // =====================================================================

    /// <summary>
    /// Build a sandbox engine and Execute <paramref name="source"/>; return
    /// the engine for post-execute introspection (errors, success bit).
    /// Mirrors the pattern from <see cref="CallerStrictModeSnapshotTests"/>.
    /// </summary>
    private static (FlowEngine engine, bool ok) RunSource(string source)
    {
        var engine = new FlowEngine();
        var ok = engine.Execute(source, "<test>");
        return (engine, ok);
    }

    /// <summary>
    /// Assert that <paramref name="engine"/>'s error reporter contains
    /// a "No matching overload for function '<paramref name="fnName"/>'"
    /// error (substring match). Asserts <c>HasErrors</c> first so a
    /// missing-overload regression doesn't silently pass.
    /// </summary>
    private static void AssertNoMatchingOverload(FlowEngine engine, string fnName)
    {
        Assert.True(engine.ErrorReporter.HasErrors,
            "expected ErrorReporter to have errors; saw none. Engine output: "
            + engine.ErrorReporter.FormatErrors());
        var formatted = engine.ErrorReporter.FormatErrors();
        Assert.Contains(
            $"No matching overload for function '{fnName}'",
            formatted);
    }

    // =====================================================================
    // .flow source-level Facts (end-to-end strict pragma → resolver gate)
    // =====================================================================

    /// <summary>
    /// Pitfall 1 clause (a): numeric widening. <c>(add 1 2.5)</c> in a strict
    /// file must produce "No matching overload" — <c>Int.CanConvertTo(Double)</c>
    /// is dropped under strict, so neither <c>(Int,Int)</c> nor
    /// <c>(Double,Double)</c> can accept the mixed-arity call.
    /// </summary>
    [Fact]
    public void Fact_StrictDropsNumericWidening_AddIntDouble_Fails()
    {
        var (engine, ok) = RunSource(
            "enable strict;\n"
            + "Double r = (add 1 2.5)\n");
        try
        {
            Assert.False(ok, "expected strict (add 1 2.5) to fail overload resolution");
            AssertNoMatchingOverload(engine, "add");
        }
        finally { engine.Dispose(); }
    }

    /// <summary>
    /// Pitfall 1 clause (b): inverse-direction music-type widening.
    /// <c>(transpose seq 2)</c> in a strict file must produce "No matching
    /// overload" — <c>Semitone.IsCompatibleWith(Int)</c> is dropped under
    /// strict, and no <c>transpose(Sequence, Int)</c> overload exists.
    /// Composer escape hatch: <c>(transpose seq +2st)</c> (Semitone literal,
    /// exact match) — verified by
    /// <see cref="Fact_StrictAcceptsExactSemitone_TransposeSeqPlusTwoSt_Succeeds"/>.
    /// </summary>
    [Fact]
    public void Fact_StrictDropsInverseMusicTypeWidening_TransposeSeqInt_Fails()
    {
        var (engine, ok) = RunSource(
            "enable strict;\n"
            + "Sequence seq = | C4q D4q |\n"
            + "Sequence shifted = (transpose seq 2)\n");
        try
        {
            Assert.False(ok, "expected strict (transpose seq 2) to fail overload resolution");
            AssertNoMatchingOverload(engine, "transpose");
        }
        finally { engine.Dispose(); }
    }

    /// <summary>
    /// Pitfall 1 clause (b) — secondary case in reverb. The 3-arg
    /// <c>reverb(Buffer, Double, Second)</c> overload accepts
    /// <c>(reverb buf 0.5 1.5)</c> non-strict via
    /// <c>Second.IsCompatibleWith(Double)</c>. Strict drops the clause →
    /// no matching overload (4-arg ReverbFull has wrong arity; only the
    /// Second-3-arg form matches 3 args at all).
    /// </summary>
    [Fact]
    public void Fact_StrictDropsInverseMusicTypeWidening_ReverbBufRoomDouble_Fails()
    {
        var (engine, ok) = RunSource(
            "enable strict;\n"
            + "Buffer src = (createSineTone 0.5 440.0 0.5)\n"
            + "Buffer wet = (reverb src 0.5 1.5)\n");
        try
        {
            Assert.False(ok, "expected strict (reverb src 0.5 1.5) to fail overload resolution");
            AssertNoMatchingOverload(engine, "reverb");
        }
        finally { engine.Dispose(); }
    }

    /// <summary>
    /// Composer escape hatch: <c>(transpose seq +2st)</c> with the Semitone
    /// literal hits the exact-match <c>transpose(Sequence, Semitone)</c>
    /// overload (+1000 specificity). Must succeed in BOTH modes.
    /// </summary>
    [Fact]
    public void Fact_StrictAcceptsExactSemitone_TransposeSeqPlusTwoSt_Succeeds()
    {
        // Strict — should succeed (exact-match Semitone hits +1000 tier).
        var (engineStrict, okStrict) = RunSource(
            "enable strict;\n"
            + "Sequence seq = | C4q D4q |\n"
            + "Sequence shifted = (transpose seq +2st)\n");
        try
        {
            Assert.True(okStrict,
                "strict (transpose seq +2st) should succeed (exact-match Semitone tier +1000). "
                + "Errors: " + engineStrict.ErrorReporter.FormatErrors());
        }
        finally { engineStrict.Dispose(); }

        // Non-strict — must also succeed (regression check).
        var (engineLax, okLax) = RunSource(
            "Sequence seq = | C4q D4q |\n"
            + "Sequence shifted = (transpose seq +2st)\n");
        try
        {
            Assert.True(okLax,
                "non-strict (transpose seq +2st) should also succeed. Errors: "
                + engineLax.ErrorReporter.FormatErrors());
        }
        finally { engineLax.Dispose(); }
    }

    /// <summary>
    /// Composer escape hatch (Decibel): <c>(gain buf -12dB)</c> hits the
    /// exact-match <c>gain(Buffer, Decibel)</c> overload (+1000). Must
    /// succeed in BOTH modes. This is the "use the music-type literal"
    /// migration target for strict-mode composers.
    /// </summary>
    [Fact]
    public void Fact_StrictAcceptsExactDecibel_GainBufNegTwelveDb_Succeeds()
    {
        var (engineStrict, okStrict) = RunSource(
            "enable strict;\n"
            + "Buffer src = (createSineTone 0.5 440.0 0.5)\n"
            + "Buffer wet = (gain src -12dB)\n");
        try
        {
            Assert.True(okStrict,
                "strict (gain src -12dB) should succeed (exact-match Decibel tier +1000). "
                + "Errors: " + engineStrict.ErrorReporter.FormatErrors());
        }
        finally { engineStrict.Dispose(); }

        var (engineLax, okLax) = RunSource(
            "Buffer src = (createSineTone 0.5 440.0 0.5)\n"
            + "Buffer wet = (gain src -12dB)\n");
        try
        {
            Assert.True(okLax,
                "non-strict (gain src -12dB) should also succeed. Errors: "
                + engineLax.ErrorReporter.FormatErrors());
        }
        finally { engineLax.Dispose(); }
    }

    /// <summary>
    /// Back-compat regression: every Pitfall-1 example call MUST still resolve
    /// successfully in NON-strict mode. Defaulted-false <c>strictMode</c>
    /// parameter preserves the legacy resolver behavior at every call site.
    /// </summary>
    [Fact]
    public void Fact_NonStrictAllAcceptedAsBefore()
    {
        // (add 1 2.5) — numeric widening Int → Double, clause (a).
        var (e1, ok1) = RunSource("Double r = (add 1 2.5)\n");
        try
        {
            Assert.True(ok1,
                "non-strict (add 1 2.5) should succeed via Int.CanConvertTo(Double). Errors: "
                + e1.ErrorReporter.FormatErrors());
        }
        finally { e1.Dispose(); }

        // (transpose seq 2) — inverse music-type widening, clause (b).
        var (e2, ok2) = RunSource(
            "Sequence seq = | C4q D4q |\n"
            + "Sequence shifted = (transpose seq 2)\n");
        try
        {
            Assert.True(ok2,
                "non-strict (transpose seq 2) should succeed via Semitone.IsCompatibleWith(Int). Errors: "
                + e2.ErrorReporter.FormatErrors());
        }
        finally { e2.Dispose(); }

        // (reverb buf 0.5 1.5) — inverse music-type widening on 3rd arg.
        var (e3, ok3) = RunSource(
            "Buffer src = (createSineTone 0.5 440.0 0.5)\n"
            + "Buffer wet = (reverb src 0.5 1.5)\n");
        try
        {
            Assert.True(ok3,
                "non-strict (reverb src 0.5 1.5) should succeed via Second.IsCompatibleWith(Double). Errors: "
                + e3.ErrorReporter.FormatErrors());
        }
        finally { e3.Dispose(); }
    }

    // =====================================================================
    // Direct OverloadResolver unit-test Facts (bypass the engine)
    // =====================================================================

    /// <summary>
    /// Pin Pitfall 1 at the lowest level: build a minimal candidate list
    /// + arg-type list and invoke <c>OverloadResolver.Resolve</c> directly
    /// with <c>strictMode: false</c> (default), then again with
    /// <c>strictMode: true</c>. The strict call returns null
    /// (no-match); non-strict returns the inverse-widened candidate.
    /// </summary>
    [Fact]
    public void Fact_OverloadResolverDirect_StrictDropsInverseDirectionMatch()
    {
        var reporter = new ErrorReporter();
        var resolver = new OverloadResolver(reporter);

        // Single candidate: foo(Semitone). Caller passes Int. Non-strict
        // accepts via Semitone.IsCompatibleWith(Int)=true (clause b).
        var sig = new FunctionSignature(
            Name: "foo",
            InputTypes: new FlowType[] { SemitoneType.Instance });
        var candidates = new[] { sig };
        var argTypes = new FlowType[] { IntType.Instance };

        // Non-strict — defaulted false — accepts.
        var nonStrictReporter = new ErrorReporter();
        var nonStrictResolver = new OverloadResolver(nonStrictReporter);
        var nonStrictResult = nonStrictResolver.Resolve("foo", candidates, argTypes);
        Assert.NotNull(nonStrictResult);
        Assert.False(nonStrictReporter.HasErrors);

        // Strict — drops clause (b) — no candidate matches.
        var strictReporter = new ErrorReporter();
        var strictResolver = new OverloadResolver(strictReporter);
        var strictResult = strictResolver.Resolve(
            "foo", candidates, argTypes, strictMode: true);
        Assert.Null(strictResult);
        Assert.True(strictReporter.HasErrors);
        Assert.Contains(
            "No matching overload for function 'foo'",
            strictReporter.FormatErrors());
    }

    /// <summary>
    /// Pitfall 1 clause (a) at the resolver level: foo(Double) with Int arg.
    /// Non-strict accepts via Int.CanConvertTo(Double); strict drops it.
    /// </summary>
    [Fact]
    public void Fact_OverloadResolverDirect_StrictDropsNumericWidening()
    {
        var sig = new FunctionSignature(
            Name: "foo",
            InputTypes: new FlowType[] { DoubleType.Instance });
        var candidates = new[] { sig };
        var argTypes = new FlowType[] { IntType.Instance };

        var nonStrictReporter = new ErrorReporter();
        var nonStrictResolver = new OverloadResolver(nonStrictReporter);
        var nonStrictResult = nonStrictResolver.Resolve("foo", candidates, argTypes);
        Assert.NotNull(nonStrictResult);
        Assert.False(nonStrictReporter.HasErrors);

        var strictReporter = new ErrorReporter();
        var strictResolver = new OverloadResolver(strictReporter);
        var strictResult = strictResolver.Resolve(
            "foo", candidates, argTypes, strictMode: true);
        Assert.Null(strictResult);
        Assert.True(strictReporter.HasErrors);
        Assert.Contains(
            "No matching overload for function 'foo'",
            strictReporter.FormatErrors());
    }

    /// <summary>
    /// Compatible (+500) tier MUST survive under strict — only exact (+1000)
    /// and compatible (+500) are accepted. Pin via a foo(Number) candidate
    /// called with an Int arg: <c>Int.IsCompatibleWith(Number)</c>? No —
    /// Int has no IsCompatibleWith override (base returns Equals). But
    /// <c>Int.CanConvertTo(Number)</c> is true (clause a). So Number is the
    /// wrong example for compat-tier preservation.
    /// <para>
    /// Use Decibel arg vs Double param instead: <c>Decibel.IsCompatibleWith(Double) = true</c>
    /// → clause 1 ("IsCompatibleWith" = +500 compatible tier) hits.
    /// Strict KEEPS this, only drops the inverse direction.
    /// </para>
    /// </summary>
    [Fact]
    public void Fact_StrictPreservesCompatibleTier_DecibelAcceptedAtDoubleParam()
    {
        // foo(Double) called with Decibel arg. Decibel.IsCompatibleWith(Double) = true
        // → +500 compatible tier on clause 1. Must accept in BOTH modes.
        var sig = new FunctionSignature(
            Name: "foo",
            InputTypes: new FlowType[] { DoubleType.Instance });
        var candidates = new[] { sig };
        var argTypes = new FlowType[] { DecibelType.Instance };

        var nonStrictReporter = new ErrorReporter();
        var nonStrictResolver = new OverloadResolver(nonStrictReporter);
        var nonStrictResult = nonStrictResolver.Resolve("foo", candidates, argTypes);
        Assert.NotNull(nonStrictResult);
        Assert.False(nonStrictReporter.HasErrors);

        var strictReporter = new ErrorReporter();
        var strictResolver = new OverloadResolver(strictReporter);
        var strictResult = strictResolver.Resolve(
            "foo", candidates, argTypes, strictMode: true);
        Assert.NotNull(strictResult);
        Assert.False(strictReporter.HasErrors,
            "compatible (+500) tier must survive under strict — Decibel.IsCompatibleWith(Double)=true. "
            + "Errors: " + strictReporter.FormatErrors());
    }

    /// <summary>
    /// Defaulted-false <c>strictMode</c> parameter preserves byte-identical
    /// behavior at EVERY existing call site that constructs an
    /// <c>OverloadResolver</c> directly. Sample 3 representative resolutions
    /// without passing <c>strictMode</c>; each must match the pre-Plan-44-03
    /// non-strict behavior verbatim.
    /// </summary>
    [Fact]
    public void Fact_StrictModeDefaultedFalseParameter_PreservesAllExistingCallers()
    {
        var reporter = new ErrorReporter();
        var resolver = new OverloadResolver(reporter);

        // Sample 1: exact match. foo(Int) called with Int.
        var sigInt = new FunctionSignature("sample1", new FlowType[] { IntType.Instance });
        var r1 = resolver.Resolve(
            "sample1", new[] { sigInt }, new FlowType[] { IntType.Instance });
        Assert.NotNull(r1);

        // Sample 2: numeric widening (clause a). foo(Double) called with Int.
        var sigDouble = new FunctionSignature("sample2", new FlowType[] { DoubleType.Instance });
        var r2 = resolver.Resolve(
            "sample2", new[] { sigDouble }, new FlowType[] { IntType.Instance });
        Assert.NotNull(r2);

        // Sample 3: inverse music-type widening (clause b). foo(Semitone) called with Int.
        var sigSemi = new FunctionSignature(
            "sample3", new FlowType[] { SemitoneType.Instance });
        var r3 = resolver.Resolve(
            "sample3", new[] { sigSemi }, new FlowType[] { IntType.Instance });
        Assert.NotNull(r3);

        Assert.False(reporter.HasErrors,
            "no errors expected when defaulted-false strictMode is used. "
            + "Errors: " + reporter.FormatErrors());
    }
}
