# Phase 39: Notation Citizenship - Research

**Researched:** 2026-05-23
**Domain:** Music-notation file formats (MusicXML 3.1, LilyPond, ABC 2.1, MML PC-98)
**Confidence:** HIGH (CONTEXT D-39-01..22 lock the design; this research only verifies the
implementation surface, vendor licenses, environment availability)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
Verbatim from 39-CONTEXT.md `<decisions>` section: D-39-01 (single `@notation-io` stdlib
module, NOT `@notation` to avoid collision with existing music-notation primitives), D-39-02
(sub-phase order MusicXML → LilyPond → ABC → MML; MML defers first, ABC defers second),
D-39-03 (vendor `sightreader/musicxml-schemas` POCOs; verify license at plan-start, fall back
to hand-roll `XmlWriter` if blocked), D-39-04 (vendor `matthewcpp/ABCSharp` source; verify
license, fall back to hand-roll if blocked), D-39-05 (LilyPond + MML fully hand-rolled —
no vendoring), D-39-06 (microtonal as decimal `<alter>` — no text fallback ladder),
D-39-07 (Legato slur grouping: runs of ≥2 consecutive Legato notes per voice become one
`<slur number="N">` span; single Legato notes get no slur), D-39-08 (round-trip CI gate
charitable: `mscore --convert-to mxl` when available; stderr advisory + PASS when absent),
D-39-09 (multi-track Song → multi-`<part>` MusicXML; reuse `MidiExport.ResolveGmProgram`
for `<part-name>`), D-39-10 (`.musicxml` extension default; no `.mxl` compression),
D-39-11 (nested tuplets flatten by ratio-multiplication for LilyPond), D-39-12 (LilyPond
microtonal: nearest 12-TET pitch + `% +50c` comment), D-39-13 (per-Sequence `\new Staff`;
voice blocks → `\new Voice` siblings inside `<< { ... } \\ { ... } >>`),
D-39-14 (`\version "2.24.0"` LilyPond header), D-39-15 (ABC 2.1 core + abc2midi `Q:` +
modal keys; unknown ornaments dropped with `[abc]` advisory), D-39-16 (single tune → `Section`;
multi-tune → `Array[Section]`), D-39-17 (charitable ABC parser — never throws),
D-39-18 (MML PC-98 core only; FM/drum-bank ignored with `[mml]`), D-39-19 (charitable MML;
loop nesting cap 16 mirroring T-36-17), D-39-20 (extract `InstrumentRouting` helper from
`MidiExport.ResolveGmProgram` if both MusicXML and LilyPond consume it), D-39-21 (Phase 35
`(match articulation | ...)` is the natural articulation-emit site),
D-39-22 (`examples/notation/` chapters: `to_musicxml.flow`, `to_lilypond.flow`, `from_abc.flow`,
`from_mml.flow`; each doubles as `tests/test_notation_*_example.flow` regression test).

### Claude's Discretion
- `Vendor/` directory naming convention — **DECISION: `flow-lang/Vendor/`** (matches `Samples/`
  precedent: PascalCase, sibling to source tree).
- MusicXML emit path — **DECISION: hand-rolled `XmlWriter` with full control**, not
  `System.Xml.Serialization` against the POCOs. Rationale: emit needs deterministic attribute
  ordering for two-run cmp-clean (Phase 18/25/27 contract), and `XmlSerializer` reflection
  ordering is unstable across .NET patch versions. The POCOs become deserialization-only
  scaffolding for the MusicXML round-trip CI gate (XML-02) test where we read MuseScore's
  output back to compare structurally. **Update during plan-start vendor audit**: if the POCOs
  prove unnecessary for the round-trip test (i.e., we can structurally diff XML element trees
  with raw `XDocument`), drop the vendor entirely. We will revisit this in Plan 39-01 wave 0.
- LilyPond `\midi { }` block default — **DECISION: keep** (matches LilyPond user expectation;
  trivial to emit; composer can post-edit out).
- ABC `Q:` tempo parsing — researcher confirms ABCSharp's `Q:` handling at vendor time;
  if gaps exist (e.g., `Q:"Allegro" 1/4=120` annotated form), hand-fill in `AbcImport.cs`
  post-parse normalization.
- MML loop semantics edge case `[abc[de]2f]3` — **DECISION: inner expands each outer
  iteration** (PC-98 MUCOM/PMD convention; verified against pmd2vgm reference docs at
  http://www.tcat.ne.jp/~kihachi/pmd_e.htm — outer iterations are independent macro
  expansions; nested loops are macro-recursive substitution, not byte-shared expansion).
- `flow notation convert` CLI subcommand — **DECISION: not for v1.5**. Composer composes
  `flow run script.flow` with `(writeMusicXML)` inside; revisit if v1.6 doc work needs it.
- Plan breakdown — **DECISION (5 plans, 3 waves):**
  - **Wave 0 (Plan 39-01):** Vendor `MusicXmlSchemas` + license audit + `@notation-io`
    stdlib module skeleton + `InstrumentRouting` shared helper extraction (D-39-20) +
    `MusicXmlExport.cs` emit + `writeMusicXML` builtin + round-trip CI gate (XML-02 charitable
    skip when `mscore` absent). Wave 0 because all later plans depend on the module + helper.
  - **Wave 1 (Plan 39-02):** `LilyPondExport.cs` emit + `writeLilyPond` builtin (consumes
    `InstrumentRouting`; standalone otherwise). LILY-01.
  - **Wave 1 (Plan 39-03):** Vendor `ABCSharp` + license audit + `AbcImport.cs` adapter +
    `abc` builtin (single-tune `Section` + multi-tune `Array[Section]` overloads). ABC-01,
    ABC-02. Parallel-safe with Plan 39-02 since LilyPond doesn't consume any ABC types.
  - **Wave 2 (Plan 39-04):** `MmlImport.cs` hand-rolled tokenizer + `mml` builtin. MML-01.
    Wave 2 because the wave-0 module skeleton must be in place; otherwise standalone.
  - **Wave 2 (Plan 39-05):** Examples (`examples/notation/*.flow`) + regression tests
    (`tests/test_notation_*.flow`) + VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md
    sweep. Wave 2 because it consumes the outputs of all prior plans.

### Deferred Ideas (OUT OF SCOPE)
Verbatim from 39-CONTEXT.md `<deferred>` section: MusicXML import (anti-feature lock until
v1.6), LilyPond import, ABC export, MML export, MEI/GuitarPro/PowerTab, custom notation DSLs,
`flow notation convert` CLI, MML multi-dialect, ABC strict mode, compressed `.mxl`.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| XML-01 | `(writeMusicXML song "piece.musicxml")` — MuseScore-compatible 3.1 partwise emit | `MidiExport.cs` walks the same `SongData→Sections→Sequences→Bars→Notes` structure; MusicXML emit mirrors that walk, swapping NoteOn/NoteOff for `<note><pitch>...</pitch><duration>N</duration><type>quarter</type></note>` and articulation switch from D-v1.5-08 table |
| XML-02 | MusicXML round-trip CI gate via `mscore --convert-to mxl` | `mscore` not in PATH on dev box (verified) → D-39-08 charitable skip + stderr advisory honored |
| LILY-01 | `(writeLilyPond song "piece.ly")` — `lilypond -dno-print-pages` compiles cleanly | `lilypond` not in PATH on dev box (verified); test is "emit textually well-formed `.ly`" + skip the engraver-compile check with stderr advisory when `lilypond` absent |
| ABC-01 | `(abc "X:1\n...")` returns `Section` or `Array[Section]`; ABC 2.1 + abc2midi | Vendor `matthewcpp/ABCSharp` (MIT-licensed, verified — see Vendor Audit below) under `flow-lang/Vendor/ABCSharp/` |
| ABC-02 | Modal keys + unknown ornament drop with `[abc]` advisory | `RenderingDiagnostics.WarnOnce` (existing Phase 36/33 pattern) keyed by `(token, line)` dedup |
| MML-01 | `(mml "T120 L4 O4 cdefga>c")` returns `Sequence`; PC-98 common core | Hand-rolled tokenizer ~10 commands (notes, accidentals, octave, length, tempo, loops); recursive descent matches `ScalaParser.cs` style at ~200 lines |
</phase_requirements>

## Summary

Phase 39 ships **6 REQs** across **4 notation surfaces** (MusicXML export, LilyPond export,
ABC import, MML import) as a **single composer-facing stdlib module** `@notation-io`. The
phase is **structurally well-bounded**: every emit/import path walks or constructs Flow's
existing `SongData / SequenceData / BarData / MusicalNoteData` model — there are zero new AST
nodes, zero new value types, and zero new musical concepts. The risk surface is narrow:

1. **Two vendored sources** (musicxml-schemas POCOs, ABCSharp parser) — both verified MIT at
   research time, low integration risk.
2. **Two external CI binaries** (`mscore`, `lilypond`) — both absent on the dev box; D-39-08
   charitable-skip posture means CI never blocks on their absence.
3. **Two charitable-interpretation import surfaces** (ABC, MML) — must NEVER throw on
   malformed input; the existing `RenderingDiagnostics.WarnOnce` advisory pattern handles
   this cleanly.

**Primary recommendation:** Execute the 5-plan breakdown above. Plan 39-01 is the dependency
root (vendor + module skeleton + InstrumentRouting helper + MusicXML); Plans 39-02 + 39-03 are
commutative; Plan 39-04 is sequential after wave 0; Plan 39-05 closes the phase. Total
estimated C# LOC ≈ 2500 (MusicXML ~600, LilyPond ~400, ABC adapter ~300 + vendored ~800,
MML ~400, InstrumentRouting + module skeleton ~200).

## Vendor License Audit

> Per D-39-03 / D-39-04, vendoring is **gated** on MIT/Apache/BSD/PublicDomain compatibility.
> Copyleft (GPL/LGPL/MPL) blocks vendor → fall back to hand-roll.

| Source | Upstream URL | License | Copyright | Disposition |
|--------|--------------|---------|-----------|-------------|
| `sightreader/musicxml-schemas` | `https://github.com/sightreader/musicxml-schemas` | MIT | © 2019 SightReader | **VENDOR-APPROVED** — verified via WebFetch `raw.githubusercontent.com/sightreader/musicxml-schemas/master/LICENSE` (2026-05-23) |
| `matthewcpp/ABCSharp` | `https://github.com/matthewcpp/ABCSharp` | MIT | © 2020 Matthew LaRocca | **VENDOR-APPROVED** — verified via WebFetch `raw.githubusercontent.com/matthewcpp/ABCSharp/master/LICENSE` (2026-05-23) |

**Vendoring discipline (Phase 29 precedent):** each vendored source ships
`flow-lang/Vendor/<Name>/LICENSE` (verbatim copy) + `VENDORED-FROM.md` (upstream URL + commit
SHA + date + Flow's local modifications, if any). License audit gate test under
`flow-lang.Tests/Integration/Phase39/VendoredSourceLicenseTests.cs` (mirrors
`LicenseAuditTests.cs` shape — pure file existence + content assertions, no live network).

**No new NuGet packages** per D-39-03/04/05; the existing `Melanchall.DryWetMidi 8.0.3` +
`Pidgin 3.5.1` references in `flow-lang/flow-lang.csproj` are unchanged.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| MusicXML export | StandardLibrary → C# emit | Vendor POCOs (optional) | Walks SongData; XmlWriter for deterministic emit |
| MusicXML round-trip gate | flow-lang.Tests → xUnit | External `mscore` (optional) | CI gate; charitable skip when binary absent |
| LilyPond export | StandardLibrary → C# emit | (none) | Pure StringBuilder text composition |
| ABC import | StandardLibrary → C# adapter | Vendor `ABCSharp` | Adapter translates ABCSharp's AST → Flow `SectionData` |
| MML import | StandardLibrary → C# parser | (none) | Hand-rolled tokenizer + recursive interpreter |
| Stdlib module activation | `@notation-io` flow file | C# `__enableNotationIoModule` marker | Mirrors `@sfz` (Phase 33) precedent |

## Standard Stack

### Core (existing, no changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 / C# 13 | net10.0 | Runtime | Existing project target |
| `System.Xml` (BCL) | 10.0 | `XmlWriter` for MusicXML emit | BCL — zero dependency; deterministic output via `XmlWriterSettings.NewLineHandling = Replace` + `OmitXmlDeclaration = false` |
| `System.Text` (BCL) | 10.0 | `StringBuilder` for LilyPond emit | BCL — zero dependency |
| `System.IO` (BCL) | 10.0 | `File.WriteAllText` for output | BCL — existing pattern from `writeMidi` |

### New vendored sources (no NuGet)
| Source | Local Path | Purpose | Why Vendored |
|--------|-----------|---------|--------------|
| `sightreader/musicxml-schemas` | `flow-lang/Vendor/MusicXmlSchemas/` | MusicXML 3.1 POCO schema | XSD-generated POCOs — used for round-trip CI gate diff (optional); also lets future XML import (v1.6) skip schema work |
| `matthewcpp/ABCSharp` | `flow-lang/Vendor/ABCSharp/` | ABC 2.1 parser | Only actively-maintained C# ABC implementation (last commit 2024); hand-rolling would take ~800 LOC and miss abc2midi edge cases |

### Alternatives Considered (rejected per STACK.md)
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled MusicXML POCOs | `MusicXml.NET` NuGet | Rejected: parser-only (wrong direction, we need emit); STACK.md anti-recommends |
| Hand-rolled ABC parser | `LilyPond's abc2ly` shell-out | Rejected: needs `lilypond` binary, not portable; STACK.md anti-recommends |
| `NAudio` for any DSP | (existing hand-rolled) | Rejected: Windows-centric; project already targets PulseAudio |

## Package Legitimacy Audit

> Per the `package_legitimacy_protocol`: this phase installs **zero new packages**. All
> external code arrives via source vendoring under `flow-lang/Vendor/`. No NuGet additions
> means no slopcheck pass is required.

| Package | Registry | Disposition |
|---------|----------|-------------|
| (none added) | — | — |

## Architecture Patterns

### Module Activation (Phase 33 `@sfz` precedent)

`flow-lang/notation-io.flow` (NEW file; `@notation-io` resolves to this) declares forward
decls for the 4 builtins + a marker `__enableNotationIoModule` builtin called once at module
load. The C# `BuiltInFunctions.RegisterAllImplementations` registers the 4 builtins (always);
the marker flips a runtime gate. The gate is **purely advisory** here — the 4 builtins have
no shared global state (unlike Phase 33's `Sfz` registry), so the gate primarily prevents
"called without `use \"@notation-io\"`" footgun errors.

### MusicXML Export (`MusicXmlExport.cs`)

Mirrors `MidiExport.ExportMidiInternal` structure:
1. Compute global context (tempo, time signature, key) from first section.
2. Emit `<score-partwise version="3.1">` + `<part-list>` (one `<score-part>` per
   uniqueSequenceName).
3. For each part: emit `<part>` containing `<measure>` blocks (one per bar). First measure
   carries `<attributes>` (divisions=480 default, key fifths, time, clef = G2 default).
4. For each `<measure>`: walk `bar.MusicalNotes` + `bar.ParallelVoices`. Each `MusicalNoteData`
   → `<note>` containing `<pitch>` (step + octave + alter), `<duration>` (in divisions),
   `<type>` (quarter/eighth/etc.), `<voice>N</voice>`, and articulation
   `<notations><articulations>...</articulations></notations>` per D-v1.5-08 table.
5. Legato slur grouping: scan-ahead during emission, track `<slur number="N">` open/close
   per D-39-07.

### LilyPond Export (`LilyPondExport.cs`)

Pure `StringBuilder` composition. Structure:
```
\version "2.24.0"
\score {
  <<
    \new Staff = "sequence1" {
      \tempo 4 = 120
      \time 4/4
      \key c \major
      << { voice1-notes } \\ { voice2-notes } >>
    }
    \new Staff = "sequence2" { ... }
  >>
  \layout { }
  \midi { }    % D-39-13 default-keep per Claude's Discretion
}
```

### ABC Import (`AbcImport.cs`)

Two builtins: `abc(String) → Section | Array[Section]`. Adapter walks ABCSharp's AST → Flow
`SectionData`. Per D-39-16: count `X:N` blocks; 1 → return single `Value.Section(...)`; ≥2 →
`Value.Array(SectionType, [Section, ...])`. Unknown ornaments / headers dropped via
`RenderingDiagnostics.WarnOnce("abc-ornament:{token}:{line}", "[abc] dropped ornament...")`.

### MML Import (`MmlImport.cs`)

Hand-rolled tokenizer:
- **State machine:** octave (default 4), length (default L4 = quarter), tempo (default 120),
  iteration stack for loops.
- **Tokens:** notes `[a-g]` + accidentals `+`/`#`/`-`, octave `O<n>` / `>` / `<`, length
  `L<n>`, tempo `T<n>`, loops `[...]<n>`, dots `.`, ties `&`.
- **Unknown opcode:** consume up to next whitespace; `WarnOnce("mml-opcode:{token}:{offset}",
  "[mml] dropped opcode...")`. Continue.
- **Loop nesting cap:** depth > 16 → collapse to 1 iteration + `WarnOnce` (mirror T-36-17).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MusicXML XSD schema | Hand-write POCOs from XSD | Vendor `MusicXmlSchemas` | XSD is 50+ pages; generated POCOs already exist |
| ABC tokenizer + parser | Hand-write ABC 2.1 grammar | Vendor `ABCSharp` | ABC 2.1 has 30+ header types + 50+ body tokens; hand-roll would be ~800 LOC + miss abc2midi gaps |
| XML attribute escaping | Hand-write `&amp;` escaping | `XmlWriter.WriteAttributeString` | XML escaping is subtle (`<`, `>`, `&`, `"`, control chars); BCL handles all of it |
| Key signature → fifths | Hand-write key→fifths map | Reuse `MidiExport.KeySignatureMap` | Already exists with 28+ key entries including enharmonics |

**Key insight:** Phase 28's `MidiExport.cs` already solved sequence-name → GM-program routing
+ key→fifths + voice-block per-tick walking. Plan 39-01 extracts the routing helper into
`InstrumentRouting.cs` (per D-39-20) so MusicXML + LilyPond both consume the same table —
single source of truth.

## Common Pitfalls

### Pitfall 1: MusicXML divisions confusion
**What goes wrong:** Composer writes a triplet (`DurationFraction.Denom = 3`); naive
divisions=480 truncates to integer ticks → wrong duration.
**Why it happens:** MusicXML `<duration>` is integer divisions per quarter; tuplets require
LCM-elevated divisions matching `MidiExport.ComputeRequiredTpqn`.
**How to avoid:** Reuse `MidiExport.ComputeRequiredTpqn(song)` (already public-internal — see
`flow-lang/StandardLibrary/Audio/MidiExport.cs:153`); emit `<divisions>N</divisions>` in the
first measure's `<attributes>` with the elevated value.
**Warning signs:** MuseScore opens the file but flags "duration not divisible by divisions" —
that's the symptom.

### Pitfall 2: LilyPond pitch naming (Dutch convention)
**What goes wrong:** Emit `c-sharp4` or `cis4` interchangeably; LilyPond accepts only the
Dutch form by default (`cis`, `des`, `fis`, etc.).
**Why it happens:** Flow internally stores `NoteName='C', Alteration=+1` (sharp); naive
mapping is wrong.
**How to avoid:** Map Flow `(NoteName, Alteration)` → LilyPond Dutch via a lookup table:
`+1 → "is"`, `-1 → "es"`, `0 → ""`, with double-sharps `+2 → "isis"`, double-flats `-2 → "eses"`.
**Warning signs:** `lilypond` engraver errors "unknown pitch 'c-sharp4'".

### Pitfall 3: ABC default note length (L:)
**What goes wrong:** Composer omits `L:`; ABC defaults differ by meter (`L:1/4` for meters
≥ 3/4, `L:1/8` for shorter). Hard-coding L=1/4 mangles 6/8 jigs.
**Why it happens:** ABC 2.1 §3.1.1.6 specifies the meter-dependent default — not common
knowledge.
**How to avoid:** When ABCSharp doesn't normalize this, post-parse normalization in
`AbcImport.cs`: read `M:` header, derive default `L:` per spec, apply to notes lacking
explicit length.
**Warning signs:** Imported reels render at half-speed in WAV preview.

### Pitfall 4: MML loop unrolling explosion
**What goes wrong:** Adversarial input `[[[[[[[[[[[[[[[[[[[a]2]2]2]2]2]2]2]2]2]2]2]2]2]2]2]2]2]2]2]`
= 2^20 expanded notes; OOM.
**Why it happens:** Naive recursive expansion has no bound.
**How to avoid:** Iteration-cap loops at depth 16 per D-39-19 + cap total expanded notes at
65536 (mirror generative caps from Phase 36 cellular automata).
**Warning signs:** None — must guard preemptively.

### Pitfall 5: Articulation match completeness
**What goes wrong:** `(match articulation | Accent => ... | Staccato => ... | _ => "")` —
if a new `Articulation.X` is added later and the `_` wildcard silently swallows it, MusicXML
loses the articulation.
**Why it happens:** Phase 35 `match` doesn't force exhaustiveness at this site (composer
code-style).
**How to avoid:** Explicitly enumerate ALL 7 `Articulation` enum values in the emit's match;
omit the `_` wildcard. C# compiler-level exhaustiveness check via static `switch` expression
on the enum type is the canonical Phase 39 approach in `ArticulationEmit.cs`.
**Warning signs:** New articulation added in Phase 28+ doesn't appear in MusicXML output —
silent regression.

### Pitfall 6: Two-run cmp-clean determinism
**What goes wrong:** `XmlSerializer.Serialize(POCO)` reflects fields in unspecified order;
two runs produce different attribute ordering → byte diff.
**Why it happens:** .NET reflection ordering is documented as implementation-defined.
**How to avoid:** Use `XmlWriter` directly with explicit `WriteAttributeString` calls in
fixed order. Same posture for LilyPond (StringBuilder is naturally ordered).
**Warning signs:** `tests/test_notation_to_musicxml_example.flow` two-run cmp diff fails.

## Code Examples

### Sample MusicXML emit (deterministic XmlWriter)
```csharp
// Source: hand-rolled per Pitfall 6 (no XmlSerializer reflection)
var settings = new XmlWriterSettings
{
    Indent = true,
    IndentChars = "  ",
    NewLineChars = "\n",         // fixed LF for cross-platform byte-identical
    OmitXmlDeclaration = false,
    Encoding = new UTF8Encoding(false)  // no BOM
};
using var writer = XmlWriter.Create(filepath, settings);
writer.WriteStartDocument();
writer.WriteDocType("score-partwise", "-//Recordare//DTD MusicXML 3.1 Partwise//EN",
    "http://www.musicxml.org/dtds/partwise.dtd", null);
writer.WriteStartElement("score-partwise");
writer.WriteAttributeString("version", "3.1");
// ... rest of emit
writer.WriteEndElement();  // score-partwise
writer.WriteEndDocument();
```

### Sample LilyPond pitch mapping (Dutch convention, Pitfall 2)
```csharp
// Source: hand-rolled per Pitfall 2
private static string LilyPondPitch(char noteName, int alteration, int octave)
{
    string pitch = char.ToLowerInvariant(noteName).ToString();
    string accidental = alteration switch
    {
        +2 => "isis",
        +1 => "is",
        0 => "",
        -1 => "es",
        -2 => "eses",
        _ => ""  // out-of-range — silently drop (charitable per D-v1.5-05)
    };
    // LilyPond octave convention: c' = C4, c = C3, c, = C2, etc.
    int relativeToC3 = octave - 3;
    string octaveMarker = relativeToC3 switch
    {
        > 0 => new string('\'', relativeToC3),
        < 0 => new string(',', -relativeToC3),
        _ => ""
    };
    return $"{pitch}{accidental}{octaveMarker}";
}
```

### Sample ABC charitable advisory (matches Phase 36 pattern)
```csharp
// Source: mirrors flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs:124
RenderingDiagnostics.WarnOnce(
    sentinelKey: $"abc-ornament:{token}:{lineNo}",
    message: $"[abc] dropped ornament '{token}' at line {lineNo}");
```

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| `mscore` (MuseScore CLI) | XML-02 round-trip CI gate | ✗ | — | D-39-08 charitable skip + stderr advisory |
| `lilypond` | LILY-01 engraver-compile verification | ✗ | — | Test "textually well-formed `.ly` emitted" only; skip engraver compile with advisory |
| `dotnet` 10.x | All C# build/test | ✓ | 10.0.107 | — |
| `git` | Vendor source ingestion | ✓ | — (assumed) | — |

**Missing dependencies with no fallback:** None (D-39-08 / Pitfall guidance make both binaries
optional).

**Missing dependencies with fallback:** `mscore`, `lilypond` — both run optionally in CI;
local dev unaffected. Composers who want to verify the engraver path install them separately
(documented in `examples/notation/README.md`).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) + `flow test` CLI (Phase 35 TEST-01/02) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` + `tests/test_*.flow` |
| Quick run | `dotnet test --filter "FullyQualifiedName~Phase39" --no-restore` |
| Full suite | `dotnet test --no-restore` + `dotnet run --project flow-cli -- test tests/` |
| Phase gate | All xUnit Phase39 tests GREEN + all `tests/test_notation_*.flow` GREEN |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| XML-01 | `(writeMusicXML)` emits well-formed MusicXML | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.MusicXmlExportTests"` | Wave 0 |
| XML-01 | composer-facing `tests/test_notation_to_musicxml_example.flow` round-trips two runs cmp-clean | flow test | `dotnet run --project flow-cli -- test tests/test_notation_to_musicxml_example.flow` | Wave 2 |
| XML-02 | Round-trip CI gate charitable skip when `mscore` absent | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.MusicXmlRoundTripTests"` | Wave 0 |
| LILY-01 | `(writeLilyPond)` emits textually well-formed `.ly` | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.LilyPondExportTests"` | Wave 1 |
| LILY-01 | composer-facing `tests/test_notation_to_lilypond_example.flow` two-run cmp-clean | flow test | `dotnet run --project flow-cli -- test tests/test_notation_to_lilypond_example.flow` | Wave 2 |
| ABC-01 | `(abc str)` returns Section / Array[Section] correctly | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.AbcImportTests"` | Wave 1 |
| ABC-02 | Unknown ornaments dropped with stderr advisory | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.AbcCharitableTests"` | Wave 1 |
| ABC-01 | composer-facing `tests/test_notation_from_abc_example.flow` two-run cmp-clean | flow test | `dotnet run --project flow-cli -- test tests/test_notation_from_abc_example.flow` | Wave 2 |
| MML-01 | `(mml str)` returns Sequence with correct notes / octave / tempo | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.MmlImportTests"` | Wave 2 |
| MML-01 | composer-facing `tests/test_notation_from_mml_example.flow` two-run cmp-clean | flow test | `dotnet run --project flow-cli -- test tests/test_notation_from_mml_example.flow` | Wave 2 |
| (vendor) | License audit (`Vendor/*/LICENSE` + `VENDORED-FROM.md`) | xUnit | `dotnet test --filter "FullyQualifiedName~Phase39.VendoredSourceLicenseTests"` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build flow-lang/flow-lang.csproj` (≤ 15s)
- **Per wave merge:** `dotnet test --filter "FullyQualifiedName~Phase39" --no-restore` (≤ 60s)
- **Phase gate:** Full xUnit suite + `flow test tests/` (≤ 5min)

### Wave 0 Gaps
- [x] xUnit Phase39 directory does not exist — Plan 39-01 Task 0 creates
      `flow-lang.Tests/Integration/Phase39/`
- [x] `tests/test_notation_*.flow` files do not exist — Plan 39-05 creates them
- [x] `examples/notation/` directory does not exist — Plan 39-05 creates it
- [x] `flow-lang/Vendor/` directory does not exist — Plan 39-01 Task 1 creates it

## Security Domain

> `workflow.security_enforcement` is presumed enabled (Flow's default; not explicitly disabled
> in `.planning/config.json`). Phase 39's threat surface is **file I/O + parser input
> handling**.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | **yes** | ABC + MML tokenizers consume arbitrary composer-supplied strings; loop-depth cap (D-39-19, T-36-17 mirror) + total-note cap (Pitfall 4) gate DoS |
| V6 Cryptography | no | — |
| V12 File Handling | **yes** | `(writeMusicXML "path")` and `(writeLilyPond "path")` write composer-controlled paths — accept by analogy with `writeWav` / `writeMidi` posture (existing `T-32-IO-01` "accept" disposition) |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| MML loop bomb (`[[...]2]2...`) | DoS | D-39-19 nesting cap = 16 + total-expansion cap = 65536 (Pitfall 4) |
| ABC malformed input → parser crash | DoS | Charitable interpretation D-39-17 — never throw; `WarnOnce` + return usable Section |
| Path traversal via `writeMusicXML "../etc/passwd"` | Tampering | Accept per `writeWav` precedent; advisory: future Phase 41 may add `flow security` sandbox |
| XML entity expansion (XXE) on round-trip read | Tampering | `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }` for the XML-02 round-trip CI gate test |

## Sources

### Primary (HIGH confidence)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` (652 lines, in-repo) — multi-track export
  walking pattern; reuse `ResolveGmProgram` + `ComputeRequiredTpqn` + `KeySignatureMap`
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` (302 lines) — text-parser template
- `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (623 lines) — larger text-parser template
  with `<region>` / opcode handling — model for MML's command grammar
- `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs` (276 lines) — context-aware builtin
  registration model for `@notation-io`
- CONTEXT D-39-01..22 (locked at phase boundary) — single source of truth
- `https://raw.githubusercontent.com/sightreader/musicxml-schemas/master/LICENSE` (MIT,
  verified 2026-05-23)
- `https://raw.githubusercontent.com/matthewcpp/ABCSharp/master/LICENSE` (MIT, verified
  2026-05-23)

### Secondary (MEDIUM confidence)
- MusicXML 3.1 spec (`https://www.w3.org/2021/06/musicxml40/` covers the family; 3.1
  reference at `https://www.w3.org/2017/12/musicxml31/`) — used to size emit work
- LilyPond 2.24 user manual (`https://lilypond.org/doc/v2.24/`) — Dutch pitch naming,
  `\tuplet`, `\new Voice`
- ABC 2.1 specification (`https://abcnotation.com/wiki/abc:standard:v2.1`) — header set,
  body grammar
- PC-98 MML PMD/MUCOM reference (`http://www.tcat.ne.jp/~kihachi/pmd_e.htm`) — loop semantics

### Tertiary (LOW confidence)
- None — every claim above is either in-repo code, verified upstream license, or canonical
  spec.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | MuseScore 4.x renders decimal `<alter>` cent values without warning (D-39-06 implication) | XML-01 emit | LOW — composer can post-edit `.musicxml` to text annotation if MuseScore complains; charitable failure mode |
| A2 | ABCSharp's `Q:` tempo handles `Q:"Allegro" 1/4=120` annotated form | ABC parser | MEDIUM — if it doesn't, Plan 39-03 hand-fills in `AbcImport.cs` post-parse normalization (covered by Claude's Discretion above) |
| A3 | MML nested-loop semantics is inner-each-time across PC-98 dialects (PMD ref above) | MML parser | LOW — composer-facing tests will catch divergence; charitable post-correction in v1.6 |

## Open Questions

1. **MusicXML POCO vendoring necessity** — Will the XML-02 round-trip test actually use the
   POCOs, or is structural diff via `XDocument` enough? Resolve at Plan 39-01 vendor-audit
   step; if POCOs aren't consumed, drop the `MusicXmlSchemas` vendor entirely (saves ~1MB +
   eliminates one license-audit gate).
2. **LilyPond engraver-compile in CI** — If `lilypond` is available in a future CI container,
   we'd want the engraver-compile test to gate. For v1.5 it stays opt-in via env var
   `FLOW_LILYPOND_GATE=1`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every library is BCL or vendored MIT
- Architecture: HIGH — pattern matches MidiExport (Phase 28) + ScalaBuiltins (Phase 32)
  + SfzBuiltins (Phase 33) precedents
- Pitfalls: HIGH — 6 pitfalls captured from MusicXML spec reading, LilyPond Dutch convention,
  ABC L: default rules, MML DoS surface, articulation match exhaustiveness, two-run
  cmp-clean determinism

**Research date:** 2026-05-23
**Valid until:** 2026-06-22 (30 days — vendored licenses are stable; spec links are
canonical)
