namespace FlowLang.Runtime;

/// <summary>
/// REQ-4 / Phase 30 SPEC-4: POCO mapping of the ~/.config/flow/config.toml schema.
/// All five keys are nullable — a missing key in the user's TOML file means "no
/// override, use the baked-in default." The five-key surface is locked by SPEC-4:
///
///   - <see cref="InstallPath"/>          (required at install-time; the rest are optional)
///   - <see cref="DefaultAudioDevice"/>   (e.g. "alsa_output.usb-FocusriteScarlett")
///   - <see cref="DefaultTempo"/>         (BPM; e.g. 90)
///   - <see cref="DefaultTimesig"/>       (string "N/M"; e.g. "3/4")
///   - <see cref="StdlibSearchPath"/>     (list of absolute paths for custom modules)
///
/// The POCO lives in <c>flow-lang/Runtime/</c> (NOT <c>flow-cli/</c>) so the
/// interpreter can read <see cref="FlowConfig.Active"/> without taking a dependency
/// on the CLI project. flow-cli is the only writer (via
/// <c>FlowConfigLoader.LoadFromXdg()</c>); flow-lang stays Tomlyn-free.
/// </summary>
public record FlowConfigPoco
{
    public string? InstallPath { get; init; }
    public string? DefaultAudioDevice { get; init; }
    public int? DefaultTempo { get; init; }
    public string? DefaultTimesig { get; init; }
    public List<string>? StdlibSearchPath { get; init; }

    /// <summary>
    /// Static instance representing "no config loaded yet" — every key is null,
    /// triggering the baked-in defaults at each read site. The interpreter's
    /// <see cref="FlowConfig.Active"/> property starts pointing at this instance
    /// and is replaced by <c>FlowConfigLoader.LoadFromXdg()</c> if a user config
    /// file exists.
    /// </summary>
    public static FlowConfigPoco Defaults { get; } = new();
}

/// <summary>
/// REQ-4 static-singleton config holder. flow-cli writes
/// (<c>FlowConfigLoader.LoadFromXdg()</c>); flow-lang reads from the four propagation
/// sites:
///   - <see cref="Runtime.ExecutionContext.GetMusicalContext"/> reads
///     <see cref="FlowConfigPoco.DefaultTempo"/> + <see cref="FlowConfigPoco.DefaultTimesig"/>
///   - <see cref="Runtime.ModuleLoader"/>'s <c>AdditionalSearchPaths</c> seeded from
///     <see cref="FlowConfigPoco.StdlibSearchPath"/> via <see cref="ConfiguredStdlibSearchPaths"/>
///   - flow-cli Run/Play/Watch commands consult <see cref="FlowConfigPoco.DefaultAudioDevice"/>
///     when <c>--device</c> is unspecified.
/// </summary>
public static class FlowConfig
{
    /// <summary>
    /// The active config. Default: <see cref="FlowConfigPoco.Defaults"/> (all keys null,
    /// triggering existing baked-in fallbacks). flow-cli replaces this at startup;
    /// tests should call <see cref="Reset"/> to restore the default-singleton state.
    /// </summary>
    public static FlowConfigPoco Active { get; set; } = FlowConfigPoco.Defaults;

    /// <summary>
    /// Convenience read for <see cref="FlowEngine"/> + <see cref="ModuleLoader"/>
    /// callers: returns the configured stdlib_search_path list (?? empty list).
    /// Never null. Used by <c>FlowEngine</c> at ModuleLoader construction time to
    /// seed the loader's <c>AdditionalSearchPaths</c>.
    /// </summary>
    public static IReadOnlyList<string> ConfiguredStdlibSearchPaths =>
        Active.StdlibSearchPath ?? new List<string>();

    /// <summary>
    /// Test helper: explicit reset to defaults. Tests must call this in their
    /// setup/teardown to avoid cross-test pollution of the singleton state.
    /// </summary>
    public static void Reset() => Active = FlowConfigPoco.Defaults;
}
