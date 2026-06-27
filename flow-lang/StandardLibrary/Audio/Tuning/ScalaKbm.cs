using System.Collections.Generic;
using System.Globalization;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Resolved <c>.kbm</c> keyboard mapping. Per CONTEXT D-05 the internal model is
/// ALWAYS "has KBM" — when no .kbm file is loaded, <see cref="ScalaKbmParser.Default"/>
/// produces a synthetic linear mapping that auto-adopts the tuning's period (D-07).
///
/// Fields mirror the Huygens-Fokker spec's 7-field header (RESEARCH §.kbm Format
/// Reference) plus the trailing mapping entries:
///   1. Size           — # of mapping entries that follow (0 = linear mapping)
///   2. FirstMidi      — lowest MIDI key the mapping applies to (0..127)
///   3. LastMidi       — highest MIDI key the mapping applies to (0..127)
///   4. MiddleNote     — MIDI key where the mapping's first entry / degree 0 lands
///   5. ReferenceNote  — MIDI key assigned the reference frequency
///   6. ReferenceHz    — frequency assigned to ReferenceNote (typically 440.0)
///   7. FormalOctave   — scale degree treated as the formal period (Phase 32 rejects
///                       non-zero; 0 means "use the .scl's period as the wrap point")
///   8. Mapping        — Size-many entries; null = unmapped (`x`), int = scale degree
/// Plus the convenience mirror <see cref="Period"/> populated by the parent .scl's
/// PeriodCents per D-07.
/// </summary>
public sealed class ScalaKbm
{
    public int Size { get; }
    public int FirstMidi { get; }
    public int LastMidi { get; }
    public int MiddleNote { get; }
    public int ReferenceNote { get; }
    public double ReferenceHz { get; }
    public int FormalOctave { get; }
    public IReadOnlyList<int?> Mapping { get; }
    public double Period { get; }

    public ScalaKbm(
        int size,
        int firstMidi,
        int lastMidi,
        int middleNote,
        int referenceNote,
        double referenceHz,
        int formalOctave,
        IReadOnlyList<int?> mapping,
        double period)
    {
        Size = size;
        FirstMidi = firstMidi;
        LastMidi = lastMidi;
        MiddleNote = middleNote;
        ReferenceNote = referenceNote;
        ReferenceHz = referenceHz;
        FormalOctave = formalOctave;
        Mapping = mapping;
        Period = period;
    }

    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "ScalaKbm(size={0}, middle={1}, ref={2}@{3:F2}Hz, period={4:F2}¢)",
        Size, MiddleNote, ReferenceNote, ReferenceHz, Period);
}
