# Flow restructuring progress

Roadmap: [Flow restructuring and focused DAW](../2026-09-20-flow-restructuring-roadmap.md).

## Current milestone

**Phase 2 is complete (2026-09-22).** Engines own their state (sinks, advisory
log, config snapshot, render services, tracked resources), evaluations support
cooperative cancellation and budgets, watch mode renders through a
latest-request-wins coordinator, and a process worker provides hard termination.
All four gate items have tests; core, platform and long tiers pass. See the
[Phase 2 record](../../baselines/phase2/README.md). Phase 3 (language-only runtime
and pure standard library) is next and has not started.

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

Phase 0, Phase 1 and Phase 2 gates are met for the documented Linux baseline
(details in the completion sections below). Environmental skips and remaining
warnings are explicit. Hardware/audio listening validation and remote GitHub
Actions execution are not claimed. Phase 3 is the next milestone.

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
- Five tests that asserted advisory dedup across separate engines now pin the
  shared-`AdvisoryLog` contract (advisories are once per engine by default).

Next ready slice: Phase 3 (syntax-level type names, music bindings behind an
interface, stdlib split with compatibility aggregates, language-only build).
