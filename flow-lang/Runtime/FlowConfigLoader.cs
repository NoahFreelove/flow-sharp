#if !FLOW_WEB
using System.Text.Json;
using Tomlyn;

namespace FlowLang.Runtime;

/// <summary>
/// REQ-4 (Plan 30-03 Task 2): reads <c>~/.config/flow/config.toml</c> via Tomlyn
/// 2.3.2 and populates the engine-side <see cref="FlowConfig.Active"/> singleton.
///
/// <para>
/// Lives in <c>flow-lang/Runtime/</c> (moved here from <c>flow-cli/Config/</c> in
/// the sweep-0614 fix) so BOTH the <c>flow</c> CLI (<c>flow-cli</c>) AND the
/// bare interpreter (<c>flow-interpreter</c>, the <c>dotnet run --project
/// flow-interpreter</c> path) can load the user's config before any
/// <see cref="FlowLang.Core.FlowEngine"/> is constructed. Previously only
/// flow-cli called this, so the interpreter silently ignored
/// <c>~/.config/flow/config.toml</c> — <c>sfz_root</c> stayed null and
/// <c>loadSfz #violin</c> hard-failed under the interpreter even with a valid
/// config file. flow-interpreter references flow-lang (no circular dependency),
/// so the loader belongs here.
/// </para>
///
/// <para>
/// Tomlyn 2.3.2 is a Desktop-only dependency (added to flow-lang.csproj with
/// <c>Condition="'$(FlowTarget)' != 'Web'"</c>). This whole file is
/// <c>#if !FLOW_WEB</c>-guarded so the Web/WASM build never references Tomlyn
/// (AssemblyReferenceScanTests forbids the "Tomlyn" type-reference prefix on
/// the Web target). The browser sandbox has no <c>~/.config</c> anyway.
/// </para>
///
/// Charitable behavior per CLAUDE.md feedback_charitable_interpretation memory:
///
///   - Missing file       -> silent fallback to <see cref="FlowConfigPoco.Defaults"/>
///                           (no warning, no error, exit 0).
///   - Malformed TOML     -> single "Warning:" line to stderr + fall back to
///                           defaults; DO NOT abort. Composer can still run the
///                           CLI; they just lose their config overrides.
///   - IO error           -> same as malformed (charitable warn, continue).
///
/// Tomlyn 2.x is a System.Text.Json-style API: <c>TomlSerializer.Deserialize&lt;T&gt;</c>
/// + <c>TomlSerializerOptions.PropertyNamingPolicy</c>. We pass
/// <see cref="JsonNamingPolicy.SnakeCaseLower"/> so the snake_case TOML keys
/// (<c>default_tempo</c>) deserialize into the PascalCase POCO properties
/// (<c>DefaultTempo</c>) without per-property <c>[JsonPropertyName]</c> attributes.
/// </summary>
public static class FlowConfigLoader
{
    /// <summary>
    /// Tomlyn 2.x serializer options: snake_case key naming. Static so successive
    /// loads (test rapid-fire <see cref="LoadFromXdg"/> calls) reuse one instance.
    /// </summary>
    private static readonly TomlSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// XDG path: <c>$HOME/.config/flow/config.toml</c>. Per SPEC-4 (RESEARCH Open
    /// Question 5 closure): we DO NOT honor <c>$XDG_CONFIG_HOME</c> — the
    /// composer-facing locked path is exactly this concrete location to keep
    /// install-docs simple.
    /// </summary>
    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "flow", "config.toml");

    /// <summary>
    /// Reads <see cref="ConfigPath"/>, parses, and assigns to
    /// <see cref="FlowConfig.Active"/>. Must be called BEFORE any
    /// <see cref="FlowLang.Core.FlowEngine"/> is constructed — FlowEngine's
    /// constructor reads <see cref="FlowConfig.ConfiguredStdlibSearchPaths"/> at
    /// ModuleLoader-seed time, so the config must be active by then.
    /// </summary>
    public static void LoadFromXdg() => LoadFromFile(ConfigPath);

    /// <summary>
    /// Reads <paramref name="path"/>, parses, and assigns to
    /// <see cref="FlowConfig.Active"/>. Factored out of <see cref="LoadFromXdg"/>
    /// so the sweep-0614 regression test can exercise the parse/populate logic
    /// against a temp file without touching the real <c>~/.config/flow/config.toml</c>.
    /// Same charitable contract as <see cref="LoadFromXdg"/>: missing file →
    /// silent fallback; malformed/IO → warn once + defaults.
    /// </summary>
    internal static void LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            // Silent fallback per SPEC-4 acceptance — no warning, no error.
            return;
        }

        try
        {
            var text = File.ReadAllText(path);
            var model = TomlSerializer.Deserialize<FlowConfigPoco>(text, SerializerOptions);
            FlowConfig.Active = model ?? FlowConfigPoco.Defaults;
        }
        catch (Exception ex) when (ex is TomlException || ex is IOException)
        {
            // Charitable per CLAUDE.md memory: warn once + continue with defaults.
            Console.Error.WriteLine($"Warning: {path} could not be parsed: {ex.Message}");
            Console.Error.WriteLine("Falling back to baked-in defaults.");
            FlowConfig.Active = FlowConfigPoco.Defaults;
        }
    }
}
#endif
