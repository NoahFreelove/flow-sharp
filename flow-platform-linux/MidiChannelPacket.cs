namespace Flow.Platform.Linux;

public readonly record struct MidiChannelPacket(byte Status, byte Data1, byte Data2)
{
    /// <summary>Decode one complete channel packet. System packets are ignored.
    /// Running-status byte streams are not accepted at the RtMidi packet boundary.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> packet, out MidiChannelPacket message)
    {
        message = default;
        if (packet.IsEmpty) throw new ArgumentException("Empty MIDI packet.");
        byte status = packet[0];
        if (status >= 0xf0) return false;
        if (status < 0x80) throw new ArgumentException("Expected a complete MIDI message with status.");
        int length = (status & 0xf0) is 0xc0 or 0xd0 ? 2 : 3;
        if (packet.Length != length || packet[1] > 127 || (length == 3 && packet[2] > 127))
            throw new ArgumentException("Malformed MIDI channel message.");
        message = new(status, packet[1], length == 3 ? packet[2] : (byte)0); return true;
    }
}
