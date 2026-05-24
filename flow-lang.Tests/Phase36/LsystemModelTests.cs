using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-07 Task 1 — identity facts for the L-system model surface.
///
/// Pins the reference-identity contract per Pitfall 6: <see cref="LsystemModelData"/>
/// is NOT a record (no implicit structural equality); the dedicated
/// <see cref="LsystemModelData.StructurallyEquals"/> method is the only
/// structural compare path. <see cref="LsystemModelType"/> is a singleton at
/// specificity 149 slotted between Plan 36-06's <see cref="MarkovModelType"/>
/// (148) and Phase 33's <see cref="SfzType"/> (150).
///
/// Task 2 behavior facts (Lindenmayer algae canonical, iterations clamp,
/// terminal passthrough, lsystemToSequence mapper invocation,
/// one-shot ≡ model+generate, lsystemEqual structural compare) live in
/// <c>LsystemDeterminismTests</c> + this file's later facts.
/// </summary>
public class LsystemModelTests
{
    /// <summary>
    /// Builds a fresh sample model. We construct Symbol Values directly (NOT via
    /// <see cref="Value.Symbol(string,ExecutionContext)"/>, which interns per
    /// context) so each call produces a fresh axiom Value — exercising the
    /// reference-identity contract. <see cref="LsystemModelData.StructurallyEquals"/>
    /// uses underlying-data equality for cross-instance Symbol compares.
    /// </summary>
    private static LsystemModelData BuildSampleModel()
    {
        var symA = new Value("A", SymbolType.Instance);
        var symB = new Value("B", SymbolType.Instance);

        var rules = new Dictionary<Value, IReadOnlyList<Value>>
        {
            [symA] = new[] { symA, symB },
            [symB] = new[] { symA },
        };
        return new LsystemModelData(axiom: symA, rules: rules, iterations: 4);
    }

    [Fact]
    public void LsystemModelTypeSingletonExists()
    {
        Assert.NotNull(LsystemModelType.Instance);
        Assert.Equal("LsystemModel", LsystemModelType.Instance.Name);
        Assert.Equal(149, LsystemModelType.Instance.GetSpecificity());
    }

    [Fact]
    public void ValueLsystemModelFactoryWraps()
    {
        var model = BuildSampleModel();
        var value = Value.LsystemModel(model);

        Assert.Same(LsystemModelType.Instance, value.Type);
        Assert.Same(model, value.Data);
    }

    [Fact]
    public void IsCompatibleWithEnforcesType()
    {
        Assert.True(LsystemModelType.Instance.IsCompatibleWith(LsystemModelType.Instance));
        Assert.False(LsystemModelType.Instance.IsCompatibleWith(SequenceType.Instance));
        Assert.False(LsystemModelType.Instance.IsCompatibleWith(MarkovModelType.Instance));
        Assert.False(LsystemModelType.Instance.IsCompatibleWith(SfzType.Instance));
        Assert.False(LsystemModelType.Instance.IsCompatibleWith(TuningType.Instance));
    }

    [Fact]
    public void DataClassIsNotRecord()
    {
        // Two LsystemModelData instances with structurally-identical content must
        // NOT be equal under default object equality — reference identity is the
        // contract (Pitfall 6 in 36-PATTERNS.md).
        var m1 = BuildSampleModel();
        var m2 = BuildSampleModel();

        Assert.False(object.ReferenceEquals(m1, m2));
        Assert.False(m1.Equals(m2));
        // No GetHashCode equality assertion — default object.GetHashCode is
        // address-derived; two heap-distinct instances are virtually guaranteed
        // to hash differently but it's not a STRICT contract. The reference-
        // equality + Equals checks above pin the actual contract.
    }

    [Fact]
    public void StructurallyEqualsCompares()
    {
        // Same two instances above ARE structurally equal via the dedicated method.
        var m1 = BuildSampleModel();
        var m2 = BuildSampleModel();

        Assert.True(m1.StructurallyEquals(m2));
        Assert.True(m2.StructurallyEquals(m1));
        Assert.True(m1.StructurallyEquals(m1));
        Assert.False(m1.StructurallyEquals(null));

        // A model with different iteration count is not structurally equal.
        var symA = new Value("A", SymbolType.Instance);
        var symB = new Value("B", SymbolType.Instance);
        var rules = new Dictionary<Value, IReadOnlyList<Value>>
        {
            [symA] = new[] { symA, symB },
            [symB] = new[] { symA },
        };
        var differentIterations = new LsystemModelData(symA, rules, iterations: 7);
        Assert.False(m1.StructurallyEquals(differentIterations));

        // A model with different axiom is not structurally equal.
        var differentAxiom = new LsystemModelData(symB, rules, iterations: 4);
        Assert.False(m1.StructurallyEquals(differentAxiom));

        // A model with different rule count is not structurally equal.
        var rulesShort = new Dictionary<Value, IReadOnlyList<Value>>
        {
            [symA] = new[] { symA, symB },
        };
        var differentRules = new LsystemModelData(symA, rulesShort, iterations: 4);
        Assert.False(m1.StructurallyEquals(differentRules));
    }

    // ====================================================================
    // Task 2 — behavior facts (Lindenmayer algae canonical + edge cases)
    // ====================================================================

    private const string Prelude = """
        use "@std"
        use "@generative"
        """;

    private static IReadOnlyList<Value> RunAndGetSymbolArray(string body, string varName)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<IReadOnlyList<Value>>();
    }

    [Fact]
    public void LindenmayerAlgaeCanonical()
    {
        // The canonical Lindenmayer algae growth example:
        //   #A → #A #B
        //   #B → #A
        // Iteration 0: [#A]
        // Iteration 1: [#A, #B]
        // Iteration 2: [#A, #B, #A]
        // Iteration 3: [#A, #B, #A, #A, #B]
        // Iteration 4: [#A, #B, #A, #A, #B, #A, #B, #A]  (Fibonacci-count 8)
        var result = RunAndGetSymbolArray("""
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            Symbol[] result = (lsystem #A rules 4)
            """, "result");

        Assert.Equal(8, result.Count);
        // Expected sequence: A, B, A, A, B, A, B, A
        string[] expected = { "A", "B", "A", "A", "B", "A", "B", "A" };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.IsType<SymbolType>(result[i].Type);
            Assert.Equal(expected[i], result[i].Data);
        }
    }

    [Fact]
    public void IterationsZeroReturnsAxiomOnly()
    {
        var result = RunAndGetSymbolArray("""
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            Symbol[] result = (lsystem #A rules 0)
            """, "result");

        Assert.Single(result);
        Assert.IsType<SymbolType>(result[0].Type);
        Assert.Equal("A", result[0].Data);
    }

    [Fact]
    public void IterationsClampedToMax()
    {
        // Composer asks for 100; should clamp to 20 + emit a one-shot stderr
        // advisory. We can't easily assert the advisory body here without
        // process-stderr capture; the clamping is the load-bearing behavior.
        // At iteration 20 the algae rules produce a 17,711-element Fibonacci
        // result (F(22) starting from F(2)=2 — but iteration counting starts
        // from 1, so the 20th iteration gives F(22) ≈ 17,711 symbols). The
        // exact count isn't load-bearing; we just verify the call completes
        // without OOM and produces a non-degenerate result.
        var result = RunAndGetSymbolArray("""
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            Symbol[] result = (lsystem #A rules 100)
            """, "result");

        // Iteration 20 of algae: produces Fibonacci-count(22) symbols. The
        // value is well above iteration-4's 8 — assert the clamp didn't
        // accidentally use iteration 100.
        Assert.True(result.Count > 100, $"Expected clamp to 20 producing many symbols; got {result.Count}");
        Assert.True(result.Count < 100_000, $"Expected clamp at 20 capping below 100k symbols; got {result.Count} (suggests clamp failed)");
    }

    [Fact]
    public void TerminalSymbolsPassThrough()
    {
        // A rule with NO entry for #X (a "terminal" symbol) leaves it unchanged
        // across all iterations — canonical Lindenmayer terminal semantics.
        // Setup: only #A has a rule; #X is terminal.
        //   #A → #A #X
        // Iteration 0: [#A]
        // Iteration 1: [#A, #X]
        // Iteration 2: [#A, #X, #X]
        // Iteration 3: [#A, #X, #X, #X]
        var result = RunAndGetSymbolArray("""
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #X))
            Symbol[] result = (lsystem #A rules 3)
            """, "result");

        Assert.Equal(4, result.Count);
        // [A, X, X, X]
        Assert.Equal("A", result[0].Data);
        Assert.Equal("X", result[1].Data);
        Assert.Equal("X", result[2].Data);
        Assert.Equal("X", result[3].Data);
    }

    [Fact]
    public void LsystemModelEqualsStructural()
    {
        // Two independently-built models on the same axiom + rules are
        // STRUCTURALLY equal but REFERENCE-DISTINCT.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + """

            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            LsystemModel m1 = (lsystemModel #A rules)
            LsystemModel m2 = (lsystemModel #A rules)
            Bool structural = (lsystemEqual m1 m2)
            """);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}");

        var structural = runner.GetVariable("structural");
        Assert.True(structural.As<bool>(),
            "(lsystemEqual m1 m2) should be true on independently-built identical models");

        // Reference identity: m1 and m2 wrap distinct LsystemModelData instances.
        var m1 = runner.GetVariable("m1");
        var m2 = runner.GetVariable("m2");
        Assert.False(object.ReferenceEquals(m1.Data, m2.Data));
    }

    [Fact]
    public void LsystemToSequenceMapperInvocation()
    {
        // The composer supplies a mapper(Symbol => MusicalNote); the builtin
        // walks the expanded symbol array, invoking the mapper once per symbol,
        // and builds a Sequence from the returned notes. Symbols that don't
        // map to a MusicalNote get a charitable advisory (the lambda in this
        // test always returns a valid MusicalNote so no advisory fires).
        // Use the @notation `createMusicalNote(Note pitch, NoteValue duration)`
        // builtin (notation.flow:6) so the test doesn't depend on a runtime
        // C# helper that may not exist.
        using var runner = new FlowEngineRunner();
        var source = """
            use "@std"
            use "@notation"
            use "@generative"
            Symbol[] expanded = (list #A #B #A)
            Sequence result = (lsystemToSequence expanded (fn Symbol s => (if (equals s #A) (createMusicalNote C4 4) (createMusicalNote E4 4))))
            """;
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");

        var seq = runner.GetVariable("result").As<SequenceData>();
        Assert.Single(seq.Bars);
        Assert.Equal(3, seq.Bars[0].MusicalNotes.Count);
        // Verify per-symbol mapping: #A → C4, #B → E4, #A → C4
        Assert.Equal('C', seq.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('E', seq.Bars[0].MusicalNotes[1].NoteName);
        Assert.Equal('C', seq.Bars[0].MusicalNotes[2].NoteName);
    }
}
