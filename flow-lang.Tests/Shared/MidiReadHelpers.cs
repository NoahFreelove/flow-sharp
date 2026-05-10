using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace FlowLang.Tests.Shared;

internal static class MidiReadHelpers
{
    public static byte[] GetVelocityBytes(string midiPath)
    {
        var midiFile = MidiFile.Read(midiPath);
        return midiFile.GetNotes().Select(n => (byte)n.Velocity).ToArray();
    }

    public static int[] GetNoteNumbers(string midiPath)
    {
        var midiFile = MidiFile.Read(midiPath);
        return midiFile.GetNotes().Select(n => (int)n.NoteNumber).ToArray();
    }

    public static byte[] ReadAllBytes(string midiPath) => File.ReadAllBytes(midiPath);
}
