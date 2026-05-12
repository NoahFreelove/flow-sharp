using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 4 (GREEN): pins DICT-02 constructor surface — both
/// <c>(dict K V K V ...)</c> flat interleaved and <c>(dictTuple &lt;&lt;K,V&gt;&gt; ...)</c> tuple-pair
/// forms ship in 26.1 per CONTEXT § Final dict op surface.
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class DictConstructFacts
{
    [Fact]
    public void Flat_DictKVKV()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> v = (dict #kick 90 #snare 70)
(print (str (size v)))
(print (str (get v #kick)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("2", stdout);
        Assert.Contains("90", stdout);
    }

    [Fact]
    public void TuplePair_DictTuple()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> v = (dictTuple <<#kick, 90>> <<#snare, 70>>)
(print (str (size v)))
(print (str (get v #snare)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("2", stdout);
        Assert.Contains("70", stdout);
    }
}
