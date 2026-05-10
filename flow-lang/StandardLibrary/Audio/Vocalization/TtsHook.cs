using System.Diagnostics;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// Wraps an external TTS (text-to-speech) process, piping text in and WAV audio out.
/// Default engine is espeak-ng. Users can change via SetCommand().
/// </summary>
public static class TtsHook
{
    private static string _ttsCommand = "espeak-ng --stdout";

    /// <summary>
    /// Sets the external TTS command. The first token is the executable,
    /// the rest are base arguments. The text to speak is appended as a quoted argument.
    /// </summary>
    public static void SetCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("TTS command cannot be null or whitespace", nameof(command));

        _ttsCommand = command;
    }

    /// <summary>
    /// Returns the current TTS command string.
    /// </summary>
    public static string GetCommand() => _ttsCommand;

    /// <summary>
    /// Runs the external TTS process on the given text and returns the resulting audio.
    /// The TTS command must produce WAV data on stdout.
    /// </summary>
    /// <param name="text">Text to synthesize.</param>
    /// <returns>AudioBuffer containing the TTS output.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the TTS command is not found, times out, fails, or produces invalid output.
    /// </exception>
    public static AudioBuffer RunTts(string text)
    {
        var parts = _ttsCommand.Split(' ', 2);
        string executable = parts[0];
        string baseArgs = parts.Length > 1 ? parts[1] : "";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"{baseArgs} \"{text}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            using var memStream = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(memStream);

            if (!process.WaitForExit(30000))
            {
                try { process.Kill(); } catch { /* best effort */ }
                throw new InvalidOperationException("TTS command timed out after 30 seconds");
            }

            if (process.ExitCode != 0)
            {
                string stderr = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"TTS command failed (exit {process.ExitCode}): {stderr}");
            }

            return LoadWavFromStream(memStream);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                $"TTS command not found: '{executable}'. Install it or change with setTtsCommand()");
        }
    }

    /// <summary>
    /// Parses a WAV file from a MemoryStream. Mirrors FileIO.LoadWavInternal logic
    /// but reads from memory instead of disk.
    /// </summary>
    private static AudioBuffer LoadWavFromStream(MemoryStream stream)
    {
        if (stream.Length == 0)
            throw new InvalidOperationException("TTS command produced invalid or empty WAV output");

        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Read RIFF header
        var riffId = new string(reader.ReadChars(4));
        if (riffId != "RIFF")
            throw new InvalidOperationException("TTS command produced invalid or empty WAV output");

        int fileSize = reader.ReadInt32();

        var waveId = new string(reader.ReadChars(4));
        if (waveId != "WAVE")
            throw new InvalidOperationException("TTS command produced invalid or empty WAV output");

        // Parse chunks
        short audioFormat = 0;
        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        bool fmtFound = false;
        float[]? samples = null;

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 8)
                break;

            var chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            switch (chunkId)
            {
                case "fmt ":
                    audioFormat = reader.ReadInt16();
                    if (audioFormat != 1)
                        throw new InvalidOperationException(
                            $"Unsupported WAV format from TTS: expected PCM (1), got {audioFormat}");
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    int byteRate = reader.ReadInt32();
                    short blockAlign = reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (chunkSize > 16)
                        reader.ReadBytes(chunkSize - 16);
                    fmtFound = true;
                    break;

                case "data":
                    if (!fmtFound)
                        throw new InvalidOperationException("WAV from TTS has data chunk before fmt chunk");
                    // Clamp to actual remaining bytes — espeak-ng writes 0x7ffff000
                    // as a placeholder size when streaming to stdout
                    long remainingBytes = stream.Length - stream.Position;
                    long dataBytes = Math.Min((long)chunkSize, remainingBytes);
                    int totalSamples = (int)(dataBytes / (bitsPerSample / 8));
                    samples = ReadSamples(reader, totalSamples, bitsPerSample);
                    break;

                default:
                    int skipBytes = chunkSize;
                    if (chunkSize % 2 != 0)
                        skipBytes++;
                    if (stream.Position + skipBytes <= stream.Length)
                        reader.ReadBytes(skipBytes);
                    else
                        stream.Position = stream.Length;
                    break;
            }
        }

        if (!fmtFound || samples == null)
            throw new InvalidOperationException("TTS command produced invalid or empty WAV output");

        int frames = samples.Length / channels;
        var buffer = new AudioBuffer(frames, channels, sampleRate);
        Array.Copy(samples, buffer.Data, samples.Length);

        // Resample to 44100 Hz if needed
        if (sampleRate != 44100)
            buffer = FileIO.Resample(buffer, 44100);

        return buffer;
    }

    /// <summary>
    /// Reads PCM samples from a BinaryReader, converting to float32.
    /// </summary>
    private static float[] ReadSamples(BinaryReader reader, int totalSamples, short bitsPerSample)
    {
        var samples = new float[totalSamples];

        switch (bitsPerSample)
        {
            case 16:
                for (int i = 0; i < totalSamples; i++)
                    samples[i] = reader.ReadInt16() / 32768f;
                break;

            case 24:
                for (int i = 0; i < totalSamples; i++)
                {
                    byte b0 = reader.ReadByte();
                    byte b1 = reader.ReadByte();
                    byte b2 = reader.ReadByte();
                    int value = b0 | (b1 << 8) | (b2 << 16);
                    if ((value & 0x800000) != 0)
                        value |= unchecked((int)0xFF000000);
                    samples[i] = value / 8388608f;
                }
                break;

            case 32:
                for (int i = 0; i < totalSamples; i++)
                    samples[i] = reader.ReadInt32() / 2147483648f;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported bit depth from TTS: {bitsPerSample}");
        }

        return samples;
    }
}
