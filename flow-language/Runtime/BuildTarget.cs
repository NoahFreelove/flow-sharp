namespace FlowLang.Runtime;

/// <summary>
/// The compile-target flavor this build was produced for (<c>FlowTarget=Desktop|Web</c>,
/// Phase 47). The language reads it for Web-target diagnostics at parse and import
/// time; <see cref="Core.FlowEngine.IsWebTarget"/> exposes the same facts to hosts.
/// </summary>
public static class BuildTarget
{
    /// <summary>True in the <c>FlowTarget=Web</c> (browser/WASM) build.</summary>
    public static bool IsWeb { get; } =
#if FLOW_WEB
        true;
#else
        false;
#endif

    /// <summary>
    /// Live blocks need file watching, which the browser build lacks; a <c>live</c>
    /// block is a parse error there.
    /// </summary>
    public static bool SupportsLiveBlocks { get; } = !IsWeb;
}
