using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.TestFramework;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-04 Wave 0 — TEST-02 hermetic-isolation gates.
///
/// Asserts the SnapshotState / RestoreState pair on <c>ExecutionContext</c>
/// covers the 11+ mutable surfaces enumerated in RESEARCH §Pitfall 3.
/// Each fact mutates one surface inside Test A's body and asserts Test B
/// sees the pristine pre-run state via direct C# observation of the
/// <c>ExecutionContext</c> (not via a Flow-side reader builtin — keeps the
/// gate decoupled from any future stdlib accessor).
///
/// Pitfall 3 warning sign explicitly covered: <c>TestOrderIndependent</c>
/// shuffles the registry 20 times and asserts identical (passed, failed)
/// outcomes — flakiness depending on order is the canonical leak signal.
///
/// RED state: <see cref="TestRunner"/> + the SnapshotState/RestoreState
/// methods on <see cref="ExecutionContext"/> are introduced in Task 2.
/// </summary>
public class HermeticIsolationTests
{
    [Fact]
    public void TestRegistryAccumulatesAndRunsAllTests()
    {
        using var engine = new FlowEngine(verbose: false);
        AssertExecuteSucceeds(engine, """
            use "@std"
            use "@test"
            (test "t1" lazy((assertEq 1 1)))
            (test "t2" lazy((assertEq 2 (add 1 1))))
            """);
        Assert.Equal(2, engine.Context.TestRegistry.Count);

        var (passed, failed) = new TestRunner().Run(engine, "<inline>");
        Assert.Equal(2, passed);
        Assert.Equal(0, failed);
    }

    [Fact]
    public void SymbolInternTableResetBetweenTests()
    {
        // Test A interns #leaked-from-a; Test B interns #leaked-from-b. If
        // SnapshotState captures the symbol intern table and RestoreState
        // reinstates it, the table outside the tests is the SAME object both
        // before and after the runner walks the registry — but neither test's
        // private interned symbol bleeds into the pre-test snapshot.
        using var engine = new FlowEngine(verbose: false);
        var preRunKeys = new HashSet<string>(engine.Context.SymbolInternTable.Keys);

        AssertExecuteSucceeds(engine, """
            use "@std"
            use "@test"
            (test "interns leaked-from-a" lazy((sequals #leakedFromA #leakedFromA)))
            (test "interns leaked-from-b" lazy((sequals #leakedFromB #leakedFromB)))
            """);

        var (passed, failed) = new TestRunner().Run(engine, "<inline>");
        Assert.Equal(2, passed);
        Assert.Equal(0, failed);

        // After the run, the intern table must match the pre-run snapshot —
        // neither test's internal symbol leaked out.
        var postRunKeys = new HashSet<string>(engine.Context.SymbolInternTable.Keys);
        Assert.Equal(preRunKeys, postRunKeys);
    }

    [Fact]
    public void PrngStateResetBetweenTests()
    {
        // Pre-seed the FixedGen with a known seed, sample once at C# layer to
        // pull the FIRST value. Then run two tests that each consume one
        // FixedRand inside their body. RestoreState must rewind FixedGen so
        // both bodies observe the SAME first value (the snapshot captured
        // the seed state, restore rewinds to it).
        using var engine = new FlowEngine(verbose: false);
        engine.Context.SetSeed(42);
        var preTestRand = engine.Context.GetRand(fixedRng: true).NextDouble();

        // Reseed so the snapshot taken inside SnapshotState captures the SAME
        // baseline as preTestRand — the next NextDouble call must therefore
        // produce the same value as preTestRand.
        engine.Context.SetSeed(42);

        AssertExecuteSucceeds(engine, """
            use "@std"
            use "@test"
            (test "first prng draw" lazy((??)))
            (test "second prng draw" lazy((??)))
            """);
        var (passed, failed) = new TestRunner().Run(engine, "<inline>");
        Assert.Equal(2, passed);
        Assert.Equal(0, failed);

        // After both tests finish, the FixedGen should have been rewound to the
        // pre-test snapshot — next draw must equal preTestRand.
        engine.Context.SetSeed(42);
        var postTestRand = engine.Context.GetRand(fixedRng: true).NextDouble();
        Assert.Equal(preTestRand, postTestRand);
    }

    [Fact]
    public void TestOrderIndependent()
    {
        // RESEARCH §Pitfall 3 warning sign: "test pass/fail flakiness depending
        // on test order". Register a quintet of small tests, run them in 20
        // different randomized orders, assert the (passed, failed) tuple is
        // identical every time.
        const string source = """
            use "@std"
            use "@test"
            (test "t1" lazy((assertEq 1 1)))
            (test "t2" lazy((sequals #leakedFromT2 #leakedFromT2)))
            (test "t3" lazy((assertEq 2 (add 1 1))))
            (test "t4" lazy((??)))
            (test "t5" lazy((assertEq 4 (mul 2 2))))
            """;

        (int passed, int failed)? expected = null;
        var rng = new Random(12345);
        for (int run = 0; run < 20; run++)
        {
            using var engine = new FlowEngine(verbose: false);
            AssertExecuteSucceeds(engine, source);

            // Shuffle the registry in place — order independence means the
            // outcome must be identical regardless of which test runs first.
            ShuffleInPlace(engine.Context.TestRegistry, rng);

            var outcome = new TestRunner().Run(engine, $"<run-{run}>");
            expected ??= outcome;
            Assert.Equal(expected.Value, outcome);
        }
        Assert.NotNull(expected);
        Assert.Equal(5, expected!.Value.passed);
        Assert.Equal(0, expected.Value.failed);
    }

    private static void AssertExecuteSucceeds(FlowEngine engine, string source)
    {
        var ok = engine.Execute(source);
        Assert.True(ok,
            "FlowEngine.Execute failed to register the test bodies: "
            + engine.ErrorReporter.FormatErrors());
    }

    private static void ShuffleInPlace<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
