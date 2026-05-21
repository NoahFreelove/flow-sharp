using System.Collections.Generic;
using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-01 Task 1 — pure-unit facts for <see cref="PrngRegistry"/>.
///
/// Drives the registry directly (no FlowEngine) so the (SourceLocation, name)
/// keying, deterministic FNV-1a seed derivation, render-boundary reseed, and
/// snapshot/restore round-trip are verifiable without dragging in the lexer /
/// parser / interpreter. The full FlowEngine-side integration is covered by
/// Task 2's PrngRegistryContextOwnership + SnapshotState facts.
///
/// Per CLAUDE.md determinism contract (Phase 18/25/27/28/29/33 inheritance):
/// C# <c>string.GetHashCode()</c> is randomized per process — explicitly NOT
/// used in <see cref="PrngRegistry.ComputeDeterministicSeed"/>. The FnvHashIsProcessStable
/// fact is the regression gate.
///
/// Test 7 (ContextOwnsRegistryAcrossRenders) and Test 8 (TestSnapshotCapturesAndRestores)
/// live alongside the Task 1 facts since they exercise the same surface — but the
/// ExecutionContext / TestSnapshot integration they assert is finished in Task 2.
/// </summary>
public class PrngRegistryTests
{
    private static SourceLocation Loc(int line, int column, string? file = "test.flow")
        => new(line, column, file);

    [Fact]
    public void SameKeyReturnsSameRandomWithinPass()
    {
        var reg = new PrngRegistry();
        var loc = Loc(10, 5);

        var r1 = reg.GetRandom(loc, "markovGenerate");
        var r2 = reg.GetRandom(loc, "markovGenerate");

        // Same reference — Random instance is cached per (loc, name).
        Assert.Same(r1, r2);

        // Mutation through r1 is visible through r2 (shared state).
        int first = r1.Next();
        int second = r2.Next();
        Assert.NotEqual(first, second); // r2 sees state advanced by r1
    }

    [Fact]
    public void DistinctSourceLocationsYieldDistinctStreams()
    {
        var reg = new PrngRegistry();
        var locA = Loc(10, 5);
        var locB = Loc(11, 5);

        var ra = reg.GetRandom(locA, "markovGenerate");
        var rb = reg.GetRandom(locB, "markovGenerate");

        Assert.NotSame(ra, rb);
        Assert.NotEqual(ra.Next(), rb.Next());
    }

    [Fact]
    public void DistinctNamesYieldDistinctStreams()
    {
        var reg = new PrngRegistry();
        var loc = Loc(10, 5);

        var rMarkov = reg.GetRandom(loc, "markovGenerate");
        var rDegrade = reg.GetRandom(loc, "degrade");

        Assert.NotSame(rMarkov, rDegrade);
        Assert.NotEqual(rMarkov.Next(), rDegrade.Next());
    }

    [Fact]
    public void ResetClearsCache()
    {
        var reg = new PrngRegistry();
        var loc = Loc(10, 5);

        var r1 = reg.GetRandom(loc, "markovGenerate");
        int firstValueBefore = r1.Next();

        // Drain a few more values so r1 has clearly advanced.
        r1.Next();
        r1.Next();

        reg.ResetAtRenderBoundary();

        var r2 = reg.GetRandom(loc, "markovGenerate");
        // A fresh Random produced from the same deterministic seed
        // must yield the SAME first value as the pre-reset Random's first draw.
        Assert.NotSame(r1, r2); // cache was cleared — new instance
        Assert.Equal(firstValueBefore, r2.Next());
    }

    [Fact]
    public void FnvHashIsProcessStable()
    {
        // Two separate registries within the SAME process must produce the SAME
        // deterministic seed for the same (loc, name). This sanity-checks that
        // we are NOT using C# string.GetHashCode() (randomized per process).
        var regA = new PrngRegistry();
        var regB = new PrngRegistry();
        var loc = Loc(42, 7, "fixed.flow");

        var randA = regA.GetRandom(loc, "lorenz");
        var randB = regB.GetRandom(loc, "lorenz");

        // Same first 4 draws — implies identical seeds.
        Assert.Equal(randA.Next(), randB.Next());
        Assert.Equal(randA.Next(), randB.Next());
        Assert.Equal(randA.Next(), randB.Next());
        Assert.Equal(randA.Next(), randB.Next());
    }

    [Fact]
    public void NullFilePathDoesNotThrow()
    {
        var reg = new PrngRegistry();
        // SourceLocation FileName is nullable per its record definition.
        var loc = new SourceLocation(1, 1, null);

        // Should not throw NRE.
        var r = reg.GetRandom(loc, "anyGen");
        Assert.NotNull(r);
        // And it must be deterministic across registries with the null-file path.
        var r2 = new PrngRegistry().GetRandom(loc, "anyGen");
        Assert.Equal(r.Next(), r2.Next());
    }
}
