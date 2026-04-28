# Phase 21: Pragma System + H-Alias — Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

File-scope `enable <pragma>;` infrastructure that downstream phases plug into (Phase 23 microtonal, Phase 24 scaleLint), plus the first concrete pragma `hAsB` that aliases `H` → `B` inside note streams only. Locked by REQUIREMENTS.md PRAG-01 / PRAG-02 / DEFER-02/03 and milestone-locked decision D-02.

**In scope:**
- New `flow-lang/Lexing/PragmaScanner.cs` running before `SimpleLexer` in the pipeline.
- New `flow-lang/Lexing/PragmaSet.cs` (or sibling) — value type carrying enabled pragma names + declaration sites.
- New `flow-lang/Lexing/PragmaRegistry.cs` — closed set of known pragma names.
- `Parser` constructor gains a `PragmaSet` parameter; threaded into `Parser.NoteStream`.
- `ModuleLoader.cs` parses each imported file with its own `PragmaSet`.
- `SimpleLexer.TryParseNote` accepts `H`-shaped notes when `pragmaSet.Has("hAsB")`; substitutes to `B` at lex time.
- `Program` AST gains a `PragmaSet Pragmas` field.
- `FlowEngine.cs` orchestrates the new pre-scan stage.
- Acceptance: PRAG-01 + PRAG-02 + DEFER-02/03 from REQUIREMENTS.md.

**Out of scope (deferred or other phases):**
- Block-scope pragmas — explicitly deferred per D-02.
- `enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;` — Phase 23 (MICR-*).
- `enable scaleLint;` — Phase 24 (LINT-*).
- `Hmaj7` / H-rooted ChordLiteral expressions — note-stream-only per DEFER-02/03.
- LSP integration of pragma diagnostics — flow-lsp work, not flow-lang.

</domain>

<decisions>
## Implementation Decisions

### Pre-scan Architecture
- **D-01:** Pragma extraction lives in a new `flow-lang/Lexing/PragmaScanner.cs`, invoked by `FlowEngine.cs` BEFORE `SimpleLexer`. Reads raw source text, extracts `enable <name>;` lines from the prefix, returns `(PragmaSet, transformedSource)`. `SimpleLexer` never sees `enable` syntax. Cleanest separation; testable in isolation; lexer file unchanged.
- **D-02:** Pre-scan returns a `PragmaSet` record: `PragmaSet(IReadOnlySet<string> Enabled, IReadOnlyList<PragmaDeclarationSite> Sites)` where each `PragmaDeclarationSite` carries `(string Name, SourceLocation Location)`. Future-proof for Phase 23/24 metadata; declaration sites enable error messages that point back to where each pragma was declared.
- **D-03:** Pre-scan accepts comments (`// ...`) and blank lines anywhere in the prefix region — before, between, and after pragma declarations. `// my file\n\nenable hAsB;\n// notes\nenable scaleLint;\n\nuse "@std";` is legal. Matches Haskell `LANGUAGE` pragma ergonomics composers will expect.
- **D-04:** After pragma extraction, the main `SimpleLexer` sees the source with pragma lines REPLACED BY EQUIVALENT-LENGTH WHITESPACE (preserving newlines). Line numbers in subsequent error messages line up with the user's original source file. Implementation: `PragmaScanner` returns a `string transformedSource` that is character-by-character identical to the original except pragma regions become spaces + retained `\n` characters.

### Pragma Plumbing
- **D-05:** `PragmaSet` is a constructor parameter on `Parser`: `new Parser(tokens, errorReporter, pragmaSet)`. Threaded into `Parser.NoteStream` via the existing partial class. Explicit, testable, no global state. Updates required at every Parser instantiation site (FlowEngine + tests + REPL).
- **D-06:** `ModuleLoader.cs` enforces PRAG-02 structurally: each imported file is parsed with its OWN `PragmaSet`, computed from that file's source via `PragmaScanner`. The importing file's `PragmaSet` stays untouched. Imports continue to execute in caller's runtime context (existing behavior preserved) but parse with their own pragmas. Closes Pitfall 4 at the parse-time boundary, not the runtime boundary.
- **D-07:** REPL and `-e` string evaluation: each input line / `-e` string is treated as one logical "file" and gets its own `PragmaSet`. `enable hAsB; | H4q B4q |` typed in one REPL line works; the next REPL line resets to empty pragmas. Matches file-scope semantics applied to a logical input. Predictable, no hidden cross-input state.
- **D-08:** `Program` AST gains a `PragmaSet Pragmas` field: `Program(IReadOnlyList<Statement> Statements, PragmaSet Pragmas)`. Useful for LSP tooling, future incremental re-parse, and diagnostic reporting. Tiny memory cost; high signal for downstream tooling.

### Error UX
- **D-09:** Duplicate `enable hAsB; enable hAsB;` is SILENT — set semantics, second declaration is a no-op. No error, no warning. Aligns with charitable-interpretation memory (silent-and-documented over errors). Documented in pragma reference.
- **D-10:** Pragmas declared inside a module loaded via `use` are SILENT — module's pragma applies to module's parse only, never seen by importer (already enforced structurally by D-06). No diagnostic emitted. Module legitimately uses its pragma internally; isolation just means it doesn't leak.
- **D-11:** Pragma after first non-pragma statement raises a parse error citing BOTH locations + a suggested fix. Error format: `'enable {name};' at line {N}: pragmas must appear before any other statement. First non-pragma statement was at line {M} ({first_stmt_summary}). Move the pragma to the top of the file.` ErrorReporter accumulates as usual; parse continues so other errors surface in the same pass.
- **D-12:** Unknown pragma name cites the full alphabetized known list + did-you-mean suggestion. Error format: `unknown pragma '{typed_name}' at line {N}. Did you mean '{nearest}'?\nKnown pragmas: {alphabetized_csv}.` Levenshtein distance for did-you-mean. Composer-friendly; satisfies PRAG-01 "unknown pragma names raise a clear error citing the known list".

### H-Alias Substitution
- **D-13:** `H` → `B` substitution happens at LEX TIME inside `SimpleLexer.TryParseNote`, gated by `pragmaSet.Has("hAsB")`. When the pragma is active, `TryParseNote` accepts `H`-shaped strings (letter + accidental? + octave + duration) and emits a `NoteLiteral` token with text canonicalized to start with `B` instead of `H`. Bare `H` (no octave digit) continues to fall through to `Identifier`, so `Int H = 5;` keeps compiling unchanged. The `Token` retains the original source location and original-text metadata so diagnostics can show `H` to the composer (per D-15).
- **D-14:** Full alias coverage — every B-shape works with H. `H4q`, `Hb4q` (= Bb4q), `H#4q` (= B#4q which DEFER-04 from Phase 20 resolves to C natural via `HarmonyFunctions.Enharmonic`), `H4w`, `Hb4+50c`, `H4q.`, `H4h~` all work. Predictable mental model: H is a perfect synonym for B inside `| ... |`.
- **D-15:** Error and diagnostic messages preserve the composer's authorship — when a note was typed as `H`, the message says `H4q` not `B4q`. Internal pitch handling uses B canonically, but `Token` carries the original source text alongside the canonical text. Renderer / MIDI export consult the canonical text (B); diagnostics consult the original text (H). Aligns with charitable-interpretation memory: the composer wrote H, we play B, they read H back when something goes wrong.
- **D-16:** H-alias is NOTE-STREAM-CONTEXT ONLY per REQUIREMENTS.md DEFER-02/03 acceptance. `Hmaj7` outside `| ... |` is NOT a valid chord literal — `ChordParser.cs` is unchanged. Inside `| ... |`, chord brackets like `[H4 D#5 F#5]q` work because their inner notes go through the same H-aliasing `TryParseNote` path. Smallest blast radius matching REQ acceptance verbatim. Hmaj7 chord literal is captured as a deferred idea (separate phase or follow-up).

### Phase 21 Registry Surface
- **D-17:** `PragmaRegistry` ships with `hAsB` as the only ACTIVE pragma in Phase 21. Future pragmas (`justIntonation`, `pythagorean`, `equalTemperament`, `scaleLint`) are NOT pre-registered — they activate in their owning phases (23, 24). Rationale: smallest blast radius for Phase 21; closed-set checking still works (unknown names error per D-12); Phase 23/24 will add their entries when they ship. Avoids stub-pragma test surface that would need maintenance.

### Claude's Discretion
- Exact file layout under `flow-lang/Lexing/` (one file vs split between `PragmaScanner.cs`, `PragmaSet.cs`, `PragmaRegistry.cs`) — researcher / planner decides.
- Internal naming of the `PragmaDeclarationSite` record and of the `PragmaSet` static factory methods — researcher / planner decides.
- Whether `PragmaSet` is a `record` or `record struct` — researcher decides based on memory profile of REPL hot path.
- Levenshtein algorithm choice for the did-you-mean suggestion (raw Levenshtein vs Damerau-Levenshtein vs Wagner-Fischer) — planner decides; correctness over speed since this is only emitted on error.
- Test placement: `tests/test_pragmas.flow` for PRAG-01/02 coverage + `tests/test_h_alias.flow` for DEFER-02/03 coverage, OR a single combined file — planner decides.
- Whether to add an `XUnit` Facts class for PragmaScanner unit tests — planner decides; existing pattern in flow-lang has Facts for individual features.

### Folded Todos
None — no pending todos matched Phase 21 scope at the time of discussion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 21 Locked Requirements
- `.planning/REQUIREMENTS.md` §"Pragma System & H-Alias" — PRAG-01, PRAG-02, DEFER-02/03 acceptance criteria.
- `.planning/REQUIREMENTS.md` §"Locked decisions" D-02 — pragmas are file-scope only, top-of-file only, NOT propagated via `use`.
- `.planning/ROADMAP.md` §"Phase 21" — 4 success criteria + binding pre-orderings #2/#3.

### Pitfalls and Constraints
- `.planning/research/PITFALLS.md` §"Pitfall 4: Pragma system leaking across `use` imports" — file-scoped at parser level, never enter `ExecutionContext`.
- `.planning/research/PITFALLS.md` §"Performance Traps" + §"Module imports (`use`)" row.

### Existing Code This Phase Touches
- `flow-lang/Core/FlowEngine.cs:51` — pipeline orchestration; `ModuleLoader` instantiation site; insertion point for the new pre-scan stage.
- `flow-lang/Lexing/SimpleLexer.cs` — `TryParseNote` (line 689) and `ScanIdentifierOrKeyword` (line 526) where note letter recognition lives. Modified for D-13/D-14.
- `flow-lang/Lexing/Token.cs` — `Token` record. Augmented with original-text metadata for D-15.
- `flow-lang/Lexing/TokenType.cs` — `NoteLiteral` token type. Unchanged.
- `flow-lang/Parsing/Parser.cs:18,32` — `Parser` partial class + constructor. Constructor gains `PragmaSet` parameter per D-05.
- `flow-lang/Parsing/Parser.NoteStream.cs:38` — `ParseNoteStream` entry. Reads `pragmaSet` via the parser instance for D-13 substitution check.
- `flow-lang/Runtime/ModuleLoader.cs:18,27,122` — module loading; runs each imported file's pragma scan per D-06.
- `flow-lang/Ast/Statements/` — `Program` AST node. Gains `PragmaSet Pragmas` field per D-08.

### Prior-Phase Decisions This Phase Builds On
- Phase 20 (DEFER-04, shipped d835336) — multi-letter enharmonic edges `B# ↔ C` resolution in `HarmonyFunctions.Enharmonic`. H-alias inherits this when `H#4` resolves through the canonical B path.
- Phase 14 — keyword-collision grep precedent (`H`, `end`, `buf`, `_` rename incidents). Pragma names + the H token must pass collision grep before introduction.

### Charitable Interpretation Memory (Project-Wide)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md` — informs D-09, D-10, D-15 (silent-and-documented over errors when behavior is reasonable).
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md` — informs D-02 syntax shape (functional, no infix), and the choice not to introduce pragma operators.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ErrorReporter` (referenced throughout flow-lang) — accumulates errors rather than throwing per CLAUDE.md "Error accumulation" principle. Used for D-11 / D-12 error emission without halting the parse pass.
- `SourceLocation` type (already in use across `Token.cs`) — feeds `PragmaDeclarationSite` per D-02 directly.
- `IsKeyword` / `ScanIdentifierOrKeyword` machinery in `SimpleLexer.cs` — pattern for pragma-aware token classification at lex time without rewriting the lexer.
- `Token` record — already an immutable record; augmenting with an optional `originalText` field is additive per D-15.

### Established Patterns
- **No-runtime-state for parse-time concepts** (CLAUDE.md "Module imports execute in the caller's context — no separate scope/namespace isolation"). Pragmas MUST stay at parse time and never enter `ExecutionContext` per D-05/D-06 — running afoul of this would re-trigger Pitfall 4.
- **Manual recursive-descent parsing with partial classes** (`Parser.cs` + `Parser.NoteStream.cs`). New pragma plumbing follows the same partial-class extension shape rather than introducing a visitor pattern.
- **Closed-enum / closed-set design** (existing pattern: `TokenType` is a closed enum; `DurationValue` is a closed enum). `PragmaRegistry` matches this house style as a closed set per D-17.
- **Record types for AST nodes** (CLAUDE.md "AST nodes are `record` types for immutability"). `Program` extension per D-08 + `PragmaSet` per D-02 follow this pattern.

### Integration Points
- `FlowEngine.cs:51` — single insertion point where `PragmaScanner.Scan(source)` is called before `SimpleLexer` and the resulting `PragmaSet` flows into `new Parser(...)` per D-01 / D-05.
- `ModuleLoader.cs:122` (assembly directory + import resolution) — point where each imported file's source is loaded; D-06 inserts a `PragmaScanner.Scan` per imported file.
- `Parser.cs` constructor + `Parser.NoteStream.cs` partial — only two places that read the `PragmaSet` per D-05.
- `SimpleLexer.cs:689` (`TryParseNote`) — only one place where H → B substitution lives per D-13. Pragma flag reaches the lexer either via constructor (cleanest, matches D-05 shape) or via the upstream PragmaScanner result threaded through. Planner decides exact wiring.

</code_context>

<specifics>
## Specific Ideas

- The composer using `H` notation is most likely a German-trained musician; D-15 (preserve H in error messages) is targeted at that user. Renderer canonicalization to B is invisible; error messages show what they typed.
- `enable hAsB;` is the only pragma to ship in Phase 21. The closed-registry mechanism + error UX (D-12 did-you-mean) is what makes the pragma system **infrastructure** rather than a one-off feature — Phase 23/24 just register their names and the rest works.
- The `transformedSource` returned by `PragmaScanner` (per D-04) is critical for downstream error messages. Implementation note for the planner: replacing pragma chars with spaces (NOT removing) is the cheap way to preserve line+column numbering. Don't skimp.

</specifics>

<deferred>
## Deferred Ideas

### Out of Phase 21 Scope
- **`Hmaj7` / H-rooted ChordLiteral expressions** — REQ DEFER-02/03 explicitly limits H-alias to note-stream context. Captured for a possible follow-up phase or as out-of-scope for v1.3 entirely. Decision deferred until composer feedback indicates need.
- **Pragma-stub pre-registration for Phase 23/24** (`justIntonation`, `pythagorean`, `equalTemperament`, `scaleLint`) — Phase 23 / Phase 24 will register their own pragmas when they ship. Phase 21 ships only `hAsB` per D-17. Avoids stub-pragma test maintenance.
- **Block-scope pragmas** — explicitly deferred per D-02 (REQUIREMENTS.md "Future Requirements"). v1.3 ships file-scope only.
- **LSP integration of pragma diagnostics** — flow-lsp surfaces pragma errors only via the existing diagnostic plumbing. No new LSP affordances (hover-over-pragma docs, completion of known pragma names) in Phase 21. Captured for flow-lsp roadmap.
- **`use` source-level warning when imported module has pragmas** — discussed in Error UX area; rejected (D-10) in favor of silence per charitable interpretation. If composer feedback indicates confusion, revisit.

### Reviewed Todos (not folded)
None — no todos surfaced for Phase 21.

</deferred>

---

*Phase: 21-pragma-system-h-alias*
*Context gathered: 2026-04-26*
