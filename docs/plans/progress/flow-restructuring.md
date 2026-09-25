# Flow restructuring progress

Roadmap: [Flow restructuring and focused DAW](../2026-09-20-flow-restructuring-roadmap.md).

## Current milestone

**Phase 3 is complete (2026-09-24).** The BCL-only `flow-language` artifact now
includes the core library and module assets. `scripts/LanguageHost` runs all seven
non-musical examples, embedded sessions and a line REPL without the music assembly
or audio packages. The compatibility host still passes the music corpus. Full
verification passes in a fresh clone; see the [Phase 3 record](../../baselines/phase3/README.md).

Phase 2 (session ownership, cancellation/budgets, evaluation coordination and
process isolation) remains complete; see the [Phase 2 record](../../baselines/phase2/README.md).
**Phase 4 is complete — honest analysis and shared tooling (2026-09-24).**
Shared import-aware analysis, metadata-driven editor tooling, parsed-tree
execution and explicit CLI/browser adapters pass the documented gate. See the
[Phase 4 record](../../baselines/phase4/README.md) for verification and static limits.
**Phase 5 is in progress — music model and efficient offline renderer.**
The detached score boundary and linear song-buffer assembly are implemented;
full snapshot-driven rendering/export and the remaining gate are still open.

Phase 1 (contracts, examples, seam decisions) and its semantic fixes are complete;
see the [Phase 1 record](../../baselines/phase1/README.md).

Starting revision: `1e85f6710b6a39c293b0e4361ca57364489c824d`.
Owner: primary implementation agent. No delegated file ownership.

| Task | Status | Scope / dependencies |
| --- | --- | --- |
| P0-01a SDK and terminal isolation | verified | `global.json`, terminal test classes, terminal environment helper. No runtime semantic changes. |
| P0-01b Fresh baseline | verified | Main and MIDI test suites; logs and TRX under `/tmp/flow-restructuring-baseline/`. |
| P0-01c CI and disputed behavior contracts | verified | Existing vowel/MIDI semantics recorded and tested; three CI tiers pass from a fresh checkout. |
| P0-03 Dependency/API/global inventory | verified | Machine-readable snapshots and current-contract/ownership documentation. |
| P0-04 Warning triage | verified | Targeted warning fixes; remaining diagnostics classified without suppression. |
| P0-05 Performance baseline | verified | Reproducible Release parse, pre-parsed evaluation, engine and offline render measurements. |
| P0-02 Non-mutating test outputs | verified | Audit and bundle reports use isolated artifacts; explicit update mode verified; full solution run preserved tracked contents. |
| P1-01 Executable language contract | verified | `contracts/language/` (69 contracts), `LanguageContractTests`. Pins current behavior only. |
| P1-02 Non-musical examples | verified | `examples/language/` (7 programs with expected output, incl. two-module utility). |
| P1-03 Seam decisions | recorded | Six `docs/decisions/2026-09-22-*` files: profiles, grammar, signature ownership, dependency direction, session lifetime, analysis/evaluation API. |
| P1-04 Dependency ratchet | verified | `DependencyDirectionTests` + `docs/baselines/phase1/language-dependency-edges.json` (38 edges). |
| P1-05 Minimal host prototype | verified | `scripts/MinimalHost` + `docs/baselines/phase1/minimal-host.json`. |
| P1-06 Fix Phase 1 findings | verified | Runtime, stdlib, parser, host diagnostics; 70 contracts all preserve/generous. |
| P2-01 Session-owned state | verified | `SessionServices`/`EngineOptions`/`RenderServices`; `FlowEngine.Current*` statics removed; per-session output, advisories, config, RNGs, OSC state. |
| P2-02 Cancellation and budgets | verified | `FlowEngine.Evaluate`, deadline checkpoints, iteration ceiling, cancellation-safe thunks; WASM 30 s cap enforced. |
| P2-03 Job coordination | verified | `LatestRequestCoordinator<T>`; watch mode migrated; REPL Ctrl+C cancels. |
| P2-04 Process worker | verified | `ProcessEvaluationWorker` + `flow-interpreter --worker`; kill-and-replace tested. |
| P2-05 Hosts without console redirection | verified | WASM, `flow check`, `flow doc`, test fixture, minimal host use engine sinks. |
| P4-01 Shared static frontend | verified | Parse, module/signature descriptors, bounded imports and conservative source binding/call warnings. |
| P4-02 Non-executing check and editors | verified | Shared CLI/LSP analysis, unsaved module overlays, import-aware completion/hover and related diagnostic locations. |
| P4-03 Metadata-only introspection | verified | Declaration-based signatures; IL gate and real LSP strace smoke show no runtime/sample/device initialization. |
| P4-04 Explicit evaluation adapters | verified | Parsed-tree Evaluate, coded results/HostFailure, explicit host session sinks/config, frozen browser JSON adapter. |
| P4-05 Gate and documented limits | verified | 3,012 main + 21 MIDI tests; Web publish/Node boot; source/parsed contract parity; documented runtime-only checks. |

## Decisions

- Start with SDK selection and terminal test reliability before runtime extraction.
- Use .NET SDK `10.0.100` with `latestFeature` roll-forward and prereleases disabled: accept installed stable .NET 10 feature bands, avoid silently switching major versions.
- Terminal tests own `NO_COLOR` and `TERM` for each test and restore prior values. Existing suite-wide collection serialization remains required for process-global state.
- Preserve intentional current vowel fallback and MIDI hand/voice splitting per the [compatibility decision](../../decisions/2026-09-20-baseline-compatibility.md); stale assertions are replaced, not suppressed.

## Verification

Host: Ubuntu 26.04 x64; installed SDK `10.0.112`, runtime `10.0.12`.
The initial `dotnet --info` reported `10.0.0` as an invalid SDK version.

Before-change baseline (inherited `NO_COLOR=1`, `TERM=xterm-256color`):

```sh
dotnet test flow-lang.Tests/flow-lang.Tests.csproj --logger 'trx;LogFileName=language-before.trx' --results-directory /tmp/flow-restructuring-baseline
```

Log: `/tmp/flow-restructuring-baseline/language-before.log`.
Result: **2,753 passed, 9 failed, 19 skipped**, 2,781 total. Local `/tmp` artifacts are not committed.

## Current gate status

Phases 0–4 meet their documented gates for the Linux baseline (details below).
Environmental skips, analysis limits and remaining warnings are explicit.
Hardware/audio listening and remote GitHub Actions execution are not claimed.
Phase 5 is the next milestone.

## Completed slice — 2026-09-20

P0-01a is verified in the working tree: `global.json`,
`flow-lang.Tests/Helpers/TerminalEnvironmentScope.cs`,
`Integration/Audit0609/LiveStatusPanelTests.cs`, and
`Integration/Phase38/{AnsiPanelRenderTests,PanelTtyFallbackTests}.cs`
(the integration paths are relative to `flow-lang.Tests`).
The ledger was initially committed as `6ef3d91`; implementation files remain
uncommitted for review. No runtime behavior changed. Phase 0 remains incomplete.

SDK verification: `dotnet --version` selects `10.0.112` with the corrected
configuration. `git diff --check` passes.

Terminal validation commands:

```sh
NO_COLOR=1 TERM=dumb dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter 'FullyQualifiedName~AnsiPanelRenderTests|FullyQualifiedName~LiveStatusPanelTests|FullyQualifiedName~PanelTtyFallbackTests' --logger 'trx;LogFileName=terminal-hostile.trx' --results-directory /tmp/flow-restructuring-baseline
env -u NO_COLOR -u TERM dotnet test flow-lang.Tests/flow-lang.Tests.csproj --no-build --filter 'FullyQualifiedName~AnsiPanelRenderTests|FullyQualifiedName~LiveStatusPanelTests|FullyQualifiedName~PanelTtyFallbackTests' --logger 'trx;LogFileName=terminal-unset.trx' --results-directory /tmp/flow-restructuring-baseline
dotnet test flow-midi.Tests/flow-midi.Tests.csproj --logger 'trx;LogFileName=midi-baseline.trx' --results-directory /tmp/flow-restructuring-baseline
```

Both terminal runs: **13 passed, 0 failed, 0 skipped**, including the eight
previously failing cases and the new `TERM=dumb` case. The new ESC assertion
uses ordinal comparison because culture-sensitive comparison can ignore ESC.
The entire main suite was not rerun after these test-only changes; no claim of
a post-change full-suite pass is made.

MIDI baseline: **19 passed, 2 failed, 0 skipped**, 21 total.
Logs share each TRX filename stem with a `.log` suffix in the artifact directory.

### Failure and skip classification

- Eight main-suite failures: four `AnsiPanelRenderTests` and four
  `LiveStatusPanelTests` depended on the inherited terminal environment.
  All now pass under both tested environments.
- `FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException`:
  test expects an exception; `FormantData.GetFormants` deliberately falls back
  to `ah` with an advisory. Preserve current runtime behavior until the contract
  decision is recorded and the conflicting test is reconciled.
- `FlowGeneratorStructureTests.One_Sequence_Per_Track_Channel_No_RH_LH_Suffix`:
  expects two sequences, receives four.
- `QuantizerRoundingTests.Two_Octave_Range_Does_Not_Split_RH_LH`:
  expects one track, receives right/left-hand tracks. Both MIDI failures come
  from current melodic hand/voice splitting in `Quantizer`; whether to preserve
  splitting by default or expose an option remains a compatibility decision.
- Main-suite skips: 13 Web-only tests on Desktop, 3 real MIDI loopback tests
  lacking librtmidi/VirMIDI prerequisites, 2 macOS-only CoreAudio tests, and
  1 MusicXML round-trip test lacking `mscore`. No failures were suppressed.

### Test side effects and warnings

The default main suite changed seven previously clean tracked reports:
`.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/` files
`advisory-sites.txt`, `all-clamps.txt`, `charitable-sites.txt`, `flow-call-sites.txt`,
`flow-proc-decls.txt`, `summary.txt`, and
`.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md`.
Generated versions were preserved under `/tmp/flow-restructuring-baseline/generated/`
and only those test-generated changes were restored. User changes remain intact.
The non-mutating-test gate therefore still fails; restoration is not a fix.

Build warnings include Rug.Osc .NET Framework compatibility (`NU1701`),
nullability (`CS8765`, `CS8604`, `CS8602`), duplicate import (`CS0105`), unused
members/locals (`CS0414`, `CS0219`), and test analyzer warnings about blocking,
cancellation, and unused theory arguments. Logs retain details; no blanket
warning suppression or warnings-as-errors policy was introduced.

At the end of the first slice, P0-02 was next. Its completion is recorded below.
No architecture extraction has begun.


## Completed slice P0-02 — 2026-09-20

Starting revision: `8c623c7`, with the previous slice's implementation still
in the working tree. Owner: primary implementation agent. No delegates.

Verified working-tree changes:

- `flow-lang.Tests/Helpers/TestReportDirectory.cs`: unique retained directories
  under the system temporary `flow-test-reports/` directory by default;
  `FLOW_TEST_ARTIFACTS_DIR` selects another root. Only `FLOW_UPDATE_REPORTS=1`
  directs reports to their historical paths, taking precedence over that root.
- `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs`: passes
  the scripts' existing `--out-dir` argument using `ProcessStartInfo.ArgumentList`,
  reads newly generated artifacts, and checks historical file contents and
  membership recursively in normal mode.
- `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs`: writes the
  bundle report through the same output policy and documents explicit updates
  in newly generated report text.
- `docs/TESTING.md`: artifact collection and explicit historical-update commands.

Standalone audit script defaults are unchanged; direct callers can already
select `--out-dir`. No production runtime changes or historical report refreshes
are included. Implementation changes remain uncommitted; only the ledger is
committed as requested.

### Verification

Artifacts: `/tmp/flow-restructuring-reports/` (local, not committed).

```sh
FLOW_TEST_ARTIFACTS_DIR='/tmp/flow-restructuring-reports/artifacts with spaces' dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter 'FullyQualifiedName~ClampGrepConsistencyTests|FullyQualifiedName~BundleSizeBudgetTests' --logger 'trx;LogFileName=reports.trx' --results-directory /tmp/flow-restructuring-reports
dotnet test flow-sharp.sln --logger trx --results-directory /tmp/flow-restructuring-reports/full
FLOW_UPDATE_REPORTS=1 dotnet test flow-lang.Tests/flow-lang.Tests.csproj --no-build --filter 'FullyQualifiedName~InventoryFiles_LandInSelectedDirectory|FullyQualifiedName~BundleSizeReport_WrittenToDisk' --logger 'trx;LogFileName=update.trx' --results-directory /tmp/flow-restructuring-reports
```

- Focused filter: **12 passed**, including the matching strict-clamp tests.
  Artifact paths containing spaces worked. Hashes of all existing tracked files
  were unchanged after the run (`tracked-before.json`).
- Full default solution run: main **2,762 passed, 1 failed, 19 skipped**;
  MIDI **19 passed, 2 failed, 0 skipped**. Remaining failures are exactly the
  previously classified vowel/MIDI contract disagreements. This also verifies
  the prior terminal fixes in the full suite.
- Full-run before/after SHA-256 comparison of every existing tracked file found
  **zero content changes**, without restoration (`full-before.json`). Default
  temporary report generation was exercised because neither report setting
  was supplied to this run. The non-mutating tracked-content gate passes on
  this Linux configuration; this is not a claim that all tests pass.
- Explicit update mode: **2 passed**; all eight intended historical outputs
  were written (including the empty `input-clamps.txt`). The validation wrapper
  saved results under `explicit-update-output/` and restored exact pre-run
  contents in a `finally` block. The wrapper is retained as `verify-update.py`.
- `git diff --check` passes. `reports.log`, `full.log`, and `update.log` retain
  build/test evidence; corresponding TRX files are in the same artifact tree.

Next ready work: settle the documented vowel/MIDI contracts and add PR CI
without suppressing failures. Dependency/API inventory, warning triage, and
performance measurements are still required for the complete Phase 0 gate.


## Phase 0 completion — 2026-09-20

Starting revision for the closing slice: `8135359`, retaining earlier working-tree
implementation changes. Primary agent owns all integration; no delegated agents.
User changes to `docs/ARCHITECTURE.md`, the untracked roadmap, and the two ignored
local Flow test inputs were preserved. Implementation, workflow, baseline data,
and documentation changes remain uncommitted for review; this ledger is committed
as requested. Completion describes the verified working tree, not a pushed release.

### Delivered

- Recorded existing vowel fallback and MIDI hand/voice-splitting intent before
  changing tests. Assertions now check per-phoneme advisory dedup, source channels,
  pitches, onset positions and durations. No converter/language semantics changed.
- Added `scripts/ci/verify.py` and `.github/workflows/verify.yml`: separate core,
  platform/Web, and long-running jobs, no failure ignores, TRX/log artifacts,
  explicit empty-result rejection and tracked-content checks even after failures.
  Added category traits to the owning test classes. Python verifier tests cover
  failure propagation and mutation detection.
- Fixed JACK test advisory-state isolation after a fresh-checkout failure exposed
  test-order dependence. Normalized showcase source identity after the same
  checkout exposed path-derived granular randomness; kept the RMS tolerance,
  added repeated-byte equality, isolated MIDI output, and made audio baseline
  updates explicit. The reviewed WAV refresh is documented with hashes and the
  changed first-3.5-second region; no production DSP changed.
- Fixed SDK selection, terminal environment ownership, generated report paths,
  and targeted compiler warnings. Classified remaining warnings as follow-up work.
- Added `scripts/BaselineProbe` and `scripts/baseline/inventory.py`, plus checked-in
  snapshots under `docs/baselines/phase0/`: 402 visible public types, 789 static-field
  candidates, 10 projects, 18 module/style sources, native interop sites, asset
  hashes, source hashes, and measured parse/evaluation/render time/allocation/memory.
  The 32-section offline sine render allocates about 387 MiB for 21.5 MiB final PCM;
  this establishes a Phase 5 comparison point, not real-time safety.

### Accepted verification

- Fresh checkout: core main **2,715 passed / 14 skipped**, platform **13 passed /
  5 skipped**, long **33 passed**, MIDI **21 passed**; all zero failures and zero
  tracked-file changes. Tier coverage has no overlaps or missing tracked tests.
- Final unfiltered original-workspace run: main **2,763 passed / 0 failed /
  19 skipped**, MIDI **21 passed / 0 failed**. Zero tracked-content changes,
  without restoring files after the run. The two extra local cases are the
  pre-existing ignored `test_break_builtin.flow` and `test_markov_corpus_array.flow`.
- SDK selection and Desktop build pass; platform tier compiles/publishes Web.
  CLI smoke prints `3`. `git diff --check` passes.
- Two Python verifier tests pass (success plus five failure scenarios).
- Release baseline probe validates collection result 99,990,000 and finite,
  nonempty 44.1 kHz stereo renders. Measured production/probe source hashes still
  match the final sources. No hardware playback or callback deadlines claimed.

Exact commands, skip reasons, checked-in summaries, and artifact paths are in the
[verification record](../../baselines/phase0/verification.md). Raw closing artifacts:
`/tmp/flow-phase0/`; pre-fix failures are retained alongside accepted results.

Next ready slice: Phase 1's executable language-personality corpus and first
non-musical examples, followed by module/profile, grammar, signature ownership,
and session/API decisions. Phase 0 has no unresolved completion gate.


## Phase 1 completion — 2026-09-22

### Delivered

- Committed Phase 0 first in four path-scoped commits after rerunning the core
  tier on the uncommitted tree: main 2,717 passed / 0 failed (the two extra over
  2,715 are the ignored local Flow tests), MIDI 21 passed, zero tracked changes.
- Language contract: 69 programs across calls, procedures, functional behavior,
  data, ergonomics, forgiving/strict semantics, modules and music. 48 preserve,
  3 generous, 7 disputed, 7 defect, 4 gap. Each has expected stdout, error count,
  stderr fragments, rationale and status; an index test enforces documentation.
- Seven non-musical examples with expected output, exercising collections,
  dictionary aggregation, tuple pipelines, recursive trees, deferred evaluation,
  pattern dispatch and a two-module utility. Gaps they work around are listed in
  `examples/language/README.md`, not fixed.
- Decisions: legacy profile default with a host-selected `language` profile; one
  grammar with optional music semantics; C# registration authoritative for native
  signatures with `.flow` exports derived/validated; namespace-level dependency
  rules enforced by a shrink-only ratchet; session-owned state and sinks; separate
  Parse/Analyze/Evaluate APIs with one diagnostic model.
- Minimal host (library-only reference) runs a non-musical program and records
  remaining dependencies: static references to DryWetMidi/NAudio/Rug.Osc/Tomlyn,
  three process statics, eager `@bars`/`@improv` loading and style packs, ~600
  mostly musical builtins, console redirection, private-only module/impl lists.

### Findings (not fixed; owner decisions needed)

Defects: lazy `and`/`or` operands return a truthy thunk (wrong branch taken);
thunks held in variables are never forced; `Lazy<T>` declarations, arithmetic on
an unknown identifier, Int/String comparison, arrays of tuples in variables and a
bare `(dict)` reduce seed abort evaluation with `0:0` internal errors. Disputed:
optional-paren calls with nested arguments are silently dropped, `(Nothing)` vs
docs, Int overflow wraps, interpolation formatting, circular imports, strict-proc
widening, unit arithmetic dropping units. Also: the CLI forces ANSI color on rich
diagnostics despite `NO_COLOR`, and `FlowEngineRunner`/`FlowScriptTests` ignore
rich diagnostics (a scan of all 152 tracked `tests/*.flow` found none masked).

### Verification (2026-09-22)

```sh
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier core --artifacts <dir>
dotnet run --project scripts/MinimalHost -c Release -- --json docs/baselines/phase1/minimal-host.json
dotnet flow-cli/bin/Debug/net10.0/flow.dll run examples/language/modules/report.flow
```

- Core tier: main **2,795 passed / 0 failed / 14 skipped** (2,717 + 78 new:
  69 contracts, 7 examples, index check, dependency ratchet), MIDI **21 passed**,
  zero tracked-content changes.
- Harness self-check: a changed `.out` and an invalid status both fail.
- CLI runs the examples; `recursion-trees.flow` output matches its `.out`.
- `git diff --check` passes. Platform and long tiers were not rerun: Phase 1 made
  no runtime, build-flavor or long-test changes. `scripts/MinimalHost` is not in
  the solution (like `BaselineProbe`) and is built only when run.

Next ready slice (superseded below): owner review of the Phase 1 findings, then
Phase 2 session ownership.


## Phase 1 findings fixed — 2026-09-22

The owner asked to fix the issues before Phase 2. Each contract's expectation was
changed first (it failed), then the runtime was fixed and the contract re-pinned.

- Evaluation never ends early: builtin and statement exceptions become located
  errors; a failed argument skips its call instead of cascading; failed
  declarations bind a default. `TestRunner` fails bodies that report errors.
- Lazy values force deeply (stored thunks work, memoized, failures once); `and`/`or`
  force lazy operands; `Lazy<T>` declarations work.
- Integer arithmetic promotes on overflow; out-of-range Int binding is a named
  error. `Voids` takes any array, `Tuple<<...>>[]` and bare `Tuple` types exist,
  untyped dicts convert to typed ones.
- Statement-head paren-less calls accept parenthesized arguments; `(t ~> f)` in
  parentheses; tuple patterns in `match`; one formatter for interpolation/`str`/
  `print`; `(Nothing)` returns Void; circular imports skipped with an advisory;
  strict-proc docs corrected (behavior kept).
- Unit-preserving arithmetic and cross-unit duration comparison; `transpose` on a
  Note; `str` on tuples/dicts; new `mod`, `sort`, `split`.
- Hosts: `ErrorReporter.ErrorCount`/`FormatAll`/`ShouldUseColor`; interpreter CLI,
  REPL, `flow eval/check/test/doc`, `FlowEngineRunner` and WASM `errors[]` include
  rich diagnostics; `NO_COLOR`/`TERM=dumb`/redirection disable styling.
- Examples no longer need workarounds; wiki pages updated (functions, strict mode,
  imports, interpolation, language basics, collections, standard library).

Verification (2026-09-22): core **2,800 passed / 0 failed / 14 skipped**, MIDI
**21 passed**; platform **13 passed / 5 skipped**; long **33 passed**; zero
tracked-content changes in every tier. No existing test expectation changed.
The committed flow-site playground AppBundle still carries the old runtime until
`flow-site/scripts/sync-runtime.sh` is rerun (not done here).

**Commit gap found and fixed.** The global `*.flow` rule in `.gitignore`
kept every contract and example `.flow` program out of the Phase 1 commits
(`ad45aaf` etc.); only their `.out` files were committed, so a fresh checkout
could not run the contract tests. `.gitignore` now allow-lists `contracts/` and
`examples/language/`, the programs are committed, and the contract tests were run
from a fresh `git clone` of the result.

The fresh-clone core run exposed an intermittent OSC loopback failure: three
tests bound their UDP receiver inside `Task.Run` while the sender fired at once,
so a datagram could arrive before the socket existed. The receivers now bind
before sending. This was a test race, unrelated to the interpreter changes.

Next ready slice: Phase 2 session ownership against the session-lifetime
decision's gate.


## Phase 2 completion — 2026-09-22

### Delivered

- `SessionServices` (language layer) and `RenderServices` (music layer) own what was
  process-global: output and diagnostic sinks, `AdvisoryLog` dedup, config
  snapshot, cancellation/deadline, iteration ceiling, tracked resources, sample
  caches, noise and dither RNGs (original seeds), custom wavetables, `setBPM`
  tempo, MIDI sink, OSC queues and rate limits, TTS command. `EngineOptions`
  configures them. `FlowEngine.Current*` statics are removed. Legacy static code
  reaches the session through a scoped transitional lookup set only inside engine
  entry points.
- `FlowEngine.Evaluate` + `EvaluationOptions` → `EvaluationResult`
  (Succeeded/Failed/Cancelled/TimedOut). Checkpoints in loops, calls, builtins,
  section rendering, voice mixing and DSP loops. Deadline-based budgets work in
  single-threaded WASM. Cancellation is not cached by thunks or swallowed by
  module/test catches. Cancelled evaluations stop their playback. Script-opened
  OSC listeners, MIDI clocks and ports are released on engine disposal.
- `FlowLang.Hosting.LatestRequestCoordinator<T>` (latest-request-wins, generation
  IDs, stale-result rejection, last-good retention). Watch mode uses it and a
  per-watch-session advisory log; its orphaning `Task.Run` + `Wait` is gone. REPL
  Ctrl+C cancels the running evaluation.
- `FlowLang.Hosting.ProcessEvaluationWorker` + `flow-interpreter --worker`: kill the
  worker process tree on timeout/cancel, start a fresh one next time.
- WASM, `flow check`, `flow doc`, the `FlowEngineRunner` fixture and the minimal
  host use engine sinks; `flow doc` examples stop cooperatively.
- Language fix: unit values format numbers like plain doubles (10 significant
  digits). Browser fix: the D-48-10 30 s cap is enforced and reports `cancel`.
- Decision record updated with implementation notes and the process-wide state that
  remains by design. CLAUDE.md and wiki pages (loops, live coding, playground,
  CLI) updated.

### Verification (2026-09-22)

```sh
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier core --artifacts <dir>
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier platform --artifacts <dir>
NO_COLOR=1 TERM=dumb python3 scripts/ci/verify.py --tier long --artifacts <dir>
dotnet run --project scripts/MinimalHost -c Release -- --json docs/baselines/phase2/minimal-host.json
```

- Core **2,821 passed / 0 failed / 14 skipped** (21 new hosting tests), MIDI
  **21 passed**; platform **13 passed / 5 skipped**; long **33 passed**; zero
  tracked-content changes in every tier.
- CLI smoke: `flow run`, `flow check` (including a rich diagnostic), `flow eval`,
  and a `--worker` round trip.
- Fresh `git clone` of `6745994`: core **2,819 passed / 0 failed** (the workspace's
  two extra are the ignored local Flow tests), MIDI **21 passed**, zero tracked changes.
- Five tests that asserted advisory dedup across separate engines now pin the
  shared-`AdvisoryLog` contract (advisories are once per engine by default).

Next ready slice: Phase 3 (syntax-level type names, music bindings behind an
interface, stdlib split with compatibility aggregates, language-only build).

## Phase 3 complete — 2026-09-24

### Characterization suite (`933f2d4`, fixes `94792af`)

Snapshot tests under `flow-lang.Tests/Characterization/` pin what the extraction
must not change: parser ASTs for every contract and example, overload resolution
and conversions for 33 representative types, value formatting/equality/members,
module export surfaces, the public API (removals need an allow-list entry), and the
whole music corpus (stdout, diagnostics, error count, buffer hashes; `LongRunning`).
Writing them surfaced five language inconsistencies, fixed first (integer `delay`
time, identity casts, unit × unit `mul`, empty-buffer default, `Void` variables);
see `docs/decisions/2026-09-22-phase1-semantic-fixes.md`. The corpus test pins
microphone capture to silence at 44.1 kHz so the host input device cannot change it.

### Extraction slices

- **A (`ecd9db4`)** — lexical rules move to `FlowLang.Syntax` (notes, chords,
  numerals, articulations, the fixed type-name grammar). `TypeCatalog` binds names
  to types; music types register from `MusicTypeCatalog`; unbound names parse as
  `UnresolvedType`. Tuple/Dict types move to `FlowLang.TypeSystem`. The parser and
  lexer no longer reference music runtime types. Edges 38 → 30.
- **B** — `FlowType.IsUnitQuantity` / `AcceptsConversionFrom` traits replace
  music-type checks in `IntType` and `FunctionSignature`; the note-stream parser
  uses `NumeralSyntax`. `WasmEntry` is classified as host glue (the frozen
  `flow-runtime.js` binds it by full name). Edges 30 → 25.
- **D1** — the 30 music `Value` factories move to `MusicValue`
  (`FlowLang.TypeSystem.SpecialTypes`), and music conversions leave
  `Value.ConvertTo` for a `ValueConversions` registry the music layer fills.
  `Value` has no music dependencies. Edges 25 → 19.

- **D2** — `MusicalContext` moves to the new `FlowLang.Music` namespace (forbidden
  to the language), with `MusicSession`: the sections, style packs, SFZ registries,
  tuning-stack operations and memoized context resolution that lived on
  `ExecutionContext`. The language gains two neutral seams: typed per-frame scope
  state (`StackFrame.GetScope/SetScope`, `ScopeVersion`) and per-context extensions
  (`GetExtension<T>`, snapshotted for tests via `ISessionExtension`). C# 14
  extension members keep the old call shapes. `ExecutionContext` and
  `MusicalContext` have no music edges; the remaining edges into `FlowLang.Music`
  are the D3/D4 targets. Edges 19 → 18 (with `FlowLang.Music` newly forbidden). The
  public-API test no longer truncates its removal list (it had hidden 72 entries,
  now allow-listed).

- **D3** (`3d64844` + this slice) — the note-stream and progression compilers move to
  `FlowLang.Music`. The interpreter now evaluates every music construct through
  per-context `DomainBindings` (expression/statement evaluators by node type, literal
  parsers, constants, members, defaults, declaration observers, patterns);
  `MusicBindings` supplies them and `FlowEngine` installs them. Section declarations,
  section calls/overload dispatch, context/tuning/live blocks, songs, chords, beats,
  note streams and progressions moved out of `Interpreter`/`ExpressionEvaluator`/
  `PatternMatcher` verbatim. A context without bindings runs plain code and reports
  music constructs (`DomainBindings` tests). **Edges 18 → 12 → 0**: the language
  namespaces have no music or platform dependencies.

Verification after D1: `verify.py --tier all` core 2,882 passed (the one failure
was the host-dependent mic advisory, now pinned), MIDI 21 passed, Web build OK,
zero tracked-content changes. After D2: `--tier all` core 2,883 passed / 0 failed,
MIDI 21 passed, Web build OK, zero tracked-content changes. After D3: core 2,890
passed / 0 failed, MIDI 21, Web build OK, zero tracked-content changes; CLI smoke OK.

- **E1 (`304f9d8`)** — the language's transitive closure stops reaching music:
  build-target constants (`BuildTarget`), domain comparisons (`ValueComparisons`),
  and formatting (`ValueFormatter`, `FlowType.Format`) move behind language seams.
- **E2 (this slice)** — **the language is its own assembly.** `flow-language/`
  (`flow-language.dll`, 127 files moved with history) holds lexer, parser, AST,
  types, values, interpreter and the extension contracts, and references only the
  BCL; `flow-lang` references it. No public API change (types kept namespaces).
  `ModuleLoader` finds embedded stdlib modules in any loaded assembly; WASM trim roots
  gain a `flow-language` block (and drop stale `SpecialTypes.Tuple/DictType` names left
  from slice A); the Web reference scan, API snapshot and audit harness cover both
  assemblies; the test assembly runs the music library's initializer up front.
  Verified: `--tier all` 2,893 passed / 0 failed, MIDI 21; Web build OK;
  `dotnet publish -p:FlowTarget=Web` bundles `flow-language.wasm` and the AppBundle
  boots under Node and runs a music script through `flow-runtime.js`
  (`5 C 6s`, no errors). The committed `flow-site/static/wasm` bundle is untouched
  (refresh with `sync-runtime.sh` when the site should pick this up).

- **F1 (this slice)** — stdlib split, owner decision 2026-09-23: `@core` is the
  essential general-purpose library (121 declarations moved out of `std.flow`, plus
  `@collections`); `@std` imports `@core` + `@bars` and keeps the 221 music-typed and
  non-essential declarations (`slice(Sequence)` moved there from `@collections`). The
  recorded `@std` export surface, the corpus and all overload snapshots are
  unchanged. The LSP index knows `@core` (`ModulesVisibleThrough`); the non-musical
  examples import `@core`; contract `modules.core-library`. Found: `FlowEngine`
  loads `@std` (and the improv packs) at init as an implicit prelude, so imports are
  currently optional in the music host — owner decision pending on removing it.
- **F2 (this slice)** — owner decision 2026-09-23: **no implicit prelude.**
  `FlowEngine` no longer loads `@std` at init; scripts import what they use. The REPL,
  `-e`, `flow eval`, `flow doc` examples and the `RunSource` test fixture import `@std`
  (`FlowEngine.ImportInteractiveDefaults`). Every music module imports `@std`, so any
  script with one music import is unchanged; only `tests/test_error_masking.flow` (no
  imports) needed `use "@std"`, and the corpus is identical. Style packs load lazily in
  a private engine on first `registerStyle`/`listStyles`/`jam`, so `@improv`/`@std` no
  longer leak into every script. Contracts `modules.no-prelude`, `modules.core-library`
  (music builtins absent under `@core` alone). ~265 test snippets moved to the
  interactive path (they are `-e`-style snippets).

D4 (pattern matcher, member access, section dispatch) landed inside D3. The
remaining core implementation/assets and language-only host work landed in F3/F4/G
below. The BCL-only music-shaped descriptors remain optional later cleanup.

- **F3 — core implementations** (2026-09-24): `CoreLibrary.Register(registry[, context])`
  now supplies the general-purpose builtins entirely from `flow-language`:
  arithmetic/conversions, strings, collections/callbacks, dictionaries, control flow,
  random state, comparisons and output. The music host interleaves internal core
  registration slices at their original positions, preserving overload order;
  `RegisterSignaturesOnly` still follows that same path. `StdLib`/`Collections`
  retain forwarding C# APIs plus their music-specific implementations. Note sorting
  uses a domain ordering hook in `ValueComparisons`, installed by the music layer.
  Verification: all 112 contract/characterization tests pass, including the music
  corpus, overload/value/module snapshots, public API and BCL-only closure checks.

- **F4 — core module assets** (2026-09-24): `core.flow` and `collections.flow`
  move to `flow-language`, with transitive output/publish copying and Web embedded
  resources under their existing logical names. Publish verification distinguishes
  the language module list; Phase 48 checks scan both trimmed assemblies. The AST
  manifest changes only the two paths (hashes unchanged). Verification: 106
  contracts/module/parser/Web-embedding tests pass, including a trimmed Web publish.

- **G — language-only host** (2026-09-24): `scripts/LanguageHost` references
  only `flow-language` and composes `CoreLibrary`, session sinks and the interpreter
  with no domain bindings. It supports scripts/stdin (explicit imports), `-e` and a
  line REPL (`@core` interactive default), plus a JSON assembly/native-library
  report. Twelve tests cover all seven examples in separate processes, exact output,
  artifact/loaded/native closure, embedded use, REPL state, and unavailable prelude/
  music. The two nested tutorial helper modules still imported `@std`; corrected
  both to `@core` without changing their expected output. Parser snapshots record
  only those import changes. Verification: all 12 host tests and parser gate pass.

- **Gate portability fixes** (2026-09-24): the first fresh-clone run exposed
  corpus snapshots whose unseeded granular buffers depended on the checkout's
  absolute source path. The harness now evaluates with repository-relative source
  names (runtime seeding semantics unchanged); a two-pass regeneration changes only
  the three affected buffer hashes. Full suites in overlapping checkouts also share
  the strict-showcase `/tmp` WAV, so final verification runs serially. Documented
  this constraint in `docs/TESTING.md`. Restored the MinimalHost probe's missing
  `FlowLang.Music` import after the scope-state move; construction now records zero
  modules/styles. Also removed the new host-test helper's async naming warning.
  Verification: corpus two-pass regeneration succeeds; final fresh-clone gate follows.

- **OSC disposal-test readiness** (2026-09-24): the serial fresh-clone gate
  exposed a pre-existing scheduling race in the Phase 2 resource-lifetime test:
  `oscListen` connects on its worker task, but the test immediately asserted the
  UDP port was occupied. It now waits up to five seconds for the receiver's
  connected state, then performs the same occupied-before/free-after checks.
  A `using` also guarantees engine cleanup on assertion failure. The test is
  explicitly Desktop-only. All seven cancellation tests pass; no runtime changes.

- **Phase 3 gate closed** (2026-09-24): implementation through `e2bd5f8`; the
  fresh clone at `/tmp/flow-phase3-verified` publishes LanguageHost before building any
  music project, then `verify.py --tier all` passes **2,906** main tests + **21** MIDI
  tests, **19** prerequisite/Web-only skips, **0** failures and **0** tracked-content
  changes. The original checkout run passed 2,908 + 21 (it discovers two additional
  ignored local Flow scripts). Twelve new host tests cover the language-only gate.
  The published host also matches all seven `.out` files from `/tmp`. The Web
  AppBundle boots under Node through the frozen runtime and prints `10` / `150ms`
  with no errors. The refreshed compatibility MinimalHost records zero startup
  modules/styles. Gate evidence and limitations: `docs/baselines/phase3/`.

## Phase 4 complete — 2026-09-24

- **A — non-executing frontend and check**: `FlowLang.Analysis` exposes `Parse`,
  declaration descriptors and bounded, cancellable discovery of top-level imports
  through a host-supplied source provider. Sources retain their identities; no
  interpreter, module body or builtin implementation is invoked. Syntax diagnostics
  have stable stage codes and spans; rich diagnostic details survive unchanged.
  `flow check` now uses this frontend and explicitly reports its current scope
  (syntax/imports; runtime behavior unchecked). The LSP parse adapter shares the
  same parser and publishes its coded spans while preserving its legacy parse API.
  Name/type/overload binding and nested/conditional import analysis are explicitly
  listed as unchecked, not treated as proven valid. Verification: 137 frontend and
  existing Phase 17/pragma tests pass, including a CLI script with writes, playback,
  infinite looping and imported initializer effects, none of which execute.

- **B — declaration-driven editor metadata**: LSP startup now builds builtin
  signatures from the shared parsed module descriptors, rather than invoking
  `RegisterSignaturesOnly` and constructing dummy runtime/audio registrations.
  The public registry-based index constructor remains an adapter for callers.
  Completion/hover/varargs test fixtures use the production descriptor path;
  tests validate every shipped internal declaration against its runtime binding
  and inspect LSP IL to reject calls to runtime registration/engine/audio-manager
  construction. The doc collector also shares `Parse`. `flow check` honors the
  host's configured module search paths through the explicit source provider.
  Verification before the final core gate: 195 frontend/editor tests pass.

- **Interim verification and documentation**: core verifier at `39eb9bc` passes
  the Desktop solution build, **2,871** main tests + **21** MIDI tests, **14** skips,
  zero failures and zero tracked-content changes. LSP stdio smoke boots, responds
  and exits cleanly. `FlowLang.Analysis` is classified as language in the dependency
  decision/baseline; the ratchet remains empty. Updated CLI, architecture, testing
  and API docs to describe the implemented syntax/import scope honestly. Evidence:
  `docs/baselines/phase4/`. Phase 4 remains open: import-aware lexical binding,
  known call arity/name/type diagnostics, richer shared editor analysis and the
  unified evaluation/browser adapters still need work. Resume instructions:
  `docs/plans/handoffs/2026-09-24-phase4-in-progress.md`.

- **C — shared import-aware editor analysis and conservative binding** (2026-09-24):
  check and LSP diagnostics now share module discovery plus lexical-name and
  source-procedure call warnings (arity/names and provable primitive literal
  mismatches). Dynamic values, host overloads/defaults and declaration ordering
  stay explicitly unknown. Editor imports honor unsaved buffers; imported errors
  attach to the use statement with related source spans rather than appearing at
  the wrong document line. Completion/hover use the discovered module descriptors.
  Stdlib visibility follows actual transitive imports and preserves module owners;
  corrected the old test claiming @audio did not bring @collections (it does via
  @std/@core). Verification: 224 analysis, adapter, Phase 17/31 and cancellation
  tests pass; the preceding run also passed 246 including Phase 48 (5 skips).

- **D — explicit evaluation adapters and coded outcomes** (2026-09-24): added
  `FlowEngine.Evaluate(SyntaxTree)` while retaining source-based Evaluate/Execute.
  Results expose coded diagnostics and a distinct HostFailure with host-only
  exception detail; terminal cancellation/failure diagnostics are located and do
  not relabel earlier evaluation errors. CLI/REPL/watch adapters explicitly supply
  session sinks/configuration and consume Evaluate. Browser runs use explicit
  default config and map coded results to the unchanged JSON field shape (parse,
  eval, cancel, runtime categories; snippets use the diagnostic's own source).
  Verification: 104 analysis/adapter tests pass, including source-versus-parsed
  evaluation parity for every language contract, isolated sessions, invalid partial
  syntax and host failures. Frozen JavaScript and website bundle are unchanged.

- **E — process-level tooling gate** (2026-09-24): new
  `scripts/ci/analysis_smoke.py` drives real LSP initialization, core completion,
  hover and clean shutdown. A Linux strace run observes zero sample-file, device,
  or native audio-library accesses; the IL gate additionally rejects sample-cache
  and style-registry construction/calls. Full final verification follows.

- **Final compatibility cleanup** (2026-09-24): editor visibility accepts the
  runtime-supported `@core.flow` spelling as well as `@core`, with an explicit
  regression assertion. Removed the now-unused private WASM diagnostic mapping
  helpers after switching production to coded results. Post-cleanup verification:
  25 binding/completion/import tests and 94 evaluation/browser adapter tests pass.
  This follows the full all-tier gate at `c7476b5` (3,012 main + 21 MIDI, 19 skips,
  zero failures and zero tracked-content mutations).

- **Phase 4 gate closed** (2026-09-24): full all-tier verification at `c7476b5`
  and targeted follow-up verification at `85725de` pass. The real LSP process
  supplies core completion/hover with no sample/device/native audio access in
  strace. The generated trimmed Web bundle boots under Node through the frozen
  adapter, preserves fresh sessions on repeat runs, returns located parse errors
  and evaluates music arithmetic (`150ms`). Seven language examples and all
  zero-error language contracts pass non-executing check without warnings.
  Evidence and limits: `docs/baselines/phase4/`. Source-only warnings deliberately
  leave dynamic/runtime facts unknown; whole-program inference is not claimed.
  Next milestone: Phase 5. Handoff: `docs/plans/handoffs/2026-09-24-phase4-complete.md`.


## Phase 5 in progress — 2026-09-24

- **A — detached evaluated score**: added the BCL-only `Flow.Music.Model`
  assembly (`flow-music-model`) with immutable, collection-owning composition,
  section, sequence, bar and note snapshots. Placements share evaluated sections
  across repeats; timing is quarter-relative with per-section tempo conversion.
  `CompositionCompiler` lowers an already evaluated `SongData` without executing
  or retaining section AST, scopes or runtime values. It preserves rests, parallel
  voice identities, tuplet fractions, onset shifts, articulation, ties, overlap,
  portamento, source origins and resolved tuning frequencies. Compiler IDs are
  deterministic by source identity/structural path, not persistent edit IDs; native
  hosts supply their own IDs. Six snapshot/compilation tests and three language
  dependency-closure tests pass. Rendering still uses the legacy model: this is
  the first boundary slice, not completion of Phase 5.

- **B — linear song-buffer assembly**: string, lambda, automatic-instrument and
  SFZ rendering now retain completed section buffers and copy each output sample
  once into the final allocation, instead of repeatedly copying every prefix.
  Repeats share source storage; final output owns its samples; copies check
  cancellation every 65,536 samples and reject unrepresentable contiguous sizes.
  Four assembly tests cover bit-preserving concatenation, empty/negative repeats,
  ownership, cancellation, and allocation growth. For 128 repeats of 2,048 stereo
  frames, measured allocations drop from 135,302,544 to 2,097,392 bytes (about 64x).
  This still returns one contiguous buffer; streaming/render-job extraction remains
  open. Full audio/corpus/Web verification follows before closing this continuation.

- **Cancellation fixture follow-up**: the first all-tier run passed 3,021 main
  and 21 MIDI tests with 19 skips; its only failure assumed a finite 400-repeat
  render could not finish within 300 ms. Linear assembly invalidated that timing
  assumption. The fixture now renders bounded buffers continuously until its
  deadline; separate chunk-copy tests assert cancellation within assembly.
  All 17 cancellation/model tests pass. Web publish checks also exposed idle
  MSBuild workers retaining output pipes; final verification disables node reuse.
  No tracked-content mutations or audio baseline changes occurred in the first run.

- **Initial-slice verification**: all-tier verifier at `b3834d9` passes the
  Desktop solution build, **3,022 main + 21 MIDI tests**, **19 skips**, zero
  failures and zero tracked-content mutations, including audio/corpus and Web
  publish gates. `MSBUILDDISABLENODEREUSE=1` avoids inherited publish pipes.
  Evidence: `docs/baselines/phase5/`. Phase 5 remains in progress; snapshot-driven
  rendering/export, shared transforms and streaming/render contexts are open.
  Resume: `docs/plans/handoffs/2026-09-24-phase5-in-progress.md`.
  The generated Web bundle also boots through the unchanged JavaScript adapter:
  repeated fresh sessions, located parse errors and `150ms` music arithmetic pass.
