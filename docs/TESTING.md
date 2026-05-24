<!-- generated-by: gsd-doc-writer -->

# Testing Flow

This document is the contributor's guide to Flow's test infrastructure. Flow
ships five distinct test layers that work together to keep the language honest:
a composer-facing pure-Flow framework for `.flow` test scripts, an xUnit C#
project for engine internals, an RMS-windowed regression helper for
audio-fidelity guarantees, a two-run determinism harness, and a set of
source-grep CI gates that enforce invariants the type system can't.

If you only need one command: `dotnet test` runs every C# test; `flow test`
runs every `.flow` script under `tests/`.

## Quick Start

```bash
# Build the full solution first.
dotnet build

# Run every xUnit test (~all C# layers).
dotnet test

# Run every pure-Flow test under tests/ (default directory).
flow test
# Or against a single file:
flow test tests/test_jam_styles.flow
# Or from the source tree without installing:
dotnet run --project flow-cli -- test tests/test_jam_styles.flow

# Two-run byte-identical determinism check on a single .flow script.
scripts/test_two_run_determinism.sh path/to/script.flow

# LSP boot/shutdown smoke (used by extension CI; also useful locally).
scripts/lsp-smoke.sh path/to/flow-lsp
```

## The Five Test Layers

| Layer | Where | What it covers |
|-------|-------|----------------|
| Pure-Flow framework | `flow-lang/StandardLibrary/TestFramework/` | Composer-facing assertions; runs `.flow` test scripts via `flow test` |
| xUnit C# project | `flow-lang.Tests/` | Engine internals: lexer, parser, interpreter, DSP, sample-cache, audit gates |
| RMS regression helper | `flow-lang.Tests/Helpers/RmsRegressionTests.cs` | Perceptual-fidelity assertions when bytes legitimately change |
| Two-run determinism harness | `scripts/test_two_run_determinism.sh` | SHA-256 byte-identical guarantee across renders |
| Source-grep CI gates | `flow-lang.Tests/{Phase36,Integration/Phase29,Integration/Phase33}/` | Invariants the type system can't express (PRNG routing, license audit, etc.) |

## Pure-Flow Test Framework

Opt in by importing the `@test` module. The framework adds one
test-registration builtin and five assertion primitives.

```flow
use "@std"
use "@improv"
use "@test"

Sequence chords = | Cmaj7 | Am7 | Dm7 | G7 |
Sequence jazz_a = (jam chords #jazz 4 "Cmajor" 42 2)
Sequence jazz_b = (jam chords #jazz 4 "Cmajor" 42 2)

(test "jam jazz pack is deterministic"
    lazy((assertNotesMatch jazz_a jazz_b)))
```

The `lazy(...)` wrap on the body is load-bearing: without it the body would
evaluate at registration time, and hermetic isolation would be meaningless.
The `test` builtin's `body` parameter is signed as `LazyType(VoidType)` to
enforce this — follow the same pattern in every test you write.

### Assertion Primitives

| Builtin | Signature | Notes |
|---------|-----------|-------|
| `(test "name" lazy(body))` | `String, Lazy[Void] → Void` | Registers a test on the engine's `TestRegistry` |
| `(assert cond)` | `Bool → Void` | Throws `AssertionException` when `cond` is false |
| `(assertEq actual expected)` | `Void, Void → Void` | Wildcard-typed pair, matches the `(equals a b)` shape |
| `(assertNotesMatch seqA seqB)` | `Sequence, Sequence → Void` | Structural `SequenceData` equality |
| `(assertBytesEqual bufA bufB)` | `Buffer, Buffer → Void` | PCM sample-for-sample equality |
| `(assertWithinDb bufA bufB tolerance)` | `Buffer, Buffer, Decibel → Void` | SPEC-8 100ms RMS-window comparison |

### Hermetic Isolation

Each test runs inside a `SnapshotState`/`RestoreState` guard implemented in
`flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs`. The snapshot
captures 11+ explicit mutable engine surfaces: global frame variables,
section registry (overload-aware), Symbol intern table, PRNG seeds, musical
context, the Phase 33 SFZ static block (`SfzEnabled`/`SfzInstruments`/
`SfzPatchRegistry`/`SfzDiagnostics`/`ResolvedSfzRoot`), Phase 39 notation-io
activation, `FlowConfig.Active`, the Phase 36 `PrngRegistry` draw-count map,
and the Phase 36 `StyleRegistry`. The list is explicit on purpose — there
is no reflection. Adding a new mutable engine surface requires adding a
field to `TestSnapshot` AND touching `ExecutionContext.SnapshotState` /
`RestoreState` so leak audits remain possible.

Live audio playback (`AudioPlaybackManager`) is intentionally NOT snapshotted.
Tests must never trigger live playback — use `writeWav` to render to disk
instead.

### Runner Behaviour

`flow test [path]` (implemented at `flow-cli/Commands/TestCommand.cs`):

- No argument → defaults to `tests/`.
- Directory argument → globs `test_*.flow` at the top level only (no
  recursion, ordinal-sorted for reproducible output).
- File argument → runs that file directly.
- Output format: `  PASS  {file}::{name}` / `  FAIL  {file}::{name}: {msg}`
  (red on TTY), plus a `Total: N; Passed: P; Failed: F` summary.
- Exit code: `0` iff every test passed AND every file parsed cleanly.

`tests/` currently contains 123 `test_*.flow` files; 35 use the `@test`
framework with assertions. The rest are legacy run-and-check-exit-code smoke
tests that pre-date Phase 35 — they remain valuable but new tests should use
the framework.

## xUnit Test Project (`flow-lang.Tests/`)

Standard `dotnet test` integration. Layout:

```
flow-lang.Tests/
  Integration/
    Phase06/  Phase07/  Phase09/  Phase14/  Phase15/  Phase18/
    Phase21/  Phase23/  Phase25/  Phase27/  Phase28/  Phase29/
    Phase30/  Phase32/  Phase33/  Phase37/  Phase39/
  Phase35/                # diagnostic-renderer tests
  Phase36/                # PRNG-gate + parameter-names tests
  Unit/                   # narrow per-type unit tests
  Helpers/                # RmsRegressionTests, WavReader, Phase29Fft, Phase37Fixtures
  Tools/                  # script-driving helpers
  Fixtures/               # .flow + WAV fixtures referenced by tests
  Shared/                 # cross-phase shared infrastructure
  baselines/
    Phase28/   Phase35/diagnostics/   Phase37/
  FlowScriptTests.cs      # runs every fixture .flow as a smoke test
  TestAssemblyInit.cs
```

### Running

```bash
# Everything.
dotnet test

# A single phase.
dotnet test --filter "FullyQualifiedName~Phase37"

# A single test class.
dotnet test --filter "FullyQualifiedName~PrngRegistryNewRandomGateTests"

# Verbose output.
dotnet test --logger "console;verbosity=detailed"
```

### Adding a New Test

1. Place the file in `flow-lang.Tests/Integration/Phase{N}/` if it's tied to
   a phase's feature, `Unit/` for narrow per-type checks, or `Phase{N}/` at
   the project root for cross-cutting gates.
2. Use the namespace `FlowLang.Tests.{Phase|Integration.Phase}{N}` (mirrors
   directory).
3. xUnit conventions: `[Fact]` for single-case, `[Theory]` + `[InlineData]`
   for parameterised. The project already references xUnit — no NuGet
   additions needed.
4. For tests that need a repo path, copy the `FindRepoRoot()` helper pattern
   from `Phase36/PrngRegistryNewRandomGateTests.cs` — it walks upward from
   the assembly location looking for a marker file.

## RMS Regression Helper

When a change legitimately alters rendered audio bytes but should preserve
perceptual fidelity (Phase 28's articulation envelope rewrite is the
canonical example), the byte-equality contract is replaced by an
RMS-windowed similarity check.

```csharp
using FlowLang.Tests.Helpers;

// AudioBuffer overload — for tests that render a fresh buffer in-memory.
RmsRegressionTests.AssertRmsWithinTolerance(
    rendered,
    "flow-lang.Tests/baselines/Phase28/ragtime_polyphony.wav");

// File-path overload — when the rendered audio is already on disk
// (e.g. a .flow script wrote it via its own writeWav call).
RmsRegressionTests.AssertWavMatchesBaseline(
    "tmp/rendered.wav",
    "flow-lang.Tests/baselines/Phase37/piano_warmth_smoke.wav");
```

### Tolerances and Overrides

The SPEC-8 locked default is **±0.5 dB over 100ms RMS windows**. Both
parameters are overridable, but a non-default `toleranceDb` requires an
`overrideReason` argument documenting why the test legitimately needs a
wider band — `ValidateOverride` throws if you omit it. The same shared
math (`RmsComparator.FirstWindowExceedingTolerance`) backs both the C#
helper and the pure-Flow `(assertWithinDb ...)` builtin, so the two
diagnostic surfaces stay in lock-step.

### Recording a New Baseline

1. Render the WAV from inside the test code path (or by running the relevant
   `.flow` script), writing to a temp file.
2. Listen to it. RMS regression catches energy drift but not all musical
   regressions — a baseline is a contract about what "good" sounds like.
3. Copy the temp file into `flow-lang.Tests/baselines/Phase{N}/`.
4. Commit the WAV. Baselines are committed because the TPDF dither RNG is
   seeded deterministically (Phase 15 Plan 05) — two writes of the same
   buffer produce byte-identical baselines, so the WAV is a stable artifact.

### Interpreting Failures

The diagnostic message is fixed:

```
RMS deviation in window N (XXXms-YYYms): expected -A dB, got -B dB
  (delta C dB exceeds tolerance 0.5 dB)
```

The window index tells you *where* in the rendered audio the divergence
starts. Common causes, in order of likelihood:

1. A DSP / synthesizer change that drifted energy. Listen to both files; if
   the new render sounds better, re-record the baseline.
2. A new envelope multiplier or articulation rule that bleeds into adjacent
   notes. Bisect by inspecting which window first fails.
3. A PRNG-routing regression that changed which seed feeds a stochastic
   primitive. Check the source-grep gates below.
4. Frame-count / sample-rate / channel-count mismatch. The helper asserts
   these first — the message will name the field, not "RMS deviation".

## Two-Run Determinism Harness

The contract: rendering the same `.flow` script twice at the same git SHA
produces byte-identical WAV output. This is preserved across Phase 18, 25,
27, 28, 29, 33, and 37 — every phase that ships PRNG-driven primitives
threads them through `Runtime/PrngRegistry` so unseeded calls are still
reproducible within a single render boundary.

```bash
scripts/test_two_run_determinism.sh tests/test_stretch_pitchshift_example.flow

# Override the render command (useful before `flow` is on PATH):
scripts/test_two_run_determinism.sh path/to/script.flow \
    --render-cmd "dotnet run --project flow-cli -- render <SCRIPT> -o <OUT>"
```

The harness extracts the first `(writeWav "path" ...)` target from the
script, renders twice into a tempdir, copies both outputs aside, and
SHA-256s them. Exit code 0 iff identical; 1 on mismatch (with both SHAs
printed); 2 on setup error.

### Investigating Failures

When two-run determinism fails on previously-passing code, the change
introduced a non-deterministic source. The usual suspects, in order:

1. A `new Random()` (wall-clock-seeded) construction outside of
   `Runtime/PrngRegistry`. Run the source-grep gate locally:
   `dotnet test --filter "PrngRegistryNewRandomGateTests"`.
2. A `DateTime.Now` / `Guid.NewGuid()` / `Environment.TickCount` read in
   a render path.
3. A dictionary iteration that depends on hash order. Use ordered
   collections (`List<KeyValuePair>` or sorted dicts) in render paths.
4. A floating-point reduction whose order depends on parallel scheduling.
   Flow's audio pipeline is sequential by design — if you added parallelism,
   gate it on a flag and default off.

## Cross-Platform Determinism Caveat

Two-run determinism on a single platform is contractual. Cross-platform
byte-identical output is NOT a contract for Flow's chaos primitives:

- `lorenz` and `logistic` (in `@generative`) are forward-Euler-integrated
  chaotic systems. After ~50 iterations, chained floating-point arithmetic
  amplifies platform-specific FPU and `Math.*` quirks exponentially. Two
  runs on the same machine (Linux x64 verified) produce byte-identical
  SHA-256 output; two runs on different platforms may not.
- Markov, L-system, and cellular-automata primitives stay cross-platform
  deterministic because they use integer arithmetic only.
- Every other Phase 36 stochastic primitive (`@patterns` `sometimes` /
  `degrade` / `sparseSeq`, and `@improv` `jam`) routes via
  `Runtime/PrngRegistry` and inherits two-run cmp-clean across platforms.

Any future cross-platform CI gate must exclude fixtures that exercise
`lorenz` or `logistic`. The on-platform two-run harness applies to them
without modification.

## Source-Grep CI Gates

These tests live in the xUnit project but enforce repo-wide invariants by
scanning source files. They are CI gates, not unit tests — adding a violation
will fail `dotnet test`.

| Gate | File | Enforces |
|------|------|----------|
| `PrngRegistryNewRandomGateTests` | `flow-lang.Tests/Phase36/` | Zero unsanctioned `new Random(` in `StandardLibrary/{Patterns,Generative,Improv}/` |
| `ParameterNamesCoverageTest` | `flow-lang.Tests/Phase36/` | Every `FunctionSignature` registered for named-arg dispatch declares `ParameterNames` |
| `LicenseAuditTests` | `flow-lang.Tests/Integration/Phase29/` | Bundled samples are CC0 / Public Domain / CC-BY 3.0 / CC-BY 4.0 only — CC-BY-SA and CC-BY-NC rejected |
| `RepoSizeTests` (Phase29 + Phase33) | `flow-lang.Tests/Integration/Phase29/`, `Integration/Phase33/` | `flow-lang/Samples/` bundle stays ≤ 5 MB |
| `HarmonicRichnessTests` | `flow-lang.Tests/Integration/Phase29/` | Synthesis-based instruments (drums, organ, wavetable) keep ≥ 20% harmonic richness |

### Adding a Sanctioned Exception

When you genuinely need an otherwise-banned construct, the gate looks for
an inline marker so the exception is documented at the point of use.

For PRNG routing, the marker is `// PRNG-SANCTIONED:` on the same line as
`new Random(`. Examples already live in `ChaosFunctions.cs` and
`JamFunctions.cs`:

```csharp
var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed REQ contract per D-36-09
```

Without the marker the line counts as an offender and the gate fails. The
marker is intentional friction: it forces the contributor to document why
this is the right exception, and it makes future audits trivial.

For the license gate, add the new sample's license file under
`flow-lang/Samples/{instrument}/LICENSE.md` matching the existing CC-BY 4.0
attribution pattern. The audit reads each `LICENSE.md` and rejects unknown
or banned license strings — no inline marker, just a real license file.

## LSP Smoke Test

`scripts/lsp-smoke.sh` boots the `flow-lsp` binary, sends framed
`initialize` + `initialized` + `shutdown` + `exit` messages over stdio, and
asserts the binary responds and exits cleanly within 15 seconds (override
via `LSP_SMOKE_TIMEOUT_SEC`). It accepts exit codes 0 or 1 — the contract
is "doesn't crash or hang", not "shutdown handlers fully wired".

```bash
# Against a freshly published binary.
dotnet publish flow-lsp -c Release -o publish/lsp
scripts/lsp-smoke.sh publish/lsp/flow-lsp
```

Used by `.github/workflows/publish-extension.yml` for per-platform CI so the
VSIX never ships an LSP binary that fails to start. Safe to run locally
against any binary the script finds on disk.

## Test Data

| Asset | Location | Constraint |
|-------|----------|------------|
| RMS regression baselines | `flow-lang.Tests/baselines/Phase28/`, `baselines/Phase37/` | Committed; deterministic dither (seed `0xD17E2`) keeps them stable |
| Diagnostic-renderer baselines | `flow-lang.Tests/baselines/Phase35/diagnostics/` | Plain-text expected error renderings |
| Bundled audio samples | `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/` | ≤ 5 MB total enforced by `RepoSizeTests`; CC0/PD/CC-BY only enforced by `LicenseAuditTests`; per-instrument `LICENSE.md` ships attribution |
| `.flow` test scripts | `tests/test_*.flow` | Top-level only; `flow test` does not recurse |
| xUnit fixtures | `flow-lang.Tests/Fixtures/`, `flow-lang.Tests/fixtures/` | Referenced by `FlowScriptTests` and per-phase integration tests |

The bundled sample directory currently sits at ~3.8 MB (21 WAVs at 44.1 kHz
16-bit mono from the CC-BY 4.0 University of Iowa MIS dataset, plus VSCO-CE
drum kit additions). Adding samples without trimming existing ones risks
the 5 MB cap — `RepoSizeTests` will fail loudly if you do.
