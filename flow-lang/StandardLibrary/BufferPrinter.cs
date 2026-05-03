using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Printer built-ins for the <see cref="AudioBuffer"/> type:
/// <list type="bullet">
///   <item><c>prettyBuffer(Buffer) -> Void</c> — multi-line, human-readable
///         summary (frames, channels, sample rate, duration, peak, RMS) plus
///         a small ASCII waveform.</item>
///   <item><c>bufferHex(Buffer) -> Void</c> — classic hex-editor dump of the
///         buffer's float samples encoded as little-endian IEEE-754 32-bit
///         bytes (16 bytes per row, offset prefix, ASCII gutter).</item>
///   <item><c>bufferHex(Buffer, Int, Int) -> Void</c> — slice of the dump
///         starting at byte <c>offset</c> for at most <c>length</c> bytes.
///         Out-of-range arguments are silently clamped per the project's
///         charitable-interpretation memory.</item>
/// </list>
///
/// Both functions are pure side-effect printers (return Void), mirror the
/// shape of the existing <see cref="VisualizationFunctions"/> builtins, and
/// do NOT alter <see cref="Value.ToString"/> or any existing <c>str(...)</c>
/// overload — they are purely additive surface area.
/// </summary>
public static class BufferPrinter
{
    /// <summary>
    /// Wires <c>prettyBuffer</c> and the two <c>bufferHex</c> overloads into
    /// the supplied <see cref="InternalFunctionRegistry"/>. The arity-1 vs
    /// arity-3 overload of <c>bufferHex</c> is disambiguated by the overload
    /// resolver via argument count alone — no Void-wildcard tricks needed.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        var prettySig = new FunctionSignature("prettyBuffer", [BufferType.Instance]);
        registry.Register("prettyBuffer", prettySig, PrettyBuffer);

        var hexSig = new FunctionSignature("bufferHex", [BufferType.Instance]);
        registry.Register("bufferHex", hexSig, BufferHex);

        var hexSliceSig = new FunctionSignature(
            "bufferHex",
            [BufferType.Instance, IntType.Instance, IntType.Instance]);
        registry.Register("bufferHex", hexSliceSig, BufferHexSlice);
    }

    /// <summary>
    /// Prints a multi-line, human-readable summary of an <see cref="AudioBuffer"/>
    /// followed by a 60-column ASCII waveform. Empty buffers print
    /// <c>(empty buffer)</c> and return Void without throwing.
    /// </summary>
    public static Value PrettyBuffer(IReadOnlyList<Value> args)
    {
        var buf = args[0].As<AudioBuffer>();
        if (buf.Frames == 0)
        {
            Console.WriteLine("(empty buffer)");
            return Value.Void();
        }

        // ----- header stats ----------------------------------------------------
        double durationSeconds = (double)buf.Frames / buf.SampleRate;

        float peak = 0f;
        double sumSq = 0.0;
        for (int i = 0; i < buf.Data.Length; i++)
        {
            float s = buf.Data[i];
            float a = s < 0 ? -s : s;
            if (a > peak) peak = a;
            sumSq += (double)s * s;
        }
        double rms = Math.Sqrt(sumSq / buf.Data.Length);

        string peakDb = FormatDbfs(peak);
        string rmsDb = FormatDbfs(rms);

        string channelLabel = buf.Channels switch
        {
            1 => "mono",
            2 => "stereo",
            _ => $"{buf.Channels}-channel"
        };

        var header = new StringBuilder();
        header.AppendLine("Buffer:");
        header.AppendLine($"  frames      : {buf.Frames}");
        header.AppendLine($"  channels    : {buf.Channels} ({channelLabel})");
        header.AppendLine($"  sample rate : {buf.SampleRate} Hz");
        header.AppendLine($"  duration    : {durationSeconds:F3} s");
        header.AppendLine($"  peak        : {peak:F4} ({peakDb} dBFS)");
        header.AppendLine($"  rms         : {rms:F4}  ({rmsDb} dBFS)");
        Console.Write(header.ToString());

        // ----- 60-column ASCII waveform ---------------------------------------
        // Mirrors the shape of VisualizationFunctions.VisualizeBuffer but smaller
        // (60x11 instead of 80x20) so prettyBuffer fits in a typical terminal
        // alongside the header.
        float[] mono;
        if (buf.Channels == 1)
        {
            mono = buf.Data;
        }
        else
        {
            mono = new float[buf.Frames];
            for (int i = 0; i < buf.Frames; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < buf.Channels; ch++)
                    sum += buf.GetSample(i, ch);
                mono[i] = sum / buf.Channels;
            }
        }

        const int width = 60;
        const int height = 11; // 5 above midline, midline, 5 below
        const int midRow = height / 2;

        char[,] grid = new char[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                grid[r, c] = ' ';

        float step = (float)buf.Frames / width;
        for (int x = 0; x < width; x++)
        {
            int start = (int)(x * step);
            int end = (int)((x + 1) * step);
            if (end > buf.Frames) end = buf.Frames;
            if (end <= start) end = start + 1;
            if (end > buf.Frames) end = buf.Frames;

            float min = 1f;
            float max = -1f;
            for (int i = start; i < end; i++)
            {
                if (mono[i] < min) min = mono[i];
                if (mono[i] > max) max = mono[i];
            }
            // Defensive: silent (or single-sample) bucket — collapse to 0.
            if (min > max) { min = 0f; max = 0f; }

            // Map [-1, +1] -> grid rows. row 0 is top (= +1.0).
            int rMin = (int)((1f - max) * 0.5f * (height - 1));
            int rMax = (int)((1f - min) * 0.5f * (height - 1));
            rMin = Math.Clamp(rMin, 0, height - 1);
            rMax = Math.Clamp(rMax, 0, height - 1);

            for (int r = rMin; r <= rMax; r++)
                grid[r, x] = '*';
        }

        // Midline: '-' wherever it isn't already '*'.
        for (int c = 0; c < width; c++)
        {
            if (grid[midRow, c] == ' ')
                grid[midRow, c] = '-';
        }

        var wf = new StringBuilder();
        for (int r = 0; r < height; r++)
        {
            wf.Append('|');
            for (int c = 0; c < width; c++)
                wf.Append(grid[r, c]);
            wf.AppendLine("|");
        }
        Console.Write(wf.ToString());

        return Value.Void();
    }

    /// <summary>
    /// Prints the buffer's underlying float samples as little-endian IEEE-754
    /// 32-bit bytes in classic 16-bytes-per-row hex-editor format with offset
    /// prefix and ASCII gutter. Empty buffers print <c>(empty buffer)</c>.
    /// </summary>
    public static Value BufferHex(IReadOnlyList<Value> args)
    {
        var buf = args[0].As<AudioBuffer>();
        if (buf.Frames == 0)
        {
            Console.WriteLine("(empty buffer)");
            return Value.Void();
        }

        // Documented assumption per project memory: every platform Flow runs on
        // is little-endian. AsBytes is therefore already in the "wire" order
        // we want without byte-swapping. Asserted for documentation.
        Debug.Assert(BitConverter.IsLittleEndian,
            "BufferPrinter assumes little-endian host (true on every platform Flow targets).");

        byte[] bytes = MemoryMarshal.AsBytes(buf.Data.AsSpan()).ToArray();
        DumpHex(bytes, startIndex: 0, absoluteOffset: 0L, length: bytes.Length);
        return Value.Void();
    }

    /// <summary>
    /// Slice variant: dumps at most <c>length</c> bytes starting at byte
    /// <c>offset</c>. Out-of-range / negative arguments are silently clamped
    /// (charitable interpretation — never throws).
    /// </summary>
    public static Value BufferHexSlice(IReadOnlyList<Value> args)
    {
        var buf = args[0].As<AudioBuffer>();
        int offset = args[1].As<int>();
        int length = args[2].As<int>();

        if (buf.Frames == 0)
        {
            Console.WriteLine("(empty buffer)");
            return Value.Void();
        }

        Debug.Assert(BitConverter.IsLittleEndian,
            "BufferPrinter assumes little-endian host (true on every platform Flow targets).");

        byte[] bytes = MemoryMarshal.AsBytes(buf.Data.AsSpan()).ToArray();

        // Charitable clamping (silent and documented):
        //   - negative offset  -> 0
        //   - negative length  -> 0
        //   - offset past end  -> print "(empty slice)" and return
        //   - length past end  -> clamped to bytes.Length - offset
        if (offset < 0) offset = 0;
        if (offset >= bytes.Length)
        {
            Console.WriteLine("(empty slice)");
            return Value.Void();
        }
        if (length < 0) length = 0;
        int maxLen = bytes.Length - offset;
        if (length > maxLen) length = maxLen;

        DumpHex(bytes, startIndex: offset, absoluteOffset: (long)offset, length: length);
        return Value.Void();
    }

    /// <summary>
    /// Renders a classic xxd-style hex dump:
    ///   <c>OOOOOOOO  bb bb bb bb bb bb bb bb  bb bb bb bb bb bb bb bb  |ascii.gutter....|</c>
    /// Then a final line containing only the end offset for easy length read-off.
    /// </summary>
    private static void DumpHex(byte[] bytes, int startIndex, long absoluteOffset, int length)
    {
        if (length <= 0)
        {
            // Even an empty slice prints the trailing offset line so callers
            // can always count rows.
            Console.WriteLine($"{absoluteOffset:x8}");
            return;
        }

        var sb = new StringBuilder();
        const int bytesPerRow = 16;

        int endExclusive = startIndex + length;
        for (int rowStart = startIndex; rowStart < endExclusive; rowStart += bytesPerRow)
        {
            int rowEnd = Math.Min(rowStart + bytesPerRow, endExclusive);
            int rowBytes = rowEnd - rowStart;
            long rowOffset = absoluteOffset + (rowStart - startIndex);

            // 8-digit lowercase hex offset.
            sb.Append(rowOffset.ToString("x8"));
            sb.Append("  ");

            // Hex columns — 16 slots, double space between byte 7 and byte 8.
            for (int i = 0; i < bytesPerRow; i++)
            {
                if (i == 8) sb.Append(' '); // extra space separator at the midpoint

                if (i < rowBytes)
                {
                    sb.Append(bytes[rowStart + i].ToString("x2"));
                }
                else
                {
                    sb.Append("  "); // missing byte: pad two spaces in place of "xx"
                }

                if (i < bytesPerRow - 1) sb.Append(' ');
            }

            // ASCII gutter — only for bytes actually present in this row.
            sb.Append("  |");
            for (int i = 0; i < rowBytes; i++)
            {
                byte b = bytes[rowStart + i];
                sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
            }
            sb.Append('|');
            sb.AppendLine();
        }

        // Trailing end-offset line.
        long endOffset = absoluteOffset + length;
        sb.Append(endOffset.ToString("x8"));
        sb.AppendLine();

        Console.Write(sb.ToString());
    }

    /// <summary>
    /// Formats a linear amplitude value as dBFS, returning <c>"-inf"</c> for
    /// silent (or near-silent) values so the header doesn't print
    /// <c>-Infinity</c>. Floor at 1e-12 (~ -240 dBFS) is well below any
    /// audio noise floor.
    /// </summary>
    private static string FormatDbfs(double linear)
    {
        if (linear <= 1e-12) return "-inf";
        double db = 20.0 * Math.Log10(linear);
        return db.ToString("F2");
    }
}
