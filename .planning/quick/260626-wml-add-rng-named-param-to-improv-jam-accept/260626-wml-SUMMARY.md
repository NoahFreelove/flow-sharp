---
quick_id: 260626-wml
slug: add-rng-named-param-to-improv-jam-accept
status: complete
date: 2026-06-27
---

# Summary: `rng=` custom random function for `@improv` `jam`

## What shipped
`jam` now accepts an optional `rng=` named parameter — a composer-supplied Flow
function of shape `(Int => Double)` (index → value in `[0,1)`). When supplied it
drives every random draw inside jam, taking precedence over `seed=` and the
PrngRegistry. Functions are first-class, so a composer can now plug in any custom
RNG (hash-based, low-discrepancy, biased distribution, …) — option A, jam-only.

```flow
use "@improv"
Sequence solo = (jam over=changes rng=(fn Int i => (myHash i)))
```

## How
- **`flow-lang/StandardLibrary/Improv/LambdaRandom.cs`** (new) — `internal sealed
  class LambdaRandom : System.Random`. Drives a monotonic 0-based call index,
  invokes the composer function (`fn(index++)`) via
  `ExecutionContext.Invoker.ExecuteUserFunctionWithCaptures` (the same mechanism
  `@patterns` uses), charitably coerces the return to `double`
  (double/float/int/long/BigInteger; else 0) and folds it into `[0,1)` via
  `x - floor(x)` (NaN/±Inf → 0). Overrides `Sample`/`NextDouble`/`Next()`/
  `Next(int)`/`Next(int,int)`. Contains no `new Random(` — passes the PRNG gate.
- **`JamFunctions.cs`** — added a 7th `rng` param (`FunctionType`, default
  `Value.Void()` = not provided); parse `args[6]` into `FunctionOverload? rngFn`;
  at the single RNG funnel the precedence is now `rngFn → new LambdaRandom` else
  `seed → new Random(seed)` else `PrngRegistry`. Uses `new LambdaRandom(` (not
  `new Random(`), so the `JamDeterminismTests` cap of one `new Random(` holds.
- **`improv.flow`** — extended the `jam` internal-proc surface with `Function: rng`
  + doc notes so the param is reachable by composers.

## Verification
- New `flow-lang.Tests/Phase36/JamCustomRngTests.cs` — **5/5 pass**: different
  rngs ⇒ different music (proves it drives the draws), same pure rng ⇒ identical
  (two-run determinism), rng beats seed, the `rng=` named surface resolves, and
  omitting it leaves the seed/PrngRegistry path intact.
- Regression gates green (16/16): `JamDeterminismTests` (incl. the `new Random(`
  cap), `PrngRegistryNewRandomGateTests`, `JamFunctionsTests`.
- `dotnet build flow-lang` clean on Desktop **and** `-p:FlowTarget=Web`.

## Out of scope / follow-ups
- `@generative` primitives (markov/lsystem/cellular/lorenz/…) still seed-only;
  extending the same `rng=` to them is a natural follow-on (they share the
  PrngRegistry funnel).
- An impure `(Int => Double)` (closing over wall-clock/entropy) opts out of
  two-run cmp-clean by the composer's own choice — no advisory is emitted for it.
