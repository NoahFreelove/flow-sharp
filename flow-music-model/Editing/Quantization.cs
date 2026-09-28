namespace Flow.Music.Model.Editing;

/// <summary>
/// Grid, strength (0 = unchanged, 1 = hard snap) and swing (-1..1; odd grid
/// positions move by swing × grid / 2). Units are quarter notes.
/// </summary>
public sealed record QuantizeSettings(double GridQuarters, double Strength = 1, double Swing = 0);

/// <summary>
/// The one quantize implementation shared by Flow's <c>quantize</c> builtin and
/// editing hosts. Positions are relative to their bar's downbeat, so the grid
/// follows meters and pickups. The result depends only on a note's real onset:
/// notes sharing an onset (chords) move together.
/// </summary>
public static class Quantization
{
    public static double Onset(double barRelativeOnset, QuantizeSettings settings)
    {
        Validate(settings);
        if (!double.IsFinite(barRelativeOnset)) throw new ArgumentOutOfRangeException(nameof(barRelativeOnset));
        double grid = settings.GridQuarters;
        // Halfway between grid points snaps later.
        double index = Math.Floor(barRelativeOnset / grid + 0.5);
        double target = index * grid;
        if (Math.Abs(index % 2) == 1) target += settings.Swing * (grid / 2.0);
        return barRelativeOnset + settings.Strength * (target - barRelativeOnset);
    }

    /// <summary>
    /// Returns a copy with every event (rests included) quantized against the bar that
    /// contains its current onset; onsets before the first bar use the first bar.
    /// Identity, duration and all other note properties are preserved.
    /// </summary>
    public static SequenceSnapshot Apply(SequenceSnapshot sequence, QuantizeSettings settings,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        Validate(settings);
        var notes = new NoteEvent[sequence.Notes.Count];
        for (int i = 0; i < notes.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var note = sequence.Notes[i];
            double downbeat = BarStart(sequence.Bars, note.OffsetQuarters);
            notes[i] = note with { OffsetQuarters = downbeat + Onset(note.OffsetQuarters - downbeat, settings) };
        }
        return new(sequence.Id, sequence.Name, sequence.DurationQuarters, notes, sequence.Bars);
    }

    private static double BarStart(IReadOnlyList<BarSpan> bars, double offset)
    {
        double start = bars.Count > 0 ? bars[0].OffsetQuarters : 0;
        foreach (var bar in bars)
        {
            if (bar.OffsetQuarters > offset) break;
            start = bar.OffsetQuarters;
        }
        return start;
    }

    private static void Validate(QuantizeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(settings.GridQuarters) || settings.GridQuarters <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Grid must be positive and finite");
        if (!(settings.Strength is >= 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(settings), "Strength must be in [0, 1]");
        if (!(settings.Swing is >= -1 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(settings), "Swing must be in [-1, 1]");
    }
}
