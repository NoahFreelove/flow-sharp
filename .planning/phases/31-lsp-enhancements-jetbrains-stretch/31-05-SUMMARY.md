---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 05
subsystem: lsp
tags: [lsp, hover, signature-help, varargs, unicode, rendering, spec-3]

# Dependency graph
requires:
  - phase: 17
    provides: HoverHandler.BuildHover + SignatureHelpHandler.Handle pure-static seam; BuiltInIndex.Find returning IReadOnlyList<FunctionSignature>; RegisterSignaturesOnly D-07 full coverage
  - phase: 24
    provides: D-04 "zero flow-lang touch for LSP-only work" — Phase 31 honors by keeping FunctionSignature.ToString() emitting ASCII `"..."` for runtime use, while the LSP-side layer renders U+2026
  - phase: 31
    plan: 01
    provides: Plan 31-01 D-12 re-confirmation of Unicode `…` (U+2026) over ASCII three-dots
provides:
  - "LspMappings.FormatSignature(FunctionSignature) — `name(Type…)` form; U+2026 trails the type ONLY on the last param when IsVarArgs; non-varargs render bare. Used by HoverHandler + SignatureHelpHandler (and transitively any future consumer of the signatures array)."
  - "LspMappings.BuildParameters(FunctionSignature) — emits Container<ParameterInformation> with explicit per-parameter labels (`Symbol`, `Int…`, ...). Mitigates Pitfall 3: clients hand-resolve from the explicit array instead of computing byte offsets across the U+2026 grapheme."
  - "HoverHandler.BuildHover now routes built-in signatures through LspMappings.FormatSignature — hover panel shows U+2026 for any varargs builtin (list, dict, dictTuple, etc.)"
  - "SignatureHelpHandler.Handle now routes through FormatSignature + BuildParameters — SignatureInformation.Label carries U+2026 AND SignatureInformation.Parameters is non-empty"
affects: [31-08, 31-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "LSP-side rendering layer for cross-cutting type display — flow-lang stays untouched (Phase 24 D-04). The pure-static helper convention from LspMappings (existing ToRange/ToSeverity) is extended with two more helpers; HoverHandler + SignatureHelpHandler are thin consumers."
    - "Explicit ParameterInformation array as the active-parameter-highlight contract (Pitfall 3). LSP clients (VSCode, JetBrains via LSP4IJ) compute active-parameter ranges in UTF-16 code units across the SignatureInformation.Label string; populating SignatureInformation.Parameters with explicit ParameterInformationLabel strings sidesteps every encoding-width concern."
    - "Byte-level Unicode assertion in tests — `Assert.DoesNotContain(\"...\", rendered)` paired with `Assert.Equal(\"concat(String…)\", rendered)` pins BOTH 'no ASCII three-dots' AND 'exact U+2026 byte sequence' simultaneously. Editor-visible glyph is the contract; runtime ASCII path is preserved per D-04."

key-files:
  created:
    - flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs
  modified:
    - flow-lsp/LspMappings.cs
    - flow-lsp/Handlers/HoverHandler.cs
    - flow-lsp/Handlers/SignatureHelpHandler.cs

key-decisions:
  - "Test uses `list` not `concat` for hover/signature-help integration. The plan's literal text invoked `BuildHover(\"concat\", ...)`, but `concat` is registered as a fixed-arity builtin in BuiltInFunctions.cs (line 212: `[StringType.Instance, StringType.Instance]`; line 552: `[ArrayType, ArrayType]`) — neither has IsVarArgs=true. `list` is a real registered varargs builtin (line 492-496: `[VoidType.Instance]` IsVarArgs=true). Synthetic FunctionSignature(\"concat\", ..., IsVarArgs:true) is still used in the unit-level FormatSignature_VarargsParam_UsesU2026 test to exercise the renderer with the canonical literal name from D-01."
  - "BuildParameters returns Container<ParameterInformation> directly (not List<>). Matches the OmniSharp wire type expected by SignatureInformation.Parameters; avoids a wrapping allocation at every call site. Uses ParameterInformationLabel(typeStr) constructor — verified compile-clean against OmniSharp.Extensions.LanguageProtocol 0.19.9."
  - "Triple-slash docblock cites D-01 + D-02 + D-04 + Pitfall 3 inline. Future agents touching LspMappings (and forensics on a v1.5 ascii-fallback feature, if one ever ships) can re-derive the design without re-reading CONTEXT.md."

patterns-established:
  - "LspMappings as the cross-cutting LSP-side type-display layer. Whenever a Flow type or signature needs editor-visible rendering distinct from its runtime ToString() form, the helper goes in LspMappings — not in flow-lang's TypeSystem. Phase 24 D-04 / Phase 31 SPEC-3 codify this."
  - "Explicit ParameterInformation array as the standard SignatureInformation.Parameters output — even for non-varargs sigs (test BuildParameters_NonVarargs_NoEllipsisAnywhere pins this). Editor clients gain consistent per-parameter granularity regardless of whether the signature uses varargs."

requirements-completed: [SPEC-3]
deferred-items: []
threat-flags: []

# Metrics
metrics:
  duration_seconds: ~2400
  duration_human: "~40 min"
  task_count: 1
  files_created: 1
  files_modified: 3
  commits: 2
  tests_added: 8
  tests_passing_in_scope: 18  # 8 new + 10 Phase 17 hover/sig-help regression
  completed_at: "2026-05-12T23:22:36Z"

# Verification record
verification:
  build:
    - cmd: "dotnet build flow-lsp/flow-lsp.csproj /clp:ErrorsOnly"
      result: "0 errors, 3 warnings (pre-existing)"
  tests:
    - cmd: "dotnet test flow-lang.Tests --filter \"FullyQualifiedName~Phase31.VarargsRendering\""
      result: "Passed: 8, Failed: 0 (duration 200 ms)"
    - cmd: "dotnet test flow-lang.Tests --filter \"FullyQualifiedName~Phase17.HoverHandler|FullyQualifiedName~Phase17.SignatureHelpHandler\""
      result: "Passed: 10, Failed: 0 (Phase 17 regression GREEN — no impact from FormatSignature swap)"
    - cmd: "dotnet test flow-lang.Tests --filter \"FullyQualifiedName~Phase31\""
      result: "Passed: 44, Failed: 0 (full Phase 31 suite — 8 new + 36 prior plans)"
    - cmd: "dotnet test flow-lang.Tests --filter \"FullyQualifiedName~Phase17\""
      result: "Passed: 117, Failed: 0 (full Phase 17 LSP suite stays green)"
    - cmd: "dotnet test flow-lang.Tests --filter \"FullyQualifiedName~ByteIdentical\""
      result: "Passed: 20, Failed: 0 (determinism contract preserved by construction — change is LSP-only)"
  unicode_assertion:
    - cmd: "python3 byte count of E2 80 A6 in flow-lsp/LspMappings.cs"
      result: "4 occurrences (FormatSignature body + BuildParameters body + docblock prose + docblock prose)"
    - cmd: "python3 byte count of E2 80 A6 in flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs"
      result: "13 occurrences (test literal Asserts + Assert.Equal expected strings)"
    - cmd: "FormatSignature body grep for ASCII three-dots"
      result: "Absent — only U+2026 used in the method body"
  acceptance_grep:
    - cmd: "grep -c 'public static string FormatSignature' flow-lsp/LspMappings.cs"
      result: "1 ✓"
    - cmd: "grep -c 'public static Container<ParameterInformation> BuildParameters' flow-lsp/LspMappings.cs"
      result: "1 ✓"
    - cmd: "grep -c 'LspMappings.FormatSignature' flow-lsp/Handlers/HoverHandler.cs"
      result: "1 ✓"
    - cmd: "grep -c 'LspMappings.FormatSignature|LspMappings.BuildParameters' flow-lsp/Handlers/SignatureHelpHandler.cs"
      result: "2 ✓ (one FormatSignature + one BuildParameters reference)"
---

# Phase 31 Plan 05: SPEC-3 Varargs Visibility in Signature Help + Hovers Summary

LSP-side renderer surfaces Flow's variadic function shapes with the Unicode horizontal
ellipsis `…` (U+2026) in hover panels and signature-help tooltips — `(list Void…)`,
`(dict Symbol, Int…)`, etc. — while flow-lang's runtime `FunctionSignature.ToString()`
keeps emitting ASCII `"..."` per Phase 24 D-04's "zero flow-lang touch for LSP-only work."

## What Shipped

### LspMappings (flow-lsp/LspMappings.cs)

Two new static helpers extend the existing pure-translation pattern (alongside
`ToRange` and `ToSeverity`):

```csharp
public static string FormatSignature(FunctionSignature sig)
{
    var inputs = sig.InputTypes.Select((t, i) =>
        sig.IsVarArgs && i == sig.InputTypes.Count - 1
            ? $"{t}…"   // U+2026 horizontal ellipsis — trails the type (D-02)
            : $"{t}");
    return $"{sig.Name}({string.Join(", ", inputs)})";
}

public static Container<ParameterInformation> BuildParameters(FunctionSignature sig)
{
    var list = new List<ParameterInformation>(sig.InputTypes.Count);
    for (int i = 0; i < sig.InputTypes.Count; i++)
    {
        var typeStr = sig.IsVarArgs && i == sig.InputTypes.Count - 1
            ? $"{sig.InputTypes[i]}…"
            : $"{sig.InputTypes[i]}";
        list.Add(new ParameterInformation
        {
            Label = new ParameterInformationLabel(typeStr)
        });
    }
    return new Container<ParameterInformation>(list);
}
```

The U+2026 character in both bodies is the canonical Unicode glyph (UTF-8 bytes
`E2 80 A6`), verified by byte-level inspection. Python check confirms 4
occurrences in `LspMappings.cs` (two in each method body — interpolated string +
docblock prose).

### HoverHandler (flow-lsp/Handlers/HoverHandler.cs)

Single-line swap at the built-in branch (line 55):

```diff
-var signature = b.Signatures.Count > 0 ? b.Signatures[0].ToString() : identifier;
+// Phase 31 SPEC-3 (D-01/D-02): LSP-side renderer emits U+2026 for varargs.
+// FunctionSignature.ToString() still emits ASCII "..." for runtime use
+// (Phase 24 D-04 — zero flow-lang touch for LSP-only work).
+var signature = b.Signatures.Count > 0 ? LspMappings.FormatSignature(b.Signatures[0]) : identifier;
```

Composer hovering `list` in a `.flow` file now sees `list(Void…)` in the
hover panel; hovering `print` (non-varargs) sees `print(Void)` unchanged
(Phase 17 regression GREEN).

### SignatureHelpHandler (flow-lsp/Handlers/SignatureHelpHandler.cs)

`Handle` method routes both Label AND Parameters through the new helpers:

```diff
-var sig = new SignatureInformation
-{
-    Label = b.Signatures.Count > 0 ? b.Signatures[0].ToString() : ctx.FunctionName,
-    Parameters = new Container<ParameterInformation>()
-};
+var sig = new SignatureInformation
+{
+    Label = b.Signatures.Count > 0
+        ? LspMappings.FormatSignature(b.Signatures[0])
+        : ctx.FunctionName,
+    Parameters = b.Signatures.Count > 0
+        ? LspMappings.BuildParameters(b.Signatures[0])
+        : new Container<ParameterInformation>()
+};
```

Triggering signature help inside `(list ` now returns a SignatureInformation
whose Label is `list(Void…)` and whose Parameters container has one entry
labeled `Void…`. The explicit Parameters array means VSCode's
`signatureHelp.activeParameter` highlight uses per-parameter offsets that the
client computes from the explicit ParameterInformationLabel strings —
sidestepping any UTF-8 vs UTF-16 vs grapheme width discrepancy across the
U+2026 codepoint (Pitfall 3 mitigation).

### Test Pinning (flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs)

Eight `[Fact]` tests fixate the rendering rules:

| # | Test | What It Pins |
|---|------|--------------|
| 1 | `FormatSignature_VarargsParam_UsesU2026` | `concat(String, IsVarArgs:true)` → exact string `"concat(String…)"` + Assert.DoesNotContain `"..."` |
| 2 | `FormatSignature_NonVarargs_NoEllipsis` | `add(Int, Int, IsVarArgs:false)` → `"add(Int, Int)"` |
| 3 | `FormatSignature_MultiParam_OnlyLastGetsEllipsis` | `dict(Symbol, Int, IsVarArgs:true)` → `"dict(Symbol, Int…)"` (D-02 — trails LAST type only) |
| 4 | `BuildParameters_VarargsParam_LastLabelHasEllipsis` | Last `ParameterInformationLabel` = `"Int…"`; preceding = `"Symbol"` bare |
| 5 | `BuildParameters_NonVarargs_NoEllipsisAnywhere` | No `…` in any of the per-param labels |
| 6 | `Hover_VarargsBuiltin_RendersEllipsis` | `BuildHover("list", ...)` → `MarkupContent.Value` contains `"…"` AND does NOT contain `"..."` |
| 7 | `Hover_NonVarargsBuiltin_NoEllipsisRegression` | `BuildHover("print", ...)` → `MarkupContent.Value` does NOT contain `"…"` |
| 8 | `SignatureHelp_VarargsBuiltin_LabelHasEllipsis_ParametersNonEmpty` | `FormatSignature(list-sig).Label` contains `"…"`; `BuildParameters(list-sig)` non-empty; last param label contains `"…"` |

All 8 pass on first GREEN run after the LspMappings + handler wiring.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Test target builtin: `list` instead of `concat`**

- **Found during:** Task 1 implementation
- **Issue:** The plan's literal text invoked `HoverHandler.BuildHover("concat", ...)` and `SignatureHelp...` for `concat`, but `concat` is NOT registered as a varargs builtin. Inspection of `BuiltInFunctions.cs`:
  - Line 212: `new FunctionSignature("concat", [StringType.Instance, StringType.Instance])` — fixed arity, 2 strings, NO IsVarArgs.
  - Line 552: `new FunctionSignature("concat", [new ArrayType(VoidType.Instance), new ArrayType(VoidType.Instance)])` — fixed arity, 2 arrays, NO IsVarArgs.
  - There is no varargs overload of `concat` in the stdlib registration paths.
  Had I literally used `concat` for the hover/signature-help integration test, the assertion `Assert.Contains("…", md)` would FAIL because the rendered hover would be `concat(String, String)` — no varargs, no ellipsis.
- **Fix:** Used `list` for tests 6 + 8 (the integration tests that hit `BuiltInIndex`). `list` IS a real registered varargs builtin (`BuiltInFunctions.cs:492-496`: `new FunctionSignature("list", [VoidType.Instance], IsVarArgs: true)`). Test 1 (`FormatSignature_VarargsParam_UsesU2026`) still uses the canonical `concat` literal from D-01 — it constructs a synthetic `FunctionSignature` directly so the registration mismatch is irrelevant. Test 7 (`Hover_NonVarargsBuiltin_NoEllipsisRegression`) uses `print` per the plan.
- **Files modified:** flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs (tests 6, 8 use `list`; test 1 keeps the synthetic `concat`)
- **Commit:** 592a55a (RED commit; pinned the corrected target at test-authoring time, not after a failed GREEN)
- **Justification:** SPEC-3 intent is "varargs render with U+2026 in hover and signature help" — the test target is incidental, what matters is that A varargs builtin renders correctly. `list` satisfies that with a real registration. Plan acceptance criteria's `dotnet test ... ~Phase31.VarargsRendering exits 0 with ≥ 6 tests run` is preserved (8 ≥ 6).

### Architectural Decisions Surfaced

None — the plan was structurally accurate. The only deviation was the test fixture target.

## Self-Check

### Created Files

- [x] `flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs` — FOUND (165 lines, 8 facts)
- [x] `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-05-SUMMARY.md` — this file

### Modified Files

- [x] `flow-lsp/LspMappings.cs` — `FormatSignature` + `BuildParameters` added (commit fb3f611)
- [x] `flow-lsp/Handlers/HoverHandler.cs` — line 55 swapped to `LspMappings.FormatSignature` (commit fb3f611)
- [x] `flow-lsp/Handlers/SignatureHelpHandler.cs` — both Label and Parameters routed through new helpers (commit fb3f611)

### Commits

- [x] `592a55a` — RED: test(31-05) — VerifiedExists in `git log --oneline | grep 592a55a`
- [x] `fb3f611` — GREEN: feat(31-05) — VerifiedExists

### Acceptance Criteria

- [x] `grep -c "public static string FormatSignature" flow-lsp/LspMappings.cs` → 1 ✓
- [x] `grep -c "public static Container<ParameterInformation> BuildParameters" flow-lsp/LspMappings.cs` → 1 ✓
- [x] `flow-lsp/LspMappings.cs` contains U+2026 — Python byte check: 4 occurrences of `E2 80 A6` ✓
- [x] `grep -c "LspMappings.FormatSignature" flow-lsp/Handlers/HoverHandler.cs` → 1 ✓
- [x] `grep -c "LspMappings.FormatSignature" flow-lsp/Handlers/SignatureHelpHandler.cs` → 1 ✓
- [x] `grep -c "LspMappings.BuildParameters" flow-lsp/Handlers/SignatureHelpHandler.cs` → 1 ✓
- [x] `dotnet test ... ~Phase31.VarargsRendering` → 8 passed, 0 failed ✓ (≥ 6 required)
- [x] `dotnet test ... ~Phase17.HoverHandler` → all green (10 hover + sig-help, no regression) ✓
- [x] `dotnet test ... ~Phase17.SignatureHelpHandler` → green ✓

## Self-Check: PASSED
