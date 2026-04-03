# Phase 4: Composition Tools - Context

**Gathered:** 2026-04-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Add chord progression DSL with automatic voice leading, polyrhythmic layering of sections with different time signatures, and probabilistic pattern variation for generating musically related sequence mutations. All features produce Sequence values composable via `->` operator.

</domain>

<decisions>
## Implementation Decisions

### Chord Progression DSL
- **D-01:** New `progression` keyword with pipe syntax: `progression | I IV vi V |` — parsed by lexer/parser into a `ProgressionExpression` AST node. Matches existing note stream `| ... |` style.
- **D-02:** Each chord fills one bar by default. Optional `:N` suffix for multi-bar chords: `progression | I:2 IV vi V:2 |` (I and V get 2 bars each).
- **D-03:** Progressions resolve roman numerals using the active `key` context (same as existing note stream roman numeral resolution via `HarmonyFunctions`).
- **D-04:** Output is a `Sequence` value — directly usable with `renderSequence`, `renderSong`, transforms, etc.

### Voice Leading
- **D-05:** Configurable voice count: user can specify number of voices. Default: match chord note count (3 for triads, 4 for 7th chords).
- **D-06:** Voice leading minimizes movement in upper voices — each voice moves to the nearest available chord tone. Bass voice follows chord root.
- **D-07:** Voice leading algorithm applies between adjacent chords in the progression. First chord uses root position.
- **D-08:** Optional voice count parameter in syntax or via a configuration function. Exact syntax is Claude's discretion.

### Polyrhythm
- **D-09:** Users create sections with different `timesig` blocks, then combine with `polyrhythm(section1, section2)` built-in function.
- **D-10:** Auto-calculates LCM of the two time signatures for cycle alignment (e.g., 3/4 + 4/4 = 12 beats). Both patterns loop until they meet.
- **D-11:** Optional `beats` parameter overrides auto-calculation: `polyrhythm(waltz, groove, 8)` cuts at 8 beats.
- **D-12:** Each section renders with its own time grid independently, then both are mixed into a single stereo buffer.

### Pattern Variation
- **D-13:** Four mutation types: pitch shift (by scale degrees, stay in key), rhythm variation (split/merge notes, preserve bar length), rest insertion (replace notes with rests), velocity variation (alter dynamics).
- **D-14:** Two overload styles: simple `vary(sequence, 0.3)` for 30% overall mutation, and specific `vary(sequence, 0.3, "pitch")` for controlling which mutation type.
- **D-15:** Simple overload randomly selects mutation type per note. Specific overload applies only the named type.
- **D-16:** Optional seed parameter for reproducible variations: `vary(sequence, 0.3, 42)` or `vary(sequence, 0.3, "pitch", 42)`.
- **D-17:** Pitch mutations stay within the active key context (diatonic movement). If no key context, use chromatic movement.

### Functional Style (carrying forward)
- **D-18:** All functions return new Sequence values — never mutate input. Composable via `->`: `melody -> vary(0.3) -> transpose(2)`.

### Claude's Discretion
- Exact voice leading algorithm implementation (nearest-neighbor vs. more sophisticated)
- Whether voice count for progressions is a syntax parameter or a separate function
- Internal data structures for voice state tracking across chord changes
- How `vary` interacts with existing `(? ...)` random choice in note streams (they're separate features)
- polyrhythm function signature details (Section vs Sequence arguments)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Harmony Infrastructure
- `flow-lang/StandardLibrary/Harmony/ChordParser.cs` — Chord symbol parsing with quality/interval sets. Progressions need the same root+quality resolution.
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — `chordNotes`, `chordRoot`, `arpeggio`, `resolveNumeral`. Voice leading builds on these.
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` — Scale definitions. Pitch mutations need scale-aware movement.

### Note Stream / Sequence Pipeline
- `flow-lang/Runtime/NoteStreamCompiler.cs` — Compiles `| ... |` syntax into Sequences. Progression syntax is similar and may reuse patterns. Also has `RandomChoiceElement` for existing `(? ...)`.
- `flow-lang/TypeSystem/SpecialTypes/SequenceType.cs` — Sequence type that progressions must produce.
- `flow-lang/TypeSystem/SpecialTypes/MusicalNoteData.cs` — Note data with pitch, duration, velocity. Variation engine mutates these.

### Musical Context
- `flow-lang/Runtime/MusicalContext.cs` — Key, tempo, timesig, swing, pan. Progressions resolve from key. Polyrhythm uses timesig.
- `flow-lang/Runtime/ExecutionContext.cs` — `GetMusicalContext()` resolves context from stack. Key resolution for roman numerals.

### Audio Pipeline
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — Section rendering. Polyrhythm renders sections independently then mixes.
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — Renders sequences to voices. Progression output feeds into this.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — Bar-level rendering with INoteSynthesizer.

### Parser / Lexer
- `flow-lang/Lexing/SimpleLexer.cs` — Add `Progression` token.
- `flow-lang/Parsing/Parser.cs` — Add `ParseProgressionExpression`. Reference note stream parsing for `| ... |` pipe syntax.
- `flow-lang/Ast/Expressions/NoteStreamExpression.cs` — Similar AST structure for progression expression.

### Transforms
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — Existing `transpose`, `invert`, `retrograde`. Variation engine is a new transform.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ChordParser.Parse()` — Full chord symbol recognition with intervals. Voice leading needs chord-to-notes conversion.
- `HarmonyFunctions.ResolveNumeral()` — Roman numeral to chord resolution in key context. Progression DSL reuses this directly.
- `NoteStreamCompiler` — Pattern for compiling `| ... |` syntax into Sequences. Progression compiler follows same architecture.
- `RandomChoiceElement` in NoteStreamCompiler — Existing probabilistic selection. Pattern variation is a separate mechanism operating on completed sequences.
- `MusicalContext.Key` — Already available for key-aware operations.
- `SynthUtils.BeatsToSeconds()` — Beat/time conversion for polyrhythm duration calculation.

### Established Patterns
- AST nodes are C# `record` types. `ProgressionExpression` follows this.
- Parser dispatches via `Match(TokenType.X)` then `ParseXxx()`.
- Built-in functions registered via `FunctionSignature` + lambda in `BuiltInFunctions.cs`.
- Transforms operate on Sequence values and return new Sequence values.

### Integration Points
- `SimpleLexer.cs`: Add `Progression` token type.
- `Parser.cs`: Add `ParseProgressionExpression` after note stream parsing.
- `ExpressionEvaluator.cs`: Evaluate `ProgressionExpression` to produce Sequence.
- `BuiltInFunctions.cs`: Register `polyrhythm`, `vary` functions.
- `TransformFunctions.cs`: Alternative location for `vary` registration.

</code_context>

<specifics>
## Specific Ideas

- Progression syntax should feel natural: `key Cmajor { Sequence chords = progression | I IV vi V | }` — reads like music notation
- Voice leading should "just work" — users don't specify inversions, the algorithm handles smooth transitions
- Polyrhythm is the "advanced" feature — combine existing sections with different time signatures for complex rhythmic patterns
- Variation should be a quick way to generate musical siblings: `melody -> vary(0.3)` produces something recognizably related but different each time

</specifics>

<deferred>
## Deferred Ideas

- **Vocaloid support** — Audio synthesis from text/phonemes. Entirely new capability requiring text-to-speech synthesis pipeline. Belongs in its own phase or future milestone.

</deferred>

---

*Phase: 04-composition-tools*
*Context gathered: 2026-04-02*
