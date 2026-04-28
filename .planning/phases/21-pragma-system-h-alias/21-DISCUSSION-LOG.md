# Phase 21: Pragma System + H-Alias — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 21-pragma-system-h-alias
**Areas discussed:** Pre-scan architecture, Pragma plumbing, Error UX, H-alias substitution layer

---

## Pre-scan Architecture

### Q1: Where should pragma extraction live in the lex/parse pipeline?

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated PragmaScanner (Recommended) | New `PragmaScanner.cs` runs BEFORE `SimpleLexer`. Cleanest separation; testable in isolation; lexer unchanged. | ✓ |
| Inline pre-pass inside SimpleLexer | Lexer gains a `ScanPragmas()` mode. Less new code; couples pragma logic to the 700-line lexer file. | |
| Tokenize-then-parser-validates | Lexer tokenizes pragma syntax as normal; parser validates "no statements before". Most uniform; loses the "pragmas don't need parser" guarantee. | |

**User's choice:** Dedicated PragmaScanner (Recommended) → D-01.

### Q2: What does the pre-scan return to downstream stages?

| Option | Description | Selected |
|--------|-------------|----------|
| PragmaSet record (Recommended) | `PragmaSet(IReadOnlySet<string> Enabled, IReadOnlyList<SourceLocation> DeclarationSites)`. Future-proof; declaration sites enable error messages. | ✓ |
| HashSet<string> of names | Minimal: just the names. Smallest API; declaration sites lost. | |
| Frozen immutable set in a static singleton | Per-parse `FrozenSet<string>` accessible globally. Avoids threading through constructors but reintroduces global state. | |

**User's choice:** PragmaSet record (Recommended) → D-02.

### Q3: What's allowed in the file prefix before the first `enable` and between pragmas?

| Option | Description | Selected |
|--------|-------------|----------|
| Comments + blank lines OK between/before (Recommended) | `// ...` and blank lines skipped during pre-scan. Matches Haskell `LANGUAGE` pragma ergonomics. | ✓ |
| Strict: pragmas must be the very first non-whitespace tokens | Comments before pragmas trigger error. Simpler; harsher UX. | |
| Comments OK, blank lines required between pragmas | Middle ground. Adds one rule to remember. | |

**User's choice:** Comments + blank lines OK (Recommended) → D-03.

### Q4: After pre-scan extracts pragmas, what does the main SimpleLexer see?

| Option | Description | Selected |
|--------|-------------|----------|
| Pragma lines stripped (replaced with blank lines preserving line numbers) (Recommended) | Line numbers in error messages still line up with original source. | ✓ |
| Unchanged source + lexer skips pragma region | Span-based fast-forward; couples scanner output with lexer internals. | |
| Pragma lines truly removed (line numbers shift) | Clean source string but every later error reports the wrong line number. | |

**User's choice:** Pragma lines stripped, line numbers preserved (Recommended) → D-04.

---

## Pragma Plumbing

### Q1: How is the active PragmaSet exposed to Parser and downstream stages?

| Option | Description | Selected |
|--------|-------------|----------|
| Constructor param on Parser (Recommended) | `new Parser(tokens, errorReporter, pragmaSet)`. Threaded into Parser.NoteStream via partial class. | ✓ |
| Field on FlowEngine threaded into each stage | Stages read PragmaSet via FlowEngine reference. Couples each stage to FlowEngine. | |
| AsyncLocal<PragmaSet> set by FlowEngine before parse | Ambient context. Hard to reason about; breaks across sync contexts. | |

**User's choice:** Constructor param on Parser (Recommended) → D-05.

### Q2: How does ModuleLoader prevent pragmas from leaking across `use` (PRAG-02)?

| Option | Description | Selected |
|--------|-------------|----------|
| Each imported file parsed with its OWN PragmaSet, never shared (Recommended) | Imports execute in caller's runtime context (existing) but parse with their own pragmas. Closes Pitfall 4 structurally. | ✓ |
| Imports parse with EMPTY PragmaSet (pragmas in modules ignored) | Modules can't use H-alias internally. Strict but lossy. | |
| Imports parse with empty PragmaSet + warning if module has pragmas | Educational but spammy if stdlib ships with pragmas. | |

**User's choice:** Each imported file parsed with its own PragmaSet (Recommended) → D-06.

### Q3: How do pragmas behave in the REPL and `-e` string evaluation?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-input: each REPL line / -e string parses with its own PragmaSet (Recommended) | One line at a time. Predictable; no hidden cross-input state. | ✓ |
| Per-session: REPL accumulates pragmas across inputs | Convenient for exploration; introduces a third scope to remember. | |
| Disallowed in REPL/-e: parse error | Forces composers to use scripts to test pragmas. Punishes the REPL workflow. | |

**User's choice:** Per-input REPL pragma scope (Recommended) → D-07.

### Q4: Should the parsed AST or Program record which pragmas were active during parsing?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — Program AST gains PragmaSet field (Recommended) | `Program(IReadOnlyList<Statement>, PragmaSet)`. Useful for LSP, diagnostics, future incremental re-parse. | ✓ |
| No — PragmaSet is parse-time-only and discarded after | Smallest surface. If LSP needs pragmas, it re-parses. | |

**User's choice:** Program AST gains PragmaSet field (Recommended) → D-08.

---

## Error UX

### Q1: What happens for `enable hAsB; enable hAsB;` (duplicate same pragma)?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent — set semantics, second one is a no-op (Recommended) | Aligns with charitable-interpretation memory. Documented in pragma reference. | ✓ |
| Parse error citing both lines | Catches accidental copy-paste; costs tolerance for low-stakes mistake. | |
| Warning, not error | Middle ground; introduces a third diagnostic level. | |

**User's choice:** Silent (Recommended) → D-09.

### Q2: What about `enable hAsB;` declared inside a module loaded via `use`?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent — module's pragma applies to module's parse only, never seen by importer (Recommended) | Already enforced structurally by D-06. Charitable interpretation + Pitfall 4 mitigation. | ✓ |
| Warn at the import site | Educational; spammy if stdlib ships with pragmas. | |
| Error: pragmas in modules forbidden | Strictest; means modules can't use H-alias internally. | |

**User's choice:** Silent (Recommended) → D-10.

### Q3: Error message + recovery for pragma after first non-pragma statement?

| Option | Description | Selected |
|--------|-------------|----------|
| Parse error citing both locations + suggested fix (Recommended) | Cites first non-pragma statement location; suggests "move to top". ErrorReporter accumulates; parse continues. | ✓ |
| Parse error, no fix suggestion, halt parsing | Brief; halt-parse hides subsequent errors. | |
| Silent: late `enable` lines treated as identifiers/syntax errors elsewhere | Composer gets confusing error elsewhere. | |

**User's choice:** Parse error citing both locations + suggested fix (Recommended) → D-11.

### Q4: Error message style for `enable nonExistentPragma;` (PRAG-01 mandates citing known list)?

| Option | Description | Selected |
|--------|-------------|----------|
| Cite full alphabetized known list + did-you-mean suggestion (Recommended) | Levenshtein-distance suggestion. Composer-friendly. | ✓ |
| Cite full known list, no did-you-mean | Cheaper; composer types it again. | |
| Cite first-N pragmas only + 'and N more, see docs' | Premature future-proofing. Phase 21 has at most 5 pragmas. | |

**User's choice:** Full list + did-you-mean (Recommended) → D-12.

---

## H-Alias Substitution Layer

### Q1: Where does the `H4q` → `B4q` substitution actually happen?

| Option | Description | Selected |
|--------|-------------|----------|
| SimpleLexer.TryParseNote (lex-time, pragma-gated) (Recommended) | TryParseNote accepts H-shapes when pragma active; bare `H` stays an identifier. | ✓ |
| Parser.NoteStream rewrites NoteLiteral tokens before compiling | Lexer unchanged; intermediate layers see `H` tokens. | |
| NoteStreamCompiler post-parse substitution | AST keeps `H` everywhere; type system / overload resolver / interpreter need to know H is sometimes valid. | |

**User's choice:** SimpleLexer.TryParseNote (Recommended) → D-13.

### Q2: What letter-shapes does H-alias cover?

| Option | Description | Selected |
|--------|-------------|----------|
| Full alias: every B-shape works with H (Recommended) | `H4q`, `Hb4q`, `H#4q`, `H4w`, `Hb4+50c` — all work. Predictable mental model. | ✓ |
| Bare H only (no accidental modifiers) | `Hb4q` raises "did you mean Bb?". German notation footgun; inconsistent with Phase 20 multi-letter enharmonics. | |
| H + naturals + octaves, but reject Hb / H# | Compromise; adds a rule to remember. | |

**User's choice:** Full alias (Recommended) → D-14.

### Q3: When error/diagnostic mentions an H-aliased note, what does it show?

| Option | Description | Selected |
|--------|-------------|----------|
| Show as H (preserve composer's authorship) (Recommended) | Token retains original-text metadata. Composer sees what they wrote. Aligns with charitable interpretation. | ✓ |
| Show as B (canonical form) | Internally consistent; composer who typed H sees B in errors and may not connect them. | |
| Show both: 'note H4q (= B4q)' | Most explicit; double the noise in every error. | |

**User's choice:** Show as H, preserve authorship (Recommended) → D-15.

### Q4: Should the H-alias also work inside ChordLiteral expressions like `Hmaj7` or `Hm`?

| Option | Description | Selected |
|--------|-------------|----------|
| Note-stream context only (Recommended) — ChordParser unchanged | Matches DEFER-02/03 acceptance verbatim. Smallest blast radius. Hmaj7 captured as deferred. | ✓ |
| Extend to ChordLiteral too — `Hmaj7` works as Bmaj7 anywhere | Larger blast radius; counts as scope creep. | |

**User's choice:** Note-stream context only (Recommended) → D-16.

---

## Closure

### Final closure question

| Option | Description | Selected |
|--------|-------------|----------|
| Write CONTEXT.md (Recommended) | All 16 questions resolved at Recommended choices. Phase boundary tight. Proceed to capture decisions. | ✓ |
| Explore one more gray area | Open a 5th area (registry surface stubs, testing strategy, tutorial integration timing). | |

**User's choice:** Write CONTEXT.md (Recommended).

---

## Claude's Discretion

The user did not invoke "you decide" explicitly on any question — every answer was an explicit Recommended pick. Items left for the planner / researcher to decide are documented in CONTEXT.md `<decisions>` §"Claude's Discretion" (file layout, internal naming, record vs record-struct, Levenshtein flavor, test-file split, Facts class shape).

## Deferred Ideas

Captured in CONTEXT.md `<deferred>`:
- `Hmaj7` ChordLiteral H-alias (out of Phase 21 scope; possible follow-up).
- Pre-registering Phase 23/24 pragma stubs (D-17 ships only `hAsB`).
- Block-scope pragmas (REQ-deferred per D-02).
- LSP affordances for pragma diagnostics (flow-lsp roadmap).
- `use`-time warning when imported module has pragmas (rejected per D-10).
