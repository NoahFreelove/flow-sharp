# Phase 24: Scale Linting (flow-lsp) - Context

**Gathered:** 2026-05-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Opt-in `enable scaleLint;` pragma activates a new flow-lsp analysis pass that surfaces non-diatonic notes inside `key { ... }` contexts as Information-severity LSP diagnostics. Locked by REQUIREMENTS.md LINT-01 / LINT-02 / LINT-03 and the ROADMAP "zero flow-lang touch" goal — flow-lang receives one mechanical line (the pragma name in `PragmaRegistry.cs`, reserved by Phase 21 D-17 for this phase).

**In scope:**
- Register `"scaleLint"` in `flow-lang/Lexing/PragmaRegistry.cs.KnownPragmas` — the only flow-lang touch.
- New `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (or sibling) that walks `Program.Statements` looking for `MusicalContextStatement(ContextType=Key, ...)` blocks containing `NoteStreamExpression` nodes; emits LSP `Diagnostic` instances for non-diatonic `NoteElement` / `ChordElement` / `RandomChoiceElement` / `TupletElement`-recursed notes.
- New `flow-lsp/Diagnostics/DiatonicSpellings.cs` private helper holding 7-mode interval arrays (Major / Natural Minor / Dorian / Phrygian / Lydian / Mixolydian / Locrian) and per-key diatonic-spelling derivation (circle-of-fifths or hardcoded 30-key map — planner decides).
- Wire the analyzer into the `didChange` pipeline so diagnostics publish alongside parse errors; clear behavior preserved (empty publish clears prior squiggles).
- Acceptance: LINT-01 + LINT-02 + LINT-03 from REQUIREMENTS.md.

**Out of scope (deferred or other phases):**
- Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone scales — REQ scope is church modes only; deferred to a future phase or v1.4.
- Quick-fix code actions ("respell as F4", "wrap in `key Gmajor { }`") — Phase 17 D defers code actions to a future phase.
- Hover-rich diagnostic detail — chosen against in DA-6 (helpful inline message instead).
- Standalone notes outside `| ... |` note streams — no surrounding key context to compare against; silent.
- Notes inside `| ... |` but outside any `key { }` block — no diatonic set defined; silent.
- Borrowed-chord / modal-mixture analysis (e.g., recognizing bVII as a legal Mixolydian borrowing in major) — REQ defines diatonic strictly; modal mixture would belong in a future "harmonic linting" phase.
- Roman numeral mismatch warnings (e.g., `iii` in Cmajor when major-quality `III` was intended) — out of scope per REQ.
- Performance optimization beyond "good enough on a typical-size .flow file at the existing 150ms didChange debounce" — Phase 17 D-03 already establishes the debounce shape.

</domain>

<decisions>
## Implementation Decisions

### Diatonic Comparison Shape (DA-1)
- **D-01:** Spelling-aware diatonic check. The analyzer compares letter+accidental against the active key's diatonic spelling set, not just the pitch-class set. Consequences:
  - In `key Cmajor { }`: `F#4` flagged AND `Gb4` flagged (both non-diatonic; messages differ).
  - In `key Cmajor { }`: `E#4` flagged even though pitch-class 5 (= F natural) IS diatonic — the spelling `E#` is not in Cmajor's diatonic set `{C, D, E, F, G, A, B}`.
  - In `key Fmajor { }`: `Bb4` is diatonic (the key's b̂7), `A#4` would be flagged as non-diatonic (same pitch class, wrong spelling for the key).
  - Aligns with Phase 23's spelling-aware JI tables (Eb4 ≠ D#4 under JI per Phase 23 D-09) and the project's `feedback_charitable_interpretation` memory: a composer who spells F# wrote F# on purpose; we should call out spelling drift, not just pitch-class drift.

### Mode Coverage (DA-2)
- **D-02:** All seven church modes produce diagnostics. The analyzer recognizes `key <root>major`, `<root>minor`, `<root>dorian`, `<root>phrygian`, `<root>lydian`, `<root>mixolydian`, `<root>locrian` via `ScaleDatabase.TryParseKeyWithMode` (Phase 23 D-04, already public + shipped). Each mode has its own 7-note diatonic-spelling set derived from W/H step patterns rooted at the parsed tonic.
- **D-03:** [informational] Pentatonic, blues, harmonic-minor, melodic-minor, whole-tone, octatonic, and other non-7-note scales are out of scope. `key Cblues { }` (parser-rejected today anyway) emits no diagnostics if it ever parses; the lint stays silent on unrecognized modes per the charitable-interpretation memory. (No plan task — covered by D-22 fail-open and the closed-set design of `Mode` enum.)

### Helper Location (DA-3)
- **D-04:** The 7-mode diatonic-spelling helper lives in `flow-lsp/Diagnostics/DiatonicSpellings.cs` — private to flow-lsp. Honors the "zero flow-lang touch" goal verbatim. flow-lang gets the single one-line `PragmaRegistry.KnownPragmas["scaleLint"] = "..."` entry and nothing else. If a second consumer of mode-aware diatonic sets emerges (e.g., future theory tooling, lint reuse in a CLI checker), promote the helper to flow-lang then — YAGNI now.
- **D-05:** The helper signature is approximately `static IReadOnlyList<string> GetDiatonicSpellings(string rootNote, Mode mode)` returning the 7 letter+accidental strings (e.g., `["C","D","E","F","G","A","B"]` for Cmajor; `["F","G","A","Bb","C","D","E"]` for Fmajor; `["E","F#","G","A","B","C#","D"]` for Edorian). Planner decides exact return type (`string[]` vs `IReadOnlySet<string>` for membership-check perf).

### Scan Scope Inside Note Streams (DA-4 + DA-5)
- **D-06:** `NoteElement` — always checked. Locked by REQ LINT-01.
- **D-07:** `ChordElement` (bracket chord like `[C4 E4 G4]q`) — recursed; each contained `NoteElement` checked independently. `[C4 F#4 G4]q` in Cmajor flags exactly one diagnostic on `F#4`.
- **D-08:** `NoteElement` with `CentOffset` (e.g., `E4+50c`) — diatonicity decided by the BASE note (E4 in Cmajor → diatonic, no diagnostic). Cents are intentional fine-tuning per the project's notational-intent posture; never trigger lint. `Eb4+50c` in Cmajor → flagged on the base spelling, cents irrelevant to the message.
- **D-09:** `RandomChoiceElement` (e.g., `(? C4 F#4)` and seeded `(?? ...)` form) — recursed; each option treated as a separate notional NoteStreamElement and checked. `(? C4 F#4)` in Cmajor flags one diagnostic on `F#4`.
- **D-10:** `TupletElement` (`{3:2 C4 D4 F#4}q`) — recursed; each contained NoteStreamElement checked. Nested tuplets recurse through.
- **D-11:** `RomanNumeralElement` (`I`, `IV`, `V7`, `vii°`) — SKIP. Roman numerals are diatonic-by-construction (resolved against the active key); flagging them would be a logical contradiction. Aligns with charitable-interpretation memory.
- **D-12:** `NamedChordElement` (chord literal like `F#m`, `Cmaj7`, `Bbmaj7` inside `| ... |`) — SKIP. Chord literals are intentional declarative notation; flagging `F#m` inside `key Cmajor { }` would create three diagnostics per chord and clobber the editor for borrowed-chord progressions, secondary dominants, and modal mixture. Composers who write `Bbmaj7` in C major are deliberately reaching for it.
- **D-13:** `VariableReferenceElement` (e.g., `| $myseq |` interpolation) — SKIP. Statically undecidable; the variable's contents are only known at evaluation time, which the LSP does not run per Phase 17 architecture.
- **D-14:** `RestElement` — SKIP. No pitch.
- **D-15:** Notes inside `| ... |` but outside any enclosing `key { }` block emit zero diagnostics. No diatonic set is defined; silence is the correct behavior. Detection uses Phase 17's `NoteStreamContext.FindEnclosingKey` returning null.

### Diagnostic Message + Range (DA-6 + DA-7)
- **D-16:** Helpful message style. Format:
  - Standard non-diatonic, normal spelling: `"<note> not diatonic in <key> (try <alt1> or <alt2>)"` — e.g., `"F#4 not diatonic in Cmajor (try F4 or G4)"`. Alternatives are the two adjacent in-scale pitches by semitone distance.
  - Spelling-aware case (pitch-class IS diatonic but spelling is not, per D-01): `"<note> not diatonic in <key>; pitch-class matches <enharmonic> (try <enharmonic>)"` — e.g., `"E#4 not diatonic in Cmajor; pitch-class matches F (try F4)"`.
  - Same-pitch-class-different-spelling case: `"Gb4 not diatonic in Cmajor (try F4 or G4)"` — same suggestion as `F#4` since the diagnostic's purpose is to surface the missing-from-key fact, not to lecture on spelling drift between identical sounding pitches.
- **D-17:** Token-wide squiggle range. The analyzer walks `ParseResult.Tokens` to find the `NoteLiteral` token whose `Token.Location` matches the offending `NoteElement.Location`, then constructs an LSP `Range` from `(line-1, col-1)` to `(line-1, col-1 + Token.Text.Length)`. `Token.Text` already canonicalized (Phase 21 D-13 H→B substitution applies before the LSP sees it; under `enable hAsB; enable scaleLint;` an `H4q` in Cmajor lints against canonical `B4q` and is silent — diatonic). `Token.OriginalText` (Phase 21 D-15) used in the diagnostic message text so composers see the spelling they typed.
- **D-18:** Diagnostic source string is `"flow.scaleLint"`. The existing `LspMappings.ToRange` pipeline used by parse errors emits `Source = "flow"` (per `DiagnosticsPublisher.BuildDiagnostics` line 41); scale-lint diagnostics use the dotted suffix `"flow.scaleLint"` so editors and editor-toggle UIs can filter or disable scale-lint independently of parse errors. Existing `LspMappings.ToRange` is unchanged for parse errors; the analyzer constructs its own `Range` and `Diagnostic` instances directly (skipping `ToRange`'s 1-character default).

### Pragma Activation Plumbing
- **D-19:** Activation gate is `parseResult.Ast.Pragmas.Has("scaleLint")` — the `Program.Pragmas` field (Phase 21 D-08) already populated during parse. The analyzer is invoked unconditionally on every `didChange` parse but short-circuits and emits zero diagnostics when the pragma is absent. Satisfies LINT-02 ("opt-in only, never default-on") at the analyzer entry point.
- **D-20:** [informational] REPL handling inherits Phase 21 D-07 — pragma scope is per-line. A REPL line that declares `enable scaleLint;` activates lint for THAT input only. The next REPL line resets to empty pragmas. The LSP does not run inside the REPL — this decision matters only for symmetry with how parse errors are scoped. (No plan task — REPL is outside the LSP boundary; symmetry is automatic via Phase 21 D-07 reuse.)

### Innermost-Key-Wins (LINT-03)
- **D-21:** Reuse Phase 17's `NoteStreamContext.FindEnclosingKey` token-walk algorithm verbatim. The analyzer iterates each `NoteStreamExpression`'s contained elements, computes each element's source offset, and calls `FindEnclosingKey(ast, tokens, source, position)` to resolve the innermost active key. Brace-depth tracking already correctly handles nested `key { key { } }` blocks (Phase 17 D-11 acceptance) and block exits (cursor after a closed key block returns null). Zero new traversal logic.
- **D-22:** When the innermost enclosing key is itself non-parseable by `TryParseKeyWithMode` (e.g., `key Eblues { }`), the analyzer emits zero diagnostics for that block — silent. Charitable: an unrecognized key is composer's choice; the lint is opt-in and should fail open. The key's mode is unknown, so no diatonic set exists.

### Activation State Diagnostic (Optional Bookkeeping)
- **D-23:** When `enable scaleLint;` is declared but NO `key { ... }` block exists anywhere in the file, the analyzer emits zero diagnostics. No "scaleLint pragma is inactive" meta-diagnostic — composers turn it on speculatively before adding a key block, and a meta-diagnostic would be noise. Pure silence; charitable.

### Claude's Discretion (Planner Decides)
- Pipeline integration shape: extend `DiagnosticsPublisher.Publish` to accept a second `IReadOnlyList<Diagnostic>` for LSP-native diagnostics, OR widen `FlowError` with a `Source` field, OR build a new `IScaleLintPublisher` interface that `TextDocumentSyncHandler` invokes alongside `IDiagnosticsPublisher`. Each preserves the empty-publish-clears-squiggles behavior.
- Whether to produce diagnostics during a partial-parse (when there are also parse errors) or only when the parse is clean. Recommendation: still produce — Information-severity diagnostics on a partially-parsed file are useful and the analyzer already null-checks element types.
- Diatonic-spelling derivation strategy in `DiatonicSpellings.cs`: 30-key hardcoded map (mechanical, fast, complete) vs. circle-of-fifths algorithm (~30 lines, generalizes). Recommendation: hardcoded map — explicit beats clever for a closed set this small.
- Whether to add per-mode acceptance tests (one `.flow` smoke per mode = 7 files) or a single `tests/test_scale_lint.flow` covering all 7 modes plus the spelling-aware corner cases. Planner decides.
- Test placement under `flow-lang.Tests/Unit/Phase24/` (mirroring Phase 17's `Unit/Phase17/Lsp*Tests.cs` convention) or a new `flow-lsp.Tests/` project. Recommendation: `flow-lang.Tests/Unit/Phase24/` — matches existing convention; no new test project needed.
- Exact wording of diagnostic alternative-pitch suggestions when the non-diatonic note is exactly midway between two diatonic neighbors (e.g., F# in C major is equidistant from F and G); D-16 says "two adjacent in-scale pitches by semitone distance" — planner picks ordering convention (lower-first vs. nearest-first vs. preferred-resolution-direction).
- Whether the analyzer caches the per-key diatonic-spelling set across `didChange` calls (small perf win for large files with stable keys). Default: don't cache; recompute per parse — the set is 7 strings.

### Folded Todos
None — no pending todos surfaced for Phase 24 scope at the time of discussion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 24 Locked Requirements
- `.planning/REQUIREMENTS.md` lines 79–81 — LINT-01, LINT-02, LINT-03 acceptance criteria (canonical contract).
- `.planning/REQUIREMENTS.md` line 13 — D-02 locks pragmas as file-scope only, top-of-file only, NOT propagated via `use`.
- `.planning/REQUIREMENTS.md` line 113 — "Default-on scale linting — anti-feature" out-of-scope confirmation.
- `.planning/ROADMAP.md` lines 175–184 — Phase 24 goal, dependency on Phase 21, 3 success criteria, "zero flow-lang touch" framing.

### Pitfalls and Constraints
- `.planning/research/PITFALLS.md` §"Pitfall 8" (referenced by REQ LINT-02 — never default-on). The pragma is the gate; analyzer must short-circuit when pragma is absent per D-19.

### Prior-Phase Decisions This Phase Builds On
- `.planning/phases/17-flow-language-server/17-CONTEXT.md` — Phase 17 D-03 (debounced full re-parse on `didChange`), D-06 (LSP forwards `ErrorReporter` to `publishDiagnostics`), D-11 (`NoteStreamContext.FindEnclosingKey` brace-depth-tracked innermost-key resolver — REUSED VERBATIM by D-21 here for LINT-03).
- `.planning/phases/21-pragma-system-h-alias/21-CONTEXT.md` — Phase 21 D-08 (`Program.Pragmas` field — read by D-19 here), D-13 (lex-time H→B substitution — relevant to D-17 token text canonicalization), D-15 (`Token.OriginalText` for spelling-preserving diagnostics — used by D-17 message text), D-17 (`PragmaRegistry` reservation of `"scaleLint"` for THIS phase).
- `.planning/phases/23-microtonal-tuning-wedge/23-CONTEXT.md` — Phase 23 D-04 (`ScaleDatabase.TryParseKeyWithMode` extended to recognize the 5 church-mode suffixes — REUSED by D-02 here for mode parsing). Phase 23 D-09 spelling-aware JI tables establish the spelling-aware precedent for D-01.

### Existing Code This Phase Touches or Reads
- `flow-lang/Lexing/PragmaRegistry.cs:16` — `KnownPragmas` dictionary. ONE-LINE TOUCH: add `["scaleLint"] = "..."` entry per D-04 / Phase 21 D-17 reservation. The only flow-lang touch in Phase 24.
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207` — `TryParseKeyWithMode` (Phase 23 D-04, public). READ-ONLY consumption by `DiatonicSpellings.GetDiatonicSpellings` per D-02 + D-05.
- `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs` — `Mode` enum (Major / Minor / Dorian / Phrygian / Lydian / Mixolydian / Locrian) defined by Phase 23. READ-ONLY consumption.
- `flow-lang/Ast/Expressions/NoteStreamExpression.cs:9-94` — `NoteStreamElement` hierarchy (`NoteElement`, `RestElement`, `ChordElement`, `NamedChordElement`, `RomanNumeralElement`, `VariableReferenceElement`, `RandomChoiceElement`, `TupletElement`). READ-ONLY traversal target.
- `flow-lang/Ast/Statements/MusicalContextStatement.cs:14` — `ContextType=Key` discriminant; READ-ONLY traversal target for D-15 / D-21.
- `flow-lang/Lexing/Token.cs` — `Token.Text` (canonical) and `Token.OriginalText` (Phase 21 D-15). READ-ONLY consumption per D-17.
- `flow-lsp/ParseSession.cs:18` — `Parse` returns `ParseResult(Ast, Tokens, Errors)` — both `Ast.Pragmas` (D-19) and `Tokens` (D-17 range computation) feed the analyzer.
- `flow-lsp/NoteStream/NoteStreamContext.cs:43` — `FindEnclosingKey(ast, tokens, source, position)`. REUSED VERBATIM by D-21.
- `flow-lsp/Handlers/DiagnosticsPublisher.cs:50` — `Publish(uri, errors)` integration point; planner extends or sibling-publisher per Claude's-discretion list.
- `flow-lsp/LspMappings.cs:33` — `ToSeverity(DiagnosticLevel.Info) → DiagnosticSeverity.Information` already maps correctly.

### Test Patterns to Follow
- `flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs` and `LspFixtures.cs` — Phase 17's xUnit pattern for LSP unit tests. Phase 24 mirrors this with `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerTests.cs` (or similar) per Claude's-discretion recommendation.
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — pattern for asserting against `PragmaRegistry.KnownPragmas` membership (one new fact: `"scaleLint"` is a known pragma).
- `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` — pattern for asserting analyzer behavior under a specific pragma being on/off.
- `tests/test_scale_lint.flow` — `.flow` integration smoke (ships at least one). Acceptance per LINT-01 / LINT-02 / LINT-03 examples in REQUIREMENTS.md.

### Project Memory (CLAUDE.md auto-memory)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md` — informs D-11 (skip Roman numerals), D-12 (skip named chord literals), D-13 (skip variable refs), D-15 (no diagnostic without an enclosing key), D-22 (silent on unknown keys), D-23 (no meta-diagnostic when pragma is on but no key block exists).
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md` — informs the no-arg pragma syntax shape (S-expr-aligned, no infix) inherited from Phase 21.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`NoteStreamContext.FindEnclosingKey`** (`flow-lsp/NoteStream/NoteStreamContext.cs:43`) — brace-depth-tracked token walk that resolves the innermost active `key <name>` block at any source offset. REUSED VERBATIM by D-21 for LINT-03 acceptance.
- **`ScaleDatabase.TryParseKeyWithMode`** (`flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207`) — public, mode-aware key parser (major / minor + 5 church modes). REUSED for D-02 mode resolution.
- **`Program.Pragmas`** (Phase 21 D-08) — already populated by `ParseSession.Parse`. D-19 reads `parseResult.Ast.Pragmas.Has("scaleLint")` directly.
- **`Token.OriginalText`** (Phase 21 D-15) — preserves composer-typed spelling through the H→B substitution. D-17 consumes this for the diagnostic message text so composers see the letter they typed.
- **`Token.Text` length** — D-17 token-wide range computation reads `Token.Text.Length` as the squiggle width.
- **`LspMappings.ToSeverity`** (`flow-lsp/LspMappings.cs:33`) — already maps `DiagnosticLevel.Info → DiagnosticSeverity.Information` for cases where the analyzer wants to construct via `FlowError`. (D-18 path constructs `Diagnostic` directly.)
- **`DiagnosticsPublisher.BuildDiagnostics`** (`flow-lsp/Handlers/DiagnosticsPublisher.cs:34`) — reference for the `FlowError → Diagnostic` mapping pattern; analyzer builds its own `Diagnostic` instances using the same shape (Severity / Source / Message / Range).
- **`Mode` enum** (`flow-lang/StandardLibrary/Audio/Tuning/Mode.cs`, Phase 23 D-04) — closed enum of the 7 supported modes; reused by `DiatonicSpellings.GetDiatonicSpellings(rootNote, mode)`.

### Established Patterns
- **Parse-time-only LSP analysis** (Phase 17 D-03 / D-06) — the LSP server NEVER runs the evaluator. Phase 24's analyzer is a pure AST + token traversal; no `ExecutionContext` or runtime state. Maintained.
- **Soft-failure error model** — analyzer must produce diagnostics even when the parse has errors (the AST is still mostly correct). Default behavior of OmniSharp `publishDiagnostics` already handles per-publish replacement; an empty diagnostic list clears prior squiggles.
- **Closed-set / closed-enum design** — `PragmaRegistry.KnownPragmas` is a closed set; `Mode` is a closed enum. D-04 stays inside the LSP boundary (private `DiatonicSpellings.cs`), but its 7-mode coverage matches the closed-set posture.
- **One-shot stderr warnings reserved for runtime** — Phase 23 D-11 / D-13 use `Console.Error` for one-shot session warnings. Phase 24 does NOT use this — LSP diagnostics replace the one-shot pattern for the static-analysis case. Composers get a squiggle, not a runtime warning.
- **AST node `record` types** — `NoteStreamElement` hierarchy is records; pattern-match dispatch in the analyzer per CLAUDE.md "switch expressions for node dispatch rather than visitor pattern".
- **xUnit Facts written BEFORE production code** — Phases 17–23 all RED → GREEN. Phase 24 plans should follow.
- **Phase 18 byte-identical regression gate** — D-04 / D-19 / D-23 ensure the LSP-only nature: no .flow script's runtime behavior changes; tutorial.flow / showcase.flow remain byte-identical because no flow-lang code path is altered beyond the one-line pragma registration.

### Integration Points
- **`PragmaRegistry.cs` line 16** — single one-line touch in flow-lang (the dictionary literal). Adds `["scaleLint"] = "Inside `key { }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."` (or similar one-line description). Honors Phase 21 D-17 reservation; honors Phase 24 ROADMAP "zero flow-lang touch" goal at the maximally-conservative interpretation.
- **`ParseSession.Parse` return** — `ParseResult(Ast, Tokens, Errors)` is the analyzer's input. No new field needed; all required state is already there.
- **`TextDocumentSyncHandler.didChange`** — invokes the analyzer alongside the existing `DiagnosticsPublisher.Publish` call. Pipeline-integration shape is Claude's discretion (planner decides extend-publisher vs. sibling-publisher vs. widen-FlowError).
- **`NoteStreamContext` static** — analyzer calls `FindEnclosingKey` directly; no need to extend the class. Phase 17's API surface is sufficient.

</code_context>

<specifics>
## Specific Ideas

- The diagnostic message format for a same-pitch-class-different-spelling case (e.g., `Gb4` in `key Cmajor { }`) intentionally suggests `F4` and `G4` rather than `F#4` — the goal of the diagnostic is to surface "this note is not in your key", not to teach circle-of-fifths spelling preference. Composers who deliberately wrote `Gb` rather than `F#` already have a notational reason; we don't lecture them via the alternative-pitch suggestion.
- The `E#4` in Cmajor case is the canary that DA-1 (spelling-aware) is actually doing what it claims: pitch-class 5 is diatonic in Cmajor (= F natural), but spelling `E#` is not. The diagnostic message format `"E#4 not diatonic in Cmajor; pitch-class matches F (try F4)"` makes the spelling-vs-pitch distinction explicit so composers don't think the lint is broken.
- LINT-03's acceptance text in REQUIREMENTS.md notes a wording bug ("F#4 not flagged in `key Cmajor { key Aminor { | F#4 | } }` because Aminor is innermost — but F# is not diatonic in Aminor either"). The replacement during planning should pick a realistic pair, e.g., outer `key Cmajor { key Gmajor { | F#4 | } }` — F#4 IS diatonic in Gmajor (the inner key wins) so it does NOT flag, even though it would flag against outer Cmajor. This is the LINT-03 invariant: innermost-key wins, full stop.
- The `tests/test_scale_lint.flow` smoke script should pin the LINT-01 acceptance verbatim (`enable scaleLint; key Cmajor { | C4 D4 E4 F#4 G4 | }` → exactly one Information diagnostic on `F#4`) and the LINT-02 acceptance (same key block without the pragma → zero diagnostics).
- The pragma-registry one-line entry text is a public-facing description (per Phase 21 D-17 the registry doubles as canonical pragma reference). Suggested: `"Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."` — terse, mirrors the existing entries' tone.
- Future "scope" expansion candidates that are conscientiously NOT in this phase: borrowed-chord recognition, secondary dominant tolerance (V/V always allowed), modal-mixture allowance (bVII in major), neapolitan / augmented-sixth chord exceptions. All would belong in a separate "harmonic-aware lint" phase if composer feedback requests it.

</specifics>

<deferred>
## Deferred Ideas

### Out of Phase 24 Scope
- **Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone / octatonic scale support** — REQ scope is church modes only. Future phase or v1.4 if composer feedback requests; would require new interval tables AND `ScaleDatabase.ParseKeyName` extensions.
- **Quick-fix code actions** ("respell as F4", "transpose to nearest in-scale", "wrap selection in `key Gmajor { }`") — Phase 17 explicitly defers code actions to a future phase; Phase 24 stays informational-only.
- **Borrowed-chord / modal-mixture analysis** — recognizing bVII as a legal Mixolydian borrowing in major, neapolitan/augmented-sixth tolerance, secondary-dominant pre-allowance — all belong in a separate "harmonic-aware lint" phase.
- **Roman numeral mismatch warnings** — flagging `iii` in Cmajor when the active context implies `III`-major-quality. Out of scope; not the same problem as scale linting.
- **Hover-rich diagnostic detail** — long-form explanation in hover text (key signature, mode interval pattern, alternative spellings list). DA-6 chose helpful inline message instead.
- **Per-token flow-lsp performance optimization** beyond the existing 150ms `didChange` debounce — no profiling has shown lint as a bottleneck.
- **Configurable diagnostic severity** (e.g., user toggles `enable scaleLint;` to Warning instead of Information for stricter projects) — not in REQ; future config flag if needed.
- **A "did you mean a different mode?" suggestion** when a user's notes don't match the declared key but DO match another mode (e.g., a stream of `C D Eb F G Ab Bb` in `key Cmajor { }` would fit `Cdorian` perfectly — "did you mean Cdorian?"). Interesting future enhancement; out of scope.
- **Analysis of standalone notes outside `| ... |` note streams** — there's no surrounding `key { }` context for a `Note c = F#4;` declaration; the lint stays silent. Could change if a future phase wires "implicit key context" into expression evaluation.
- **CLI lint mode** — running `dotnet run --project flow-lsp -- --lint path/to/file.flow` for headless CI use. Promote `DiatonicSpellings.cs` to flow-lang first if this is requested. YAGNI now.
- **Default-on scale linting** — explicitly anti-feature per REQUIREMENTS.md line 113 ("composers expect non-diatonic notes by design"). Will not change.

### Reviewed Todos (not folded)

None — no todos surfaced for Phase 24.

</deferred>

---

*Phase: 24-scale-linting-flow-lsp*
*Context gathered: 2026-05-04*
