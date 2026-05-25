using System.Collections.Generic;
using FlowLang.Diagnostics;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 Plan 39-03 ABC-01 / ABC-02 — hand-rolled tokenizer for the ABC 2.1
/// subset Flow consumes. Coexists with <see cref="AbcImport"/> which walks
/// the token stream to build <see cref="FlowLang.TypeSystem.SpecialTypes.SectionData"/>.
///
/// <para>
/// Charitable per D-39-17: unknown characters drop with a one-shot
/// <see cref="RenderingDiagnostics.WarnOnce"/> advisory and the lexer
/// advances. Decorations <c>!...!</c> and <c>+...+</c> are captured as
/// <see cref="AbcTokenType.Decoration"/> tokens; the parser drops them
/// with a <c>[abc] dropped ornament</c> advisory per D-39-15.
/// </para>
/// </summary>
internal enum AbcTokenType
{
    Header,       // "X:1" or "T:My Tune" or "K:Cmaj" — text is "{letter}:{rest of line}"
    Note,         // "A".."G" or "a".."g"
    Accidental,   // ^ (sharp), ^^ (double-sharp), _ (flat), __ (double-flat), = (natural)
    OctaveUp,     // '
    OctaveDown,   // ,
    Duration,     // "2" or "/" or "/2" — text is the literal duration spec
    BarLine,      // |, ||, |], :|, |:
    Rest,         // z (rest) or Z (multi-measure rest, treated as single rest by Flow)
    Tie,          // -
    Decoration,   // !ff! or +ff+ — text is the inner content
    Ornament,     // ~, T, H, S, O, M, P, u, v
    OpenChord,    // [
    CloseChord,   // ]
    Quote,        // "Allegro" or "Gm" — chord symbol annotation
    Newline,      // \n
    Eof,
    Unknown,      // captured for diagnostic; advancing
}

internal readonly record struct AbcToken(AbcTokenType Type, string Text, int Line, int Column);

internal static class AbcLexer
{
    public static IReadOnlyList<AbcToken> Tokenize(string source)
    {
        var tokens = new List<AbcToken>();
        if (source == null) return tokens;

        int line = 1;
        int col = 1;
        int i = 0;
        bool atLineStart = true;

        while (i < source.Length)
        {
            char c = source[i];

            // Newline handling — line-tracking + reset atLineStart
            if (c == '\n')
            {
                tokens.Add(new AbcToken(AbcTokenType.Newline, "\n", line, col));
                line++;
                col = 1;
                i++;
                atLineStart = true;
                continue;
            }
            if (c == '\r')
            {
                // Skip CR (handled by next \n)
                i++;
                continue;
            }

            // Header lines: detect "X:..." pattern at line start. Capture entire line.
            if (atLineStart && i + 1 < source.Length && char.IsLetter(c) && source[i + 1] == ':')
            {
                int lineStart = i;
                while (i < source.Length && source[i] != '\n' && source[i] != '\r')
                    i++;
                string headerText = source.Substring(lineStart, i - lineStart);
                tokens.Add(new AbcToken(AbcTokenType.Header, headerText, line, col));
                col += i - lineStart;
                atLineStart = false;
                continue;
            }

            // Comment lines: % to end of line — skip
            if (atLineStart && c == '%')
            {
                while (i < source.Length && source[i] != '\n' && source[i] != '\r')
                    i++;
                continue;
            }

            // Whitespace: skip (preserves token boundaries)
            if (c == ' ' || c == '\t')
            {
                i++; col++;
                continue;
            }

            atLineStart = false;

            // Accidentals: ^^, ^, __, _, =
            if (c == '^')
            {
                if (i + 1 < source.Length && source[i + 1] == '^')
                {
                    tokens.Add(new AbcToken(AbcTokenType.Accidental, "^^", line, col));
                    i += 2; col += 2;
                }
                else
                {
                    tokens.Add(new AbcToken(AbcTokenType.Accidental, "^", line, col));
                    i++; col++;
                }
                continue;
            }
            if (c == '_')
            {
                if (i + 1 < source.Length && source[i + 1] == '_')
                {
                    tokens.Add(new AbcToken(AbcTokenType.Accidental, "__", line, col));
                    i += 2; col += 2;
                }
                else
                {
                    tokens.Add(new AbcToken(AbcTokenType.Accidental, "_", line, col));
                    i++; col++;
                }
                continue;
            }
            if (c == '=')
            {
                tokens.Add(new AbcToken(AbcTokenType.Accidental, "=", line, col));
                i++; col++;
                continue;
            }

            // Octave shift
            if (c == '\'')
            {
                tokens.Add(new AbcToken(AbcTokenType.OctaveUp, "'", line, col));
                i++; col++;
                continue;
            }
            if (c == ',')
            {
                tokens.Add(new AbcToken(AbcTokenType.OctaveDown, ",", line, col));
                i++; col++;
                continue;
            }

            // Bar lines
            if (c == '|')
            {
                int startCol = col;
                if (i + 1 < source.Length && source[i + 1] == '|')
                {
                    tokens.Add(new AbcToken(AbcTokenType.BarLine, "||", line, startCol));
                    i += 2; col += 2;
                }
                else if (i + 1 < source.Length && source[i + 1] == ']')
                {
                    tokens.Add(new AbcToken(AbcTokenType.BarLine, "|]", line, startCol));
                    i += 2; col += 2;
                }
                else if (i + 1 < source.Length && source[i + 1] == ':')
                {
                    tokens.Add(new AbcToken(AbcTokenType.BarLine, "|:", line, startCol));
                    i += 2; col += 2;
                }
                else
                {
                    tokens.Add(new AbcToken(AbcTokenType.BarLine, "|", line, startCol));
                    i++; col++;
                }
                continue;
            }
            if (c == ':' && i + 1 < source.Length && source[i + 1] == '|')
            {
                tokens.Add(new AbcToken(AbcTokenType.BarLine, ":|", line, col));
                i += 2; col += 2;
                continue;
            }

            // Duration: digit sequences (numerator) or / + optional digits (denominator)
            if (char.IsDigit(c))
            {
                int start = i;
                while (i < source.Length && char.IsDigit(source[i])) i++;
                string dur = source.Substring(start, i - start);
                tokens.Add(new AbcToken(AbcTokenType.Duration, dur, line, col));
                col += i - start;
                continue;
            }
            if (c == '/')
            {
                int start = i;
                i++;
                while (i < source.Length && char.IsDigit(source[i])) i++;
                string dur = source.Substring(start, i - start);
                tokens.Add(new AbcToken(AbcTokenType.Duration, dur, line, col));
                col += i - start;
                continue;
            }

            // Tie
            if (c == '-')
            {
                tokens.Add(new AbcToken(AbcTokenType.Tie, "-", line, col));
                i++; col++;
                continue;
            }

            // Rest
            if (c == 'z' || c == 'Z')
            {
                tokens.Add(new AbcToken(AbcTokenType.Rest, c.ToString(), line, col));
                i++; col++;
                continue;
            }

            // Note letter (A-G or a-g)
            if ((c >= 'A' && c <= 'G') || (c >= 'a' && c <= 'g'))
            {
                tokens.Add(new AbcToken(AbcTokenType.Note, c.ToString(), line, col));
                i++; col++;
                continue;
            }

            // Decorations: !...! or +...+
            if (c == '!')
            {
                int start = i + 1;
                int end = source.IndexOf('!', start);
                if (end < 0) { i++; col++; continue; }  // unterminated — skip char
                string inner = source.Substring(start, end - start);
                tokens.Add(new AbcToken(AbcTokenType.Decoration, inner, line, col));
                int consumed = end - i + 1;
                i = end + 1; col += consumed;
                continue;
            }
            if (c == '+')
            {
                int start = i + 1;
                int end = source.IndexOf('+', start);
                if (end < 0) { i++; col++; continue; }
                string inner = source.Substring(start, end - start);
                tokens.Add(new AbcToken(AbcTokenType.Decoration, inner, line, col));
                int consumed = end - i + 1;
                i = end + 1; col += consumed;
                continue;
            }

            // Chord brackets
            if (c == '[')
            {
                tokens.Add(new AbcToken(AbcTokenType.OpenChord, "[", line, col));
                i++; col++;
                continue;
            }
            if (c == ']')
            {
                tokens.Add(new AbcToken(AbcTokenType.CloseChord, "]", line, col));
                i++; col++;
                continue;
            }

            // Quoted annotations (chord symbols / text)
            if (c == '"')
            {
                int start = i + 1;
                int end = source.IndexOf('"', start);
                if (end < 0) { i++; col++; continue; }
                string inner = source.Substring(start, end - start);
                tokens.Add(new AbcToken(AbcTokenType.Quote, inner, line, col));
                int consumed = end - i + 1;
                i = end + 1; col += consumed;
                continue;
            }

            // Ornaments — single character
            if (c == '~' || c == 'T' || c == 'H' || c == 'S' || c == 'O'
                || c == 'M' || c == 'P' || c == 'u' || c == 'v')
            {
                tokens.Add(new AbcToken(AbcTokenType.Ornament, c.ToString(), line, col));
                i++; col++;
                continue;
            }

            // Unknown character — charitable advisory + advance.
            // Phase 44 Plan 44-07: route through AbcImport.EmitAbcAdvisory so
            // the strict-mode branch fires when caller is in strict-mode (the
            // thread-local strictCtx is set by AbcImport.ParseSingleTune /
            // ParseMultiTune entry points).
            AbcImport.EmitAbcAdvisory(
                sentinelKey: $"abc-token:{c}:{line}",
                sentinelBody: $"[abc] unknown character '{c}' at line {line} col {col}",
                strictBody: $"[abc] unknown character '{c}' at line {line} col {col}");
            tokens.Add(new AbcToken(AbcTokenType.Unknown, c.ToString(), line, col));
            i++; col++;
        }

        tokens.Add(new AbcToken(AbcTokenType.Eof, string.Empty, line, col));
        return tokens;
    }
}
