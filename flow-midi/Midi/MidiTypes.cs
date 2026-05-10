namespace FlowMidi.Midi;

record MidiFile(int Format, int TicksPerQuarterNote, List<MidiTrack> Tracks);

record MidiTrack(string? Name, List<MidiEvent> Events);

abstract record MidiEvent(long AbsoluteTick);

record NoteOnEvent(long AbsoluteTick, int Channel, int Pitch, int Velocity) : MidiEvent(AbsoluteTick);

record NoteOffEvent(long AbsoluteTick, int Channel, int Pitch) : MidiEvent(AbsoluteTick);

record TempoEvent(long AbsoluteTick, int MicrosecondsPerBeat) : MidiEvent(AbsoluteTick)
{
    public double Bpm => 60_000_000.0 / MicrosecondsPerBeat;
}

record TimeSignatureEvent(long AbsoluteTick, int Numerator, int Denominator) : MidiEvent(AbsoluteTick);

record KeySignatureEvent(long AbsoluteTick, int SharpsFlats, bool IsMinor) : MidiEvent(AbsoluteTick);

record ProgramChangeEvent(long AbsoluteTick, int Channel, int Program) : MidiEvent(AbsoluteTick);
