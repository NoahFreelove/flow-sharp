---
phase: 21-pragma-system-h-alias
plan: 02
subsystem: lexing
tags: [pragma, h-alias, lexer, token, defer-02, defer-03, prag-02]

dependency_graph:
  requires:
    - 21-01   # PragmaScanner / PragmaSet / PragmaRegistry plumbing + ctor wiring
  provides:
    - flow-lang/Lexing/Token.cs::OriginalText            # composer-original pre-canonicalization text (D-15)
    - flow-lang/Lexing/Token.cs::DiagnosticText          # OriginalText ?? Text helper for error UX
    - flow-lang/Lexing/SimpleLexer.cs::TryParseNote(H→B) # H-prefix accepted under enable hAsB; (D-13/D-14)
    - DEFER-02/03 closure                                # H-as-B alias inside note streams
  affects:
    - flow-lang.Tests/Unit/Phase21/HAliasFacts.cs              # 9 Facts pinning DEFER-02/03 acceptance
    - flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs  # tightened from 1 to 2 Facts
    - tests/test_h_alias.flow                                  # NEW acceptance script
    - tests/test_h_identifier.flow                             # NEW Pitfall C regression gate
    - tests/test_pragma_isolation*.flow                        # tightened to actually exercise the pragma

tech-stack:
  added: []
  patterns:
    - "Probe-based lex-time substitution: prepend a canonical letter, run NoteType.Parse on the probe, accept on success / fall through on failure (Pitfall E auto-rejection of Hmaj7)."
    - "Token-level original-text preservation via optional positional record field (additive — every existing 4-arg construction site compiles unchanged)."
    - "DiagnosticText helper as a property over Text + OriginalText so renderer/MIDI consume canonical Text while diagnostics consume composer-authored OriginalText."
    - "Cleanly-split test fixtures: tracked .flow scripts stay GREEN in the integration loop; failing-importer assertions live ONLY in the Fact's inline RunSource so the loop doesn't spuriously FAIL."

key-files:
  created:
    - flow-lang.Tests/Unit/Phase21/HAliasFacts.cs
    - tests/test_h_alias.flow
    - tests/test_h_identifier.flow
  modified:
    - flow-lang/Lexing/Token.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs
    - tests/test_pragma_isolation.flow
    - tests/test_pragma_isolation_module.flow

key-decisions:
  - "Closed DEFER-02 + DEFER-03 in a single 3-task plan: RED scaffolding + Token additive field + lexer wiring."
  - "TryParseNote H→B branch placed BEFORE the standard A-G acceptance because 'H' is outside [A,G] — the only acceptance path for an H-prefix is the new probe attempt."
  - "Both Token-construction sites in ScanIdentifierOrKeyword (direct-note + duration-suffix-stripping) plumb OriginalText so Pitfall D (inner-call canonicalization) is structurally covered."
  - "Integration-loop cleanliness preserved by moving the failing-importer assertion (H4q in importer body) into the Fact's inline RunSource; the tracked tests/test_pragma_isolation.flow keeps a clean PASSED sentinel."

patterns-established:
  - "Token additive field — adding optional positional params to records preserves binary compatibility at every existing N-arg call site (~67 sites in flow-lang, all unchanged)."
  - "Probe-substitution pattern for lex-time letter aliasing — extensible to future single-letter aliases without touching ChordParser or the parser."

requirements-completed:
  - DEFER-02
  - DEFER-03

metrics:
  duration_minutes: 22
  tasks_completed: 3
  files_changed: 8
  lines_added: ~280
  test_count_delta: +10   # +9 HAliasFacts + 1 tightened PragmaIsolationFact gain
  date_completed: 2026-05-01
---

# Phase 21 Plan 02: H-Alias Substitution — Summary

**`enable hAsB;` activates lex-time H→B canonicalization in note streams via a probe-substitution path in `SimpleLexer.TryParseNote`, with `Token.OriginalText` preserving the composer's authored shape for diagnostics — closes DEFER-02/03 and tightens the PRAG-02 isolation Fact.**

## Performance

- **Duration:** ~22 min
- **Tasks:** 3
- **Files changed:** 8 (2 production + 1 unit Fact + 1 integration Fact + 4 .flow fixtures)
- **Test count delta:** +10 (9 HAliasFacts + 1 tightened PragmaIsolation Fact)
- **Completed:** 2026-05-01

## Accomplishments

- DEFER-02/03 acceptance closed: `enable hAsB;` declared at the top of a file makes `H4q` parse identically to `B4q` inside `| ... |`. Full alias coverage: `H4q`, `Hb4q`, `H#4q`, `H4w`, `Hb4+50c`, `H4q.`, `H4h~`, `[H4 D#5 F#5]q` (chord-bracket inner notes).
- `Token.OriginalText` (D-15) ships as an optional positional record field — Token now carries both the canonical B-rooted `Text` (renderer + MIDI consume this) AND the composer-authored `OriginalText` (diagnostics consume `DiagnosticText`).
- `SimpleLexer.TryParseNote` H→B substitution is gated on `_pragmaSet.Has("hAsB")` AND `text.Length > 1` so bare `H` continues to fall through to Identifier (`Int H = 5;` keeps compiling — Pitfall C).
- `Hmaj7` outside `| ... |` stays an Identifier per D-16 — `NoteType.Parse("Bmaj7")` fails (Pitfall E + Assumption A1), so the probe-substitution rejects automatically. ChordParser is untouched.
- PragmaIsolationFacts tightened from 1 baseline Fact to 2: cleanly-loads (kept) + `Importer_WithoutHAsB_RejectsHNote_EvenWhenModuleEnablesIt` (NEW — load-bearing PRAG-02 acceptance via inline RunSource).

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 — HAliasFacts.cs RED + tightened isolation fixtures** — `e25edbd` (test)
2. **Task 2: Token.OriginalText + DiagnosticText helper (D-15)** — `352efac` (feat)
3. **Task 3: TryParseNote H→B substitution + ScanIdentifierOrKeyword OriginalText plumbing (D-13/D-14/D-15/D-16)** — `05c2174` (feat)

## Files Created/Modified

### Created (3)
- `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` — 9 Facts pinning the full DEFER-02/03 acceptance surface (HMatchesB_InNoteStream, WithoutPragma_HRejected, BareH_StaysIdentifier, BareH_StaysIdentifier_EvenWithPragma, FullCoverage_HbHsharpDottedTied, HmajOutsideNoteStream_StaysIdentifier, Token_PreservesOriginalText_WhenHCanonicalized, NoteType_Parse_Bmaj7_Fails, ChordBracketInner_HRecognized).
- `tests/test_h_alias.flow` — acceptance script exercising every alias shape.
- `tests/test_h_identifier.flow` — Pitfall C regression gate proving bare H stays an Identifier when no pragma is declared.

### Modified (5)
- `flow-lang/Lexing/Token.cs` — added optional 5th positional `string? OriginalText = null` parameter + `DiagnosticText => OriginalText ?? Text;` helper; `ToString()` unchanged. Every existing 4-arg `new Token(...)` call site compiles unchanged.
- `flow-lang/Lexing/SimpleLexer.cs` — TryParseNote prepends an H→B branch gated on `_pragmaSet.Has("hAsB")` AND `text.Length > 1`; ScanIdentifierOrKeyword's two NoteLiteral construction sites now pass `noteValue` (canonical) as `Token.Text` and the original `text` as `Token.OriginalText` when canonicalization happened.
- `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` — tightened from 1 baseline Fact to 2: kept the cleanly-loads Fact (now exercising the module's H4q under its pragma), added `Importer_WithoutHAsB_RejectsHNote_EvenWhenModuleEnablesIt` using inline RunSource that places H4q in the importer body and asserts `errorCount > 0`.
- `tests/test_pragma_isolation_module.flow` — body now runs `Sequence seq = | H4q B4q |; (print (str seq))` under its own `enable hAsB;` declaration (proving the pragma is alive in module scope).
- `tests/test_pragma_isolation.flow` — kept clean (no H4q in importer body) so the integration loop doesn't pick it up as FAIL; the failing-importer assertion lives in the Fact's inline RunSource.

## Decisions Made

- **Substitution pattern (D-13).** `TryParseNote` prepends a tight H-prefix branch BEFORE the standard A-G acceptance because `'H'` is outside the `[A,G]` range — without the new branch, the existing branch would always reject H regardless of pragma state. Probe approach: build `"B" + text[1..]`, run `NoteType.Parse` on the probe, accept on success / fall through on failure. This means Pitfall E (`Hmaj7` → probe `"Bmaj7"`) is structurally rejected with no extra branches.
- **Token field placement (D-15).** Optional 5th positional record parameter rather than a sibling property because (a) records support optional positional params (every existing 4-arg call site stays unchanged), (b) it lets the value flow through pattern-matching deconstruction if any consumer needs it, (c) memory cost is 8 bytes per token (a null reference) — negligible.
- **DiagnosticText idiom.** Property `=> OriginalText ?? Text;` rather than a method so consumers read it like a field. Renderer + MIDI export continue consuming `Token.Text` directly (canonical B-rooted); diagnostics consult `DiagnosticText`. ToString() stays unchanged so debug dumps stay terse and predictable.
- **Integration-loop cleanliness.** The plan template originally wanted `tests/test_pragma_isolation.flow` to use `H4q` in its body to trigger the parse error. That would make the integration-loop runner report it as FAIL. The cleaner split (per the plan's REVISED Step 5): the .flow fixture body has NO H4q (so it PASSes the loop); the failing-importer assertion lives in the Fact's inline RunSource exclusively. This keeps the loop clean while tightening the Fact assertion.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Replaced non-existent `length` builtin with `Sequence seq = | ... |; (print (str seq))` idiom**

- **Found during:** Task 3 verification (running Phase 21 Facts after wiring TryParseNote)
- **Issue:** The plan template (CONTEXT-section behavior list + the .flow fixtures) used `(length | ... |)` to assert sequence size. There is no `length` builtin in flow-lang — only `len(Array, ...)` (registered in `RegisterCollections`) and `len(String)` (registered in `RegisterStdLib`). Neither has a Sequence overload. As a result, `HMatchesB_InNoteStream`, `FullCoverage_HbHsharpDottedTied`, `ChordBracketInner_HRecognized`, and the module fixture all failed with `Function 'length' not found`.
- **Fix:** Replaced the `(length | ... |)` idiom with `Sequence seq = | ... |; (print (str seq))`. The `str(Sequence)` overload exists at `BuiltInFunctions.cs:190` and emits `Sequence[1 bars, 4 beats total]`. Clean parse + run with zero errors is the gate — the H tokens were the only thing that could fail at the lex/parse boundary.
- **Files modified:** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs`, `tests/test_h_alias.flow`, `tests/test_pragma_isolation_module.flow`
- **Verification:** Phase 21 Facts: 25/25 GREEN. test_h_alias.flow / test_pragma_isolation_module.flow run clean.
- **Committed in:** `05c2174` (Task 3 commit; the Task 1 RED commit `e25edbd` had the broken `length` references but was committed deliberately as Wave-0 RED state per the TDD pattern)

**2. [Rule 3 — Blocking] Added `use "@std"` to BareH_StaysIdentifier and HmajOutsideNoteStream_StaysIdentifier Facts**

- **Found during:** Task 3 verification (same pass as deviation 1)
- **Issue:** The plan's `<behavior>` block listed BareH_StaysIdentifier as `Run inline source 'Int H = 5;\n(print (str H))' (no pragma)`. Without `use "@std"`, the parser doesn't see `print` or `str(Int)` declared as `internal proc` and emits `Function 'print' not found` / `Function 'str' not found` errors — the Facts failed even though the H-as-Identifier path was working correctly.
- **Fix:** Added `use "@std"` to both Facts (and to all other RunSource-based Facts that print/stringify) so the procedure declarations from `flow-lang/std.flow` are in scope.
- **Files modified:** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs`
- **Verification:** Both Facts now GREEN; the H-identifier path is verified, not masked by stdlib dispatch failure.
- **Committed in:** `05c2174` (Task 3 commit)

**3. [Rule 1 — Bug] Adjusted HMatchesB_InNoteStream substring assertion to `Sequence[`**

- **Found during:** Task 3 verification
- **Issue:** Initial Fact rewrite asserted `Assert.Contains("B4", stdout)` after switching to `(print (str seq))`. But `str(Sequence)` returns the timeline summary `Sequence[1 bars, 4 beats total]` rather than enumerating note names — so the literal canonical "B4" never appeared in stdout.
- **Fix:** Changed the substring to `Sequence[` — the bar-count summary fires only when the sequence renders cleanly (both H4q and B4q occupied beats 1-2 and 3-4 of the default 4/4). The Fact still proves zero errors via `Assert.Equal(0, errorCount)` + `Assert.True(ok, ...)`.
- **Files modified:** `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs`
- **Verification:** GREEN.
- **Committed in:** `05c2174` (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (2 Rule 3 — blocking; 1 Rule 1 — bug)
**Impact on plan:** All three are test-suite plumbing fixes; production code (Token.cs + SimpleLexer.cs) shipped exactly as the plan's `<interfaces>` block specified. No D-13 / D-14 / D-15 / D-16 deviations.

### Locked Decisions Honored

D-13 (H→B at lex time, gated on `_pragmaSet.Has("hAsB")`), D-14 (full alias coverage — flats / sharps / dotted / tied / cent offsets / chord brackets), D-15 (Token preserves composer's original via `OriginalText`), D-16 (note-stream-only — `ChordParser.cs` unchanged) — all honored without modification. Pitfalls C (bare H), D (inner-call coverage at both Token sites), E (`NoteType.Parse("Bmaj7")` failure-as-rejection) all pinned by Facts that flipped GREEN as Task 3 landed.

## Verification Results

| Check | Result |
| ----- | ------ |
| `dotnet build` | clean (0 errors, 11 warnings — all pre-existing, none introduced) |
| Phase 21 Facts (`FullyQualifiedName~Phase21`) | 25/25 GREEN (15 from 21-01 + 9 HAliasFacts + 1 tightened-PragmaIsolation gain) |
| Phase 18 byte-identical regression (`FullyQualifiedName~Phase18`) | 19/19 GREEN |
| Full xUnit suite (`dotnet test`) | 411/411 GREEN (up from 399 in 21-01) |
| `dotnet run --project flow-interpreter tests/test_h_alias.flow` | exit 0 + `test_h_alias: PASSED` |
| `dotnet run --project flow-interpreter tests/test_h_identifier.flow` | exit 0 + `test_h_identifier: PASSED` |
| `dotnet run --project flow-interpreter tests/test_pragma_isolation_module.flow` | exit 0 + `test_pragma_isolation_module: PASSED` |
| `dotnet run --project flow-interpreter tests/test_pragma_isolation.flow` | exit 0 + both PASSED sentinels (module loaded cleanly, importer never inherited its pragma) |
| `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t" || echo FAIL: $t; done` | 56 PASS, 3 documented `ExpectedErrorScripts` (test_error_masking, test_iteration_guard, test_musical_context_errors) — no regressions |

## Pitfall Coverage Verification

| Pitfall | Coverage |
| ------- | -------- |
| **C — Bare H stays Identifier** | `BareH_StaysIdentifier` + `BareH_StaysIdentifier_EvenWithPragma` Facts + `tests/test_h_identifier.flow` integration script |
| **D — Inner-call canonicalization** | Both Token-construction sites in `ScanIdentifierOrKeyword` (direct-note line 656 + duration-suffix-stripping line 677) plumb `OriginalText`. `FullCoverage_HbHsharpDottedTied` Fact exercises `H4q.` and `H4h~` shapes that flow through the suffix-stripping path. |
| **E — `NoteType.Parse("Bmaj7")` rejection** | Direct unit Fact `NoteType_Parse_Bmaj7_Fails` (Assumption A1 guard) + `HmajOutsideNoteStream_StaysIdentifier` Fact (D-16 acceptance through the rejection path) |

## Phase 21 Fact Count Delta

- Pre-Plan-21-02: 15 Phase 21 Facts (9 PragmaScannerFacts + 5 PragmaRegistryFacts + 1 PragmaIsolationFact)
- Post-Plan-21-02: **25 Phase 21 Facts** (+9 HAliasFacts + 1 tightened-PragmaIsolation gain)

Total xUnit suite: 399 → **411** (the +12 covers +9 HAliasFacts + +1 PragmaIsolation new Fact + +2 from elsewhere in the tree picked up by the rebuild — note this is a cumulative count, not a delta-only).

## Hand-off to Plan 21-03

Plan 21-03 (closure) is unblocked. PRAG-01, PRAG-02, DEFER-02, and DEFER-03 are all shipped. The closure plan needs to:

- Strike DEFER-02 and DEFER-03 from `.planning/14-deferred-items.md`
- Mark DEFER-02 / DEFER-03 (and PRAG-01 / PRAG-02 if not already) Shipped in `.planning/REQUIREMENTS.md`
- Update `.planning/ROADMAP.md` Phase 21 row to `Complete` with the 4 success criteria checkmarks
- Update `.planning/STATE.md` cumulative metrics + decisions

No production-code changes expected in 21-03 — closure-only.

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The Phase 21 threat register (T-21-01 DoS, T-21-02 Levenshtein, T-21-03 closed-set tampering) is unchanged. The new threat-register entry `T-21-02-substitution` from Plan 21-02 (crafted H followed by control chars) is structurally mitigated: `NoteType.Parse` is the gate — anything not matching `letter [accidental]* [octave-digit]+ [alteration]?` throws and TryParseNote returns false. `HmajOutsideNoteStream_StaysIdentifier` Fact pins this.

## Self-Check: PASSED

**Files verified to exist:**
- FOUND: flow-lang/Lexing/Token.cs (modified)
- FOUND: flow-lang/Lexing/SimpleLexer.cs (modified)
- FOUND: flow-lang.Tests/Unit/Phase21/HAliasFacts.cs (created)
- FOUND: flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs (modified — 2 Facts)
- FOUND: tests/test_h_alias.flow (created)
- FOUND: tests/test_h_identifier.flow (created)
- FOUND: tests/test_pragma_isolation.flow (modified — kept clean)
- FOUND: tests/test_pragma_isolation_module.flow (modified — exercises pragma)

**Commits verified to exist:**
- FOUND: e25edbd (Task 1 — Wave 0 RED scaffolding)
- FOUND: 352efac (Task 2 — Token.OriginalText)
- FOUND: 05c2174 (Task 3 — SimpleLexer H→B + Fact fixups)
