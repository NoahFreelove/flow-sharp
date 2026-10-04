using Flow.Platform.Linux;
using Flow.Studio.Host;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class SharedMidiInputHubTests
{
    private sealed class Input(MidiPacketReceiver receive) : IMidiInputConnection
    {
        public Exception? Error { get; set; }
        public bool IsRunning { get; private set; } = true;
        public bool FailStop;
        public int Stops;
        public void Send(byte[] packet) => receive(packet, 123);
        public bool TryStop(TimeSpan timeout)
        { Stops++; if (FailStop) return false; IsRunning = false; return true; }
        public void Dispose() => TryStop(TimeSpan.Zero);
    }

    [Fact]
    public void TwoSubscribersShareOneDeviceAndStoppingOneLeavesTheOtherRunning()
    {
        Input? input = null; int opens = 0, monitor = 0, recorder = 0;
        using var hub = new SharedMidiInputHub((_, receive) => { opens++; return input = new(receive); });
        using var first = hub.Subscribe("keyboard", (_, _) => monitor++);
        using var second = hub.Subscribe("keyboard", (_, _) => recorder++);
        Assert.Equal(1, opens); input!.Send([0x90, 60, 127]);
        Assert.Equal(1, monitor); Assert.Equal(1, recorder);
        Assert.Throws<InvalidOperationException>(() => hub.Subscribe("keyboard", (_, _) => { }));
        Assert.True(second.TryStop(TimeSpan.Zero)); Assert.Equal(0, input.Stops);
        input.Send([0x80, 60, 0]); Assert.Equal(2, monitor); Assert.Equal(1, recorder);
        Assert.True(first.IsRunning); Assert.False(second.IsRunning);
        Assert.Throws<InvalidOperationException>(() => hub.Subscribe("other port", (_, _) => { }));
        Assert.True(first.TryStop(TimeSpan.Zero)); Assert.False(hub.DeviceOwned); Assert.Equal(1, input.Stops);
    }

    [Fact]
    public async Task SubscriberStopJoinsEnteredCallbackAndRetainsOwnershipOnTimeout()
    {
        Input? input = null;
        using var hub = new SharedMidiInputHub((_, receive) => input = new(receive));
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var first = hub.Subscribe("keys", (_, _) => { });
        var second = hub.Subscribe("keys", (_, _) =>
        { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); });
        var producer = Task.Run(() => input!.Send([0x90, 60, 127]));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(second.TryStop(TimeSpan.Zero)); Assert.True(hub.DeviceOwned);
            Assert.Equal(2, hub.SubscriberCount); Assert.Equal(0, input!.Stops);
            Assert.Throws<InvalidOperationException>(() => hub.Subscribe("keys", (_, _) => { }));
        }
        finally { release.Set(); await producer.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(second.TryStop(TimeSpan.Zero)); Assert.Equal(1, hub.SubscriberCount);
        Assert.True(first.IsRunning); Assert.Equal(0, input!.Stops);
        second.Dispose();
    }

    [Fact]
    public void NativeJoinFailureBlocksReopenAndErrorsStayVisibleAfterClosing()
    {
        Input? input = null; int opens = 0;
        using var hub = new SharedMidiInputHub((_, receive) => { opens++; return input = new(receive); });
        var lease = hub.Subscribe("keys", (_, _) => { });
        input!.FailStop = true; Assert.False(lease.TryStop(TimeSpan.Zero));
        Assert.True(hub.DeviceOwned); Assert.Throws<InvalidOperationException>(() => hub.Subscribe("keys", (_, _) => { }));
        Assert.Equal(1, opens);
        input.Error = new IOException("device lost"); input.FailStop = false;
        Assert.True(lease.TryStop(TimeSpan.Zero)); Assert.IsType<IOException>(lease.Error);
        Assert.False(hub.DeviceOwned); lease.Dispose();
    }

    [Fact]
    public void ReceiverFailureIsIsolatedAndValidFanoutAllocatesNothing()
    {
        Input? input = null; int received = 0;
        using var hub = new SharedMidiInputHub((_, receive) => input = new(receive));
        using var healthy = hub.Subscribe("keys", (_, _) => received++);
        using var faulty = hub.Subscribe("keys", (_, _) => throw new InvalidOperationException("subscriber failed"));
        byte[] packet = [0x90, 60, 127]; input!.Send(packet);
        Assert.IsType<InvalidOperationException>(faulty.Error); Assert.Null(healthy.Error); Assert.True(healthy.IsRunning);
        Assert.True(faulty.TryStop(TimeSpan.Zero));
        using var replacement = hub.Subscribe("keys", (_, _) => received++);
        input.Send(packet);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) input.Send(packet);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(203, received);
    }
}
