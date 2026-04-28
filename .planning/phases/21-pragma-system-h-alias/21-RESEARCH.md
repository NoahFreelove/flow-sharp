# Phase 21: Pragma System + H-Alias — Research

**Researched:** 2026-04-26
**Domain:** C# .NET 10 lexer/parser infrastructure — file-scope pragma extraction + H→B note alias
**Confidence:** HIGH

## Summary

Phase 21 adds two coupled but cleanly separable pieces of infrastructure to flow-lang:

1. A **pre-lex pragma extraction stage** (`PragmaScanner`) that runs before `SimpleLexer` on every parse unit (top-level file, every `use`-imported module, every REPL line, every `-e` string). It returns a `(PragmaSet, transformedSource)` tuple where pragma lines are replaced by equivalent-length whitespace so subsequent line/column numbers in the lexer/parser line up with the user's original source.
2. The **first concrete pragma `hAsB`** that, when active for the file currently being parsed, makes `SimpleLexer.TryParseNote` accept H-shaped strings (`H4q`, `Hb4q`, `H#4q`, `Hb4+50c`, `H4q.`, `H4h~`) as note literals canonicalized to `B`-rooted notes — but **only inside note streams**, only when the `H` carries an octave digit or accidental (so bare `H` continues to scan as `Identifier` and `Int H = 5;` keeps compiling).

The work is overwhelmingly local: 1 new file folder (`flow-lang/Lexing/Pragma*.cs`), surgical edits to `SimpleLexer.TryParseNote`, additive changes to `Token`, a constructor-parameter addition on `Parser`, a one-call insertion in `FlowEngine.Execute`, and the same one-call insertion mirrored into `ModuleLoader.LoadModule`. The `Program` AST gains one record field. ~16 Parser/Lexer construction call sites need updates (mostly tests and the LSP).

There are no external dependencies, no new NuGet packages, no Pidgin-like libraries needed. The entire feature is hand-rolled C# and matches the project's "minimal dependencies" + "manual recursive-descent + immutable record AST" house style.

**Primary recommendation:** Three-plan decomposition — (21-01) PragmaScanner + PragmaSet + PragmaRegistry + Program.Pragmas + Parser/SimpleLexer constructor plumbing + FlowEngine + ModuleLoader wiring (closes PRAG-01 + PRAG-02 except for the lexer-time substitution); (21-02) `hAsB` pragma — Token original-text metadata + `SimpleLexer.TryParseNote` H acceptance gated on `pragmaSet.Has("hAsB")` + diagnostic emission preserves H (closes DEFER-02/03); (21-03) closure (REQUIREMENTS / ROADMAP / STATE / VERIFICATION update + 14-deferred-items DEFER-02/03 strikethrough). Plan 21-01 and 21-02 are STRICTLY SEQUENTIAL — H-alias depends on the pragma plumbing existing.

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Pre-scan Architecture:**
- **D-01:** Pragma extraction lives in a new `flow-lang/Lexing/PragmaScanner.cs`, invoked by `FlowEngine.cs` BEFORE `SimpleLexer`. Returns `(PragmaSet, transformedSource)`. `SimpleLexer` never sees `enable` syntax.
- **D-02:** Pre-scan returns `PragmaSet(IReadOnlySet<string> Enabled, IReadOnlyList<PragmaDeclarationSite> Sites)` where each `PragmaDeclarationSite` carries `(string Name, SourceLocation Location)`.
- **D-03:** Comments (`// ...`) and blank lines OK before/between/after pragma declarations in the prefix region.
- **D-04:** Pragma lines REPLACED with equivalent-length whitespace preserving newlines so subsequent line+column numbers line up with original source.

**Pragma Plumbing:**
- **D-05:** `PragmaSet` is a constructor parameter on `Parser`: `new Parser(tokens, errorReporter, pragmaSet)`. Threaded into `Parser.NoteStream` via the existing partial class.
- **D-06:** `ModuleLoader.cs` runs `PragmaScanner` per imported file — each imported file is parsed with its OWN `PragmaSet`.
- **D-07:** REPL and `-e` strings get a per-input `PragmaSet`.
- **D-08:** `Program` AST gains `PragmaSet Pragmas` field.

**Error UX:**
- **D-09:** Duplicate `enable hAsB; enable hAsB;` is SILENT (set semantics).
- **D-10:** Module pragmas don't leak — silent (already enforced by D-06).
- **D-11:** Pragma after first non-pragma statement → parse error citing both locations + "move to top" suggestion.
- **D-12:** Unknown pragma → error citing full alphabetized known list + Levenshtein did-you-mean.

**H-Alias Substitution:**
- **D-13:** `H` → `B` substitution at LEX TIME inside `SimpleLexer.TryParseNote`, gated by `pragmaSet.Has("hAsB")`. Bare `H` (no octave digit) stays an Identifier.
- **D-14:** Full alias coverage — H4q, Hb4q, H#4q, Hb4+50c, H4q., H4h~ all work.
- **D-15:** Token retains both original-text (`H4q`) and canonical-text (`B4q`) so diagnostics show H, renderer/MIDI uses B.
- **D-16:** Note-stream-context only — `Hmaj7` outside `| ... |` unchanged (`ChordParser` untouched).

**Phase 21 Registry Surface:**
- **D-17:** `PragmaRegistry` ships with `hAsB` as the only ACTIVE pragma in Phase 21.

### Claude's Discretion
- Exact file layout under `flow-lang/Lexing/` (one file vs split into `PragmaScanner.cs`, `PragmaSet.cs`, `PragmaRegistry.cs`).
- Internal naming of the `PragmaDeclarationSite` record + `PragmaSet` static factory methods.
- Whether `PragmaSet` is a `record` or `record struct`.
- Levenshtein algorithm choice for the did-you-mean suggestion.
- Test placement: combined or split between `tests/test_pragmas.flow` + `tests/test_h_alias.flow`.
- Whether to add an XUnit Facts class for PragmaScanner unit tests.

### Deferred Ideas (OUT OF SCOPE)
- **`Hmaj7` / H-rooted ChordLiteral expressions** — note-stream context only per DEFER-02/03 acceptance.
- **Pragma-stub pre-registration for Phase 23/24** (`justIntonation`, `pythagorean`, `equalTemperament`, `scaleLint`).
- **Block-scope pragmas** — explicitly deferred per D-02; v1.3 file-scope only.
- **LSP integration of pragma diagnostics** (hover/completion on pragma names) — flow-lsp roadmap, not Phase 21.
- **`use` source-level warning when imported module has pragmas** — rejected by D-10.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRAG-01 | `enable <featureName>;` declarations top-of-file only; pragmas after first non-pragma statement raise parse error; lexer pre-scan extracts pragmas before main lexing; PragmaRegistry is closed-set with clear unknown-name errors | §Pre-Scan Algorithm; §PragmaRegistry; §Levenshtein did-you-mean |
| PRAG-02 | Pragmas do NOT propagate across `use` imports — each module parsed with its own PragmaSet | §ModuleLoader integration |
| DEFER-02/03 | `enable hAsB;` activates `H` as `B` alias inside note-stream context; outside note streams `Int H = 5;` keeps compiling | §SimpleLexer.TryParseNote pragma gating; §Token original-text plumbing |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Pragma extraction from raw source | Pre-lex (new `PragmaScanner` stage) | — | D-01 — runs before `SimpleLexer`; produces transformedSource so the lexer never sees `enable` syntax. |
| Pragma registry (closed-set) | Compile-time (static class) | — | Mirrors existing closed-set patterns (`TokenType` enum, `DurationValue` enum). |
| Top-of-file enforcement (D-11) | Parser | — | Parser sees the FIRST non-pragma token; if a later `enable` slipped through, we have a different error. Actually: under D-04 `enable` lines are stripped pre-lex, so an `enable` AFTER a non-pragma statement WOULD reach the lexer/parser as a literal `enable` identifier — hence enforcement is cleanest in the **PragmaScanner** itself. See §Pre-Scan Algorithm. |
| Pragma threading into note-stream parse | Parser → Parser.NoteStream (D-05) | — | Parser carries `_pragmaSet` field; partial class accesses it. |
| H→B substitution at lex time | `SimpleLexer.TryParseNote` (D-13) | — | Single insertion point; the pragma flag reaches the lexer via a new constructor parameter. |
| Original-text preservation (D-15) | `Token` record (additive new field) | — | Token already carries `Text` and `Value` — the canonical-vs-original split is one additional optional field. |
| Renderer / MIDI export consults canonical | Existing render-path (no change) | — | Renderer reads `noteToken.Text` today; we set Text=canonical (`B4q`) and the new field=original (`H4q`). Zero render-path edits. |
| ModuleLoader-per-file isolation (D-06) | `ModuleLoader.LoadModule` | — | One additional `PragmaScanner.Scan(source)` call before the existing lex/parse. |

## Standard Stack

### Core (existing — no changes)

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | net10.0 | Runtime [VERIFIED: existing csproj] |
| C# 13 | latest | Language — record types, pattern matching [VERIFIED: existing code] |
| `xUnit` | (via flow-lang.Tests) | Test framework [VERIFIED: existing flow-lang.Tests/*.cs Facts] |

### New
None. Phase 21 is pure hand-rolled C# in flow-lang. Closed-set design means the registry is a `static class` with a `HashSet<string>` — no library needed.

### Libraries Explicitly NOT Recommended
| Library | Why Not |
|---------|---------|
| Any regex DSL / pragma-DSL parser library | The pragma syntax is one rule (`enable IDENT;`) — Wagner-Fischer Levenshtein and a per-line state machine are both ~30 lines of pure C#. Adding a dependency for this is overkill and contradicts CLAUDE.md "minimal dependencies". |
| Pidgin (already in csproj but unused) | Already cited in CLAUDE.md as opportunistic-cleanup candidate. Not relevant to Phase 21 — do NOT remove during this phase (separate concern, separate commit). |

## Architecture Patterns

### Pipeline Diagram (with Phase 21 changes highlighted)

```
                      ┌─────────────────────────────────────┐
                      │   Source code (raw string)          │
                      └──────────────────┬──────────────────┘
                                         │
                  ┌──────────────────────▼─────────────────────┐
NEW (Phase 21) →  │ PragmaScanner.Scan(source)                 │
                  │   - Walk prefix line-by-line               │
                  │   - Skip comments + blank lines (D-03)     │
                  │   - On `enable NAME;` → record + replace   │
                  │     with equivalent-length whitespace (D-4)│
                  │   - On first non-pragma line, stop scan    │
                  │   - On `enable` after stop → emit D-11 err │
                  │   - Validate names against PragmaRegistry  │
                  │     (D-12 Levenshtein did-you-mean)        │
                  │   Returns: (PragmaSet, transformedSource)  │
                  └──────────────────────┬─────────────────────┘
                                         │
                                         ▼
                   ┌────────────────────────────────────┐
                   │ SimpleLexer.Tokenize(transformed)  │
                   │   - Reads transformedSource        │
                   │   - In TryParseNote, accept H-     │ ← MODIFIED (D-13)
                   │     shaped notes when              │   PragmaSet from
                   │     pragmaSet.Has("hAsB")          │   ctor param
                   │   - Token gets canonical Text      │   (B4q) + new
                   │     OriginalText field (H4q)       │   field (D-15)
                   └────────────────────┬───────────────┘
                                        │
                                        ▼
                   ┌─────────────────────────────────────┐
                   │ new Parser(tokens, errReporter,     │ ← MODIFIED (D-05)
                   │            pragmaSet)               │
                   │   - Parses statements               │
                   │   - Builds Program(stmts, pragmaSet)│ ← MODIFIED (D-08)
                   └────────────────────┬────────────────┘
                                        │
                                        ▼
                   ┌────────────────────────────────────┐
                   │ Interpreter.Execute(program)       │
                   │   (unchanged — pragmas never enter │
                   │    runtime ExecutionContext)       │
                   └────────────────────────────────────┘
```

For `use "@module"` imports, the same pre-scan stage runs INSIDE `ModuleLoader.LoadModule` against the imported file's source — that file's PragmaSet stays attached to that file's parse only. Closes Pitfall 4 structurally (D-06 / D-10).

### Recommended File Layout

```
flow-lang/
└── Lexing/
    ├── PragmaScanner.cs       # new: pre-lex extraction
    ├── PragmaSet.cs            # new: PragmaSet + PragmaDeclarationSite records
    ├── PragmaRegistry.cs       # new: closed-set + Levenshtein did-you-mean
    ├── SimpleLexer.cs          # modified: ctor + TryParseNote
    ├── Token.cs                # modified: additive OriginalText field
    └── TokenType.cs            # unchanged
flow-lang/
└── Parsing/
    ├── Parser.cs               # modified: ctor + _pragmaSet field
    ├── Parser.NoteStream.cs    # unchanged signature; reads _pragmaSet implicitly
    └── ...
flow-lang/
└── Ast/
    └── Program.cs              # modified: + PragmaSet Pragmas field
flow-lang/
├── Core/FlowEngine.cs          # modified: pre-scan call before lexer
└── Runtime/ModuleLoader.cs     # modified: pre-scan call before lexer
```

### Pattern 1: Pre-Lex Source Transformation (D-04)

**What:** PragmaScanner returns a transformedSource that is byte-for-byte identical to original except pragma regions are replaced with spaces (preserving `\n`, `\r\n`).

**When to use:** Any source-level transformation that needs to remain invisible to downstream lex/parse positional info. (Standard precedent: C preprocessor `#line` directives, Haskell pragma stripping in GHC.)

**Example (CITED: existing `SimpleLexer.SkipWhitespaceAndComments` at SimpleLexer.cs:798 — same approach to whitespace-equivalence):**

```csharp
// PragmaScanner.cs (sketch)
public static (PragmaSet Pragmas, string TransformedSource) Scan(
    string source,
    string? fileName,
    ErrorReporter errors)
{
    var sb = new StringBuilder(source.Length);    // rebuild as we walk
    var enabled = new HashSet<string>(StringComparer.Ordinal);
    var sites = new List<PragmaDeclarationSite>();
    int line = 1, col = 1, i = 0;
    bool prefixDone = false;     // flips true on first non-pragma non-blank non-comment line

    while (i < source.Length)
    {
        // Snap to start of line; consume leading whitespace tracking col
        int lineStart = i;
        // Walk to end-of-line OR end-of-source, decide what kind of line this is.
        // ...

        // If this line is `enable IDENT;` (with optional surrounding whitespace
        // and trailing comment) and NOT prefixDone:
        //   - record (IDENT, SourceLocation)
        //   - validate IDENT against PragmaRegistry; emit error w/ did-you-mean
        //     if unknown (D-12) but STILL include in transformedSource as
        //     spaces (so positions align even on errors)
        //   - replace this line in sb with ' ' chars + the original newline
        //
        // If this line is comment or blank and NOT prefixDone:
        //   - copy verbatim
        //
        // Otherwise: prefixDone = true; copy verbatim.
        //
        // After prefixDone, if we encounter `enable IDENT;` later, emit D-11
        // error + copy verbatim (lexer will then see literal `enable` and either
        // tokenize as Identifier or — depending on grammar — emit a more local
        // syntax error; but the FIRST diagnostic is the high-quality D-11).
    }

    return (new PragmaSet(enabled, sites), sb.ToString());
}
```

**Critical implementation detail:** The "this line is `enable IDENT;`" check must tolerate:
- leading whitespace (space, tab) before `enable`
- whitespace between `enable` and `IDENT`
- whitespace between `IDENT` and `;`
- a trailing `// comment` after `;`
- a trailing comment `// comment\n`

But it must NOT match `enable` as a substring — `enableThing` is NOT a pragma. The match is anchored: `^[ \t]*enable[ \t]+([A-Za-z_][A-Za-z0-9_]*)[ \t]*;[ \t]*(//[^\n]*)?[ \t]*$` per line (or the equivalent state machine).

### Pattern 2: Constructor-Threaded Optional Configuration

**What:** Add `PragmaSet pragmaSet` as a constructor parameter on `Parser` and `SimpleLexer`, with a default value `PragmaSet.Empty` for backward compatibility.

**When to use:** Adding parse-time configuration that must reach deep code paths without global state. Established by existing `Parser(List<Token>, ErrorReporter)` pattern.

**Example:**

```csharp
public partial class Parser
{
    private readonly List<Token> _tokens;
    private readonly ErrorReporter _errorReporter;
    private readonly PragmaSet _pragmaSet;   // NEW
    // ...

    public Parser(List<Token> tokens, ErrorReporter errorReporter, PragmaSet? pragmaSet = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _pragmaSet = pragmaSet ?? PragmaSet.Empty;
    }
}
```

The default-null fallback minimizes blast radius on the ~16 existing `new Parser(...)` call sites — only flow-lang's own FlowEngine and ModuleLoader need to thread an actual PragmaSet; all tests and the LSP can keep passing zero pragmas (which is correct — they were testing parse without pragmas).

### Pattern 3: Token Additive Metadata (D-15)

**What:** Add an optional `string? OriginalText` field to the `Token` record. When the lexer canonicalizes (e.g., `H4q` → `B4q`), `Text="B4q"` and `OriginalText="H4q"`. For 99.9% of tokens, `OriginalText` stays null (no canonicalization happened).

**Example:**

```csharp
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null,
    string? OriginalText = null)   // NEW — null means "Text is original"
{
    public string DiagnosticText => OriginalText ?? Text;

    public override string ToString()
    {
        if (Value != null)
            return $"{Type}('{Text}', {Value}) at {Location}";
        return $"{Type}('{Text}') at {Location}";
    }
}
```

This is the cleanest of the three options the orchestrator cited:
- **(a) optional second string field on Token** — RECOMMENDED. Records support optional positional/named parameters; existing call sites unchanged; the field defaults to null for the 67-ish `new Token(...)` sites that don't canonicalize. Memory cost: 8 bytes per token (null reference) — negligible.
- **(b) separate `OriginalText` map keyed by Token** — over-engineered; adds a Dictionary lookup at every diagnostic site; risks GC retention bugs.
- **(c) `NoteLiteral` subtype that carries both** — would require dispatching on Token type at every consumption site (Parser, ChordParser, NoteStreamCompiler); large blast radius for tiny payoff.

### Anti-Patterns to Avoid

- **Threading PragmaSet through ExecutionContext / runtime stack frames** — explicitly forbidden by Pitfall 4. Pragmas are PARSE-TIME only. Once parsing finishes, PragmaSet lives only on the Program AST node (D-08) and on Token instances that recorded their original text. Runtime never reads it.
- **Reusing `MusicalContext`'s push/pop stack for pragmas** — Pitfall 4 anti-pattern. MusicalContext is a runtime concept (tempo/key/timesig change inside `{ ... }` blocks). Pragmas are parse-time, file-scoped, no nesting.
- **Stripping pragma lines down to nothing (zero chars)** — would shift all subsequent line numbers. D-04 explicitly requires equivalent-length whitespace WITH the original newline preserved. Don't take the shortcut.
- **Using `string.Replace("enable", "       ")`** — would corrupt any string literal or identifier that contains the substring `enable`. Always anchor at line start, lex the line, only blank out whole-line pragma matches.
- **Detecting "first non-pragma statement" inside the Parser** — by D-04 the pragma lines are gone before the Parser runs, so the Parser sees a normal token stream. The "after-first-statement enable" detection (D-11) belongs INSIDE PragmaScanner, where we still have the line-level view of the source.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pragma name → docstring registry | Inheritable polymorphic Pragma class hierarchy | Static `Dictionary<string, PragmaDoc> Pragmas` (a la BuiltInDocs 104-entry pattern) | Pragmas are data not behavior; closed-set design demands a single static table. Pitfall 4 §"How to avoid" point 4 mandates this. |
| Levenshtein did-you-mean | A new C# library dependency | Inline ~25-line Wagner-Fischer DP algo in `PragmaRegistry.cs` | Standard textbook algorithm; only invoked on error (cold path); pure stdlib; deterministic. See §Levenshtein Implementation below. |
| Parsing the `enable IDENT;` line | Reusing `SimpleLexer.Tokenize` on a single-line slice | Manual ~50-line state machine inside `PragmaScanner.Scan` | The pragma line grammar is trivial (whitespace, `enable`, IDENT, `;`, optional `// comment`). Reusing SimpleLexer would create a circular dependency (lexer needs PragmaSet, but pre-scan needs lexer). Manual scan keeps the dependency arrow one-way. |
| Did-you-mean threshold tuning | A configurable distance + algorithm parameter | Hard-coded `distance ≤ max(2, name.Length / 3)` rule | Composer-friendly, simple, matches user expectation: "off by 1-2 chars → suggest". Avoids over-aggressive suggestions for short names like `hi`. |

**Key insight:** Pragma extraction is a tiny, well-bounded problem. Hand-rolled C# is the right answer; new dependencies would be net-negative.

## Runtime State Inventory

This phase is a **greenfield** code addition (new files + targeted edits). It is NOT a rename/refactor/migration phase — no existing renamed strings, no datastores keyed by old names, no service configurations to update.

**Stored data:** None — pragmas live only in parse-time data structures, never persisted.
**Live service config:** None — flow-lang is a single-binary CLI; no n8n/Datadog/Cloudflare equivalents.
**OS-registered state:** None — no scheduled tasks, no systemd units, no pm2 processes.
**Secrets/env vars:** None — no env vars introduced or renamed.
**Build artifacts:** Adding new files under `flow-lang/Lexing/` will be picked up automatically by the .NET 10 build (csproj uses globbing). No stale .egg-info / compiled-binary equivalents to worry about. **One verification:** after adding `Pragma*.cs` files, confirm `dotnet build` finds them by spot-checking the .NET project glob — `flow-lang/flow-lang.csproj` should not have explicit per-file `<Compile Include>` entries that would silently exclude them.

## Common Pitfalls

### Pitfall A: Pragma leak via `use` (PITFALLS.md §Pitfall 4)
**What goes wrong:** Treating pragmas as runtime state on `ExecutionContext` causes them to inherit the existing "imports execute in caller's context" pattern, propagating across `use` boundaries.

**Why it happens:** Naive convenience pattern — `MusicalContext` is a stack with push/pop scoping; reusing this for pragmas means "pragmas push on use, never pop" because `use` has no closing brace.

**How to avoid:** Pragmas live ONLY in `PragmaSet` returned by the parse-time `PragmaScanner`. ExecutionContext does NOT carry a pragma field. Each `use`-imported file runs its own `PragmaScanner` per D-06. Test fixture: `tests/test_pragma_isolation.flow` imports a module that declares `enable hAsB;` and asserts `H4q` in the importer is a parse error (because importer never declared the pragma).

**Warning signs:**
- Two-file fixture where importer's parse semantics changes after `use` is added.
- Determinism contract on `tutorial.flow` / `showcase.flow` breaks because some stdlib `.flow` file declares a pragma and the parse output now varies.
- LSP completion suggests `H` notes in a file that doesn't have the alias enabled.

### Pitfall B: Pre-scan corrupts string literals containing "enable"
**What goes wrong:** A naive `string.Replace("enable ", "       ")` would corrupt source like `(print "Did you enable hAsB?")` by zeroing the substring inside a quoted string.

**Why it happens:** Pragma extraction needs awareness of string-literal context (and arguably block-comment context if Flow had them — it doesn't, only `//`).

**How to avoid:** Walk the source line-by-line. For each line, determine if the line MATCHES the anchored pattern `^[ \t]*enable\s+IDENT\s*;\s*(//.*)?$` after string/comment stripping is unnecessary BECAUSE Flow strings and `//` comments cannot span lines (verified: `SimpleLexer.cs` scans `"..."` strings within a single token call but the source has no triple-quote / multiline-string syntax; `//` is line-comment only). Therefore: anchored line match is sufficient — IF the line matches the pattern, it is a pragma; otherwise it is normal content.

**Edge case:** A user could write `enable hAsB;` followed by trailing junk like `enable hAsB;  not a comment`. Decision: this should be a parse error (NOT silently accepted). The PragmaScanner's regex / state machine should require the line to END with `;` + optional `// comment`. Anything else makes it not-a-pragma → it's real source code that the lexer will then process (with whatever syntax error follows).

**Verified-clean call:** Pre-landing collision grep `git grep -wn 'enable' -- '*.flow'` returned EMPTY [VERIFIED: ran in this session] — no string-literal collisions to worry about today.

### Pitfall C: Bare `H` is currently scannable as Identifier (DEFER-02/03 acceptance)
**What goes wrong:** D-13 says bare `H` (no octave digit) MUST stay an Identifier so `Int H = 5;` keeps compiling. If we naively make `TryParseNote` accept any `H`-prefixed string when `hAsB` is on, `H` alone could become a NoteLiteral with no octave — which would either crash NoteType.Parse or produce a malformed token.

**Why it happens:** The existing `TryParseNote` (SimpleLexer.cs:706) already has the protection: `if (text.Length == 1) return false;` — bare single letters are Identifiers, never notes. We must preserve this for H — i.e., only accept `H` when followed by something else (octave digit OR accidental).

**How to avoid:** When `pragmaSet.Has("hAsB")` and `text` starts with `H`:
1. If `text.Length == 1` → return false (Identifier) — same as current bare-letter rule.
2. Else → substitute `H` with `B` to form a probe text, and run the existing `NoteType.Parse` on the probe. If it succeeds, emit `Token(NoteLiteral, Text=probe (B-canonical), OriginalText=text (H-original))`.

This means `H4q` becomes Token(NoteLiteral, "B4q", originalText="H4q"), but bare `H` falls through unchanged and remains an Identifier.

**Verified-clean call:** Pre-landing collision grep for any existing `H` identifier in `.flow` files returned EMPTY [VERIFIED: ran in this session]. No existing `Int H = 5;` or similar `H`-as-variable in the corpus today. Future-user-guard remains important regardless.

### Pitfall D: `TryParseNote` with note-suffix-stripping path
**What goes wrong:** SimpleLexer.cs:651-665 has a special path for `NoteLiteral + duration suffix` that retries `TryParseNote(text[..^1])` after stripping the last char (`w/h/q/e/s/t`). This means `H4q` enters the lexer as a single identifier `H4q`, fails the chord-symbol check, fails the no-suffix `TryParseNote("H4q")` (under existing rules), then has the `q` stripped, retries `TryParseNote("H4")`, and that's where the H acceptance must work.

**Why it happens:** The existing token-fragment logic glues the duration-suffix into the identifier before re-classifying. If we add H-acceptance only at the outermost `TryParseNote`, we'll handle `H4` directly but break for `H4q` because the OUTER call sees `H4q` and rejects it (since `NoteType.Parse("B4q")` would fail — `q` isn't a valid alteration character).

**How to avoid:** The H→B substitution must happen at the INNER `TryParseNote(notePartText, ...)` call too — i.e., inside `TryParseNote`, gate the whole letter-acceptance check on `pragmaSet.Has("hAsB") || firstChar != 'H'`. The cleanest implementation: at the TOP of `TryParseNote`, before `NoteType.Parse`, if `firstChar == 'H'` and `pragmaSet.Has("hAsB")`, compute `var probe = "B" + text[1..];` and run `NoteType.Parse(probe)` on the probe. If it succeeds, set `noteValue = probe` (canonical-text).

The OuterCall path (D-15) is then handled at the call site — `ScanIdentifierOrKeyword` builds the Token and the only edit needed is to pass `originalText=text` when text differed from the canonical.

### Pitfall E: ChordParser sees `H` first under D-16 (NOTE-STREAM-CONTEXT ONLY)
**What goes wrong:** SimpleLexer.cs:637 dispatches `ChordParser.IsChordSymbol(text)` BEFORE `TryParseNote(text)`. If a user wrote `Hmaj7` outside a note stream, ChordParser would currently reject it (it uses `s/f` not `b/#` — see existing comment at SimpleLexer.cs:631-636 — and `Hmaj7` is also not B-rooted). Per D-16, we want `Hmaj7` to remain unchanged — i.e., `ChordParser.IsChordSymbol("Hmaj7")` should still return false, and the token falls through to `TryParseNote("Hmaj7")`.

`TryParseNote("Hmaj7")` under the new H-acceptance would compute probe `"Bmaj7"` and run `NoteType.Parse("Bmaj7")` — does that succeed? If so, we'd have a regression: `Hmaj7` would become a NoteLiteral `Bmaj7` instead of staying an Identifier. **This is the failure mode D-16 forbids.**

**How to avoid:** `NoteType.Parse` accepts shapes like `letter [accidental]* [octave-digit]+`. `Bmaj7` doesn't fit (`maj` is not a sequence of `b/#/+/-` accidentals followed by digits). So `NoteType.Parse("Bmaj7")` already fails [ASSUMED — must verify with a unit Fact]. If it fails, the rejection is automatic. If it succeeds (unlikely but possible if NoteType.Parse is permissive), an additional gate is needed.

**Mitigation Fact:** A new test in `tests/test_h_alias.flow` demonstrating `Int Hmaj7 = 0;` outside a note stream still parses (or `Hmaj7` outside `| ... |` doesn't parse as a note literal). Plus a `PragmaH_AliasFacts.cs` Fact: `WithHAsB_HmajPaul_OutsideNoteStream_StaysIdentifier` runs `enable hAsB;\nInt Hmaj7 = 0;\n` and asserts no parse errors and Hmaj7 is treated as an identifier.

**Note:** D-16 also says "inside `| ... |`, chord brackets like `[H4 D#5 F#5]q` work because their inner notes go through the same H-aliasing `TryParseNote` path". This is automatic — chord-bracket inner notes are fed through the same NoteLiteral tokens — so H-acceptance flows through cleanly.

### Pitfall F: Determinism contract on existing tutorial.flow / showcase.flow
**What goes wrong:** Phase 21 changes the parse pipeline for ALL files (the new pre-scan stage runs on every parse). Even when a file declares no pragmas, every byte must round-trip identically. If `PragmaScanner.Scan` accidentally changes a non-pragma source (e.g., trims a trailing newline, normalizes line endings, eats a tab), the byte-identical determinism contract on `examples/tutorial.flow` + `examples/showcase.flow` breaks.

**How to avoid:**
1. The default-empty path through `PragmaScanner.Scan` must return `(PragmaSet.Empty, source)` — the SAME string instance, not a copied one — when the prefix contains zero `enable` statements. (i.e., if no pragma was found, do NOT return a StringBuilder copy; return the original `source` reference.) This makes the no-pragma path a literal pass-through.
2. The two existing byte-identical Facts (`ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs`) MUST stay green throughout Phase 21 development. Run them after every commit on the phase branch. They are the headline regression gate.

### Pitfall G: Whitespace-equivalence preserves NEWLINE EXACTLY (D-04)
**What goes wrong:** Both `\n` and `\r\n` line endings exist in the wild. If PragmaScanner replaces a pragma line with spaces and a `\n` but the original line ended with `\r\n`, subsequent character offsets are off by one for every line after.

**How to avoid:** When stripping a pragma line, copy spaces for every non-newline character, then preserve the EXACT trailing newline sequence (`\n`, `\r\n`, or end-of-source). Implementation pattern: scan from `lineStart` to `lineEnd` (inclusive of `\n` if present); for `lineEnd-1` characters preceding `\n`, emit space; emit the `\n` (and the preceding `\r` if present) verbatim.

Cross-platform note: `examples/showcase.flow` and `examples/tutorial.flow` are committed with `\n` (LF) only (verified by inspection of normal Linux dev workflow). Tests/CI machines respect this — adding a CRLF test fixture is unnecessary unless we're paranoid.

## Code Examples

### PragmaSet record (D-02)

```csharp
// flow-lang/Lexing/PragmaSet.cs
namespace FlowLang.Lexing;

using FlowLang.Core;

/// <summary>
/// Per-file pragma extraction result. Closed-set membership defined by PragmaRegistry.
/// </summary>
public sealed record PragmaSet(
    IReadOnlySet<string> Enabled,
    IReadOnlyList<PragmaDeclarationSite> Sites)
{
    public static readonly PragmaSet Empty = new(
        new HashSet<string>(StringComparer.Ordinal),
        Array.Empty<PragmaDeclarationSite>());

    public bool Has(string pragmaName) => Enabled.Contains(pragmaName);
}

public sealed record PragmaDeclarationSite(string Name, SourceLocation Location);
```

**Decision: `record` not `record struct`.** Although the orchestrator left this to discretion, `PragmaSet` is referenced by `Program` (heap object) and by `Parser` (heap object), and is created at most a handful of times per parse session. The struct copy semantics would be a micro-pessimization, and the `IReadOnlySet<string>` reference field already forces heap allocation for the underlying HashSet. Keep it as a class-record.

### PragmaRegistry (D-12, D-17)

```csharp
// flow-lang/Lexing/PragmaRegistry.cs
namespace FlowLang.Lexing;

public static class PragmaRegistry
{
    /// <summary>
    /// Closed set of recognized pragma names. Phase 21 ships only "hAsB".
    /// Phase 23 will add justIntonation/pythagorean/equalTemperament.
    /// Phase 24 will add scaleLint.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation)."
        };

    public static bool IsKnown(string name) => KnownPragmas.ContainsKey(name);

    /// <summary>Returns the alphabetized csv of known pragma names for D-12 errors.</summary>
    public static string AlphabetizedKnownNames() =>
        string.Join(", ", KnownPragmas.Keys.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// Wagner-Fischer Levenshtein. Returns the closest known pragma name within distance
    /// max(2, typed.Length/3), or null if no candidate is close enough.
    /// </summary>
    public static string? SuggestNearest(string typed)
    {
        if (string.IsNullOrEmpty(typed)) return null;
        int threshold = Math.Max(2, typed.Length / 3);
        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var name in KnownPragmas.Keys)
        {
            int d = LevenshteinDistance(typed, name);
            if (d <= threshold && d < bestDist)
            {
                bestDist = d;
                best = name;
            }
        }
        return best;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        // Wagner-Fischer DP. Pure-stdlib implementation.
        int n = a.Length, m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}
```

[CITED: standard Wagner-Fischer DP — Levenshtein, Vladimir I. (1966), "Binary codes capable of correcting deletions, insertions, and reversals"]

### PragmaScanner.Scan skeleton (D-01, D-03, D-04, D-11, D-12)

```csharp
// flow-lang/Lexing/PragmaScanner.cs
namespace FlowLang.Lexing;

using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;

public static class PragmaScanner
{
    public static (PragmaSet Pragmas, string TransformedSource) Scan(
        string source,
        string? fileName,
        ErrorReporter errors)
    {
        if (string.IsNullOrEmpty(source))
            return (PragmaSet.Empty, source ?? string.Empty);

        // Quick check: if "enable" doesn't appear anywhere in source, return original
        // unchanged (zero-allocation fast path — preserves byte-identical determinism for
        // every pre-Phase-21 .flow file).
        if (source.IndexOf("enable", StringComparison.Ordinal) < 0)
            return (PragmaSet.Empty, source);

        var enabled = new HashSet<string>(StringComparer.Ordinal);
        var sites = new List<PragmaDeclarationSite>();
        SourceLocation? firstNonPragmaLineLoc = null;
        string? firstNonPragmaLineSummary = null;
        var sb = new StringBuilder(source.Length);

        int i = 0, line = 1, col = 1;
        bool prefixDone = false;

        while (i < source.Length)
        {
            // Find the end of the current line
            int lineStart = i;
            while (i < source.Length && source[i] != '\n') i++;
            int lineEndExclNewline = i;
            int lineEndInclNewline = (i < source.Length) ? i + 1 : i;
            string lineText = source.Substring(lineStart, lineEndExclNewline - lineStart);

            var pragmaMatch = TryMatchPragmaLine(lineText);
            // Returns: { Name, NameStartCol, NameEndCol } or null

            bool isBlank = string.IsNullOrWhiteSpace(lineText);
            bool isLineComment = lineText.TrimStart().StartsWith("//", StringComparison.Ordinal);

            if (pragmaMatch != null && !prefixDone)
            {
                // Validate against PragmaRegistry (D-12)
                var name = pragmaMatch.Name;
                var nameLoc = new SourceLocation(line, pragmaMatch.NameStartCol, fileName);
                if (!PragmaRegistry.IsKnown(name))
                {
                    var sugg = PragmaRegistry.SuggestNearest(name);
                    var msg = $"unknown pragma '{name}' at line {line}. " +
                              (sugg != null ? $"Did you mean '{sugg}'? " : "") +
                              $"Known pragmas: {PragmaRegistry.AlphabetizedKnownNames()}.";
                    errors.ReportError(msg, nameLoc);
                    // continue scanning — accumulate other errors per CLAUDE.md "error accumulation"
                }
                else
                {
                    // D-09: duplicate is silent (set semantics)
                    enabled.Add(name);
                    sites.Add(new PragmaDeclarationSite(name, nameLoc));
                }

                // D-04: replace the pragma line with equivalent-length whitespace
                for (int k = lineStart; k < lineEndExclNewline; k++) sb.Append(' ');
                if (lineEndInclNewline > lineEndExclNewline) sb.Append('\n');
            }
            else if (pragmaMatch != null && prefixDone)
            {
                // D-11: pragma after first non-pragma statement
                var nameLoc = new SourceLocation(line, pragmaMatch.NameStartCol, fileName);
                errors.ReportError(
                    $"'enable {pragmaMatch.Name};' at line {line}: pragmas must appear " +
                    $"before any other statement. First non-pragma statement was at " +
                    $"line {firstNonPragmaLineLoc!.Value.Line} ({firstNonPragmaLineSummary}). " +
                    $"Move the pragma to the top of the file.",
                    nameLoc);
                // Still strip the line (so subsequent lex/parse doesn't double-error on `enable`)
                for (int k = lineStart; k < lineEndExclNewline; k++) sb.Append(' ');
                if (lineEndInclNewline > lineEndExclNewline) sb.Append('\n');
            }
            else
            {
                // Blank, comment, or normal source line — copy verbatim
                sb.Append(source, lineStart, lineEndInclNewline - lineStart);
                if (!prefixDone && !isBlank && !isLineComment)
                {
                    prefixDone = true;
                    firstNonPragmaLineLoc = new SourceLocation(line, 1, fileName);
                    firstNonPragmaLineSummary = lineText.Trim();
                    if (firstNonPragmaLineSummary.Length > 40)
                        firstNonPragmaLineSummary = firstNonPragmaLineSummary[..37] + "...";
                }
            }

            line++;
            i = lineEndInclNewline;
        }

        return (new PragmaSet(enabled, sites), sb.ToString());
    }

    private record PragmaLineMatch(string Name, int NameStartCol, int NameEndCol);

    private static PragmaLineMatch? TryMatchPragmaLine(string lineText)
    {
        // Match: ^[ \t]*enable[ \t]+IDENT[ \t]*;[ \t]*(// any)?$
        // 1-based column tracking.
        int p = 0;
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p + 6 > lineText.Length) return null;
        if (string.CompareOrdinal(lineText, p, "enable", 0, 6) != 0) return null;
        p += 6;
        // Require at least one whitespace after "enable"
        if (p >= lineText.Length || (lineText[p] != ' ' && lineText[p] != '\t')) return null;
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        // Identifier
        int identStart = p;
        if (p >= lineText.Length || !(char.IsLetter(lineText[p]) || lineText[p] == '_')) return null;
        while (p < lineText.Length && (char.IsLetterOrDigit(lineText[p]) || lineText[p] == '_')) p++;
        int identEnd = p;
        string ident = lineText.Substring(identStart, identEnd - identStart);
        // Optional whitespace, then ';'
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p >= lineText.Length || lineText[p] != ';') return null;
        p++;
        // Optional trailing whitespace, optional // comment to end of line
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p < lineText.Length)
        {
            // Must be the start of a // comment, otherwise the line has trailing junk
            // and is NOT a pragma (let the lexer handle it).
            if (p + 1 < lineText.Length && lineText[p] == '/' && lineText[p + 1] == '/')
                return new PragmaLineMatch(ident, identStart + 1, identEnd + 1);  // +1: 1-based col
            return null;
        }
        return new PragmaLineMatch(ident, identStart + 1, identEnd + 1);
    }
}
```

### SimpleLexer.TryParseNote modification (D-13, D-14)

```csharp
// In SimpleLexer.cs
public class SimpleLexer
{
    private readonly PragmaSet _pragmaSet;   // NEW

    public SimpleLexer(string source, ErrorReporter errorReporter, string? fileName = null,
                       PragmaSet? pragmaSet = null)   // NEW: optional, default Empty
    {
        // ...
        _pragmaSet = pragmaSet ?? PragmaSet.Empty;
    }

    private bool TryParseNote(string text, out string noteValue)
    {
        noteValue = text;
        if (text.Length == 0) return false;

        char firstChar = text[0];

        // === D-13: H→B substitution under hAsB pragma ===
        if (firstChar == 'H' && _pragmaSet.Has("hAsB") && text.Length > 1)
        {
            var probe = "B" + text[1..];   // canonical
            try
            {
                var (note, octave, alteration) = NoteType.Parse(probe);
                noteValue = probe;   // canonical text returned (B4q etc.)
                return true;
            }
            catch
            {
                // Fall through — H4xyz that doesn't parse as B4xyz isn't a note
            }
            // IMPORTANT: do NOT fall through to the standard A-G check below — we already
            // know firstChar is 'H', and 'H' is outside [A, G], so the standard path would
            // reject it. The probe attempt above is the only acceptance path for H.
            return false;
        }

        // Standard A-G acceptance (unchanged)
        if (firstChar < 'A' || firstChar > 'G') return false;
        if (text.Length == 1) return false;
        try
        {
            var (note, octave, alteration) = NoteType.Parse(text);
            noteValue = text;
            return true;
        }
        catch { return false; }
    }
}
```

### Token canonical-vs-original wiring (D-15)

The cleanest place to attach `OriginalText` is at the existing token-construction site in `ScanIdentifierOrKeyword` (SimpleLexer.cs:645):

```csharp
// At SimpleLexer.cs:645 (inside ScanIdentifierOrKeyword, after TryParseNote succeeds)
if (TryParseNote(text, out var noteValue))
{
    // D-15: When canonicalization happened (text != noteValue), preserve original
    string? originalText = (text != noteValue) ? text : null;
    return new Token(TokenType.NoteLiteral, noteValue, start, noteValue, originalText);
}
```

The mirrored note-suffix-stripping path at SimpleLexer.cs:651-665 needs the same treatment:

```csharp
if (TryParseNote(notePartText, out var notePartValue))
{
    _position--;
    _column--;
    string? originalText = (notePartText != notePartValue) ? notePartText : null;
    return new Token(TokenType.NoteLiteral, notePartValue, start, notePartValue, originalText);
}
```

The renderer / MIDI-export path consumes `Token.Text` (= canonical, B-rooted), so they need NO edits. Diagnostic emission paths that want to show the original would call `token.DiagnosticText` (the helper added on Token).

### FlowEngine integration (D-01, D-05, D-08)

```csharp
// In FlowEngine.cs:Execute (replaces lines 65-74)
public bool Execute(string source, string? fileName = null)
{
    _errorReporter.Clear();
    try
    {
        // 0. Pre-lex: extract pragmas (Phase 21)
        var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, fileName, _errorReporter);
        if (_errorReporter.HasErrors) return false;   // D-12 unknown-pragma errors halt here

        // 1. Lex transformedSource into tokens (with pragma awareness for H-alias)
        var lexer = new SimpleLexer(transformedSource, _errorReporter, fileName, pragmaSet);
        var tokens = lexer.Tokenize();
        if (_errorReporter.HasErrors) return false;

        // 2. Parse tokens into AST (with pragma set for note-stream awareness if needed)
        var parser = new Parser(tokens, _errorReporter, pragmaSet);
        var program = parser.Parse();
        if (_errorReporter.HasErrors) return false;

        // 3. (type check skipped)
        _diagnosticOutput?.WriteLine($"[verbose] Executing {fileName ?? "<eval>"}");

        // 4. Interpret AST
        _interpreter.Execute(program);
        return !_errorReporter.HasErrors;
    }
    catch (Exception ex)
    {
        _errorReporter.ReportError($"Unexpected error: {ex.Message}", SourceLocation.Unknown);
        return false;
    }
}
```

### ModuleLoader integration (D-06)

```csharp
// In ModuleLoader.cs:LoadModule (replaces lines 64-79)
var source = File.ReadAllText(resolvedPath);

// Phase 21 D-06: each imported file gets its own PragmaSet — pragmas do NOT leak
var localReporter = new Diagnostics.ErrorReporter();
var (pragmaSet, transformedSource) = Lexing.PragmaScanner.Scan(source, resolvedPath, localReporter);
if (localReporter.HasErrors)
{
    _diagnosticOutput?.WriteLine($"[verbose] Failed to pre-scan module: {resolvedPath}");
    _errorReporter.ReportError(
        $"Module '{resolvedPath}' has invalid pragma declarations.", errorLocation);
    return ModuleLoadResult.Error;
}

var lexer = new Lexing.SimpleLexer(transformedSource, localReporter, resolvedPath, pragmaSet);
var tokens = lexer.Tokenize();
if (localReporter.HasErrors) { /* ... existing error path ... */ }

var parser = new Parsing.Parser(tokens, localReporter, pragmaSet);
var program = parser.Parse();
// ... rest unchanged
```

### Program AST extension (D-08)

```csharp
// flow-lang/Ast/Program.cs
namespace FlowLang.Ast;

using FlowLang.Core;
using FlowLang.Lexing;

public record Program(
    SourceLocation Location,
    IReadOnlyList<Statement> Statements,
    PragmaSet Pragmas) : AstNode(Location)
{
    // Backward-compat overload for tests / LSP that don't care about pragmas
    public Program(SourceLocation location, IReadOnlyList<Statement> statements)
        : this(location, statements, PragmaSet.Empty) { }
}
```

The Parser's `Parse()` method then becomes:
```csharp
return new Program(SourceLocation.Unknown, statements, _pragmaSet);
```

## State of the Art

This is greenfield infrastructure for flow-lang. Reference precedents:

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Pragmas treated as a runtime concept (CLAUDE.md "imports execute in caller's context") | File-scope parse-time only — never enter `ExecutionContext` | Phase 21 (this) | Closes Pitfall 4 structurally; matches Rust `#![feature(...)]` semantics |
| No pragma extraction stage in the pipeline | New pre-lex `PragmaScanner.Scan` runs before `SimpleLexer` | Phase 21 (this) | Adds one ~150-line file to the pipeline; fast-path for no-pragma source means zero-allocation overhead for legacy files |
| `Token.Text` is the only text representation | `Token.Text` (canonical) + `Token.OriginalText` (verbatim, optional) | Phase 21 (this) | Diagnostic UX preserves what the composer wrote; renderer keeps consuming canonical text |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `NoteType.Parse("Bmaj7")` fails (returns/throws), so `Hmaj7` outside note streams stays an Identifier per D-16 | Pitfall E | If NoteType.Parse is permissive enough to accept `Bmaj7`, `Hmaj7` outside `\| ... \|` would regress to a NoteLiteral. Mitigation: **a unit Fact** in Plan 21-02 explicitly verifies `NoteType.Parse("Bmaj7")` fails; if it doesn't, add a positive guard ("text must end with valid alteration+digit shape") before substitution. |
| A2 | `PragmaScanner` running on every parse adds < 1ms overhead even for the largest file in `tests/` | §Pitfall F | If overhead is measurable, REPL feel could degrade. Mitigation: the fast-path (no `enable` substring → return original source) covers ~100% of legacy files. Real-world risk: ~zero. |
| A3 | All `.flow` source files in the repo today use `\n` line endings (no `\r\n`) | §Pitfall G | If any file uses CRLF, the equivalent-length-whitespace replacement could shift offsets by one per line. Mitigation: PragmaScanner preserves the original `\r\n` sequence verbatim (don't normalize). |
| A4 | The 16-ish `new Parser(...)` and `new SimpleLexer(...)` call sites can keep their current signatures via default-null PragmaSet param | §Pattern 2 | If a caller relies on positional ordering of constructor args, adding a trailing optional arg is binary-compatible but source-breaking. Mitigation: the new param is the LAST one and defaults to null. All existing call sites compile unchanged. [VERIFIED via grep: none of the 16 sites use named-args to argue against this] |

## Open Questions

1. **Should the LSP (`flow-lsp/ParseSession.cs`) participate in Phase 21?**
   - What we know: LSP currently calls `new SimpleLexer(source, er, path)` and `new Parser(tokens, er)`. Default-null PragmaSet means LSP keeps working without pragma awareness — it'll still accept H-as-Identifier (correct fallback), but won't surface pragma diagnostics.
   - What's unclear: Whether Phase 21 SHOULD update the LSP to call PragmaScanner too, so users typing `enable hAsB;` in VSCode get D-11/D-12 diagnostics.
   - Recommendation: **Out of scope per CONTEXT.md** ("LSP integration of pragma diagnostics — flow-lsp work, not flow-lang"). The LSP keeps using PragmaSet.Empty until a future phase. Document this as a deliberate gap.

2. **Should `flow-editor/Editor/ScopeColorizer.cs` and `flow-editor/Editor/FlowSyntaxHighlighter.cs` thread PragmaSet?**
   - What we know: These are syntax-highlight components, not full parsers. They'll see `H4q` as a NoteLiteral only if the lexer is given the pragma — without the pragma, `H4q` stays an Identifier (the user's editor shows it as a regular identifier, not a note color).
   - What's unclear: Whether composer expects highlight to update based on `enable hAsB;` at top of file.
   - Recommendation: Same as above — treat as flow-editor follow-up work, NOT Phase 21 scope. The codepath compiles cleanly with default PragmaSet.Empty.

3. **Should PragmaScanner emit a structured `PragmaScanResult` that includes both errors AND the partial PragmaSet, or rely on ErrorReporter side-channel?**
   - What we know: The existing pipeline uses `ErrorReporter` accumulator pattern (CLAUDE.md error accumulation principle). PragmaScanner.Scan takes ErrorReporter, accumulates errors via it, and still returns whatever pragmas it managed to parse.
   - What's unclear: None — the current sketch already follows the project convention. No question.

## Environment Availability

This phase has no external dependencies — pure C# stdlib + project-internal types. Skipped.

## Validation Architecture

> Skip condition checked: `workflow.nyquist_validation: true` in `.planning/config.json` — section IS required.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing flow-lang.Tests project) [VERIFIED: ls .Tests/Unit/Phase19/*.cs shows xUnit Facts] |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21"` |
| Full suite command | `dotnet test` |
| Integration script harness | `tests/test_*.flow` files run via `dotnet run --project flow-interpreter <path>`; success = exit 0 (no errors emitted) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PRAG-01 | `enable hAsB;` recognized at top of file | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts.EnableHAsB_AtTop_Recognized"` | Wave 0 |
| PRAG-01 | Pragma after first non-pragma stmt → parse error | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts.EnableAfterStatement_RaisesError"` | Wave 0 |
| PRAG-01 | Comments + blank lines OK in prefix region | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts.PrefixCommentsAndBlanks_Allowed"` | Wave 0 |
| PRAG-01 | Pragma lines stripped to whitespace preserve line numbers | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts.LineNumbersAlignAfterStrip"` | Wave 0 |
| PRAG-01 | Unknown pragma → error citing known list + did-you-mean | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryFacts.Unknown_RaisesError_WithSuggestion"` | Wave 0 |
| PRAG-01 | Levenshtein suggests `hAsB` for typed `hasb` / `hAsBb` / `HAsB` | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryFacts.SuggestNearest_FindsClose"` | Wave 0 |
| PRAG-01 | Duplicate `enable hAsB; enable hAsB;` is silent (no error) | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts.Duplicate_Silent"` | Wave 0 |
| PRAG-02 | Importing module that declares `enable hAsB;` does NOT enable hAsB in importer | integration | `dotnet test --filter "FullyQualifiedName~PragmaIsolationFacts"` + `tests/test_pragma_isolation.flow` | Wave 0 |
| DEFER-02/03 | `enable hAsB; \| H4q B4q \|` → both notes pitch-equal, byte-identical render | integration | `dotnet test --filter "FullyQualifiedName~HAliasFacts.HMatchesB_InNoteStream"` | Wave 0 |
| DEFER-02/03 | `Hb4q` / `H#4q` / `Hb4+50c` / `H4q.` / `H4h~` all parse identically to B-equivalents | unit | `dotnet test --filter "FullyQualifiedName~HAliasFacts.FullCoverage"` | Wave 0 |
| DEFER-02/03 | Without `enable hAsB;`, `H4q` inside note stream → parse error | unit | `dotnet test --filter "FullyQualifiedName~HAliasFacts.WithoutPragma_HRejected"` | Wave 0 |
| DEFER-02/03 | Outside note streams, `Int H = 5;` continues to compile (with OR without hAsB) | integration | `tests/test_h_identifier.flow` (asserts no parse errors) | Wave 0 |
| DEFER-02/03 | `Hmaj7` outside `\| ... \|` does NOT parse as a chord literal even with hAsB on (D-16) | unit | `dotnet test --filter "FullyQualifiedName~HAliasFacts.HmajOutsideNoteStream_Unchanged"` | Wave 0 |
| DEFER-02/03 | Token from `H4q` has Text="B4q" canonical AND OriginalText="H4q" (D-15) | unit | `dotnet test --filter "FullyQualifiedName~HAliasFacts.Token_PreservesOriginalText"` | Wave 0 |
| Determinism | Existing `tutorial.flow` + `showcase.flow` two-runs cmp-clean | integration | existing `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` (must stay green) | EXISTS |
| Determinism | All ~31 existing `tests/test_*.flow` scripts pass byte-identically when no pragmas active | integration | `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` | EXISTS |

### Sampling Rate

Per Phase 19/20 precedent and `nyquist_validation: true`:

- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21"` (~5–15s; covers all 14 Phase 21 unit Facts)
- **Per wave merge:** `dotnet test` (full suite, ~30–60s; catches regression in Phase 14/19/20 Facts)
- **Phase gate:** Full `dotnet test` green + integration loop over all `tests/test_*.flow` (no errors) + `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` green before `/gsd-verify-work`

### Wave 0 Gaps

All Phase 21 test files are NEW. Wave 0 prerequisites:
- [ ] `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — 6+ unit Facts covering pre-scan algorithm, prefix accept, top-of-file enforcement, line-number preservation, duplicate silence
- [ ] `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — 4+ Facts covering closed-set membership, alphabetized list, Levenshtein suggestion, IsKnown
- [ ] `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` — 7+ Facts covering H acceptance under pragma, full coverage (Hb/H#/+50c/dotted/tied), without-pragma rejection, Hmaj outside-stream unchanged, Token original-text preservation
- [ ] `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` — 1 Fact: two-file fixture verifying PRAG-02 non-propagation
- [ ] `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` — paired fixture
- [ ] `tests/test_h_alias.flow` — DEFER-02/03 acceptance script
- [ ] `tests/test_h_identifier.flow` — `Int H = 5;` continues to compile script
- [ ] **No new framework install.** xUnit + .NET test infrastructure already in place per Phase 19 / 20 precedent.

## Security Domain

> `security_enforcement` not explicit in config — treating as enabled. Phase 21 has minimal security surface (it's a parse-time feature in a single-user CLI tool).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a — Flow has no auth |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | yes | PragmaRegistry closed-set check rejects unknown names; Levenshtein hint is informational only — no execution path triggered by suggestion |
| V6 Cryptography | no | n/a |

### Known Threat Patterns for {flow-lang lexer/parser}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Crafted .flow file with thousands of pragma-shaped lines exhausting memory | DoS / Tampering | PragmaScanner is O(n) in source size; the StringBuilder grows to at most source.Length. No exponential / quadratic path. The `IndexOf("enable", ...)` fast-path means files without the substring do zero allocation. |
| Crafted pragma name that triggers Levenshtein DP on a huge string | DoS | Levenshtein DP allocates two int arrays of size `m+1` where `m` is the longest known pragma name (`hAsB` = 4 chars in Phase 21, `equalTemperament` = 16 in Phase 23). DP is bounded; not user-controlled in the dimension that matters. |
| Pragma allows a future "disable type checking" pragma loaded from arbitrary source | Tampering / Privilege escalation | PragmaRegistry is a CLOSED set in code (D-17) — unknown names error, never silently pass. Future high-impact pragmas (e.g., a hypothetical `enable noTypeCheck;`) would require an explicit code edit to PragmaRegistry, gated by code review. [PITFALLS.md §Security Mistakes row 2 captures this — explicitly designed for] |

## Project Constraints (from CLAUDE.md)

| Directive | Source line | How Phase 21 honors it |
|-----------|-------------|------------------------|
| Minimal external dependencies | "Guiding Principle: Minimal Dependencies" | No new NuGet packages. Wagner-Fischer Levenshtein hand-rolled (~25 lines). |
| .NET 10 / C# 13 | "Constraints: Runtime: .NET 10" | All new code targets net10.0; uses records, file-scoped namespaces, pattern matching. |
| AST nodes are records | "AST nodes are `record` types for immutability" | `Program` extension is record. `PragmaSet` + `PragmaDeclarationSite` are records. |
| Error accumulation, not throwing | "ErrorReporter accumulates errors rather than throwing" | PragmaScanner accumulates D-11 / D-12 errors via ErrorReporter; pre-scan stage doesn't halt on first error. Multiple unknown-pragma errors surface in one pass. |
| Module imports execute in caller's context | "Module imports execute in the caller's context — no separate scope/namespace isolation" | **Pragmas explicitly DO NOT follow this pattern** (Pitfall 4). Each imported file has its own pragma extraction. Runtime-context merge is preserved (D-06: pragmas isolate at PARSE TIME, runtime context behavior unchanged). |
| Existing .flow scripts continue to work | "Compatibility: Existing .flow scripts and test suite must continue to work" | Default `PragmaSet.Empty` for all existing parser/lexer call sites; pre-scan fast path returns input unchanged when no `enable` substring present. Byte-identical determinism gate (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`) enforces this. |
| Functional S-expression style, no infix operators | user memory: language_philosophy | `enable name;` is a statement, not an operator. No infix introduced. |
| Charitable interpretation: silent-and-documented over errors | user memory: charitable_interpretation | D-09 (duplicate enable silent), D-10 (module pragmas don't error in importer), D-15 (preserve composer's original H in diagnostics). |

## Plan Decomposition Recommendation

### Three-plan shape (matches Phase 19/20 precedent)

The orchestrator asked for "3-5 plan decomposition." Phase 21 is small enough that **3 plans** is the right count — fewer means the plumbing-and-substitution coupling becomes too heavy for one task; more means we'd be artificially splitting tightly-coupled work.

Phase 19 used 5 plans for 8 requirements. Phase 20 used 4 plans for 3 requirements (one per req + closure). Phase 21 should mirror Phase 20: separate plumbing from feature, end with closure.

#### Plan 21-01 — Pragma plumbing (PRAG-01 + PRAG-02)
**Scope:**
- Create `flow-lang/Lexing/PragmaSet.cs` (PragmaSet + PragmaDeclarationSite records)
- Create `flow-lang/Lexing/PragmaRegistry.cs` (closed-set + Wagner-Fischer Levenshtein + alphabetized known-list helper). **Phase 21 only registers `hAsB` per D-17.**
- Create `flow-lang/Lexing/PragmaScanner.cs` (Scan method with line-by-line state machine)
- Modify `flow-lang/Ast/Program.cs` (add PragmaSet field; backward-compat ctor)
- Modify `flow-lang/Parsing/Parser.cs` (add `_pragmaSet` field + ctor parameter; Parse() returns Program with pragmas)
- Modify `flow-lang/Lexing/SimpleLexer.cs` (add `_pragmaSet` field + ctor parameter — but DO NOT yet use it; that's Plan 21-02)
- Modify `flow-lang/Core/FlowEngine.cs` (call PragmaScanner.Scan before lexer; thread pragmaSet into lexer + parser)
- Modify `flow-lang/Runtime/ModuleLoader.cs` (mirror the FlowEngine wiring per D-06)
- New tests:
  - `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (6+ Facts: prefix accept, comments-and-blanks-ok, line-numbers-align, duplicate-silent, after-statement-error, no-enable-substring fast path)
  - `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` (4+ Facts: IsKnown, AlphabetizedKnownNames, SuggestNearest finds close, SuggestNearest returns null for far-away typos)
  - `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` (PRAG-02: two-file fixture)
  - `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` (paired)
- Verification: full `dotnet test` green + tutorial/showcase byte-identical Facts green + all `tests/test_*.flow` scripts run clean.

**Wave shape:** Strictly sequential (each file depends on the previous one).

**Acceptance:** PRAG-01 closed; PRAG-02 closed.

#### Plan 21-02 — `hAsB` H-alias substitution (DEFER-02/03)
**Scope:**
- Modify `flow-lang/Lexing/Token.cs` (add `string? OriginalText` field as record positional; add `DiagnosticText` helper)
- Modify `flow-lang/Lexing/SimpleLexer.cs:TryParseNote` (D-13 substitution path: H + len>1 + pragmaSet.Has("hAsB") → probe with B + run NoteType.Parse)
- Modify `flow-lang/Lexing/SimpleLexer.cs:ScanIdentifierOrKeyword` (line ~645 + ~657 — wire originalText into emitted Tokens for both the direct-note path and the duration-suffix-stripping path)
- New tests:
  - `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (7+ Facts: HMatchesB_InNoteStream, FullCoverage_HbHsharpCentDottedTied, WithoutPragma_HRejected, Token_PreservesOriginalText, BareH_StaysIdentifier, NoteType_Parse_Bmaj7_Fails [A1 verification], HmajOutsideNoteStream_Unchanged)
  - `tests/test_h_alias.flow` — runs `enable hAsB;` + a note stream with H notes + asserts identical render to a B-only version (or asserts equal MIDI output)
  - `tests/test_h_identifier.flow` — `Int H = 5;` + `(print (str H))` → expects `5\n` regardless of pragma state
- Verification: full `dotnet test` green + tutorial/showcase byte-identical Facts green + `tests/test_h_alias.flow` and `tests/test_h_identifier.flow` run clean.

**Wave shape:** Strictly sequential after Plan 21-01.

**Acceptance:** DEFER-02/03 closed.

#### Plan 21-03 — Closure
**Scope:**
- REQUIREMENTS.md PRAG-01 / PRAG-02 / DEFER-02/03 strikethrough + Traceability table update (Phase 21 → Shipped)
- ROADMAP.md Phase 21 status → Complete; Progress table update
- STATE.md Phase 21 entry: shipped commits, deferred items closed for v1.3 14-deferred and 12-deferred, Hmaj7 chord-literal captured as v1.3-out-of-scope
- VERIFICATION.md for Phase 21 (collision-grep evidence: empty for `enable`/`hAsB`/`H` per pre-landing greps documented in this RESEARCH; full test-suite output)
- 21-VALIDATION.md (per nyquist_validation: true workflow standard)

**Wave shape:** Strictly sequential after Plan 21-02.

**Acceptance:** Phase 21 closure markers committed.

### Wave-able parallelism

Plan 21-01 and Plan 21-02 cannot run in parallel — Plan 21-02 needs `_pragmaSet` to exist on SimpleLexer, which Plan 21-01 introduces. Inside Plan 21-01, the `Tests/Unit/Phase21/PragmaScannerFacts.cs` work could run in parallel with the `Tests/Unit/Phase21/PragmaRegistryFacts.cs` work (independent files, independent Facts), and the test files could be authored alongside the production code. But these are all small tasks within one plan — wave granularity isn't necessary.

### Why not split further

A four-plan shape (e.g., separating PragmaSet/PragmaRegistry from PragmaScanner) was considered and rejected: the PragmaScanner cannot be tested without PragmaRegistry (it calls IsKnown / SuggestNearest), and PragmaRegistry can't be exercised meaningfully without a scanner that consumes it. Bundling them is the smallest unit that produces a testable artifact.

A five-plan shape (separating Token D-15 from SimpleLexer.TryParseNote D-13) was considered and rejected: D-13 substitution happens at the same call site that constructs the Token, and D-15 is a one-line change at that same site. Splitting into two plans means Plan 21-02a leaves Token unchanged but SimpleLexer canonicalizes (so Token.Text="B4q" with no record of original H — a regression to D-15 semantics during the gap between 21-02a and 21-02b). Bundling them keeps Token+SimpleLexer in one atomic, reviewable unit.

## Sources

### Primary (HIGH confidence)
- **CLAUDE.md** (project root) — house style, minimal-deps philosophy, AST-record pattern, error-accumulation principle, module-imports-in-caller's-context [VERIFIED via Read]
- **CONTEXT.md** (.planning/phases/21-pragma-system-h-alias/21-CONTEXT.md) — 17 locked decisions D-01 to D-17 [VERIFIED via Read]
- **REQUIREMENTS.md** (.planning/REQUIREMENTS.md) — PRAG-01 / PRAG-02 / DEFER-02/03 acceptance + locked decision D-02 [VERIFIED via Read]
- **ROADMAP.md** (.planning/ROADMAP.md) — Phase 21 success criteria + binding pre-orderings [VERIFIED via Read]
- **PITFALLS.md** (.planning/research/PITFALLS.md) — Pitfall 4 (pragma leak via use), Pitfall 8 (lexer/parser collisions with existing tests), §Performance Traps + §Integration Gotchas + §Security Mistakes rows [VERIFIED via Read]
- **flow-lang/Core/FlowEngine.cs** — pipeline orchestration; pre-scan insertion site at line 65–74 [VERIFIED via Read]
- **flow-lang/Lexing/SimpleLexer.cs** — `TryParseNote` line 689 + `ScanIdentifierOrKeyword` line 526 + ChordParser dispatch line 637 [VERIFIED via Read]
- **flow-lang/Lexing/Token.cs** — record at line 8; OriginalText is additive [VERIFIED via Read]
- **flow-lang/Parsing/Parser.cs** — partial class line 18; ctor line 32 [VERIFIED via Read]
- **flow-lang/Parsing/Parser.NoteStream.cs** — ParseNoteStream line 38 [VERIFIED via Read]
- **flow-lang/Runtime/ModuleLoader.cs** — LoadModule line 37–111 [VERIFIED via Read]
- **flow-lang/Ast/Program.cs** — record at line 8 [VERIFIED via Read]
- **.planning/config.json** — `nyquist_validation: true` [VERIFIED via Read]
- **flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs** — XUnit Facts pattern [VERIFIED via Read]
- **flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs** — Facts pattern using FlowEngineRunner [VERIFIED via Read]
- **flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs** — byte-identical determinism harness [VERIFIED via Read]

### Secondary (MEDIUM confidence — pre-landing collision greps)
- `git grep -wn 'enable' -- '*.flow'` → empty [VERIFIED in this session]
- `git grep -wn 'hAsB' -- '*.flow'` → empty [VERIFIED in this session]
- `git grep -wn 'H' -- '*.flow'` → empty [VERIFIED in this session]
- `git grep -wnE '\bH\b|\bH[0-9]\b|\bH[#b][0-9]?\b' tests/ examples/ flow-lang/*.flow` → empty [VERIFIED in this session]
- 16 Parser/SimpleLexer construction sites enumerated via grep [VERIFIED in this session]

### Tertiary (LOW confidence — algorithm references)
- Wagner-Fischer Levenshtein algorithm — Levenshtein, V.I. (1966), "Binary codes capable of correcting deletions, insertions, and reversals". Soviet Physics Doklady 10, 707–710. [CITED]
- Rust `#![feature(...)]` file-scope semantics as design precedent for non-propagation [CITED: PITFALLS.md Pitfall 4 §"How to avoid" point 1]
- Haskell `LANGUAGE` pragma ergonomics for prefix-region comments/blanks [CITED: CONTEXT.md D-03 rationale]

## Metadata

**Confidence breakdown:**
- Pre-scan algorithm: HIGH — locked by D-01/D-03/D-04; reference implementation sketch is faithful to a standard line-by-line scan; one-pass IndexOf fast path verified zero-alloc for legacy files.
- Pragma plumbing through Parser/SimpleLexer: HIGH — locked by D-05; default-null PragmaSet keeps all 16 existing call sites compiling unchanged.
- ModuleLoader integration: HIGH — single-call insertion at line 64–79 of ModuleLoader.cs; closes Pitfall 4 structurally.
- Token original-text plumbing: HIGH — locked by D-15; Token record additive change; renderer/MIDI consume canonical Text unchanged.
- TryParseNote H acceptance: HIGH — locked by D-13/D-14/D-16; substitution + NoteType.Parse on probe is the cleanest path. One assumption (A1: NoteType.Parse("Bmaj7") fails) is flagged for an explicit Fact in Plan 21-02.
- Levenshtein did-you-mean: HIGH — Wagner-Fischer is textbook; ~25 lines of pure C#; only invoked on error.
- Test strategy: HIGH — mirrors Phase 19 / 20 patterns exactly (XUnit Facts class per file under Tests/Unit/Phase21 + integration script under tests/).
- Plan decomposition: HIGH — three-plan shape mirrors Phase 20's four-plan-with-closure precedent; the first plan landing the plumbing is the load-bearing one.
- Determinism preservation: HIGH — explicit fast-path returns original source unchanged when no `enable` substring; existing byte-identical Facts are the regression gate.

**Research date:** 2026-04-26
**Valid until:** 2026-05-26 (30 days; flow-lang internal contract is stable, not externally-driven)
