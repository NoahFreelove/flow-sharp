using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
namespace FlowLang.Hosting;

internal static class PluginSampleAssets
{
    internal static void Install(InternalFunctionRegistry registry, PluginPackage package, CancellationToken cancellation)
    {
        var dependencies = package.Dependencies.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var decoded = new Dictionary<string, PcmAsset>(StringComparer.Ordinal);
        long remaining = 16 * 1024 * 1024;
        registry.ReplaceAll("dawPluginSample", new FunctionSignature("dawPluginSample", [StringType.Instance]), args =>
        {
            cancellation.ThrowIfCancellationRequested();
            string id = args[0].As<string>();
            if (!dependencies.TryGetValue(id, out var dependency)) throw new ArgumentException("Sample is not a pinned package dependency");
            if (!decoded.TryGetValue(id, out var asset))
            {
                byte[] bytes = Convert.FromBase64String(dependency.Base64);
                package.Manifest.ValidateDependency(id, dependency.Version, bytes);
                using var stream = new MemoryStream(bytes, writable: false);
                asset = WaveAssetReader.Read(stream, remaining, cancellation);
                if (asset.Frames == 0) throw new ArgumentException("Packaged sample is empty");
                remaining -= asset.Bytes; decoded.Add(id, asset);
            }
            return new Value(asset, DawAudioType.Instance);
        });
    }
}
