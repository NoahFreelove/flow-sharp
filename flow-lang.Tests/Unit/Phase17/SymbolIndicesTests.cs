using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 05 Task 1 Facts — upstream registry + module-loader additions.
/// Task 2 extends this partial class with index-level Facts.
/// </summary>
public partial class SymbolIndicesTests
{
    [Fact]
    public void ModuleLoader_ResolveStdlibPath_ReturnsAbsolutePathEndingInModuleFlow()
    {
        var path = ModuleLoader.ResolveStdlibPath("@audio");
        Assert.EndsWith("audio.flow", path);
        Assert.True(System.IO.Path.IsPathRooted(path));
    }

    [Fact]
    public void ModuleLoader_ResolveStdlibPath_WithoutAtPrefix_MatchesWithAtPrefix()
    {
        Assert.Equal(ModuleLoader.ResolveStdlibPath("@std"), ModuleLoader.ResolveStdlibPath("std"));
    }

    [Fact]
    public void ModuleLoader_ResolveStdlibPath_HandlesAlreadyHasExtension()
    {
        // "@std.flow" and "@std" should resolve to the same path.
        Assert.Equal(ModuleLoader.ResolveStdlibPath("@std"), ModuleLoader.ResolveStdlibPath("@std.flow"));
    }

    [Fact]
    public void Registry_EnumerateSignatures_OnEmptyRegistry_IsEmpty()
    {
        // Empty registry — enumerator surface check. Populated-registry behavior
        // is covered by CompletionHandlerTests + SymbolIndicesTests in Task 2,
        // where RegisterSignaturesOnly has been landed.
        var r = new InternalFunctionRegistry();
        Assert.Empty(r.EnumerateSignatures());
    }
}
