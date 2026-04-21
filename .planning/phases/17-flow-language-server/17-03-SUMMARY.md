---
phase: 17-flow-language-server
plan: 03
subsystem: lsp
tags: [lsp, language-server, diagnostics, text-sync, debounce, close-race]

# Dependency graph
requires:
  - phase: 17-flow-language-server
    plan: 01
    provides: "ParseSession + OmniSharp boot-verified flow-lsp scaffold"
provides:
  - "DocumentManager: per-URI debounce + cancel + HasDocument close-race accessor"
  - "LspMappings: 1-based SourceLocation -> 0-based LSP Range + DiagnosticLevel -> DiagnosticSeverity"
  - "TextDocumentSyncHandler: didOpen/didChange/didClose wired into DocumentManager with Full-sync registration"
  - "DiagnosticsPublisher: pure BuildDiagnostics static + PublishDiagnostics wrapper + IDiagnosticsPublisher test seam"
  - "Program.cs bootstrap with close-race guard in the onParse callback"
affects: [17-04, 17-05, 17-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CancellationTokenSource-per-URI debounce (NEW IDIOM — first instance in repo)"
    - "Type alias `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range` to disambiguate from System.Range (downstream LSP files should follow suit)"
    - "IDiagnosticsPublisher interface seam — test-only mock substitutes the real ILanguageServerFacade consumer"
    - "Partial test class split (DocumentManagerTests.cs + DocumentManagerTests.CloseRace.cs) to keep Task 1 / Task 2 atomic commits compile-clean in isolation"
    - "Self-referential DI factory closure (DocumentManager's onParse callback captures `dm` via forward-declared local)"

key-files:
  created:
    - "flow-lsp/DocumentManager.cs"
    - "flow-lsp/LspMappings.cs"
    - "flow-lsp/Handlers/DiagnosticsPublisher.cs"
    - "flow-lsp/Handlers/TextDocumentSyncHandler.cs"
    - "flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs"
    - "flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs"
    - "flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs"
    - "flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs"
  modified:
    - "flow-lsp/Program.cs (Wave 2 DI wiring + close-race guard)"

key-decisions:
  - "Range type alias applied because `System.Range` and `OmniSharp.Extensions.LanguageServer.Protocol.Models.Range` collide under standard usings"
  - "OmniSharp 0.19.9 override signature is `CreateRegistrationOptions(TextSynchronizationCapability, ClientCapabilities)` — NOT the `SynchronizationCapability` name in the plan's approximate sample (Rule 3 deviation discovered at first build)"
  - "Added ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded Fact because the original CloseCancelsPendingDiagnostics Fact does NOT discriminate: the CTS-based cancellation in ScheduleParseAsync short-circuits the onParse callback before HasDocument is ever evaluated. The new Fact uses TaskCompletionSource to force the narrow post-delay/pre-publish race window (Rule 2 addition to strengthen regression coverage)"
  - "Discrimination spot-check performed: removing the HasDocument guard from ParseCompletingConcurrentlyWithClose flips the Fact from pass to fail (stack-trace captured during execution). Guard restored before commit."
  - "DocumentManager schedules parse OUTSIDE the lock (read CTS token under lock, dispatch fire-and-forget Task outside) — prevents async-over-lock contention under rapid keystroke bursts (Rule 2 addition over the 17-PATTERNS template)"
  - "ILanguageServerFacade resolved by OmniSharp's ambient DI without explicit registration — DiagnosticsPublisher constructor parameter injected transparently"

patterns-established:
  - "Close-race guard wiring pattern: onParse callback captures `dm` via forward-declared local + `if (dm!.HasDocument(uri)) diag.Publish(...)` — downstream plans (17-05 user-symbol index, 17-06 hover/def) must preserve this guard when extending the callback body"
  - "Task-split partial-class pattern: when a Task 1 commit creates a test class and Task 2 must extend it, split new Facts into a separate `.SomeGroup.cs` file declaring the same `partial class`, so Task 1's file compiles without Task 2 symbols"
  - "BuildDiagnostics static separation: every handler that emits LSP Diagnostic[] should expose a pure static Build method so unit tests can exercise mapping without an ILanguageServerFacade"
  - "IDiagnosticsPublisher interface seam: the real impl depends on a transport-bound facade, so the test seam is declared alongside it — downstream plans (17-04 semantic tokens publisher, 17-05 completion response) may follow the same split if they need test substitutability"

requirements-completed: []
# (Plan frontmatter references D-03 and D-06 — these are CONTEXT-locked decisions, not REQ-IDs in REQUIREMENTS.md. No traceability row to flip.)

# Metrics
duration: ~30min
completed: 2026-04-20
---

# Phase 17 Plan 03: DocumentManager + TextDocumentSyncHandler + DiagnosticsPublisher + LspMappings Summary

**Wave 2 Flow LSP wiring: debounced per-URI parse scheduling, close-race-guarded diagnostics publish, and the OmniSharp text-sync handler that routes didOpen/didChange/didClose through the pipeline — 16 new Facts green, 23/23 Phase17 green overall.**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2 (atomic commits)
- **Files created:** 8 (4 production + 4 test)
- **Files modified:** 1 (flow-lsp/Program.cs)
- **Tests added:** 16 Facts (9 Task 1 + 7 Task 2, all green)

## Accomplishments

- `DocumentManager` debounces keystroke events at 150 ms (D-03), cancels prior in-flight parses on every new Update, and exposes `HasDocument(uri)` so the onParse callback can suppress post-close publishes.
- `LspMappings` centralizes the 1-based → 0-based coordinate transform and DiagnosticLevel → DiagnosticSeverity translation. Underflow-guarded with `Math.Max(0, ...)` for `SourceLocation.Unknown` (0,0).
- `TextDocumentSyncHandler` subclasses `TextDocumentSyncHandlerBase`, registers for `language="flow"` via `TextDocumentSelector.ForLanguage("flow")`, and uses `TextDocumentSyncKind.Full` (D-03 no-incremental).
- `DiagnosticsPublisher` exposes a pure static `BuildDiagnostics(FlowError[]) -> Diagnostic[]` for unit testing, plus the transport-bound `Publish(uri, errors)` that always fires (empty array is how LSP clears stale squiggles — RESEARCH §Pattern 2 caveat).
- `Program.cs` DI factory wires DocumentManager's onParse callback with the close-race guard: `if (dm!.HasDocument(uri)) diag.Publish(uri, result.Errors);`
- `IDiagnosticsPublisher` interface enables a test-only RecordingPublisher substitute for end-to-end close-race Facts without booting an ILanguageServerFacade.

## Task Commits

Each task was committed atomically:

1. **Task 1: DocumentManager + LspMappings + 9 Facts** — `86a4364` (feat)
2. **Task 2: TextDocumentSyncHandler + DiagnosticsPublisher + Program.cs wire-up + 7 Facts** — `04e8cda` (feat)

## Files Created/Modified

### Created
- `flow-lsp/DocumentManager.cs` — debounce-and-cancel buffer cache; `HasDocument` accessor; lock-guarded Dictionary for multi-threaded OmniSharp dispatch.
- `flow-lsp/LspMappings.cs` — static `ToRange(SourceLocation)` and `ToSeverity(DiagnosticLevel)`. Applies the `using Range = OmniSharp…Models.Range` alias.
- `flow-lsp/Handlers/DiagnosticsPublisher.cs` — `IDiagnosticsPublisher` interface + pure static `BuildDiagnostics` + transport `Publish`.
- `flow-lsp/Handlers/TextDocumentSyncHandler.cs` — OmniSharp `TextDocumentSyncHandlerBase` subclass wiring the 4 text-sync RPCs.
- `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs` — 4 Facts (debounce, cancel, close, HasDocument).
- `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs` — 3 Facts (close-before-debounce, control case, slow-parse discriminator).
- `flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs` — 2 Facts + 3 Theory rows (range math, underflow clamp, severity map).
- `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs` — 4 Facts (empty, error-severity, Source=flow, 0-based Range).

### Modified
- `flow-lsp/Program.cs` — Wave 2 DI graph: ParseSession + DiagnosticsPublisher + IDiagnosticsPublisher + DocumentManager factory (self-referencing closure) + WithHandler<TextDocumentSyncHandler>. Wire-up preserves the `HasDocument`-guarded publish branch.

## OmniSharp API Observations

Per Plan's output requirement, for downstream plans:

- **TextDocumentSyncHandler base class:** `OmniSharp.Extensions.LanguageServer.Protocol.Document.TextDocumentSyncHandlerBase` — abstract methods matched the 17-PATTERNS shape EXCEPT for the registration-options parameter type.
- **`CreateRegistrationOptions` signature:** `(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)` — NOT `SynchronizationCapability` as the plan's approximate sample suggested. This is an OmniSharp 0.19.9 naming choice to disambiguate the text-sync capability from other sync capability kinds (NotebookSynchronization, etc.).
- **DocumentSelector class name:** `TextDocumentSelector` (not `DocumentSelector`). `DocumentSelector` may also exist as a legacy alias in some versions, but `TextDocumentSelector.ForLanguage(...)` is the resolved name under 0.19.9.
- **Range / Position namespace:** `OmniSharp.Extensions.LanguageServer.Protocol.Models`. Range collides with `System.Range`; the alias `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` resolves. Position does not collide.
- **`ILanguageServerFacade` ambient DI:** Works transparently — declaring `ILanguageServerFacade` as a constructor parameter in `DiagnosticsPublisher` causes OmniSharp's DI container to inject the facade without explicit registration. No factory wiring required.
- **`PublishDiagnosticsParams`** lives under `OmniSharp.Extensions.LanguageServer.Protocol.Models`; `Container<T>` lives in the same namespace.

## Close-Race Guard Discrimination

Plan called for a CloseCancelsPendingDiagnostics Fact proving the `HasDocument` check fires. Observation during execution:

- The plan-specified Fact (`Close` immediately after `Update`, wait past debounce) **does NOT discriminate** — the CTS-based cancellation in `ScheduleParseAsync` short-circuits the onParse callback before the HasDocument check is ever evaluated. Removing the guard from that Fact's callback body still yielded a passing test.
- Added a THIRD Fact (`ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded`) using a `TaskCompletionSource` to simulate a slow parse and force the narrow post-delay/pre-publish race window. This Fact **does discriminate** — spot-check removed the guard, Fact failed with stack trace; guard restored, Fact passes. This is the real regression gate for the close-race guard.

Three Facts retained in DocumentManagerTests.CloseRace.cs:
1. `CloseCancelsPendingDiagnostics_NoPublishAfterClose` — proves the CTS path works for the common case.
2. `OpenThenUpdate_PublishesAfterDebounce` — control Fact that publishes DO fire without Close.
3. `ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded` — discriminator that actually pins the HasDocument guard.

Taken together, (1) + (3) cover both race paths: the common case (CTS cancels pending work) and the narrow case (parse runs to completion, then Close lands during/before publish).

## Constraints Confirmed

- **net10.0** — all csprojs still target `net10.0`. No framework change introduced.
- **No audio** — `flow-lsp/Program.cs` still does not reference `AudioPlaybackManager`, `FlowEngine`, or `Interpreter`. The onParse callback calls `ParseSession.Parse` only.
- **Thread-safety** — `DocumentManager._buffers` Dictionary access is fully inside `lock (_lock)`; the async `ScheduleParseAsync` task dispatch is done outside the lock (lock held only to read/replace the CTS, then released).

## Decisions Made

1. **Range type alias** — `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` in `LspMappings.cs` to disambiguate from `System.Range`. Discovered at first build (CS0104 ambiguous reference). Downstream LSP files should follow the same convention.
2. **Partial test class split** — `DocumentManagerTests.cs` (Task 1 Facts) + `DocumentManagerTests.CloseRace.cs` (Task 2 Facts) declared as `partial class DocumentManagerTests`. This keeps Task 1's commit compile-clean without Task 2 symbols (IDiagnosticsPublisher, RecordingPublisher), while still presenting a single logical test class to xUnit.
3. **Third close-race Fact** — Added `ParseCompletingConcurrentlyWithClose` under Rule 2 (auto-add missing critical functionality) because the plan's two Facts do not discriminate. The slow-parse TaskCompletionSource Fact is the actual regression gate. Without it, a future regression that removes the HasDocument guard would not be caught.
4. **Schedule outside the lock** — Small Rule 2 refinement over the 17-PATTERNS template: `_onParse` dispatch is fire-and-forget outside the lock scope, preventing potential async continuations from holding the lock under high keystroke rate.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `System.Range` vs `OmniSharp...Models.Range` ambiguous reference**
- **Found during:** Task 1 first build
- **Issue:** CS0104 — `Range` is an ambiguous reference between `OmniSharp.Extensions.LanguageServer.Protocol.Models.Range` and `System.Range`. The plan's approximate sample relied on unique resolution; the actual file with implicit usings did not resolve.
- **Fix:** Added `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` alias to `flow-lsp/LspMappings.cs`.
- **Files modified:** `flow-lsp/LspMappings.cs`
- **Verification:** `dotnet build flow-lsp/flow-lsp.csproj` exits 0.
- **Committed in:** `86a4364`

**2. [Rule 3 - Blocking] OmniSharp API drift: `SynchronizationCapability` → `TextSynchronizationCapability`**
- **Found during:** Task 2 first build
- **Issue:** CS0534 + CS0246 — `TextDocumentSyncHandlerBase.CreateRegistrationOptions` abstract override expects `TextSynchronizationCapability` not `SynchronizationCapability`. Plan's approximate sample had the wrong type name.
- **Fix:** Changed the override's first parameter type to `TextSynchronizationCapability`.
- **Files modified:** `flow-lsp/Handlers/TextDocumentSyncHandler.cs`
- **Verification:** `dotnet build flow-lsp/flow-lsp.csproj` exits 0.
- **Committed in:** `04e8cda`

**3. [Rule 2 - Addition] Added discriminating close-race Fact**
- **Found during:** Task 2 optional discrimination spot-check (called out in Plan output requirements as "optional")
- **Issue:** The plan's `CloseCancelsPendingDiagnostics_NoPublishAfterClose` Fact does NOT discriminate — removing the HasDocument guard still yields a passing test because the CTS-cancel path in ScheduleParseAsync already suppresses the callback before the guard is evaluated.
- **Fix:** Added `ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded` using a `TaskCompletionSource` to force the post-delay/pre-publish race window. Verified: fails when guard removed, passes when guard restored.
- **Files modified:** `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs`
- **Committed in:** `04e8cda`

**4. [Rule 2 - Refinement] Schedule parse dispatch outside the lock**
- **Found during:** Task 1 implementation
- **Issue:** The 17-PATTERNS template dispatches the fire-and-forget `ScheduleParseAsync` task inside the `lock (_lock)` block. Under high keystroke rate, this could cause async continuations to contend on the lock.
- **Fix:** Read the CTS token under the lock, release the lock, then start the async dispatch. Tests still pass.
- **Files modified:** `flow-lsp/DocumentManager.cs`
- **Committed in:** `86a4364`

---

**Total deviations:** 4 auto-fixed (2 Rule 3 blocking API drifts, 1 Rule 2 discriminator, 1 Rule 2 refinement). No Rule 4 escalations.

## Issues Encountered

- xUnit analyzer emitted VSTHRD200 / xUnit1051 warnings about async method suffixes and using `TestContext.Current.CancellationToken`. These are analyzer suggestions, not plan-specified acceptance criteria, and the existing Phase 12-14 test files follow the same pattern. Not fixed — out of scope per the deviation rules.

## Next Phase Readiness

Plan 17-04 (SemanticTokensHandler) can safely assume:
- `DocumentManager` is available via DI. `GetText(uri)` is already exposed for handlers that need the source buffer.
- `LspMappings.ToRange(SourceLocation)` is available and tested — semantic-token positions can reuse it (though semantic tokens emit deltas, not Range records, so the helper is advisory, not prescriptive).
- `TextDocumentSyncHandler.Handle(DidChangeTextDocumentParams)` calls `DocumentManager.Update`, which triggers onParse and updates DI-bound `DocumentManager.GetText`. Downstream handlers can read the latest buffer text from there.
- OmniSharp handler-registration pattern established via `.WithHandler<T>()`. 17-04 will call `.WithHandler<SemanticTokensHandler>()` alongside the TextDocumentSyncHandler.

Plan 17-05 (symbol indices + CompletionHandler) can safely assume:
- `DocumentManager.HasDocument(uri)` is available for post-parse guards (same pattern applies to UserSymbolIndex.Update — a symbol index update for a closed URI should be a no-op).
- The onParse callback in `Program.cs` is the extension point for additional post-parse work. 17-05 will add `userSymbols.Update(uri, result.Ast)` alongside the existing `diag.Publish(...)`, keeping the HasDocument guard wrapping both.

Plan 17-08 (manual Extension Dev Host smoke) needs to tune the 150 ms debounce per D-03 Claude's Discretion. Current value feels reasonable in unit tests (no noticeable lag in the 300 ms Fact wait). Defer real tuning to that phase.

## Self-Check: PASSED

Verification that all claimed artifacts exist:

- `flow-lsp/DocumentManager.cs` — FOUND
- `flow-lsp/LspMappings.cs` — FOUND
- `flow-lsp/Handlers/DiagnosticsPublisher.cs` — FOUND
- `flow-lsp/Handlers/TextDocumentSyncHandler.cs` — FOUND
- `flow-lsp/Program.cs` (modified) — FOUND
- `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs` — FOUND
- Commit `86a4364` — FOUND
- Commit `04e8cda` — FOUND
- `dotnet build flow-sharp.sln -c Debug` exits 0 — VERIFIED
- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` — 23/23 Facts pass — VERIFIED
- `grep -q "public bool HasDocument" flow-lsp/DocumentManager.cs` — PASS
- `grep -q "HasDocument" flow-lsp/Program.cs` (close-race guard wired) — PASS
- `grep -q "interface IDiagnosticsPublisher" flow-lsp/Handlers/DiagnosticsPublisher.cs` — PASS
- `grep -q "TextDocumentSyncKind.Full" flow-lsp/Handlers/TextDocumentSyncHandler.cs` — PASS
- `grep -q "CloseCancelsPendingDiagnostics" flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs` — PASS

---
*Phase: 17-flow-language-server*
*Plan: 03*
*Completed: 2026-04-20*
