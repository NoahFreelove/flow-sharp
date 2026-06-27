using System.Text.RegularExpressions;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 D-39-01 — registration entry point for the <c>@notation-io</c>
/// stdlib module surface: 4 builtins (<c>writeMusicXML</c>, <c>writeLilyPond</c>,
/// <c>abc</c>, <c>mml</c>) + 1 marker builtin (<c>__enableNotationIoModule</c>).
///
/// <para>
/// All 4 surface builtins gate on
/// <see cref="ExecutionContext.NotationIoEnabled"/>. The gate flips
/// <c>true</c> when the <c>__enableNotationIoModule</c> marker runs at
/// import time (per the trailing init call in <c>flow-lang/notation-io.flow</c>).
/// Calling any surface builtin without first importing the module raises a
/// clear, composer-facing error.
/// </para>
///
/// <para>
/// Mirrors the Phase 33 <see cref="FlowLang.StandardLibrary.Audio.Sfz.SfzBuiltins"/>
/// registration shape: <c>Register(InternalFunctionRegistry, ExecutionContext)</c>
/// is called from <see cref="FlowLang.Core.FlowEngine"/>'s constructor between
/// the SFZ registration and the StyleRegistry registration. The 4 builtins
/// are ALWAYS registered (so the function-name resolution finds them); the
/// runtime gate enforces module activation.
/// </para>
/// </summary>
public static class NotationIoBuiltins
{
    /// <summary>
    /// Wire the 5 notation-io builtins into the internal function registry.
    /// Idempotent. Called once per <see cref="FlowLang.Core.FlowEngine"/>
    /// instance at construction time.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // Marker builtin: flips the module-activation gate. Called by the
        // trailing `(__enableNotationIoModule)` line in flow-lang/notation-io.flow.
        var sigMarker = new FunctionSignature("__enableNotationIoModule", System.Array.Empty<FlowType>());
        registry.Register("__enableNotationIoModule", sigMarker, _ =>
        {
            context.NotationIoEnabled = true;
            return Value.Void();
        });

        // writeMusicXML(String path, Song song) → Void  [Plan 39-01 XML-01]
        var sigMxl = new FunctionSignature("writeMusicXML",
            new FlowType[] { StringType.Instance, SongType.Instance },
            ParameterNames: new[] { "path", "song" });
        registry.Register("writeMusicXML", sigMxl, args =>
        {
            RequireModuleActivated(context, "writeMusicXML");
            string path = args[0].As<string>();
            var song = args[1].As<SongData>();
            MusicXmlExport.WriteMusicXml(path, song);
            return Value.Void();
        });

        // writeLilyPond(String path, Song song) → Void  [Plan 39-02 LILY-01]
        var sigLy = new FunctionSignature("writeLilyPond",
            new FlowType[] { StringType.Instance, SongType.Instance },
            ParameterNames: new[] { "path", "song" });
        registry.Register("writeLilyPond", sigLy, args =>
        {
            RequireModuleActivated(context, "writeLilyPond");
            string path = args[0].As<string>();
            var song = args[1].As<SongData>();
            LilyPondExport.WriteLilyPond(path, song);
            return Value.Void();
        });

        // abc(String source) → Section | Array[Section]  [Plan 39-03 ABC-01 / ABC-02]
        var sigAbc = new FunctionSignature("abc",
            new FlowType[] { StringType.Instance },
            ParameterNames: new[] { "source" });
        registry.Register("abc", sigAbc, args =>
        {
            RequireModuleActivated(context, "abc");
            string src = args[0].As<string>();
            // Dispatch by counting X: headers per D-39-16.
            int xCount = CountAbcXHeaders(src);
            if (xCount >= 2)
            {
                // Phase 44 Plan 44-07: thread the calling ExecutionContext so
                // the deep parser helpers can elevate WarnOnce advisories to
                // composer-visible [strict] errors when context.CallerStrictMode.
                var sections = AbcImport.ParseMultiTune(src, context);
                var values = new List<Value>(sections.Count);
                foreach (var s in sections) values.Add(Value.Section(s));
                return Value.Array(values, SectionType.Instance);
            }
            else
            {
                var section = AbcImport.ParseSingleTune(src, context);
                return Value.Section(section);
            }
        });

        // mml(String source) → Sequence  [Plan 39-04 MML-01]
        var sigMml = new FunctionSignature("mml",
            new FlowType[] { StringType.Instance },
            ParameterNames: new[] { "source" });
        registry.Register("mml", sigMml, args =>
        {
            RequireModuleActivated(context, "mml");
            string src = args[0].As<string>();
            // Phase 44 Plan 44-07: thread the calling ExecutionContext so deep
            // parser helpers can elevate WarnOnce advisories to [strict] errors.
            var seq = MmlImport.ParseMml(src, context);
            return Value.Sequence(seq);
        });
    }

    private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
    {
        if (!context.NotationIoEnabled)
            throw new System.InvalidOperationException(
                $"{builtinName} requires `use \"@notation-io\"`");
    }

    private static readonly Regex XHeaderRegex =
        new Regex(@"^X:\s*\d+", RegexOptions.Multiline | RegexOptions.Compiled);

    private static int CountAbcXHeaders(string source) =>
        XHeaderRegex.Matches(source).Count;
}
