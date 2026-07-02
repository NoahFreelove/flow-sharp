# Strict Mode

Flow is **charitable by default** — degenerate inputs get clamped to a sensible range or fall back to a safe default with a one-shot advisory, and numeric types coerce freely (a `Double` where a `Float` is expected Just Works). That is the right default for music, where a wrong note is better than a crash.

Sometimes you want the opposite: a hard perimeter that rejects sloppy input instead of quietly fixing it — for library code, for input parsing, or just to catch mistakes early. That is what `enable strict;` gives you.

```flow
enable strict;
use "@std"

Note: the rest of this file is now strict
```

`enable strict;` is a **file-scope pragma** — it must appear at the top of the file, and it hardens **only the declaring file**. Charitable behavior stays the default everywhere else in your program.

## What Strict Mode Changes

Strict mode hardens three things:

### 1. No implicit coercion

The overload resolver normally has three tiers — exact match, compatible match, and *convertible* match (the widening chain `Int → Long → Float → Double → Number`). Strict mode **drops the convertible tier**. A call that only matched by widening now fails to resolve. You must pass the exact type or convert explicitly.

### 2. Clamp and advisory sites become errors

Charitable stdlib functions clamp out-of-range inputs (Markov order to `[1, 3]`, L-system iterations to `[0, 20]`, bundle depth to 8, and roughly 115 other sites) and emit a `[prefix]` advisory. Under strict mode those sites raise a `[strict]` error instead. If you feed a Markov chain an order of 5, charitable Flow clamps to 3 and warns; strict Flow stops.

### 3. Bool-required logic and same-type comparisons

`if`, `and`, and `or` require actual `Bool` values (no truthy coercion), and comparisons require both sides to be the same type. This catches the classic "compared a String to an Int and got a surprising answer" bug.

Strict mode is captured **per proc at its declaring file**. A proc defined in a strict file stays strict even when called from a charitable file, and vice versa — strictness does not propagate across `use` boundaries.

## Explicit Conversion Builtins

Because strict mode turns off implicit widening and coercion, Flow ships explicit conversion builtins so you can be deliberate. These work in charitable mode too — they are just how you say "yes, I mean this type."

**Number → music type** (`db`, `hz`, `ms`, `sec`, `cents`, `semitones`):

```flow
use "@std"

Decibel   d  = (db -6)         Note: -6 dB from a bare Int
Hertz     f  = (hz 440)        Note: 440 Hz
Millisecond a = (ms 50)        Note: 50 ms
Second    s  = (sec 2)         Note: 2 s
Cent      c  = (cents 50)      Note: +50 cents
Semitone  st = (semitones 2)   Note: +2 semitones
```

Each is idempotent — passing an already-typed value (e.g. `(db -6dB)`) returns it unchanged.

**Music type → number** (`double`, `float`, `int`, `long`) — extract the underlying scalar from any of the six music types (`Decibel`, `Hertz`, `Cent`, `Millisecond`, `Second`, `Semitone`):

```flow
Double hzValue = (double 440Hz)    Note: 440.0
Int    stValue = (int +2st)        Note: 2
```

## REPL Toggle

The REPL exposes strict mode as a session flag so you can experiment without editing a file:

```
flow> :strict on
flow> :strict off
```

`:strict on` flips `StrictMode` for the session; `:strict off` restores charitable behavior. This is the interactive equivalent of `enable strict;` at the top of a script.

## Philosophy

Strict mode is an **opt-in input perimeter**, not a global mode switch. The design intent (see [Design Philosophy](Design-Philosophy.md)):

- **Charitable stays the default.** Most composers never touch strict mode, and nothing they write changes.
- **Strict is a per-file choice.** You harden the boundary where untrusted or fiddly input enters — a parser, a config loader, a shared library — and leave the rest of your program charitable.
- **No propagation.** Importing a strict module does not make your file strict, and importing a charitable module into a strict file does not soften it. Each file (and each proc, at its declaring site) owns its own posture.

## See Also

- [Language Basics](Language-Basics.md) — Types, the numeric widening chain, pragmas
- [Functions](Functions.md) — Overload resolution and specificity scoring
- [Tips and Tricks](Tips-and-Tricks.md) — Charitable interpretation in practice
- [Design Philosophy](Design-Philosophy.md) — Why charitable is the default and strict is the perimeter
