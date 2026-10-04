using Flow.Music.Model;
using Flow.Studio.Model;
namespace Flow.Studio.Engine;

/// <summary>Captured straight-through take against one project snapshot. MIDI pitch
/// uses the captured pitch map; channel identity is retained in VoiceId. No implicit quantization.</summary>
public sealed class MidiRecordingTake
{
    private readonly ProjectSnapshot _expected;
    private bool _committed;
    public Guid SourceId { get; } = Guid.NewGuid();
    public Guid ClipId { get; } = Guid.NewGuid();
    public double AnchorQuarters { get; }
    public double LengthQuarters { get; }
    public IReadOnlyList<NoteEvent> Notes { get; }
    public MidiRecordingTake(ProjectSnapshot expected, IEnumerable<RecordedMidiNote> notes,
        long captureStartFrame, long captureEndFrame, long projectStartFrame, int sampleRate, int inputLatencyFrames = 0, MidiPitchMap? midiPitchMap = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (captureStartFrame < 0 || captureEndFrame <= captureStartFrame || projectStartFrame < 0 ||
            sampleRate is < 1 or > 384000 || inputLatencyFrames < 0 || inputLatencyFrames > (long)sampleRate * 10)
            throw new ArgumentException("Invalid recording timeline");
        if (midiPitchMap is null && (expected.Context.Tuning.Scala is not null || expected.Context.Tuning.System != "EqualTemperament"))
            throw new ArgumentException("Recording requires a resolved MIDI pitch map for this tuning");
        midiPitchMap ??= MidiPitchMap.EqualTemperament;
        _expected = expected;
        long Map(long frame) => checked(projectStartFrame + (frame - captureStartFrame) - inputLatencyFrames);
        long start = Math.Max(0, Map(captureStartFrame)), end = Math.Max(0, Map(captureEndFrame));
        var tempo = expected.Arrangement.Tempo;
        AnchorQuarters = tempo.QuarterAt((double)start / sampleRate);
        LengthQuarters = tempo.QuarterAt((double)end / sampleRate) - AnchorQuarters;
        var input = notes.Take(100001).ToArray();
        if (input.Length > 100000) throw new ArgumentException("Recording note budget exceeded");
        var result = new List<NoteEvent>();
        foreach (var note in input)
        {
            if (note is null || note.Channel is < 0 or > 15 || note.Note is < 0 or > 127 || note.Velocity is < 1 or > 127 ||
                note.StartFrame < captureStartFrame || note.EndFrame > captureEndFrame || note.EndFrame <= note.StartFrame)
                throw new ArgumentException("Invalid recorded note");
            long from = Math.Max(0, Map(note.StartFrame)), to = Math.Max(0, Map(note.EndFrame));
            if (to <= from) continue; // Latency compensation can place a note wholly before zero.
            double offset = tempo.QuarterAt((double)from / sampleRate), finish = tempo.QuarterAt((double)to / sampleRate);
            if (midiPitchMap[note.Note] == 0) continue; // Unmapped keys are silent in monitoring too.
            const string letters = "CCDDEFFGGAAB";
            int pitchClass = note.Note % 12;
            int alteration = pitchClass is 1 or 3 or 6 or 8 or 10 ? 1 : 0;
            var pitch = new NotePitch(letters[pitchClass], note.Note / 12 - 1, alteration, null, note.Note,
                midiPitchMap[note.Note]);
            result.Add(new(Guid.NewGuid(), "midi-channel-" + (note.Channel + 1), offset - AnchorQuarters, finish - offset,
                pitch, note.Velocity / 127.0));
        }
        Notes = Array.AsReadOnly(result.OrderBy(n => n.OffsetQuarters).ToArray());
    }
    public bool Commit(ProjectDocument document, Guid trackId, string name = "Recorded notes")
    {
        if (_committed) throw new InvalidOperationException("Take has already been committed");
        if (!ReferenceEquals(document.Snapshot, _expected)) throw new InvalidOperationException("Project changed during recording; take requires explicit rebase");
        if (Notes.Count == 0 || LengthQuarters <= 0) return false;
        ProjectNoteCommands.CreateFromNotes(document, SourceId, ClipId, trackId, AnchorQuarters, LengthQuarters, Notes, name);
        _committed = true; return true;
    }
}
