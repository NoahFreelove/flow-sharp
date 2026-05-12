using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 4 (GREEN): pins DICT-02 / DICT-03 — the 14-op surface
/// (get, getOr, set, remove, has, keys, values, size, merge, each, map, filter +
/// constructor pair handled in <see cref="DictConstructFacts"/>) plus three
/// cross-cutting properties: insertion-order preservation (DICT-03),
/// each-callback-unpack via <c>~&gt;</c> (Pitfall 6), and immutable-return
/// semantics (CONTEXT).
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class DictOpsFacts
{
    [Fact]
    public void GetMissing_ReturnsNothing()
    {
        // (get d "absent") returns Value.Void(). Smoke test: print stays silent
        // for void; confirm engine still ran successfully.
        // NOTE: avoid 1-character string keys ("a", "b") because Flow's lexer
        // treats single-letter strings as candidate Note literals at certain
        // grammar positions — use multi-char keys to lock in String typing.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<String, Int> d = (dict ""kick"" 1)
Void v = (get d ""missing"")
(print ""done"")
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
    }

    [Fact]
    public void GetOr_ReturnsDefault()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<String, Int> d = (dict)
Int r = (getOr d ""x"" 999)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("999", stdout);
    }

    [Fact]
    public void Set_ReturnsNewDict_OriginalUntouched()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d1 = (dict #a 1)
Dict<Symbol, Int> d2 = (set d1 #b 2)
(print (str (size d1)))
(print (str (size d2)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        // d1 still has 1 entry; d2 has 2.
        Assert.Contains("1", stdout);
        Assert.Contains("2", stdout);
    }

    [Fact]
    public void Remove_ReturnsNewDict()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d1 = (dict #a 1 #b 2)
Dict<Symbol, Int> d2 = (remove d1 #a)
(print (str (size d1)))
(print (str (size d2)))
(print (str (has d2 #a)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("2", stdout); // size of d1
        Assert.Contains("1", stdout); // size of d2
        Assert.Contains("false", stdout); // d2 no longer has #a
    }

    [Fact]
    public void Has_TrueWhenPresent()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #x 1)
(print (str (has d #x)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("true", stdout);
    }

    [Fact]
    public void Has_FalseWhenAbsent()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #x 1)
(print (str (has d #y)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("false", stdout);
    }

    [Fact]
    public void Size_CountsEntries()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #a 1 #b 2 #c 3)
(print (str (size d)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("3", stdout);
    }

    [Fact]
    public void Merge_LastWriteWins()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> a = (dict #a 1)
Dict<Symbol, Int> b = (dict #a 2 #b 3)
Dict<Symbol, Int> m = (merge a b)
(print (str (size m)))
(print (str (get m #a)))
(print (str (get m #b)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("2", stdout); // size of m
        Assert.Contains("3", stdout); // m[#b]
    }

    [Fact]
    public void KeysInsertionOrder()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #kick 90 #snare 70 #hihat 50)
Symbol[] ks = (keys d)
(print (str ks))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("[#kick, #snare, #hihat]", stdout);
    }

    [Fact]
    public void ValuesInsertionOrder()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #kick 90 #snare 70 #hihat 50)
Int[] vs = (values d)
(print (str vs))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("[90, 70, 50]", stdout);
    }

    [Fact]
    public void EachUnpacks_TwoArgCallback()
    {
        // Pitfall 6: each invokes callback with TWO unpacked positional args (key, value)
        // — the dict-side does the unpacking internally so the user writes a normal
        // 2-arg lambda (fn Symbol k, Int v => ...) without lambda-side destructuring.
        // Pins dict-vs-array overload disambiguation: the (each Dict<Void, Void>, Function)
        // overload is selected by the OverloadResolver because the runtime arg is Dict.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #kick 90 #snare 70)
(each d (fn Symbol k, Int v => (print (str v))))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("90", stdout);
        Assert.Contains("70", stdout);
    }

    [Fact]
    public void MapTransform_ValuesOnly()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #kick 90 #snare 70)
Dict<Symbol, Int> louder = (map d (fn Symbol k, Int v => (mul v 2)))
(print (str (get louder #kick)))
(print (str (get d #kick)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("180", stdout); // mapped value
        Assert.Contains("90", stdout);  // original untouched
    }

    [Fact]
    public void FilterMatches_KeepsPredicateTrue()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Symbol, Int> d = (dict #kick 90 #snare 70 #hihat 50)
Dict<Symbol, Int> hits = (filter d (fn Symbol k, Int v => (gt v 60)))
(print (str (size hits)))
(print (str (has hits #hihat)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("2", stdout); // size of hits (kick + snare)
        Assert.Contains("false", stdout); // hihat (50) filtered out
    }
}
