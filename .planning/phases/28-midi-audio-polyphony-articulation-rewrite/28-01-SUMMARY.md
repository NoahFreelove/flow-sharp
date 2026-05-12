---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 01
status: complete
requirements: [SPEC-1, SPEC-3]
self_check: PASSED
test_count_before: 883
test_count_after: 887
new_facts: 4
commits:
  - 81040dc feat(28-01): add Articulation.Legato enum value
  - 916bc96 feat(28-01): lex `voicePool` keyword + add VoicePool TokenType
  - 00b56c9 feat(28-01): parse `leg` articulation token to Articulation.Legato
  - 2842593 feat(28-01): add VoiceBlockElement AST node
  - ee0036c feat(28-01): parse `{voice ...}` blocks → VoiceBlockElement
  - c7350f1 feat(28-01): compile VoiceBlockElement → BarData.ParallelVoices
  - 0bc898a test(28-01): pin Articulation.Legato + voice-block parser/compiler facts
key_files:
  created:
    - flow-lang.Tests/Unit/Phase28/LegatoEnumTests.cs
    - flow-lang.Tests/Unit/Phase28/VoiceBlockParserTests.cs
  modified:
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs (+ Articulation.Legato)
    - flow-lang/TypeSystem/SpecialTypes/BarType.cs (+ ParallelVoices field)
    - flow-lang/Lexing/TokenType.cs (+ VoicePool)
    - flow-lang/Lexing/SimpleLexer.cs (+ voicePool keyword)
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs (+ VoiceBlockElement)
    - flow-lang/Parsing/Parser.NoteStream.cs (+ leg token, voice-block dispatch, ParseVoiceBlockChildren)
    - flow-lang/Runtime/NoteStreamCompiler.cs (+ VoiceBlockElement case, CompileVoiceBlock)
---

## Plan 01 — Voice-Block Parser + Legato Enum + voicePool Keyword

### What shipped

Source-level recognition for Phase 28's polyphony + articulation surface, with no
audio render changes yet. All four acceptance must-haves verified by xUnit facts.

1. **`Articulation.Legato` enum value** added at the end of `Articulation` so existing
   enum-int casts remain stable. Distinct from the Phase 22 `legato()` transform
   (which adjusts `DurationOverlap`); the enum value drives per-note articulation
   envelope shaping (Plan 28-03 owns the synth-side rendering).

2. **`voicePool` keyword** lexed to a new `TokenType.VoicePool`. Plan 28-05 wires
   the parser/AST/runtime/voice-allocator chain; this commit only opens the
   lexical surface so depending plans can consume the token.

3. **`leg` articulation token** added to `TryParseArticulation` (returns
   `Articulation.Legato`) and `IsEndOfNoteStream` (continues the stream).

4. **`VoiceBlockElement` AST node** record sits adjacent to `TupletElement` in
   `flow-lang/Ast/Expressions/NoteStreamExpression.cs`.

5. **`{voice ...}` brace dispatch** in `ParseNoteStream` peeks for the `voice`
   keyword before the existing tuplet-IntLiteral expect, falls through to tuplet
   logic on miss (rewinds to before `{`). Backward-compat: `{N ...}q` and
   `{N:M ...}q` paths unchanged.

6. **`ParseVoiceBlockChildren`** helper parses note/rest/chord/named-chord/tuplet
   elements with full `NoteElement` articulation parsing. Nested voice blocks
   emit `"Nested voice blocks are not supported (Phase 28 scope)"`.

7. **`BarData.ParallelVoices`** field (defaults `null`) and `CompileVoiceBlock`
   helper in `NoteStreamCompiler` produce per-block parallel `BarData` instances
   sharing the parent bar's onset (0). Renderer changes ship in Plans 02/03.

### Key links — verified

- `Parser.NoteStream.cs:153` — `if (Check(TokenType.LBrace))` branch peeks for
  `voice` before `Expect(TokenType.IntLiteral, ...)`.
- `Parser.NoteStream.cs` — `TryParseArticulation` switch contains `case "leg":
  return Articulation.Legato;`.
- `BarType.cs` — `public List<BarData>? ParallelVoices { get; set; }` defaults
  null so all pre-Phase-28 bars stay byte-identical.
- `NoteStreamCompiler.cs` — `case VoiceBlockElement:` arm in `CompileBar`
  switch; `CompileVoiceBlock` mirrors element loop, drives off
  `voiceBlock.Children`.

### Truths verified by xUnit

| Truth | Fact |
|-------|------|
| Articulation enum contains Legato | LegatoEnumTests.Legato_EnumValueExists |
| Parser accepts `leg` token; produces Articulation.Legato | VoiceBlockParserTests.Parser_AcceptsLegToken |
| Parser accepts `\| {voice C4w} {voice C5q D5q E5q F5q} \|` without error | VoiceBlockParserTests.VoiceBlockParser_AcceptsBasicSyntax |
| Compiler emits two parallel BarData objects sharing onset 0 | VoiceBlockParserTests.VoiceBlockCompiler_EmitsParallelBars |

### Test counts

- Phase 28 unit facts (xUnit): **4/4 GREEN**
- Full unit suite: **887/887 GREEN** (was 883 — +4 new facts, no regressions)

### Self-Check: PASSED

Build clean (3 pre-existing warnings unchanged), all targeted tests pass, full
suite green, no architectural deviations from PLAN.md.

### Deviations

None. Task 7 chose the local-list-on-stack accumulator pattern for
`parallelVoices` (re-entrant, no compiler instance state) rather than a private
field; documented inline. This is a Rule-1 implementation detail, not an
architectural deviation — the PLAN action text said "concrete mechanism: extend
BarData with a List<BarData> ParallelVoices field, or attach voice-index" and
explicitly locked the ParallelVoices approach, which is what landed.

### Hand-off to dependent plans

- **Plan 28-02** can read `Articulation.Legato` in `NoteStreamCompiler` velocity
  rules + `BarRenderer` duration multipliers without further surface work.
- **Plans 28-03 / 28-05** can iterate `bar.ParallelVoices` in `BarRenderer` /
  `SequenceRenderer` to emit per-voice render passes. Each child bar carries
  its own `MusicalNotes` list ready for the existing render path.
- **Plan 28-05** can consume `TokenType.VoicePool` from the lexer; the parser
  dispatch / AST / interpreter chain ships in that plan.
