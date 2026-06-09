---
phase: 41-reach-v1-5-closer
plan: 03
subsystem: tooling
tags: [flow-doc, doc-generator, html, markdown, content-hash-cache, DOC-01, DOC-02, cli-verb]

# Dependency graph
requires:
  - phase: 41-02
    provides: "ProcDeclaration.DocComment (/// text bound at parse) + BuiltInDocs.All accessor — the two content sources this generator reads"
provides:
  - "`flow doc` flow-cli verb (sibling to run/test/repl), registered in CommandRegistry (14 subcommands)"
  - "DocCollector: BuiltInDocs.All + harvested ProcDeclaration.DocComment → DocModel[] (charitable, TopDirectoryOnly-bounded)"
  - "DocExampleRunner: in-process /// example execution via fresh FlowEngine per example (hermetic, no second isolation framework)"
  - "HtmlEmitter: category-grouped static HTML, light+dark via prefers-color-scheme, no JS"
  - "MarkdownEmitter: greppable reference.md sibling"
  - "ContentHashCache: per-entry SHA256 skip/regenerate over every rendered input + GeneratorVersion"
  - "DocGenerator: end-to-end pipeline + NormalizeOutDir traversal-confinement (T-41-03-V12)"
affects: [DOC-01, DOC-02, flow-cli, docs/reference]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New CLI verb mirrors TestCommand.Build()/SetAction (System.CommandLine)"
    - "In-process example execution reuses the FlowEngine.Execute + ErrorReporter check from TestCommand:79-107 (D-10 — no new isolation machinery)"
    - "Lex+parse-without-interpret to harvest ProcDeclaration AST (the FlowEngine.Execute lex/parse path stopped before step 4)"
    - "Content-hash cache key covers EVERY rendered input + a bumpable GeneratorVersion (Pitfall 6)"
    - "Path-traversal confinement: honor explicit absolute --out, confine relative ..-escape under base root (boundary-safe, not glob-prefix)"

key-files:
  created:
    - flow-cli/Doc/DocModel.cs
    - flow-cli/Doc/DocCollector.cs
    - flow-cli/Doc/DocExampleRunner.cs
    - flow-cli/Doc/HtmlEmitter.cs
    - flow-cli/Doc/MarkdownEmitter.cs
    - flow-cli/Doc/ContentHashCache.cs
    - flow-cli/Doc/DocGenerator.cs
    - flow-cli/Commands/DocCommand.cs
  modified:
    - flow-cli/Commands/CommandRegistry.cs
    - flow-lang.Tests/flow-lang.Tests.csproj
    - flow-lang.Tests/Integration/Phase41/FlowDocGenTests.cs
    - flow-lang.Tests/Integration/Phase41/DocCacheTests.cs
    - flow-lang.Tests/Integration/Phase41/DocExampleExecTests.cs

key-decisions:
  - "Factored a DocGenerator orchestrator out of DocCommand so FlowDocGenTests can drive the full pipeline in-process without spawning the CLI — the verb is a thin SetAction over DocGenerator.Generate."
  - "Made Doc classes public (not internal) + added a flow-cli ProjectReference to the test project. The three DOC test stubs live in flow-lang.Tests, which did not reference flow-cli; a public API + ProjectReference is the clean seam (referencing an Exe project from a test project is supported)."
  - "NormalizeOutDir honors explicit absolute --out (a composer's deliberate choice — e.g. the plan's /tmp/flowdoc-smoke), and confines only relative ..-traversal under the base root. The V12 threat is a relative --out sneaking out of the tree, not a user picking an absolute dir."
  - "Examples are bare expressions judged by run-without-error (Open Q3 default); audio/MIDI examples pass on successful render, never byte comparison (D-10). A fresh FlowEngine per example IS the isolation (engine-scoped state) — no second framework."

requirements-completed: [DOC-01, DOC-02]

# Metrics
duration: ~35min
completed: 2026-06-08
---

# Phase 41 Plan 03: `flow doc` Documentation Generator Summary

**The `flow doc` flow-cli verb now generates a browsable static HTML reference (`docs/reference/index.html`) + a greppable Markdown sibling from `BuiltInDocs.All` (~104 builtins) and harvested `///` `ProcDeclaration.DocComment`s, executes every `///` code example in-process via a hermetic per-example FlowEngine (failures annotated `[example failed]`, doubling as regression tests), and skips unchanged entries via a content-hash cache — fully delivering DOC-01 + DOC-02 and turning the three 41-01 doc stubs LIVE (12/12 GREEN).**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-06-08T00:26Z
- **Completed:** 2026-06-08
- **Tasks:** 3
- **Files created:** 8 / **modified:** 5

## Accomplishments

- **`flow doc` is a registered flow-cli verb** (`Command("doc", ...)`), sibling to `run`/`test`/`repl`, with `--out` / `--format html|md|both` / `--source` options — mirrors `TestCommand` exactly (D-06). 14 subcommands total.
- **Two content sources, no duplication (D-08):** `DocCollector` enumerates `BuiltInDocs.All` (~104 builtin entries, signature synthesized from the param list) AND lex+parses harvested `.flow` files to read each `ProcDeclaration.DocComment` (the 41-02 field), splitting it into a summary + fenced ``` example blocks. Charitable (D-07): a proc with no `///` yields a signature-only entry; a malformed `.flow` file is parse-skipped, never an error.
- **In-process example execution (DOC-02, D-10):** `DocExampleRunner` runs each example through a fresh `FlowEngine(verbose:false)` + `ErrorReporter` check — the same in-process pattern as `TestCommand:79-107`. A fresh engine per example IS the hermetic isolation (engine-scoped state); no second isolation framework was built. Audio/MIDI examples pass on successful render, never byte comparison. A 30 s per-example wall-clock budget guards against a runaway example (T-41-03-DOS).
- **Browsable static HTML (D-09):** category-grouped single-column reference, light + dark via `prefers-color-scheme`, monospace styled `<pre>` signatures + examples, top-index nav — and **zero `<script>` tags** (no-JS contract). `[example failed]` renders inline. Hand-rolled string templating with deterministic `\n` newlines (diffable). A greppable `reference.md` sibling carries the same grouping + annotations.
- **Content-hash incremental cache (Pitfall 6):** `ContentHashCache` keys each entry on SHA256 of (name + signature + summary + params + example bodies + example failures + `GeneratorVersion`), stored as a `.flowdoc-cache.json` sidecar. A second `flow doc` run with no change skips every entry (smoke: `0 regenerated, 647 unchanged`); editing a `///` comment, a `BuiltInDocs` summary, or an example body regenerates that entry; a `GeneratorVersion` bump invalidates the whole cache.
- **Path-traversal confinement (T-41-03-V12):** `NormalizeOutDir` honors an explicit absolute `--out` (a composer's deliberate choice) and confines a relative `../../etc`-shaped arg under the base root (boundary-safe containment, not a glob prefix). Default `docs/reference`.
- **The three 41-01 DOC stubs are LIVE and GREEN:** `FlowDocGenTests` (3), `DocCacheTests` (4), `DocExampleExecTests` (5) — 12/12, no longer skipped. Phase41 category: 25 passed / 1 skipped (the unrelated SHOWCASE-01 stub) / 0 failed.

## Task Commits

1. **Task 1: DocModel + DocCollector + DocExampleRunner** — `245628f` (feat) — in-process `///` example execution via hermetic per-example FlowEngine; DocExampleExecTests 5/5 GREEN; flow-lang.Tests references flow-cli.
2. **Task 2: HtmlEmitter + MarkdownEmitter + ContentHashCache** — `3570066` (feat) — category-grouped no-JS HTML + greppable Markdown + per-entry SHA256 cache; DocCacheTests 4/4 GREEN.
3. **Task 3: register flow doc verb + wire pipeline + path normalization** — `3d71980` (feat) — DocGenerator orchestrator + DocCommand verb + CommandRegistry registration + traversal-confined `--out`; FlowDocGenTests 3/3 GREEN; CLI smoke produces index.html + reference.md + cache sidecar.

## Files Created/Modified

**Created (flow-cli/Doc/ + flow-cli/Commands/):**
- `DocModel.cs` — `record DocModel(Name, Signature, Summary, Params, Examples, ExampleFailures, Category, Source)` + `DocParam` + `DocSource` enum.
- `DocCollector.cs` — `Collect(flowSourceDirs)`: BuiltInDocs.All + harvested ProcDeclaration.DocComment; `ParseDocComment` fence-splitter; `ClassifyCategory` (CLAUDE.md categories); signature synthesis; TopDirectoryOnly bounding.
- `DocExampleRunner.cs` — `RunAll`/`RunOne`: fresh FlowEngine per example, ErrorReporter check, stdout/stderr suppressed, 30 s budget, render-not-bytes.
- `HtmlEmitter.cs` — `Emit`/`Write` → `index.html`; prefers-color-scheme CSS, category nav, inline `[example failed]`, no JS.
- `MarkdownEmitter.cs` — `Emit`/`Write` → `reference.md`; same grouping + annotations.
- `ContentHashCache.cs` — `Load`/`Decide`/`Save`/`HashFor`/`KeyFor`; JSON sidecar; `GeneratorVersion` invalidation.
- `DocGenerator.cs` — `Generate` pipeline + `NormalizeOutDir` (V12) + `ParseFormat`.
- `DocCommand.cs` — `flow doc` verb (`--out`/`--format`/`--source`), default sources = cwd + bundled stdlib.

**Modified:**
- `flow-cli/Commands/CommandRegistry.cs` — `DocCommand.Build()` added (14 subcommands); comment block updated.
- `flow-lang.Tests/flow-lang.Tests.csproj` — added `<ProjectReference Include="..\flow-cli\flow-cli.csproj" />` so the DOC tests exercise the generator.
- `flow-lang.Tests/Integration/Phase41/{FlowDocGenTests,DocCacheTests,DocExampleExecTests}.cs` — skip-stubs replaced with 3/4/5 live assertions.

## Decisions Made

- **DocGenerator orchestrator factored out of DocCommand.** The verb's `SetAction` is a thin wrapper over `DocGenerator.Generate`, so `FlowDocGenTests` drive the full Collect→Run→cache→emit pipeline in-process without spawning the CLI process. Cleaner + faster than shelling out to `flow doc` from xUnit.
- **Public Doc API + flow-cli ProjectReference from the test project.** The three DOC stubs live in `flow-lang.Tests`, which had no flow-cli reference and no `InternalsVisibleTo`. Making the Doc classes `public` and adding the ProjectReference is the clean integration seam (a test project referencing an Exe project is fully supported in .NET 10).
- **Honor explicit absolute `--out`, confine only relative traversal.** The plan's smoke command (`--out /tmp/flowdoc-smoke`) expects the absolute path honored. The V12 threat is a *relative* `--out` (`../../etc`) escaping the project tree — that case is rebased under the base root; an explicit absolute path is the composer's deliberate, legitimate choice and is written as-is.
- **Bare-expression examples, render-not-bytes (Open Q3 + D-10).** Doc examples are bare expressions that pass on run-without-error; audio/MIDI examples pass on successful render. The fresh-engine-per-example pattern reuses the proven `TestCommand` in-process path — no second isolation framework (D-10 mandate).

## Deviations from Plan

**[Rule 3 — Blocking issue] Test project did not reference flow-cli.**
- **Found during:** Task 1 (writing DocExampleExecTests).
- **Issue:** The three DOC test stubs live in `flow-lang.Tests`, which referenced flow-lang/lsp/midi/interpreter but NOT flow-cli — so the tests could not reach the generator classes the plan places under `flow-cli/Doc/`. The plan's interfaces assumed the generator was test-reachable but did not call out the missing reference.
- **Fix:** Added `<ProjectReference Include="..\flow-cli\flow-cli.csproj" />` to `flow-lang.Tests.csproj` and made the Doc classes `public` (they are a tooling API, not internal plumbing). No alternative install/package needed — this is a project-reference wiring fix.
- **Files modified:** `flow-lang.Tests/flow-lang.Tests.csproj`, all `flow-cli/Doc/*.cs` (public visibility).
- **Commit:** `245628f` (Task 1).

**[Rule 1 — Behavior correction] Absolute-path confinement was initially too aggressive.**
- **Found during:** Task 3 (CLI smoke).
- **Issue:** The first `NormalizeOutDir` confined ALL paths outside cwd — including the plan's own smoke arg `--out /tmp/flowdoc-smoke` (an absolute path), which got rebased to `docs/reference/flowdoc-smoke`. That contradicts the plan's documented smoke expectation (`/tmp/flowdoc-smoke/index.html`) and is over-broad: an explicit absolute path is a deliberate composer choice, not a traversal attack.
- **Fix:** `NormalizeOutDir` now honors explicit absolute paths and confines only relative `..`-escape under the base root. The V12 traversal test (`../../escape`, a relative arg) still confines correctly.
- **Files modified:** `flow-cli/Doc/DocGenerator.cs`.
- **Commit:** `3d71980` (Task 3). Cleaned the accidental `docs/reference/flowdoc-smoke` dir before committing.

## Known Stubs

None. The generator produces real output from real content sources; the only remaining Phase41 skip-stub (`Phase41ShowcaseRmsTests`) belongs to SHOWCASE-01 (a different plan, 41-07) and is out of 41-03 scope.

## Threat Flags

None — no new network endpoints, auth paths, or trust-boundary surface beyond the two boundaries the plan's threat model already covers (`///` example source → in-process FlowEngine; `--out` → filesystem), both mitigated (hermetic per-example engine + 30 s budget; traversal-confined `--out`).

## Verification

- `dotnet build flow-cli` → **Build succeeded, 0 errors**.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~DocExampleExecTests"` → **5 passed, 0 failed**.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~DocCacheTests"` → **4 passed, 0 failed**.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~FlowDocGenTests"` → **3 passed, 0 failed**.
- `dotnet test flow-lang.Tests --filter "Category=Phase41"` → **25 passed, 1 skipped (SHOWCASE-01, out of scope), 0 failed**.
- `dotnet run --project flow-cli -- doc --out /tmp/flowdoc-smoke` → produced `index.html` + `reference.md` + `.flowdoc-cache.json`; second run reported `Cache: 0 regenerated, 647 unchanged`.
- HTML sanity: contains `transpose`, `reverb`, `Audio effects`, `Collections`, `prefers-color-scheme`; `<script` count = 0.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` → **Build succeeded** (no regression — Doc code is flow-cli Desktop-only, never in the WASM closure).

## User Setup Required

None — pure C# tooling over the existing FlowEngine + BuiltInDocs; no new packages, no external service config.

## Next Phase Readiness

- **DOC-01 + DOC-02 legitimately complete:** the generator exists, runs, and produces browsable HTML + Markdown; examples execute as regression tests. The three DOC test stubs are LIVE.
- **For the v1.5 docs build step:** `flow doc` from a repo root harvests the bundled stdlib `.flow` corpus + cwd `.flow` files. Composers can author `///` doc-comments on stdlib procs in a later pass; the generator already renders them (zero-`///` corpus still yields a full builtin reference — charitable).
- No blockers introduced. Remaining Phase 41 work (binaries, WASAPI/CoreAudio, JetBrains, showcase) is untouched by this plan.

## Self-Check: PASSED

- FOUND: `flow-cli/Doc/DocModel.cs`, `DocCollector.cs`, `DocExampleRunner.cs`, `HtmlEmitter.cs`, `MarkdownEmitter.cs`, `ContentHashCache.cs`, `DocGenerator.cs`
- FOUND: `flow-cli/Commands/DocCommand.cs`
- FOUND: commit `245628f` (Task 1 — DocModel/Collector/ExampleRunner)
- FOUND: commit `3570066` (Task 2 — Emitters + cache)
- FOUND: commit `3d71980` (Task 3 — verb + pipeline + path normalization)

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-08*
