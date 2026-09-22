namespace FlowLang.Tests.Helpers;

/// <summary>
/// Gives terminal tests a known color environment and restores the caller's
/// settings. Requires serialized tests because environment variables are global.
/// </summary>
internal sealed class TerminalEnvironmentScope : IDisposable
{
    private readonly string? _noColor = Environment.GetEnvironmentVariable("NO_COLOR");
    private readonly string? _term = Environment.GetEnvironmentVariable("TERM");

    public TerminalEnvironmentScope()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", null);
        Environment.SetEnvironmentVariable("TERM", "xterm");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", _noColor);
        Environment.SetEnvironmentVariable("TERM", _term);
    }
}
