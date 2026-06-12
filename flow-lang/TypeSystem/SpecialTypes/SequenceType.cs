using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Runtime representation of a Sequence containing ordered bars.
/// </summary>
public class SequenceData
{
    /// <summary>
    /// The ordered list of bars in this sequence.
    /// </summary>
    public List<BarData> Bars { get; }

    /// <summary>
    /// The total duration of the sequence in beats.
    /// </summary>
    public double TotalBeats { get; private set; }

    public SequenceData()
    {
        Bars = new List<BarData>();
        TotalBeats = 0;
    }

    /// <summary>
    /// Adds a bar to the sequence.
    /// The bar will be positioned after the last bar in the sequence.
    /// </summary>
    public void AddBar(BarData bar)
    {
        if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
        {
            throw new InvalidOperationException("Only musical bars with time signatures can be added to sequences");
        }

        Bars.Add(bar);
        TotalBeats += BarLengthBeats(bar);
    }

    /// <summary>
    /// The length, in beats, that a bar contributes to the sequence timeline.
    ///
    /// A monophonic note stream packs all its notes into a single bar, which can
    /// legitimately exceed the time-signature numerator (e.g.
    /// <c>| C4q D4q E4q F4q G4q A4q B4q C5h |</c> is 9 beats in 4/4). Reporting the bare
    /// numerator under-counted such bars, so MixVoicesToBuffer truncated the render to one
    /// bar and trailing notes were dropped — the "note streams cut off after ~1 bar" bug.
    /// <see cref="BarData.GetActualBeats"/> gives the true sequential length, and Math.Max
    /// preserves full-bar padding for underfull bars (no layout regression / two-run
    /// determinism preserved).
    ///
    /// Bars containing parallel <c>{voice}</c> blocks keep the time-signature length:
    /// GetActualBeats() SUMS the simultaneous voices (plus the placeholder full-bar rest
    /// the compiler inserts for voice-only bars), which over-counts a bar whose voices
    /// actually fit. Overfull parallel voices are out of scope for this fix.
    /// </summary>
    private static double BarLengthBeats(BarData bar)
    {
        if (bar.IsPickup)
            return bar.GetActualBeats();
        double numerator = bar.TimeSignature!.Numerator;
        if (bar.ParallelVoices is { Count: > 0 })
            return numerator;
        return Math.Max(numerator, bar.GetActualBeats());
    }

    /// <summary>
    /// Converts the sequence to a timeline with each bar's offset in beats.
    /// </summary>
    public List<(BarData bar, double offsetBeats)> ToTimeline()
    {
        var timeline = new List<(BarData, double)>();
        double offset = 0;

        foreach (var bar in Bars)
        {
            timeline.Add((bar, offset));
            if (bar.TimeSignature != null)
            {
                // Mirror AddBar: an overfull monophonic bar must advance the next bar's
                // offset by its real length, not the numerator. (See BarLengthBeats.)
                offset += BarLengthBeats(bar);
            }
        }

        return timeline;
    }

    /// <summary>
    /// Returns the number of bars in this sequence.
    /// </summary>
    public int Count => Bars.Count;

    /// <summary>
    /// Formats the sequence as a string.
    /// </summary>
    public override string ToString()
    {
        return $"Sequence[{Count} bars, {TotalBeats} beats total]";
    }
}

/// <summary>
/// Represents a sequence of musical bars.
/// </summary>
public sealed class SequenceType : FlowType
{
    private SequenceType() { }

    public static SequenceType Instance { get; } = new();

    public override string Name => "Sequence";

    public override int GetSpecificity() => 134;
}
