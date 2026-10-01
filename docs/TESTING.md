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

## Generated audit and bundle reports

Normal `ClampGrepConsistencyTests` and `BundleSizeBudgetTests` runs write
reports under the system temporary directory, in `flow-test-reports/<unique-id>/`.
Each report directory is printed in test output and retained for inspection.
Set `FLOW_TEST_ARTIFACTS_DIR` to collect these reports elsewhere; relative paths
are resolved from the test process working directory. TRX output is controlled
separately by `--results-directory`.

```bash
FLOW_TEST_ARTIFACTS_DIR=/tmp/flow-reports dotnet test flow-lang.Tests/flow-lang.Tests.csproj \
  --filter 'FullyQualifiedName~ClampGrepConsistencyTests|FullyQualifiedName~BundleSizeBudgetTests' \
  --logger trx --results-directory /tmp/flow-test-results
```

Updating the historical reports under `.planning/phases/42-type-system-stdlib-audit/`
and `.planning/phases/48-wasm-runtime-webaudio-backend/` requires an explicit command:

```bash
FLOW_UPDATE_REPORTS=1 dotnet test flow-lang.Tests/flow-lang.Tests.csproj \
  --filter 'FullyQualifiedName~ClampGrepConsistencyTests|FullyQualifiedName~BundleSizeBudgetTests'
```

Only the exact value `1` enables updates, and it takes precedence over the
artifact-directory setting. Review the generated diff before committing it.
This option controls these reports only, not audio or diagnostic golden baselines.
Bundle tests require the `wasm-tools` workload and publish the Web target;
do not run another build/publish against the same checkout simultaneously.
The standalone audit scripts retain their existing defaults; pass `--out-dir`
when invoking them directly to write outside the historical report directory.

## Language contract and dependency ratchet

`contracts/language/` holds the executable language contract: short programs
with expected stdout (`.out`), error counts, stderr fragments, a rationale and a
compatibility status (`preserve`, `generous`, `disputed`, `defect`, `gap`).
`examples/language/` holds non-musical tutorial programs with expected output.
`LanguageContractTests` runs both in the core tier; `LanguageHostTests` also runs
all seven examples in fresh language-only processes and checks output, artifact and
loaded/native dependency closure, embedding and line-REPL behavior; see
[the contract README](../contracts/language/README.md) for the format.

```bash
dotnet test flow-lang.Tests --filter "FullyQualifiedName~FlowLang.Tests.Contracts"
```

A failing contract means observable behavior changed. Either revert the change
or record the new semantics deliberately: update the `.out`/header, and for
`preserve` entries add a decision under `docs/decisions/`. Never regenerate
`.out` files wholesale.

`DependencyDirectionTests` pins every language-core → music/audio/platform
namespace edge in `flow-lang.dll`
(`docs/baselines/phase1/language-dependency-edges.json`). New edges fail. When an
extraction removes edges, regenerate with
`FLOW_UPDATE_DEPENDENCY_BASELINE=1` and review the shrinking diff.

## Characterization snapshots

`flow-lang.Tests/Characterization/` pins current behavior in the areas the
language/music extraction touches. Each test compares generated text with a file
under `Characterization/Snapshots/`:

| Test | Snapshot | Pins |
| --- | --- | --- |
| `ParserCharacterizationTests` | `ast/` | Canonical, location-free AST of every tracked `.flow` file (hash manifest, plus full dumps for contracts and examples). Types print by name. |
| `TypeSystemCharacterizationTests` | `types/` | Type relations (compatible/convertible/equal, specificity) and the overload each builtin call resolves to when each argument varies over 33 representative types, charitable and strict. |
| `ValueCharacterizationTests` | `values/` | Formatting, equality and hashing, `Value.ConvertTo`, and member access, including the reflection fallback that exposes CLR properties. |
| `ModuleSurfaceCharacterizationTests` | `modules/` | What a fresh engine and each `use "@x"` make visible, and which native registrations lack a `.flow` surface. |
| `PublicApiCharacterizationTests` | `api/` | Public C# API of `flow-lang`. Removals fail unless listed in `api/allowed-removals.txt`; additions are free. |
| `CorpusCharacterizationTests` (long tier) | `corpus/` | For every tracked script under `tests/` and `examples/`: stdout, diagnostics, error count and rendered-buffer hashes. Fields that differed between two recording runs are marked unstable. |

A failure writes the actual text to the test-report directory and shows the first
differing lines. A changed snapshot is a behavior change: fix the regression, or,
for a deliberate change, regenerate with `FLOW_UPDATE_SNAPSHOTS=1` and review the diff.
The AST and corpus snapshots take their file lists from `git ls-files` when regenerated.

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
    "flow-lang.Tests/baselines/Phase37/piano_warmth_smoke.wav");

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

## Phase 0 CI tiers and reproducible baseline

`.github/workflows/verify.yml` runs three required-to-pass jobs on each PR
and pushes to `dev`/`main`, with separate checkouts/build outputs:

| Tier | Selection | Coverage |
| --- | --- | --- |
| `core` | `Category!=Platform&Category!=LongRunning`, plus all MIDI tests | Language, music, host, and ordinary integration tests |
| `platform` | `Category=Platform` | Desktop/Web build and publish checks; prerequisite-gated MIDI/macOS device tests |
| `long` | `Category=LongRunning` | Longer DSP/stretch, audio regression, and repeated-render determinism tests |

Every test belongs to the core selection unless explicitly assigned one of the
other categories. None are excluded from the combined jobs. Hardware and
Web-target-only tests retain explicit prerequisite skip reasons. The platform
job installs `wasm-tools`; ordinary native-device availability is not assumed.
These jobs establish Linux compatibility-host coverage; they do not claim a
language-only build, hardware listening, or Windows/macOS release certification.
Branch protection must be configured separately to make GitHub checks mandatory.

Run the identical verification locally (Python 3 and .NET 10 required):

```bash
python3 scripts/ci/verify.py --tier core --artifacts /tmp/flow-ci-core
python3 scripts/ci/verify.py --tier platform --artifacts /tmp/flow-ci-platform
python3 scripts/ci/verify.py --tier long --artifacts /tmp/flow-ci-long
# Unfiltered baseline, including both C# test projects:
python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-ci-all
```

The verifier builds first, fails on missing/empty TRX evidence or nonzero exit
codes, and checks tracked-file content hashes even after failures. It supports
a pre-existing dirty working tree by comparing against entry contents. It does
not discard or restore changes. Do not edit tracked files during verification.
Keep artifact paths outside tracked source directories. Run full suites serially,
even across checkouts: legacy audio fixtures still share fixed `/tmp` WAV paths.
CI uploads logs, TRX, verification summaries, and generated report artifacts even when tests fail.

The [Phase 0 baseline](baselines/phase0/README.md) records parse/evaluation/render
measurements, dependencies, public APIs, global-state candidates, and warning
triage. Its probe writes outside the repository and measures offline behavior;
no real-time performance promise is inferred from its results.

The Phase 41 showcase audio fixture uses the logical source name
`examples/edm/pulse.flow` because granular randomness includes source location.
It compares RMS and requires two renders to be byte-identical. To deliberately
refresh this specific audio baseline (separate from report updates):

```bash
FLOW_UPDATE_AUDIO_BASELINES=1 dotnet test flow-lang.Tests/flow-lang.Tests.csproj \
  --filter FullyQualifiedName~Phase41ShowcaseRmsTests
```

Do not enable either update mode in CI. See the [baseline compatibility
decision](decisions/2026-09-20-baseline-compatibility.md) for the initial portable
fixture refresh and PCM comparison evidence.

### Shared analysis frontend (restructuring Phase 4)

`Analysis/LanguageAnalysisTests` covers syntax codes/spans, imported diagnostics,
cycles, explicit module roots/search paths, cancellation and discovery budgets.
Its CLI subprocess fixture includes file writes, playback, looping and imported
initializer output: `flow check` must finish without any of those effects.
`Analysis/ModuleDescriptorTests` checks declaration/runtime binding parity and
rejects LSP IL calls to engine/audio construction or runtime builtin registration.
Completion, hover and varargs fixtures use the descriptor path. These tests are
in the core tier; desktop-specific fixtures are excluded on Web.

`StaticBindingTests` covers source-procedure call warnings, lexical scope, dynamic
uncertainty, actual transitive stdlib visibility, unsaved imported buffers and
related cross-file diagnostics. `EvaluationAdapterTests` compares parsed-tree and
source evaluation for every language contract, including codes and spans, and
checks independent sessions and distinct host failures.

For the Phase 4 process gate (the `--trace` option requires Linux `strace`):

```bash
python3 scripts/ci/analysis_smoke.py flow-lsp/bin/Debug/net10.0/flow-lsp \
  --trace --artifacts /tmp/flow-analysis-smoke
```

This drives actual completion and hover requests, verifies core imports exclude
OSC completions, requires clean shutdown, and rejects sample-file/device/native
audio accesses in the trace. It writes evidence outside the checkout.

After a Web publish, exercise the frozen JavaScript adapter under Node:

```bash
node scripts/ci/wasm-session-smoke.mjs \
  "$PWD/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js" \
  /tmp/flow-wasm-session-smoke.json
```

This verifies independent repeat runs, located parse errors and music arithmetic;
it does not require or certify audible playback.

### Music snapshots and assembly allocation (restructuring Phase 5)

`MusicModel/CompositionSnapshotTests` checks the BCL-only assembly closure,
collection ownership, native construction, Flow compilation parity, source
origins, tuplets/parallel voices, tuning data, timing and cancellation.
`RenderedBufferSequenceTests` checks bit-exact concatenation, final-buffer
ownership, overflow, cancellation between copy chunks and allocation growth.
The 128-repeat fixture compares against the former prefix-copy algorithm and
requires at least an eightfold allocation reduction; it is not a wall-clock test.

```bash
dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter FullyQualifiedName~MusicModel
```

Run full verification serially. If idle MSBuild workers retain subprocess output
pipes during Web-publish checks, use `MSBUILDDISABLENODEREUSE=1` for the verifier.
The render timeout fixture uses sustained small renders so faster finite-song
assembly cannot turn expected cancellation into successful completion.

`MusicModel/SnapshotRenderingTests` verifies the model/BCL-only audio assembly,
bit-exact legacy sine parity (tuplets, negative onsets, ties, overlap, parallel
voices, pedal, pool stealing, tuning and multiple tempos), Flow/native equality,
block-size independence, streaming cancellation/progress, explicit failure cases,
independent concurrent jobs and repeat-independent PCM storage allocation.

`MusicModel/SnapshotMidiExportTests` verifies the model/DryWetMidi-only IO
assembly, byte-identical output against legacy `writeMidi` for three Flow corpora
(chords, rests, triplets/quintuplets/septuplets with TPQN elevation, drums, keys,
per-section tempo, repeats, serial legato/portamento, voice blocks, 3/4), native
track/routing/tempo-map readback, and the intentional score-timing divergences.

`MusicModel/NoteEditingTests` pins the shared quantize (grid, halfway, strength,
swing parity, bar anchoring, chord cohesion, validation) and the Flow `quantize`
adapter's grid-correct behavior, including unchanged output for straight rhythms,
plus shared transpose spelling/clamping/cent splitting, snapshot transposition and
Flow `transpose` parity.

The standalone native host proof is reproducible without a language runtime:

```bash
dotnet run --project scripts/MusicHost -- /tmp/flow-native-host.wav /tmp/flow-native-host.mid
```

`scripts/RenderScaleProbe` measures long-song render time, allocation and peak
working set per process (Release; see `docs/baselines/phase5/README.md`).

It reports loaded assemblies and writes 294,000 stereo PCM16 frames at 44,100 Hz,
plus an optional Standard MIDI File. The example WAV encoder is intentionally
separate from legacy WAV byte baselines.

### Prepared block playback (restructuring Phase 6)

`MusicModel/PreparedSinePlaybackTests` verifies pull/offline sample parity,
variable block sizes, partial/empty/EOF buffers, section/repeat/tempo crossings,
seek/reset, independent cursors, preparation limits, cancellation and compact
large repeats. A warmed thread-allocation check includes reads, seeks, resets
and boundary crossings and requires zero bytes allocated. The existing four
legacy sine parity cases also run through prepared playback (ties, pedal, tuning,
voice stealing and parallel voices). These prove behavior and local allocation,
not audio-device deadlines or freedom from runtime-wide GC pauses.

`MusicModel/PreparedSineTransportTests` checks play/pause/resume/stop/seek state,
reference-sample equality over section/repeat/tempo crossings, exact and partial
EOF, lead-in and half-open loop ranges, one-frame loops, long frame positions,
empty scores/buffers, and invalid-call atomicity in every state. Warmed reads and
transport commands allocate zero bytes. The transport is single-owner; these
checks do not certify cross-thread control, audio deadlines or click-free loops.

`MusicModel/QueuedSinePlaybackTests` checks block-boundary command order and sample
parity, capacity-one and non-power-of-two FIFO wraparound, explicit overflow,
stop recovery through saturation, invalid/empty reads, producer validation and
zero warmed allocation. Concurrent tests exercise 100,000 ordered commands and
5,000 stop/ack cycles with a bounded timeout. These cover the documented single
producer/single consumer contract; they do not establish device deadlines or
multi-producer support.

`MusicModel/PlaybackPublicationTests` checks prepared-source installation at block
boundaries, stale-command discard, shorter/empty scores, stop precedence, pending
and retired slot backpressure, rejected-source ownership, format validation and
invalid/empty reads. Concurrent stress swaps 2,000 generations and reclaims each
old transport exactly once. Warmed callback installation allocates zero bytes;
preparation and transport construction are deliberately outside that measurement.
These tests certify the dry-sine publication protocol, not seamless plugin reload,
native-resource disposal or device callback deadlines.

### Linux callback prototype (restructuring Phase 6)

`PlatformAudio/CallbackRenderProbeTests` runs without hardware: assembly closure,
unmanaged stereo output parity/muting, underflow capture, exception-to-silence/abort,
bounded timing storage and zero warmed callback allocation. The platform reference
and these tests are excluded from the Web test target. No ordinary test opens an
audio device; existing Flow.Audio model/BCL closure tests still apply.

Explicit Linux measurements require system `libportaudio.so.2` (v19), an output
device/session and a Release build. No native binary is bundled or new NuGet used.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet build scripts/AudioCallbackProbe -c Release -p:FlowTarget=Desktop
scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe --list
# Device indices are local/ephemeral; choose one from --list rather than copying 31.
scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe 20 idle /tmp/callback-idle.json DEVICE_INDEX 256
scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe 20 inprocess /tmp/callback-load.json DEVICE_INDEX 256
scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe 20 isolated /tmp/callback-isolated.json DEVICE_INDEX 256
```

The probe renders 32 voices, exercises control/replacement and mutes device output.
Load includes repeated Flow evaluation, cached 4 MiB file reads/hashing and forced
GC every eight iterations. Isolated load runs in a child process; the child stays
active throughout capture. Timings include startup and separately report after
the first second. Capture happens only after native close joins callbacks.

Use 128 as the final argument for the smaller-block check; up to 1800 seconds is
supported for sustained testing. Do not run suites/builds in parallel with timing
probes. Exit 1 indicates native/worker/callback failure or dropped timing storage;
headroom/deadline/underflow counts are data in the report, not exit-code assertions.
A failing device produces a structured error report. Native device enumeration may
emit ALSA diagnostics to stderr even when the selected output succeeds.

Managed-body time excludes native dispatch and runtime pauses before managed entry;
entry gaps and native underflow flags are separate evidence. A native reported
latency of zero means unavailable here, not zero hardware latency. Short muted
runs do not close the 30-minute/device/audible gates. See the Phase 6 baseline.

For sustained runs, the callback report also records first-to-last capture span,
total frames and the 20 longest managed bodies with their times relative to each
report window. Retain all-callback/startup results when assessing the 70% target.
A successful process exit is not a timing verdict: explicitly inspect over70Percent,
overDeadline, outputUnderflows, callbackFault and droppedTimingSamples. The first
30-minute assessment is in `docs/baselines/phase6/callback-stress-assessment.json`.
It met the body target but left a 15.686 ms entry gap and device latency unresolved.
