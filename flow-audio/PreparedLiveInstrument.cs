using Flow.Audio.Graph;

namespace Flow.Audio;

/// <summary>
/// Bounded single MIDI producer and single audio consumer. Complete channel messages
/// apply at the next nonempty block boundary. The same voice kernels as scheduled
/// notes implement synthesis, sampling, Flow graphs, release tails and stealing.
/// This is an instrument bus, not a second output device or a master-effects bypass.
/// </summary>
public sealed class PreparedLiveInstrument
{
    private readonly record struct Message(byte Status, byte First, byte Second);
    private readonly Message[] _commands;
    private readonly bool[] _held = new bool[16 * 128], _sustained = new bool[16 * 128], _pedal = new bool[16];
    private readonly Flow.Music.Model.MidiPitchMap _frequencies;
    internal PreparedNotePlayback VoicePool { get; }
    private int _head, _tail, _panic, _closed, _faulted;
    private long _dropped, _stolen;

    public int SampleRate => VoicePool.SampleRate;
    public int MaxBlockFrames => VoicePool.MaxBlockFrames;
    public int MaxMessagesPerBlock { get; }
    public int Capacity => _commands.Length - 1;
    public bool Faulted => Volatile.Read(ref _faulted) != 0;
    public bool AdmissionClosed => Volatile.Read(ref _closed) != 0;
    public long DroppedMessages => Interlocked.Read(ref _dropped);
    public long StolenVoices => Interlocked.Read(ref _stolen);
    /// <summary>Audio-owner cursor; independent of any project's finite duration.</summary>
    public long PositionFrames => VoicePool.PositionFrames;

    public PreparedLiveInstrument(SineVoiceSettings settings, int sampleRate = 48000, int blockFrames = 256,
        int capacity = 4096, int maxMessagesPerBlock = 256, IEnumerable<GraphAutomationLane>? automation = null)
        : this(settings, Flow.Music.Model.MidiPitchMap.EqualTemperament, sampleRate, blockFrames, capacity, maxMessagesPerBlock, automation) { }

    public PreparedLiveInstrument(SineVoiceSettings settings, Flow.Music.Model.MidiPitchMap pitchMap,
        int sampleRate = 48000, int blockFrames = 256, int capacity = 4096,
        int maxMessagesPerBlock = 256, IEnumerable<GraphAutomationLane>? automation = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pitchMap);
        _frequencies = pitchMap;
        if (maxMessagesPerBlock is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maxMessagesPerBlock));
        MaxMessagesPerBlock = maxMessagesPerBlock;
        if (capacity is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        _commands = new Message[capacity + 1];
        VoicePool = new([], sampleRate, blockFrames, minimumFrames: long.MaxValue, settings: settings, graphAutomation: automation);
    }

    /// <summary>One producer submits validated, complete channel messages. Overflow
    /// latches a fault, closes admission and silences the instance at the next block.
    /// A new prepared instance is required after a fault or explicit close.</summary>
    public bool TryWrite(byte status, byte data1, byte data2 = 0)
    {
        if (status is < 0x80 or > 0xef || data1 > 127 || data2 > 127)
            throw new ArgumentException("Invalid MIDI channel message.");
        if (AdmissionClosed) return false;
        int next = Next(_tail);
        if (next == Volatile.Read(ref _head))
        {
            Interlocked.Increment(ref _dropped);
            Volatile.Write(ref _faulted, 1);
            CloseAdmission(); return false;
        }
        _commands[_tail] = new(status, data1, data2);
        Volatile.Write(ref _tail, next); return true;
    }

    /// <summary>May be called by the control owner or producer. Permanently closes
    /// this instance; any writer already in flight is silenced too on the next block.</summary>
    public void CloseAdmission() => Volatile.Write(ref _closed, 1);

    /// <summary>Silence voices and discard commands observed at the next boundary.
    /// Admission remains open; later input can sound. Close admission on device loss.</summary>
    public void RequestPanic() => Volatile.Write(ref _panic, 1);

    /// <summary>Single control producer for graph parameters, separate from MIDI.
    /// Uses the same validation, automation ownership and smoothing as playback.</summary>
    public void SetLatestParameters(IEnumerable<GraphParameterValue> values) => VoicePool.SetLatestParameters(values);

    public int Read(Span<float> output) => ReadAt(output, PositionFrames);

    /// <summary>Audio owner supplies the project automation clock independently of
    /// the live voice's elapsed DSP time, which always advances through rendering.</summary>
    public int ReadAt(Span<float> output, long projectFrame, bool advanceTimeline = true)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames) throw new ArgumentException("Invalid stereo block.");
        if (projectFrame < 0 || (advanceTimeline && projectFrame > long.MaxValue - output.Length / 2))
            throw new ArgumentOutOfRangeException(nameof(projectFrame));
        if (output.IsEmpty) return 0;
        // Apply the complete control snapshot before starting/resetting any voice.
        // Updates arriving later wait for the next block, matching scheduled voices.
        VoicePool.BeginLiveBlock(projectFrame);
        bool panic = Interlocked.Exchange(ref _panic, 0) != 0;
        if (AdmissionClosed || panic)
        {
            Volatile.Write(ref _head, Volatile.Read(ref _tail));
            ClearKeys(); VoicePool.LivePanic();
        }
        else
        {
            int boundary = Volatile.Read(ref _tail), count = 0;
            while (_head != boundary && count++ < MaxMessagesPerBlock)
            {
                var message = _commands[_head];
                Volatile.Write(ref _head, Next(_head));
                Process(message);
            }
            // A concurrent overflow/close must not leave newly started voices held.
            if (AdmissionClosed) { ClearKeys(); VoicePool.LivePanic(); }
        }
        int frames = VoicePool.ReadLive(output, projectFrame, advanceTimeline);
        Volatile.Write(ref _stolen, VoicePool.StolenVoices);
        return frames;
    }

    /// <summary>Audio-owner discontinuity reset. Discards queued input observed now
    /// and held notes; later messages can sound unless admission was closed.</summary>
    public void Reset(long projectFrame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(projectFrame);
        Volatile.Write(ref _head, Volatile.Read(ref _tail));
        ClearKeys(); VoicePool.Seek(projectFrame);
    }

    private void Process(Message message)
    {
        int channel = message.Status & 15, key = channel * 128 + message.First;
        switch (message.Status & 0xf0)
        {
            case 0x90 when message.Second != 0:
                if (_frequencies[message.First] == 0) break;
                _held[key] = true; _sustained[key] = false;
                VoicePool.LiveNoteOn(key, _frequencies[message.First], message.Second / 127.0);
                break;
            case 0x80:
            case 0x90:
                NoteOff(key, channel); break;
            case 0xb0 when message.First == 64:
                _pedal[channel] = message.Second >= 64;
                if (!_pedal[channel])
                    for (int i = channel * 128; i < (channel + 1) * 128; i++)
                        if (_sustained[i]) { _sustained[i] = false; VoicePool.LiveNoteOff(i); }
                break;
            case 0xb0 when message.First == 123:
                for (int i = channel * 128; i < (channel + 1) * 128; i++) NoteOff(i, channel);
                break;
            case 0xb0 when message.First == 120:
                Array.Clear(_held, channel * 128, 128); Array.Clear(_sustained, channel * 128, 128);
                VoicePool.LivePanic(channel); break;
            // Pitch bend, pressure, program and other CC mappings require explicit
            // device contracts; they do not invent parameter mappings here.
        }
    }

    private void NoteOff(int key, int channel)
    {
        if (!_held[key]) return;
        _held[key] = false;
        if (_pedal[channel]) _sustained[key] = true;
        else VoicePool.LiveNoteOff(key);
    }
    private void ClearKeys() { Array.Clear(_held); Array.Clear(_sustained); Array.Clear(_pedal); }
    private int Next(int index) => index + 1 == _commands.Length ? 0 : index + 1;
}
