using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// — bare name is ambiguous under net10.0's implicit usings (Plan 36-05 precedent).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Generative;

/// <summary>
/// Phase 36 Plan 36-06 (GEN-01, D-36-06 + D-36-07): the Markov chain primitive
/// in BOTH one-shot and train+generate-split shapes.
///
/// <para>
/// <b>Composer surface (six registered overloads + markovEqual):</b>
/// <code>
///   (markov corpus order length)             ; one-shot, unseeded → PrngRegistry
///   (markov corpus order length seed)        ; one-shot, explicit seed
///   (markov noteOrIntArray order length)     ; one-shot over a Note[]/Int[] corpus (0615)
///   (markov noteOrIntArray order length seed); seeded variant of the same
///   (markovTrain corpus order)               ; default features=#pitch
///   (markovTrain corpus order features=#pitch)
///   (markovTrain corpus order features=&lt;&lt;#pitch, #duration&gt;&gt;)
///   (markovTrain noteOrIntArray order)       ; train from a Note[]/Int[] corpus (0615)
///   (markovGenerate model length)            ; unseeded → PrngRegistry
///   (markovGenerate model length seed)       ; explicit seed
///   (markovEqual a b)                        ; structural compare → Bool
/// </code>
/// </para>
///
/// <para>
/// <b>D-v1.5-06 / D-36-09 PRNG threading:</b> the explicit-seed paths use
/// <c>new Random(seed)</c> directly; all unseeded paths route through
/// <see cref="ExecutionContext.PrngRegistry"/> keyed by
/// <c>(CurrentCallSite, "markov" | "markovGenerate")</c>. The source-grep CI
/// gate (<c>PrngRegistryNewRandomGateTests</c> + this plan's
/// <c>MarkovDeterminismTests.NoNewRandomInMarkovFunctions</c>) enforces zero
/// unsanctioned <c>new Random(</c> constructions outside the seeded overloads
/// (markovGenerate-seeded, markov-seeded-one-shot over a Sequence, and
/// markov-seeded-one-shot over a Note[]/Int[] array corpus — three sanctioned sites).
/// </para>
///
/// <para>
/// <b>D-36-07 feature extraction:</b> training accepts an optional
/// <c>features=</c> named arg (Phase 36 Plan 36-02 surface):
/// <list type="bullet">
///   <item><c>features=#pitch</c> (the implicit default): each state is a raw
///     MIDI pitch int. Lowest-cost mode.</item>
///   <item><c>features=&lt;&lt;#pitch, #duration&gt;&gt;</c>: each state is
///     <c>(durationEnumSlot &lt;&lt; 12) | (pitch &amp; 0xFFF)</c>. Pitch occupies
///     the low 12 bits; the NoteValueType enum slot (0..7: WHOLE=0 ..
///     ONETWENTYEIGHTH=7, e.g. QUARTER=2 — NOT a quarter-note count) occupies
///     the next 20 bits. Generation unpacks the state back into a typed note
///     with the encoded duration. Higher fidelity at the cost of sparser
///     transitions table.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Charitable interpretation (D-v1.5-05 + Pitfall 2):</b> degenerate inputs
/// (empty corpus, length &lt;= 0, order outside [1,3]) return a charitable
/// empty/clamped result + emit a one-shot stderr advisory. The Markov order is
/// clamped to <c>[1, 3]</c> per GEN-01 — composers asking for order 5 get a
/// 3rd-order model and a heads-up advisory rather than an error.
/// </para>
/// </summary>
public static class MarkovFunctions
{
    // ====================================================================
    // Constants — feature-mode encoding (D-36-07)
    // ====================================================================

    /// <summary>Default feature mode — state is raw MIDI pitch.</summary>
    private const string FeatureModePitch = "pitch";

    /// <summary>
    /// Combined feature mode — state is
    /// <c>(durationEnumSlot &lt;&lt; 12) | (pitch &amp; 0xFFF)</c>: pitch in the
    /// low 12 bits, the NoteValueType enum slot (0..7: WHOLE=0 ..
    /// ONETWENTYEIGHTH=7, e.g. QUARTER=2 — NOT a quarter-note count) shifted up
    /// by 12.
    /// </summary>
    private const string FeatureModePitchDuration = "pitch+duration";

    /// <summary>Default note duration enum slot when no duration info is preserved.</summary>
    private const int DefaultDurationValue = (int)NoteValueType.Value.QUARTER;

    /// <summary>
    /// Encodes a (pitch, durationEnumSlot) pair into a single state int for the
    /// <c>"pitch+duration"</c> feature mode. The duration component is a
    /// <c>NoteValueType.Value</c> enum slot (WHOLE=0 .. ONETWENTYEIGHTH=7), NOT
    /// a quarter-note count. See the class xmldoc for the bit layout.
    /// </summary>
    internal static int EncodePitchDurationState(int midiPitch, int durationEnumSlot)
    {
        // Mask pitch into 12 bits; clamp the duration enum slot into [0, 2^20).
        int p = midiPitch & 0xFFF;
        int d = Math.Max(0, durationEnumSlot) & 0xFFFFF;
        return (d << 12) | p;
    }

    /// <summary>
    /// Decodes a packed state int produced by <see cref="EncodePitchDurationState"/>.
    /// Returns the raw 12-bit pitch and the 20-bit NoteValueType enum-slot
    /// duration components.
    /// </summary>
    internal static (int MidiPitch, int DurationEnumSlot) DecodePitchDurationState(int state)
    {
        int p = state & 0xFFF;
        int d = (state >> 12) & 0xFFFFF;
        return (p, d);
    }

    // ====================================================================
    // Registration entry point
    // ====================================================================

    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // ---- markovTrain ----
        // Default features=#pitch.
        var trainSig = new FunctionSignature("markovTrain",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order"]);
        registry.Register("markovTrain", trainSig, args => MarkovTrainDefault(args, context));

        // features= named-arg accepts EITHER a Symbol (e.g. #pitch / #pitch+duration)
        // OR a Tuple of Symbols (e.g. <<#pitch, #duration>>). The Void wildcard at
        // the third slot keeps a single named-arg-bearing overload registered so the
        // resolver's "first survivor wins" pick is unambiguous; the dispatch on the
        // actual runtime arg type happens inside MarkovTrainWithFeatures.
        var trainSigFeatures = new FunctionSignature("markovTrain",
            [SequenceType.Instance, IntType.Instance, VoidType.Instance],
            ParameterNames: ["corpus", "order", "features"]);
        registry.Register("markovTrain", trainSigFeatures, args => MarkovTrainWithFeatures(args, context));

        // ---- markovGenerate ----
        // Explicit seed — uses new Random(seed) directly (exempt from grep gate).
        var genSeededSig = new FunctionSignature("markovGenerate",
            [MarkovModelType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["model", "length", "seed"]);
        registry.Register("markovGenerate", genSeededSig, args => MarkovGenerateSeeded(args, context));

        // Unseeded — routes through PrngRegistry keyed by (CurrentCallSite, "markovGenerate").
        var genUnseededSig = new FunctionSignature("markovGenerate",
            [MarkovModelType.Instance, IntType.Instance],
            ParameterNames: ["model", "length"]);
        registry.Register("markovGenerate", genUnseededSig, args => MarkovGenerateUnseeded(args, context));

        // ---- markov (one-shot) ----
        var oneShotSeededSig = new FunctionSignature("markov",
            [SequenceType.Instance, IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length", "seed"]);
        registry.Register("markov", oneShotSeededSig, args => MarkovOneShotSeeded(args, context));

        var oneShotUnseededSig = new FunctionSignature("markov",
            [SequenceType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length"]);
        registry.Register("markov", oneShotUnseededSig, args => MarkovOneShotUnseeded(args, context));

        // ---- markov (one-shot over a Note[] / Int[] corpus — feature #1 0615) ----
        // Composers can train directly off an array of pitches without first
        // wrapping them in a Sequence: (markov (list C4 D4 E4) order length [seed]).
        // A Note[] element parses each note string to a MIDI pitch state; an
        // Int[] element is taken as a raw MIDI pitch. Both feed the identical
        // pitch-feature Markov path, so determinism + the train/generate split
        // stay byte-identical with the Sequence form.
        var oneShotNoteArraySeededSig = new FunctionSignature("markov",
            [new ArrayType(NoteType.Instance), IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length", "seed"]);
        registry.Register("markov", oneShotNoteArraySeededSig,
            args => MarkovOneShotFromArraySeeded(args, context, ArrayCorpusKind.Note));

        var oneShotNoteArrayUnseededSig = new FunctionSignature("markov",
            [new ArrayType(NoteType.Instance), IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length"]);
        registry.Register("markov", oneShotNoteArrayUnseededSig,
            args => MarkovOneShotFromArrayUnseeded(args, context, ArrayCorpusKind.Note));

        var oneShotIntArraySeededSig = new FunctionSignature("markov",
            [new ArrayType(IntType.Instance), IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length", "seed"]);
        registry.Register("markov", oneShotIntArraySeededSig,
            args => MarkovOneShotFromArraySeeded(args, context, ArrayCorpusKind.Int));

        var oneShotIntArrayUnseededSig = new FunctionSignature("markov",
            [new ArrayType(IntType.Instance), IntType.Instance, IntType.Instance],
            ParameterNames: ["corpus", "order", "length"]);
        registry.Register("markov", oneShotIntArrayUnseededSig,
            args => MarkovOneShotFromArrayUnseeded(args, context, ArrayCorpusKind.Int));

        // ---- markovTrain over a Note[] / Int[] corpus (feature #1 0615) ----
        // Mirrors the one-shot array surface so composers can pre-train a reusable
        // model from a raw pitch array, then call markovGenerate as usual.
        var trainNoteArraySig = new FunctionSignature("markovTrain",
            [new ArrayType(NoteType.Instance), IntType.Instance],
            ParameterNames: ["corpus", "order"]);
        registry.Register("markovTrain", trainNoteArraySig,
            args => MarkovTrainFromArray(args, context, ArrayCorpusKind.Note));

        var trainIntArraySig = new FunctionSignature("markovTrain",
            [new ArrayType(IntType.Instance), IntType.Instance],
            ParameterNames: ["corpus", "order"]);
        registry.Register("markovTrain", trainIntArraySig,
            args => MarkovTrainFromArray(args, context, ArrayCorpusKind.Int));

        // ---- markovEqual (structural compare) ----
        var markovEqualSig = new FunctionSignature("markovEqual",
            [MarkovModelType.Instance, MarkovModelType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("markovEqual", markovEqualSig, args => MarkovEqual(args));
    }

    // ====================================================================
    // markovTrain implementations
    // ====================================================================

    private static Value MarkovTrainDefault(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var corpus = args[0].As<SequenceData>();
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        return Value.MarkovModel(TrainMarkov(corpus, order, FeatureModePitch));
    }

    /// <summary>
    /// Dispatch helper for the named-arg <c>features=</c> surface. Accepts either
    /// a Symbol (e.g. <c>#pitch</c>) or a Tuple of Symbols
    /// (e.g. <c>&lt;&lt;#pitch, #duration&gt;&gt;</c>) and resolves to the
    /// <see cref="FeatureModePitch"/> / <see cref="FeatureModePitchDuration"/>
    /// internal mode string.
    /// </summary>
    private static Value MarkovTrainWithFeatures(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var corpus = args[0].As<SequenceData>();
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);

        string mode;
        var featuresVal = args[2];
        if (featuresVal.Type is SymbolType && featuresVal.Data is string featureSymbol)
        {
            mode = featureSymbol switch
            {
                "pitch" => FeatureModePitch,
                "pitch+duration" => FeatureModePitchDuration,
                _ => UnknownFeatureFallback(featureSymbol, ctx),
            };
        }
        else if (featuresVal.Type is TupleType && featuresVal.Data is IReadOnlyList<Value> tupleComponents)
        {
            // Recognised tuple shape: <<#pitch, #duration>> (in either order —
            // order doesn't change the encoding).
            bool hasPitch = false, hasDuration = false;
            foreach (var v in tupleComponents)
            {
                if (v.Type is SymbolType && v.Data is string s)
                {
                    if (s == "pitch") hasPitch = true;
                    else if (s == "duration") hasDuration = true;
                }
            }

            if (hasPitch && hasDuration)
            {
                mode = FeatureModePitchDuration;
            }
            else if (hasPitch)
            {
                mode = FeatureModePitch;
            }
            else
            {
                // Phase 44 Plan 44-07 Pattern S3: under strict mode the advisory
                // becomes a composer-facing [strict] error; non-strict path
                // preserved byte-identical (Pitfall 5 two-run cmp-clean).
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [markov] features unsupported — unrecognised features tuple at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                    return Value.Void();
                }
                RenderingDiagnostics.WarnOnce(
                    $"markovTrain:unknown-features:{ctx.CurrentCallSite}",
                    $"[markovTrain] unrecognised features tuple at {ctx.CurrentCallSite}; "
                    + "falling back to features=#pitch");
                mode = FeatureModePitch;
            }
        }
        else
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [markov] features unsupported — expects Symbol or Tuple<<Symbol,...>> at {ctx.CurrentCallSite}; got {featuresVal.Type.Name}",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            RenderingDiagnostics.WarnOnce(
                $"markovTrain:non-symbol-features:{ctx.CurrentCallSite}",
                $"[markovTrain] features= expects Symbol or Tuple<<Symbol,...>> at {ctx.CurrentCallSite}; "
                + $"got {featuresVal.Type.Name}; falling back to features=#pitch");
            mode = FeatureModePitch;
        }
        return Value.MarkovModel(TrainMarkov(corpus, order, mode));
    }

    private static string UnknownFeatureFallback(string sym, ExecutionContext ctx)
    {
        // Phase 44 Plan 44-07 Pattern S3: strict-mode branch. Returns the
        // default mode after reporting — caller sees a non-null mode but the
        // ErrorReporter accumulates the [strict] error for surface visibility.
        if (ctx.CallerStrictMode)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] [markov] features unsupported — unrecognised features=#{sym} at {ctx.CurrentCallSite}",
                ctx.CurrentCallSite);
            return FeatureModePitch;
        }
        RenderingDiagnostics.WarnOnce(
            $"markovTrain:unknown-features:{ctx.CurrentCallSite}:{sym}",
            $"[markovTrain] unrecognised features=#{sym} at {ctx.CurrentCallSite}; "
            + "falling back to features=#pitch");
        return FeatureModePitch;
    }

    private static int ClampOrderWithAdvisory(int requestedOrder, ExecutionContext ctx)
    {
        int clamped = Math.Clamp(requestedOrder, 1, 3);
        if (clamped != requestedOrder)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch. Returns the
            // clamped value after reporting — the caller still gets a valid
            // order so the rest of training/generation completes; the
            // ErrorReporter accumulates the [strict] error for surface visibility.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [markov] order clamped to {clamped} (got {requestedOrder}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return clamped;
            }
            RenderingDiagnostics.WarnOnce(
                $"markov:order-clamp:{ctx.CurrentCallSite}:{requestedOrder}",
                $"[markov] order {requestedOrder} clamped to {clamped} at {ctx.CurrentCallSite} "
                + "(GEN-01 limits order to [1, 3])");
        }
        return clamped;
    }

    // ====================================================================
    // markovGenerate implementations
    // ====================================================================

    private static Value MarkovGenerateSeeded(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        int seed = args[2].As<int>();
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed overload per D-36-06
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    private static Value MarkovGenerateUnseeded(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        var rng = ctx.PrngRegistry.GetRandom(ctx.CurrentCallSite, "markovGenerate");
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    // ====================================================================
    // markov one-shot implementations
    // ====================================================================

    private static Value MarkovOneShotSeeded(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var corpus = args[0].As<SequenceData>();
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        int length = args[2].As<int>();
        int seed = args[3].As<int>();
        var model = TrainMarkov(corpus, order, FeatureModePitch);
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed overload per D-36-06
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    private static Value MarkovOneShotUnseeded(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var corpus = args[0].As<SequenceData>();
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        int length = args[2].As<int>();
        var model = TrainMarkov(corpus, order, FeatureModePitch);
        var rng = ctx.PrngRegistry.GetRandom(ctx.CurrentCallSite, "markov");
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    // ====================================================================
    // markov / markovTrain over a Note[] / Int[] corpus (feature #1 0615)
    // ====================================================================

    /// <summary>
    /// Which array element shape an array-corpus overload was registered for.
    /// </summary>
    private enum ArrayCorpusKind { Note, Int }

    private static Value MarkovOneShotFromArraySeeded(
        IReadOnlyList<Value> args, ExecutionContext ctx, ArrayCorpusKind kind)
    {
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        int length = args[2].As<int>();
        int seed = args[3].As<int>();
        var states = ExtractStatesFromArray(args[0], kind, ctx);
        var model = TrainMarkovFromStates(states, order, FeatureModePitch);
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed overload per D-36-06
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    private static Value MarkovOneShotFromArrayUnseeded(
        IReadOnlyList<Value> args, ExecutionContext ctx, ArrayCorpusKind kind)
    {
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        int length = args[2].As<int>();
        var states = ExtractStatesFromArray(args[0], kind, ctx);
        var model = TrainMarkovFromStates(states, order, FeatureModePitch);
        var rng = ctx.PrngRegistry.GetRandom(ctx.CurrentCallSite, "markov");
        return Value.Sequence(GenerateMarkov(model, length, rng, ctx));
    }

    private static Value MarkovTrainFromArray(
        IReadOnlyList<Value> args, ExecutionContext ctx, ArrayCorpusKind kind)
    {
        int order = ClampOrderWithAdvisory(args[1].As<int>(), ctx);
        var states = ExtractStatesFromArray(args[0], kind, ctx);
        return Value.MarkovModel(TrainMarkovFromStates(states, order, FeatureModePitch));
    }

    /// <summary>
    /// Converts a Flow array Value (Note[] or Int[]) into the list of MIDI-pitch
    /// state ints the Markov training core consumes. Note elements parse their
    /// stored note string; Int elements are taken as raw MIDI pitches. Charitable
    /// per D-v1.5-05: an unparseable / null element is skipped with a one-shot
    /// advisory (or a [strict] error under strict mode) rather than throwing.
    /// </summary>
    private static List<int> ExtractStatesFromArray(
        Value arrayValue, ArrayCorpusKind kind, ExecutionContext ctx)
    {
        var elements = arrayValue.As<IReadOnlyList<Value>>();
        var states = new List<int>(elements.Count);
        foreach (var element in elements)
        {
            if (kind == ArrayCorpusKind.Int)
            {
                if (element.Data is int midi)
                {
                    states.Add(midi);
                }
                continue;
            }

            // Note element — stored as the original note string (e.g. "C4", "G#3").
            if (element.Data is string noteStr)
            {
                try
                {
                    var (letter, octave, alteration) = NoteType.Parse(noteStr);
                    states.Add(NoteType.ToMidiNote(letter, octave, alteration));
                }
                catch (Exception)
                {
                    if (ctx.CallerStrictMode)
                    {
                        ctx.ErrorReporter.ReportError(
                            $"[strict] [markov] unparseable Note '{noteStr}' in corpus at {ctx.CurrentCallSite}",
                            ctx.CurrentCallSite);
                        continue;
                    }
                    RenderingDiagnostics.WarnOnce(
                        $"markov:unparseable-corpus-note:{ctx.CurrentCallSite}:{noteStr}",
                        $"[markov] unparseable Note '{noteStr}' in corpus at "
                        + $"{ctx.CurrentCallSite}; skipped");
                }
            }
        }
        return states;
    }

    // ====================================================================
    // markovEqual — structural compare
    // ====================================================================

    private static Value MarkovEqual(IReadOnlyList<Value> args)
    {
        var a = args[0].As<MarkovModelData>();
        var b = args[1].As<MarkovModelData>();
        return Value.Bool(a.StructurallyEquals(b));
    }

    // ====================================================================
    // Training algorithm
    // ====================================================================

    /// <summary>
    /// Slides an <paramref name="order"/>-sized window over the corpus notes
    /// and tallies <c>prefix → next-state</c> transitions. Non-pitched notes
    /// (rests) skip the window — they neither contribute as states nor break
    /// the window. The chosen <paramref name="featureMode"/> determines how
    /// each note becomes an int state.
    /// </summary>
    internal static MarkovModelData TrainMarkov(SequenceData corpus, int order, string featureMode)
    {
        var states = ExtractStates(corpus, featureMode);
        return TrainMarkovFromStates(states, order, featureMode);
    }

    /// <summary>
    /// Markov training core over an already-extracted list of integer states.
    /// Shared by the Sequence-corpus path (<see cref="TrainMarkov"/>) and the
    /// Note[]/Int[]-corpus path (<see cref="MarkovTrainFromArray"/> /
    /// <see cref="MarkovOneShotFromArraySeeded"/>). The feature mode is recorded
    /// on the model; array corpora always use <c>"pitch"</c> states.
    /// </summary>
    internal static MarkovModelData TrainMarkovFromStates(
        List<int> states, int order, string featureMode)
    {
        var alphabet = new List<int>();
        var alphabetSet = new HashSet<int>();
        foreach (var s in states)
            if (alphabetSet.Add(s)) alphabet.Add(s);

        // Per-prefix transition tally — uses a dict-of-dicts internally then
        // freezes into the final (state → weight) list shape at the end.
        var working = new Dictionary<ImmutableArray<int>, Dictionary<int, double>>(
            MarkovModelData.PrefixComparer.Instance);

        if (states.Count > order)
        {
            for (int i = 0; i + order < states.Count; i++)
            {
                var prefixBuilder = ImmutableArray.CreateBuilder<int>(order);
                for (int j = 0; j < order; j++) prefixBuilder.Add(states[i + j]);
                var prefix = prefixBuilder.ToImmutable();
                int next = states[i + order];

                if (!working.TryGetValue(prefix, out var inner))
                {
                    inner = new Dictionary<int, double>();
                    working[prefix] = inner;
                }
                inner.TryGetValue(next, out double w);
                inner[next] = w + 1.0;
            }
        }

        var transitions = new Dictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>>(
            MarkovModelData.PrefixComparer.Instance);
        foreach (var kv in working)
        {
            // Preserve first-seen state order in the inner list — Dictionary's
            // insertion-order semantics (.NET runtime contract) keeps it
            // deterministic across consecutive calls on the same corpus.
            var list = new List<(int State, double Weight)>(kv.Value.Count);
            foreach (var inner in kv.Value) list.Add((inner.Key, inner.Value));
            transitions[kv.Key] = list;
        }

        return new MarkovModelData(order, transitions, alphabet, featureMode);
    }

    /// <summary>
    /// Walks the corpus bars and converts each non-rest <see cref="MusicalNoteData"/>
    /// into a state int per the chosen feature mode.
    /// </summary>
    private static List<int> ExtractStates(SequenceData corpus, string featureMode)
    {
        var result = new List<int>();
        foreach (var bar in corpus.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest) continue;
                int midi = NoteType.ToMidiNote(note.NoteName, note.Octave, note.Alteration);
                if (featureMode == FeatureModePitchDuration)
                {
                    int dur = note.DurationValue ?? DefaultDurationValue;
                    result.Add(EncodePitchDurationState(midi, dur));
                }
                else
                {
                    result.Add(midi);
                }
            }
        }
        return result;
    }

    // ====================================================================
    // Generation algorithm
    // ====================================================================

    /// <summary>
    /// Generates a <see cref="SequenceData"/> of <paramref name="length"/> notes
    /// from <paramref name="model"/> using cumulative-weight roulette via
    /// <paramref name="rng"/>. The first <c>order</c> notes are seeded from the
    /// alphabet's first entry (or random alphabet pick for shorter corpora);
    /// subsequent notes are sampled per the model's transitions table.
    /// </summary>
    internal static SequenceData GenerateMarkov(
        MarkovModelData model,
        int length,
        Random rng,
        ExecutionContext ctx)
    {
        var output = new SequenceData();
        if (length <= 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [markov] length clamped — got {length} (must be > 0) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return output;
            }
            RenderingDiagnostics.WarnOnce(
                $"markov:invalid-length:{ctx.CurrentCallSite}",
                $"[markov] length {length} must be > 0; returned empty sequence");
            return output;
        }
        if (model.StateAlphabet.Count == 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [markov] empty corpus — trained model has empty state alphabet at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return output;
            }
            RenderingDiagnostics.WarnOnce(
                $"markov:empty-corpus:{ctx.CurrentCallSite}",
                "[markov] trained on empty corpus; returned empty sequence");
            return output;
        }

        var generatedStates = new List<int>(length);

        // Seed the window with the first `order` states from the alphabet
        // (deterministic — uses the alphabet's first observed states for the
        // cold start, NOT the rng, so generation with the same model on the
        // same seed produces the same output regardless of alphabet hashing).
        int seedCount = Math.Min(model.Order, model.StateAlphabet.Count);
        for (int i = 0; i < seedCount && generatedStates.Count < length; i++)
            generatedStates.Add(model.StateAlphabet[i]);

        // Walk the window forward, sampling each next state.
        while (generatedStates.Count < length)
        {
            ImmutableArray<int> window;
            if (generatedStates.Count >= model.Order)
            {
                var b = ImmutableArray.CreateBuilder<int>(model.Order);
                int start = generatedStates.Count - model.Order;
                for (int j = 0; j < model.Order; j++) b.Add(generatedStates[start + j]);
                window = b.ToImmutable();
            }
            else
            {
                // Cold-start: short corpus, no full window yet — pick from the
                // alphabet directly via the rng.
                int idx = rng.Next(model.StateAlphabet.Count);
                generatedStates.Add(model.StateAlphabet[idx]);
                continue;
            }

            int picked;
            if (model.Transitions.TryGetValue(window, out var distribution) && distribution.Count > 0)
            {
                picked = SampleByWeight(distribution, rng);
            }
            else
            {
                // No transition data for this window — uniform pick from the alphabet.
                picked = model.StateAlphabet[rng.Next(model.StateAlphabet.Count)];
            }
            generatedStates.Add(picked);
        }

        BuildSequenceFromStates(output, generatedStates, model);
        return output;
    }

    private static int SampleByWeight(
        IReadOnlyList<(int State, double Weight)> distribution,
        Random rng)
    {
        double total = 0.0;
        for (int i = 0; i < distribution.Count; i++) total += distribution[i].Weight;
        if (total <= 0.0) return distribution[0].State;

        double draw = rng.NextDouble() * total;
        double accum = 0.0;
        for (int i = 0; i < distribution.Count; i++)
        {
            accum += distribution[i].Weight;
            if (draw < accum) return distribution[i].State;
        }
        return distribution[^1].State;
    }

    /// <summary>
    /// Materialises the generated state ints into a one-bar
    /// <see cref="SequenceData"/>. Each state becomes a single
    /// <see cref="MusicalNoteData"/>; in the <c>pitch+duration</c> feature mode
    /// the encoded duration is restored, otherwise we stamp the default
    /// quarter-note duration.
    /// </summary>
    private static void BuildSequenceFromStates(
        SequenceData output,
        List<int> states,
        MarkovModelData model)
    {
        // Pick a sane default time signature for the rendered bar. The composer
        // can re-bar later via existing transforms; the structural intent of a
        // Markov output is the note sequence, not the bar layout.
        var timeSig = new TimeSignatureData(4, 4);

        var notes = new List<MusicalNoteData>(states.Count);
        foreach (int state in states)
        {
            int midi;
            int durationValue;
            if (model.FeatureMode == FeatureModePitchDuration)
            {
                var (p, d) = DecodePitchDurationState(state);
                midi = p;
                durationValue = d;
            }
            else
            {
                midi = state;
                durationValue = DefaultDurationValue;
            }

            // Clamp the duration enum into a valid NoteValueType slot (WHOLE..ONETWENTYEIGHTH).
            const int minDur = (int)NoteValueType.Value.WHOLE;
            const int maxDur = (int)NoteValueType.Value.ONETWENTYEIGHTH;
            durationValue = Math.Clamp(durationValue, minDur, maxDur);

            // Clamp the MIDI pitch into the valid range; out-of-range states fall back
            // to middle C rather than throwing.
            int clampedMidi = Math.Clamp(midi, 12, 127);
            var (name, octave, alteration) = NoteType.FromMidiNote(clampedMidi);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false));
        }

        if (notes.Count > 0)
            output.AddBar(new BarData(notes, timeSig));
    }
}
