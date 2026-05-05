---
phase: 26
slug: op-standardization-prefix-only
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-04
---

# Phase 26 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `26-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + custom `FlowEngineRunner` test fixture |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Phase26"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds (full suite, post-migration) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build && dotnet test --filter "FullyQualifiedName~Phase26"` (once Phase 26 fact files exist)
- **After every plan wave:** Run `dotnet test` (full suite ≥287 Facts post-Phase 25)
- **Phase gate (commit 2 — migration):** SHA256 hash diff empty for `examples/output/flow_tutorial.{wav,mid}` and `examples/output/flow_showcase.{wav,mid}` pre vs post migration
- **Phase gate (commit 3 — final):** Full suite green; `grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/` returns empty
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 26-XX-01 | TBD | 0 | STD-02 | — | N/A (internal refactor) | unit | `dotnet test --filter "Phase26.NewOverloadFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-02 | TBD | 0 | STD-02 | — | N/A | unit | `dotnet test --filter "Phase26.NegOverloadFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-03 | TBD | 0 | STD-02 | — | N/A | unit | `dotnet test --filter "Phase26.IntegerDivisionFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-04 | TBD | 0 | STD-02 | — | N/A | unit | `dotnet test --filter "Phase26.MixedTypeArithmeticFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-05 | TBD | 0 | STD-02 | — | N/A | unit | `dotnet test --filter "Phase26.NegativeLiteralLexFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-06 | TBD | 0 | STD-02 | — | N/A | unit | `dotnet test --filter "Phase26.UnaryMinusShorthandFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-07 | TBD | 0 | STD-01 | — | N/A | unit | `dotnet test --filter "Phase26.InfixRejectedFacts"` | ❌ W0 | ⬜ pending |
| 26-XX-08 | TBD | 1+ | STD-01 | — | N/A | static | `! grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/` | ✅ shell | ⬜ pending |
| 26-XX-09 | TBD | 2 | STD-03 | — | N/A | smoke | `for f in tests/*.flow; do dotnet run --project flow-interpreter "$f" \|\| echo "FAIL: $f"; done` | ✅ shell | ⬜ pending |
| 26-XX-10 | TBD | 2 | STD-03 | — | N/A | integration | `dotnet test --filter "Phase18.ByteIdentical\|Phase23.ByteIdentical\|Phase25.ByteIdenticalShowcaseGaussian"` | ✅ existing | ⬜ pending |
| 26-XX-11 | TBD | 2 | STD-03 | — | N/A | one-shot | sha256 gate during commit 2 (manual procedure documented in plan) | ✅ shell | ⬜ pending |
| 26-XX-12 | TBD | 2 | STD-03 | — | N/A | build | `dotnet build` | ✅ existing | ⬜ pending |

*Plan IDs and exact task IDs filled in by planner. Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` — covers STD-02 same-type fast paths (Long/Number for add/sub/mul/div)
- [ ] `flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs` — covers STD-02 `(neg)` 5-pack
- [ ] `flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs` — covers STD-02 `(idiv)` + `(div Int Int)` Double promotion
- [ ] `flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs` — covers STD-02 OverloadResolver convertible-scoring path (depends on EvaluateFunctionCall coercion fix)
- [ ] `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` — covers STD-02 lexer 6-position expression-start matrix
- [ ] `flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs` — covers D-01 parser shorthand `-x → (neg x)`
- [ ] `flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs` — covers STD-01 (bare infix `1 + 2` produces parse error)
- [ ] `scripts/Migrate26/Migrate26.csproj` — migration tool entry point (per RESEARCH.md recommendation: standalone csproj, not `.csx`, since `dotnet-script` is not installed)
- [ ] `scripts/Migrate26/Program.cs` — token walker + precedence climber

*Existing test infrastructure (Phase 18, 23, 25 byte-identical Facts) covers STD-03 byte-identical regression. No NEW byte-identical Facts needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| SHA256 byte-identical hash gate during migration commit | STD-03 | One-shot pre/post-migration check; not part of CI loop | 1. Pre-migration: `sha256sum examples/output/flow_tutorial.{wav,mid} examples/output/flow_showcase.{wav,mid} > /tmp/26-hashes-pre.txt`. 2. Run migration script + commit. 3. Rebuild outputs (re-run tutorial.flow + showcase.flow). 4. Post-migration: `sha256sum ... > /tmp/26-hashes-post.txt`. 5. `diff /tmp/26-hashes-pre.txt /tmp/26-hashes-post.txt` MUST be empty. If not: bisect commits and abort. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (7 fact files + 1 migration csproj + 1 program file)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter (set after planner finalizes task IDs)

**Approval:** pending
