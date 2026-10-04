using Flow.Audio.Graph;
using Flow.Studio.Model;
namespace Flow.Studio.Engine;

public static class AutomationCompiler
{
    public static GraphAutomationLane Lower(ProjectAutomationLane lane, ProjectTempoMap tempo, int sampleRate) =>
        MusicalAutomationCompiler.Lower(lane, tempo, sampleRate);
}
