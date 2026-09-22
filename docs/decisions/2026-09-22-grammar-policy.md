# One stable grammar with optional music semantics

Status: accepted for restructuring Phase 1 (2026-09-22). Needed by Phases 3–4.

## Context

`SimpleLexer` recognizes note, chord, unit and duration literals, and `Parser`
recognizes note streams, songs, sections and musical-context blocks. Both refer to
runtime music types directly: `Parsing/TypeParser.cs` binds `FlowType`s, and the
dependency baseline records `SimpleLexer`, `Parser` and `TypeParser` referencing
`TypeSystem.SpecialTypes` and Harmony. File-scope pragmas (`hAsB`, tuning, `strict`,
`beat-true-to-sig`) are scanned before lexing.

## Decision

1. **One grammar for every profile.** Music syntax always parses, including in the
   `language` profile. A language-only host reports "requires music support"
   during analysis or evaluation of a music construct. It does not report a
   parse error.
2. **Syntax nodes describe source only.** Recognizing a note stream, chord or unit
   literal must not construct music runtime values or initialize music or audio
   services. Phase 3 replaces parser references to concrete runtime types with
   type-name/type-argument syntax records bound later against the module/type
   catalog.
3. **Tokenization does not depend on imports.** `use` never changes how the rest of
   a file lexes. File-scope pragmas may keep adjusting lexing (for example `hAsB`)
   because they are declared before any code and scanned before lexing.
4. **Reserved words and note-like identifier rules stay as they are.** Reclaiming
   names (for example identifiers that look like notes, or the musical-context
   keywords) would be a language-version decision, never a side effect of a smaller
   installation.
5. **No grammar plugin system.** Music expression and statement families go through
   a small explicit interface inside the frontend. No runtime extension can add
   syntax.
6. **No syntax redesign during restructuring.** Prefix calls, optional parentheses,
   `->`/`~>`, implicit returns, comment forms and literal spellings are pinned by
   `contracts/language/`. Gaps recorded there (`(t ~> f)`, tuple patterns in
   `match`, `Tuple<<...>>[]` annotations) are separate feature decisions.

## Consequences

- A tiny distribution without music grammar is a possible later packaging feature
  with its own compatibility matrix. It is not a Phase 3 goal.
- The frontend can move to the language component once its music references are
  syntax-only. This shrinks the lexer/parser edges in the dependency baseline.

## Verification to add

- Parsing every `contracts/language/**` and `examples/**` file produces identical
  syntax trees before and after frontend extraction (a round-trip or AST-dump
  comparison).
- A language-profile session parses `contracts/language/music/*.flow` without
  parse errors and reports a missing-capability diagnostic when evaluating them.
- `DependencyDirectionTests` shows no `FlowLang.Lexing`/`FlowLang.Parsing` edges
  once the frontend is syntax-only.
