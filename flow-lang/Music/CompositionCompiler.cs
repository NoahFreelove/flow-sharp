using System.Security.Cryptography;
using System.Text;
using Flow.Music.Model;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Music;

/// <summary>
/// Copies an already evaluated song into a host-neutral score. The caller owns
/// the mutable input for the duration of conversion. Never executes a
/// section body or retains AST, Value, scope, mutable note lists or tuning tables.
/// IDs are deterministic for a source identity and structural path; authoring hosts
/// should supply their own persistent IDs when editing/inserting/reordering events.
/// </summary>
public static class CompositionCompiler
{
    public static CompositionSnapshot Compile(SongData song, string sourceIdentity,
        CancellationToken cancellation = default)
    {
        var sections = new Dictionary<string, SectionSnapshot>(StringComparer.Ordinal);
        var placements = new List<SectionPlacement>();
        for (int index = 0; index < song.Sections.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var reference = song.Sections[index];
            if (!song.SectionRegistry.TryGetValue(reference.Name, out var section))
                throw new InvalidOperationException($"Section '{reference.Name}' not found in song registry");
            if (!sections.TryGetValue(reference.Name, out var snapshot))
            {
                snapshot = CompileSection(section, sourceIdentity + "/section/" + Uri.EscapeDataString(reference.Name), cancellation);
                sections.Add(reference.Name, snapshot);
            }
            placements.Add(new(Id($"{sourceIdentity}/placement/{index}"), snapshot, reference.RepeatCount));
        }
        return new(Id(sourceIdentity), placements);
    }

    private static SectionSnapshot CompileSection(SectionData section, string identity, CancellationToken cancellation)
    {
        var context = section.Context;
        var tuning = SongRenderer.ResolveRenderTuning(context);
        var sequences = new List<SequenceSnapshot>();
        foreach (var (name, sequence) in section.Sequences)
        {
            var path = identity + "/sequence/" + Uri.EscapeDataString(name);
            var notes = new List<NoteEvent>();
            var bars = new List<BarSpan>();
            var timeline = sequence.ToTimeline();
            for (int index = 0; index < timeline.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                var (bar, offset) = timeline[index];
                var meter = bar.TimeSignature ?? throw new InvalidOperationException("A score bar requires a time signature");
                var duration = (index + 1 < timeline.Count ? timeline[index + 1].offsetBeats : sequence.TotalBeats) - offset;
                var barPath = $"{path}/bar/{index}";
                bars.Add(new(Id(barPath), offset, duration, meter.Numerator, meter.Denominator, bar.IsPickup));
                CompileBar(bar, offset, barPath, tuning, notes, cancellation, meter.Denominator);
            }
            sequences.Add(new(Id(path), name, sequence.TotalBeats, notes, bars));
        }
        return new(Id(identity), section.Name,
            new(context?.Tempo ?? 120, context?.Pan ?? 0, context?.Gain ?? 1,
                context?.ReverbTime, context?.VoicePoolSize, context?.SustainPedal ?? false,
                context?.Key, context?.Swing), sequences, Origin(section.SourceLocation));
    }

    private static void CompileBar(BarData bar, double offset, string path, RenderTuning tuning,
        List<NoteEvent> result, CancellationToken cancellation, int inheritedDenominator)
    {
        cancellation.ThrowIfCancellationRequested();
        var denominator = bar.TimeSignature?.Denominator ?? inheritedDenominator;
        if (bar.ParallelVoices is { Count: > 0 } voices)
        {
            for (int index = 0; index < voices.Count; index++)
                CompileBar(voices[index], offset, $"{path}/voice/{index}", tuning, result, cancellation, denominator);
            return;
        }
        // Mirror BarData.ToTimeline without mutating a child bar to inherit its meter.
        double cursor = 0, lead = 0;
        for (int index = 0; index < bar.MusicalNotes.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var note = bar.MusicalNotes[index];
            var onset = (note.IsChordTone ? lead : cursor) + note.OnsetOffset;
            var duration = note.GetBeats(denominator);
            if (!note.IsChordTone) { lead = cursor; cursor += duration; }
            var pitch = note.IsRest ? null : new NotePitch(note.NoteName, note.Octave, note.Alteration,
                note.CentOffset, PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration),
                PitchConversion.NoteToFrequency(note, tuning));
            var exact = note.DurationFraction is { } fraction ? new RationalDuration(fraction.Num, fraction.Denom) : null;
            result.Add(new(Id($"{path}/note/{index}"), path, offset + onset, duration, pitch,
                note.Velocity, Enum.Parse<NoteArticulation>(note.Articulation.ToString()), note.IsTied,
                note.DurationOverlap, note.PortamentoMs, exact, Origin(note.SourceLocation, note.SourceLength)));
        }
    }

    private static SourceOrigin? Origin(SourceLocation? source, int length = 0) => source is null ? null
        : new(source.FileName, source.Line, source.Column, length);

    private static Guid Id(string path) => new(SHA256.HashData(Encoding.UTF8.GetBytes(path)).AsSpan(0, 16));
}
