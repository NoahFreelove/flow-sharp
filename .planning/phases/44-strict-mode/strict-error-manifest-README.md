# strict-error-manifest.csv — schema + regeneration policy

**Owner:** Phase 44 Plan 44-00 (Wave 0 / W1 test-infrastructure deliverable)
**Consumers:** Plans 44-05 / 44-06 / 44-07 / 44-08 xUnit `[Theory]` data sources
**Authoritative count:** in-scope rows ≈ 110 (13 §6a + ~97 §6b) + 5 carve-outs ≈ 115 total

This file documents the CSV manifest at
`.planning/phases/44-strict-mode/strict-error-manifest.csv` — the single
hand-curated source of truth for every Phase 44 strict-mode site. The Phase 44
plan stack reconciles two discrepant counts upstream:

- AUDIT §6b cites 117 advisory sites grouped by 19 stdlib modules.
- RESEARCH §"Site Inventory" §6b grep counts ~120 sites (Phase 43's stdlib
  additions added ~3 advisories after the AUDIT was authored).
- After excluding 15 doc-only XML `<see cref="WarnOnce"/>` references and 5
  carve-outs (D-06), the live in-scope §6b count is **~97**.

The CSV is the single artifact that resolves the discrepancy. Plans
44-05..44-08 consume rows from it via
`StrictErrorManifestLoader.LoadInScopeSites()` / `LoadHighPrioritySites()` /
`LoadMedLowPrioritySites()` / `LoadCarveOutSites()` to drive xUnit `[Theory]`
data sources without ever re-counting the upstream surface.

---

## Schema (10 columns)

| Column          | Type   | Required | Example                                                                                           |
| --------------- | ------ | -------- | ------------------------------------------------------------------------------------------------- |
| `file_path`     | string | yes      | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`                                      |
| `line`          | int    | yes      | `649`                                                                                             |
| `builtin`       | string | yes      | `crescendo`                                                                                       |
| `tag`           | string | yes      | `crescendo`                                                                                       |
| `sentinel_body` | string | yes      | `[strict] crescendo startVel {value} outside [0.0, 1.0]`                                          |
| `priority`      | string | yes      | `HIGH` / `MED` / `LOW`                                                                            |
| `carve_out`     | bool   | yes      | `false` (in-scope) / `true` (5 sites stay charitable)                                             |
| `axis`          | string | yes      | `B` (Phase 44 Axis B input-perimeter)                                                             |
| `param`         | string | optional | `startVel` (only meaningful for §6a clamp rows)                                                   |
| `range`         | string | optional | `[0.0, 1.0]` (only meaningful for §6a clamp rows)                                                 |

**CSV quoting:** fields containing commas, double quotes, or newlines are
enclosed in double quotes per RFC 4180. The double-quote character inside a
quoted field is escaped by doubling (`""`). Currently every Phase 44
`sentinel_body` contains commas (e.g. `outside [0.0, 1.0]`) so the column
SHOULD always be quoted in this CSV to keep `StrictErrorManifestLoader` simple
and the header layout stable.

**Header line** (first row, MUST match exactly):

```csv
file_path,line,builtin,tag,sentinel_body,priority,carve_out,axis,param,range
```

---

## Carve-out policy (D-06 + RESEARCH Pitfall 2)

Five sites are listed in the manifest with `carve_out=true` and MUST stay
charitable in BOTH strict and non-strict modes. The xUnit loader's
`LoadInScopeSites()` filters these out; `LoadCarveOutSites()` yields only
these. Promoting any of these to a strict error contradicts a locked design
decision (D-06 + D-v1.5-07):

| Carve-out site                                                  | Tag       | Why charitable                                                                                                        |
| --------------------------------------------------------------- | --------- | --------------------------------------------------------------------------------------------------------------------- |
| `flow-lang/Interpreter/Interpreter.cs:476`                      | `[live]`  | D-v1.5-07 design-lock — live coding must never die mid-set. The advisory IS the determinism opt-out announcement.     |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:156`         | `[improv]`| Style-pack discovery is environmental, not composer-surface (AUDIT §7b LOW).                                          |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:244`         | `[improv]`| Same as above.                                                                                                        |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:258`         | `[improv]`| Same as above.                                                                                                        |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs:265`         | `[improv]`| Same as above.                                                                                                        |

---

## Priority routing (per AUDIT §7b)

Plans 44-06 (HIGH) and 44-07 (MED/LOW) split the in-scope rows by `priority`
column. Routing is fixed by AUDIT §7b's prioritization table and is NOT
something xUnit Theory rows should derive at run time — the column value IS
the routing.

| Priority | Module clusters                                                                                                                              |
| -------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| HIGH     | 13 §6a input-perimeter clamps + SFZ (22) + Patterns (17) + DSP (4) + Render (2) + Match (1) ≈ 59 rows                                          |
| MED      | Jam (9) + Markov (6) + Chaos (9) + Lsystem (6) + Cellular (3) + ABC (8 + 2 lexer) + MML (5) + OSC (3) + Scala (2) + Harmony (1) ≈ 54 rows      |
| LOW      | Piano sample (3) + AudioIn (3) + Midi (1) ≈ 7 rows                                                                                            |

(Counts are approximate; the curated CSV is authoritative.)

---

## Regeneration policy

The CSV is **hand-curated**. To refresh the upstream raw counts when stdlib
WarnOnce sites change:

1. Run `scripts/audit/strict-site-grep.sh` to regenerate
   `.planning/phases/44-strict-mode/strict-site-raw.txt` (NOT committed; gitignored
   via `.planning/phases/44-strict-mode/.gitignore`).
2. Diff the raw output against the CSV's `file_path,line` projection.
3. Add new rows or update existing line numbers BY HAND, preserving the
   `sentinel_body` verbatim from AUDIT §6a Column 5 + AUDIT §6b representative
   sentinels with the `[strict] ` prefix per D-07.
4. Re-run the Wave 0 sanity Facts in `StrictErrorManifestSanityTests.cs` to
   confirm the partition counts + header schema + carve-out cardinality stay
   green.

**Why hand-curated:** the `sentinel_body` strings are LOAD-BEARING composer-
visible error wording (per D-07 + AUDIT §6a Column 5 + composer Phase 42
closeout approval 2026-05-24). They cannot be derived from grep — only the
file:line + tag can. A re-runnable extractor would risk overwriting carefully-
worded sentinels with incorrect interpolations.

---

## Relevant decisions

- **D-06** (44-CONTEXT.md) — 5 carve-out sites enumerated above STAY charitable.
- **D-07** (44-CONTEXT.md) — Error format `[strict] <existing-tag> <issue>`;
  the existing WarnOnce sentinel body is kept verbatim and a `[strict] ` prefix
  prepended.
- **D-14** (44-CONTEXT.md) — xUnit Theory consumption via the loader; positive
  `.flow` smoke tests live in `tests/strict/`.
- **D-v1.5-07** (external memory) — `[live]` entry advisory design-locked
  charitable.
- **REQ-STRICT-08** (REQUIREMENTS.md) — all in-scope §6b advisories emit
  `[strict] ...` error when `ctx.CallerStrictMode == true`.

---

## See also

- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §6a (13 verbatim
  clamp error messages — Column 5 LOAD-BEARING per D-07).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §6b (advisory
  sites grouped by module + representative sentinels).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/input-clamps.txt`
  (raw file:line refs for §6a — 13 entries).
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/advisory-sites.txt`
  (raw file:line refs for §6b — 117 entries; doc-only XML refs subtracted by
  the curated CSV).
- `.planning/phases/44-strict-mode/44-RESEARCH.md` §"Site Inventory" (Phase 44
  count reconciliation).
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestLoader.cs` (the C#
  loader consumed by Plans 44-05..44-08 Theory rows).
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestSanityTests.cs` (the
  Wave 0 sanity Facts pinning header + partition + carve-out cardinality).
