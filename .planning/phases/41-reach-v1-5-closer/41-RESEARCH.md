# Phase 41: Reach + v1.5 Closer - Research

**Researched:** 2026-06-07
**Domain:** Documentation generation (in-process), cross-platform .NET publish, Windows/macOS audio P/Invoke backends, JetBrains plugin signing/publish, multi-genre showcase authoring + determinism
**Confidence:** HIGH (all integration points verified against current source; one external package + JetBrains DSL verified against official docs)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Execution split (D-01..D-05):**
- **D-01 (autonomous, fully completable on Linux):** `flow doc` generator (DOC-01/02), third-genre showcase (SHOWCASE-01), `linux-x64` + `linux-arm64` binaries with runtime smoke-test (BIN-01 subset).
- **D-02 (autonomous code-write, human-verify):** Write `WasapiBackend.cs` (WASAPI-01) + confirm/keep `CoreAudioBackend.cs` (COREAUDIO-01) compile-clean and `IAudioBackend.IsAvailable()`-probe-gated; **cross-compile** `osx-x64`/`osx-arm64`/`win-x64` binaries (produced from Linux without executing) and checksum them. Real-hardware audible verification is a human gate.
- **D-03 (human gate — external account):** JetBrains Marketplace publish (JET-01) needs the composer's JetBrains account + signing cert. Prepare ALL artifacts (plugin.xml metadata, `build.gradle.kts` signing config, CHANGELOG.md, plugin-verifier CI, `docs/jetbrains/install.html` fallback); the actual upload is the human's action.
- **D-04 (human gate — outward-facing publish):** Do NOT cut the v1.5.0 GitHub Release autonomously. Stage release notes + all binary artifacts + SHA-256 checksums; composer pushes the Release.
- **D-05 (human gate — hardware UAT):** Windows WASAPI + macOS CoreAudio audible playback verification, and execution-smoke of osx/win binaries, are HUMAN-UAT rows in `41-HUMAN-UAT.md` (mirror `40-HUMAN-UAT.md` / `49-HUMAN-UAT.md`).

**`flow doc` (D-06..D-10):**
- **D-06:** `flow doc` ships as a **`flow-cli` verb** (lands in `flow-cli/Commands/`, sibling to `run`/`test`/`repl`). Not a new project.
- **D-07:** `///` doc-comment grammar is **additive to `//`** at the lexer (`SimpleLexer.SkipWhitespaceAndComments`): a `///` line emits a doc-comment token bound to the *following* proc declaration; `//` line comments and `/* */` blocks unchanged. Charitable: a proc with no `///` still gets a signature-only doc entry (never an error).
- **D-08:** Content sources = `///` doc-comments + parsed proc signatures + **`flow-lang/StandardLibrary/BuiltInDocs.cs`** builtin metadata. No duplication — generator reads BuiltInDocs directly.
- **D-09:** Output = **browsable static HTML** at `docs/reference/index.html` (default) **plus a Markdown sibling**. Content-hash incremental cache. Static HTML only — no search/interactive JS for v1.5.
- **D-10 (DOC-02):** Code examples inside `///` execute via the Phase 35 TEST-01 hermetic test framework. Failures surface as `[example failed]` annotations AND double as regression tests. Audio/MIDI examples checked for successful render, not byte output (platform-portable).

**Showcase (D-11..D-13):**
- **D-11 (RECOMMENDED DEFAULT — composer confirms/overrides at plan time):** **EDM.** Composer should confirm before execution.
- **D-12 (alternatives, equally valid):** Death metal (boldest genre-agnostic proof); Jazz (overlaps existing `examples/generative/markov_jazz.flow` — showcases less new ground).
- **D-13:** ~60s curated piece at `examples/<genre>/<piece>.flow`; README.md `## Showcase` v1.5 section embeds inline-audio; WAV + MIDI offline render must hold two-run cmp-clean + RMS-windowed regression (±0.5 dB / 100 ms per SPEC-8).

**Binaries (D-14..D-16):**
- **D-14:** `dotnet publish -p:PublishSingleFile=true --self-contained -r <rid>` for all 5 RIDs, extending **`scripts/publish.sh`**. All 5 build autonomously; only linux-x64/arm64 runtime-smoke-tested here.
- **D-15:** **No trimming for v1.5.** Reflection-heavy `InternalFunctionRegistry` makes `PublishTrimmed` a runtime-breakage hazard. Trimming + source-gen registry is a v1.6 item.
- **D-16:** Artifact naming: `flow-<rid>-v1.5.0.tar.gz` (Linux/macOS) + `flow-win-x64-v1.5.0.zip` (Windows), alongside existing `flow-linux-x64.tar.gz`. Each ships with a `.sha256`. Framework-dependent = false (self-contained).

**Audio backends (D-17..D-18):**
- **D-17 (Windows):** Single `flow-lang/Audio/WasapiBackend.cs` implementing `IAudioBackend` via **NAudio.Wasapi 2.3.0**, structured after `PulseAudioSimpleBackend.cs`. Shared-mode default; exclusive-mode opt-in via config flag (Phase 30 `config.toml`). Desktop-only → Web-stripped + `IsAvailable()`-probe-gated.
- **D-18 (macOS):** Existing hand-rolled `CoreAudioBackend.cs` **is the shipping path**. OwnAudioSharp 1.0.68 evaluation **deferred unless** the human Mac smoke-test shows >20 ms round-trip latency. NAudio.Wasapi PackageReference MUST be Desktop-only (Web `AssemblyReferenceScanTests` forbidden-prefix gate).

**Bookkeeping (D-19):** Reconcile WASM-01/02/03 traceability (mark Shipped/carved to 47-49, not Pending/41) and stale ROADMAP progress-table rows (Phase 39 "Not started", Phase 48 "In Progress 5/7" — both shipped).

### Claude's Discretion
- Exact HTML template/styling for `flow doc` output (functional, readable; no design contract — this is a reference site, not the flowlang.dev marketing site).
- Internal structure of the doc-comment AST attachment and the content-hash cache key.
- Specific Euclidean/generative/DSP knobs in the showcase piece (composer owns the *musical* content; Claude owns the *scaffolding* that proves the feature checklist).

### Deferred Ideas (OUT OF SCOPE)
- OwnAudioSharp 1.0.68 swap for macOS — only if human Mac smoke-test shows >20 ms round-trip latency.
- Binary trimming + source-generated `InternalFunctionRegistry` — v1.6.
- `flow doc` search / interactive site features — v1.6.
- Custom flowlang.dev domain + Phase 49 live deploy / OAuth / cross-browser audio — Phase 49's own gates.
- Phase 40 hardware UAT — Phase 40's human gate.

### EXPLICITLY OUT OF SCOPE (do NOT research or plan)
**WASM-01 / WASM-02 / WASM-03** were carved out of Phase 41 on 2026-05-25 and SHIPPED by Phases 47/48/49 (compile-target conditioning + Mono-WASM runtime + SvelteKit playground). Phase 41 does NOT build a WASM playground. No Blazor WebAssembly, no KristofferStrube.Blazor.WebAudio, no browser audio. One bookkeeping note only (D-19): REQUIREMENTS.md still lists WASM-01/02/03 as "Pending/Phase 41" — reconcile to "Shipped (carved to 47-49)" (doc-edit, not implementation work).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOC-01 | `flow doc` generator: `///` doc-comments + proc signatures + BuiltInDocs → HTML + Markdown; content-hash cache | §`flow doc` Architecture — lexer insertion point, ProcDeclaration.DocComment field, BuiltInDocs shape, cache key design |
| DOC-02 | `flow doc` example execution via TEST-01 hermetic runner; `[example failed]` annotations | §`flow doc` Example Execution — TestRunner in-process API, SnapshotState/RestoreState, render-not-bytes check |
| BIN-01 | 5-RID self-contained single-file binaries from Linux host | §Cross-Platform Binaries — verified cross-compile matrix, publish.sh generalization, no-trim rationale, P/Invoke system-lib confirmation |
| WASAPI-01 | Windows backend via NAudio.Wasapi 2.3.0, shared+exclusive mode | §WASAPI Backend — WasapiOut pull-model API, BufferedWaveProvider bridge, DetectBackend slot, Web-strip discipline |
| COREAUDIO-01 | macOS backend (existing CoreAudioBackend.cs shipping path) | §CoreAudio Backend — code audit (complete/correct), smoke-test checklist, OwnAudioSharp fallback scope |
| JET-01 | JetBrains Marketplace publish from Phase 31 scaffolding | §JetBrains Publish — verified 2.x signing/publishing/verification DSL, autonomous-vs-human artifact split |
| SHOWCASE-01 | Third-genre ~60s piece exercising 5 feature primitives + determinism | §Showcase — verified feature syntax, existing showcase style, RMS pinning, two-run harness |

**Bookkeeping (D-19, not a REQ but owed):** Reconcile WASM-01/02/03 → "Shipped (carved to 47-49)"; fix stale ROADMAP progress-table rows.
</phase_requirements>

## Summary

Phase 41 is the v1.5 closer. It is unusual among Flow phases in that **most of its surface is integration glue and packaging, not new language semantics** — the language is essentially complete after Phase 40. The six requirement clusters split cleanly into (a) fully-autonomous work on this Linux box (`flow doc`, showcase, linux binaries), (b) autonomous code-write/human-verify (WASAPI backend, CoreAudio confirm, cross-compiled osx/win binaries), and (c) pure human gates (Marketplace publish, GitHub Release, real-hardware audible UAT). The CONTEXT decisions D-01..D-05 lock this split; research confirms every autonomous item is mechanically reachable from the existing codebase with no architectural surprises.

The two areas needing the most care are **`flow doc`** (the only piece touching the language core — a lexer/parser change to bind `///` doc-comments to `ProcDeclaration`, plus an in-process invocation of the Phase 35 test runner to execute examples) and **WASAPI-01** (one new desktop-only `WasapiBackend.cs` over NAudio.Wasapi 2.3.0, which must follow the exact Phase 47 Web-strip discipline that CoreAudio/PulseAudio already follow). Both have proven templates in-repo: `flow doc` mirrors the `flow test` CLI verb + the `IsStrict`/`IsBeatTrueToSig` pragma-bit threading pattern for the doc-comment field; `WasapiBackend.cs` mirrors `PulseAudioSimpleBackend.cs` structurally and slots into `AudioPlaybackManager.DetectBackend` exactly where the OSX branch already sits.

The existing `CoreAudioBackend.cs` is **complete, correct, and ready to ship** (full AudioQueue push-mode implementation with `IsAvailable()` DllNotFoundException probe, free-buffer pool, drain-on-Play). COREAUDIO-01 is verify-not-build; the OwnAudioSharp swap is a deferred conditional. Cross-compiling all 5 RIDs from Linux works because Flow's binaries are managed-only — the audio backends P/Invoke **system** libraries (libpulse, AudioToolbox.framework, the WASAPI COM stack via NAudio) that are never bundled, so there is no native cross-toolchain requirement.

**Primary recommendation:** Sequence the phase `flow doc` first (only language-core change, purely additive), then binaries + WASAPI backend (packaging, parallelizable), then showcase last (consumes everything), with JetBrains artifacts and HUMAN-UAT files prepared alongside. Pin the showcase render with the existing `RmsRegressionTests.AssertRmsWithinTolerance` + `scripts/test_two_run_determinism.sh`. Treat `NAudio.Wasapi` as the single new dependency and add it to the `AssemblyReferenceScanTests` forbidden-prefix gate at the same commit that adds the PackageReference.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `///` doc-comment lexing | Lexer (`SimpleLexer`) | Parser (binds to ProcDeclaration) | Doc-comments are source tokens; binding to the following decl is a parse-time concern (mirrors pragma-bit capture) |
| Doc-comment → AST attachment | Parser | AST (`ProcDeclaration` new field) | Same pattern as `IsStrict`/`IsBeatTrueToSig` — capture at parse, read later |
| Doc HTML/Markdown emit | CLI generator (`flow-cli/Commands/DocCommand.cs` + helper class) | — | Output is a packaging/tooling concern, not language semantics; lives in flow-cli per D-06 |
| Doc example execution | CLI generator → `FlowLang.StandardLibrary.TestFramework.TestRunner` | FlowEngine (per-engine isolation) | Reuses Phase 35 hermetic runner in-process; no new isolation machinery (D-10) |
| Windows audio output | `flow-lang/Audio/WasapiBackend.cs` (IAudioBackend impl) | NAudio.Wasapi (WASAPI COM) | Desktop-only P/Invoke-equivalent tier; same tier as Pulse/CoreAudio |
| macOS audio output | `flow-lang/Audio/CoreAudioBackend.cs` (existing) | AudioToolbox.framework | Already shipping; verify-not-build |
| Backend selection | `flow-lang/Audio/AudioPlaybackManager.DetectBackend` | OS probe (`RuntimeInformation.IsOSPlatform`) | Single dispatch point; Windows branch slots beside OSX branch |
| Binary packaging | `scripts/publish.sh` (build host tooling) | dotnet SDK publish pipeline | Pure tooling tier; produces RID-specific self-contained artifacts |
| JetBrains plugin build/sign/publish | `flow-jetbrains/build.gradle.kts` (Gradle/JVM tier) | JetBrains Marketplace (external service) | Separate JVM ecosystem; signing/publish are external-account gated |
| Showcase composition | `.flow` source (composer/language tier) | All Phase 35-40 stdlib surface | Pure language-level artifact; exercises but does not modify the engine |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| NAudio.Wasapi | 2.3.0 | Windows WASAPI audio output backend | Locked in D-17/WASAPI-01; de-facto .NET Windows audio lib; `WasapiOut` exposes shared+exclusive mode via `AudioClientShareMode` [VERIFIED: nuget.org/packages/NAudio.Wasapi — 2.3.0 is a published stable release] |
| (existing) Melanchall.DryWetMidi | 8.0.3 | Offline MIDI file I/O (showcase `writeMidi`) | Already referenced; cross-compiles cleanly to all RIDs (verified WASM-compatible Phase 47) [VERIFIED: flow-lang.csproj] |
| (existing) Pidgin | 3.5.1 | Referenced but unused (manual lexer/parser) | Already present; no Phase 41 change [VERIFIED: flow-lang.csproj] |

### Supporting (no new packages — these are SDK/tooling capabilities, not NuGet deps)
| Capability | Version | Purpose | When to Use |
|------------|---------|---------|-------------|
| `dotnet publish` single-file | .NET 10 SDK (10.0.108 installed) | 5-RID self-contained binaries | BIN-01 — cross-compiles from Linux host [VERIFIED: dotnet --version → 10.0.108] |
| IntelliJ Platform Gradle Plugin | 2.2.0 (already pinned) | Plugin build/sign/verify/publish | JET-01 — `signPlugin`/`publishPlugin`/`verifyPlugin` tasks [VERIFIED: flow-jetbrains/build.gradle.kts + plugins.jetbrains.com docs] |
| `System.CommandLine` | (already used) | `flow doc` verb registration | DOC-01 — mirrors `TestCommand.Build()` [VERIFIED: flow-cli/Commands/*.cs] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| NAudio.Wasapi (Windows) | Hand-rolled WASAPI COM P/Invoke | NAudio is locked (D-17) and handles WASAPI's awkward COM/event-sync lifecycle; hand-rolling would mirror CoreAudio effort for no benefit and isn't justified for a single platform |
| Existing CoreAudioBackend (macOS) | OwnAudioSharp 1.0.68 (miniaudio) | DEFERRED (D-18) — only swap if human Mac smoke-test shows >20 ms round-trip. Hand-rolled AudioQueue already exists and is complete |
| Static HTML doc output | DocFX / docusaurus | Over-engineered for a ~200-builtin reference; D-09 locks hand-rolled static HTML + Markdown, no JS, no search for v1.5 |
| `///` as a new TokenType | Reuse existing comment skip + side-channel collection | Either works; recommend a dedicated `DocComment` token (cleaner parser binding). See §`flow doc` Architecture for both options |

**Installation:**
```bash
# The ONLY new package — added to flow-lang.csproj under the Desktop-only (FlowTarget != 'Web') ItemGroup:
#   <PackageReference Include="NAudio.Wasapi" Version="2.3.0" />
# No npm/pip — this is a .NET phase. NAudio.Wasapi is Windows-only at runtime but the
# package restores on Linux (the assembly is reference-able; the COM calls only fire on Windows).
```

**Version verification:** `NAudio.Wasapi` flat-container index confirms `2.3.0` is a published stable release (versions list: `2.0.0`, `2.1.0`, `2.2.1`, `2.3.0`, then `3.0.0-preview.*` and an anomalous `22.0.0`). Pin exactly `2.3.0` — do NOT use a `+` range that could resolve to the suspicious `22.0.0`. [VERIFIED: api.nuget.org/v3-flatcontainer/naudio.wasapi/index.json]

## Package Legitimacy Audit

> Phase 41 installs exactly ONE new external package: `NAudio.Wasapi 2.3.0`.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| NAudio.Wasapi | NuGet | 2.3.0 is part of the mature NAudio 2.x line (NAudio itself is 10+ yrs) | Very high (NAudio is the canonical .NET audio lib) | github.com/naudio/NAudio | unavailable (CLI lacks `install` verb here) | Approved — pin `2.3.0` exactly |

**slopcheck status:** `slopcheck 0.6.1` is installed but its CLI did not expose the `install` subcommand expected by the protocol in this environment. Per the graceful-degradation rule, NAudio.Wasapi would normally be tagged `[ASSUMED]`. However, it is independently verified via: (1) the official `github.com/naudio/NAudio` source repo (the WasapiOut.cs file is in that repo), (2) the NuGet flat-container index confirming `2.3.0` as a stable published version under the `naudio` org, and (3) the package is explicitly locked by CONTEXT D-17 and REQUIREMENTS WASAPI-01 — it is a user-chosen dependency, not a Claude-discovered one. **Recommendation:** the planner SHOULD still gate the install behind a single `checkpoint:human-verify` task confirming the PackageReference reads exactly `<PackageReference Include="NAudio.Wasapi" Version="2.3.0" />` (not a range, not `22.0.0`), because the registry contains an anomalous `22.0.0` version that a careless `latest`/`+` resolution could pull.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none (but the `22.0.0` registry entry is a version-pin trap — pin `2.3.0` exactly)

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────── flow doc (DOC-01/02) ───────────────────────┐
                          │                                                                      │
  .flow source ──► SimpleLexer ──► [/// DocComment token] ──► Parser ──► ProcDeclaration.DocComment
                          │                                                  │                   │
  BuiltInDocs.cs ─────────┼──────────────────────────────────────────────►  │                   │
   (~104 builtins)        │                                                  ▼                   │
                          │                                          DocModel (name, sig,        │
                          │                                          summary, params, examples)  │
                          │                                                  │                    │
                          │                          ┌───────────────────────┼─────────────┐     │
                          │                          ▼                       ▼              ▼     │
                          │                  content-hash cache       HTML emitter   Markdown emit│
                          │                  (skip unchanged)         docs/reference/  docs/...md  │
                          │                          │                                            │
                          │            ``` example ``` blocks ──► TestRunner.Run(fresh FlowEngine)│
                          │                          │            (Snapshot/Restore per example)  │
                          │                          ▼                                            │
                          │              pass → runnable docs ; fail → [example failed] annotation│
                          └──────────────────────────────────────────────────────────────────────┘

                          ┌──────────────── cross-platform audio (WASAPI-01 / COREAUDIO-01) ─────┐
  (play buf) ──► AudioPlaybackManager.DetectBackend ──► OS probe                                  │
                          │                               ├─ IsBrowser → WebAudioBackend (P48)     │
                          │                               ├─ OSX + CoreAudioBackend.IsAvailable() ─┼─► AudioQueue
                          │                               ├─ Windows + WasapiBackend.IsAvailable()─┼─► WASAPI (NAudio)
                          │                               └─ PulseAudioSimpleBackend.IsAvailable()─┼─► libpulse
                          └──────────────────────────────────────────────────────────────────────┘

                          ┌──────────────────── packaging (BIN-01) ────────────────────────────┐
  scripts/publish.sh ──► for rid in linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64:            │
                          dotnet publish flow-cli -r $rid --self-contained -p:PublishSingleFile │
                          ──► flow-<rid>-v1.5.0.{tar.gz|zip} + .sha256                           │
                          (linux-* smoke-run here; osx-*/win-x64 = HUMAN-UAT exec smoke)         │
                          └──────────────────────────────────────────────────────────────────────┘

                          ┌──────────────────── JetBrains (JET-01) ────────────────────────────┐
  flow-jetbrains/ ──► gradle buildPlugin ──► verifyPlugin (recommended IDEs)                    │
                       ──► [signPlugin (CERTIFICATE_CHAIN/PRIVATE_KEY)] ──► [publishPlugin]      │
                       autonomous: build + verify + config + CHANGELOG ; HUMAN: sign + publish   │
                          └──────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure (new/touched files only)
```
flow-lang/
├── Lexing/SimpleLexer.cs          # MODIFY: SkipWhitespaceAndComments — emit /// DocComment token
├── Lexing/TokenType.cs            # MODIFY: add DocComment token type
├── Parsing/Parser.cs              # MODIFY: collect pending DocComment before proc; thread into ProcDeclaration
├── Ast/Statements/ProcDeclaration.cs  # MODIFY: add `string? DocComment = null` trailing field
└── Audio/
    └── WasapiBackend.cs           # NEW: IAudioBackend over NAudio.Wasapi WasapiOut
flow-cli/
├── Commands/DocCommand.cs         # NEW: `flow doc [--out dir] [--format html|md|both]` verb
├── Commands/CommandRegistry.cs    # MODIFY: add DocCommand.Build()
└── Doc/                           # NEW (Claude's discretion on internal structure)
    ├── DocModel.cs                # name/signature/summary/params/examples record
    ├── DocCollector.cs            # walks parsed AST + BuiltInDocs → DocModel[]
    ├── DocExampleRunner.cs        # invokes TestRunner in-process per example
    ├── HtmlEmitter.cs             # DocModel[] → docs/reference/index.html
    ├── MarkdownEmitter.cs         # DocModel[] → docs/reference/*.md
    └── ContentHashCache.cs        # per-entry hash → skip unchanged
flow-jetbrains/
├── build.gradle.kts              # MODIFY: add signing{} + publishing{} + pluginVerification{} blocks
├── src/main/resources/META-INF/plugin.xml  # MODIFY: <change-notes>, untilBuild verification
└── CHANGELOG.md                  # NEW
docs/
├── reference/                    # NEW: flow doc output target
└── jetbrains/install.html        # NEW: direct-download fallback page (D-03)
examples/<genre>/<piece>.flow     # NEW: showcase (D-13)
scripts/publish.sh               # MODIFY: 5-RID loop + tar/zip + .sha256
.planning/phases/41-.../41-HUMAN-UAT.md  # NEW: human gates (D-05)
```

### Pattern 1: Doc-comment binding via parse-time pragma-bit precedent
**What:** The codebase already threads file-scoped metadata (`enable strict;` → `IsStrict`, `enable beat-true-to-sig;` → `IsBeatTrueToSig`) onto every `ProcDeclaration` at parse time. The `///` doc-comment binding is the same shape: collect the comment text in the lexer/parser, attach it to the following declaration.
**When to use:** DOC-01 doc-comment attachment.
**Example:**
```csharp
// Source: flow-lang/Ast/Statements/ProcDeclaration.cs:62 (current shape)
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal,
    Span? Span = null,
    bool IsStrict = false,
    bool IsBeatTrueToSig = false) : Statement(Location);
//  ^ ADD a trailing `string? DocComment = null` — defaulted-trailing preserves
//    binary back-compat with every existing positional construction site
//    (the exact rationale the IsStrict/IsBeatTrueToSig XML-doc cites).
```
```csharp
// Source: flow-lang/Parsing/Parser.cs:411 (current return)
return new ProcDeclaration(
    location, name, parameters, body, isInternal,
    Span: new Span(location, PreviousToken.Location),
    IsStrict: _pragmaSet?.Has("strict") ?? false,
    IsBeatTrueToSig: _pragmaSet?.Has("beat-true-to-sig") ?? false);
//  ^ ADD: DocComment: _pendingDocComment (cleared after consume)
```

### Pattern 2: New CLI verb mirrors TestCommand
**What:** Every `flow` subcommand is a static `XxxCommand.Build() → Command` registered in `CommandRegistry.BuildAllCommands()`. `flow doc` is one more entry.
**When to use:** DOC-01 verb registration (D-06).
**Example:**
```csharp
// Source: flow-cli/Commands/CommandRegistry.cs:16 (current array)
return new[]
{
    RunCommand.Build(), EvalCommand.Build(), ReplCommand.Build(),
    WatchCommand.Build(), PlayCommand.Build(), RenderCommand.Build(),
    Flow2MidiCommand.Build(), Midi2FlowCommand.Build(), CheckCommand.Build(),
    VersionCommand.Build(), NewCommand.Build(), LspCommand.Build(),
    TestCommand.Build(),
    // ADD: DocCommand.Build(),  // Phase 41 DOC-01
};
```

### Pattern 3: In-process example execution via TestRunner
**What:** The Phase 35 `TestRunner.Run(FlowEngine, filePath)` walks the engine's `TestRegistry`, wrapping each body in `SnapshotState`/`RestoreState` for hermetic isolation. `flow doc` reuses this exactly: for each `///` example, construct a fresh `FlowEngine`, execute the example source, and capture pass/fail.
**When to use:** DOC-02 example execution (D-10).
**Example:**
```csharp
// Source: flow-cli/Commands/TestCommand.cs:79-107 (proven in-process pattern)
var runner = new TestRunner();
using var engine = new FlowEngine(verbose: false);
var executeOk = engine.Execute(source, file);   // returns bool; accumulates errors on engine.ErrorReporter
if (!executeOk) { /* [example failed] — render engine.ErrorReporter.FormatErrors() */ }
// For doc examples that aren't (test ...) blocks, wrap the snippet in execute-and-check-no-error.
// Per D-10: audio/MIDI examples check for SUCCESSFUL RENDER (no error), not byte output, to stay portable.
```
`TestRunner.Run` returns `(int passed, int failed)`; for plain (non-`test`-block) doc snippets, prefer the simpler "execute returns true AND ErrorReporter is empty" check. [VERIFIED: flow-cli/Commands/TestCommand.cs + flow-lang/StandardLibrary/TestFramework/TestRunner.cs:36]

### Pattern 4: New audio backend mirrors PulseAudioSimpleBackend + slots into DetectBackend
**What:** Every `IAudioBackend` has a `static bool IsAvailable()` that catches `DllNotFoundException` (Pulse/CoreAudio both do). `DetectBackend` probes them in OS-priority order. `WasapiBackend` adds a Windows branch beside the OSX branch.
**When to use:** WASAPI-01 (D-17).
**Example:**
```csharp
// Source: flow-lang/Audio/AudioPlaybackManager.cs DetectBackend (current)
if (WebAudioBackend.IsAvailable()) return new WebAudioBackend();
#if !FLOW_WEB
if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
    if (CoreAudioBackend.IsAvailable()) return new CoreAudioBackend();
}
// ADD (before the PulseAudio probe):
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
    if (WasapiBackend.IsAvailable()) return new WasapiBackend();
}
if (PulseAudioSimpleBackend.IsAvailable()) return new PulseAudioSimpleBackend();
#endif
```

### Anti-Patterns to Avoid
- **Building a WASM playground.** OUT OF SCOPE — shipped by Phases 47-49. Any plan task that mentions Blazor, KristofferStrube, AudioWorklet, or browser audio for Phase 41 is wrong.
- **Enabling PublishTrimmed for BIN-01.** D-15 forbids it for v1.5 — the reflection-heavy `InternalFunctionRegistry` would silently break at runtime. The existing `publish.sh` already sets `-p:PublishTrimmed=false`; keep it.
- **Hand-rolling a doc isolation framework.** D-10 mandates reuse of the Phase 35 TestRunner; there is no second isolation machinery to build.
- **Adding NAudio.Wasapi to the unconditional ItemGroup.** It MUST go under the `Condition="'$(FlowTarget)' != 'Web'"` ItemGroup AND be added to `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes`, or the Web build drifts.
- **Byte-comparing audio doc examples across platforms.** D-10 says check render success, not bytes — audio output is platform-portable only at the RMS level, not byte level, off-Linux.
- **Faking the human gates.** D-02/D-03/D-04/D-05 + `feedback_autonomous_phase_execution`: write honest `41-HUMAN-UAT.md` rows, never a fabricated pass.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Doc-example hermetic isolation | Custom per-example reset machinery | Phase 35 `TestRunner` + `FlowEngine.SnapshotState/RestoreState` | D-10 mandate; the 11+ pieces of state (musical context, voice pool, PRNG, bindings) are already reset by the proven runner |
| Windows WASAPI COM lifecycle | Hand-rolled `IAudioClient`/`IAudioRenderClient` P/Invoke | NAudio.Wasapi `WasapiOut` + `BufferedWaveProvider` | WASAPI's event-sync + COM-apartment + format-negotiation is famously error-prone; NAudio is locked (D-17) and handles it |
| macOS audio output | New miniaudio binding (OwnAudioSharp) speculatively | Existing `CoreAudioBackend.cs` | D-18 — it's complete + correct; swap only on a real >20 ms latency failure |
| Single-file binary assembly | Custom ILMerge/bundler | `dotnet publish -p:PublishSingleFile=true` | Native .NET 10 SDK capability; cross-compiles all 5 RIDs from Linux |
| Plugin signing/upload | Custom JAR signer + Marketplace REST calls | IntelliJ Gradle plugin `signPlugin`/`publishPlugin` | Already pinned (2.2.0); env-var-driven cert/token is the canonical path |
| CLI arg parsing for `flow doc` | Manual arg loop | `System.CommandLine` (already used) | Mirror `TestCommand`'s `Argument`/`Option` shape |

**Key insight:** Phase 41 is overwhelmingly *integration*, not *invention*. The two new code artifacts (`WasapiBackend.cs`, the `flow doc` generator) both have exact in-repo templates. The phase's risk is in *discipline* (Web-strip gate, no-trim, honest human gates, determinism pinning), not in solving hard problems.

## Runtime State Inventory

> Phase 41 includes a string-replacement/bookkeeping sub-task (D-19 reconciliation), but it touches only `.planning/` tracking docs — no runtime state. The audio/doc/binary work is additive. This section is included for completeness because D-19 is a doc-edit-class change.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — `flow doc` writes new files under `docs/reference/`; no datastore keys change. Content-hash cache is a NEW artifact (no migration). | None |
| Live service config | None — no external services reconfigured. JetBrains Marketplace publish is a NEW upload (human gate), not a reconfiguration. | None |
| OS-registered state | None — self-contained binaries are new artifacts; no OS task/service registration. | None |
| Secrets/env vars | NEW (not a rename): JetBrains signing needs `CERTIFICATE_CHAIN`/`PRIVATE_KEY`/`PRIVATE_KEY_PASSWORD`/`PUBLISH_TOKEN` env vars at the human's publish time. These are NOT committed; documented in the runbook only. | Document in JET-01 runbook; human supplies |
| Build artifacts | The existing `publish/flow-linux-x64/` dir is regenerated by `publish.sh`; Phase 41 adds 4 more RID output dirs + tar/zip artifacts. Stale `bin/Release/net10.0/<rid>/` obj dirs from prior single-RID publishes are harmless (cleaned per-RID by the `rm -rf` in publish.sh). | publish.sh `rm -rf` per RID output dir (already pattern) |

**Nothing found in categories Stored data / Live service config / OS-registered state** — verified: Phase 41's only mutation of existing state is the D-19 `.planning/` doc reconciliation (REQUIREMENTS.md WASM-01/02/03 status lines + 2 stale ROADMAP progress-table rows), which is a tracked-doc edit with no runtime footprint.

## Common Pitfalls

### Pitfall 1: `///` lexing collides with `//` line comments
**What goes wrong:** A naive `c == '/' && PeekNext() == '/'` arm consumes `///` as a normal `//` line comment, and the doc-comment text is lost.
**Why it happens:** `SkipWhitespaceAndComments` checks `//` first; `///` starts with `//`.
**How to avoid:** Check `///` (three slashes) BEFORE the two-slash arm. The existing lexer already orders multi-char before single-char checks (e.g. `~>` before `~`, `...` before `..`) — follow that precedent: a `c == '/' && PeekNext() == '/' && PeekAt(2) == '/'` branch must precede the `//` branch. Capture to end-of-line, store as a pending doc-comment (NOT skipped), and only the `///` form is captured; `//` and `/* */` stay byte-for-byte unchanged.
**Warning signs:** Existing `//`-comment tests change behavior; `/* */` block handling breaks. Add a regression test asserting `//` and `/* */` are unaffected.

### Pitfall 2: Doc-comment binding leaks across non-proc statements
**What goes wrong:** A `///` comment far above a proc (with blank lines / other statements between) wrongly attaches, or a `///` with no following proc orphans.
**Why it happens:** The pending-doc-comment buffer isn't cleared on intervening tokens.
**How to avoid:** Clear the pending doc-comment when any non-doc-comment, non-whitespace token other than the immediately-following `proc` is seen. Charitable rule (D-07): an orphaned `///` (no following proc) is silently dropped — never an error. Recommend binding ONLY when `proc`/`internal proc` immediately follows the doc-comment block.
**Warning signs:** Generated docs show a summary on the wrong builtin.

### Pitfall 3: NAudio.Wasapi leaks into the Web build
**What goes wrong:** Adding the PackageReference unconditionally pulls a Windows-COM type-reference into the WASM closure; `AssemblyReferenceScanTests` (Web target) goes RED, OR worse, isn't updated and the drift ships silently.
**Why it happens:** Forgetting the Phase 47 Web-strip discipline that Pulse/CoreAudio/Rug.Osc/RtMidi all follow.
**How to avoid:** (1) PackageReference under `<ItemGroup Condition="'$(FlowTarget)' != 'Web'">`. (2) `<Compile Remove="Audio\WasapiBackend.cs" />` under the `Condition="'$(FlowTarget)' == 'Web'"` ItemGroup. (3) `#if !FLOW_WEB` guard at the `WasapiBackend` instantiation in `DetectBackend`. (4) Add `"NAudio"` (or `"NAudio.Wasapi"`) to `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` in the SAME commit. (5) Keep `dotnet build flow-lang -p:FlowTarget=Web` green as the gate.
**Warning signs:** Web build fails or the forbidden-prefix scan doesn't list NAudio.

### Pitfall 4: Cross-compiled osx/win binaries silently fail at first audio call
**What goes wrong:** The binary builds + checksums fine from Linux but the human's macOS/Windows smoke-test crashes on `(play ...)` because a P/Invoke entry-point is wrong.
**Why it happens:** Cross-compile produces the managed assembly correctly, but native interop can only be exercised on the target OS.
**How to avoid:** This is exactly why D-02/D-05 split "code lands + cross-compile" (autonomous) from "audible verification" (human). The plan MUST write honest `41-HUMAN-UAT.md` rows for each of: Windows WASAPI audible, macOS CoreAudio audible, osx-x64 exec smoke, osx-arm64 exec smoke, win-x64 exec smoke. Do NOT mark these passed.
**Warning signs:** A plan task claims osx/win audio "verified" on the Linux box.

### Pitfall 5: Showcase render isn't deterministic (uses live block or unseeded PRNG)
**What goes wrong:** The showcase `.flow` includes a `live` block or an unseeded generative call in the OFFLINE render path, breaking two-run cmp-clean.
**Why it happens:** D-v1.5-07 — `live` blocks explicitly opt OUT of determinism; an unseeded `(markov ...)`/`(jam ...)` reseeds per render unless routed through PrngRegistry at the writeWav boundary.
**How to avoid:** The feature-checklist `live` block belongs in a **playback/demo** section of the showcase, NOT in the `writeWav`/`writeMidi` offline render that gets pinned. Seed every generative primitive in the rendered path (the existing `examples/generative/markov_jazz.flow` pattern: `(markov corpus 2 16 42)` with explicit seed). Pin with `scripts/test_two_run_determinism.sh examples/<genre>/<piece>.flow` + an `RmsRegressionTests.AssertRmsWithinTolerance` baseline. SHOWCASE-01's "real-time MIDI" requirement is also a playback-path demonstration, not an offline-render artifact (offline MIDI is `writeMidi` → DryWetMidi, deterministic).
**Warning signs:** Two consecutive renders produce different SHA-256s; the determinism script exits 1.

### Pitfall 6: `flow doc` content-hash cache produces stale output
**What goes wrong:** Re-running `flow doc` after editing a `///` comment doesn't regenerate that entry because the cache key doesn't include the doc-comment text.
**Why it happens:** Hashing only the proc signature, not the full doc-comment + example bodies + BuiltInDocs entry.
**How to avoid:** Cache key = hash of (proc signature + doc-comment text + example bodies + the relevant BuiltInDocs entry + generator version). Bump a generator-version constant whenever the emitter template changes so a template change forces full regen. Claude's discretion on the exact key (D-09) — but it MUST cover every input that affects the rendered entry.
**Warning signs:** Edited docs don't appear after re-gen; a `--no-cache`/`--force` flag becomes necessary as a workaround.

## Code Examples

Verified patterns from current sources and official docs:

### Lexer: order the `///` check before `//` (insertion site)
```csharp
// Source: flow-lang/Lexing/SimpleLexer.cs SkipWhitespaceAndComments (current `//` arm)
//   else if (c == '/' && PeekNext() == '/') { /* line comment: skip to EOL */ }
// INSERT BEFORE that arm (note: SkipWhitespaceAndComments currently SKIPS comments;
// for /// you must CAPTURE, so either: (a) collect into a _pendingDocComment field here,
// or (b) introduce a DocComment TokenType emitted from NextToken. Option (b) is cleaner
// for parser binding — recommended.):
//   else if (c == '/' && PeekNext() == '/' && CharAt(_position + 2) == '/') {
//       // capture /// ... to end of line as doc-comment text (strip leading ///), DON'T skip
//   }
```

### CLI verb skeleton (mirror TestCommand)
```csharp
// Source pattern: flow-cli/Commands/TestCommand.cs:39-49
internal static class DocCommand
{
    public static Command Build()
    {
        var outOpt = new Option<string>("--out") { Description = "Output directory (default: docs/reference)" };
        var fmtOpt = new Option<string>("--format") { Description = "html|md|both (default: both)" };
        var cmd = new Command("doc", "Generate browsable reference docs from /// comments + BuiltInDocs");
        cmd.Add(outOpt); cmd.Add(fmtOpt);
        cmd.SetAction(parseResult => {
            var outDir = parseResult.GetValue(outOpt) ?? "docs/reference";
            // 1. lex+parse stdlib .flow + project .flow → collect ProcDeclaration.DocComment
            // 2. read BuiltInDocs._docs (expose via a public accessor)
            // 3. execute /// examples via TestRunner (DOC-02)
            // 4. emit HTML + Markdown; content-hash cache skip
            return 0;
        });
        return cmd;
    }
}
```

### BuiltInDocs consumption shape
```csharp
// Source: flow-lang/StandardLibrary/BuiltInDocs.cs:14-19
public static class BuiltInDocs {
    public sealed record Doc(string Summary, IReadOnlyList<ParamDoc> Params);
    public sealed record ParamDoc(string Name, string Description);
    private static readonly IReadOnlyDictionary<string, Doc> _docs = new Dictionary<string, Doc> { /* ~104 entries */ };
}
// NOTE: _docs is PRIVATE today. DOC-01 must add a public accessor, e.g.:
//   public static IReadOnlyDictionary<string, Doc> All => _docs;
// (BuiltInDocs already has TryGet per the Phase 38 :help fn surface — confirm/reuse.)
// Doc entries carry Summary + per-param descriptions but NO example field today.
// /// examples come from doc-comments on .flow procs, not from BuiltInDocs.
```

### WasapiBackend bridge (NAudio pull-model → IAudioBackend push contract)
```csharp
// NAudio's WasapiOut is a PULL model (background PlayThread calls IWaveProvider.Read);
// IAudioBackend.Play() is a BLOCKING push that returns when playback completes.
// Bridge: feed samples through a BufferedWaveProvider, Init+Play, then block on
// PlaybackStopped (or poll PlaybackState) until drained.
// Source: github.com/naudio/NAudio WasapiOut.cs (namespace NAudio.Wave)
//
//   var fmt = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels); // 32-bit float
//   var shareMode = exclusiveOpt ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
//   _out = new WasapiOut(shareMode, latencyMs);   // ctor: (AudioClientShareMode, int latency)
//   var provider = new BufferedWaveProvider(fmt) { BufferDuration = ..., DiscardOnBufferOverflow = false };
//   _out.Init(provider);
//   provider.AddSamples(bytes, 0, bytes.Length);  // float[] → byte[] via Buffer.BlockCopy
//   _out.Play();
//   // block until the buffered audio drains (PlaybackStopped event OR poll BufferedBytes==0 then Stop)
//
// IsAvailable(): probe by catching DllNotFoundException/PlatformNotSupportedException OR
//   RuntimeInformation.IsOSPlatform(OSPlatform.Windows) — WASAPI is Windows-only.
// Exclusive-mode opt-in reads the Phase 30 config.toml flag (FlowConfig.Active).
```
[CITED: github.com/naudio/NAudio/blob/master/NAudio.Wasapi/WasapiOut.cs — constructors `WasapiOut(AudioClientShareMode, int)` / `(AudioClientShareMode, bool, int)`, `Init(IWaveProvider)`, `Play()`/`Stop()`/`Pause()`, `PlaybackStopped` event, pull-model background `PlayThread`; namespace `NAudio.Wave`; Windows-only]

### JetBrains build.gradle.kts additions (verified 2.x DSL)
```kotlin
// Source: plugins.jetbrains.com/docs/intellij + blog.jetbrains.com/platform/2024/07/...
// ADD to the existing intellijPlatform { } block in flow-jetbrains/build.gradle.kts:
intellijPlatform {
    pluginVerification { ides { recommended() } }       // → verifyPlugin task
    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey       = providers.environmentVariable("PRIVATE_KEY")
        password         = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }
    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
        // channels = listOf("default")  // optional
    }
}
// Tasks: buildPlugin (autonomous) → verifyPlugin (autonomous) →
//        signPlugin (HUMAN cert) → publishPlugin (HUMAN token, Marketplace upload)
```
[CITED: blog.jetbrains.com/platform/2024/07/intellij-platform-gradle-plugin-2-0 + plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin.html]

### publish.sh 5-RID generalization (verified cross-compile)
```bash
# Source pattern: scripts/publish.sh (current single-RID linux-x64)
# Generalize to a loop. Managed-only publish → all 5 RIDs cross-compile from Linux.
for RID in linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64; do
  OUT="$PROJECT_ROOT/publish/flow-$RID"
  rm -rf "$OUT"; mkdir -p "$OUT"
  dotnet publish flow-cli/flow-cli.csproj -c Release -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \            # D-15 — NEVER trim (InternalFunctionRegistry)
    -p:DebugType=embedded \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUT"
  # package: tar.gz for linux/osx, zip for win; emit .sha256
  case "$RID" in
    win-x64) (cd "$OUT" && zip -r "../flow-$RID-v1.5.0.zip" .) ;;
    *)       tar -czf "$PROJECT_ROOT/publish/flow-$RID-v1.5.0.tar.gz" -C "$OUT" . ;;
  esac
  # sha256sum the archive → .sha256
done
# Only linux-x64/linux-arm64 get a `"$OUT/flow" version` runtime smoke here (D-02);
# osx-*/win-x64 exec smoke is a HUMAN-UAT row (the Linux box cannot run them).
```
[VERIFIED: dotnet single-file cross-compile from Linux to all 5 RIDs is supported — learn.microsoft.com/dotnet/core/deploying/single-file; the only caveat is that single-file apps are OS+arch specific, which is exactly the per-RID loop]

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WASM playground as a `flow-wasm/` Blazor project (original WASM-01 plan) | Mono-WASM `flow-lang` Web target + SvelteKit site (Phases 47-49) | 2026-05-25 | Phase 41 does NOT touch WASM at all; REQUIREMENTS WASM-01/02/03 are stale-Pending and need D-19 reconciliation |
| RtMidi.Core 1.0.53 NuGet for real-time MIDI | Direct librtmidi P/Invoke (`Audio/LibRtMidi.cs`) | Phase 40 Plan 40-04 (2026-06-07) | MIDI-RT-03 (CoreMIDI/WinMM) deferred to Phase 41 but the abstraction (`IMidiBackend`) is in place; showcase real-time MIDI uses the ALSA path on Linux |
| Old gradle-intellij-plugin (`org.jetbrains.intellij`) | IntelliJ Platform Gradle Plugin 2.x (`org.jetbrains.intellij.platform`) | Plugin already on 2.2.0 (Phase 31) | Signing/publishing DSL is the `intellijPlatform { signing/publishing/pluginVerification }` block, NOT the legacy `signPlugin {}`/`publishPlugin {}` top-level tasks |

**Deprecated/outdated:**
- The ROADMAP Phase 41 "Success Criterion 1" still describes the WASM playground (`KristofferStrube.Blazor.WebAudio`, bundle ≤15 MB, `flow-lang.example.dev`). This is SUPERSEDED — Phases 47-49 shipped it differently. Phase 41 plans must use the CONTEXT's 6-cluster scope, NOT ROADMAP criterion 1.
- REQUIREMENTS.md COREAUDIO-01 line names "OwnAudioSharp 1.0.68 preferred path" — SUPERSEDED by CONTEXT D-18 (hand-rolled CoreAudio is the shipping path; OwnAudioSharp is the conditional fallback). Trust CONTEXT over REQUIREMENTS here.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `BuiltInDocs` has a `TryGet` accessor (from Phase 38 `:help fn`) but `_docs` is private; DOC-01 needs a public `All`/enumerable accessor added | Code Examples / BuiltInDocs | Low — trivially add a public accessor; CLAUDE.md confirms `BuiltInDocs.TryGet(identifier)` exists for `:help fn`, so a read path already exists |
| A2 | `flow doc` should lex+parse the stdlib `.flow` files + project `.flow` to harvest `///` comments (not just builtins) | flow doc Architecture | Medium — if the intended doc surface is builtins-only, the `///` lexer change is still needed for composer procs but the harvest scope narrows. Confirm at plan time whether stdlib `.flow` procs get `///` comments authored in this phase |
| A3 | The showcase "real-time MIDI" feature-checklist item is a playback-path demonstration (Linux ALSA via `midiOut`), NOT part of the pinned offline render | Pitfall 5 / Showcase | Low — consistent with D-13 (only WAV+MIDI *offline* render is pinned) and Phase 40 (real-time MIDI is `play`-path only); but confirm the showcase structure splits demo-playback from pinned-render |
| A4 | NAudio.Wasapi 2.3.0 restores on a Linux build host (reference assembly resolves) even though WASAPI only runs on Windows | Standard Stack | Low — NAudio.Wasapi targets netstandard/net; the package restores cross-platform, COM calls only fire at runtime on Windows. The `#if !FLOW_WEB` + Desktop-only ItemGroup means Linux Desktop builds reference it but never call it. Verify the Linux `dotnet build` stays green after adding the ref |
| A5 | The macOS `>20 ms round-trip latency` OwnAudioSharp-swap trigger (D-18) is measured by the human, not autonomously | CoreAudio / Deferred | None for autonomous scope — it's explicitly a human-gate measurement |

**If this table is empty:** it is not — these 5 items need confirmation at plan time, but none block research. A1/A4 are near-certain; A2/A3 are scope-clarification questions for the planner.

## Open Questions

1. **Does the stdlib `.flow` corpus get `///` doc-comments authored in Phase 41, or does `flow doc` ship covering only the BuiltInDocs builtins + any existing composer procs?**
   - What we know: D-08 sources are `///` comments + proc signatures + BuiltInDocs. The lexer change enables `///` everywhere; BuiltInDocs already has ~104 entries.
   - What's unclear: whether authoring `///` comments across the stdlib `.flow` files (audio.flow, patterns.flow, etc.) is in-scope for this phase or a v1.6 fill-in.
   - Recommendation: Plan the lexer/parser/generator infrastructure unconditionally (it's the load-bearing work). Treat broad `///` authoring as an optional content task the composer can scope — the generator works with zero `///` comments (charitable signature-only entries per D-07).

2. **Which genre does the composer pick for SHOWCASE-01?**
   - What we know: D-11 recommends EDM; D-12 lists death metal + jazz as valid alternatives. This is a genuine composer creative choice.
   - What's unclear: only the composer can decide.
   - Recommendation: Surface the choice at plan time (as CONTEXT D-11 instructs — "composer should confirm before execution"). The scaffolding (feature checklist: pattern matching + generative primitive + granular/stretch + live block + real-time MIDI) is genre-independent; only the musical content changes.

3. **Should `flow doc` example execution distinguish `(test ...)`-block examples from plain-expression snippets?**
   - What we know: `TestRunner.Run` walks the `TestRegistry` (populated by `(test ...)` calls). Plain doc snippets aren't test blocks.
   - What's unclear: whether examples are expected to be full `(test ...)` blocks or bare expressions.
   - Recommendation: Support bare expressions (execute-and-check-no-error) as the default — it's the lower-friction composer ergonomic (per `feedback_ergonomics_priority`). A doc example is "this code runs without error" by default; `(test ...)` blocks with assertions are the richer opt-in. D-10's "checked for successful render, not byte output" wording supports the bare-expression default.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All builds + BIN-01 publish | ✓ | 10.0.108 | — |
| .NET 10 runtime | Smoke-run linux binaries | ✓ | 10.0.8 | self-contained binaries bundle their own |
| `dotnet publish` single-file | BIN-01 (5 RIDs) | ✓ | SDK 10.0.108 | — |
| libpulse (Linux audio) | linux binary smoke audible | ✓ (this is a Linux box; Pulse/PipeWire present) | — | — |
| NAudio.Wasapi | WASAPI-01 (Windows runtime) | restores on Linux; runs only on Windows | 2.3.0 (NuGet) | — (Windows-only by nature; verified on Windows = HUMAN-UAT) |
| AudioToolbox.framework | COREAUDIO-01 (macOS runtime) | ✗ on Linux (DllNotFoundException → IsAvailable() false) | — | macOS-only; verified on Mac = HUMAN-UAT |
| Gradle + JDK 21 | JET-01 buildPlugin/verifyPlugin | likely (flow-jetbrains has gradlew + toolchain 21) | gradle 8.6 wrapper / JBR 21 | — (confirm `./gradlew buildPlugin` runs; if JDK 21 absent, toolchain auto-provisions) |
| JetBrains signing cert + Marketplace token | JET-01 signPlugin/publishPlugin | ✗ (human account) | — | HUMAN gate (D-03) — no fallback, by design |
| `zip` / `tar` | BIN-01 artifact packaging | ✓ (standard Linux) | — | — |
| ctx7 / Context7 MCP | docs lookup | ✗ (not installed) | — | WebFetch/WebSearch used instead |
| slopcheck CLI `install` verb | package legitimacy gate | partial (0.6.1 installed; `install` subcommand not exposed here) | 0.6.1 | manual registry + repo verification (done in audit) |

**Missing dependencies with no fallback (by design — human gates):**
- JetBrains signing cert + Marketplace publish token (JET-01) — D-03 human gate.
- macOS hardware (AudioToolbox real audible test) — D-05 human gate.
- Windows hardware (WASAPI real audible test + win-x64 exec smoke) — D-05 human gate.

**Missing dependencies with viable fallback:**
- Context7 → WebFetch/WebSearch (used this session).
- slopcheck `install` verb → manual NuGet registry + GitHub repo verification (NAudio.Wasapi confirmed legitimate).

## Validation Architecture

> nyquist_validation is enabled (config.json workflow.nyquist_validation = true). This section is REQUIRED.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (C# unit/integration tests in `flow-lang.Tests/`) + the in-language Flow test framework (`flow test`, Phase 35) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `tests/test_*.flow` (Flow scripts) |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase41"` (per-task) |
| Full suite command | `dotnet test` (xUnit) + `for t in tests/test_*.flow; do dotnet run --project flow-cli -- test "$t"; done` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DOC-01 | `///` lexes as doc-comment; `//` + `/* */` unchanged | unit | `dotnet test --filter "FullyQualifiedName~DocCommentLexTests"` | ❌ Wave 0 |
| DOC-01 | `///` binds to following ProcDeclaration; orphan dropped charitably | unit | `dotnet test --filter "FullyQualifiedName~DocCommentBindTests"` | ❌ Wave 0 |
| DOC-01 | `flow doc` emits HTML + Markdown with BuiltInDocs entries | integration | `dotnet test --filter "FullyQualifiedName~FlowDocGenTests"` | ❌ Wave 0 |
| DOC-01 | content-hash cache skips unchanged entries; edited entry regens | integration | `dotnet test --filter "FullyQualifiedName~DocCacheTests"` | ❌ Wave 0 |
| DOC-02 | passing `///` example renders no `[example failed]`; failing one does | integration | `dotnet test --filter "FullyQualifiedName~DocExampleExecTests"` | ❌ Wave 0 |
| BIN-01 | `publish.sh` produces 5 RID archives + .sha256 | smoke (script) | `bash scripts/publish.sh && ls publish/flow-*-v1.5.0.*` | ❌ Wave 0 (extend existing) |
| BIN-01 | linux-x64 + linux-arm64 binary runs `flow version` | smoke (script) | `publish/flow-linux-x64/flow version` (arm64 = qemu or skip-with-reason) | ❌ Wave 0 |
| WASAPI-01 | `WasapiBackend` compiles on Desktop; Web build stays green | unit/build | `dotnet build flow-lang -p:FlowTarget=Web` (must exit 0) | exists (gate) |
| WASAPI-01 | NAudio not in Web closure | invariant | `dotnet test --filter "AssemblyReferenceScanTests"` (Web target) | exists (extend forbidden-prefix) |
| WASAPI-01 | `IsAvailable()` returns false on Linux (no crash) | unit | `dotnet test --filter "WasapiBackendAvailabilityTests"` | ❌ Wave 0 |
| WASAPI-01 | Windows audible playback | manual | HUMAN-UAT (D-05) | n/a (human) |
| COREAUDIO-01 | `CoreAudioBackend.IsAvailable()` false on Linux; compiles clean | unit | `dotnet test --filter "CoreAudioBackendAvailabilityTests"` | ❌ Wave 0 (likely exists from prior) |
| COREAUDIO-01 | macOS audible playback + <20 ms latency check | manual | HUMAN-UAT (D-05) | n/a (human) |
| JET-01 | `./gradlew buildPlugin` + `verifyPlugin` succeed | integration (gradle) | `cd flow-jetbrains && ./gradlew buildPlugin verifyPlugin` | ❌ Wave 0 |
| JET-01 | Marketplace publish | manual | HUMAN-UAT (D-03) | n/a (human) |
| SHOWCASE-01 | showcase WAV two-run cmp-clean | smoke (script) | `bash scripts/test_two_run_determinism.sh examples/<genre>/<piece>.flow` | ❌ Wave 0 |
| SHOWCASE-01 | showcase WAV RMS-windowed regression ±0.5 dB/100 ms | integration | `dotnet test --filter "FullyQualifiedName~Phase41ShowcaseRmsTests"` | ❌ Wave 0 |
| SHOWCASE-01 | showcase MIDI offline render deterministic | smoke | render twice, cmp `.mid` SHA-256 | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase41"` (the targeted Phase 41 unit/integration subset, < 30 s).
- **Per wave merge:** `dotnet test` (full xUnit suite) + the Flow `test_*.flow` loop.
- **Phase gate:** Full suite green + `bash scripts/test_two_run_determinism.sh` on the showcase + `dotnet build flow-lang -p:FlowTarget=Web` exit 0, before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/.../DocCommentLexTests.cs` — covers DOC-01 (`///` vs `//` vs `/* */`)
- [ ] `flow-lang.Tests/.../DocCommentBindTests.cs` — covers DOC-01 (binding + orphan-drop)
- [ ] `flow-lang.Tests/.../FlowDocGenTests.cs` — covers DOC-01 (HTML+MD emit)
- [ ] `flow-lang.Tests/.../DocCacheTests.cs` — covers DOC-01 (content-hash incremental)
- [ ] `flow-lang.Tests/.../DocExampleExecTests.cs` — covers DOC-02 (pass/fail annotation)
- [ ] `flow-lang.Tests/.../WasapiBackendAvailabilityTests.cs` — covers WASAPI-01 (Linux IsAvailable false, no crash)
- [ ] `flow-lang.Tests/.../Phase41ShowcaseRmsTests.cs` + baseline WAV under `flow-lang.Tests/baselines/Phase41/` — covers SHOWCASE-01 RMS regression
- [ ] Extend `scripts/publish.sh` for 5-RID + tar/zip + .sha256 (BIN-01)
- [ ] Extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` with `"NAudio"` (WASAPI-01 Web-strip gate)
- [ ] `41-HUMAN-UAT.md` rows (5+): Windows WASAPI audible, macOS CoreAudio audible+latency, osx-x64 exec, osx-arm64 exec, win-x64 exec, JetBrains Marketplace publish, GitHub Release cut

*(COREAUDIO-01 availability test may already exist from prior phases — confirm before adding a duplicate.)*

## Security Domain

> security_enforcement is not explicitly false in config — treat as enabled. Phase 41's attack surface is narrow: a doc-example executor that runs `.flow` source, a CLI that reads/writes files, and a publish pipeline handling secrets.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | Web-strip invariant (`AssemblyReferenceScanTests`) keeps Windows-only NAudio out of the browser closure |
| V2 Authentication | no | No auth surface in Phase 41 (Marketplace token is the human's, supplied at publish time) |
| V5 Input Validation | yes | `flow doc` parses `.flow` source — already goes through the charitable lexer/parser (errors accumulate, never crash). Doc-example executor runs untrusted-shaped source through the existing hermetic `TestRunner` (state reset between examples) |
| V6 Cryptography | yes (indirect) | SHA-256 checksums for binaries (`sha256sum`); JetBrains signing cert handled by the Gradle plugin — never hand-rolled. NEVER commit `CERTIFICATE_CHAIN`/`PRIVATE_KEY`/`PUBLISH_TOKEN` |
| V12 Files & Resources | yes | `flow doc --out` writes files; the existing `flow test` directory glob is `test_*.flow` + `TopDirectoryOnly` (T-35-10 precedent). `flow doc` should constrain its output path and avoid path-traversal in the `--out` arg |
| V14 Configuration | yes | Secrets via env vars only (`providers.environmentVariable(...)`), never in `build.gradle.kts` or committed files |

### Known Threat Patterns for {C# / .NET + Gradle + audio P/Invoke}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Doc example runs malicious/runaway `.flow` (DoS) | Denial of Service | Reuse `TestRunner` hermetic isolation; the Phase 38 30 s wall-clock cap pattern is available if needed for long examples (examples should be tiny — recommend a per-example time budget) |
| Secret leakage (signing cert / publish token in VCS) | Information Disclosure | Env-var-only (`CERTIFICATE_CHAIN`/`PRIVATE_KEY`/`PRIVATE_KEY_PASSWORD`/`PUBLISH_TOKEN`); documented in runbook, never committed |
| Supply-chain (typosquatted/wrong NAudio.Wasapi version) | Tampering | Pin `2.3.0` EXACTLY (the registry contains an anomalous `22.0.0` — a `+`/`latest` range is a trap); verify against `github.com/naudio/NAudio` |
| Tampered binary download | Tampering | Ship `.sha256` alongside every artifact (D-16); the human verifies before the GitHub Release |
| Web build drift (Windows COM type in WASM) | (correctness/integrity) | `AssemblyReferenceScanTests` forbidden-prefix gate + Desktop-only ItemGroup + `#if !FLOW_WEB` |
| Path traversal in `flow doc --out` | Tampering | Validate/normalize the output dir; default to `docs/reference` |

## Sources

### Primary (HIGH confidence)
- `flow-lang/Lexing/SimpleLexer.cs` (`SkipWhitespaceAndComments`, multi-char-before-single-char ordering) — `///` insertion point
- `flow-lang/Ast/Statements/ProcDeclaration.cs` (record shape + `IsStrict`/`IsBeatTrueToSig` precedent) — doc-comment field
- `flow-lang/Parsing/Parser.cs` (lines 130-136 dispatch, 334 `ParseProcDeclaration`, 411 return) — binding site
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — content source shape (`Doc`/`ParamDoc`, private `_docs`)
- `flow-cli/Commands/TestCommand.cs` + `CommandRegistry.cs` + `Program.cs` — CLI verb pattern + in-process TestRunner usage
- `flow-lang/StandardLibrary/TestFramework/TestRunner.cs` (`Run(FlowEngine, path)` + Snapshot/Restore) — DOC-02 example execution
- `flow-lang/Audio/IAudioBackend.cs` + `PulseAudioSimpleBackend.cs` + `CoreAudioBackend.cs` + `AudioPlaybackManager.cs` (`DetectBackend`) — backend template + slot
- `flow-lang/flow-lang.csproj` — Web-strip discipline (conditional ItemGroups, Compile-Remove, PackageReference conditioning)
- `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` — forbidden-prefix gate
- `flow-jetbrains/build.gradle.kts` + `plugin.xml` + `gradle.properties` — existing plugin scaffolding (2.2.0 plugin, LSP4IJ 0.19.3, JDK 21)
- `scripts/publish.sh` + `scripts/test_two_run_determinism.sh` — packaging + determinism harness
- `flow-lang.Tests/Helpers/RmsRegressionTests.cs` (`AssertRmsWithinTolerance`/`AssertWavMatchesBaseline`) — SPEC-8 pinning
- `examples/generative/markov_jazz.flow` + `examples/showcase.flow` — showcase style + seeded-determinism pattern
- `.planning/REQUIREMENTS.md` (Phase 41 cluster) + `.planning/ROADMAP.md` (Phase 41 section) + `.planning/STATE.md`
- api.nuget.org/v3-flatcontainer/naudio.wasapi/index.json — NAudio.Wasapi 2.3.0 version verification

### Secondary (MEDIUM confidence — official docs, cross-verified)
- github.com/naudio/NAudio/blob/master/NAudio.Wasapi/WasapiOut.cs — WasapiOut API (ctors, Init, Play/Stop, pull-model, Windows-only, `NAudio.Wave` namespace)
- plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin.html + plugin-signing.html — task names + env vars
- blog.jetbrains.com/platform/2024/07/intellij-platform-gradle-plugin-2-0 — `intellijPlatform { pluginVerification / signing / publishing }` DSL
- learn.microsoft.com/dotnet/core/deploying/single-file/overview + dotnet-publish — cross-compile single-file matrix, `IncludeNativeLibrariesForSelfExtract`

### Tertiary (LOW confidence)
- (none — all claims verified against source or official docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — single new package (NAudio.Wasapi 2.3.0) verified on NuGet + official repo; everything else is existing/SDK
- Architecture (flow doc): HIGH — every integration point (lexer, ProcDeclaration field, parser binding, CLI verb, TestRunner) read in current source; binding precedent (`IsStrict`) is exact
- Architecture (audio backends): HIGH — DetectBackend slot + IAudioBackend contract + CoreAudio completeness all read in source; WasapiOut bridge verified against NAudio repo
- Architecture (binaries): HIGH — publish.sh read; cross-compile matrix verified against MS docs
- Architecture (JetBrains): MEDIUM-HIGH — existing build.gradle.kts read; 2.x signing/publishing DSL verified against official blog/docs (not run on this box — gradle build is a Wave-0 verification)
- Pitfalls: HIGH — derived from in-repo Web-strip discipline, determinism contracts, and the explicit autonomous/human split
- Showcase: HIGH — feature syntax + seeded-determinism + RMS pinning all read in existing examples/tests

**Research date:** 2026-06-07
**Valid until:** 2026-07-07 (stable — Flow is a mature codebase; the only external moving part is NAudio.Wasapi, pinned to 2.3.0, and the JetBrains plugin DSL, pinned to plugin 2.2.0)
