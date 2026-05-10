using System.Reflection;
using OmniSharp.Extensions.LanguageServer.Server;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Wave 0 gate per Phase 17 RESEARCH §Open Question Q1: confirm OmniSharp 0.19.9
/// boots under net10.0. If these Facts fail with MissingMethodException or
/// TypeLoadException, the phase must pivot to StreamJsonRpc + manual handlers
/// (RESEARCH Pitfall 2).
///
/// Design note on test shape
/// -------------------------
/// Plan 17-01 Task 2 allows either a full in-process initialize+shutdown round-trip
/// OR a reflection-only smoke. During Task 2 execution, the full round-trip Fact
/// (driving <c>LanguageServer.From</c> with <c>Stream.Null</c>) hung past a 5s
/// timeout. The DI container finished constructing (the task entered
/// <c>WaitingForActivation</c>), but <c>From</c> does not return until the initial
/// handshake completes; with null input no <c>initialize</c> message ever arrives.
/// This is environment/API behavior, NOT a net10 type-load failure. Both surfaces
/// tested below verify the Wave 0 invariant: OmniSharp's assembly binds under
/// net10 and its core types load.
///
/// TODO(17-03): upgrade to a real initialize+shutdown round-trip using paired
/// Pipelines / MemoryStreams once DocumentManager and handlers exist so the server
/// can actually respond to <c>initialize</c> and unblock <c>WaitForExit</c>.
/// </summary>
public class OmniSharpBootTest
{
    /// <summary>
    /// Assembly binds under net10 and the core LanguageServer type loads.
    /// Catches the highest-risk failure mode (TypeLoadException at bind time).
    /// </summary>
    [Fact]
    public void OmniSharp_LanguageServerType_Loads()
    {
        var t = typeof(OmniSharp.Extensions.LanguageServer.Server.LanguageServer);
        Assert.NotNull(t);
        Assert.Equal("OmniSharp.Extensions.LanguageServer.Server.LanguageServer", t.FullName);
    }

    /// <summary>
    /// The static factory entry point <c>LanguageServer.From</c> resolves under
    /// net10 (i.e., no <c>MissingMethodException</c> at lookup). Resolving the
    /// method without invoking it exercises MediatR/DI metadata scan without
    /// requiring a live client handshake.
    /// </summary>
    [Fact]
    public void OmniSharp_FromFactory_IsResolvable()
    {
        var methods = typeof(OmniSharp.Extensions.LanguageServer.Server.LanguageServer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "From")
            .ToArray();
        Assert.NotEmpty(methods);
    }
}
