# Phase 24: Scale Linting (flow-lsp) - Pattern Map

**Mapped:** 2026-05-04
**Files analyzed:** 11 (6 production + 5 tests + 1 .flow smoke)
**Analogs found:** 11 / 11 (every new file has a strong in-tree precedent)

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| `flow-lang/Lexing/PragmaRegistry.cs` (modified) | config / closed-set registry | request-response (static lookup) | existing `KnownPragmas` entries (`hAsB`, `justIntonation`, `pythagorean`, `equalTemperament`) at lines 16–23 | exact (one-line dictionary add) |
| `flow-lsp/ParseSession.cs` (modified) | service / parse-pipeline orchestrator | transform (text → AST + tokens) | `flow-lang/Core/FlowEngine.cs:66–82` (canonical pragma-scan-then-parse pipeline) | exact (mirror pipeline, drop runtime steps) |
| `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | analyzer (static-analysis pass) | transform (AST + tokens → `Diagnostic[]`) | `flow-lsp/NoteStream/NoteStreamContext.cs` (AST + token walker that produces an LSP-shaped result; same data flow shape) | role-match (analyzer vs context-resolver, but both walk AST + tokens read-only and emit LSP types) |
| `flow-lsp/Diagnostics/DiatonicSpellings.cs` | static helper / closed-set lookup | request-response (`(root, mode) → IReadOnlySet<string>`) | `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:60–185` (hardcoded mode-keyed table dictionary) | exact (closed-set, hardcoded, mode-keyed lookup) |
| `flow-lsp/Diagnostics/IScaleLintPublisher.cs` | interface (DI seam) | request-response | `flow-lsp/Handlers/DiagnosticsPublisher.cs:14–17` (`IDiagnosticsPublisher`) | exact |
| `flow-lsp/Diagnostics/ScaleLintPublisher.cs` (or `CombinedDiagnosticsPublisher.cs`) | publisher / DI implementation | event-driven (onParse → publishDiagnostics) | `flow-lsp/Handlers/DiagnosticsPublisher.cs:24–60` (`DiagnosticsPublisher` concrete) | exact |
| `flow-lsp/Program.cs` (modified) | DI composition root | event-driven (LSP server bootstrap) | existing onParse callback at `flow-lsp/Program.cs:34–53` | exact (extend the same callback) |
| `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` | xUnit Facts | request-response (parse text → assert AST shape) | `flow-lang.Tests/Unit/Phase17/ParseSessionTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` | xUnit Facts | request-response (closed-set membership) | `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` + `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` | exact |
| `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` | xUnit Theory | request-response (closed-set lookup) | `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs:20–36` (Theory over closed-enum × root) | exact |
| `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` | xUnit Facts | request-response (parse + analyze + assert diagnostics) | `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` (parse-then-call-static-helper-then-assert pattern using `LspFixtures.Parse`) | exact |
| `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` | xUnit Facts | request-response (build LSP diagnostics from FlowError list + analyzer output) | `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs` | exact |
| `tests/test_scale_lint.flow` | integration smoke | request-response (interpreter executes script; PASSED line printed) | `tests/test_tuning_ji.flow` (pragma + key context + sequence + render + PASSED print) | exact |

## Pattern Assignments

### `flow-lang/Lexing/PragmaRegistry.cs` (modified — one-line add)

**Analog:** existing `KnownPragmas` literal in the same file.

**Imports / structure pattern** (`flow-lang/Lexing/PragmaRegistry.cs:1`):
```csharp
namespace FlowLang.Lexing;

public static class PragmaRegistry
{
    public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
            ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
            ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
            ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent."
        };
```

**Action:** Add one entry after the existing `equalTemperament` line; preserve trailing-comma / no-trailing-comma style of the surrounding entries (no trailing comma in current literal — match):
```csharp
["scaleLint"] = "Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."
```

Note: switching the previous last line to end in `,` and adding the new line is conventional. The phrasing is a one-line user-facing description that matches the existing tone (terse, indicative mood, no period vs. period style — current entries end with `.`; match that).

---

### `flow-lsp/ParseSession.cs` (modified — Wave 0 widen)

**Analog:** `flow-lang/Core/FlowEngine.cs:66–82` — the canonical pragma-scan-then-parse pipeline; this is the call-site that already wires `PragmaScanner.Scan` → `SimpleLexer(..., pragmaSet)` → `Parser(..., pragmaSet)`.

**Pipeline pattern to copy** (`flow-lang/Core/FlowEngine.cs:66–82`):
```csharp
// 0. Pre-lex: extract file-scope pragmas (Phase 21 D-01).
//    Fast path returns the original string reference unchanged when
//    no `enable` substring is present — preserves Phase 18 byte-identical
//    determinism for legacy .flow files (Pitfall F mitigation).
var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, fileName, _errorReporter);
if (_errorReporter.HasErrors)
    return false;

// 1. Lex transformed source into tokens (pragmaSet wired for Plan 21-02).
var lexer = new SimpleLexer(transformedSource, _errorReporter, fileName, pragmaSet);
var tokens = lexer.Tokenize();

if (_errorReporter.HasErrors)
    return false;

// 2. Parse tokens into AST (pragmaSet attached to Program per D-08).
var parser = new Parser(tokens, _errorReporter, pragmaSet);
var program = parser.Parse();
```

**Current ParseSession to be replaced** (`flow-lsp/ParseSession.cs:18–24`):
```csharp
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    var tokens = new SimpleLexer(source, er, path).Tokenize();
    var ast = new Parser(tokens, er).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```

**Action:** Insert `PragmaScanner.Scan` between `er` allocation and `SimpleLexer` construction; switch to the 4-arg `SimpleLexer` ctor and 3-arg `Parser` ctor. The LSP variant deliberately does NOT short-circuit on `er.HasErrors` between stages — soft-failure model means downstream stages still run and produce a partial AST (Phase 17 D-06; mirrored in current code by always calling `.ToList()` over `er.Errors`).

Resulting body shape (from RESEARCH §Code Examples / Wave 0):
```csharp
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, path, er);
    var tokens = new SimpleLexer(transformedSource, er, path, pragmaSet).Tokenize();
    var ast = new Parser(tokens, er, pragmaSet).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```

**Imports already present:** `using FlowLang.Lexing;` covers `PragmaScanner` / `PragmaSet` — no new `using` needed.

---

### `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (new)

**Analog:** `flow-lsp/NoteStream/NoteStreamContext.cs` — the existing AST + token walker that produces LSP-shaped output. Phase 17 ships it as `static class`; matches D-04 "private to flow-lsp" + the AST-walk-then-token-scan pattern that D-21 explicitly REUSES verbatim.

**Imports pattern** (`flow-lsp/NoteStream/NoteStreamContext.cs:1–10`):
```csharp
using System;
using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.NoteStream;
```

**Static-class declaration pattern** (`flow-lsp/NoteStream/NoteStreamContext.cs:25`):
```csharp
public static class NoteStreamContext
```

For Phase 24 the analyzer should likewise be `internal static` (Phase 24 lives under `FlowLsp.Diagnostics` per D-04; `internal` honors "private to flow-lsp" while keeping it visible to `flow-lang.Tests` via `InternalsVisibleTo` if already configured — verify; otherwise `public`).

**AST switch-walk pattern to copy** (`flow-lsp/NoteStream/NoteStreamContext.cs:185–210`):
```csharp
private static bool StatementContainsStream(Statement s, string source, int cursorOffset)
{
    switch (s)
    {
        case MusicalContextStatement m:
            if (WalkFindStream(m.Body, source, cursorOffset)) return true;
            break;
        case SectionDeclaration sd:
            if (WalkFindStream(sd.Body, source, cursorOffset)) return true;
            break;
        case ProcDeclaration pd:
            if (WalkFindStream(pd.Body, source, cursorOffset)) return true;
            break;
        case ExpressionStatement es:
            if (es.Expression is NoteStreamExpression ns
                && StreamContainsOffset(ns, source, cursorOffset))
                return true;
            break;
        case VariableDeclaration vd:
            if (vd.Value is NoteStreamExpression nsv
                && StreamContainsOffset(nsv, source, cursorOffset))
                return true;
            break;
    }
    return false;
}
```

The Phase 24 walker has the same five `Statement` shapes to recurse through and adds the inner element-level walk over `NoteStreamExpression.Bars[].Elements[]` (records defined at `flow-lang/Ast/Expressions/NoteStreamExpression.cs:9–151`). Element-level switch follows the same record pattern-match style (RESEARCH §Code Examples already drafted the exact branches per D-06..D-14).

**Innermost-key resolution pattern (REUSED VERBATIM per D-21)** (`flow-lsp/NoteStream/NoteStreamContext.cs:43–48`):
```csharp
public static string? FindEnclosingKey(
    FlowProgram ast,
    IReadOnlyList<Token> tokens,
    string source,
    Position cursor)
```

Analyzer call site:
```csharp
var pos = new Position(Math.Max(0, loc.Line - 1), Math.Max(0, loc.Column - 1));
var keyName = NoteStreamContext.FindEnclosingKey(ast, tokens, source, pos);
if (keyName is null) return; // D-15
```

**1-based-to-0-based math pattern (DO NOT reinvent)** (`flow-lsp/NoteStream/NoteStreamContext.cs:166–171` and `flow-lsp/LspMappings.cs:21–26`):
```csharp
int line0 = Math.Max(0, t.Location.Line - 1);
int col0 = Math.Max(0, t.Location.Column - 1);
```

**Diagnostic construction pattern** — see `DiagnosticsPublisher.BuildDiagnostics` excerpt below. Analyzer constructs `Diagnostic` instances directly (not via `LspMappings.ToRange`) per D-17 / D-18:
```csharp
new Diagnostic
{
    Severity = DiagnosticSeverity.Information,            // hard-coded; LspMappings.ToSeverity not needed
    Source   = "flow.scaleLint",                          // D-18
    Message  = ...,                                       // D-16 branches
    Range    = new Range(new Position(line0, col0),
                         new Position(line0, col0 + token.Text.Length))  // D-17 token-wide
}
```

**Token lookup by Location** — RESEARCH Pitfall 3 cautions against line-only lookup. Build `Dictionary<SourceLocation, Token>` once per `Analyze` call (or linear-scan `tokens` on (Line, Column) equality):
```csharp
var tokenByLoc = tokens.ToDictionary(t => t.Location);
// ... per element ...
if (tokenByLoc.TryGetValue(elem.Location, out var tok))
{
    int width = tok.Text.Length;     // canonical width for Range
    string display = tok.OriginalText ?? tok.Text;  // OR tok.DiagnosticText helper
    // build Range + Message ...
}
```

(`Token.DiagnosticText` helper at `flow-lang/Lexing/Token.cs:32` returns `OriginalText ?? Text` — use it for the message text per Phase 21 D-15.)

---

### `flow-lsp/Diagnostics/DiatonicSpellings.cs` (new)

**Analog:** `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:60–185`. Hardcoded mode-keyed tables; closed-set; auditable at a glance. RESEARCH explicitly cites this file as the precedent; the same author-friendly literal-data style applies.

**Imports + namespace pattern** (`flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:1`):
```csharp
namespace FlowLang.StandardLibrary.Audio.Tuning;
// Mode + ChromaticRatioTable referenced by simple type names — same assembly.
```

For Phase 24:
```csharp
using FlowLang.StandardLibrary.Audio.Tuning;  // for Mode enum

namespace FlowLsp.Diagnostics;
```

**Closed-set lookup pattern** (`flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:60–67`):
```csharp
public static double LookupRatio(TuningSystem system, Mode mode, char letter, int alteration)
{
    if (!Tables.TryGetValue((system, mode), out var table))
        throw new KeyNotFoundException(
            $"TuningTables: no table for ({system}, {mode}). EqualTemperament should " +
            $"short-circuit before calling LookupRatio per Pitfall 6.");
    return table.Lookup(letter, alteration);
}
```

For Phase 24, the lookup is by `(root, mode)` and returns null (silent fail-open per D-22) instead of throwing — the analyzer is opt-in and must never crash the LSP:
```csharp
internal static class DiatonicSpellings
{
    private static readonly Dictionary<(string Root, Mode Mode), string[]> Map = new()
    {
        // C major: C D E F G A B
        [("C", Mode.Major)]      = new[] { "C", "D", "E", "F", "G", "A", "B" },
        [("C", Mode.Minor)]      = new[] { "C", "D", "Eb", "F", "G", "Ab", "Bb" },
        // ... 117 more entries; mirror TuningTables literal-data style
    };

    public static IReadOnlySet<string>? GetDiatonicSpellings(string root, Mode mode) =>
        Map.TryGetValue((root, mode), out var arr)
            ? new HashSet<string>(arr, StringComparer.Ordinal)
            : null;  // D-22 silent fail-open
}
```

**Per-table comment pattern to copy** (`flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:71` and similar):
```csharp
/// <summary>JI Ionian (Major). Diatonic: 1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8.</summary>
public static readonly ChromaticRatioTable JustIonian = ...;
```

For Phase 24, prepend each row with a one-line comment showing the diatonic set for visual audit:
```csharp
// C major: C D E F G A B
[("C", Mode.Major)] = new[] { "C", "D", "E", "F", "G", "A", "B" },
// F major: F G A Bb C D E (canonical b̂7 = Bb spelling)
[("F", Mode.Major)] = new[] { "F", "G", "A", "Bb", "C", "D", "E" },
```

**Coverage:** 17 root spellings × 7 modes = 119 entries (matches `MusicalContext.ValidKeys.Count` per Phase 23 — see test `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs:91` `Assert.Equal(119, FlowLang.Runtime.MusicalContext.ValidKeys.Count)`). The 17 roots are exactly those `ScaleDatabase.NoteToSemitone` accepts — see `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:33–42` (cited by RESEARCH).

---

### `flow-lsp/Diagnostics/IScaleLintPublisher.cs` (new)

**Analog:** `flow-lsp/Handlers/DiagnosticsPublisher.cs:9–17`.

**Interface pattern to copy verbatim** (`flow-lsp/Handlers/DiagnosticsPublisher.cs:1–17`):
```csharp
using FlowLang.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace FlowLsp.Handlers;

/// <summary>
/// Interface over PublishDiagnostics so tests can substitute a recording implementation
/// without standing up an OmniSharp <c>ILanguageServerFacade</c>. The real publisher's
/// ctor takes ILanguageServerFacade; tests inject a mock that implements IDiagnosticsPublisher.
/// </summary>
public interface IDiagnosticsPublisher
{
    void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors);
}
```

**Phase 24 sibling shape:** different return contract — per RESEARCH §Pattern 1, the analyzer is a "diagnostic source" that returns `IReadOnlyList<Diagnostic>` rather than calling `PublishDiagnostics` itself, because LSP `publishDiagnostics` REPLACES per-URI; a single combined publish is required at the call site. Two valid shapes:

Shape A (analyzer-returns, no publish):
```csharp
namespace FlowLsp.Diagnostics;

public interface IScaleLintPublisher
{
    IReadOnlyList<Diagnostic> Analyze(ParseResult result, string source);
}
```

Shape B (sibling-publish, requires combined orchestrator):
```csharp
public interface IScaleLintPublisher
{
    void Publish(DocumentUri uri, ParseResult result, string source);
}
```

RESEARCH recommends Shape A + a `CombinedDiagnosticsPublisher` orchestrator that owns the single `_server.TextDocument.PublishDiagnostics` call. Planner picks; both are equally analog-faithful.

---

### `flow-lsp/Diagnostics/ScaleLintPublisher.cs` (or `CombinedDiagnosticsPublisher.cs`) (new)

**Analog:** `flow-lsp/Handlers/DiagnosticsPublisher.cs:19–60`.

**Concrete-publisher pattern to copy** (`flow-lsp/Handlers/DiagnosticsPublisher.cs:24–60`):
```csharp
public sealed class DiagnosticsPublisher : IDiagnosticsPublisher
{
    private readonly ILanguageServerFacade _server;

    public DiagnosticsPublisher(ILanguageServerFacade server) => _server = server;

    /// <summary>
    /// Pure: transform FlowError list to LSP Diagnostic list. Exposed static so
    /// unit tests can exercise the mapping without constructing an ILanguageServerFacade.
    /// </summary>
    public static IReadOnlyList<Diagnostic> BuildDiagnostics(IReadOnlyList<FlowError> errors)
    {
        var list = new List<Diagnostic>(errors.Count);
        foreach (var e in errors)
        {
            list.Add(new Diagnostic
            {
                Severity = LspMappings.ToSeverity(e.Level),
                Source = "flow",
                Message = e.Message,
                Range = LspMappings.ToRange(e.Location)
            });
        }
        return list;
    }

    public void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors)
    {
        // MUST publish even when empty — that is how LSP clears prior markers.
        var diags = BuildDiagnostics(errors);
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(diags)
        });
    }
}
```

**Critical invariant to copy:** the empty-publish-clears-squiggles rule (the inline comment at line 52). Phase 24's combined publisher MUST honor this when EITHER list (parse errors OR scale-lint diagnostics) goes empty. Test analog for this is `DiagnosticsHandlerTests.BuildDiagnostics_ValidSource_ReturnsEmpty` (`flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs:18–24`) — repeat for the combined case.

**Combined-publish call pattern** (Phase 24 new responsibility, no exact analog — but `_server.TextDocument.PublishDiagnostics` call shape is identical, just with the union):
```csharp
var diags = parseDiags.Concat(lintDiags).ToList();
_server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
{
    Uri = uri,
    Diagnostics = new Container<Diagnostic>(diags)
});
```

---

### `flow-lsp/Program.cs` (modified — onParse callback wiring)

**Analog:** existing `flow-lsp/Program.cs:34–53` — the `DocumentManager` registration with the closure capturing `parser` and `diag`.

**Existing pattern to extend** (`flow-lsp/Program.cs:34–53`):
```csharp
.AddSingleton<DocumentManager>(sp =>
{
    var parser = sp.GetRequiredService<ParseSession>();
    var diag = sp.GetRequiredService<IDiagnosticsPublisher>();
    var users = sp.GetRequiredService<UserSymbolIndex>();
    DocumentManager? dm = null;
    dm = new DocumentManager((uri, text, ct) =>
    {
        if (ct.IsCancellationRequested) return Task.CompletedTask;
        var result = parser.Parse(text, uri.GetFileSystemPath());
        // CLOSE-RACE GUARD: if the doc closed during the debounce window,
        // do NOT publish — that would revive cleared diagnostics.
        if (dm!.HasDocument(uri))
        {
            users.Update(uri, result.Ast);
            diag.Publish(uri, result.Errors);
        }
        return Task.CompletedTask;
    });
    return dm;
})
```

**Phase 24 additions:**
1. `.AddSingleton<IScaleLintPublisher, ScaleLintPublisher>()` — DI registration mirroring the existing `IDiagnosticsPublisher` line at `flow-lsp/Program.cs:21–22`.
2. Resolve in the `DocumentManager` factory: `var lint = sp.GetRequiredService<IScaleLintPublisher>();`
3. Inside the onParse closure (after the close-race guard), invoke `lint.Analyze(result, text)` and combine its output with `diag`'s `BuildDiagnostics(result.Errors)` into a single PublishDiagnostics call.

The close-race guard at line 46 (`if (dm!.HasDocument(uri))`) MUST wrap both the parse-error publish AND the scale-lint publish — current pattern is the literal model.

---

### `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` (new)

**Analog:** `flow-lang.Tests/Unit/Phase17/ParseSessionTests.cs`.

**Imports + class declaration pattern** (`flow-lang.Tests/Unit/Phase17/ParseSessionTests.cs:1–11`):
```csharp
using FlowLsp;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 01 ParseSession Facts. Validates D-01 (reuse SimpleLexer + Parser +
/// ErrorReporter) and D-02 (no FlowEngine / audio surface in the LSP).
/// </summary>
public class ParseSessionTests
{
```

For Phase 24, copy verbatim and switch namespace to `FlowLang.Tests.Unit.Phase24`. The `LspFixtures.Parse` helper at `flow-lang.Tests/Unit/Phase17/LspFixtures.cs:9–13` is in the same `FlowLang.Tests.Unit.Phase17` namespace; either reuse with `using FlowLang.Tests.Unit.Phase17;` or duplicate the 5-line helper (recommend reuse).

**Fact pattern to copy** (`flow-lang.Tests/Unit/Phase17/ParseSessionTests.cs:13–19`):
```csharp
[Fact]
public void ValidSource_ReturnsAstWithZeroErrors()
{
    var result = LspFixtures.Parse("proc greet()\n    (print \"hi\")\nend proc");
    Assert.NotNull(result.Ast);
    Assert.Empty(result.Errors);
}
```

**Phase 24 facts to write** (per RESEARCH §Wave 0 Gaps + Pitfall 1):
```csharp
[Fact]
public void Parse_EnableScaleLint_PopulatesPragmas()
{
    var result = LspFixtures.Parse("enable scaleLint;\nkey Cmajor { | C4 D4 | }");
    Assert.True(result.Ast.Pragmas.Has("scaleLint"));
}

[Fact]
public void Parse_NoEnable_PragmasIsEmpty()
{
    var result = LspFixtures.Parse("key Cmajor { | C4 D4 | }");
    Assert.False(result.Ast.Pragmas.Has("scaleLint"));
}

[Fact]
public void Parse_EnableHAsB_LexesH4qAsNoteLiteral()
{
    // Wave 0 latent-bug regression: ParseSession now honors enable hAsB;
    var result = LspFixtures.Parse("enable hAsB;\n| H4q |");
    Assert.Empty(result.Errors); // H4q canonicalizes to B4q via lexer pragma path
}
```

---

### `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` (new)

**Analog:** `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` + `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs`. Phase 23 is the closer match because it adds entries (Phase 24 also adds an entry); both are closed-set membership Facts.

**Imports + class pattern** (`flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs:1–19`):
```csharp
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 D-08 closed-set growth Facts. Wave 2 grows
/// <see cref="PragmaRegistry.KnownPragmas"/> from the Phase 21 single entry (hAsB)
/// to 4 entries by adding three tuning pragmas: justIntonation, pythagorean,
/// equalTemperament. Phase 24 will further extend this set with scaleLint —
/// the lower-bound pin (<c>>= 4</c>) is intentionally upper-unconstrained per
/// WARNING-3 so that future pragma additions don't break this Fact.
/// </summary>
public class PragmaTuningFacts
{
```

**Fact patterns to copy** (`flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs:21–53`):
```csharp
[Fact]
public void IsKnown_JustIntonation_ReturnsTrue()
{
    Assert.True(PragmaRegistry.IsKnown("justIntonation"));
}

[Fact]
public void KnownPragmas_HasAtLeastFourEntries()
{
    // Phase 24 will add scaleLint (count → 5); the upper bound is intentionally
    // unconstrained so future pragma additions don't break this Fact (WARNING-3).
    Assert.True(PragmaRegistry.KnownPragmas.Count >= 4,
        $"expected >= 4 known pragmas; got {PragmaRegistry.KnownPragmas.Count}");
}
```

**Phase 24 facts to write:**
```csharp
[Fact]
public void IsKnown_ScaleLint_ReturnsTrue()
{
    Assert.True(PragmaRegistry.IsKnown("scaleLint"));
}

[Fact]
public void KnownPragmas_HasAtLeastFiveEntries()
{
    Assert.True(PragmaRegistry.KnownPragmas.Count >= 5);
}

[Fact]
public void AlphabetizedKnownNames_IncludesScaleLint()
{
    var csv = PragmaRegistry.AlphabetizedKnownNames();
    Assert.Contains("scaleLint", csv);
}
```

**Migration target** — RESEARCH §Pitfall 2: `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28` currently has `Assert.False(PragmaRegistry.IsKnown("scaleLint"));` and line 39 expects the CSV `"equalTemperament, hAsB, justIntonation, pythagorean"` (no `scaleLint`). Both must migrate as part of this plan: replace `"scaleLint"` with a sentinel like `"futureUnknownPragma"`, and update the CSV expectation to `"equalTemperament, hAsB, justIntonation, pythagorean, scaleLint"` (ordinal sort: `p` < `s`).

---

### `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` (new)

**Analog:** `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs:18–36`. Theory pattern over a closed enum × closed-set roots, asserting expected output for each combination.

**Theory pattern to copy verbatim** (`flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs:18–36`):
```csharp
public class ChurchModeParseFacts
{
    [Theory]
    [InlineData("Cmajor",       "C",       Mode.Major)]
    [InlineData("Aminor",       "A",       Mode.Minor)]
    [InlineData("Cdorian",      "C",       Mode.Dorian)]
    [InlineData("Aphrygian",    "A",       Mode.Phrygian)]
    [InlineData("Glydian",      "G",       Mode.Lydian)]
    [InlineData("Bmixolydian",  "B",       Mode.Mixolydian)]
    [InlineData("Dlocrian",     "D",       Mode.Locrian)]
    [InlineData("Csharpdorian", "Csharp",  Mode.Dorian)]
    [InlineData("Bbmixolydian", "Bb",      Mode.Mixolydian)]
    public void TryParseKeyWithMode_RecognizesAllSuffixes(string input, string expectedRoot, Mode expectedMode)
    {
        bool ok = ScaleDatabase.TryParseKeyWithMode(input, out string? root, out Mode mode);
        Assert.True(ok, $"expected TryParseKeyWithMode to accept {input}");
        Assert.Equal(expectedRoot, root);
        Assert.Equal(expectedMode, mode);
    }
}
```

**Phase 24 fact shapes:**
- Theory over `(root, mode, expectedSpellings)` pinning representative entries (Cmajor, Cminor, Fmajor, Edorian, Cphrygian, Clydian, Cmixolydian, Clocrian, Bmajor with double-sharps if any, Asharpminor edge case).
- Single Fact: `Map_HasExactly119Entries` — pins coverage matches `MusicalContext.ValidKeys.Count` (Phase 23 D-04).
- Single Fact: `GetDiatonicSpellings_UnknownRoot_ReturnsNull` — D-22 silent fail-open.
- Single Fact: `Cmajor_DoesNotContainEsharp` — spelling-aware D-01 canary (E# is not in Cmajor's set even though pitch-class 5 is).

**Imports pattern** (mirror `ChurchModeParseFacts.cs:1–4`):
```csharp
using FlowLang.StandardLibrary.Audio.Tuning;  // for Mode
using FlowLsp.Diagnostics;                    // for DiatonicSpellings
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;
```

---

### `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` (new)

**Analog:** `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` — parse-then-call-static-helper-then-assert pattern using `LspFixtures.Parse`.

**Imports + class pattern** (`flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs:1–14`):
```csharp
using FlowLsp.NoteStream;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 plan 06 Task 1 Facts — NoteStreamContext token-scan walker.
/// </summary>
public class NoteStreamContextTests
{
```

**Parse-then-static-helper Fact pattern** (`flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs:26–35`):
```csharp
[Fact]
public void CursorInsideStreamWithKey_ReturnsKeyName()
{
    var source = "tempo 120 {\n  key Cmajor {\n    | I IV V7 |\n  }\n}";
    var result = LspFixtures.Parse(source);
    var key = NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, new Position(2, 10));
    Assert.NotNull(key);
    Assert.Contains("major", key!, System.StringComparison.OrdinalIgnoreCase);
}
```

For Phase 24 (template from RESEARCH §Pattern 3 + Validation Architecture):
```csharp
[Fact]
public void NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic()
{
    var src = "enable scaleLint;\nkey Cmajor { | C4 D4 E4 F#4 G4 | }";
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Single(diags);
    Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
    Assert.Equal("flow.scaleLint", diags[0].Source);
    Assert.Contains("F#4 not diatonic in Cmajor", diags[0].Message);
}

[Fact]
public void PragmaAbsent_NeverFlags_LINT02()
{
    var src = "key Cmajor { | C4 D4 E4 F#4 G4 | }"; // no enable scaleLint;
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Empty(diags);
}

[Fact]
public void NestedKeys_InnermostWins_NoFlag()
{
    // F#4 IS diatonic in Gmajor (the inner key) — D-21 says inner key wins
    var src = "enable scaleLint;\nkey Cmajor { key Gmajor { | F#4 | } }";
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Empty(diags);
}
```

**Required Facts pin map (from RESEARCH §Validation Architecture):**
- LINT-01 positive: `NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic`
- LINT-02 negative: `PragmaAbsent_NeverFlags_LINT02`
- LINT-03 nested: `NestedKeys_InnermostWins_NoFlag`
- D-01 spelling: `SpellingAware_EsharpInCmajor_Flags_PitchClassMatchHint`
- D-02 modes: `EachMode_FlagsExpectedNonDiatonic` Theory
- D-08 cents: `CentOffset_E4plus50c_InCmajor_Silent`, `CentOffset_Ebplus50c_InCmajor_FlagsBaseSpelling`
- D-11..D-14 SKIPs: `Skip_RomanNumerals`, `Skip_NamedChordLiterals`, `Skip_VariableRefs`, `Skip_Rests`
- D-17 range: `Range_SpansFullTokenWidth`
- D-18 source: `Source_IsFlowScaleLint`
- D-22 fail-open: `UnparseableKey_SilentFailOpen`

---

### `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` (new)

**Analog:** `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs`.

**BuildDiagnostics-style pattern** (`flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs:18–42`):
```csharp
[Fact]
public void BuildDiagnostics_ValidSource_ReturnsEmpty()
{
    var result = LspFixtures.Parse("proc greet()\n    (print \"hi\")\nend proc");
    var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
    Assert.Empty(diags);
}

[Fact]
public void BuildDiagnostics_SourceFieldIsFlow()
{
    var result = LspFixtures.Parse("proc (");
    var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
    Assert.NotEmpty(diags);
    Assert.All(diags, d => Assert.Equal("flow", d.Source));
}
```

**Phase 24 facts** (assert source-tag and union semantics):
```csharp
[Fact]
public void CombinedPublish_ParseErrorsTagged_Flow()
{
    var src = "proc (";
    var result = LspFixtures.Parse(src);
    var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
    Assert.All(diags.Where(d => d.Severity == DiagnosticSeverity.Error),
        d => Assert.Equal("flow", d.Source));
}

[Fact]
public void CombinedPublish_ScaleLintTagged_FlowScaleLint()
{
    var src = "enable scaleLint;\nkey Cmajor { | F#4 | }";
    var result = LspFixtures.Parse(src);
    var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
    Assert.Contains(diags, d => d.Source == "flow.scaleLint");
}
```

(Exact API name `BuildAll` is planner's call — match the existing static `BuildDiagnostics` shape so tests bypass `ILanguageServerFacade`.)

---

### `tests/test_scale_lint.flow` (new)

**Analog:** `tests/test_tuning_ji.flow` — pragma + key block + sequence + render + PASSED print. Closer than `tests/test_pragma_isolation.flow` because Phase 24 declares a real-runtime-effect pragma alongside a key context, and the LINT-01 acceptance text already specifies the exact source.

**Pattern to copy** (`tests/test_tuning_ji.flow:1–29`):
```flow
enable justIntonation;

use "@std"
use "@audio"

Note: MICR-01 acceptance — 5:4 just-intonation third (Phase 23 plan 23-04)
Note: With `enable justIntonation;` declared, the C-E interval renders at
Note: ratio 5/4 (= 1.25) instead of 12-TET ~1.2599 (Math.Pow(2, 4/12)).

Note: Build a basic C-major triad sequence under justIntonation
tempo 120 {
    timesig 4/4 {
        section ji_triad {
            | C4q E4q G4q |
        }
    }
}

Note: Render path Sequence -> Section -> Song -> Buffer
Song song = [ji_triad]
Buffer audio = (renderSong song "piano")
(writeWav "/tmp/flow_test_tuning_ji.wav" audio)
(print "JI ratio applied")

(print "test_tuning_ji: PASSED")
```

**Phase 24 smoke shape** (per RESEARCH §Validation Architecture LINT-01 row + CONTEXT specifics line 172):
```flow
enable scaleLint;

use "@std"
use "@audio"

Note: LINT-01 acceptance — F#4 in Cmajor flagged as Information diagnostic
Note: in flow-lsp. flow-interpreter does not run the LSP, so this smoke verifies
Note: only that (a) the pragma is accepted by the closed registry and (b) the
Note: file parses and renders without error. Per-diagnostic assertions live
Note: in flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs.

tempo 120 {
    key Cmajor {
        section lint_canary {
            | C4q D4q E4q F#4q G4q |
        }
    }
}

Song song = [lint_canary]
Buffer audio = (renderSong song "piano")
(writeWav "/tmp/flow_test_scale_lint.wav" audio)

(print "test_scale_lint: PASSED")
```

The pragma name `scaleLint` is registry-validated by `PragmaRegistry.KnownPragmas` post-add — if the new entry is missing, the file fails parse (D-12 unknown-pragma error). The smoke is therefore also a closed-set-membership integration check.

---

## Shared Patterns

### Pattern: 1-based-to-0-based source-location math

**Source:** `flow-lsp/LspMappings.cs:21–26` and `flow-lsp/NoteStream/NoteStreamContext.cs:166–171`

**Apply to:** `ScaleLintAnalyzer.cs` (every Range computation) and any test that constructs a `Position`.

```csharp
var line = Math.Max(0, loc.Line - 1);
var col = Math.Max(0, loc.Column - 1);
```

The `Math.Max(0, ...)` guards against `SourceLocation.Unknown` which uses `(0, 0)` (becomes `-1` after subtraction). RESEARCH Pitfall 5 cites this verbatim as the reuse target.

### Pattern: Empty-publish-clears-squiggles invariant

**Source:** `flow-lsp/Handlers/DiagnosticsPublisher.cs:50–59`

**Apply to:** `CombinedDiagnosticsPublisher` (or whatever orchestrator owns the single `_server.TextDocument.PublishDiagnostics` call). Comment to copy verbatim:

```csharp
// MUST publish even when empty — that is how LSP clears prior markers.
```

A test that asserts an edit which removes the non-diatonic note results in an empty publish (RESEARCH Pitfall 6 — `RemovingNonDiatonicNote_ClearsDiagnostic`).

### Pattern: Soft-failure analyzer (run on partial parse)

**Source:** `flow-lsp/NoteStream/NoteStreamContext.cs:11–20` (the comment block) and CLAUDE.md §Key Design Decisions ("Error accumulation: ErrorReporter collects errors rather than throwing").

**Apply to:** `ScaleLintAnalyzer` — never throws; returns `[]` for any AST shape it can't classify. Element `switch` falls through silently for `RestElement`, `RomanNumeralElement`, `NamedChordElement`, `VariableReferenceElement` (D-11..D-14).

### Pattern: Static helper class, internal-to-flow-lsp

**Source:** `flow-lsp/NoteStream/NoteStreamContext.cs:25` (`public static class`) and `flow-lsp/LspMappings.cs:13` (`public static class`).

**Apply to:** `ScaleLintAnalyzer` (likely `internal static` per D-04 "private to flow-lsp"; check for `InternalsVisibleTo("flow-lang.Tests")` in `flow-lsp.csproj` — if absent, default to `public static` to match existing convention).

### Pattern: Test fixture reuse via `LspFixtures.Parse`

**Source:** `flow-lang.Tests/Unit/Phase17/LspFixtures.cs:9–13`

**Apply to:** All Phase 24 LSP-aware Facts (`ParseSessionPragmaFacts`, `ScaleLintAnalyzerFacts`, `CombinedDiagnosticsPublisherFacts`).

```csharp
public static class LspFixtures
{
    public static ParseResult Parse(string source, string? path = null) =>
        new ParseSession().Parse(source, path);
}
```

Phase 24 tests should `using FlowLang.Tests.Unit.Phase17;` to consume the existing helper rather than duplicating it. The Wave 0 `ParseSession` widen is the precondition that makes this fixture usable for pragma assertions.

### Pattern: Closed-set growth Fact with upper-unconstrained count

**Source:** `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs:46–53`

**Apply to:** `PragmaRegistryScaleLintFacts` (count `>= 5` rather than `== 5` per WARNING-3).

### Pattern: AST node `record` types + pattern-match dispatch

**Source:** `flow-lsp/NoteStream/NoteStreamContext.cs:185–210` (Statement-level switch); `flow-lang/Ast/Expressions/NoteStreamExpression.cs:9–151` (record hierarchy).

**Apply to:** `ScaleLintAnalyzer` element-level dispatch. CLAUDE.md §C# Conventions: "Pattern matching (`switch` expressions) for node dispatch rather than visitor pattern".

### Pattern: Token diagnostic-friendly text via `OriginalText` fallback

**Source:** `flow-lang/Lexing/Token.cs:19–32`

**Apply to:** Diagnostic message construction in `ScaleLintAnalyzer`. Use `token.DiagnosticText` (returns `OriginalText ?? Text`) so a composer who typed `H4q` under `enable hAsB; enable scaleLint;` sees `H4q` in the message text even though the canonical `Text` is `B4q` (Phase 21 D-15 — verbatim copy).

```csharp
public string DiagnosticText => OriginalText ?? Text;
```

## No Analog Found

None — every Phase 24 file has a strong in-tree precedent. The closest "stretch" is `CombinedDiagnosticsPublisher` itself (no existing flow-lsp publisher composes two diagnostic sources into a single `PublishDiagnostics` call), but the constituent patterns (`PublishDiagnosticsParams` shape, empty-publish invariant, DI registration as `IDiagnosticsPublisher`-style interface) all exist.

## Metadata

**Analog search scope:**
- `flow-lsp/` (entire production tree — 6 directories, ~12 files)
- `flow-lang/Lexing/` (PragmaRegistry, PragmaScanner, PragmaSet, Token)
- `flow-lang/Core/` (FlowEngine pipeline)
- `flow-lang/StandardLibrary/Audio/Tuning/` (TuningTables hardcoded-data precedent + Mode enum)
- `flow-lang/StandardLibrary/Harmony/` (ScaleDatabase.TryParseKeyWithMode)
- `flow-lang/Ast/Expressions/` (NoteStreamExpression record hierarchy)
- `flow-lang/Ast/Statements/` (MusicalContextStatement)
- `flow-lang.Tests/Unit/Phase17/` (LSP test conventions — 19 files)
- `flow-lang.Tests/Unit/Phase21/` (PragmaRegistryFacts — closed-set growth precedent)
- `flow-lang.Tests/Unit/Phase23/` (PragmaTuningFacts, ChurchModeParseFacts, SpellingAwareTuningFacts — closer-match closed-set growth + Theory pattern)
- `tests/` (test_tuning_ji.flow, test_pragma_isolation.flow — pragma + key block smoke precedents)

**Files inspected:** 18

**Pattern extraction date:** 2026-05-04
