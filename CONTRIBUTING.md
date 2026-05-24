<!-- generated-by: gsd-doc-writer -->
# Contributing to Flow

Thanks for your interest in Flow. Flow is an interpreted, statically-typed language for music production — a place where composers, producers, and creative coders can write musical ideas as code and hear them immediately. Good contributions usually fall into one of four buckets: a bug report with a minimal `.flow` reproducer, a new musical primitive that opens up a genre or technique that wasn't expressible before, a DSP / synthesis improvement that makes existing output sound better, or documentation that helps the next composer get unstuck faster.

This guide tells you how to do any of those things without surprising the maintainer.

---

## Project Philosophy

Read these. They're the lens every PR is reviewed through, and the canonical phrasing lives in [`CLAUDE.md`](CLAUDE.md) under "Goals & Non-Goals".

- **Composer ergonomics first.** Music production is historically slow and demands beefy computers. Flow prioritizes composer ergonomics over runtime efficiency, type strictness, and generality. When in doubt, pick the lower-friction option for the person writing the score — even when it costs implementation complexity. Easy cases should be fast; flexible cases should be flexible.
- **Genre-agnostic.** Flow lets you write a classical symphony, an EDM track, jazzy blues, modern pop, and death metal in one place. No genre is privileged in the design. Features whose only justification is "this is how my favorite genre does it" usually get rephrased into a more general primitive before landing.
- **Charitable interpretation.** Degenerate inputs (zero factor, NaN offset, empty sequence, unknown chord quality) return a reasonable default plus a one-shot stderr advisory — never an exception on composer-facing surfaces. Music keeps playing; the composer sees the advisory and decides whether to fix it. See `feedback_charitable_interpretation.md` in user memory for the canonical statement.
- **Minimal external dependencies.** The current allowlist is small and intentional: `Melanchall.DryWetMidi` (MIDI export), `Tomlyn` (config), `System.CommandLine`, `OmniSharp LanguageServer`, and `Pidgin` (legacy, unused by the active parser). Hand-rolling DSP, audio backends, samplers, and parsers is the norm — see `CLAUDE.md` § "Technology Stack" for the rationale on each rejection (NAudio, NWaves, managed-midi, etc.).
- **Pre-traction no-deprecation latitude.** Flow shipped publicly at v1.4 but has no external traction yet. Until that changes, breaking syntax / builtin changes still land in single commits, in-repo migrators only, no `flow migrate` CLI subcommand required. See `project_pre_public_no_legacy_burden.md` for the revisit triggers.

---

## Code of Conduct

Be kind. Assume the person on the other side of the issue or PR is doing their best with imperfect information, and write your messages as if they were going to read them on the worst day of their week. Disagreement about technical direction is welcome; personal attacks, harassment, and bad-faith argument are not. If something feels off, email the maintainer (`noahfreelove@gmail.com`) rather than escalating in public.

---

## Reporting Bugs

Open an issue at [https://github.com/NoahFreelove/flow-sharp/issues](https://github.com/NoahFreelove/flow-sharp/issues). Include:

1. **A minimal `.flow` reproducer** — the smallest script that triggers the bug. If it needs a WAV or SFZ input, link or attach it.
2. **Expected vs. actual behavior** — what you thought would happen, what actually happened, and any stderr advisories you saw.
3. **Environment** — OS, .NET SDK version (`dotnet --version`), and PulseAudio version (`pulseaudio --version`) if the bug is audio-related.
4. **The Flow git SHA** you're on (`git rev-parse HEAD`).

**Composer ergonomics bugs are HIGH priority.** If you hit an "it should have just worked" moment — a piece of syntax that did the wrong thing, an error message that didn't tell you the fix, a builtin that demanded an awkward call shape — please file it. Those reports shape the language more than feature requests do.

---

## Proposing Features

Open an issue with a `[Feature]` prefix in the title.

**Describe the musical use case first, the technical proposal second.** A proposal that opens with "I want to write a four-on-the-floor kick with sidechain ducking on the bass" gets traction faster than one that opens with "we should add a `SidechainCompressorState` AST node."

Two things to know before you propose:

- **Genre-non-agnostic features usually get rejected.** If the proposal only helps one genre, the maintainer will ask you to generalize it. ("Add a trap hi-hat roll" → "expose a probabilistic note-repeat combinator that composes with existing transforms.")
- **Non-musical features are out of scope.** Flow can compute, but that's not what it's for. Don't bend the language to serve general-purpose programming use cases — see "Non-Goals" in `CLAUDE.md`.

---

## Submitting a Pull Request

The basic loop:

1. **Fork** the repo on GitHub.
2. **Branch** off `dev` with a descriptive name (`feat/granular-pitch-knob`, `fix/flute-d5-crossover`, etc.).
3. **Make your change.** Keep commits focused; prefer multiple small commits over one large one.
4. **Test locally:**
   ```bash
   dotnet build
   dotnet test
   dotnet run --project flow-interpreter examples/tutorial.flow
   ```
   All tests must pass and the tutorial smoke must produce its expected WAV output.
5. **Push** your branch and **open a PR** against `dev`.

### Source-Grep Gates That Will Catch Bad PRs

The test suite enforces a handful of cross-cutting rules via source-grep gates. CI runs all of them. The common ones:

- **`PrngRegistryNewRandomGateTests`** — any new stochastic primitive in `flow-lang/StandardLibrary/{Patterns,Generative,Improv}/` MUST route through `Runtime/PrngRegistry`. Direct `new Random(...)` calls in those directories fail the gate. Documented explicit-seed exceptions carry a `// PRNG-SANCTIONED:` comment marker. This preserves the two-run cmp-clean determinism contract.
- **`ParameterNamesCoverageTest`** — new builtins must populate `FunctionSignature.ParameterNames` so named-argument dispatch works (`(reverb buf size=0.8 mix=0.4)`). Signatures with missing parameter names fail the gate.
- **`LicenseAuditTests`** — any new bundled sample under `flow-lang/Samples/` must be CC0, Public Domain, CC-BY 3.0, or CC-BY 4.0. CC-BY-SA and CC-BY-NC are rejected. Per-instrument `LICENSE.md` files are required.
- **`RepoSizeTests`** — the bundled sample directory has a 5 MB cap. If you're adding new samples, prune existing ones to stay under it.

If your PR fails one of these, the failure message will name the gate and the offending file — fix it before re-requesting review.

### Sample Bundle Rules

If your PR ships audio samples:

- License must be CC0 / Public Domain / CC-BY 3.0 / CC-BY 4.0.
- Add a per-instrument `LICENSE.md` and update `flow-lang/Samples/CREDITS.md`.
- Keep the bundle total under 5 MB (44.1 kHz 16-bit mono is the sweet spot).
- Prefer single-velocity-layer mezzo-forte samples unless the instrument genuinely needs multi-layer crossfade (Piano is the only current exception, with 4 layers).

---

## Code Style

- **C# (.NET 10):** nullable reference types enabled, file-scoped namespaces, AST nodes as immutable `record` types, pattern matching (`switch` expressions) for node dispatch instead of the visitor pattern. All namespaces under `FlowLang.*` (library) or `FlowInterpreter` (console app).
- **`.flow` stdlib modules:** match the existing style in `flow-lang/*.flow` — prefix-only S-expression call syntax, no infix operators, charitable defaults at the call boundary.
- **Tests:** new `.flow` integration tests go under `tests/` (run via `dotnet run --project flow-interpreter tests/test_*.flow`); new C# unit / regression tests go under `flow-lang.Tests/`.

The how-to-add-a-builtin and how-to-add-a-synthesizer walkthroughs live in `docs/DEVELOPMENT.md` (generated by the doc workflow alongside this file). Read those before touching `StandardLibrary/`.

---

## Commit Messages

Loose conventional-commits style is preferred but not strictly enforced. Look at recent `git log --oneline` to match the in-house tone:

```
feat(39-04): MML import (MML-01)
fix(37): close PIANO-01 verification gap — lift Phase 29 cosSim ceiling
docs(phase-37): evolve PROJECT.md to reflect Phase 37 completion
chore: merge phase-39-notation — Notation Citizenship complete
```

Prefixes you'll see: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`. Parenthetical scopes (like `(39-04)` or `(phase-37)`) reference internal plan IDs and are optional for external contributions.

---

## A Note on the GSD Workflow

Internally, work on Flow is routed through the **Get Shit Done (GSD)** workflow — a planning / execution / verification harness that keeps phase work, ad-hoc fixes, and debugging sessions in sync with the documentation. You'll see commands like `/gsd:quick`, `/gsd:debug`, and `/gsd:execute-phase` referenced in `CLAUDE.md`.

**You don't need to install or use GSD to contribute.** Open issues and PRs against the repo as you normally would. The maintainer routes incoming contributions through GSD on the receiving side, which means the planning artifacts and execution context stay consistent without anything being demanded of you.

---

## First-Time Contributor Pointers

If you've never opened Flow before, this is the suggested reading order:

1. **`CLAUDE.md`** — sections "What This Is", "Goals & Non-Goals", and "Project Structure". This is the project's single source of truth.
2. **`docs/ARCHITECTURE.md`** — the deep dive on the execution pipeline, type system, and module layout.
3. **`docs/DEVELOPMENT.md`** — how to add a builtin, add a synthesizer, and run the test suite.
4. **`FEATURES.md`** — a skim of what's already shipped so you don't propose something that exists.
5. **`wiki/`** — 26 user-facing tutorial chapters. If you're a composer trying to *learn* Flow rather than contribute to it, start here instead.

And one strong recommendation: **write a `.flow` script before you write any C#.** Render a short piece (16 bars is plenty), export it to WAV, listen to it. That hour will tell you more about what Flow is for than any amount of source reading.

---

## License

Flow is licensed under the **GNU General Public License v3.0** (see [`LICENSE`](LICENSE) at the project root).

**What that means for you as a contributor:** by submitting a pull request, you agree that your contribution is licensed under GPL v3.0 as well. You retain copyright over your contribution; you're granting the project (and anyone who uses or redistributes it) the rights GPL v3.0 confers. If you're contributing on behalf of an employer, make sure your employer is OK with that before opening the PR.

GPL v3.0 is copyleft: anyone who redistributes Flow (or a modified version) must do so under GPL v3.0. That suits Flow's "tool for composers, not a foundation for proprietary platforms" stance.

---

## Where to Ask Questions

- **Bug reports and feature proposals:** [GitHub Issues](https://github.com/NoahFreelove/flow-sharp/issues)
- **PR feedback:** comment on the PR itself
- **Private / security concerns:** email the maintainer at `noahfreelove@gmail.com`

There's no Discord, Slack, or mailing list yet — GitHub is the one source of truth. If Flow picks up enough traction to need synchronous channels, that decision will be made in the open via an issue.

Thanks for reading this far. Now go make some music.
