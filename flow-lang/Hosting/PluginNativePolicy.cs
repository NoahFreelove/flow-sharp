using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Daw;
using FlowLang.TypeSystem;
namespace FlowLang.Hosting;

/// <summary>Default-deny native surface for declared plugin builders. Permit exact
/// signatures from the general-purpose core and detached DAW construction APIs.
/// These registrations perform in-memory work; print is directed to the worker's
/// null sink. New native families require an explicit review/addition here.</summary>
internal static class PluginNativePolicy
{
    internal static void Populate(HashSet<FunctionSignature> allowed, FlowLang.Runtime.ExecutionContext context)
    {
        var catalog = new InternalFunctionRegistry();
        CoreLibrary.Register(catalog, context);
        DawFunctions.Register(catalog);
        DawGraphFunctions.Register(catalog);
        foreach (var entry in catalog.EnumerateSignatures())
            foreach (var signature in entry.Value) allowed.Add(signature);
    }
}
