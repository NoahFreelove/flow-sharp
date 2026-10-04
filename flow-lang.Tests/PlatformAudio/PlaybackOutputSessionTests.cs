using Flow.Audio;
using Flow.Music.Model;
using Flow.Platform.Linux;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class PlaybackOutputSessionTests
{
    private static PreparedSinePlayback Source()
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "tone", new(Bpm: 120),
            [new(Guid.NewGuid(), "melody", 4,
                [new(Guid.NewGuid(), "voice", 0, 4, new('A', 4, 0, null, 69, 440))])]);
        return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section)]), new(48000, 128));
    }

    private sealed class Stream : IPlaybackOutputStream
    {
        public bool Active = true, Faulted, FailStart, FailClose, FailPoll;
        public int CloseCalls;
        public bool IsActive => FailPoll ? throw new InvalidOperationException("poll") : Active;
        public bool CallbackFaulted => Faulted;
        public void Start() { if (FailStart) throw new InvalidOperationException("start"); }
        public void Dispose()
        {
            CloseCalls++;
            if (FailClose) throw new InvalidOperationException("close");
            Active = false;
        }
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("callback")]
    [InlineData("poll")]
    public void OutputFailureStopsPlaybackAndReconnectDoesNotResume(string failure)
    {
        var playback = new QueuedSinePlayback(Source());
        var first = new Stream();
        var second = new Stream();
        int opens = 0;
        using var session = new PlaybackOutputSession(playback, _ => ++opens == 1 ? first : second);
        Assert.True(session.Connect());
        Assert.True(playback.TryPlay());
        playback.Read(new float[256]);
        Assert.Equal(TransportState.Playing, playback.State);
        first.Active = failure != "inactive";
        first.Faulted = failure == "callback";
        first.FailPoll = failure == "poll";
        Assert.Equal(PlaybackOutputState.Faulted, session.Poll());
        Assert.NotNull(session.LastError);
        Assert.Equal(1, first.CloseCalls);
        Assert.Equal(TransportState.Stopped, playback.State);
        Assert.Equal(0, playback.PositionFrames);
        Assert.False(playback.StopPending);
        Assert.True(session.Connect());
        Assert.Null(session.LastError);
        Assert.Equal(2, opens);
        Assert.Equal(TransportState.Stopped, playback.State);
        Assert.True(playback.TryPlay());
    }

    [Fact]
    public void FailedCloseRetainsOwnershipAndBlocksSecondStream()
    {
        var playback = new QueuedSinePlayback(Source());
        var stream = new Stream();
        int opens = 0;
        using var session = new PlaybackOutputSession(playback, _ => { opens++; return stream; });
        Assert.True(session.Connect());
        stream.FailClose = true;
        Assert.False(session.Disconnect());
        Assert.True(playback.StopPending);
        Assert.False(session.Connect());
        Assert.Equal(1, opens);
        Assert.Throws<InvalidOperationException>(() => session.Dispose());
        Assert.Equal(PlaybackOutputState.Faulted, session.State);
        stream.FailClose = false;
        Assert.True(session.Disconnect());
        Assert.False(playback.StopPending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenAndStartFailureCanBeRetried(bool failOpen)
    {
        var playback = new QueuedSinePlayback(Source());
        var failed = new Stream { FailStart = true };
        int opens = 0;
        using var session = new PlaybackOutputSession(playback, _ =>
        {
            if (++opens > 1) return new Stream();
            if (failOpen) throw new InvalidOperationException("missing device");
            return failed;
        });
        Assert.False(session.Connect());
        Assert.Equal(PlaybackOutputState.Faulted, session.State);
        Assert.Equal(failOpen ? 0 : 1, failed.CloseCalls);
        Assert.True(session.Connect());
        Assert.Null(session.LastError);
    }

    [Fact]
    public void DisconnectSettlesPendingPublicationAndDiscardsOldPlayCommand()
    {
        var playback = new QueuedSinePlayback(Source());
        var session = new PlaybackOutputSession(playback, _ => new Stream());
        Assert.True(session.Connect());
        Assert.True(playback.TryPlay());
        Assert.True(playback.TryReplace(Source()));
        Assert.True(session.Disconnect());
        Assert.False(playback.ReplacementPending);
        Assert.False(playback.StopPending);
        Assert.Equal(2, playback.Generation);
        Assert.Equal(TransportState.Stopped, playback.State);
        Assert.True(playback.TryReplace(Source())); // retirement was reclaimed
        session.Dispose();
        session.Dispose();
        Assert.Equal(PlaybackOutputState.Disposed, session.State);
        Assert.Throws<ObjectDisposedException>(() => session.Connect());
        Assert.Throws<ObjectDisposedException>(() => session.Poll());
    }

    [Fact]
    public void ConnectionDiscardsQueuedPlaybackAndUsesAudibleFreshProbe()
    {
        var playback = new QueuedSinePlayback(Source());
        CallbackRenderProbe? captured = null;
        int opens = 0;
        using var session = new PlaybackOutputSession(playback, probe =>
        {
            captured = probe;
            opens++;
            return new Stream();
        });
        Assert.True(playback.TryPlay());
        Assert.True(session.Connect());
        Assert.True(session.Connect());
        Assert.Equal(1, opens);
        Assert.Equal(TransportState.Stopped, playback.State);
        Assert.True(playback.TryPlay());
        var buffer = new float[256];
        captured!.Process(buffer);
        Assert.Contains(buffer, sample => sample != 0);
        var previous = captured;
        Assert.True(session.Disconnect());
        Assert.True(session.Connect());
        Assert.NotSame(previous, captured);
    }
}
