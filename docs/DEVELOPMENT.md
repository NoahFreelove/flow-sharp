<!-- generated-by: gsd-doc-writer -->

# Development

This guide is for developers who want to contribute to **Flow itself** — the
language, runtime, audio pipeline, or tooling. If you want to *write Flow
scripts* (composer-side), start with [`GETTING-STARTED.md`](GETTING-STARTED.md)
instead. For the deep system tour, see [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Quick Build

Flow targets .NET 10 and builds with the standard `dotnet` toolchain.

```bash
# Clone and enter the repo
git clone https://github.com/NoahFreelove/flow-sharp.git
cd flow-sharp

# Build the entire solution (all 7 projects)
dotnet build

# Run the full xUnit suite (covers flow-lang + flow-midi)
dotnet test

# Run a single .flow script from source
dotnet run --project flow-cli -- run examples/symphony/sfz_smoke.flow

# Start the REPL
dotnet run --project flow-cli -- repl
```

Test runs are usually phase-filtered while iterating on a single area:

```bash
dotnet test --filter "FullyQualifiedName~Phase37"
dotnet test --filter "FullyQualifiedName~Phase36.PrngRegistry"
```

## Project Layout

The solution (`flow-sharp.sln`) contains these projects:

| Project | Role |
|---------|------|
| `flow-lang/` | Core library — lexer, parser, AST, interpreter, type system, runtime, stdlib, audio pipeline. All language behavior lives here. |
| `flow-cli/` | Unified `flow` CLI (Phase 30) with 13 subcommands: `run`, `render`, `play`, `repl`, `watch`, `test`, `check`, `eval`, `new`, `version`, `lsp`, `flow2midi`, `midi2flow`. |
| `flow-interpreter/` | Legacy entry point with REPL and `--watch` mode. Kept working; new features land in `flow-cli`. |
| `flow-lsp/` | Language Server Protocol 3.17 implementation over stdio (built on OmniSharp LanguageServer). Powers editor integrations. |
| `flow-midi/` | Standalone MIDI-import CLI. |
| `flow-lang.Tests/` | xUnit test project for `flow-lang`. Per-phase subfolders under `Integration/` and `Unit/`. |
| `flow-midi.Tests/` | xUnit test project for `flow-midi`. |
| `vscode-extension/` | VSCode language extension (TypeScript, `vscode-languageclient` 9.0.1). |
| `flow-jetbrains/` | JetBrains plugin (Kotlin + Gradle, LSP4IJ-based). |

For the execution pipeline (Source → Lexer → Parser → AST → Interpreter → Value)
and a stage-by-stage map of `flow-lang/` internals, read
[`ARCHITECTURE.md`](ARCHITECTURE.md). Don't re-derive it from this guide.

## C# Conventions

- **Target framework**: `.NET 10` (`net10.0`), C# 13 language features.
- **Nullable reference types**: enabled solution-wide. Treat warnings as design
  feedback — silence with `?` / `!` only when the call shape genuinely allows it.
- **File-scoped namespaces** throughout. New files should never indent under
  `namespace { ... }`.
- **Root namespaces**: `FlowLang.*` for the library, `FlowInterpreter`,
  `FlowCli`, `FlowLsp` for the entry-point projects.
- **AST nodes are `record` types** (immutable). See `flow-lang/Ast/Expressions/`
  and `flow-lang/Ast/Statements/`. New AST nodes follow the same pattern.
- **Switch expressions for node dispatch**, not the visitor pattern. The
  interpreter and lowering passes use `switch` over node type.
- **External dependency policy is strict**: see
  [`ARCHITECTURE.md`](ARCHITECTURE.md) Technology Stack section. Today the
  whole solution depends on `Melanchall.DryWetMidi` (MIDI export), `Pidgin`
  (referenced, unused — kept for migration headroom), `Tomlyn` (config TOML),
  `System.CommandLine` (CLI), and OmniSharp `LanguageServer` (LSP). Adding
  anything new needs a justification and PR discussion.

## Adding a Built-in Function

Built-ins are C# functions exposed to Flow code via the
`InternalFunctionRegistry`. The reference site is
`flow-lang/StandardLibrary/BuiltInFunctions.cs`.

**Steps:**

1. **Pick the right file.** Group by category:
   - Core / arithmetic / I/O → `StandardLibrary/StdLib.cs` (implementations)
     registered from `StandardLibrary/BuiltInFunctions.cs` (signatures).
   - Collections → `StandardLibrary/Collections.cs` and
     `StandardLibrary/Collections/DictFunctions.cs`.
   - Audio core → `StandardLibrary/Audio/AudioCore.cs`,
     `SignalGeneration.cs`, `FileIO.cs`, `PlaybackFunctions.cs`.
   - DSP effects → `StandardLibrary/Audio/DSP/{Reverb,Filter,Compressor,Delay,Granular,Stretch,PitchShift}Functions.cs`.
   - Transforms → `StandardLibrary/Transforms/TransformFunctions.cs`.
   - Harmony → `StandardLibrary/Harmony/HarmonyFunctions.cs`.
   - Generative / patterns / improv → `StandardLibrary/{Patterns,Generative,Improv}/`.

2. **Define the `FunctionSignature`.** Always include `ParameterNames` (required
   by `ParameterNamesCoverageTest` — see Test Infrastructure below):

   ```csharp
   var sig = new FunctionSignature(
       "myBuiltin",
       [IntType.Instance, DoubleType.Instance],
       ParameterNames: ["count", "amount"]);
   ```

3. **Register the implementation** via `registry.Register(name, signature, lambda)`:

   ```csharp
   registry.Register("myBuiltin", sig, args =>
   {
       int count = args[0].As<int>();
       double amount = args[1].As<double>();
       // ... charitable-interpretation logic, return Value.Foo(...)
   });
   ```

4. **Wire it in if it's a new module.** If you added a new `Register` method,
   call it from `BuiltInFunctions.RegisterAllImplementations` (line ~35) so
   `FlowEngine.cs` picks it up at startup.

5. **Add a hover doc** in `flow-lang/StandardLibrary/BuiltInDocs.cs` so the LSP
   surfaces a useful summary. Even one line is better than nothing.

6. **Add tests** in the appropriate `flow-lang.Tests/Phase{N}/` folder. Pure-Flow
   test scripts live in `tests/` at the repo root.

**Naming**: prefer camelCase, single-word when possible (`gain`, `delay`,
`reverb`). Music-domain names are first-class; technical CS names are not (no
`hashMapInsert` — use `dict` / `set`).

## Adding a Synthesizer

Synthesizers turn a `MusicalNote` + duration + BPM into an `AudioBuffer`.

1. **Create the class** under
   `flow-lang/StandardLibrary/Audio/Synthesizers/`. The reference shape is
   `PianoSynthesizer.cs` (sample-based, delegates to
   `SampledInstrumentRenderer`) or `OrganSynthesizer.cs` (pure synthesis).
   Implement `INoteSynthesizer` — minimum is a single `RenderNote(MusicalNote,
   double duration, double bpm)` method returning a buffer.

2. **Register the name** in
   `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` inside
   `SynthesizerFactory.Create` (around line 271). Add a `case` to the switch:

   ```csharp
   "myinstrument" => new MyInstrumentSynthesizer(),
   ```

   Add aliases on the same arm if the instrument has more than one common name
   (e.g., `"sax" or "saxophone"`).

3. **Route articulation correctly.** Sample-based instruments go through
   `SampledInstrumentRenderer` which already layers Phase 28's locked
   articulation envelope (`SynthUtils.GenerateArticulationADSR`). Pure-synthesis
   instruments must call that helper themselves — see `OrganSynthesizer.cs` for
   the pattern. Percussion opts out via `isPercussion: true`.

4. **Add a Phase test** under `flow-lang.Tests/Integration/Phase{N}/` that
   asserts a deterministic render against a committed baseline WAV (see RMS
   regression below).

If the new instrument is sample-based, sample files go under
`flow-lang/Samples/{instrument}/` with `LICENSE.md` attribution — only CC0 /
Public Domain / CC-BY 3.0 / CC-BY 4.0 are accepted. `LicenseAuditTests` will
fail the build otherwise.

## Adding a Stdlib `.flow` Module

Stdlib modules live at the root of `flow-lang/` as bare `.flow` files. They are
loaded via `use "@name"` (the `@` prefix routes through `ModuleLoader.cs` to the
stdlib directory; bare paths resolve relative to the caller).

1. **Create the file** at `flow-lang/yourmodule.flow`. Follow the pattern in
   `flow-lang/std.flow` — `internal proc` declarations name the C#-backed
   builtins; pure-Flow procs can be defined inline.

2. **Register the file as a build output** in `flow-lang/flow-lang.csproj`.
   Every `.flow` file in `flow-lang/` needs an explicit `<None Update="...">`
   block with `CopyToOutputDirectory` and `CopyToPublishDirectory` set to
   `PreserveNewest`. Without this, the file won't ship with `dotnet publish`
   and the publish smoke test in `scripts/publish.sh` will fail.

3. **Compose with `@std`** if your module needs collection / arithmetic
   helpers. `@std` itself imports `@collections` and `@bars`.

4. **Document the surface** in `CLAUDE.md`'s "Standard Library Modules"
   section and add a runnable example under `examples/`.

5. **Add tests.** Both xUnit (`flow-lang.Tests/Phase{N}/`) and a Flow-side
   script (`tests/test_yourmodule.flow`) are typical.

## Test Infrastructure

Flow has four layers of testing. All four run in CI and are expected to stay
green on `dev`.

### Pure-Flow test framework (`@test`)

Phase 35 TEST-01 added a Flow-side test framework. Composers write:

```flow
use "@test"

(test "two plus two is four" lazy((assertEq 4 (add 2 2))))
(test "audio identity"        lazy((assertWithinDb buf buf 0.5dB)))
```

Run via `flow test [path]`. Surface: `test(name, lazy body)`, `assert`,
`assertEq`, `assertNotesMatch`, `assertBytesEqual`, `assertWithinDb`. The
runner snapshot/restore-isolates each test for hermetic execution.
Implementation: `flow-lang/StandardLibrary/TestFramework/`.

### xUnit (`flow-lang.Tests/`)

Per-phase folders for everything that needs C# access:
`Integration/Phase06/` ... `Integration/Phase39/` for cross-cutting feature
tests, `Unit/Phase{N}/` for unit-style asserts on individual classes. Run with
`dotnet test`. Phase-filtering with `--filter "FullyQualifiedName~Phase37"` is
the usual iteration loop.

### RMS-windowed regression

For audio output that legitimately changes bytes but should preserve
*perceptual* fidelity, use the RMS comparator instead of byte equality:

```csharp
using FlowLang.Tests.Helpers;

// AudioBuffer overload — round-trips through WriteWav for dither parity:
RmsRegressionTests.AssertRmsWithinTolerance(rendered, "baselines/Phase37/foo.wav");

// File-path overload — both paths already through dither:
RmsRegressionTests.AssertWavMatchesBaseline(actualWavPath, baselineWavPath);
```

SPEC-8 locks the default tolerance at ±0.5 dB / 100 ms windows. Per-test
overrides require `overrideReason` (compile-time error otherwise). Baselines
live under `flow-lang.Tests/baselines/Phase{28,35,37}/` and are committed
(deterministic dither RNG makes them stable).

### Two-run determinism harness

The "two-run cmp-clean" contract: rendering the same script twice in the same
process produces byte-identical WAV output. The harness:

```bash
scripts/test_two_run_determinism.sh path/to/script.flow
# Or against a non-installed build:
scripts/test_two_run_determinism.sh examples/foo.flow \
  --render-cmd "dotnet run --project flow-cli -- render <SCRIPT> -o <OUT>"
```

The script renders twice, captures both WAVs, and `sha256sum`s them. Phase 36+
generative-primitive plans wire this into their verification gates. Note:
Phase 36 chaos primitives (`lorenz`, `logistic`) are deterministic
**same-platform only** — see CLAUDE.md Conventions for the cross-platform
caveat.

### Source-grep CI gates

Three tests guard project-wide invariants by scanning source files:

| Test | File | Enforces |
|------|------|----------|
| `PrngRegistryNewRandomGateTests` | `flow-lang.Tests/Phase36/` | Zero unsanctioned `new Random(` in `StandardLibrary/{Patterns,Generative,Improv}/`. Exceptions carry `// PRNG-SANCTIONED:`. |
| `ParameterNamesCoverageTest` | `flow-lang.Tests/Phase36/` | Every non-varargs `registry.Register(` declares `ParameterNames: [...]` so universal named-arg calls work. |
| `LicenseAuditTests` | `flow-lang.Tests/Integration/Phase29/` | Every bundled sample file has a CC0 / Public Domain / CC-BY 3.0 / CC-BY 4.0 license. |

If you add a new stdlib file under `StandardLibrary/{Patterns,Generative,Improv}/`,
extend `PrngRegistryNewRandomGateTests`. If you add a new stdlib source file
covered by named-args, extend `ParameterNamesCoverageTest`'s `[InlineData]`
roster.

## Publishing Locally

The release build is self-contained Linux x64, single-file. Use the wrapper:

```bash
bash scripts/publish.sh
```

Output: `publish/flow-linux-x64/flow` (executable) plus the stdlib `.flow` files
copied alongside.

The publish flag set is **locked** — `scripts/publish.sh` and
`flow-cli/Properties/PublishProfiles/linux-x64.pubxml` mirror it:

| Flag | Why |
|------|-----|
| `-c Release` | Optimized build. |
| `-r linux-x64` | Target runtime. |
| `--self-contained true` | Ship the .NET runtime — composers don't need a separate install. |
| `-p:PublishSingleFile=true` | One `flow` binary, not a folder of DLLs. |
| `-p:PublishTrimmed=false` | Trimming **would break OmniSharp LSP reflection**. Do not flip this. |
| `-p:DebugType=embedded` | Stack traces stay useful without a separate `.pdb`. |
| `-p:IncludeNativeLibrariesForSelfExtract=true` | Required for single-file + native deps. |
| `-p:EnableCompressionInSingleFile=true` | Stays under the SPEC-2 120 MB budget. |

The script enforces the 120 MB cap with `du -sb` post-publish, verifies the six
core stdlib files (`std.flow`, `collections.flow`, `audio.flow`, `bars.flow`,
`notation.flow`, `composition.flow`) are present, then runs `./flow version`
as a smoke test. Exit code 1 on any failure.

If you add a new stdlib `.flow` file, update the verification loop in
`scripts/publish.sh` and add a `<None Update>` block to
`flow-lang/flow-lang.csproj` — see "Adding a Stdlib `.flow` Module" above.

## GSD Workflow

This project uses the **GSD workflow** for any change beyond a one-line
typo fix. From `CLAUDE.md`:

> Before using Edit, Write, or other file-changing tools, start work through a
> GSD command so planning artifacts and execution context stay in sync.

Entry points:

- `/gsd:quick` — small fixes, doc updates, ad-hoc tasks.
- `/gsd:debug` — investigation and bug fixing.
- `/gsd:execute-phase` — planned phase work (the canonical path for new
  features). Phase folders under `.planning/phases/` capture context, decisions,
  research, plans, and verification per phase.

Direct repo edits outside a GSD workflow are reserved for the rare cases the
user explicitly asks to bypass it. Contributors should route through
`/gsd:quick` even for what feels like a one-shot edit — it's how the project
keeps `CLAUDE.md`, `ROADMAP.md`, and the phase artifacts truthful.

## Code Review Norms

Reviewers (and the GSD planner) check for these:

- **GSD workflow used.** Non-trivial PRs should reference the phase folder or
  the GSD command that produced them.
- **No new external dependencies without justification.** The guiding principle
  is *minimal dependencies* (see CLAUDE.md Technology Stack). Hand-roll first;
  reach for a NuGet package only when the alternative is a serious correctness
  risk (the bar that admitted DryWetMidi).
- **Charitable interpretation preserved.** Stdlib code should accept degenerate
  inputs (empty seqs, NaN parameters, unknown symbols) by returning a sensible
  default and emitting a one-shot stderr advisory — *never* throwing into
  composer-facing code paths. See `feedback_charitable_interpretation.md` in
  user memory and Pattern E across plan files.
- **Two-run cmp-clean determinism preserved.** New stochastic primitives MUST
  route through `Runtime/PrngRegistry` keyed by `(SourceLocation,
  generator-name)`. `PrngRegistryNewRandomGateTests` will fail otherwise.
- **Sample bundle additions are correctly licensed.** Only CC0 / Public Domain
  / CC-BY 3.0 / CC-BY 4.0. `LicenseAuditTests` enforces this; per-file
  `LICENSE.md` attribution is mandatory for CC-BY material.
- **Tests added in the right phase folder.** `flow-lang.Tests/Phase{N}/` for
  the phase the change belongs to. Cross-cutting changes land in
  `Integration/Phase{N}/`; per-class unit tests under `Unit/`.
- **Pre-traction breaking changes OK.** Until Flow accrues external composers,
  breaking syntax / builtin changes still land in single commits with in-repo
  migrators. See `project_pre_public_no_legacy_burden.md` in user memory for
  the revisit triggers.

For everything else, read [`ARCHITECTURE.md`](ARCHITECTURE.md) and
[`CLAUDE.md`](../CLAUDE.md) — the agent-shaped deep dive — before opening
a PR that touches the language core, the audio pipeline, or the runtime.
