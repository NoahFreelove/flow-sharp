using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Generative;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-06 Task 1 — identity facts for the Markov model surface.
///
/// Pins the reference-identity contract per Pitfall 6: <see cref="MarkovModelData"/>
/// is NOT a record (no implicit structural equality); the dedicated
/// <see cref="MarkovModelData.StructurallyEquals"/> method is the only structural
/// compare path. <see cref="MarkovModelType"/> is a singleton at specificity 148
/// slotted between <see cref="HertzType"/> (144) and <see cref="SfzType"/> (150).
/// </summary>
public class MarkovModelTests
{
    private static MarkovModelData BuildSampleModel()
    {
        var dict = new Dictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>>(
            MarkovModelData.PrefixComparer.Instance)
        {
            [ImmutableArray.Create(60, 62)] = new[] { (64, 1.0), (65, 2.0) },
            [ImmutableArray.Create(62, 64)] = new[] { (65, 1.0) },
        };
        return new MarkovModelData(
            order: 2,
            transitions: dict,
            stateAlphabet: new[] { 60, 62, 64, 65 },
            featureMode: "pitch");
    }

    [Fact]
    public void MarkovModelTypeSingletonExists()
    {
        Assert.NotNull(MarkovModelType.Instance);
        Assert.Equal("MarkovModel", MarkovModelType.Instance.Name);
        Assert.Equal(148, MarkovModelType.Instance.GetSpecificity());
    }

    [Fact]
    public void ValueMarkovModelFactoryWraps()
    {
        var model = BuildSampleModel();
        var value = Value.MarkovModel(model);

        Assert.Same(MarkovModelType.Instance, value.Type);
        Assert.Same(model, value.Data);
    }

    [Fact]
    public void IsCompatibleWithEnforcesType()
    {
        Assert.True(MarkovModelType.Instance.IsCompatibleWith(MarkovModelType.Instance));
        Assert.False(MarkovModelType.Instance.IsCompatibleWith(SequenceType.Instance));
        Assert.False(MarkovModelType.Instance.IsCompatibleWith(SfzType.Instance));
        Assert.False(MarkovModelType.Instance.IsCompatibleWith(TuningType.Instance));
    }

    [Fact]
    public void DataClassIsNotRecord()
    {
        // Two MarkovModelData instances with structurally-identical content must NOT
        // be equal under default object equality — reference identity is the contract
        // (Pitfall 6 in 36-PATTERNS.md).
        var m1 = BuildSampleModel();
        var m2 = BuildSampleModel();

        Assert.False(object.ReferenceEquals(m1, m2));
        Assert.False(m1.Equals(m2));
        Assert.NotEqual(m1.GetHashCode(), m2.GetHashCode());
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

        // A model with different order is not structurally equal.
        var dictOrder1 = new Dictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>>(
            MarkovModelData.PrefixComparer.Instance)
        {
            [ImmutableArray.Create(60)] = new[] { (62, 1.0) },
        };
        var different = new MarkovModelData(
            order: 1,
            transitions: dictOrder1,
            stateAlphabet: new[] { 60, 62 },
            featureMode: "pitch");
        Assert.False(m1.StructurallyEquals(different));
    }

    // ====================================================================
    // Task 2 — behavior facts
    // ====================================================================

    private const string Prelude = """
        use "@std"
        use "@generative"
        """;

    private static MarkovModelData TrainViaScript(string body)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable("model").As<MarkovModelData>();
    }

    private static SequenceData GenerateViaScript(string body, string varName = "result")
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    [Fact]
    public void MarkovTrainBuildsTransitions()
    {
        // A 9-note ascending+descending corpus produces a stable order-2 model
        // whose transitions[(60, 62)] entry should observe E4 (64) following
        // C4-D4 — sanity check on the training algorithm's prefix walk.
        var model = TrainViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel model = (markovTrain corpus 2)
            """);

        Assert.Equal(2, model.Order);
        Assert.Equal("pitch", model.FeatureMode);
        Assert.Contains(60, model.StateAlphabet);  // C4
        Assert.Contains(62, model.StateAlphabet);  // D4
        Assert.Contains(64, model.StateAlphabet);  // E4

        var c4d4 = ImmutableArray.Create(60, 62);
        Assert.True(model.Transitions.ContainsKey(c4d4),
            $"expected transition entry for (60, 62); keys: "
            + string.Join("; ", model.Transitions.Keys.Select(k => "(" + string.Join(",", k) + ")")));
        var c4d4Dist = model.Transitions[c4d4];
        Assert.Contains(c4d4Dist, t => t.State == 64);
    }

    [Fact]
    public void MarkovOrderClampedTo1To3()
    {
        // GEN-01 limits Markov order to [1, 3]; over-requesting clamps to 3.
        var modelHigh = TrainViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q |
            MarkovModel model = (markovTrain corpus 5)
            """);
        Assert.Equal(3, modelHigh.Order);

        // Under-requesting (0 or negative) clamps to 1.
        var modelLow = TrainViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q |
            MarkovModel model = (markovTrain corpus 0)
            """);
        Assert.Equal(1, modelLow.Order);
    }

    [Fact]
    public void MarkovGenerateSeededDeterministic()
    {
        // Same seed → identical Sequence shape (note count + pitches).
        var resultA = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m = (markovTrain corpus 2)
            Sequence result = (markovGenerate m 16 42)
            """);
        var resultB = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m = (markovTrain corpus 2)
            Sequence result = (markovGenerate m 16 42)
            """);

        Assert.Equal(resultA.Bars.Count, resultB.Bars.Count);
        for (int i = 0; i < resultA.Bars.Count; i++)
        {
            Assert.Equal(resultA.Bars[i].MusicalNotes.Count, resultB.Bars[i].MusicalNotes.Count);
            for (int j = 0; j < resultA.Bars[i].MusicalNotes.Count; j++)
            {
                Assert.Equal(resultA.Bars[i].MusicalNotes[j].NoteName,
                             resultB.Bars[i].MusicalNotes[j].NoteName);
                Assert.Equal(resultA.Bars[i].MusicalNotes[j].Octave,
                             resultB.Bars[i].MusicalNotes[j].Octave);
            }
        }
    }

    [Fact]
    public void MarkovOneShotEquivalentToTrainGenerate()
    {
        // (markov corpus 2 16 42) == (markovGenerate (markovTrain corpus 2) 16 42)
        var oneShot = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            Sequence result = (markov corpus 2 16 42)
            """);
        var split = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m = (markovTrain corpus 2)
            Sequence result = (markovGenerate m 16 42)
            """);

        Assert.Equal(oneShot.Bars.Count, split.Bars.Count);
        for (int i = 0; i < oneShot.Bars.Count; i++)
        {
            Assert.Equal(oneShot.Bars[i].MusicalNotes.Count,
                         split.Bars[i].MusicalNotes.Count);
            for (int j = 0; j < oneShot.Bars[i].MusicalNotes.Count; j++)
            {
                Assert.Equal(oneShot.Bars[i].MusicalNotes[j].NoteName,
                             split.Bars[i].MusicalNotes[j].NoteName);
                Assert.Equal(oneShot.Bars[i].MusicalNotes[j].Octave,
                             split.Bars[i].MusicalNotes[j].Octave);
            }
        }
    }

    [Fact]
    public void MarkovEqualStructuralCompare()
    {
        // Two independently-trained models on the same corpus + order are
        // STRUCTURALLY equal but REFERENCE-DISTINCT.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + """

            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m1 = (markovTrain corpus 2)
            MarkovModel m2 = (markovTrain corpus 2)
            Bool structural = (markovEqual m1 m2)
            """);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}");

        var structural = runner.GetVariable("structural");
        Assert.True(structural.As<bool>(), "(markovEqual m1 m2) should be true on independently-trained identical models");

        // Reference identity: m1 and m2 are distinct Value instances wrapping
        // distinct MarkovModelData references.
        var m1 = runner.GetVariable("m1");
        var m2 = runner.GetVariable("m2");
        Assert.False(object.ReferenceEquals(m1.Data, m2.Data));
    }

    [Fact]
    public void MarkovGenerateUnseededReproducibleCrossEngines()
    {
        // Two FRESH engines (= fresh ExecutionContexts) running the SAME source
        // produce identical sequences from the unseeded markovGenerate — because
        // the same (SourceLocation, "markovGenerate") key drives the same
        // FNV-1a-derived seed. This pins the D-v1.5-06 unseeded-determinism
        // contract from Plan 36-01.
        var resultA = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m = (markovTrain corpus 2)
            Sequence result = (markovGenerate m 12)
            """);
        var resultB = GenerateViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |
            MarkovModel m = (markovTrain corpus 2)
            Sequence result = (markovGenerate m 12)
            """);

        Assert.Equal(resultA.Bars.Count, resultB.Bars.Count);
        for (int i = 0; i < resultA.Bars.Count; i++)
        {
            Assert.Equal(resultA.Bars[i].MusicalNotes.Count, resultB.Bars[i].MusicalNotes.Count);
            for (int j = 0; j < resultA.Bars[i].MusicalNotes.Count; j++)
            {
                Assert.Equal(resultA.Bars[i].MusicalNotes[j].NoteName,
                             resultB.Bars[i].MusicalNotes[j].NoteName);
            }
        }
    }

    [Fact]
    public void MarkovPitchDurationFeatureMode()
    {
        // features=<<#pitch, #duration>> packs duration into the state; the
        // resulting model should report the "pitch+duration" feature mode.
        var modelTuple = TrainViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q |
            MarkovModel model = (markovTrain corpus 2 features=<<#pitch, #duration>>)
            """);
        Assert.Equal("pitch+duration", modelTuple.FeatureMode);

        // The symbol-form `features=#pitch` reaches the same default mode.
        var modelSym = TrainViaScript("""
            Sequence corpus = | C4q D4q E4q F4q G4q |
            MarkovModel model = (markovTrain corpus 2 features=#pitch)
            """);
        Assert.Equal("pitch", modelSym.FeatureMode);
    }

    [Fact]
    public void MarkovEncodeDecodePitchDurationRoundTrip()
    {
        // The internal pack/unpack helper round-trips losslessly for valid pitch
        // (0..127 fits in 12 bits, and small duration values fit in 20 bits).
        var (p, d) = MarkovFunctions.DecodePitchDurationState(
            MarkovFunctions.EncodePitchDurationState(60, 2));
        Assert.Equal(60, p);
        Assert.Equal(2, d);

        (p, d) = MarkovFunctions.DecodePitchDurationState(
            MarkovFunctions.EncodePitchDurationState(127, 7));
        Assert.Equal(127, p);
        Assert.Equal(7, d);
    }
}
