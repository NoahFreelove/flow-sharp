---
quick_id: 260626-wml
slug: add-rng-named-param-to-improv-jam-accept
status: planned
date: 2026-06-27
---

# Quick Task: `rng=` custom random function for `@improv` `jam`

## Goal
Let a composer plug their own randomness into `jam` by passing a first-class Flow
function of shape `(Int => Double)` (index → value in `[0,1)`) via a new optional
`rng=` named parameter. Option A (pure index→value), **jam-only**. Existing `jam`
behavior stays byte-identical when `rng=` is omitted.

## Why this is small
Every random draw in `jam` funnels through one `System.Random` object
(`NextDouble()` / `Next(int)` in `PickNote`). Swapping that object for a
function-backed one needs no change to the generator. Flow already invokes
first-class functions from builtins via `ExecutionContext.Invoker.ExecuteUserFunctionWithCaptures`
(used by `@patterns`).

## Tasks

1. **`flow-lang/StandardLibrary/Improv/LambdaRandom.cs`** (new) — `internal sealed
   class LambdaRandom : System.Random`. Holds the composer `FunctionOverload` + the
   `ExecutionContext`, plus a monotonic 0-based call index. `NextUnit()` invokes
   `fn(index++)` (internal-impl or user-lambda path, mirroring `PatternFunctions.InvokeCallback`),
   charitably coerces the return to `double` (double/float/int/long/BigInteger; else 0),
   and reduces into `[0,1)` via `x - floor(x)` (NaN/±Inf → 0). Overrides
   `Sample()`, `NextDouble()`, `Next()`, `Next(int)`, `Next(int,int)` to all route
   through `NextUnit()`. Contains **no** `new Random(` text (passes the PRNG gate).

2. **`flow-lang/StandardLibrary/Improv/JamFunctions.cs`** — add a 7th param `rng`
   (`FunctionType.Instance`, name `"rng"`, default `Value.Void()` = not provided)
   to the signature; parse `args[6]` into `FunctionOverload? rngFn` in `Jam()`;
   thread it into `GenerateJam`; at the RNG funnel choose precedence:
   `rngFn != null → new LambdaRandom(rngFn, ctx)` ; else explicit `seed → new Random(seed)` ;
   else `PrngRegistry`. Uses `new LambdaRandom(` (not `new Random(`), so the
   `JamDeterminismTests` cap of 1 `new Random(` hit is preserved.

3. **`flow-lang/improv.flow`** — extend the `jam` internal-proc surface with
   `Function: rng` so the param is reachable by composers.

4. **`flow-lang.Tests/Phase36/JamCustomRngTests.cs`** (new) — prove the custom rng
   is consumed: two different pure rngs (`fn Int i => 0.05` vs `fn Int i => 0.95`)
   produce **different** sequences; the same rng run twice produces **identical**
   sequences (determinism); and rng= omitted still works.

## Verification
- `dotnet build flow-lang` clean (Desktop) — and `-p:FlowTarget=Web` stays green
  (improv is not stripped on Web).
- New `JamCustomRngTests` pass; existing `JamDeterminismTests` (incl. the
  `new Random(` cap) and `PrngRegistryNewRandomGateTests` stay green.

## Out of scope
The `@generative` primitives (markov/lsystem/cellular/lorenz/…). Same `rng=`
extension to them is a possible follow-on; not done here.
