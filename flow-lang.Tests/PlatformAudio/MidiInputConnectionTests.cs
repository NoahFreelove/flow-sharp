using Flow.Platform.Linux;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class MidiInputConnectionTests
{
    private sealed class Port : IMidiInputPort
    {
        public readonly ManualResetEventSlim Entered = new(false), Release = new(false);
        public bool Block, Fail;
        public int Disposals, Reads;
        public int Read(byte[] buffer)
        {
            int read = Interlocked.Increment(ref Reads);
            Entered.Set();
            if (Block && !Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            if (Fail) throw new IOException("device lost");
            if (read != 1) return 0;
            buffer[0] = 0x90; buffer[1] = 60; buffer[2] = 100;
            return 3;
        }
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }

    [Fact]
    public void TimeoutRetainsPortUntilReaderHasExited()
    {
        var port = new Port { Block = true };
        int received = 0;
        var input = new MidiInputConnection(port, (_, _) => Interlocked.Increment(ref received));
        try
        {
            Assert.True(port.Entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(input.TryStop(TimeSpan.Zero));
            Assert.Equal(0, port.Disposals);
        }
        finally { port.Release.Set(); input.Dispose(); }
        Assert.Equal(1, port.Disposals);
        Assert.Equal(0, received);
        input.Dispose();
        Assert.Equal(1, port.Disposals);
    }

    [Fact]
    public void ReadFailureIsVisibleAndOwnershipRemainsUntilStop()
    {
        var port = new Port { Fail = true };
        using var input = new MidiInputConnection(port, (_, _) => Assert.Fail("No packet expected"));
        Assert.True(SpinWait.SpinUntil(() => !input.IsRunning, TimeSpan.FromSeconds(5)));
        Assert.IsType<IOException>(input.Error);
        Assert.Equal(0, port.Disposals);
        Assert.True(input.TryStop(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, port.Disposals);
        Assert.IsType<IOException>(input.Error);
    }

    [Fact]
    public void ReceiverGetsCompletePacketAndItsFailureStopsInput()
    {
        var port = new Port();
        long timestamp = 0;
        byte[]? packet = null;
        using var input = new MidiInputConnection(port, (bytes, ticks) =>
        {
            packet = bytes.ToArray(); timestamp = ticks;
            throw new InvalidOperationException("receiver failed");
        });
        Assert.True(SpinWait.SpinUntil(() => !input.IsRunning, TimeSpan.FromSeconds(5)));
        Assert.Equal(new byte[] { 0x90, 60, 100 }, packet);
        Assert.True(timestamp > 0);
        Assert.IsType<InvalidOperationException>(input.Error);
        Assert.True(input.TryStop(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, port.Disposals);
    }
}
