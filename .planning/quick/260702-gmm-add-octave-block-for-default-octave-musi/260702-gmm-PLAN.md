---
phase: quick-260702-gmm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/Lexing/TokenType.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Ast/Statements/MusicalContextStatement.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/Runtime/MusicalContext.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/TypeSystem/SpecialTypes/NoteType.cs
  - flow-lang/Runtime/NoteStreamCompiler.cs
  - tests/test_octave_block.flow
  - CLAUDE.md
autonomous: true
requirements: [OCTAVE-BLOCK-01]
must_haves:
  truths:
    - "Inside `octave 3 { ... }` bare note letters in a `| ... |` stream compile at the block's octave (C -> C3), including bracket chords `[C E G]` and ghost/grace notes."
    - "Explicit octave digits always win over the block default (`C5` stays C5 inside `octave 3`)."
    - "Nested `octave` blocks resolve innermost-wins; code after a block reverts to the default octave 4."
    - "A note stream inside a proc/section called from within an `octave` block inherits the block's octave (dynamic scope, like tempo/swing)."
    - "Out-of-range `octave N` arguments are clamped charitably to [1, 9] with a one-shot `[octave]` advisory — never thrown."
    - "Existing 1-arg `NoteType.Parse` behavior, the full .flow test suite, the xUnit suite, and the FlowTarget=Web build all stay green."
  artifacts:
    - "flow-lang/TypeSystem/SpecialTypes/NoteType.cs — new `Parse(string, int defaultOctave)` overload; 1-arg delegates with 4."
    - "flow-lang/Runtime/MusicalContext.cs — new nullable `DefaultOctave` field with push/pop + Clone participation."
    - "tests/test_octave_block.flow — behavioral coverage."
  key_links:
    - "ExecutionContext.GetMusicalContext walks frames and resolves DefaultOctave (dynamic scope seam)."
    - "EvaluateNoteStream passes the frame-resolved MusicalContext to NoteStreamCompiler.Compile — the eval-time seam where the block octave reaches NoteType.Parse."
    - "NoteStreamCompiler in-scope `context` is threaded into CompileChordElement so bracket chords adopt the default octave."
---

<objective>
Add an `octave N { ... }` musical-context block that sets the default octave for bare note
letters (written without an explicit octave digit) inside note streams. Bare `C` inside
`octave 3 { | C D E | }` compiles as C3/D3/E3; explicit octaves (`C5`) always win.

The keyword becomes the 7th reserved musical-context keyword, mirroring the existing
`voicePool N { ... }` (integer-argument) block end-to-end: lexer token, parser dispatch,
`MusicalContextType` enum arm, interpreter push/pop apply, a nullable `DefaultOctave` field on
`MusicalContext` that participates in the `GetMusicalContext` frame-walk (dynamic scope), and a
new `NoteType.Parse` overload taking a default octave. Out-of-range block arguments are clamped
charitably (no throw), per house style.

Purpose: composers can shift a whole passage's register without appending an octave digit to
every letter — a pure-ergonomics feature (easy case fast) with the existing musical-context
mechanics doing the work.
Output: 9 modified core C# files, 1 new .flow test, CLAUDE.md doc touch.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

# The closest analog to mirror EXACTLY (integer-argument context block):
#   - Lexer keyword:            flow-lang/Lexing/SimpleLexer.cs:1020  ("voicePool" => TokenType.VoicePool)
#   - Token enum:               flow-lang/Lexing/TokenType.cs:28      (VoicePool)
#   - Parser dispatch+lookahead flow-lang/Parsing/Parser.cs:219-227
#   - Parser value-parse arm:   flow-lang/Parsing/Parser.cs:1028-1039
#   - Interpreter apply arm:    flow-lang/Interpreter/Interpreter.cs:374-389
#   - Frame-walk resolution:    flow-lang/Runtime/ExecutionContext.cs:953 (+ completion check 964-968)
#   - MusicalContextType enum:  flow-lang/Ast/Statements/MusicalContextStatement.cs:8
#
# The eval-time seam (already threads the resolved context — do NOT rewire):
#   flow-lang/Interpreter/ExpressionEvaluator.cs:1335-1340
#     context = _context.GetMusicalContext();  compiler.Compile(noteStream, context, _context);
#
# NoteType.Parse (add overload here): flow-lang/TypeSystem/SpecialTypes/NoteType.cs:36-51
#   line 51 is `int octave = 4;` — the value to parameterize.
#
# NoteStreamCompiler NoteType.Parse call sites: flow-lang/Runtime/NoteStreamCompiler.cs
#   IN SCOPE (bare letters the composer typed — apply default octave):
#     213 ghost, 225 grace, 676 tuplet main-note, 808 main-note, 921 chord bracket [ ... ]
#   OUT OF SCOPE (resolver-assigned octaves — leave 1-arg Parse):
#     952 named chord (Cmaj7), 994 roman numeral, 1104 + 1142 variable-reference expansion
#
# check-proc test idiom: tests/test_type_ergonomics.flow:8 (proc check(Bool: ok, String: label))
# WarnOnce signature: flow-lang/Diagnostics/RenderingDiagnostics.cs:29 WarnOnce(sentinelKey, message)
</context>

<tasks>

<task type="auto">
  <name>Task 1: Language machinery — octave keyword, context field, frame-walk resolution, Parse overload</name>
  <files>flow-lang/Lexing/TokenType.cs, flow-lang/Lexing/SimpleLexer.cs, flow-lang/Ast/Statements/MusicalContextStatement.cs, flow-lang/Parsing/Parser.cs, flow-lang/Interpreter/Interpreter.cs, flow-lang/Runtime/MusicalContext.cs, flow-lang/Runtime/ExecutionContext.cs, flow-lang/TypeSystem/SpecialTypes/NoteType.cs</files>
  <action>
Wire the `octave` keyword through the exact voicePool path (integer-argument context block):

1. TokenType.cs — add an `Octave` enum member next to `VoicePool` with a `// octave N { ... } musical-context block — default octave for bare note letters` comment.

2. SimpleLexer.cs — add `"octave" => TokenType.Octave,` to the keyword-map switch alongside `"voicePool"`. This makes `octave` a reserved keyword (verified: no `.flow` stdlib/test/example uses `octave` as an identifier — `octaveUp` and comment/string text are unaffected because the map matches the whole identifier text).

3. MusicalContextStatement.cs — add `Octave` to the `MusicalContextType` enum (append after `SustainPedal`).

4. Parser.cs dispatch — mirror the voicePool block at lines 219-227: add a `Check(TokenType.Octave) && _current + 1 < _tokens.Count && _tokens[_current + 1].Type is TokenType.IntLiteral` guard that Advances past `octave` and returns `ParseMusicalContextStatement(MusicalContextType.Octave)`. Integer literal only, no sign (voicePool-exact); range validation is deferred to the interpreter so the diagnostic points at the value.

5. Parser.cs value-parse — mirror the `case MusicalContextType.VoicePool` arm (lines 1028-1039): add `case MusicalContextType.Octave` that accepts an `IntLiteral` into a `LiteralExpression`, else throws a ParseException `Expected integer octave (e.g. octave 3 { ... }), got {type} '{text}' at {loc}`.

6. Interpreter.cs apply — mirror the `case MusicalContextType.VoicePool` arm (lines 374-389): evaluate `ctx.Value`, read `int oct = octVal.As<int>();`. CHARITABLE clamp to [1, 9] (NOT a throw): if `oct < 1 || oct > 9`, compute `int clamped = Math.Clamp(oct, 1, 9);` and emit `FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce($"octave-clamp:{oct}", $"[octave] octave {oct} out of range [1, 9] — clamped to {clamped}");` then use `clamped`. Set `musicalCtx.DefaultOctave = <resolved>;`. Rationale for [1, 9]: it is the widest block-octave range where every bare A–G letter (with typical accidentals) stays inside NoteType's E0(MIDI 16)–E10(MIDI 136) window (C1=24, B9=131), so the default-octave path can NEVER make NoteType.Parse throw. Extreme registers remain reachable via explicit per-note octave digits.

7. MusicalContext.cs — add `public int? DefaultOctave { get; set; }` (nullable = inherit, next to `VoicePoolSize` with a short XML doc). Add `DefaultOctave = DefaultOctave` to the `Clone()` initializer object (alongside `VoicePoolSize`). Optionally append `if (DefaultOctave != null) parts.Add($"octave={DefaultOctave}");` to `ToString()`.

8. ExecutionContext.cs GetMusicalContext — add `resolved.DefaultOctave ??= frame.MusicalContext.DefaultOctave;` to the `??=` inheritance chain (next to the VoicePoolSize line ~953), AND add `&& resolved.DefaultOctave != null` to the early-completion break condition (lines 964-968) so the frame walk never early-exits before a deeper octave frame is resolved — matching how VoicePoolSize participates.

9. NoteType.cs — add an overload `public static (char note, int octave, int alteration) Parse(string noteStr, int defaultOctave)` that is the current body with `int octave = defaultOctave;` replacing `int octave = 4;` at line 51. Change the existing `Parse(string noteStr)` to `=> Parse(noteStr, 4);` (single delegating expression body) so every existing caller is byte-identical. Keep the post-alteration MIDI range check unchanged — explicit-digit notes still validate exactly as today.

Do NOT touch ExpressionEvaluator.cs (the Compile call site already passes the resolved context) and do NOT add `octave` to any keyword-as-identifier allowlist (voicePool is not in one either — the feature is a reserved keyword).
  </action>
  <verify>
    <automated>dotnet build flow-lang/flow-lang.csproj 2>&1 | tail -3 && dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web 2>&1 | tail -3</automated>
  </verify>
  <done>Both the Desktop and FlowTarget=Web builds of flow-lang.csproj complete with 0 errors. `octave` lexes to TokenType.Octave, parses as a MusicalContextType.Octave block, and sets MusicalContext.DefaultOctave (clamped [1,9] with advisory) which GetMusicalContext resolves through the frame walk. NoteType.Parse has a 2-arg overload; the 1-arg form delegates with 4.</done>
</task>

<task type="auto">
  <name>Task 2: Thread default octave into note-stream compilation + tests + docs</name>
  <files>flow-lang/Runtime/NoteStreamCompiler.cs, tests/test_octave_block.flow, CLAUDE.md</files>
  <action>
1. NoteStreamCompiler.cs — at each IN-SCOPE NoteType.Parse call site, pass the active default octave, coalescing null to 4: `NoteType.Parse(<name>, context.DefaultOctave ?? 4)`. The five sites and their in-scope `context` variable:
   - line 213 (GhostNoteElement) — `context` in scope.
   - line 225 (GraceNoteElement) — `context` in scope.
   - line 676 (NoteElement inside CompileTupletElement) — `context` parameter in scope.
   - line 808 (CompileNoteElement) — `context` parameter in scope.
   - line 921 (CompileChordElement, the `[C E G]` bracket) — this method does NOT currently receive `context`. Add a `MusicalContext context` parameter to `CompileChordElement(ChordElement chord, NoteValueType.Value? autoFitDuration)` and pass `context` at BOTH call sites (line 183 inside CompileBar and line 315 inside CompileVoiceBlock — both have `context` in scope). Then use `context.DefaultOctave ?? 4` at line 921.
   Leave lines 952 (named chord Cmaj7), 994 (roman numeral), 1104 and 1142 (variable-reference expansion) on the 1-arg `Parse` — those note names come from ChordParser/ScaleDatabase/stored sequences, not bare letters the composer typed, so they keep their resolver-assigned octaves (matches the locked scope: main notes, brackets, ghost/grace only). Add a one-line comment at the CompileNamedChordElement/CompileRomanNumeralElement Parse sites noting they intentionally ignore DefaultOctave.

2. tests/test_octave_block.flow — new console test. Open with `use "@std"` and a local `proc check(Bool: ok, String: label)` printing `PASS: `/`FAIL: ` (copy the idiom from tests/test_type_ergonomics.flow:8). Assert with `(check (equals (str a) (str b)) "...")`. Assign note streams to `Sequence` variables first, then compare (avoids `|` arg-parse ambiguity). Cover, at minimum:
   - Bare letters adopt the block octave: define `Sequence ref3 = | C3 D3 E3 |` OUTSIDE any block; inside `octave 3 { Sequence inside = | C D E |  (check (equals (str inside) (str ref3)) "bare letters adopt octave 3") }`.
   - Differs from default: also `Sequence ref4 = | C4 D4 E4 |` outside; inside the same block `(check (not (equals (str inside) (str ref4))) "octave-3 stream differs from default-4")`.
   - Explicit octaves win: `Sequence ref5 = | C5 D5 |` outside; inside `octave 3 { Sequence x = | C5 D5 |  (check (equals (str x) (str ref5)) "explicit octave digits win over block default") }`.
   - Chord bracket: `Sequence rc = | [C3 E3 G3]q |` outside; inside `octave 3 { Sequence c = | [C E G]q |  (check (equals (str c) (str rc)) "bracket chord bare notes adopt octave 3") }`.
   - Nesting innermost-wins: `Sequence r2 = | C2 |` outside; `octave 5 { octave 2 { Sequence n = | C |  (check (equals (str n) (str r2)) "innermost octave wins") } }`.
   - Reverts after block: after the blocks close, `Sequence back = | C |  (check (equals (str back) (str ref4)) "bare note reverts to octave 4 after block")` (uses ref4 = octave-4 reference from above; if only `| C4 D4 E4 |` exists, add `Sequence refC4 = | C4 |`).
   - Dynamic scope through a proc: define `proc makeSeq() return | C D E | end proc` at top level; inside `octave 3 { (check (equals (str (makeSeq)) (str ref3)) "proc note stream inherits caller's octave (dynamic scope)") }`.
   - Charitable clamp: `octave 12 { Sequence hi = | C |  (check (equals (str hi) (str (| C9 |))) "octave 12 clamps to 9") }` — assign `Sequence ref9 = | C9 |` outside and compare to it (the block also emits a one-shot `[octave]` advisory to stderr; that is expected, not a failure). Add a `Note:` comment documenting that a bare `Note n = C;` OUTSIDE a stream still defaults to 4 (the lexer has no context — locked scope decision).

3. CLAUDE.md — in the "Music-Specific" > "Musical context blocks" bullet, add `octave N { }` to the keyword list and to the "all six keywords ... are reserved" sentence (now seven: tempo/timesig/key/swing/voicePool/tuning/octave). One concise clause describing the block: sets the default octave for bare note letters in streams; explicit octaves win; clamps to [1,9] charitably. Do not sprawl into other docs.
  </action>
  <verify>
    <automated>dotnet run --project flow-interpreter tests/test_octave_block.flow 2>/dev/null | tee /tmp/octblk.out; test "$(grep -c 'FAIL:' /tmp/octblk.out)" = "0" && echo OCTAVE_TEST_CLEAN</automated>
  </verify>
  <done>tests/test_octave_block.flow runs to exit 0 with zero `FAIL:` lines (OCTAVE_TEST_CLEAN printed). Bracket chords, ghost/grace notes, nesting, explicit-octave-wins, post-block revert, dynamic scope through a proc, and the [1,9] charitable clamp all pass. CLAUDE.md lists `octave N { }` as the 7th reserved musical-context keyword.</done>
</task>

</tasks>

<verification>
Full regression sweep (run after both tasks):

1. Desktop build clean: `dotnet build` → 0 errors.
2. Web build clean: `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` → 0 errors (pure-core feature; no web-strip surface).
3. New test green: `dotnet run --project flow-interpreter tests/test_octave_block.flow` exits 0, no `FAIL:` in output.
4. Full .flow suite green: `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` — the four known non-zero-exit scripts (test_dict_type_errors, test_error_masking, test_iteration_guard, test_musical_context_errors) stay as-is; every other script exits 0.
5. xUnit suite: `dotnet test flow-lang.Tests` introduces zero NEW failures vs the documented pre-existing baseline. If any test pins the `MusicalContextType` enum shape or the reserved-keyword/lexer list, update it to include `Octave`/`octave` (grep found no such pin — only behavior tests reference VoicePool).
6. Determinism unaffected: a script with no `octave` block renders byte-identical (DefaultOctave stays null → `context.DefaultOctave ?? 4` → octave 4, exactly as before the change).
</verification>

<success_criteria>
- `octave N { ... }` is a reserved musical-context keyword; bare note letters in streams inside it (main notes, bracket chords, ghost/grace, tuplet notes) compile at octave N.
- Explicit octave digits always override the block; nesting is innermost-wins; scope reverts to octave 4 after the block; dynamic scope reaches note streams inside called procs/sections.
- Out-of-range `octave N` clamps charitably to [1,9] with a one-shot `[octave]` advisory — no throw anywhere on the default-octave path.
- Named chords, roman numerals, and variable-reference expansions keep their resolver-assigned octaves (out of scope by design).
- Desktop + Web builds green; full .flow suite + xUnit green (zero new failures); no-octave scripts byte-identical.
- CLAUDE.md documents the 7th keyword.
</success_criteria>

<output>
Create `.planning/quick/260702-gmm-add-octave-block-for-default-octave-musi/260702-gmm-SUMMARY.md` when done.
</output>