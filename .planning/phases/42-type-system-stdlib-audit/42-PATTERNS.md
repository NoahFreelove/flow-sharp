# Phase 42: Type System & Stdlib Audit — Pattern Map

**Mapped:** 2026-05-24
**Files analyzed:** 5 candidate new-file slots
**Analogs found:** 5 / 5 (all in-tree, all read-only-reflective or markdown-report)

This phase is **audit-only** — `AUDIT.md` is the deliverable; the harness exists solely to produce / regenerate it. No production source is modified, so the only file-creation slots are (a) the markdown deliverable, (b) the audit harness (one console-program path OR one xUnit-fixture path), (c) optional Bash grep scripts. Both harness paths have direct in-tree analogs.

## File Classification

| New file slot | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` | doc / report | batch (synthesis) | `.planning/CODEBASE-AUDIT-2026-04-18.md` | exact (same 5-tier audit-report shape) |
| `flow-lang/Tools/StdlibAuditor/Program.cs` *(if Approach A)* | tool / console | batch (reflective enumerate → emit JSON+MD) | `scripts/Migrate26/Program.cs` | exact (standalone console targeting flow-lang.csproj) |
| `flow-lang/Tools/StdlibAuditor/StdlibAuditor.csproj` *(if Approach A)* | config / project | n/a | `scripts/Migrate26/Migrate26.csproj` | exact (3-line `<OutputType>Exe</OutputType>` + ProjectReference) |
| `flow-lang.Tests/Integration/Phase42/StdlibAuditTests.cs` *(if Approach B, OR as harness self-check under A)* | test / reflective audit | request-response (assert on enumerated graph) | `flow-lang.Tests/Phase36/PrngRegistryNewRandomGateTests.cs` + `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` | exact (same `FindRepoRoot` walk + per-fact `[Theory]` shape) |
| `scripts/audit/clamp-grep.sh` *(if grep pass is scripted vs ad-hoc)* | tool / shell script | streaming (grep → file) | `scripts/test_two_run_determinism.sh` | role-match (Bash script under `scripts/` with set -euo pipefail + usage block) |

**Files modified:** NONE. The Phase 42 invariant ("read-only audit") is gate-enforced — if any plan task touches `flow-lang/StandardLibrary/**` or `flow-lang/TypeSystem/**` it has escaped scope.

## Pattern Assignments

### `42-AUDIT.md` (doc, markdown report)

**Analog:** `.planning/CODEBASE-AUDIT-2026-04-18.md`

**Why:** This is the closest existing precedent for a multi-tier prioritized gap list inside `.planning/`. Same audience (composer + downstream phase researcher), same use as a phase-handoff artifact, same tier-organized table shape. RESEARCH.md §Sources line 550 explicitly names it as the structural reference (`AUDIT.md may borrow this structure`).

**Header pattern** (lines 1-7):
```markdown
# Flow Language — Codebase Audit

**Date:** 2026-04-18
**Scope:** Read-only analysis of `flow-lang/`, `flow-interpreter/`, `tests/`, and planning docs.
**Method:** 5 parallel exploration agents covering (1) lexer/parser/interpreter/runtime/types, (2) audio subsystem, (3) stdlib/harmony/transforms, (4) test coverage, (5) feature opportunities. Findings below are synthesized from those agents — file:line references should be spot-checked before acting on any individual item, since some agent claims were speculative.

---
```

**Tier-table pattern** (lines 9-21 — section-numbered with table headers):
```markdown
## 1. Critical Bugs (blocking or data-loss)

| # | Where | Issue |
|---|-------|-------|
| C1 | `Interpreter/Interpreter.cs` ~133-289 (`ExecuteMusicalContext`) | A frame is pushed early; multiple validation paths (`tempo -1`, `timesig 0/4`, bad key, etc.) `return` before reaching the pop. After the first validation error inside a context block, the musical-context stack is left unbalanced and every subsequent statement runs in the wrong scope. |
```

**Per-tier priority+rationale pattern** (lines 109-114):
```markdown
### Tier A — Small, high-leverage
1. **Sequence slicing & phrase-edit** (`slice(seq, start, end)`, `loopEdit(...)`) — S — `BuiltInFunctions.cs`, `audio.flow`. Fills an obvious composition-workflow gap.
2. **Note-name aliases & enharmonic helpers** (`H` = `B`, `Db` ↔ `C#`, `enharmonic()`) — S — `Lexing/SimpleLexer.cs`, `Parsing/Parser.cs`, `PitchConversion.cs`. Pedagogical and non-breaking.
```

**Routing-decision footer pattern** (lines 137-156):
```markdown
## 6. Recommended Next Phase

**Phase: "Stability & Correctness" (1 week)**

Bundle the critical bugs that have outsized impact relative to fix size. None are ambiguous and all have small blast radius:

- C1, C2 — fix musical-context frame leak and statement-skip-after-error in `Interpreter.cs`.
- C3, C4 — guard the envelope / fade divisions (`Math.Max(1, frames)`).
```

**Adaptations for Phase 42** (per RESEARCH.md):
- Replace the 5 tiers (Critical / Major / Minor / Test Gaps / Features) with the 7 gap classes (Orphaned types / Missing conversions / Asymmetric pairs / Dead-end builtins / Overload gaps / Clamp+advisory inventory / Prioritization).
- Per Pitfall 7 (line 293), cite **builtin name + signature** as the stable identifier — NOT file:line. File:line goes in `42-AUDIT-data/`.
- Each finding maps to a downstream phase (43 / 44 / v1.6 backlog) per AUDIT-08.
- Add `## Limitations` section per Open Question 1 (`FunctionSignature` lacks `ReturnType`, so producer half of graph is inferred not enumerated).

---

### `flow-lang/Tools/StdlibAuditor/Program.cs` (tool, console — Approach A)

**Analog:** `scripts/Migrate26/Program.cs`

**Why:** Migrate26 is the only existing standalone .NET console tool that takes a `<ProjectReference>` to `flow-lang.csproj` and operates on its public types. Same shape: `internal static class Program` with `Main(string[] args)` returning `int`, file-scoped namespace, `Console.Error.WriteLine` for usage banner, summary line at end.

**Imports + namespace pattern** (lines 1-5):
```csharp
using System.Text;
using FlowLang.Lexing;
using FlowLang.Diagnostics;

namespace FlowLang.Migrate26;
```

**Entry-point pattern** (lines 25-36 — usage banner + arg expansion + per-file try/catch):
```csharp
internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: dotnet run --project scripts/Migrate26 -- <file-or-dir> [...]");
            Console.Error.WriteLine("Migrates infix arithmetic in .flow files to prefix form.");
            return 1;
        }

        var files = ExpandPaths(args).ToList();
        int touched = 0, skipped = 0;
```

**Per-target loop + final summary pattern** (lines 38-66):
```csharp
foreach (var file in files)
{
    string before = File.ReadAllText(file);
    string after;
    try { after = Migrate(before); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"SKIP (lex error): {file}: {ex.Message}");
        skipped++;
        continue;
    }
    // ...
}
Console.WriteLine($"Done. {touched} migrated, {skipped} unchanged.");
return 0;
```

**Adaptations for Phase 42** (per RESEARCH.md §Code Examples lines 302-357):
- Body becomes: `var registry = new InternalFunctionRegistry(); BuiltInFunctions.RegisterSignaturesOnly(registry);` then reflection over `typeof(FlowType).Assembly.GetTypes()` + `EnumerateSignatures()` iteration.
- Two output flags: `--emit-json <path>` (raw graph, machine-readable) + `--emit-markdown <path>` (the AUDIT.md draft).
- Summary line: `"Done. {n_types} types, {n_signatures} signatures, {n_orphans} orphans, {n_asymmetric} asymmetric pairs."`
- Pitfall 1 from RESEARCH (`BuiltInFunctions.RegisterAllImplementations` does NOT wire Sfz/NotationIO/Osc) — MUST use `RegisterSignaturesOnly` instead.

---

### `flow-lang/Tools/StdlibAuditor/StdlibAuditor.csproj` (config — Approach A)

**Analog:** `scripts/Migrate26/Migrate26.csproj`

**Why:** Exact-shape match — a standalone Exe targeting net10.0 with a single ProjectReference to flow-lang.csproj. Copy verbatim with two renames.

**Complete file** (12 lines — entire content):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>FlowLang.Migrate26</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\flow-lang\flow-lang.csproj" />
  </ItemGroup>
</Project>
```

**Adaptations for Phase 42:**
- `<RootNamespace>FlowLang.Migrate26</RootNamespace>` → `<RootNamespace>FlowLang.StdlibAuditor</RootNamespace>`
- ProjectReference path stays `..\..\flow-lang\flow-lang.csproj` (Tools/StdlibAuditor → flow-lang both at repo-root level offset by 2 dirs, matches Migrate26's offset).
- Register in `flow-sharp.sln` (Migrate26 precedent: was added to the .sln).

---

### `flow-lang.Tests/Integration/Phase42/StdlibAuditTests.cs` (test, reflective audit — Approach B and/or harness self-check under A)

**Analog (primary):** `flow-lang.Tests/Phase36/PrngRegistryNewRandomGateTests.cs`
**Analog (secondary, for `FindTestsRoot` precedent):** `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs`

**Why:** PrngRegistryNewRandomGateTests is the cleanest existing example of a "scan source tree, assert a property holds, fail with file:line offender list" reflective audit — and its class doc explicitly says it mirrors `LicenseAuditTests`' source-grep pattern (line 27). LicenseAuditTests is the older `[Theory]+[InlineData]`-per-instrument shape, which fits each Phase 42 gap class becoming its own `[InlineData]` row.

**Class header + xmldoc pattern** (PrngRegistryNewRandomGateTests lines 1-29):
```csharp
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-01 Task 2 — source-grep CI gate.
///
/// Scans <c>flow-lang/StandardLibrary/Patterns/</c>, ...
/// Asserts zero hits EXCEPT on lines bearing the trailing comment marker
/// <c>// PRNG-SANCTIONED:</c> ...
///
/// Mirrors Phase 29 LicenseAuditTests' source-grep pattern.
/// </summary>
public class PrngRegistryNewRandomGateTests
{
```

**[Theory]+[InlineData]-per-scope iteration pattern** (PrngRegistryNewRandomGateTests lines 31-46):
```csharp
[Theory]
[InlineData("Patterns")]
[InlineData("Generative")]
[InlineData("Improv")]
public void NoNewRandomUnderGenerativeDirectories(string subDir)
{
    string repoRoot = FindRepoRoot();
    string targetDir = Path.Combine(repoRoot, "flow-lang", "StandardLibrary", subDir);

    if (!Directory.Exists(targetDir))
    {
        // Directory does not exist yet — gate is vacuously satisfied.
        return;
    }
```

**Per-file scan + offender accumulation + assertion pattern** (lines 48-77):
```csharp
int hits = 0;
var offenders = new System.Collections.Generic.List<string>();
foreach (var file in Directory.GetFiles(targetDir, "*.cs", SearchOption.AllDirectories))
{
    string[] lines = File.ReadAllLines(file);
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i];
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            continue;
        if (line.Contains("new Random(", StringComparison.Ordinal))
        {
            if (line.Contains("PRNG-SANCTIONED:", StringComparison.Ordinal))
                continue;
            hits++;
            offenders.Add($"{file}:{i + 1}: {line.Trim()}");
        }
    }
}

Assert.True(hits == 0,
    $"Found {hits} `new Random(` occurrence(s) in {subDir}/ — ...\n  " +
    string.Join("\n  ", offenders));
```

**Repo-root walker pattern** (PrngRegistryNewRandomGateTests lines 84-92):
```csharp
private static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
        dir = dir.Parent;
    if (dir == null)
        throw new InvalidOperationException("Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
    return dir.FullName;
}
```

**LicenseAuditTests `FindTestsRoot` alternative** (lines 24-26 — when tests need fixture path, not repo root):
```csharp
string testsRoot = FlowScriptData.FindTestsRoot();
string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
string licensePath = Path.Combine(repoRoot, "flow-lang", "Samples", instrument, "LICENSE.md");
```

**Adaptations for Phase 42:**
- Per AUDIT-04 / AUDIT-05 / AUDIT-07: one `[Fact]` per gap class (or `[Theory]+[InlineData]` over class names). Each materializes the same `registry + EnumerateSignatures()` graph (extract once via static field for the file) then asserts class-specific properties.
- `[Fact] public void OrphanList_ContainsBeat()` — anchor regression for the RESEARCH §Summary high-signal finding (BeatType empirically orphaned).
- `[Fact] public void AsymmetricPairs_ContainsWriteMidiWithoutReadMidi()` — anchor for AUDIT-04 expected finding.
- `[Theory]+[InlineData("Patterns")]` style for the clamp-grep gate (AUDIT-07): one row per stdlib subdir, count `Math.Clamp` sites that match the input-perimeter heuristic.
- Vacuously-passes-if-dir-missing pattern (line 40-46) — adapt for Phase 42 to "vacuously passes if registry has zero signatures" (defensive against test-harness construction failure).
- Namespace: `FlowLang.Tests.Integration.Phase42` (follows LicenseAuditTests `Integration/Phase29` convention).

---

### `scripts/audit/clamp-grep.sh` (tool, shell — optional for AUDIT-07)

**Analog:** `scripts/test_two_run_determinism.sh`

**Why:** The only existing Bash script under `scripts/` that emits a structured output as a downstream artifact (vs `install.sh` / `publish.sh` which are operator-facing one-shots). Same shebang + `set -euo pipefail` + usage banner + multi-flag CLI shape.

**Header pattern** (lines 1-22):
```bash
#!/usr/bin/env bash
# =============================================================================
# Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — two-run determinism harness.
#
# Usage:
#   scripts/test_two_run_determinism.sh path/to/script.flow [--render-cmd CMD]
#
# Renders the given .flow file twice via the `flow` CLI, captures both WAV
# outputs, and compares their SHA-256s. Exits 0 iff byte-identical; 1 otherwise.
# ...
# =============================================================================

set -euo pipefail

usage() {
    cat <<EOF
Usage: $0 <script.flow> [--render-cmd "<cmd>"]
...
EOF
}
```

**Adaptations for Phase 42:**
- Per RESEARCH.md §Code Examples lines 360-385 (Approach C), the body becomes a small fan-out of `grep -rn` invocations writing to `42-AUDIT-data/{input-clamps,all-clamps,advisory-sites,charitable-sites}.txt`, plus `wc -l` summary at the end.
- Exit 0 always (an audit grep producing zero hits is data, not failure).

---

## Shared Patterns

### Reflective `EnumerateSignatures` Iteration

**Source:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs:128-140` (the public API itself)
**Apply to:** Both Approach A (`Program.cs`) and Approach B (`StdlibAuditTests.cs`) — every audit gap class starts from the same enumeration.

```csharp
/// <summary>
/// Read-only enumerator over registered (name, signatures) pairs.
/// Added for Phase 17 LSP BuiltInIndex (17-05). Does NOT expose the implementation
/// delegates — only signatures are needed for completion/hover/signature-help.
/// </summary>
public IEnumerable<KeyValuePair<string, IReadOnlyList<FunctionSignature>>> EnumerateSignatures()
{
    foreach (var kvp in _implementations)
    {
        var sigs = kvp.Value.Select(tuple => tuple.Signature).ToList();
        yield return new KeyValuePair<string, IReadOnlyList<FunctionSignature>>(kvp.Key, sigs);
    }
}
```

**5-line usage idiom** (synthesized from RESEARCH.md §Pattern 2 + the actual API shape above):
```csharp
var registry = new InternalFunctionRegistry();
BuiltInFunctions.RegisterSignaturesOnly(registry);   // NOT RegisterAllImplementations — see Pitfall in RESEARCH.md §Pattern 2
foreach (var (name, sigs) in registry.EnumerateSignatures())
    foreach (var sig in sigs)
        Console.WriteLine($"{name}({string.Join(", ", sig.InputTypes)})");
```

### Registry Wiring (Critical — easy to mis-wire)

**Source:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:90-124` (`RegisterSignaturesOnly`)
**Apply to:** Both harness paths. Per RESEARCH.md §Pattern 2 Pitfall (line 193): `RegisterAllImplementations` does NOT wire `SfzBuiltins` / `NotationIoBuiltins` / `Network.OscFunctions` — those are wired directly in `FlowEngine.cs`. The audit MUST use `RegisterSignaturesOnly` which proxies through every Register* method including the audio-manager-bound + context-bound ones.

```csharp
public static void RegisterSignaturesOnly(InternalFunctionRegistry registry)
{
    Func<IReadOnlyList<Value>, Value> stub = args =>
        throw new NotSupportedException(
            "signatures-only — the LSP does not execute built-ins. " +
            "Use RegisterAllImplementations(registry[, audioManager]) in flow-interpreter.");

    var proxy = new StubbingRegistryProxy(registry, stub);

    // Audio-manager-free paths (RegisterAllImplementations no-arg).
    RegisterAllImplementations(proxy);

    // Manager-bound paths.
    var dummyAudio = new AudioPlaybackManager();
    RegisterAudio(proxy, dummyAudio);
    Audio.PlaybackFunctions.Register(proxy, dummyAudio);

    // Context-dependent paths.
    var dummyReporter = new FlowLang.Diagnostics.ErrorReporter();
    var dummyContext = new FlowLang.Runtime.ExecutionContext(dummyReporter, proxy);
    RegisterContextDependentFunctions(proxy, dummyContext);
    RegisterIterationGuard(proxy, dummyContext);
}
```

### FlowType Surface (29 concrete subclasses confirmed)

**Source:** `flow-lang/TypeSystem/FlowType.cs` (base) + `PrimitiveTypes/*.cs` (16 files) + `SpecialTypes/*.cs` (13 files — including Phase 36 MarkovModelType + LsystemModelType and Phase 38 OscHandleType).

```csharp
// FlowType.cs:6-44 — base class methods the audit invokes per pair:
public abstract class FlowType : IEquatable<FlowType>
{
    public abstract string Name { get; }
    public virtual bool IsCompatibleWith(FlowType target) => Equals(target);
    public virtual bool CanConvertTo(FlowType target) => IsCompatibleWith(target);
    public virtual int GetSpecificity() => 100;
    public virtual bool IsHashable() => false;
    // ...
}
```

**Reflective discovery pattern** (RESEARCH.md §Pattern 1 lines 159-176 — synthesis, no exact in-tree analog):
```csharp
var flowTypeAssembly = typeof(FlowType).Assembly;
var allTypes = flowTypeAssembly.GetTypes()
    .Where(t => typeof(FlowType).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
    .Select(t =>
    {
        var instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        return (Type: t, Instance: (FlowType?)instanceProp?.GetValue(null));
    })
    .Where(x => x.Instance is not null)
    .ToList();
```

### Test-Fixture Auxiliary Tool Shape (if Phase 42 generates `42-AUDIT-data/` JSON via xUnit)

**Source:** `flow-lang.Tests/Tools/Phase29BaselineRecorder.cs:36-130` (the only existing "tool-shaped Fact that emits a fixture artifact into the source tree" precedent)
**Apply to:** Optional — if Phase 42 emits its JSON graph via `[Fact] [Trait("Category", "Phase42Audit")]` rather than a standalone console program.

**Trait-tagged Fact + source-tree path resolver pattern** (lines 49-156):
```csharp
[Fact]
[Trait("Category", "Phase29Baseline")]
public void Compute_AndWriteJsonFixture()
{
    // ... compute values ...
    string fixtureDir = ResolveSourceTreeFixturesDir();
    Directory.CreateDirectory(fixtureDir);
    string fixturePath = Path.Combine(fixtureDir, "phase28_harmonic_richness_baseline.json");

    if (File.Exists(fixturePath))
    {
        Console.WriteLine($"[SKIP] Phase 28 baseline already pinned at: {fixturePath}");
        return;
    }
    // ... write JSON ...
}

private static string ResolveSourceTreeFixturesDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        string candidate = Path.Combine(dir.FullName, "flow-lang.Tests", "Fixtures", "Phase29", ...);
        // ...
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(...);
}
```

**Adaptations for Phase 42:**
- `[Trait("Category", "Phase42Audit")]` — explicit invocation only, doesn't pollute CI: `dotnet test --filter "Category=Phase42Audit"`.
- Target dir resolves to `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/` instead of `flow-lang.Tests/Fixtures/`.
- Same "skip if file exists" guard idiom — the audit-data JSON is regenerable but committed-once for diff visibility.

### Xunit Test Project Setup (no changes needed)

**Source:** `flow-lang.Tests/flow-lang.Tests.csproj`
**Apply to:** Approach B sits inside the existing `flow-lang.Tests` project — no new csproj. Existing references (`xunit.v3 3.2.2`, `Microsoft.NET.Test.Sdk 17.13.0`, ProjectReference to flow-lang.csproj) already cover every audit need.

---

## No Analog Found

None. Every Phase 42 new-file slot has a strong in-tree analog. This is expected — the phase ships docs + a thin reflective harness; both are well-precedented in the codebase.

## Metadata

**Analog search scope:**
- `flow-lang.Tests/` (all subdirs + Phase29/Phase33/Phase36/Phase38 integration dirs + Tools/)
- `scripts/` (Migrate26, all `.sh` files)
- `flow-lang/Tools/` (does not exist yet — confirmed)
- `.planning/` (CODEBASE-AUDIT, MILESTONE-AUDIT, all phase RESEARCH/SUMMARY templates)
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` + `BuiltInFunctions.cs:1-160` (the public-API surface the harness consumes)
- `flow-lang/TypeSystem/FlowType.cs` + `FunctionSignature.cs` (the reflected types)
- `flow-lang/Diagnostics/RenderingDiagnostics.cs` (the WarnOnce grep target for AUDIT-07)

**Files read in full or relevant section:** 14
**Pattern extraction date:** 2026-05-24
**Cross-check:** Approach A (console program) and Approach B (xUnit reflective test) BOTH have exact-shape analogs; the planner can pick freely per RESEARCH §Open Question 3 (recommendation: Approach A for v1).
