# Scala Microtonal Tunings — Tutorial

A small composer-facing tutorial for Phase 32's Scala (`.scl`) tuning loader.

Flow ships three built-in named tunings — `justIntonation`, `pythagorean`,
and `equalTemperament` (the default). Phase 32 extends that to ~5300
community-curated tunings in the [Huygens-Fokker Scala archive](http://www.huygens-fokker.org/scala/)
via `(loadScala "path.scl")` and the new `tuning t { ... }` musical-context block.

## Run

```bash
dotnet run --project flow-interpreter examples/scala/intro.flow
```

After the run, listen to `/tmp/p32_intro.wav`. The four short sections each
use a distinct active tuning system, so they sound audibly distinct from
one another:

1. **Section a** — Partch 43-tone pure scale (loaded by name)
2. **Section b** — Wendy Carlos' Alpha (a non-octave-repeating scale; string-literal sugar)
3. **Section c** — 5-limit just intonation (the file-scope `enable justIntonation;` pragma)
4. **Section d** — Partch again (last-wins: the inner block wins over the JI pragma)

## What the tutorial demonstrates

- All three composer surface forms for the `tuning { ... }` block per D-15:
  identifier-bound `Tuning` variable, inline `(loadScala "...")` call, and
  string-literal sugar.
- The last-wins interaction between file-scope `enable justIntonation;` and
  block-scope `tuning partch { ... }` (SPEC-6 acceptance shape).
- The `(str t)` D-04 description format `Tuning("<desc>", N steps, period X.XX¢)`.

## Attribution

The `.scl` fixtures referenced by this tutorial live in
[`flow-lang.Tests/fixtures/scala/`](../../flow-lang.Tests/fixtures/scala/);
see [`flow-lang.Tests/fixtures/scala/LICENSE.md`](../../flow-lang.Tests/fixtures/scala/LICENSE.md)
for Huygens-Fokker Foundation credits.

## Reference

The `tuning { ... }` block and the `(loadScala "...")` builtin are documented
in the project [`CLAUDE.md`](../../CLAUDE.md) under
"Music Types Quick Reference" and "Music-Specific Language Features".
