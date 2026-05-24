using System.Text;
using FlowLang.Core;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Phase 38 Plan 38-02 LIVE-01 — a <c>live &lt;quantize&gt; { ... }</c> block
/// declares a hot-swap unit for <c>flow watch</c> live coding (D-v1.5-07).
///
/// <para>
/// Sister node to <see cref="MusicalContextStatement"/> and
/// <see cref="TuningContextStatement"/>. Differs in that the body is intended
/// to re-evaluate on file save and swap at the next <see cref="QuantizeValue"/>
/// boundary (see <c>LiveReloadManager</c>). The <see cref="BlockId"/> is a
/// stable FNV-1a hash of the block's <see cref="SourceLocation"/> so the
/// per-block pending-buffer slot survives re-renders (D-38-02 multi-block
/// independent swap).
/// </para>
///
/// <para>
/// Quantize accepts (per RESEARCH §A lines 414-457):
/// <list type="bullet">
///   <item>Int + <c>bar</c>/<c>bars</c> identifier suffix — <c>live 1bar { }</c> /
///   <c>live 2bars { }</c></item>
///   <item>NoteValue identifier (<c>q</c>/<c>h</c>/<c>w</c>/<c>e</c>/<c>s</c>) —
///   <c>live q { }</c></item>
///   <item>Omitted — parser synthesizes a 1-bar default at the
///   <see cref="SourceLocation"/> of the <c>live</c> keyword</item>
/// </list>
/// </para>
///
/// <para>
/// Entering a live block emits the D-v1.5-07 stderr advisory once per
/// (line, process) via <see cref="FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce"/>
/// with sentinel <c>live-determinism-optout:&lt;line&gt;</c>. The two-run cmp-clean
/// determinism contract is EXPLICITLY opted out of for live blocks; offline
/// render paths (<c>writeWav</c> / <c>writeMidi</c>) STAY deterministic.
/// </para>
///
/// <para>
/// BlockId collision: FNV-1a 32-bit collision probability for distinct source
/// locations within a single composer's file is astronomically low (would
/// require &gt;65k distinct live blocks per file). Threat T-38-AST accepts
/// this — RESEARCH §A line 456.
/// </para>
/// </summary>
public record LiveBlockStatement(
    SourceLocation Location,
    Expression QuantizeValue,
    IReadOnlyList<Statement> Body,
    int BlockId,
    Span? Span = null
) : Statement(Location)
{
    /// <summary>
    /// FNV-1a 32-bit stable hash of a <see cref="SourceLocation"/>. Same source
    /// location across re-renders → same BlockId → registry slot stable so
    /// <c>LiveReloadManager</c>'s per-block pending-buffer slot survives the
    /// re-parse (D-38-02). Matches the offset-basis (2166136261) + prime
    /// (16777619) constants used by <see cref="FlowLang.Runtime.PrngRegistry"/>
    /// per Phase 36 Plan 36-01 (D-v1.5-06) so all stable-hash sites in the
    /// engine share one formula.
    ///
    /// <para>
    /// Hashed inputs in order: UTF-8 bytes of <see cref="SourceLocation.FileName"/>
    /// (null-coerced to empty), the 4 little-endian bytes of
    /// <see cref="SourceLocation.Line"/>, the 4 little-endian bytes of
    /// <see cref="SourceLocation.Column"/>.
    /// </para>
    /// </summary>
    public static int ComputeBlockId(SourceLocation location)
    {
        unchecked
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;

            uint hash = fnvOffsetBasis;

            // 1. File name (UTF-8 bytes; null → empty).
            string fileName = location.FileName ?? string.Empty;
            byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
            for (int i = 0; i < fileNameBytes.Length; i++)
            {
                hash ^= fileNameBytes[i];
                hash *= fnvPrime;
            }

            // 2. Line (4 bytes, little-endian byte-by-byte mix).
            uint lineU = unchecked((uint)location.Line);
            hash ^= (lineU & 0xFF);          hash *= fnvPrime;
            hash ^= ((lineU >> 8) & 0xFF);   hash *= fnvPrime;
            hash ^= ((lineU >> 16) & 0xFF);  hash *= fnvPrime;
            hash ^= ((lineU >> 24) & 0xFF);  hash *= fnvPrime;

            // 3. Column (4 bytes).
            uint colU = unchecked((uint)location.Column);
            hash ^= (colU & 0xFF);           hash *= fnvPrime;
            hash ^= ((colU >> 8) & 0xFF);    hash *= fnvPrime;
            hash ^= ((colU >> 16) & 0xFF);   hash *= fnvPrime;
            hash ^= ((colU >> 24) & 0xFF);   hash *= fnvPrime;

            return unchecked((int)hash);
        }
    }
}
