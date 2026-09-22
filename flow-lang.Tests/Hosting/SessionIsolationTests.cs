using System.Collections.Concurrent;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Hosting;

/// <summary>
/// Phase 2 gate: concurrent independent engines produce isolated output and results.
/// Two programs that differ in tempo, strictness, seeded randomness, proc definitions,
/// advisories and rendered instruments run many times on parallel threads; every run
/// must reproduce its own serial baseline exactly (stdout, diagnostics, error count
/// and rendered samples), and nothing may leak to the process console.
/// </summary>
[Collection("FlowScripts")]
public class SessionIsolationTests
{
    private const string ProgramA = @"use ""@std""
use ""@audio""
proc label (Int: n)
    (concat ""A"" (str n))
end proc
(print (label 1))
(print (str (beatToSec 1b)))
tempo 90 {
    (print (str (beatToSec 1b)))
    section verse { Sequence piano = | C4 E4 G4 C5 | }
}
(??set 42)
(inspect | (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) |)
Song s = [verse]
Buffer b = (renderSong s ""piano"")
(print (str (getFrames b)))
";

    private const string ProgramB = @"enable strict;
use ""@std""
use ""@audio""
proc label (Int: n)
    (concat ""B"" (str (mul n 10)))
end proc
(print (label 1))
(print (str (beatToSec 1b)))
tempo 150 {
    (print (str (beatToSec 1b)))
    section verse { Sequence organ = | A3 C4 E4 | }
}
(??set 7)
(inspect | (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) (?? C4 E4 G4 B4) |)
(print (if 1 lazy (""x"") lazy (""y"")))
Song s = [verse]
Buffer b = (renderSong s ""organ"")
(print (str (getFrames b)))
";

    private sealed record Outcome(string Stdout, string Diagnostics, int Errors, float[] Samples);

    private static Outcome Run(string source, string name)
    {
        var stdout = new StringWriter();
        var diagnostics = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions { Output = stdout, Diagnostics = diagnostics });
        engine.Evaluate(source, name);
        var buffer = engine.Context.GlobalFrame.GetVariable("b").As<AudioBuffer>();
        return new Outcome(stdout.ToString(), diagnostics.ToString(), engine.ErrorReporter.ErrorCount, buffer.Data.ToArray());
    }

    [Fact]
    public void ConcurrentEnginesMatchTheirSerialBaselines()
    {
        var baselineA = Run(ProgramA, "a.flow");
        var baselineB = Run(ProgramB, "b.flow");

        // The two programs really differ in every dimension under test.
        Assert.Contains("A1", baselineA.Stdout);
        Assert.Contains("B10", baselineB.Stdout);
        Assert.Contains("0.6666666667s", baselineA.Stdout);   // tempo 90
        Assert.Contains("0.4s", baselineB.Stdout);            // tempo 150
        Assert.Equal(0, baselineA.Errors);
        Assert.Equal(1, baselineB.Errors);                    // strict (if 1 ...)
        Assert.NotEqual(baselineA.Samples.Length, baselineB.Samples.Length);

        var consoleOut = Console.Out;
        var consoleErr = Console.Error;
        var canaryOut = new StringWriter();
        var canaryErr = new StringWriter();
        Console.SetOut(canaryOut);
        Console.SetError(canaryErr);
        var results = new ConcurrentBag<(bool IsA, Outcome Outcome)>();
        try
        {
            Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 6 }, i =>
            {
                bool isA = i % 2 == 0;
                results.Add((isA, isA ? Run(ProgramA, "a.flow") : Run(ProgramB, "b.flow")));
            });
        }
        finally
        {
            Console.SetOut(consoleOut);
            Console.SetError(consoleErr);
        }

        Assert.Equal(12, results.Count);
        foreach (var (isA, outcome) in results)
        {
            var expected = isA ? baselineA : baselineB;
            Assert.Equal(expected.Stdout, outcome.Stdout);
            Assert.Equal(expected.Diagnostics, outcome.Diagnostics);
            Assert.Equal(expected.Errors, outcome.Errors);
            Assert.Equal(expected.Samples, outcome.Samples);
        }
        Assert.Equal("", canaryOut.ToString());
        Assert.Equal("", canaryErr.ToString());
    }

    [Fact]
    public void AdvisoriesAreOncePerEngineUnlessALogIsShared()
    {
        const string source = "use \"@std\"\nuse \"@audio\"\n(beatToSec 1b)\n(beatToSec 1b)";
        static int Count(string text) => text.Split("[beatToSec] no active tempo").Length - 1;

        var first = new StringWriter();
        var second = new StringWriter();
        using (var e1 = new FlowEngine(new EngineOptions { Diagnostics = first }))
        {
            e1.Execute(source);
            e1.Execute(source);
        }
        using (var e2 = new FlowEngine(new EngineOptions { Diagnostics = second }))
            e2.Execute(source);
        Assert.Equal(1, Count(first.ToString()));
        Assert.Equal(1, Count(second.ToString()));

        var shared = new AdvisoryLog();
        var sharedOut = new StringWriter();
        for (int i = 0; i < 3; i++)
        {
            using var engine = new FlowEngine(new EngineOptions { Diagnostics = sharedOut, Advisories = shared });
            engine.Execute(source);
        }
        Assert.Equal(1, Count(sharedOut.ToString()));
    }

    [Fact]
    public void ConfigurationIsASnapshotPerEngine()
    {
        const string source = "use \"@std\"\nuse \"@audio\"\n(print (str (beatToSec 1b)))";
        var slow = new StringWriter();
        var fast = new StringWriter();
        using (var e = new FlowEngine(new EngineOptions { Output = slow, Diagnostics = TextWriter.Null, Config = FlowConfigPoco.Defaults with { DefaultTempo = 60 } }))
            e.Execute(source);
        using (var e = new FlowEngine(new EngineOptions { Output = fast, Diagnostics = TextWriter.Null, Config = FlowConfigPoco.Defaults with { DefaultTempo = 240 } }))
            e.Execute(source);
        Assert.Equal("1s", slow.ToString().Trim());
        Assert.Equal("0.25s", fast.ToString().Trim());
    }

    [Fact]
    public void NoEngineStateIsReachableOutsideItsEntryPoints()
    {
        using (var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null }))
        {
            engine.Execute("use \"@std\"\n(print \"hi\")");
            Assert.Null(SessionServices.Current);
            Assert.Null(RenderServices.Current);
            using (engine.EnterScope())
                Assert.Same(engine.RenderServices, RenderServices.Current);
            Assert.Null(RenderServices.Current);
        }
        Assert.DoesNotContain(typeof(FlowEngine).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public),
            p => p.Name.StartsWith("Current", StringComparison.Ordinal));
    }
}
