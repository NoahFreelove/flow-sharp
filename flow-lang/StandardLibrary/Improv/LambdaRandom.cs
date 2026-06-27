using System;
using System.Collections.Generic;
using System.Numerics;
using FlowLang.Runtime;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Improv;

/// <summary>
/// A <see cref="Random"/> whose entropy comes from a composer-supplied Flow
/// function of shape <c>(Int =&gt; Double)</c> (index → value). It is the engine
/// behind <c>jam</c>'s optional <c>rng=</c> parameter (option A — pure
/// index→value): <c>jam</c> consumes randomness only through a
/// <see cref="Random"/> (<c>NextDouble()</c> / <c>Next(int)</c>), so wrapping the
/// composer function in this subclass routes every draw through their function
/// while the jam generator itself stays byte-for-byte unchanged.
///
/// <para><b>Determinism (D-v1.5-06):</b> a PURE <c>(Int =&gt; Double)</c> keeps
/// two-run cmp-clean — the engine drives a monotonic 0-based call index, so the
/// same source yields the same draw sequence. A composer who closes over impure
/// state (wall clock, real entropy) opts out of that guarantee by their own
/// choice — analogous to a <c>live</c> block opting out.</para>
///
/// <para><b>Charitable range (CLAUDE.md ergonomics):</b> the function's return is
/// reduced into <c>[0, 1)</c> via fractional part (<c>x - floor(x)</c>), so any
/// real — a value already in range, a raw hash, a negative — becomes a usable
/// unit draw rather than throwing. Non-numeric / non-finite returns charitably
/// yield <c>0.0</c>.</para>
///
/// <para>This type constructs no <c>new Random(</c> of its own — the base
/// parameterless constructor's seed state is never consulted because every
/// sampling method is overridden — so it satisfies the PrngRegistry source-grep
/// gate without a sanctioned-marker exception.</para>
/// </summary>
internal sealed class LambdaRandom : Random
{
    private readonly FunctionOverload _fn;
    private readonly ExecutionContext _ctx;
    private int _index; // monotonic 0-based call counter handed to the composer fn

    public LambdaRandom(FunctionOverload fn, ExecutionContext ctx)
    {
        _fn = fn ?? throw new ArgumentNullException(nameof(fn));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    /// <summary>
    /// Invoke the composer function with the next call index, coerce its return
    /// charitably to a double, and reduce into <c>[0, 1)</c>. Mirrors the
    /// internal-vs-user-lambda dispatch of <c>PatternFunctions.InvokeCallback</c>.
    /// </summary>
    private double NextUnit()
    {
        var args = new List<Value> { Value.Int(_index++) };
        Value result = _fn.IsInternal
            ? _fn.Implementation!(args)
            : _ctx.Invoker!.ExecuteUserFunctionWithCaptures(
                _fn.Declaration!, args, _fn.CapturedVariables);

        double raw = result.Data switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            BigInteger bi => (double)bi,
            _ => 0.0, // non-numeric return → charitable zero
        };

        if (!double.IsFinite(raw)) return 0.0;        // NaN / ±Inf → 0
        double u = raw - Math.Floor(raw);             // map any real into [0, 1)
        // FP guard: floor of a value microscopically below an integer can round
        // the difference to exactly 1.0 — fold that (and any stray sign) back in.
        if (u < 0.0 || u >= 1.0) u = 0.0;
        return u;
    }

    // jam draws via NextDouble() and Next(int). Override the whole sampling
    // surface (including the protected Sample() the base methods may delegate to)
    // so every path routes through the composer function.
    protected override double Sample() => NextUnit();

    public override double NextDouble() => NextUnit();

    public override int Next() => (int)(NextUnit() * int.MaxValue);

    public override int Next(int maxValue)
        => maxValue <= 0 ? 0 : (int)(NextUnit() * maxValue);

    public override int Next(int minValue, int maxValue)
        => minValue >= maxValue
            ? minValue
            : minValue + (int)(NextUnit() * ((long)maxValue - minValue));
}
