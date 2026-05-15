# Phase 31 Plan-Phase Decisions

**Locked at plan-phase per CONTEXT.md punt list.** These decisions extend the
discuss-phase D-01..D-10 (see `31-CONTEXT.md`) with the two additional locks the
plan-phase Q&A surfaced. Downstream plans (02 for SimpleLexer, 04 for completion
filters, 05 for varargs render, 08 for JetBrains factory) cite these by ID.

---

**D-11 [semicolon-comment-position]** — `;` as a Lisp-style line comment is
**position-sensitive (Option A)** per RESEARCH §Critical Decision. Concretely:

- A `;` character at column-0 (with optional leading whitespace, gated by the
  existing `IsStartOfLineContent()` helper at `flow-lang/Lexing/SimpleLexer.cs:1159`)
  is consumed as a line comment to end-of-line by `SkipWhitespaceAndComments`.
- A `;` mid-line (any non-whitespace before it on the same logical line)
  remains `TokenType.Semicolon` and continues to terminate statements at the 14
  `Parser.cs` call sites (lines 52, 63, 293, 304, 481, 490, 670, 679, 704, 708,
  727, 731, 1115, 1119, 1334).

This mirrors the existing `Note:` arm at `SimpleLexer.cs:1144` verbatim — same
gate, same lookahead, same "consume until newline" body.

**Migration impact:** RESEARCH §Migration Audit grep across all 647 in-repo
`.flow` files (under `examples/`, `tests/`, and `flow-lang/`) confirms **zero**
column-0 `;` exist. REQ-6 migration count is therefore zero. Every shipped pragma
(`enable hAsB;`), every typed declaration (`Int x = 5;`), and every flow chain
(`5 -> doubler;`) preserves its current lex behavior — no token-stream change
for any valid existing program. By construction, this preserves the Phase
18/25/27/28 two-run byte-identical determinism contract.

**Rejected alternatives** (from RESEARCH §Critical Decision):
- Option B (`;;` double-semicolon): contradicts the SPEC's literal "`;` Lisp-style
  line comment" wording.
- Option C (remove `;` as a token entirely): high blast radius — touches
  Parser's 14 `Match(TokenType.Semicolon)` call sites and 17 `.flow` test files;
  risks breaking byte-identical determinism gates.

---

**D-12 [varargs-ellipsis-character]** — Re-confirm Unicode horizontal ellipsis
`…` (U+2026) for variadic parameter rendering, **superseding** the
planning-orchestrator note that proposed ASCII `...`. CONTEXT D-01 is
authoritative and locked Unicode at the discuss-phase; the plan-phase explicit
re-confirmation removes any "did the planner notice?" ambiguity for downstream
plans 05 (LspMappings.FormatSignature) and 03 (HoverHandler / SignatureHelp
verb swap).

Rendered format follows CONTEXT D-02: `name: Type…` — the ellipsis trails the
parameter type, not the parameter name. Example: `concat(String…)` for the
varargs `concat` built-in.

**Pitfall 3 mitigation** (RESEARCH lines 519-527 / Pitfall 3): U+2026 is 3
bytes in UTF-8 / 1 grapheme. LSP clients (VSCode, IntelliJ via LSP4IJ) compute
`ActiveParameter` offsets in UTF-16 code units. Both clients are consistent
there, but the safer path is to populate
`SignatureInformation.Parameters` with explicit `ParameterInformation` ranges
(via the new `LspMappings.BuildParameters` helper) instead of relying on
byte-offset math inside the merged label string. Plan 31-05 ships a
`VarargsRenderingFacts` unit test that pins the active-parameter highlight when
the cursor moves past the varargs ellipsis position.

**Where the U+2026 appears in code:**
- `flow-lsp/LspMappings.cs` — `FormatSignature(FunctionSignature)` emits
  `$"{t}…"` for the varargs tail position.
- `flow-lsp/LspMappings.cs` — `BuildParameters(FunctionSignature)` emits the
  same string inside each `ParameterInformation.Label`.
- `flow-lang/TypeSystem/FunctionSignature.cs` — `ToString()` continues to emit
  ASCII `"..."` for runtime / non-LSP consumers (Phase 24 D-04 "zero flow-lang
  touch for LSP-only work" policy preserved).

---

## Stretch-bar criterion (clarification — not a new decision)

This is a restatement of REQ-7's acceptance contract from CONTEXT D-10 and the
31-SPEC.md stretch-bar definition. No new decision is being added; this section
exists so downstream plans (08 JetBrains scaffolding) can cite a single
authoritative wording.

Per CONTEXT D-10 the `flow-jetbrains/` scaffolding (Gradle build files,
`plugin.xml`, LSP4IJ wiring under `dev.flowlang.jetbrains`) **ALWAYS lands at
phase closure**, regardless of stretch outcome. The plan-phase commitment is:

- **Stretch IS-MET** iff the plugin `.zip` builds via `./gradlew buildPlugin`
  AND a manual UAT in IntelliJ Community 2024.2+ shows completions on
  `examples/tutorial.flow`. In that case the phase-closure SUMMARY records
  "stretch met (plugin .zip attached to v1.4 tag)".
- **Stretch IS-DEFERRED** if either gate above fails. The scaffolding still
  lands — the closure SUMMARY records "stretch deferred to v1.5 (scaffolding
  ready)" with the specific gate that failed. Never delete the scaffolding —
  v1.5 picks it up immediately.

This restatement closes a wording-ambiguity flagged in the plan-phase Q&A about
whether "deferred" means "files removed" or "files kept, gate unmet." The
answer is the latter, per CONTEXT D-10.

---

**Plan-phase planner:** Claude Opus 4.7 (1M context). Locked 2026-05-12.
