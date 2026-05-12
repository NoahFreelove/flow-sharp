using System.Text.Json;
using FlowLang.Runtime;
using Tomlyn;

namespace FlowCli.Config;

/// <summary>
/// REQ-4 (Plan 30-03 Task 2): reads <c>~/.config/flow/config.toml</c> via Tomlyn
/// 2.3.2 and populates the engine-side <see cref="FlowConfig.Active"/> singleton
/// (which lives in <c>flow-lang/Runtime/</c> so the interpreter can read it
/// without a circular dependency on flow-cli).
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
    public static void LoadFromXdg()
    {
        if (!File.Exists(ConfigPath))
        {
            // Silent fallback per SPEC-4 acceptance — no warning, no error.
            return;
        }

        try
        {
            var text = File.ReadAllText(ConfigPath);
            var model = TomlSerializer.Deserialize<FlowConfigPoco>(text, SerializerOptions);
            FlowConfig.Active = model ?? FlowConfigPoco.Defaults;
        }
        catch (Exception ex) when (ex is TomlException || ex is IOException)
        {
            // Charitable per CLAUDE.md memory: warn once + continue with defaults.
            Console.Error.WriteLine($"Warning: {ConfigPath} could not be parsed: {ex.Message}");
            Console.Error.WriteLine("Falling back to baked-in defaults.");
            FlowConfig.Active = FlowConfigPoco.Defaults;
        }
    }
}
