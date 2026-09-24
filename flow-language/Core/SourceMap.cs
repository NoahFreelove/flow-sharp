namespace FlowLang.Core;

/// <summary>
/// Phase 35 LANG-04 Wave 1 — file-path-keyed source-text registry used
/// by the diagnostic renderer (Wave 2a) to quote the offending source
/// line beneath a Rust-style error caret without re-reading the file
/// from disk on every render call.
///
/// <para>
/// Per RESEARCH § "Don't Hand-Roll" table, the lexer registers the
/// transformed source text into the SourceMap on entry to
/// <see cref="FlowEngine.Execute"/> (so the post-pragma-scan text is
/// what diagnostics quote — see Phase 21 PragmaScanner). REPL / eval
/// callers pass a sentinel key (<c>&lt;eval&gt;</c>, <c>&lt;stdin&gt;</c>,
/// <c>&lt;repl&gt;</c>) so the in-memory source string is still
/// retrievable when no file path exists.
/// </para>
///
/// <para>
/// Per-<see cref="FlowEngine"/> instance — back-to-back engines in the
/// test suite do not share state. Long REPL sessions re-register under
/// the same sentinel key on every eval; the prior entry is OVERWRITTEN
/// (no unbounded growth — see threat model T-35-03 in 35-01-PLAN.md).
/// </para>
/// </summary>
public sealed class SourceMap
{
    /// <summary>
    /// REPL eval sentinel — the in-memory key used when no file path
    /// is supplied to <see cref="FlowEngine.Execute"/>. Mirrors the
    /// existing flow-interpreter convention (see Program.cs:73,101
    /// per PATTERNS.md Bucket 2a § SourceMap.cs).
    /// </summary>
    public const string EvalKey = "<eval>";

    /// <summary>
    /// Standard-input sentinel — used by the CLI when source is piped
    /// in via stdin.
    /// </summary>
    public const string StdinKey = "<stdin>";

    /// <summary>
    /// REPL sentinel — used by the interactive REPL session.
    /// </summary>
    public const string ReplKey = "<repl>";

    private readonly Dictionary<string, string> _sourceTexts =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Register source text under the given path key. Overwrites any
    /// prior registration under the same key (REPL re-eval safe).
    /// </summary>
    public void Register(string path, string source)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(source);
        _sourceTexts[path] = source;
    }

    /// <summary>
    /// Retrieve the source text for a registered path. Returns null
    /// when no registration exists (the renderer falls back to "?:?"
    /// in that case).
    /// </summary>
    public string? GetSource(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _sourceTexts.TryGetValue(path, out var src) ? src : null;
    }

    /// <summary>
    /// Try-pattern accessor — preferred over <see cref="GetSource"/>
    /// when the caller needs to distinguish "not registered" from
    /// "registered as empty string".
    /// </summary>
    public bool TryGetSource(string path, out string source)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_sourceTexts.TryGetValue(path, out var s))
        {
            source = s;
            return true;
        }
        source = string.Empty;
        return false;
    }
}
