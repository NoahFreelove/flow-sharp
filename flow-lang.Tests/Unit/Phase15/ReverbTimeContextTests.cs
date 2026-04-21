using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Unit.Phase15;

/// <summary>
/// DX-07 grammar + runtime Facts for the <c>reverbTime &lt;seconds&gt; { ... }</c> musical-context block.
/// Covers CONTEXT decisions D-01 (range 0-30), D-02 (0.0 sentinel), D-03 (parse-time negative reject
/// + interpret-time silent clamp at 30), D-04 (independent axes), and ROADMAP criterion #4
/// (8th-field walk + early-break predicate update).
///
/// Probe pattern: register an internal <c>probeMusicalContext</c> proc directly on the engine's
/// global frame + internal registry BEFORE <c>Execute</c>. The script body calls this proc at the
/// innermost scope; the C# impl snapshots <c>engine.Context.GetMusicalContext()</c> into a
/// test-scoped list. Avoids modifying <c>std.flow</c> (which is under concurrent edit by Plan 15-04).
/// </summary>
public class ReverbTimeContextTests
{
    /// <summary>
    /// Runs a Flow source string with a pre-registered <c>probeMusicalContext</c> proc that
    /// captures the resolved <see cref="MusicalContext"/> at each call site. Returns the list of
    /// captured snapshots plus engine diagnostics.
    /// </summary>
    private static (List<MusicalContext> Probes, bool Success, int ErrorCount, string Stderr, string Stdout)
        RunWithProbe(string source)
    {
        var probes = new List<MusicalContext>();
        var stdoutSink = new StringWriter();
        var stderrSink = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        Console.SetOut(stdoutSink);
        Console.SetError(stderrSink);

        using var engine = new FlowEngine();
        try
        {
            // Register probe in the internal function registry.
            var probeSig = new FunctionSignature("probeMusicalContext", new List<FlowType>());
            engine.Context.InternalRegistry.Register(
                "probeMusicalContext",
                probeSig,
                _ =>
                {
                    probes.Add(engine.Context.GetMusicalContext().Clone());
                    return Value.Void();
                });
            // Declare the function on the global frame so script source can resolve it without
            // an std.flow declaration.
            var overload = FunctionOverload.Internal("probeMusicalContext", probeSig,
                _ =>
                {
                    probes.Add(engine.Context.GetMusicalContext().Clone());
                    return Value.Void();
                });
            engine.Context.GlobalFrame.DeclareFunction(overload);

            var success = engine.Execute(source, "<probe-test>");
            if (engine.ErrorReporter.Errors.Count > 0)
                stderrSink.WriteLine(engine.ErrorReporter.FormatErrors());
            return (probes, success, engine.ErrorReporter.Errors.Count, stderrSink.ToString(), stdoutSink.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    // ===== F-01: Parse_Positive_StoresInContext =====

    [Fact]
    public void Parse_Positive_StoresInContext()
    {
        const string source = @"
tempo 120 {
    reverbTime 2.5 {
        (probeMusicalContext)
    }
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        Assert.Equal(2.5, probes[0].ReverbTime);
    }

    // ===== F-03: Parse_Negative_ParseError =====

    [Fact]
    public void Parse_Negative_ParseError()
    {
        const string source = @"
tempo 120 {
    reverbTime -2.5 {
        (probeMusicalContext)
    }
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.False(success, "expected parse failure on negative reverbTime");
        Assert.True(errorCount >= 1, $"expected >= 1 error, got {errorCount}");
        Assert.Contains("reverbTime cannot be negative", stderr);
        // Body must NOT have executed (probe never fires).
        Assert.Empty(probes);
    }

    // ===== F-04: Parse_AboveMax_ClampsTo30 =====

    [Fact]
    public void Parse_AboveMax_ClampsTo30()
    {
        const string source = @"
reverbTime 45 {
    (probeMusicalContext)
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        Assert.Equal(30.0, probes[0].ReverbTime);
    }

    // ===== Parse_Zero_ProducesDry (supporting, honors D-02) =====

    [Fact]
    public void Parse_Zero_ProducesDry()
    {
        const string source = @"
reverbTime 0 {
    (probeMusicalContext)
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        // Sentinel: exactly 0.0, NOT null, NOT clamped-up.
        Assert.NotNull(probes[0].ReverbTime);
        Assert.Equal(0.0, probes[0].ReverbTime!.Value);
    }

    // ===== F-05: Nested_WithGain_Independent =====

    [Fact]
    public void Nested_WithGain_Independent()
    {
        const string source = @"
gain 0.5 {
    reverbTime 2.0 {
        (probeMusicalContext)
    }
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        Assert.Equal(0.5, probes[0].Gain);
        Assert.Equal(2.0, probes[0].ReverbTime);
    }

    // ===== F-23: Nested_InsideTempoAndKey_Resolves =====

    [Fact]
    public void Nested_InsideTempoAndKey_Resolves()
    {
        const string source = @"
tempo 120 {
    key Cmajor {
        reverbTime 3.0 {
            (probeMusicalContext)
        }
    }
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        Assert.Equal(3.0, probes[0].ReverbTime);
        Assert.Equal(120.0, probes[0].Tempo);
        Assert.Equal("Cmajor", probes[0].Key);
    }

    // ===== F-22: GetMusicalContext_AllFieldsResolvedSearchesReverbTime =====
    // Pitfall 1 regression. ReverbTime at outermost frame; all 7 other fields at inner frames.
    // A stale 7-clause early-break predicate would stop walking before reaching the outer frame
    // and return ReverbTime == null. The 8-clause predicate (Task 1c edit 2) must require
    // ReverbTime != null before breaking.

    [Fact]
    public void GetMusicalContext_AllFieldsResolvedSearchesReverbTime()
    {
        // Outermost reverbTime wraps a context that fills all 7 other musical-context fields:
        //   TimeSignature (timesig), Tempo, Swing, Key, Velocity (dynamics), Pan, Gain.
        // The probe runs at the innermost scope. With an 8-clause early-break predicate,
        // ReverbTime MUST be resolved from the outermost frame.
        const string source = @"
reverbTime 2.0 {
    tempo 120 {
        timesig 4/4 {
            swing 0.6 {
                key Cmajor {
                    dynamics mf {
                        pan 0.5 {
                            gain 0.8 {
                                (probeMusicalContext)
                            }
                        }
                    }
                }
            }
        }
    }
}
";
        var (probes, success, errorCount, stderr, _) = RunWithProbe(source);
        Assert.True(success, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Single(probes);
        // If the 7-clause early-break short-circuited the walk before reaching the outer frame,
        // ReverbTime would be null. This Fact pins the 8-clause predicate.
        Assert.NotNull(probes[0].ReverbTime);
        Assert.Equal(2.0, probes[0].ReverbTime!.Value);
        // Sanity: inner fields resolved too.
        Assert.Equal(120.0, probes[0].Tempo);
        Assert.Equal(0.6, probes[0].Swing);
        Assert.Equal("Cmajor", probes[0].Key);
        Assert.Equal(0.5, probes[0].Pan);
        Assert.Equal(0.8, probes[0].Gain);
    }
}
