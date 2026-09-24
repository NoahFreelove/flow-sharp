using System.Runtime.CompilerServices;

namespace FlowLang.Tests;

/// <summary>
/// The music library registers its types, conversions and comparisons from a module
/// initializer in flow-lang.dll. Since the language runtime is its own assembly, a
/// test that only parses (flow-language.dll) could run before anything loads the
/// music library; run its initializer up front so every test sees the full catalog,
/// as the music host (FlowEngine) always does.
/// </summary>
internal static class MusicLibraryInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        RuntimeHelpers.RunModuleConstructor(typeof(FlowLang.Core.FlowEngine).Module.ModuleHandle);
}
