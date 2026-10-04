namespace Flow.Studio.Engine;

public sealed record RecordedMidiNote(int Channel, int Note, int Velocity, long StartFrame, long EndFrame);

/// <summary>Control-owner note pairing in an explicit monotonic recording clock.
/// Repeated channel/pitch retriggers close the older note; sustain extends key releases.
/// Faulted takes cannot be committed. No quantization or transport mapping occurs here.</summary>
public sealed class MidiNoteRecording
{
    private struct Held { internal bool Active, Released; internal long Start; internal byte Velocity; }
    private readonly Held[] _held = new Held[16 * 128];
    private readonly bool[] _sustain = new bool[16];
    private readonly List<RecordedMidiNote> _notes = new();
    private readonly int _limit;
    private long _frame;
    private bool _finished;
    public bool IsFaulted { get; private set; }
    public int DroppedZeroLengthNotes { get; private set; }
    public MidiNoteRecording(long startFrame, int maxNotes = 100000)
    {
        if (startFrame < 0 || maxNotes is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(startFrame));
        _frame = startFrame; _limit = maxNotes;
    }
    public void ReportInputLoss() => IsFaulted = true;
    public void Process(MidiInputEvent message)
    {
        Check(); message.Validate();
        if (message.Frame < _frame) { IsFaulted = true; throw new ArgumentException("Recording clock moved backward"); }
        _frame = message.Frame;
        int channel = message.Status & 15, kind = message.Status & 0xf0, index = channel * 128 + message.Data1;
        if (kind == 0x90 && message.Data2 > 0)
        {
            Close(index, message.Frame);
            _held[index] = new() { Active = true, Start = message.Frame, Velocity = message.Data2 };
        }
        else if (kind == 0x80 || kind == 0x90)
        {
            if (_sustain[channel]) _held[index].Released = true;
            else Close(index, message.Frame);
        }
        else if (kind == 0xb0 && message.Data1 == 64)
        {
            _sustain[channel] = message.Data2 >= 64;
            if (!_sustain[channel])
                for (int note = 0; note < 128; note++) if (_held[channel * 128 + note].Released) Close(channel * 128 + note, message.Frame);
        }
        else if (kind == 0xb0 && message.Data1 is 120 or 123)
        {
            for (int note = 0; note < 128; note++)
            {
                int slot = channel * 128 + note;
                if (message.Data1 == 123 && _sustain[channel]) _held[slot].Released = true;
                else Close(slot, message.Frame);
            }
        }
    }
    private void Close(int index, long frame)
    {
        ref var held = ref _held[index];
        if (!held.Active) return;
        if (frame > held.Start)
        {
            if (_notes.Count == _limit) { IsFaulted = true; throw new InvalidOperationException("Recording note budget exceeded"); }
            _notes.Add(new(index / 128, index % 128, held.Velocity, held.Start, frame));
        }
        else DroppedZeroLengthNotes++;
        held = default;
    }
    public IReadOnlyList<RecordedMidiNote> Complete(long endFrame)
    {
        Check();
        if (endFrame < _frame) throw new ArgumentOutOfRangeException(nameof(endFrame));
        for (int i = 0; i < _held.Length; i++) Close(i, endFrame);
        _finished = true;
        return Array.AsReadOnly(_notes.OrderBy(n => n.StartFrame).ThenBy(n => n.Channel).ThenBy(n => n.Note).ToArray());
    }
    private void Check()
    {
        if (IsFaulted || _finished) throw new InvalidOperationException("Recording is faulted or already completed");
    }
}
