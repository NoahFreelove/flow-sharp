using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// — bare name is ambiguous under net10.0's implicit usings (Plan 36-05/06/07/08 precedent).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Generative;

/// <summary>
/// Phase 36 Plan 36-09 (GEN-04, D-36-08 + D-36-09): the chaos-map generative
/// primitives — Lorenz attractor + logistic map — plus the
/// <c>quantizeToScale</c> bridge that maps a raw <c>Array[Double]</c> series
/// into a musical <c>Sequence</c>.
///
/// <para>
/// <b>Composer surface (four registered builtins):</b>
/// <code>
///   (lorenz sigma rho beta length seed)                  ; → Array[Double] (x-axis trajectory)
///   (logistic r length seed)                             ; → Array[Double] in [0, 1]
///   (quantizeToScale series scaleName)                   ; → Sequence (string scale-name form)
///   (quantizeToScale series scaleNotes)                  ; → Sequence (Array[Note] direct form)
/// </code>
/// </para>
///
/// <para>
/// <b>Lorenz algorithm (RESEARCH §Pattern 5):</b> forward-Euler integration
/// of the canonical 3-state Lorenz attractor with <c>dt=0.01</c> and 100
/// warm-up iterations (chaotic transient discarded). Initial conditions
/// <c>(x=1.0, y=0.0, z=0.0)</c> receive a tiny seed-derived perturbation
/// (within ±5e-4) so distinct seeds produce distinct trajectories. The
/// x-axis is returned by default; composer can pick y/z via a future
/// optional named arg.
/// </para>
///
/// <para>
/// <b>Logistic map algorithm:</b> standard recurrence
/// <c>x_{n+1} = r * x_n * (1 - x_n)</c> over a seed-derived initial
/// <c>x ∈ (0, 1)</c> with 100 warm-up iterations. r-values outside [0, 4]
/// charitably clamp to 4.0 + WarnOnce per D-v1.5-05 (r > 4 escapes [0, 1]
/// and produces NaN).
/// </para>
///
/// <para>
/// <b>quantizeToScale algorithm:</b> normalise the input series to [0, 1]
/// via min/max scaling, multiply by <c>scaleNotes.Length</c>, floor + clamp
/// into <c>[0, scaleNotes.Length - 1]</c>, and look up the scale note at
/// that index. Each value becomes a quarter-note in the output sequence.
/// Notes are packed into a single bar (4/4 default) without bar-fitting —
/// composer applies <c>(fast)</c> or <c>(slow)</c> downstream if needed.
/// </para>
///
/// <para>
/// <b>D-36-09 cross-platform FP divergence:</b> Lorenz (and to a lesser
/// extent logistic) are chaotic dynamical systems — chained floating-point
/// arithmetic amplifies platform-specific FPU and <c>Math.*</c> library
/// quirks exponentially. The same seed on Linux x64 vs macOS ARM64 vs
/// Windows x64 may produce subtly different trajectories after ~50
/// iterations. <b>Same-platform two-run cmp-clean is preserved</b> (IEEE
/// 754 reproducibility on a single machine); cross-platform reproducibility
/// is NOT guaranteed for the chaotic-system outputs of this module. See
/// <c>.planning/phases/36-sequence-algebra-generative/36-RESEARCH.md</c>
/// Pitfall 4 for the full discussion.
/// </para>
///
/// <para>
/// <b>Charitable interpretation (D-v1.5-05 + Pitfall 2):</b>
/// <list type="bullet">
///   <item>Lorenz: σ &lt; 0 OR ρ &lt;= 0 OR β &lt;= 0 → fall back to canonical
///         butterfly params (σ=10, ρ=28, β=8/3) + WarnOnce</item>
///   <item>Logistic: r outside [0, 4] → clamp + WarnOnce (r &gt; 4 escapes
///         [0, 1]; r &lt; 0 produces nonsense)</item>
///   <item>length &lt;= 0 → return empty Array[Double] + WarnOnce</item>
///   <item>length &gt; 100_000 → clamp + WarnOnce (T-36-21 DoS guard)</item>
///   <item>quantizeToScale: unknown string scaleName → charitable fallback
///         to chromatic 12-tone (C4..B4) + WarnOnce</item>
///   <item>quantizeToScale: empty series → empty Sequence + WarnOnce</item>
/// </list>
/// </para>
/// </summary>
public static class ChaosFunctions
{
    // ====================================================================
    // Constants — algorithm parameters
    // ====================================================================

    /// <summary>Forward-Euler integration step (RESEARCH §Pattern 5).</summary>
    private const double DefaultDt = 0.01;

    /// <summary>
    /// Warm-up iterations (chaotic transient) discarded before emitting
    /// trajectory points. RESEARCH §Pattern 5 + canonical chaos-map
    /// pedagogy.
    /// </summary>
    private const int WarmupIterations = 100;

    /// <summary>
    /// T-36-21 DoS guard. <c>length</c> arg clamped to this cap to prevent
    /// runaway Array[Double] allocations.
    /// </summary>
    internal const int MaxLength = 100_000;

    /// <summary>Canonical Lorenz butterfly σ.</summary>
    private const double CanonicalSigma = 10.0;

    /// <summary>Canonical Lorenz butterfly ρ.</summary>
    private const double CanonicalRho = 28.0;

    /// <summary>Canonical Lorenz butterfly β = 8/3.</summary>
    private const double CanonicalBeta = 8.0 / 3.0;

    // ====================================================================
    // Registration entry point
    // ====================================================================

    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // ---- lorenz ----
        var lorenzSig = new FunctionSignature("lorenz",
            [DoubleType.Instance, DoubleType.Instance, DoubleType.Instance,
             IntType.Instance, IntType.Instance],
            ParameterNames: ["sigma", "rho", "beta", "length", "seed"]);
        registry.Register("lorenz", lorenzSig, args => Lorenz(args, context));

        // ---- logistic ----
        var logisticSig = new FunctionSignature("logistic",
            [DoubleType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["r", "length", "seed"]);
        registry.Register("logistic", logisticSig, args => Logistic(args, context));

        // ---- quantizeToScale (String scale-name form) ----
        var quantizeStringSig = new FunctionSignature("quantizeToScale",
            [new ArrayType(DoubleType.Instance), StringType.Instance],
            ParameterNames: ["series", "scaleName"]);
        registry.Register("quantizeToScale", quantizeStringSig,
            args => QuantizeStringForm(args, context));

        // ---- quantizeToScale (Array[Note] direct form — escape hatch) ----
        var quantizeArraySig = new FunctionSignature("quantizeToScale",
            [new ArrayType(DoubleType.Instance), new ArrayType(NoteType.Instance)],
            ParameterNames: ["series", "scaleNotes"]);
        registry.Register("quantizeToScale", quantizeArraySig,
            args => QuantizeArrayForm(args, context));
    }

    // ====================================================================
    // Lorenz attractor
    // ====================================================================

    /// <summary>
    /// Composer-facing Lorenz attractor entry. Forward-Euler integration of
    /// <c>dx/dt = σ(y - x); dy/dt = x(ρ - z) - y; dz/dt = xy - βz</c>
    /// starting from a seed-perturbed initial condition. Returns the
    /// x-axis trajectory as Array[Double].
    ///
    /// <para>
    /// <b>D-36-09 cross-platform caveat:</b> the chained FP arithmetic in
    /// the inner loop may produce platform-specific divergence after ~50
    /// iterations. Same-platform two-run cmp-clean is preserved;
    /// cross-platform reproducibility is NOT guaranteed.
    /// </para>
    /// </summary>
    private static Value Lorenz(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        double sigma = args[0].As<double>();
        double rho = args[1].As<double>();
        double beta = args[2].As<double>();
        int length = args[3].As<int>();
        int seed = args[4].As<int>();

        (sigma, rho, beta) = ChariteableLorenzParams(sigma, rho, beta, ctx);
        length = ClampLengthWithAdvisory(length, ctx, "lorenz");

        if (length <= 0)
            return Value.Array(Array.Empty<Value>(), DoubleType.Instance);

        // Initial conditions: canonical (1, 0, 0) + small seed-derived
        // perturbation (within ±5e-4) so distinct seeds yield distinct
        // trajectories without escaping the bounded attractor.
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed REQ contract per D-36-09
        double x = 1.0 + (rng.NextDouble() - 0.5) * 1e-3;
        double y = 0.0;
        double z = 0.0;

        // Discard the chaotic transient.
        for (int i = 0; i < WarmupIterations; i++)
        {
            double dx = sigma * (y - x);
            double dy = x * (rho - z) - y;
            double dz = x * y - beta * z;
            x += dx * DefaultDt;
            y += dy * DefaultDt;
            z += dz * DefaultDt;
        }

        // Capture the x-axis trajectory.
        var result = new Value[length];
        for (int i = 0; i < length; i++)
        {
            double dx = sigma * (y - x);
            double dy = x * (rho - z) - y;
            double dz = x * y - beta * z;
            x += dx * DefaultDt;
            y += dy * DefaultDt;
            z += dz * DefaultDt;
            result[i] = Value.Double(x);
        }
        return Value.Array(result, DoubleType.Instance);
    }

    private static (double sigma, double rho, double beta) ChariteableLorenzParams(
        double sigma, double rho, double beta, ExecutionContext ctx)
    {
        if (sigma < 0 || rho <= 0 || beta <= 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            // D-36-09 caveat: the strict ERROR PATH short-circuits before any
            // chaotic FP compute, so the [strict] error itself is
            // same-platform deterministic.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [lorenz] degenerate params (σ={sigma}, ρ={rho}, β={beta}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return (CanonicalSigma, CanonicalRho, CanonicalBeta);
            }
            RenderingDiagnostics.WarnOnce(
                $"lorenz:degenerate-params:{ctx.CurrentCallSite}:{sigma},{rho},{beta}",
                $"[lorenz] degenerate params (σ={sigma}, ρ={rho}, β={beta}) at "
                + $"{ctx.CurrentCallSite}; falling back to canonical butterfly "
                + $"(σ={CanonicalSigma}, ρ={CanonicalRho}, β=8/3)");
            return (CanonicalSigma, CanonicalRho, CanonicalBeta);
        }
        return (sigma, rho, beta);
    }

    // ====================================================================
    // Logistic map
    // ====================================================================

    /// <summary>
    /// Composer-facing logistic map entry. Iterates
    /// <c>x_{n+1} = r * x_n * (1 - x_n)</c> over a seed-derived initial
    /// <c>x ∈ (0, 1)</c>. Returns Array[Double] with values in [0, 1].
    ///
    /// <para>
    /// <b>D-36-09 cross-platform caveat:</b> logistic is less FP-sensitive
    /// than Lorenz (no chained transcendental calls) but still uses
    /// chained multiplications — cross-platform reproducibility is not
    /// guaranteed for long sequences; same-platform two-run cmp-clean is
    /// preserved.
    /// </para>
    /// </summary>
    private static Value Logistic(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        double r = args[0].As<double>();
        int length = args[1].As<int>();
        int seed = args[2].As<int>();

        r = ClampRWithAdvisory(r, ctx);
        length = ClampLengthWithAdvisory(length, ctx, "logistic");

        if (length <= 0)
            return Value.Array(Array.Empty<Value>(), DoubleType.Instance);

        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed REQ contract per D-36-09
        double x = rng.NextDouble(); // initial in [0, 1)
        // Guard against the degenerate endpoints — rng.NextDouble can return
        // 0.0 (rare); nudge into the open interval (0, 1) so the recurrence
        // doesn't lock at the trivial fixed point.
        if (x == 0.0) x = 1e-6;

        // Discard the warm-up transient.
        for (int i = 0; i < WarmupIterations; i++)
        {
            x = r * x * (1.0 - x);
        }

        var result = new Value[length];
        for (int i = 0; i < length; i++)
        {
            x = r * x * (1.0 - x);
            result[i] = Value.Double(x);
        }
        return Value.Array(result, DoubleType.Instance);
    }

    private static double ClampRWithAdvisory(double r, ExecutionContext ctx)
    {
        if (r < 0.0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [logistic] r clamped to [0, 4] — got {r} (< 0) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return 0.0;
            }
            RenderingDiagnostics.WarnOnce(
                $"logistic:r-negative:{ctx.CurrentCallSite}:{r}",
                $"[logistic] r {r} < 0 at {ctx.CurrentCallSite}; clamped to 0 "
                + "(logistic map needs r ∈ [0, 4])");
            return 0.0;
        }
        if (r > 4.0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [logistic] r clamped to [0, 4] — got {r} (> 4) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return 4.0;
            }
            RenderingDiagnostics.WarnOnce(
                $"logistic:r-cap:{ctx.CurrentCallSite}:{r}",
                $"[logistic] r {r} > 4 at {ctx.CurrentCallSite}; clamped to 4.0 "
                + "(r > 4 escapes [0, 1] and produces NaN)");
            return 4.0;
        }
        return r;
    }

    // ====================================================================
    // quantizeToScale — String form (resolves via ScaleDatabase)
    // ====================================================================

    private static Value QuantizeStringForm(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var series = args[0].As<IReadOnlyList<Value>>();
        string scaleName = args[1].As<string>();

        var scaleNotes = ResolveScaleByName(scaleName, ctx);
        return Value.Sequence(Quantize(series, scaleNotes, ctx));
    }

    /// <summary>
    /// Resolves a string scale name (e.g. "cmajor") via
    /// <see cref="ScaleDatabase.GetScaleNotes"/>. On unknown name, charitably
    /// falls back to chromatic 12-tone (C4..B4) + WarnOnce per CLAUDE.md
    /// ergonomics — composer hears the warning but renders something.
    /// </summary>
    private static List<MidiPitch> ResolveScaleByName(string scaleName, ExecutionContext ctx)
    {
        // ScaleDatabase returns 7 chromatic-name strings (e.g. "C", "D", "Cs"
        // — `s` suffix denotes sharp) or null for unknown keys.
        var notes = ScaleDatabase.GetScaleNotes(scaleName);
        if (notes == null)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [quantizeToScale] unknown scale '{scaleName}' at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return ChromaticC4ToB4();
            }
            RenderingDiagnostics.WarnOnce(
                $"quantizeToScale:unknown-scale:{ctx.CurrentCallSite}:{scaleName}",
                $"[quantizeToScale] unknown scale name '{scaleName}' at "
                + $"{ctx.CurrentCallSite}; falling back to chromatic 12-tone (C4..B4)");
            return ChromaticC4ToB4();
        }

        // Convert "C" / "Cs" / "Db" → MIDI pitches at octave 4.
        var result = new List<MidiPitch>(notes.Length);
        foreach (var n in notes)
        {
            string normalized = NormalizeScaleNoteName(n);
            try
            {
                var (letter, octave, alteration) = NoteType.Parse(normalized + "4");
                result.Add(new MidiPitch(letter, octave, alteration));
            }
            catch
            {
                // Defensive — ScaleDatabase should never return an unparseable
                // name, but if it does, drop the entry rather than throw.
            }
        }
        return result.Count > 0 ? result : ChromaticC4ToB4();
    }

    /// <summary>
    /// Normalises ScaleDatabase's chromatic-name format ("Cs", "Ds") into a
    /// form <see cref="NoteType.Parse"/> understands ("C#", "D#"). Naturals
    /// pass through unchanged.
    /// </summary>
    private static string NormalizeScaleNoteName(string n)
    {
        if (n.Length >= 2 && (n[1] == 's' || n[1] == 'S'))
            return char.ToUpper(n[0]) + "#" + n.Substring(2);
        return n;
    }

    private static List<MidiPitch> ChromaticC4ToB4()
    {
        // C4 .. B4 (MIDI 60..71) — 12 notes, ascending semitones.
        var result = new List<MidiPitch>(12);
        for (int midi = 60; midi <= 71; midi++)
        {
            var (letter, octave, alteration) = NoteType.FromMidiNote(midi);
            result.Add(new MidiPitch(letter, octave, alteration));
        }
        return result;
    }

    // ====================================================================
    // quantizeToScale — Array[Note] form (composer's escape hatch)
    // ====================================================================

    private static Value QuantizeArrayForm(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var series = args[0].As<IReadOnlyList<Value>>();
        var scaleNotesValues = args[1].As<IReadOnlyList<Value>>();

        var scaleNotes = new List<MidiPitch>(scaleNotesValues.Count);
        foreach (var v in scaleNotesValues)
        {
            // Note values store the original note string (e.g. "C4", "G#3").
            if (v.Data is string noteStr)
            {
                try
                {
                    var (letter, octave, alteration) = NoteType.Parse(noteStr);
                    scaleNotes.Add(new MidiPitch(letter, octave, alteration));
                }
                catch (Exception)
                {
                    // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                    if (ctx.CallerStrictMode)
                    {
                        ctx.ErrorReporter.ReportError(
                            $"[strict] [quantizeToScale] unknown scale — unparseable Note '{noteStr}' at {ctx.CurrentCallSite}",
                            ctx.CurrentCallSite);
                        continue;
                    }
                    RenderingDiagnostics.WarnOnce(
                        $"quantizeToScale:unparseable-note:{ctx.CurrentCallSite}:{noteStr}",
                        $"[quantizeToScale] unparseable Note '{noteStr}' at "
                        + $"{ctx.CurrentCallSite}; skipped");
                }
            }
        }

        if (scaleNotes.Count == 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [quantizeToScale] empty array — using chromatic 12-tone (C4..B4) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                scaleNotes = ChromaticC4ToB4();
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"quantizeToScale:empty-scale:{ctx.CurrentCallSite}",
                    $"[quantizeToScale] empty scaleNotes Array at {ctx.CurrentCallSite}; "
                    + "falling back to chromatic 12-tone (C4..B4)");
                scaleNotes = ChromaticC4ToB4();
            }
        }

        return Value.Sequence(Quantize(series, scaleNotes, ctx));
    }

    // ====================================================================
    // Quantization core
    // ====================================================================

    /// <summary>
    /// Normalises the input series to [0, 1] via min/max scaling, then
    /// floor-maps each value to a scale-note index. The result Sequence
    /// packs all the produced notes into a single bar (4/4) — composer
    /// applies <c>fast</c> / <c>slow</c> / bar-splitting downstream.
    /// </summary>
    private static SequenceData Quantize(
        IReadOnlyList<Value> series, List<MidiPitch> scaleNotes, ExecutionContext ctx)
    {
        var seq = new SequenceData();
        if (series.Count == 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [quantizeToScale] empty series at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return seq;
            }
            RenderingDiagnostics.WarnOnce(
                $"quantizeToScale:empty-series:{ctx.CurrentCallSite}",
                $"[quantizeToScale] empty series at {ctx.CurrentCallSite}; "
                + "returning empty Sequence");
            return seq;
        }

        // Find min/max of the series (LINQ-free for clarity).
        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        foreach (var v in series)
        {
            double d = v.As<double>();
            if (d < min) min = d;
            if (d > max) max = d;
        }

        double range = max - min;
        if (range < 1e-12) range = 1.0; // avoid divide-by-zero on constant series

        int scaleLen = scaleNotes.Count;
        var notes = new List<MusicalNoteData>(series.Count);
        int durationValue = (int)NoteValueType.Value.QUARTER;
        foreach (var v in series)
        {
            double d = v.As<double>();
            double normalized = (d - min) / range; // [0, 1]
            int idx = (int)(normalized * scaleLen);
            if (idx >= scaleLen) idx = scaleLen - 1;
            if (idx < 0) idx = 0;
            var pitch = scaleNotes[idx];
            notes.Add(new MusicalNoteData(
                pitch.Letter, pitch.Octave, pitch.Alteration,
                durationValue, isRest: false));
        }

        var timeSig = new TimeSignatureData(4, 4);
        seq.AddBar(new BarData(notes, timeSig));
        return seq;
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static int ClampLengthWithAdvisory(int length, ExecutionContext ctx, string siteName)
    {
        if (length <= 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] length clamped — got {length} (<= 0) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return length;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:length-nonpositive:{ctx.CurrentCallSite}:{length}",
                $"[{siteName}] length {length} <= 0 at {ctx.CurrentCallSite}; "
                + "returning empty Array[Double]");
            return length;
        }
        if (length > MaxLength)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] length clamped to {MaxLength} — got {length} (> {MaxLength}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return MaxLength;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:length-cap:{ctx.CurrentCallSite}:{length}",
                $"[{siteName}] length {length} > {MaxLength} at {ctx.CurrentCallSite}; "
                + $"clamped to {MaxLength} (T-36-21 DoS guard)");
            return MaxLength;
        }
        return length;
    }

    /// <summary>
    /// Compact (letter, octave, alteration) triple — internal-only carrier
    /// used by Quantize to pre-compute scale notes once and look them up by
    /// index inside the per-value loop.
    /// </summary>
    private readonly record struct MidiPitch(char Letter, int Octave, int Alteration);
}
