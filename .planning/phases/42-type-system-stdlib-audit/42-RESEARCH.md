# Phase 42: Type System & Stdlib Audit — Research

**Researched:** 2026-05-24
**Domain:** Type system inventory + stdlib registration graph (read-only audit, no behavior changes)
**Confidence:** HIGH for codebase-internal claims (every claim verified by reading source); MEDIUM for tooling choice (graphify currently disabled — must be enabled or hand-rolled)

## Summary

Phase 42 is a **read-only inventory phase** that produces `AUDIT.md` — a prioritized gap list across five gap classes: orphaned types, missing conversions, asymmetric pairs, dead-end builtins, and overload gaps. It is the cheapest of the v1.5 closeout trio because it ships docs only, not code. Its output feeds Phase 43 (which functions need module-qualification first) and Phase 44 (which clamp / advisory sites need the strict-mode error branch).

The audit graph has two halves that **must both** be cross-referenced to avoid false positives:

1. **C# registration graph** — `InternalFunctionRegistry._implementations: Dictionary<string, List<(FunctionSignature, Func<...>)>>`, populated by ~370 `registry.Register(...)` calls across 30+ files (BuiltInFunctions.cs:211 + Transforms:24 + EffectsFunctions:21 + ~28 others). Already exposes a `EnumerateSignatures()` API (added Phase 17 for the LSP) — a 50-line C# program against this enumerator + reflection over `flow-lang/TypeSystem/{PrimitiveTypes,SpecialTypes}/*.cs` produces the complete `FlowType → signature[]` adjacency table for free.
2. **`.flow` stdlib graph** — 12 `flow-lang/*.flow` files declare `internal proc NAME(Type: param, ...)` lines that bind to C# registrations. A C# function with zero `.cs` consumers may still be reached via a `.flow` consumer (and vice versa — a type with zero direct registration may be consumed through a `.flow`-layer abstraction). Grepping the `.flow` files is essential.

**Empirical pre-research finding (high-signal):** `BeatType` is **completely orphaned** in `flow-lang/StandardLibrary/` — `grep -rn "BeatType" StandardLibrary/` returns zero hits. It's used only at the parser/runtime layer for literal construction (`Value.Beat()`, `Interpreter.cs:1019`). No builtin accepts or returns it. The ROADMAP's example "Beat arithmetic exists but no Beat → Second at tempo context" is dead-on and confirmed pre-audit. Other near-orphans by `.Instance` reference count: `TupleType` (uses `.AnyArity` sentinel, 2 sites), `DictType` (uses wildcard `new DictType(Void, Void)`, 3 sites), `Tuning` (1 site — `loadScala` only producer, no consumers in registry — consumed at tuning-block evaluation, not at builtin call sites), `Envelope` (1 site — `applyEnvelope`).

**Primary recommendation:** Plan as **4 plans across 3 waves** — Wave 0 enables graphify (or builds a hand-rolled C# inventory program if graphify is too coarse for FlowType-level granularity), Wave 1 runs three parallel audit passes (registration-graph, conversion-graph, clamp/advisory-graph), Wave 2 synthesizes AUDIT.md + commits. Total estimate: 1-2 days. Strictly no production code changes ship in Phase 42.

## Phase Requirements

> Phase 42 has no pre-assigned REQ-IDs in REQUIREMENTS.md ("TBD at plan-phase"). The following set is derived from the ROADMAP goal text and the downstream phase-43/44 needs. The planner may renumber or split these.

| Proposed ID | Description | Research Support |
|----|-------------|------------------|
| AUDIT-01 | Enumerate every `FlowType` (primitive + special + array + dict + tuple variants) and emit a producer/consumer table indexed by type. Source: reflection over `TypeSystem/{Primitive,Special}Types/` + `InternalFunctionRegistry.EnumerateSignatures()`. | Standard Stack §Graph Extraction; §Gap Class 1 |
| AUDIT-02 | List every orphaned type — type with zero consumers (no builtin accepts it as a param) OR zero producers (no builtin returns it). Phase 42 lists them; Phase 43/follow-up fixes them. | §Gap Class 1; pre-research confirmed `Beat` is orphaned |
| AUDIT-03 | List every missing conversion — pairs of types where `CanConvertTo` is asymmetric (A→B exists, B→A does not) AND the asymmetry is musically meaningful. Includes the tempo-context `Beat → Second` case named in the ROADMAP goal. | §Gap Class 2 |
| AUDIT-04 | List every asymmetric pair builtin — `writeX` without `readX`, `loadX` without `saveX`, `aToB` without `bToA`. Already-spotted candidate: `writeMidi` (builtin) vs `readMidi` (separate CLI `flow-midi`, not a builtin). | §Gap Class 3 |
| AUDIT-05 | List every dead-end builtin — function exists in the registry but no realistic call chain reaches it. **False-positive guard:** also grep `.flow` stdlib + `examples/*.flow` + `tests/test_*.flow` before flagging. | §Gap Class 4; §Pitfalls #1 |
| AUDIT-06 | List every overload gap — function takes `Double` but the music-type companion overload (`Decibel`/`Cent`/`Hertz`/`Millisecond`/`Second`/`Semitone`) is missing. Pre-research example: `pitchShift` has 24 overloads via prefix-ladder; `transpose` has Semitone+Cent overloads (good); `gain` accepts Decibel but `(noiseGate -40dB)` may not exist as a sibling. | §Gap Class 5 |
| AUDIT-07 | Inventory every `Math.Clamp` + `RenderingDiagnostics.WarnOnce` + courtesy-fallback site in stdlib for Phase 44 Axis B strict-mode error branch. Pre-research counts: ~72 `Math.Clamp` + ~117 `WarnOnce` + ~140 charitable-fallback references. | §Gap Class 6 (Phase 44 feed) |
| AUDIT-08 | Prioritize each finding (HIGH / MEDIUM / LOW) by composer-impact × ergonomics-impact, and map each to its consuming downstream phase (Phase 43 = module/naming, Phase 44 = strict-mode, follow-up = post-v1.5 backlog). | §AUDIT.md Structure |
| AUDIT-09 | Commit `AUDIT.md` to `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md`. No production-code edits in this phase. | §Out of Scope |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| FlowType enumeration | TypeSystem (`flow-lang/TypeSystem/`) | — | All types inherit `FlowType`; enumerated via Assembly reflection on `typeof(FlowType).Assembly` |
| Signature enumeration | StandardLibrary (`InternalFunctionRegistry.EnumerateSignatures`) | — | Already-public API added Phase 17 for LSP; returns `IEnumerable<KeyValuePair<string, IReadOnlyList<FunctionSignature>>>` |
| `.flow` stdlib parsing | (Audit-only — read as text) | — | `internal proc NAME(Type: param)` lines are stable regex targets; no full parse required |
| Conversion graph | TypeSystem (`FlowType.IsCompatibleWith` + `FlowType.CanConvertTo`) | — | Reflection invokes both methods on every (A, B) type pair to build adjacency |
| Clamp/advisory inventory | StandardLibrary (grep across all subdirs) | Diagnostics (`RenderingDiagnostics.WarnOnce` sentinel keys) | Pure source-text grep — `Math.Clamp\|RenderingDiagnostics.WarnOnce`. File:line pairs go straight into AUDIT-07 table |
| AUDIT.md authoring | (.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md) | — | Markdown synthesis from extracted tables — no code, no commits to `flow-lang/` |
| Audit harness | xUnit reflective audit (mirror Phase 29 `LicenseAuditTests`) OR standalone .NET console program | — | Existing precedent for reflective audits; can re-run after each downstream phase to verify gaps closed |
| graphify integration | `.planning/graphs/` + `gsd-tools graphify` | — | Currently disabled (`graphify.enabled = false` not set in config.json). Either enable + build (Wave 0), or skip and hand-roll — graphify operates at file/module granularity, may be too coarse for FlowType-level edges |

## Standard Stack

### Core (existing, no changes — Phase 42 is read-only)

| Library / Component | Version | Purpose | Why Standard |
|---|---|---|---|
| .NET 10 / C# 13 | net10.0 | Reflection over `typeof(FlowType).Assembly.GetTypes()` for type discovery | Already pinned milestone-wide |
| `InternalFunctionRegistry.EnumerateSignatures()` | n/a — public API on the class | Iteration entry point for ~370 registered signatures | Added Phase 17 (17-05) for LSP; same API powers the audit |
| `FlowType.IsCompatibleWith` + `FlowType.CanConvertTo` | n/a — base + per-type overrides | Conversion-graph adjacency | Already the single source of truth — no parallel definition |
| `FunctionSignature.InputTypes` + `FunctionSignature.Matches(argTypes)` | n/a — record property | Per-signature param type list | All registrations construct one `FunctionSignature`; trivially iterated |
| xUnit.v3 (3.2.2) | 3.2.2 | If audit is shipped as a reflective test | Mirrors `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` precedent |

### Supporting (audit infrastructure — choose ONE approach per AUDIT-09)

| Approach | Where it lives | Pros | Cons |
|---|---|---|---|
| **A. Standalone .NET console program** | `flow-lang/Tools/StdlibAuditor/Program.cs` (new) | Self-contained, runnable as `dotnet run --project flow-lang/Tools/StdlibAuditor`; emits AUDIT.md directly | New project to maintain; needs `<ProjectReference>` to flow-lang.csproj |
| **B. xUnit reflective audit tests** | `flow-lang.Tests/Integration/Phase42/StdlibAuditTests.cs` (new) | Mirrors LicenseAuditTests precedent (Phase 29); auto-runs in CI; can be a recurring health check | xUnit emits per-fact PASS/FAIL — AUDIT.md authored manually from test output |
| **C. Hand-rolled Bash + grep + jq** | `scripts/stdlib-audit.sh` (new) | No new code; runs anywhere | Misses runtime knowledge (specificity, IsCompatibleWith table); regex is brittle around multi-line `FunctionSignature` constructors |
| **D. graphify** | `.planning/graphs/graph.json` consumed manually | Reuses existing GSD tooling; structured node/edge output | Currently disabled in config.json; operates at file/module granularity (likely too coarse for FlowType-level edges); needs enable + build + interpret |

**Recommendation:** **Approach A (standalone .NET console program)** for the per-pass extractors (registration graph + conversion graph), **plus** **Approach C** (Bash + grep) for the clamp/advisory inventory which is pure source-text. **Skip Approach D** — graphify is module-granular and the audit needs FlowType-granular edges, which require reflection-aware extraction. Discuss-phase should confirm.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|---|---|---|
| Reflection over registry | Roslyn AST walk of `Register*` call sites | More accurate (catches `[StringType.Instance]` literal arrays) but ~10× more code. Reflection is correct here because every Register call materializes a `FunctionSignature` value that `EnumerateSignatures` exposes — no AST analysis required. |
| Producing AUDIT.md from inside a phase plan | Producing it as a docs-only commit | AUDIT.md is the deliverable. It is committed to `.planning/phases/42-type-system-stdlib-audit/` (per docs convention) so it survives the milestone and feeds the downstream phases — NOT to `flow-lang/`. |
| Treating reference-identity types (Tuning/Sfz/MarkovModel/LsystemModel/OscHandle) as orphans if they have few `.Instance` references | Different "is orphaned" rule per type kind | Reference-identity types are correctly used through 1 producer + 1 consumer (e.g., `loadScala` → `tuning t {}` block). They are NOT orphans even with 1 `.Instance` mention. The audit must distinguish coercible types from ref-identity types. |

### Installation

No new external packages. Phase 42 uses existing C# reflection + Bash grep.

## Package Legitimacy Audit

> Not applicable — Phase 42 installs no packages. AUDIT-09 ships docs only.

## Architecture Patterns

### System Architecture Diagram

```
                          ┌──────────────────────────────────────┐
                          │   flow-lang/TypeSystem/              │
                          │   {Primitive,Special}Types/*.cs      │
                          │   (29 sealed FlowType subclasses)    │
                          └────────────────┬─────────────────────┘
                                           │ reflection
                                           ▼
   ┌──────────────────────┐    ┌────────────────────────────┐    ┌─────────────────────┐
   │ flow-lang/*.flow     │    │ Audit Extractor (Approach   │    │ flow-lang/Standard- │
   │ (~470 internal proc  │───▶│  A: .NET console program)   │◀───│ Library/**.cs       │
   │  declarations)       │    │                            │    │ (~370 registry.     │
   │ — grep-extracted     │    │ Builds:                    │    │  Register calls)    │
   └──────────────────────┘    │   (1) Type→Sig adjacency   │    └─────────────────────┘
                               │   (2) Conversion graph     │
   ┌──────────────────────┐    │   (3) Clamp/advisory list  │
   │ scripts/clamp-grep   │───▶│   (4) Orphan candidates    │
   │ (~72 Math.Clamp +    │    │   (5) Asymmetric pairs     │
   │  ~117 WarnOnce +     │    │   (6) Overload gaps        │
   │  ~140 charitable)    │    └────────────┬───────────────┘
   └──────────────────────┘                 │
                                            ▼
                              ┌──────────────────────────────┐
                              │ .planning/phases/42-…/        │
                              │ 42-AUDIT.md                   │
                              │  ├─ §1 Orphaned types         │
                              │  ├─ §2 Missing conversions    │
                              │  ├─ §3 Asymmetric pairs       │
                              │  ├─ §4 Dead-end builtins      │
                              │  ├─ §5 Overload gaps          │
                              │  ├─ §6 Clamp/advisory site    │
                              │  │      inventory (→ Phase 44)│
                              │  └─ §7 Prioritization + phase │
                              │       routing (43 / 44 / v1.6)│
                              └──────────────────────────────┘
                                            │ feeds
                                            ▼
                              ┌──────────────────────────────┐
                              │ Phase 43 plan-phase           │
                              │   (module-qualification       │
                              │    consumer)                  │
                              ├──────────────────────────────┤
                              │ Phase 44 plan-phase           │
                              │   (strict-mode Axis B sites)  │
                              └──────────────────────────────┘
```

### Recommended Output Layout

```
.planning/phases/42-type-system-stdlib-audit/
├── 42-RESEARCH.md            # this file
├── 42-CONTEXT.md             # from /gsd:discuss-phase
├── 42-{N}-PLAN.md            # per-plan
├── 42-AUDIT.md               # ★ THE DELIVERABLE
├── 42-AUDIT-data/            # raw extractor outputs
│   ├── type-signature-graph.json
│   ├── conversion-graph.json
│   ├── clamp-sites.txt       (grep -rn output)
│   └── advisory-sites.txt    (grep -rn output)
└── 42-VERIFICATION.md
```

### Pattern 1: Reflective Type Enumeration
**What:** Discover every `FlowType` subclass without hard-coding a list.
**When to use:** Wave 1 — registration graph extraction.
**Example:**
```csharp
// Source: pattern derived from existing Value.cs:227 + FlowType base class
using System.Reflection;
using FlowLang.TypeSystem;

var flowTypeAssembly = typeof(FlowType).Assembly;
var allTypes = flowTypeAssembly.GetTypes()
    .Where(t => typeof(FlowType).IsAssignableFrom(t) && !t.IsAbstract)
    .Select(t =>
    {
        // singleton Instance property is the convention used by every sealed FlowType subclass
        var instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        return (Type: t, Instance: (FlowType?)instanceProp?.GetValue(null));
    })
    .Where(x => x.Instance is not null)
    .ToList();

// Yields ~29 (FlowType, Instance) pairs covering every Primitive + Special type
```

### Pattern 2: Signature Iteration
**What:** Enumerate every registered (name, signature) pair.
**When to use:** Wave 1 — both registration-graph + overload-gap passes.
**Example:**
```csharp
// Source: flow-lang/StandardLibrary/InternalFunctionRegistry.cs:133-140
var registry = new InternalFunctionRegistry();
BuiltInFunctions.RegisterAllImplementations(registry);  // wires every Register* method
// Also need SfzBuiltins + NotationIoBuiltins + OscFunctions (wired via FlowEngine — see BuiltInFunctions.cs:51-58)

foreach (var (name, sigs) in registry.EnumerateSignatures())
    foreach (var sig in sigs)
        Console.WriteLine($"{name}({string.Join(", ", sig.InputTypes)}) — params=[{string.Join(",", sig.ParameterNames ?? Array.Empty<string>())}]");
```
**Pitfall:** `BuiltInFunctions.RegisterAllImplementations` does NOT wire `SfzBuiltins.RegisterSfzBuiltins` (Phase 33), `NotationIoBuiltins.Register` (Phase 39), or `Network.OscFunctions.Register` (Phase 38) — those are wired directly in `FlowEngine.cs` because they need `ExecutionContext`. The audit harness must mirror `FlowEngine`'s full wiring, NOT just `BuiltInFunctions.RegisterAllImplementations`. Use `BuiltInFunctions.RegisterSignaturesOnly` (already exists for the LSP at line 90) — it wires EVERY signature including the context-dependent ones, with a stub delegate so the audit never executes business logic.

### Pattern 3: Conversion-Graph Build
**What:** For every (A, B) type pair, evaluate `A.IsCompatibleWith(B)` and `A.CanConvertTo(B)`; surface asymmetries.
**When to use:** Wave 1 — conversion graph pass.
**Example:**
```csharp
foreach (var a in allTypes)
    foreach (var b in allTypes)
    {
        bool aToB = a.Instance.IsCompatibleWith(b.Instance);
        bool bToA = b.Instance.IsCompatibleWith(a.Instance);
        if (aToB != bToA) Console.WriteLine($"ASYMMETRIC: {a.Type.Name} ↔ {b.Type.Name} compat=({aToB},{bToA})");

        bool aConvB = a.Instance.CanConvertTo(b.Instance);
        bool bConvA = b.Instance.CanConvertTo(a.Instance);
        if (aConvB != bConvA) Console.WriteLine($"ASYMMETRIC convert: {a.Type.Name} ↔ {b.Type.Name} conv=({aConvB},{bConvA})");
    }
// Expected musical findings: Beat→Double exists but Double→Beat does NOT (Beat is "tagged Double" — D-v1.5-08 article).
// Decibel/Cent/Hertz/Millisecond/Second all do A→Double + A→Float — but Double→Decibel does NOT exist anywhere (Phase 44 explicit-conversion builtin `(db x)` will fix).
```

### Pattern 4: `.flow` Stdlib Reverse-Reference
**What:** Grep `internal proc NAME` declarations in `flow-lang/*.flow` and cross-reference against the C# registry.
**When to use:** Wave 1 — false-positive guard for AUDIT-05 dead-end builtins.
**Example:**
```bash
grep -rn "^internal proc " flow-lang/*.flow | sed 's/.*proc //; s/(.*//' | sort -u > /tmp/flow-procs.txt
# Cross-reference: a C# builtin in registry with zero callers in flow-lang/*.flow AND zero callers in tests/test_*.flow
# AND zero callers in examples/**/*.flow is a real dead-end candidate.
```

### Anti-Patterns to Avoid

- **Flagging reference-identity types as orphans by `.Instance` count.** `Tuning` has 1 `.Instance` mention (`(loadScala)` producer) and zero consumer registrations — but it IS consumed via `tuning t { ... }` block evaluation in the interpreter, not at the registry layer. Similarly `Sfz` (consumed via `"sampler:NAME"` string-dispatch). The orphan rule for ref-identity types must check both registry consumers AND interpreter consumers (`Interpreter/Interpreter.cs` + block-statement evaluators).
- **Grep-only Bash for the graph.** A registered `FunctionSignature` constructor often wraps across 2-3 lines (`new FunctionSignature("name", [Type.Instance, Type.Instance], …)`). Multi-line regex on Bash is fragile. Use the reflective extractor for the graph; reserve Bash grep for the clamp/advisory site list which is single-line `Math.Clamp(` calls.
- **Treating the audit's findings as automatic-fix queue.** AUDIT.md is a **prioritized gap list**, not a TODO. Some gaps are intentional (e.g., `Sfz` lacks a `gain(Sfz, Decibel)` overload because gain is a Buffer operation, not a sampler-patch operation). The audit calls out each gap with a "fix in phase X" routing, but the composer/researcher of phase X is the decision authority.
- **Building AUDIT.md without the clamp/advisory inventory.** Phase 44 EXPLICITLY says "Phase 42 audit provides the clamp/advisory site inventory needed to confidently enumerate Axis B sites — missing any one regresses the strict contract" (ROADMAP line 380). AUDIT-07 is load-bearing for Phase 44 — it is not an optional add-on.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| FlowType discovery | A hand-maintained list of every type in `Primitive*` + `Special*` | `Assembly.GetTypes()` reflection (Pattern 1) | Hand-maintained list goes stale every time a new type ships (Phase 36 added MarkovModel + LsystemModel; Phase 38 added OscHandle — three opportunities to forget) |
| Signature enumeration | A hand-rolled signature-collector that walks `Register` call sites with Roslyn | `InternalFunctionRegistry.EnumerateSignatures` (already exists, line 133) | The LSP already needs this; reusing it makes the audit harness 50 lines instead of 500 |
| Module activation | Calling `Register*` methods manually in the audit harness | `BuiltInFunctions.RegisterSignaturesOnly(registry)` (line 90) | Already wires EVERY path including Sfz / NotationIO / OSC / context-dependent ones; designed for exactly this read-only-introspection use case |
| Conversion-graph adjacency | A separate enumeration of "what converts to what" | Invoking `IsCompatibleWith` + `CanConvertTo` directly on every (A,B) pair | These methods ARE the spec — any other source is by definition out of sync |
| Clamp/advisory dedup | A hand-rolled tracker | grep + sort -u + format | Phase 42 is read-only; no runtime dedup needed |

**Key insight:** Every primitive needed for the audit already exists in the codebase. Phase 42 is primarily a *synthesis* phase — wire existing extractors together, produce a markdown report, route findings to phases 43/44. No new runtime, no new type, no new builtin.

## Runtime State Inventory

> Not applicable — Phase 42 ships documentation only. No data migrations, no service config changes, no OS-registered state, no secrets, no build artifacts beyond AUDIT.md.

- **Stored data:** None — verified by Out-of-Scope rule.
- **Live service config:** None — read-only audit.
- **OS-registered state:** None.
- **Secrets/env vars:** None.
- **Build artifacts:** Only `AUDIT.md` + optional `42-AUDIT-data/` JSON/txt extractor outputs under `.planning/`.

## Common Pitfalls

### Pitfall 1: False-Positive "Dead-End Builtin"
**What goes wrong:** A C# builtin (e.g. `barLength`) shows zero callers in `flow-lang/StandardLibrary/**.cs` — flagged as dead-end — but it's actually called from `flow-lang/bars.flow:22` and used heavily by `examples/tutorial.flow`.
**Why it happens:** The `.flow` stdlib is the *missing half* of the registration graph. C# registers an implementation; `.flow` declares the proc that binds to it. Grep one without the other and every flow-stdlib function looks dead.
**How to avoid:** Cross-reference EVERY candidate against three sources: (a) `flow-lang/*.flow`, (b) `examples/**/*.flow`, (c) `tests/test_*.flow`. Only flag dead-end if all three return zero.
**Warning signs:** Audit table shows >20 "dead-end" findings — almost certainly a false-positive flood.

### Pitfall 2: Reference-Identity Types Counted as Orphans
**What goes wrong:** `Tuning` has 1 `.Instance` mention; `Sfz` has 0; `MarkovModel` has 3. The pure-count heuristic flags them as near-orphans.
**Why it happens:** Reference-identity types (`TuningType`, `SfzType`, `MarkovModelType`, `LsystemModelType`, `OscHandleType`) have `IsCompatibleWith(target) => target is X` — they never participate in numeric coercion. Their *only* call sites are (a) their producer (`loadScala` / `loadSfz` / `markovTrain` / etc.) and (b) their consumer (`tuning t {}` block / `"sampler:NAME"` string dispatch / `markovGenerate`). Two sites is the *minimum legitimate count*, not a sign of orphan-ness.
**How to avoid:** Classify each FlowType as **coercible** (overrides `IsCompatibleWith` to widen) vs **reference-identity** (strict equality only). Apply different "is orphaned" rules: coercible = "no consumer at all", ref-identity = "no producer OR no consumer beyond its own load/save pair".
**Warning signs:** AUDIT-02 list contains any ref-identity type — re-check before publishing.

### Pitfall 3: Misclassifying `Beat → Second` Conversion as "Just Add an Override"
**What goes wrong:** The audit suggests "add `BeatType.CanConvertTo(SecondType)`". But Beat→Second is **context-dependent** — it requires the active `tempo` value from `ExecutionContext.MusicalContext`. A pure `FlowType` method has no access to that.
**Why it happens:** `IsCompatibleWith` / `CanConvertTo` are pure functions on type identity, not on runtime context. They cannot do tempo-aware math.
**How to avoid:** Phase 42 documents the **gap**; Phase 43 or a follow-up decides the **shape of the fix** (likely a new builtin `(beatToSec Beat)` that reads context, NOT a type-system override). The audit must not over-prescribe.
**Warning signs:** AUDIT-03 recommendations include `CanConvertTo` overrides for music-type→time-type or time-type→music-type conversions — these are almost always context-dependent.

### Pitfall 4: Counting `Math.Clamp` Sites Without Reading Each One
**What goes wrong:** Pre-research counted ~72 `Math.Clamp` and ~117 `WarnOnce` sites. Auto-listing all of them creates noise — some clamps are pure output-domain hygiene (e.g., `Math.Clamp(midi, 0, 127)` because MIDI bytes are 0-127 *by spec*, not because the composer might violate it).
**Why it happens:** Phase 44's "input-perimeter clamp" rule applies to clamps that **silently fix composer mistakes** at the API surface — not to internal algorithm-output clamps that protect downstream invariants. Strict mode should error on the former, not the latter.
**How to avoid:** Classify each clamp as **input-perimeter** (composer's arg was out of range) vs **output-protection** (algorithm's intermediate result needs bounds for downstream consumers). Only input-perimeter sites belong in AUDIT-07. Use the heuristic: clamp is on `args[N].As<...>()` direct return → input-perimeter. Clamp is on the result of internal math → output-protection.
**Warning signs:** AUDIT-07 has 70+ entries — most are probably output-protection and should be culled.

### Pitfall 5: Asymmetric-Pair False Positives from Lifecycle Asymmetry
**What goes wrong:** `oscListen` exists but `oscUnlisten` doesn't — flagged as asymmetric pair. But the real lifecycle pair is `oscListen → oscStop` (already exists, D-38-16).
**Why it happens:** Surface-level grep for "`writeX` / `readX`" / "`X` / `unX`" doesn't see semantically equivalent names.
**How to avoid:** Asymmetric-pair detection should produce a *candidate list* that a human reviews before publishing. Candidate sources: (a) verb pairs from a small dictionary (write↔read, load↔save, listen↔stop, start↔stop, push↔pop, encode↔decode, train↔generate), (b) `to-X` / `from-X` symmetry. Review removes false positives.
**Warning signs:** AUDIT-04 lists obviously-OK names like `markovTrain` without `markovUntrain` — review didn't happen.

### Pitfall 6: graphify is Module-Granular
**What goes wrong:** Phase goal names "Graphify-driven sweep" but graphify operates at file/module granularity (per `.planning/graphs/graph.json` schema). It can tell us "ChordParser.cs references HarmonyFunctions.cs" but cannot tell us "the `Decibel` type has zero consumers in `EffectsFunctions.cs`".
**Why it happens:** graphify is a code-graph tool, not a type-graph tool. Its discovery model is "files and their imports / call relationships", not "types and their producer/consumer registrations".
**How to avoid:** Use graphify as a **complementary discovery surface** (which modules touch the type system at all → focus the reflective audit there) but build the actual FlowType→Sig adjacency table via Pattern 1+2 reflection. The phase plan should not depend on graphify producing FlowType-level edges.
**Warning signs:** A plan step says "graphify will identify orphaned types" — it cannot; replace with reflection step.

### Pitfall 7: Audit Stale Between Phase 42 and Phase 44
**What goes wrong:** AUDIT.md is written 2026-05-25; Phase 43 ships 2026-05-30 and renames `gain`→`audio.gain` (D-43-01); Phase 44 reads AUDIT-07 which still says `gain` at line N — line numbers shift.
**Why it happens:** AUDIT.md is a snapshot of `flow-lang/` at one moment.
**How to avoid:** AUDIT.md cites **builtin name + signature** as the stable identifier, NOT file:line. File:line goes in a supplementary `42-AUDIT-data/clamp-sites.txt` that is regenerated at Phase 44 plan-start.
**Warning signs:** AUDIT.md has dozens of `BuiltInFunctions.cs:1672` references — these will rot. Use names.

## Code Examples

### Audit Harness Skeleton (recommended Approach A)

```csharp
// Source: synthesis of InternalFunctionRegistry.EnumerateSignatures (line 133)
// + BuiltInFunctions.RegisterSignaturesOnly (line 90) precedent.
// Location: flow-lang/Tools/StdlibAuditor/Program.cs (new project)

using System.Reflection;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;

var registry = new InternalFunctionRegistry();
BuiltInFunctions.RegisterSignaturesOnly(registry);  // wires every path

// (1) Type inventory
var typeAsm = typeof(FlowType).Assembly;
var allTypes = typeAsm.GetTypes()
    .Where(t => typeof(FlowType).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
    .Select(t => (Name: t.Name, Instance: (FlowType?)t.GetProperty("Instance",
        BindingFlags.Public | BindingFlags.Static)?.GetValue(null)))
    .Where(x => x.Instance is not null)
    .ToList();

// (2) Build producer + consumer map keyed by type Name
var consumers = new Dictionary<string, List<string>>();  // typeName → [sigStr]
var producers = new Dictionary<string, List<string>>();  // (not populated — return types are NOT
                                                          //  in FunctionSignature today; see Open Question 1)
foreach (var t in allTypes) consumers[t.Name] = new();

foreach (var (name, sigs) in registry.EnumerateSignatures())
    foreach (var sig in sigs)
        foreach (var paramType in sig.InputTypes)
            if (consumers.TryGetValue(paramType.Name, out var list))
                list.Add(sig.ToString());

// (3) Orphan list — coercible types with no consumers
var coercibleTypes = allTypes.Where(t => OverridesIsCompatible(t.Instance!.GetType())).ToList();
foreach (var t in coercibleTypes)
{
    if (consumers[t.Name].Count == 0)
        Console.WriteLine($"ORPHAN: {t.Name} — no consumer in registry");
}

// (4) Asymmetric conversion list
foreach (var a in allTypes)
    foreach (var b in allTypes)
    {
        if (a.Name == b.Name) continue;
        bool aToB = a.Instance!.IsCompatibleWith(b.Instance!);
        bool bToA = b.Instance!.IsCompatibleWith(a.Instance!);
        if (aToB != bToA)
            Console.WriteLine($"ASYM: {a.Name}.IsCompatibleWith({b.Name})={aToB} but reverse={bToA}");
    }

static bool OverridesIsCompatible(Type t) =>
    t.GetMethod(nameof(FlowType.IsCompatibleWith),
        BindingFlags.Public | BindingFlags.Instance)!.DeclaringType == t;
```

### Clamp/Advisory Site Extraction (recommended Approach C — Bash)

```bash
# Source: synthesis of pre-research grep patterns.
# Output: 42-AUDIT-data/clamp-sites.txt + advisory-sites.txt

mkdir -p .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data

# Input-perimeter clamp candidates — Math.Clamp on a direct args[N].As<T>() read
grep -rn "Math\.Clamp.*args\[" flow-lang/StandardLibrary/ \
  > .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/input-clamps.txt

# All clamp sites (for triage)
grep -rn "Math\.Clamp\|Math\.Min.*Math\.Max" flow-lang/StandardLibrary/ \
  > .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/all-clamps.txt

# Advisory sites
grep -rn "RenderingDiagnostics\.WarnOnce" flow-lang/ \
  > .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/advisory-sites.txt

# Charitable fallback markers (informal — used as triage signal)
grep -rn "fallback\|charitable\|else.*return.*input" flow-lang/StandardLibrary/ \
  > .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/charitable-sites.txt

# Counts (for AUDIT.md §6 summary)
wc -l .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/*.txt
```

### `.flow` Stdlib Cross-Reference (false-positive guard)

```bash
# Build the .flow-declared proc list
grep -rhn "^internal proc \|^proc " flow-lang/*.flow examples/**/*.flow tests/test_*.flow 2>/dev/null \
  | sed -E 's/.*proc +([a-zA-Z_][a-zA-Z0-9_]*).*/\1/' \
  | sort -u > /tmp/flow-callers.txt

# For each "dead-end" candidate from the reflective audit, check whether the .flow side calls it
# Example: candidate is `createAR`
grep -c "^createAR$" /tmp/flow-callers.txt
# If >0, NOT a dead-end — drop from AUDIT-05
```

## State of the Art

| Old approach | Current approach | When changed | Impact |
|---|---|---|---|
| Hand-maintained list of every FlowType in docs | `Assembly.GetTypes()` reflection (Pattern 1) | Phase 17 (LSP) onward — `EnumerateSignatures` proved the reflective approach worked | Audit harness stays correct as new types ship |
| Per-file `Register*` discovery via Roslyn AST | `InternalFunctionRegistry.EnumerateSignatures` consumed at runtime | Phase 17 (17-05) added the API | 5× less code; impossible to miss a registration that compiled |
| `Beat` arithmetic via `Double` widening | Same — no Beat-specific builtins exist today | Phase 36+ added `Beat` to the IsHashable + Dict-key contract but never added Beat-aware arithmetic | Confirms the orphan finding |

**Deprecated/outdated:**
- The pre-Phase-36 LSP path that re-declared signatures in a parallel registry: replaced by `RegisterSignaturesOnly` (BuiltInFunctions.cs:90). Audit harness uses the same path.
- No deprecated audit infrastructure to remove — Phase 42 is greenfield in `.planning/phases/`.

## Project Constraints (from CLAUDE.md)

- **GSD Workflow Enforcement:** Phase 42 runs inside `/gsd:execute-phase 42`. AUDIT.md is created via the GSD planner/executor, not by direct edits outside a GSD workflow.
- **Ergonomics first:** AUDIT.md recommendations should preserve composer ergonomics. A "missing overload" finding is valid only if the composer's natural call shape fails today — `(reverb buf 2.5)` works (Second IsCompatibleWith Double); it is NOT a gap. `(noiseGate -40dB)` failing IS a gap.
- **Charitable interpretation:** AUDIT-07 (clamp/advisory site list) feeds Phase 44 *additive* strict mode. Charitable behavior remains the **default** — the audit must not recommend removing courtesy fallbacks, only inventorying them for strict-mode opt-in. ROADMAP line 378 makes this explicit.
- **Genre-agnostic, music-only scope:** Audit recommendations must not propose features whose only justification is non-musical use (e.g., do not propose "add `Hertz → Byte[]` for arbitrary network protocols").
- **Pre-public no-deprecation latitude:** AUDIT.md can recommend breaking renames freely for Phase 43 — `gain` may become `audio.gain` without a compatibility shim. No "v1.6 compat shim" suggestions.
- **No unit-test framework — tests are `.flow` scripts:** If Approach B (xUnit) is chosen, it lives in `flow-lang.Tests/` as a reflective audit alongside `LicenseAuditTests`, NOT in `tests/`.
- **Two-run cmp-clean determinism:** Not applicable — audit ships docs, no rendered bytes.
- **External deps:** Phase 42 adds zero NuGet packages. If Approach A is taken, the new `flow-lang/Tools/StdlibAuditor/` project uses only `<ProjectReference Include="../../flow-lang.csproj" />`.

## Assumptions Log

| # | Claim | Section | Risk if wrong |
|---|---|---|---|
| A1 | `BuiltInFunctions.RegisterSignaturesOnly` correctly wires every Register path including SfzBuiltins, NotationIoBuiltins, and OscFunctions. | §Pattern 2 + §Code Examples | If wrong, audit harness misses entire stdlib modules — false-negative orphans (e.g., MusicXML emit functions appear dead-end). Mitigation: cross-check `EnumerateSignatures()` output count against grep-count of `registry.Register` (expect ~370). |
| A2 | `Math.Clamp` is the dominant input-perimeter-clamp idiom; classifying by "is the arg `args[N].As<...>()`?" catches >80% of strict-mode-relevant sites. | §Pitfall 4 + §Code Examples | If wrong, AUDIT-07 misses sites that use bespoke clamp patterns (`if (x < 0) x = 0`). Mitigation: secondary grep for `if.*<.*0.*=` patterns. |
| A3 | graphify is module-granular and not suitable for FlowType-edge extraction. | §Architecture + §Pitfall 6 | If graphify already supports type-level edges in a recent version, the audit can use it directly — would simplify the harness. Mitigation: Wave 0 enables graphify and confirms schema before committing to Approach A. |
| A4 | `Beat`'s zero-references-in-StandardLibrary finding generalizes to "no Beat consumers in any registered signature" — i.e., the orphan is real. | §Summary | If wrong, Phase 42 anchor example is incorrect. Mitigation: low risk — verified by direct grep (`grep -rn "BeatType.Instance" flow-lang/StandardLibrary/` returns 0). |
| A5 | The phase ships in 1-2 days. | §Summary | Audit may surface more gaps than expected; AUDIT.md authoring takes longer than extraction. Mitigation: time-box authoring to one day; if it overflows, ship a v1 AUDIT.md and follow-up v2. |

## Open Questions

1. **Should `FunctionSignature` gain a `ReturnType` field?**
   - What we know: `FunctionSignature(name, InputTypes, IsVarArgs, ParameterNames)` has no return-type slot. Return type is implicit in the `Func<IReadOnlyList<Value>, Value>` implementation.
   - What's unclear: without return-type metadata, the audit cannot build the **producer** half of the type→sig graph (only consumers). Orphan detection ("type T has no producer") is impossible without it.
   - Recommendation: Phase 42 documents this limitation in AUDIT.md §Limitations. Adding `ReturnType` is a Phase 43+ concern (it's a registration-API change, not a Phase 42 read-only-audit concern). For now, producers are inferred manually from the function name + `Value.X()` calls inside the delegate body (e.g., `loadScala` is `Tuning`-producing because it returns `Value.Tuning(...)`). This is acceptable for the v1.5 audit but should be flagged.

2. **Where does AUDIT.md live — `.planning/phases/42-…/` or top-level `flow-lang/`?**
   - What we know: ROADMAP says "produces prioritized AUDIT.md gap list that feeds Phases 43 + 44".
   - What's unclear: not specified.
   - Recommendation: `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md`. It is a phase deliverable consumed by next-phase researchers, mirrors the `42-RESEARCH.md` / `42-CONTEXT.md` convention, and survives the milestone close. It is NOT shipped to end-users via the flow-lang library.

3. **Approach A vs B (console program vs xUnit reflective test) — which one?**
   - What we know: Both reuse `EnumerateSignatures` + reflection.
   - What's unclear: Whether the audit is a one-shot Phase 42 deliverable or a recurring CI health check.
   - Recommendation: **Approach A** for v1 (faster to ship; one Program.cs vs an xUnit test class with `[Theory]` rows for every gap class). Promote to Approach B in v1.6 IF the audit becomes a recurring concern. Discuss-phase should confirm.

4. **Should AUDIT-08 (prioritization) be researcher-discretion or composer-decided?**
   - What we know: Phase 42 produces a "prioritized" gap list per ROADMAP.
   - What's unclear: Who decides priority — the audit author (Claude) or the composer (Noah)?
   - Recommendation: Audit author proposes priority HIGH/MEDIUM/LOW with a one-line rationale; composer reviews at `/gsd:verify-work` time and reorders if needed. This is the standard `feedback_ergonomics_priority` pattern.

5. **Does Phase 42 also audit the test-coverage gap for each FlowType?**
   - What we know: ROADMAP scope is "FlowType ↔ builtin-signature graph". Test coverage is a sibling concern.
   - What's unclear: Whether AUDIT.md should also list types with zero `tests/test_<type>*.flow` coverage.
   - Recommendation: Out of scope for v1 — the existing `CODEBASE-AUDIT-2026-04-18.md` already covers test gaps. If Phase 42's reflective harness is built and is cheap to extend, add a §X TEST COVERAGE section as a stretch goal.

## Environment Availability

| Dependency | Required by | Available | Version | Fallback |
|---|---|---|---|---|
| .NET 10 SDK | Approach A — running the audit harness | (assumed yes — used by the rest of the project) | net10.0 | — |
| bash + grep | Approach C — clamp-site extraction | yes | — | — |
| node + `gsd-tools` | graphify (if Wave 0 enables it) | yes | per `node /home/noah/.claude/get-shit-done/bin/gsd-tools.cjs graphify status` | Skip graphify; use Approach A only |
| `mscore` / external tools | none required for audit | — | — | — |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none — all primary approaches are self-sufficient.

## Validation Architecture

> Phase 42 ships docs only. Nyquist validation per `config.workflow.nyquist_validation=true` means each per-plan task needs an automated check.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 (for the audit harness's self-check, if Approach A or B is taken) + `.flow` scripts in `tests/` (no change) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing); new project iff Approach A is chosen |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase42"` (per existing precedent — `Phase29`, `Phase35`, `Phase36`) |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test type | Automated command | File exists? |
|--------|----------|-----------|-------------------|--------------|
| AUDIT-01 | Type+signature graph enumerated to JSON | smoke | `dotnet run --project flow-lang/Tools/StdlibAuditor -- --emit-json /tmp/g.json && jq '.types\|length>=20' /tmp/g.json` | ❌ Wave 0 |
| AUDIT-02 | Orphan list non-empty AND contains `Beat` | smoke | `jq '.orphans \| index("Beat")' /tmp/g.json` | ❌ Wave 0 |
| AUDIT-03 | Conversion-asymmetry list emitted | smoke | `jq '.asymmetries \| length' /tmp/g.json` | ❌ Wave 0 |
| AUDIT-04 | Asymmetric-pair list contains expected `writeMidi/readMidi` finding | unit | `dotnet test --filter "FullyQualifiedName~Phase42.AsymmetricPairs.WriteMidiHasNoReadMidi"` | ❌ Wave 0 |
| AUDIT-05 | Dead-end builtin candidates pruned by `.flow` cross-reference | unit | `dotnet test --filter "FullyQualifiedName~Phase42.DeadEnds.AllCandidatesHaveZeroFlowCallers"` | ❌ Wave 0 |
| AUDIT-06 | Overload-gap list emitted | smoke | `jq '.overload_gaps \| length' /tmp/g.json` | ❌ Wave 0 |
| AUDIT-07 | Clamp/advisory site count matches `grep` independently | smoke | `[[ $(wc -l < clamps.txt) -gt 50 ]]` | ❌ Wave 0 |
| AUDIT-08 | AUDIT.md contains all 7 sections + every finding has phase routing | manual | composer review at `/gsd:verify-work` | n/a |
| AUDIT-09 | AUDIT.md committed | `gsd-sdk query commit-check 42-AUDIT.md` | n/a | n/a |

### Sampling Rate
- **Per task commit:** the relevant xUnit filter above (sub-second)
- **Per wave merge:** full audit harness run + `dotnet test --filter Phase42`
- **Phase gate:** Full suite green + composer manual review of AUDIT.md before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `flow-lang/Tools/StdlibAuditor/Program.cs` — Approach A skeleton
- [ ] `flow-lang/Tools/StdlibAuditor/StdlibAuditor.csproj` — new project file
- [ ] `flow-lang.Tests/Integration/Phase42/AuditHarnessSelfCheckTests.cs` — harness self-check
- [ ] `flow-lang.Tests/Integration/Phase42/AsymmetricPairsTests.cs` — known-pair regressions
- [ ] `flow-lang.Tests/Integration/Phase42/DeadEndCrossReferenceTests.cs` — .flow guard
- [ ] OPTIONAL: enable graphify in `.planning/config.json` (`graphify.enabled = true`) + `node $HOME/.claude/get-shit-done/bin/gsd-tools.cjs graphify build`

## Security Domain

> `security_enforcement` is unset in config — treated as enabled per defaults.

### Applicable ASVS Categories

| ASVS category | Applies | Standard control |
|---|---|---|
| V2 Authentication | no | Phase 42 has no auth surface |
| V3 Session Management | no | No sessions |
| V4 Access Control | no | Audit is read-only against in-repo files |
| V5 Input Validation | yes (narrow) | Audit harness parses its own grep output — accept only file:line:content triples; reject malformed lines silently |
| V6 Cryptography | no | No crypto |
| V7 Errors | yes | Audit harness should fail loudly on unexpected reflection errors, NOT silently emit a partial graph |
| V8 Data Protection | no | No PII; AUDIT.md is committed to a public repo |
| V12 Files | yes | Audit reads from `flow-lang/`; writes to `.planning/phases/42-…/`. No paths outside repo root |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard mitigation |
|---|---|---|
| Audit harness loads malicious assembly via reflection | Tampering / Elevation | Harness loads only `typeof(FlowType).Assembly` — a fixed in-repo build artifact, not user-supplied |
| AUDIT.md leaks accidental secret discovered while grepping | Information disclosure | Audit grep targets are scoped to `flow-lang/StandardLibrary/` source files — no `.env`, no `~/.config/flow/`, no `Vendor/` keys |
| Audit harness exits unsuccessfully and corrupts AUDIT.md mid-write | DoS via partial state | Write AUDIT.md to a tempfile + atomic rename, per `MusicXmlExport.cs:NewLineChars` precedent |

## Sources

### Primary (HIGH confidence — read source directly)

- `flow-lang/TypeSystem/FlowType.cs` — base class API: `IsCompatibleWith`, `CanConvertTo`, `GetSpecificity`, `IsHashable`, `Equals`. 75 lines, no surprises.
- `flow-lang/TypeSystem/FunctionSignature.cs` — record def, `Matches`, `CalculateSpecificity`, `ParameterNames` (Phase 36 D-36-11 extension).
- `flow-lang/TypeSystem/OverloadResolver.cs` — specificity tiers: exact +1000 / compatible +500 / convertible +100, ambiguity check at top-2-equal-score, named-arg validation gates.
- `flow-lang/TypeSystem/{PrimitiveTypes,SpecialTypes}/*.cs` — all 29 concrete FlowType subclasses verified: 16 primitives, 13 specials. Confirmed: BeatType.IsCompatibleWith(Double|Float)=true but no `BeatType.Instance` mention anywhere in `StandardLibrary/`.
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — registry shape + `EnumerateSignatures` public API + Void wildcard semantics + DictType wildcard symmetric.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — main registration site (211 Register calls); `RegisterAllImplementations` + `RegisterSignaturesOnly` (line 90, the audit-friendly path) + `StubbingRegistryProxy` (line 131).
- `flow-lang/Diagnostics/RenderingDiagnostics.cs` — WarnOnce dedup channel; 117 call sites verified by `grep -c`.
- `flow-lang/*.flow` — 12 stdlib files declaring `internal proc` signatures (audio.flow 158, std.flow 166, transforms via composition.flow + bars.flow + collections.flow + ...). Confirms cross-reference half of the registration graph.
- `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` — reflective audit precedent for Approach B.
- `.planning/CODEBASE-AUDIT-2026-04-18.md` — existing 5-tier audit format reference (#### Critical Bugs / Major / Minor / Test Coverage Gaps / Feature Opportunities). AUDIT.md may borrow this structure.
- `.planning/ROADMAP.md` lines 348-380 — phase goals 42/43/44 + dependency arrows.
- `.planning/STATE.md` — v1.5 status; Phase 42-44 added 2026-05-24 per ROADMAP line 10.

### Secondary (MEDIUM confidence — read partial, inferred)

- `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` — reflective bundle audit precedent (similar shape to what AUDIT-07 would do).
- `~/.claude/skills/gsd-graphify/SKILL.md` — graphify is config-gated; build chain is `graphify update . && cp graph.json .planning/graphs/`. Module/file granularity (verified by reading the build chain — extracts AST relationships per file).
- ROADMAP "v1.5 closeout trio" framing — Phases 42/43/44 are explicit dependency chain. Pre-research confirms Phase 42 has NO `flow-lang/` code changes by design.

### Tertiary (LOW confidence — flag for verification)

- Estimate of ~370 total `registry.Register` calls across stdlib — derived from `grep -c registry.Register` across all subdirs (211 + 24 + 21 + 15 + ...). Could be slightly off if some files registered via different APIs.
- "Beat is completely orphaned" — verified by `grep -rn "BeatType" flow-lang/StandardLibrary/` returning 0 hits + `grep -rn "BeatType.Instance" flow-lang/` returning only Value.cs:42 (constructor) + Interpreter.cs:1019 (default value). Confidence raised to HIGH by triple-source grep.

## Metadata

**Confidence breakdown:**
- Standard stack (reflection over registry): HIGH — entire approach uses existing public APIs (`EnumerateSignatures`, `RegisterSignaturesOnly`).
- Architecture (3-pass extraction + synthesis): HIGH — clear data flow, no novel components.
- Gap classes (orphan / asymmetric / dead-end / overload / clamp): HIGH for definitions + examples; MEDIUM for completeness (audit may surface unanticipated classes).
- graphify integration: MEDIUM — currently disabled; schema not verified for FlowType-level edges.
- Pitfalls: HIGH — five of the seven pitfalls were already encountered during pre-research.

**Research date:** 2026-05-24
**Valid until:** 2026-06-23 (~30 days, codebase stable — Phase 38 in progress but not touching TypeSystem)
