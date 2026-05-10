using System.Text;

namespace FlowMidi.Midi;

/// <summary>
/// Hand-rolled binary MIDI file parser. Handles Format 0 and 1 files with
/// note on/off, tempo, time signature, key signature, and program change events.
/// </summary>
static class MidiParser
{
    public static MidiFile Parse(byte[] data)
    {
        int pos = 0;

        // Header chunk: "MThd" + 4-byte length + format + nTracks + division
        string headerTag = ReadString(data, ref pos, 4);
        if (headerTag != "MThd")
            throw new FormatException($"Invalid MIDI file: expected 'MThd', got '{headerTag}'");

        int headerLength = ReadInt32BE(data, ref pos);
        int headerStart = pos;

        int format = ReadInt16BE(data, ref pos);
        int numTracks = ReadInt16BE(data, ref pos);
        int division = ReadInt16BE(data, ref pos);

        // Skip any extra header bytes beyond the standard 6
        pos = headerStart + headerLength;

        if ((division & 0x8000) != 0)
            throw new NotSupportedException("SMPTE time division is not supported; only ticks-per-quarter-note MIDI files are supported.");

        int ticksPerQuarterNote = division;

        var tracks = new List<MidiTrack>();
        for (int t = 0; t < numTracks; t++)
        {
            if (pos >= data.Length)
                break;

            string trackTag = ReadString(data, ref pos, 4);
            if (trackTag != "MTrk")
                throw new FormatException($"Invalid MIDI track: expected 'MTrk', got '{trackTag}'");

            int trackLength = ReadInt32BE(data, ref pos);
            int trackEnd = pos + trackLength;

            var events = new List<MidiEvent>();
            string? trackName = null;
            long absoluteTick = 0;
            byte runningStatus = 0;

            while (pos < trackEnd)
            {
                long delta = ReadVariableLength(data, ref pos);
                absoluteTick += delta;

                if (pos >= trackEnd) break;

                byte statusByte = data[pos];

                // Meta event
                if (statusByte == 0xFF)
                {
                    pos++; // consume 0xFF
                    byte metaType = data[pos++];
                    int metaLength = (int)ReadVariableLength(data, ref pos);
                    int metaStart = pos;

                    switch (metaType)
                    {
                        case 0x03: // Track name
                            trackName = Encoding.ASCII.GetString(data, pos, Math.Min(metaLength, trackEnd - pos));
                            break;

                        case 0x51: // Tempo (3 bytes: microseconds per beat)
                            if (metaLength >= 3)
                            {
                                int usPerBeat = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
                                events.Add(new TempoEvent(absoluteTick, usPerBeat));
                            }
                            break;

                        case 0x58: // Time signature
                            if (metaLength >= 2)
                            {
                                int numerator = data[pos];
                                int denominator = 1 << data[pos + 1]; // stored as power of 2
                                events.Add(new TimeSignatureEvent(absoluteTick, numerator, denominator));
                            }
                            break;

                        case 0x59: // Key signature
                            if (metaLength >= 2)
                            {
                                int sharpsFlats = (sbyte)data[pos]; // signed: negative = flats
                                bool isMinor = data[pos + 1] != 0;
                                events.Add(new KeySignatureEvent(absoluteTick, sharpsFlats, isMinor));
                            }
                            break;

                        case 0x2F: // End of track
                            pos = metaStart + metaLength;
                            goto trackDone;
                    }

                    pos = metaStart + metaLength;
                    continue;
                }

                // SysEx event — skip
                if (statusByte == 0xF0 || statusByte == 0xF7)
                {
                    pos++; // consume status
                    int sysexLength = (int)ReadVariableLength(data, ref pos);
                    pos += sysexLength;
                    continue;
                }

                // Channel message
                byte status;
                if ((statusByte & 0x80) != 0)
                {
                    // New status byte
                    status = statusByte;
                    runningStatus = status;
                    pos++;
                }
                else
                {
                    // Running status — reuse previous status byte
                    status = runningStatus;
                    // Don't advance pos; the current byte is data
                }

                int command = status & 0xF0;
                int channel = status & 0x0F;

                switch (command)
                {
                    case 0x80: // Note Off
                    {
                        int pitch = data[pos++];
                        int velocity = data[pos++]; // velocity ignored for note-off
                        events.Add(new NoteOffEvent(absoluteTick, channel, pitch));
                        break;
                    }

                    case 0x90: // Note On
                    {
                        int pitch = data[pos++];
                        int velocity = data[pos++];
                        if (velocity == 0)
                        {
                            // Note-on with velocity 0 = note-off
                            events.Add(new NoteOffEvent(absoluteTick, channel, pitch));
                        }
                        else
                        {
                            events.Add(new NoteOnEvent(absoluteTick, channel, pitch, velocity));
                        }
                        break;
                    }

                    case 0xA0: // Aftertouch — skip 2 data bytes
                        pos += 2;
                        break;

                    case 0xB0: // Control Change — skip 2 data bytes
                        pos += 2;
                        break;

                    case 0xC0: // Program Change
                    {
                        int program = data[pos++];
                        events.Add(new ProgramChangeEvent(absoluteTick, channel, program));
                        break;
                    }

                    case 0xD0: // Channel Pressure — skip 1 data byte
                        pos += 1;
                        break;

                    case 0xE0: // Pitch Bend — skip 2 data bytes
                        pos += 2;
                        break;

                    default:
                        // Unknown command — try to skip gracefully
                        pos++;
                        break;
                }
            }

            trackDone:
            tracks.Add(new MidiTrack(trackName, events));

            // Ensure we're at the expected end of the track chunk
            pos = trackEnd;
        }

        return new MidiFile(format, ticksPerQuarterNote, tracks);
    }

    static string ReadString(byte[] data, ref int pos, int length)
    {
        string s = Encoding.ASCII.GetString(data, pos, length);
        pos += length;
        return s;
    }

    static int ReadInt32BE(byte[] data, ref int pos)
    {
        int value = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
        pos += 4;
        return value;
    }

    static int ReadInt16BE(byte[] data, ref int pos)
    {
        int value = (data[pos] << 8) | data[pos + 1];
        pos += 2;
        return value;
    }

    static long ReadVariableLength(byte[] data, ref int pos)
    {
        long value = 0;
        for (int i = 0; i < 4; i++) // max 4 bytes
        {
            byte b = data[pos++];
            value = (value << 7) | (long)(b & 0x7F);
            if ((b & 0x80) == 0)
                break;
        }
        return value;
    }
}
