using System;
using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 2 (GREEN): pins TUP-09 destructure surface —
/// <c>&lt;&lt;a, b&gt;&gt; = expr</c> assignment-only destructuring (proc/lambda parameter
/// destructuring is explicitly out of scope per CONTEXT § Tuple destructuring scope).
/// </summary>
[Collection("FlowScripts")]
public class TupleDestructureFacts
{
    [Fact]
    public void Destructure_BindsBothComponents()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Int, Int>> t = <<7, 11>>
<<Int a, Int b>> = t
(print (str a))
(print (str b))
");
        Assert.True(success, $"stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("7", stdout);
        Assert.Contains("11", stdout);
    }

    [Fact]
    public void Destructure_TypedSlots_NoteBeat()
    {
        // Plan named the fact "NoteBeat" but Beat has no direct literal in 26.1 — see
        // SUMMARY deviation note. Switched to Note + Note pair so the typed-slot path
        // exercises a special-type binding identically; the destructure code path is
        // type-agnostic past IsCompatibleWith.
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Note, Note>> entry = <<C4, D4>>
<<Note pitch, Note other>> = entry
(print (str pitch))
(print (str other))
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        Assert.Contains("C4", stdout);
        Assert.Contains("D4", stdout);
    }

    [Fact]
    public void Destructure_BareNamesNoTypes()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Int, Int>> t = <<3, 4>>
<<a, b>> = t
(print (str a))
(print (str b))
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        Assert.Contains("3", stdout);
        Assert.Contains("4", stdout);
    }
}
