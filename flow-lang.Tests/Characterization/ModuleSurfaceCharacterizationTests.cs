using System.Text;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Pins what each import makes visible and how native registrations bind to
/// `internal proc` declarations, so the standard-library split and compatibility
/// aggregates reproduce the legacy environment exactly.
/// </summary>
[Collection("FlowScripts")]
public class ModuleSurfaceCharacterizationTests
{
    private static readonly string[] Modules =
    [
        "@std", "@collections", "@bars", "@notation", "@composition", "@audio", "@patterns",
        "@generative", "@improv", "@notation-io", "@test", "@sfz", "@osc", "@midi", "@jack",
    ];

    private static SortedSet<string> Visible(FlowEngine engine)
    {
        var frame = engine.Context.GlobalFrame;
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in frame.GetFunctionNames())
            foreach (var overload in frame.GetFunctionOverloads(name))
                set.Add((overload.IsInternal ? "native " : "flow   ") + overload.Signature);
        return set;
    }

    private static FlowEngine Quiet() =>
        new(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });

    [Fact]
    public void ImportSurfacesAreUnchanged()
    {
        var sb = new StringBuilder();
        using var bare = Quiet();
        var baseline = Visible(bare);
        sb.Append("# engine start (no prelude): ").Append(baseline.Count).Append(" overloads\n");
        foreach (var entry in baseline) sb.Append("  ").Append(entry).Append('\n');

        foreach (var module in Modules)
        {
            using var engine = Quiet();
            engine.Execute($"use \"{module}\"", "<import>");
            var added = Visible(engine).Except(baseline).ToList();
            var errors = engine.ErrorReporter.ErrorCount;
            sb.Append("# use \"").Append(module).Append("\": +").Append(added.Count)
              .Append(errors > 0 ? $" ({errors} errors)" : "").Append('\n');
            foreach (var entry in added) sb.Append("  ").Append(entry).Append('\n');
            foreach (var (name, procs) in engine.Context.ModuleRegistry.Exports.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                sb.Append("  module ").Append(name).Append(": ").Append(procs.Count).Append(" qualified procs\n");
        }
        Snapshot.Verify("modules/surfaces.txt", sb.ToString());
    }

    [Fact]
    public void NativeRegistrationsAndTheirSurfaceAreUnchanged()
    {
        using var engine = TypeSystemCharacterizationTests.LoadAllModules();
        var frame = engine.Context.GlobalFrame;
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in frame.GetFunctionNames())
            foreach (var overload in frame.GetFunctionOverloads(name))
                if (overload.IsInternal) bound.Add(overload.Signature.ToString());

        var registered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (name, signatures) in engine.Context.InternalRegistry.EnumerateSignatures())
            foreach (var signature in signatures)
                registered.Add(signature.ToString());

        var sb = new StringBuilder();
        sb.Append("# registered native signatures: ").Append(registered.Count).Append('\n');
        sb.Append("# reachable through an `internal proc` surface: ").Append(registered.Count(bound.Contains)).Append('\n');
        sb.Append("# registered without a surface (unreachable from Flow):\n");
        foreach (var signature in registered.Where(s => !bound.Contains(s))) sb.Append("  ").Append(signature).Append('\n');
        sb.Append("# surfaces bound to a signature not registered verbatim (wildcard binding):\n");
        foreach (var signature in bound.Where(s => !registered.Contains(s)).Order(StringComparer.Ordinal))
            sb.Append("  ").Append(signature).Append('\n');
        Snapshot.Verify("modules/native-bindings.txt", sb.ToString());
    }
}
