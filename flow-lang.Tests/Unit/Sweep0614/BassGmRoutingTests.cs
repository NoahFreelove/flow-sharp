using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Notation;
using Xunit;

namespace FlowLang.Tests.Unit.Sweep0614;

/// <summary>
/// sweep-0614 (gap-routing-tuning-format): a <c>bass*</c>-named sequence used to
/// fall through every prefix in <see cref="InstrumentRouting.ResolveGmProgram"/>
/// and default to GM 0 (acoustic grand piano) on MIDI/MusicXML/LilyPond export —
/// a bass line sounded like a piano. It now routes to GM 32 (Acoustic Bass).
///
/// <para>The fix MUST NOT disturb the existing <c>bassoon</c> route (GM 70), which
/// shares the <c>bass</c> prefix and is checked first.</para>
/// </summary>
public class BassGmRoutingTests
{
    [Fact]
    public void BassPrefix_RoutesToAcousticBass()
    {
        // Was (0, 0) = acoustic grand piano before the fix.
        Assert.Equal((32, 0), InstrumentRouting.ResolveGmProgram("bass"));
        Assert.Equal((32, 0), InstrumentRouting.ResolveGmProgram("bassline"));
        Assert.Equal((32, 0), InstrumentRouting.ResolveGmProgram("Bass Guitar"));
    }

    [Fact]
    public void BassoonPrefix_StillRoutesToBassoon_NotSwallowed()
    {
        // The more-specific Phase 33 entry is ordered first, so the new bass
        // route does not capture bassoon.
        Assert.Equal((70, 0), InstrumentRouting.ResolveGmProgram("bassoon"));
    }

    [Fact]
    public void SharedTableAgrees_AcrossNotationSurfaces()
    {
        // D-39-20: MidiExport + InstrumentRouting are the same table.
        Assert.Equal(InstrumentRouting.ResolveGmProgram("bass"),
                     MidiExport.ResolveGmProgram("bass"));
    }
}
