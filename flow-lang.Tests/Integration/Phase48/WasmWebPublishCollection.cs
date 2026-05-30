using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 — serializes every test class that shells out to
/// <c>dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web</c>.
///
/// <para><b>Why:</b> each such publish writes the SAME intermediate output
/// directory (<c>flow-lang/obj/Release/net10.0/browser-wasm</c> and the
/// <c>AppBundle</c> under <c>bin</c>). xUnit runs distinct test CLASSES in
/// parallel by default, so two publishes launched at once race on those shared
/// paths — a half-written Webcil/PE intermediate read by the concurrent publish's
/// <c>MarshalingPInvokeScanner</c> throws
/// <c>System.BadImageFormatException: Image is too small</c>
/// (MSB4018), failing the run non-deterministically. Each class passes in
/// isolation; the hazard is strictly cross-class parallel publishes.</para>
///
/// <para>Placing every Web-publish-shellout class in this single collection
/// forces them to run SERIALLY (xUnit never parallelizes within one collection),
/// so only one <c>dotnet publish</c> touches the shared <c>obj/</c> tree at a
/// time. No assertion is weakened — the publishes simply no longer overlap.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class WasmWebPublishCollection
{
    public const string Name = "FlowTarget=Web publish shell-out (serial)";
}
