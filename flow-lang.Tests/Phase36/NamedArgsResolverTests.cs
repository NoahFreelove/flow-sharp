using System.Collections.Generic;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-02 Task 3 — <see cref="OverloadResolver"/> named-arg
/// dispatch facts.
///
/// Drives <see cref="OverloadResolver.Resolve"/> directly with synthetic
/// candidate lists so the named-arg → positional-slot remapping is
/// verifiable without dragging the whole InternalFunctionRegistry / Parser
/// / Interpreter into the assertion. The end-to-end composer surface lives
/// in <c>tests/test_named_args.flow</c>.
///
/// <para>
/// Five facts pin the resolver contract:
/// </para>
/// <list type="bullet">
///   <item>NamedArgBindsToCorrectSlot — happy path; named args reorder.</item>
///   <item>NamedArgUnknownNameRaises — unknown parameter is a clear diag.</item>
///   <item>NamedArgDuplicatePositionalRaises — positional + named target
///         the same slot is a clear diag.</item>
///   <item>NamedArgWithVarargsRejected — Pitfall: varargs ambiguity (RESEARCH
///         Open Question 2) — explicit rejection.</item>
///   <item>SignatureWithoutParameterNamesFallsBackToPositionalOnly — the
///         backfill safety net (RESEARCH Pitfall 5): null ParameterNames +
///         named call = graceful advisory, not a crash.</item>
/// </list>
/// </summary>
public class NamedArgsResolverTests
{
    private static OverloadResolver MakeResolver(out ErrorReporter reporter)
    {
        reporter = new ErrorReporter();
        return new OverloadResolver(reporter);
    }

    [Fact]
    public void NamedArgBindsToCorrectSlot()
    {
        // Registry shape: transpose(Sequence, Semitone) with ParameterNames.
        // Composer call: `(transpose foo amount=2)` — positional Sequence
        // arg already in slot 0, named `amount` goes to slot 1.
        var sig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance },
            ParameterNames: new[] { "seq", "amount" });

        var resolver = MakeResolver(out var reporter);
        var match = resolver.Resolve(
            "transpose",
            new[] { sig },
            positionalArgTypes: new[] { (FlowType)SequenceType.Instance },
            namedArgTypes: new Dictionary<string, FlowType>
            {
                ["amount"] = SemitoneType.Instance,
            });

        Assert.NotNull(match);
        Assert.Same(sig, match);
        Assert.False(reporter.HasErrors, reporter.FormatErrors());
    }

    [Fact]
    public void NamedArgUnknownNameRaises()
    {
        // `(transpose foo invalid=2)` — 'invalid' is not in ParameterNames.
        var sig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance },
            ParameterNames: new[] { "seq", "amount" });

        var resolver = MakeResolver(out var reporter);
        var match = resolver.Resolve(
            "transpose",
            new[] { sig },
            positionalArgTypes: new[] { (FlowType)SequenceType.Instance },
            namedArgTypes: new Dictionary<string, FlowType>
            {
                ["invalid"] = SemitoneType.Instance,
            });

        Assert.Null(match);
        Assert.True(reporter.HasErrors);
        Assert.Contains("unknown parameter 'invalid'", reporter.FormatErrors());
        Assert.Contains("transpose", reporter.FormatErrors());
        // Expected-param hint — composer needs to know what's valid.
        Assert.Contains("seq", reporter.FormatErrors());
        Assert.Contains("amount", reporter.FormatErrors());
    }

    [Fact]
    public void NamedArgDuplicatePositionalRaises()
    {
        // `(transpose foo seq=bar)` — positional slot 0 + named 'seq' both
        // target slot 0. Clear diagnostic.
        var sig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance },
            ParameterNames: new[] { "seq", "amount" });

        var resolver = MakeResolver(out var reporter);
        var match = resolver.Resolve(
            "transpose",
            new[] { sig },
            positionalArgTypes: new[] { (FlowType)SequenceType.Instance },
            namedArgTypes: new Dictionary<string, FlowType>
            {
                ["seq"] = SequenceType.Instance,
            });

        Assert.Null(match);
        Assert.True(reporter.HasErrors);
        Assert.Contains("'seq'", reporter.FormatErrors());
        Assert.Contains("positional", reporter.FormatErrors());
    }

    [Fact]
    public void NamedArgWithVarargsRejected()
    {
        // RESEARCH Open Question 2: a varargs signature called with named
        // args is rejected outright — composer would need to use positional
        // form. Mirrors Python's `def f(*args)` rejecting `f(arg=1)`.
        var varargSig = new FunctionSignature(
            "dict",
            new List<FlowType> { VoidType.Instance, VoidType.Instance },
            IsVarArgs: true,
            ParameterNames: new[] { "K", "V" });

        var resolver = MakeResolver(out var reporter);
        var match = resolver.Resolve(
            "dict",
            new[] { varargSig },
            positionalArgTypes: new[] { (FlowType)StringType.Instance },
            namedArgTypes: new Dictionary<string, FlowType>
            {
                ["key"] = StringType.Instance,
            });

        Assert.Null(match);
        Assert.True(reporter.HasErrors);
        Assert.Contains("named arg", reporter.FormatErrors());
        Assert.Contains("'key'", reporter.FormatErrors());
        Assert.Contains("variadic", reporter.FormatErrors());
        Assert.Contains("'dict'", reporter.FormatErrors());
    }

    [Fact]
    public void SignatureWithoutParameterNamesFallsBackToPositionalOnly()
    {
        // Backfill safety net (RESEARCH Pitfall 5): a pre-Phase-36 signature
        // (ParameterNames=null — the not-yet-backfilled tail in Plans 36-03/04)
        // called with named args raises a clear "does not yet support" diag
        // rather than silently misbehaving. Plans 36-03/04 backfill the field
        // across ~350 sites; until then, this is the contract.
        var legacySig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance });
        Assert.Null(legacySig.ParameterNames); // sanity

        var resolver = MakeResolver(out var reporter);
        var match = resolver.Resolve(
            "transpose",
            new[] { legacySig },
            positionalArgTypes: new[] { (FlowType)SequenceType.Instance },
            namedArgTypes: new Dictionary<string, FlowType>
            {
                ["amount"] = SemitoneType.Instance,
            });

        Assert.Null(match);
        Assert.True(reporter.HasErrors);
        Assert.Contains("does not yet support named arguments", reporter.FormatErrors());
        Assert.Contains("'transpose'", reporter.FormatErrors());
    }
}
