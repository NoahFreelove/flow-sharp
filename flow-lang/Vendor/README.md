# Vendored Sources

This directory hosts third-party source code vendored into Flow alongside its own
implementation. Each vendored source ships under its own subdirectory with two
discipline files:

- `LICENSE` — verbatim copy of the upstream license text.
- `VENDORED-FROM.md` — upstream URL, commit SHA, ingestion date, and a list of any
  local modifications Flow made on top of the source.

This mirrors the discipline established in Phase 29 for `flow-lang/Samples/`
(CC-BY 4.0 audio assets with per-instrument `LICENSE.md` + `CREDITS.md` aggregation).

## Current state

- **MusicXmlSchemas: NOT vendored** (Plan 39-01 decision — the XML-02 MusicXML
  round-trip CI gate uses `System.Xml.Linq.XDocument` structural diff against
  MuseScore's output, which is sufficient without typed POCOs. `sightreader/musicxml-schemas`
  is MIT-licensed and was the candidate vendor per CONTEXT D-39-03; the actual emit
  path is hand-rolled `System.Xml.XmlWriter` in `flow-lang/StandardLibrary/Notation/MusicXmlExport.cs`
  per Pitfall 6 — `XmlSerializer` reflection ordering is non-deterministic across .NET
  patches and would break the two-run cmp-clean determinism contract.)
- **ABCSharp: NOT vendored** (Plan 39-03 decision — Flow's ABC needs are narrow
  enough — ABC 2.1 core + abc2midi `Q:` + modal keys — that a hand-rolled lexer +
  parser at `flow-lang/StandardLibrary/Notation/AbcLexer.cs` + `AbcImport.cs` is
  lower-friction than ingesting a 5000-line third-party dep. `matthewcpp/ABCSharp` is
  MIT-licensed and was the candidate vendor per CONTEXT D-39-04; the hand-rolled
  alternative is shorter to maintain and stays charitable per D-39-17 / D-v1.5-05.)

Currently no source-code dependencies live under this directory.
