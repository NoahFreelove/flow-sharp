# Phase 39: Notation Citizenship - Pattern Map

**Mapped:** 2026-05-23

Files to be created/modified, each classified by role + closest existing analog + concrete
excerpts to copy/adapt.

## File-to-Analog Map

| New / Modified File | Role | Closest Analog | Why That Analog |
|---------------------|------|----------------|-----------------|
| `flow-lang/Vendor/MusicXmlSchemas/` (vendored) | Third-party POCOs | `flow-lang/Samples/` (Phase 29 CC-BY bundle) | Vendored content with `LICENSE` + per-asset attribution; license-audit gate test pattern |
| `flow-lang/Vendor/MusicXmlSchemas/VENDORED-FROM.md` | Vendor provenance | (NEW pattern) | First vendored source-code dep; sets the discipline |
| `flow-lang/Vendor/ABCSharp/` (vendored) | Third-party C# parser | `flow-lang/Vendor/MusicXmlSchemas/` (sibling) | Same vendor discipline |
| `flow-lang/StandardLibrary/Notation/` (new dir) | Stdlib subfolder | `flow-lang/StandardLibrary/Audio/Sfz/`, `Audio/Tuning/` | Sibling folders for domain-grouped builtins |
| `flow-lang/StandardLibrary/Notation/InstrumentRouting.cs` | Shared helper | `flow-lang/StandardLibrary/Audio/MidiExport.cs:99-140` (`ResolveGmProgram`) | D-39-20 extraction — MusicXML + LilyPond both consume |
| `flow-lang/StandardLibrary/Notation/NotationIoBuiltins.cs` | Builtin registration | `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs` | `@`-module-gated builtin set with marker init |
| `flow-lang/StandardLibrary/Notation/MusicXmlExport.cs` | XML emit | `flow-lang/StandardLibrary/Audio/MidiExport.cs` | Walks the same `SongData→...→MusicalNoteData` tree |
| `flow-lang/StandardLibrary/Notation/LilyPondExport.cs` | LilyPond text emit | `flow-lang/StandardLibrary/Audio/MidiExport.cs` | Same walk; StringBuilder instead of `MidiFile` |
| `flow-lang/StandardLibrary/Notation/AbcImport.cs` | ABC adapter | `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (read+parse style) | Structured text parser with charitable advisories |
| `flow-lang/StandardLibrary/Notation/MmlImport.cs` | MML hand-rolled parser | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` (small, focused) | Small text format, recursive-descent |
| `flow-lang/StandardLibrary/Notation/ArticulationEmit.cs` | Phase 35 `match` consumer | (no analog — Phase 39 origin) | D-39-21 dependency-root contract |
| `flow-lang/notation-io.flow` | Stdlib module entry | `flow-lang/sfz.flow` | `use "@notation-io"` activator + forward decls |
| `flow-lang/Core/FlowEngine.cs` | Registration hook | (modify; FlowEngine ctor lines 100-200) | Add `NotationIoBuiltins.Register(...)` next to `SfzBuiltins.Register` |
| `flow-lang/flow-lang.csproj` | Embedded resource | (modify) | Add `<None Update="notation-io.flow">` with `CopyToOutputDirectory` |
| `flow-lang.Tests/Integration/Phase39/` (new dir) | xUnit gate dir | `flow-lang.Tests/Integration/Phase29/` | Sibling phase test layout |
| `flow-lang.Tests/Integration/Phase39/VendoredSourceLicenseTests.cs` | License audit | `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs:13-83` | File-existence + content assertions, no network |
| `flow-lang.Tests/Integration/Phase39/MusicXmlExportTests.cs` | Emit gate | (no analog — Phase 39 origin) | Emit + XSD-shape validation + two-run cmp-clean |
| `flow-lang.Tests/Integration/Phase39/MusicXmlRoundTripTests.cs` | XML-02 CI gate | (no analog) | `mscore --convert-to mxl` skip-when-absent pattern |
| `flow-lang.Tests/Integration/Phase39/LilyPondExportTests.cs` | Emit gate | (no analog) | Textual well-formedness + Dutch-pitch round-trip |
| `flow-lang.Tests/Integration/Phase39/AbcImportTests.cs` | Import gate | (no analog) | Single/multi-tune dispatch + charitable advisory |
| `flow-lang.Tests/Integration/Phase39/AbcCharitableTests.cs` | Charitable gate | `flow-lang.Tests/Integration/Phase37/` (advisory pattern) | Unknown-ornament drop without throw |
| `flow-lang.Tests/Integration/Phase39/MmlImportTests.cs` | Import gate | (no analog) | Hand-rolled tokenizer correctness + DoS cap |
| `tests/test_notation_to_musicxml_example.flow` | Composer regression | `tests/test_test_library.flow` (Phase 35 framework) | `(test "name" lazy(...))` shape |
| `tests/test_notation_to_lilypond_example.flow` | Composer regression | (sibling) | (sibling) |
| `tests/test_notation_from_abc_example.flow` | Composer regression | (sibling) | (sibling) |
| `tests/test_notation_from_mml_example.flow` | Composer regression | (sibling) | (sibling) |
| `examples/notation/to_musicxml.flow` | Tutorial chapter | `examples/generative/markov_jazz.flow`, `examples/sections/parameterized.flow` | Self-contained runnable demo |
| `examples/notation/to_lilypond.flow` | Tutorial chapter | (sibling) | (sibling) |
| `examples/notation/from_abc.flow` | Tutorial chapter | (sibling) | (sibling) |
| `examples/notation/from_mml.flow` | Tutorial chapter | (sibling) | (sibling) |
| `examples/notation/README.md` | Chapter overview | `examples/symphony/README.md` | Domain intro + dependency notes |
| `CLAUDE.md` | Project doc | (modify; add `@notation-io` to Standard Library Modules table) | Single line + section |
| `.planning/REQUIREMENTS.md` | Req sheet | (mark XML-01/02/LILY-01/ABC-01/02/MML-01 as IMPLEMENTED) | Sweep at Plan 39-05 |
| `.planning/ROADMAP.md` | Roadmap | (mark Phase 39 as COMPLETE) | Sweep at Plan 39-05 |

## Concrete Code Excerpts

### From `MidiExport.cs:99-140` (D-39-20 extract target)
```csharp
public static (int gmProgram, int channel) ResolveGmProgram(string seqName)
{
    if (string.IsNullOrEmpty(seqName)) return (0, 0);
    string stripped = StripSamplerPrefix(seqName);
    string lower = stripped.ToLowerInvariant();
    if (lower.StartsWith("violin"))      return (40, 0);
    if (lower.StartsWith("viola"))       return (41, 0);
    // ... (existing 17 entries)
    return (0, 0);
}
```
Plan 39-01 Task 2 lifts this into `Notation/InstrumentRouting.cs` and `MidiExport.cs`
delegates to it (preserves Phase 28 byte-identical contract).

### From `SfzBuiltins.cs:227-275` (registration template)
The `Register(InternalFunctionRegistry, ExecutionContext)` shape + the
`__enableSfzModule(Dict)` marker pattern transfers verbatim to
`NotationIoBuiltins.Register` + `__enableNotationIoModule()` marker.

### From `LicenseAuditTests.cs:13-83`
Pure file-existence + content-string assertions. Plan 39-01 / 39-03 add per-vendor
counterparts: `VendoredSourceLicenseTests.MusicXmlSchemas_HasLicense()` and
`VendoredSourceLicenseTests.ABCSharp_HasLicense()`.

### From `ScalaParser.cs:1-302` (text-parser template for MML)
Line-by-line state machine + `ParseException` carrying file path + line number + charitable
return-empty on unrecognized headers. MML's loop-cap counter slots into the existing iteration
structure.
