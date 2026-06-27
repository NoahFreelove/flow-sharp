namespace FlowLang.StandardLibrary;

/// <summary>
/// Static lookup table mapping built-in function name to a human-readable doc.
/// Hover handler reads this; falls back to signature-only when the key is absent.
/// Add new entries here when you register a new built-in.
///
/// Per CONTEXT D-12: file lives in flow-lang (not flow-lsp) so the interpreter can
/// reuse it later (future `flow --help &lt;fn&gt;`). Phase 17 plan 05 expansion covers
/// arithmetic, collections, audio core + effects, transforms, and harmony built-ins.
/// Each entry is a one-line composer-facing summary; Params optional for less-used
/// functions.
/// </summary>
public static class BuiltInDocs
{
    public sealed record Doc(string Summary, IReadOnlyList<ParamDoc> Params, string? Example = null);
    public sealed record ParamDoc(string Name, string Description);

    private static readonly IReadOnlyDictionary<string, Doc> _docs = new Dictionary<string, Doc>
    {
        // ===== I/O =====
        ["print"] = new("Prints a string to standard output.", new ParamDoc[] {
            new("s", "The string to print."),
        }),
        ["input"] = new("Reads a line from standard input.", Array.Empty<ParamDoc>()),
        ["str"] = new("Converts a value to its string representation.", new ParamDoc[] {
            new("value", "The value to stringify."),
        }),

        // ===== String / Collection operators =====
        ["concat"] = new("Concatenates two strings or arrays into one.", new ParamDoc[] {
            new("a", "First string/array."),
            new("b", "Second string/array."),
        }),
        ["reverse"] = new("Returns a new array with the elements in reverse order.", Array.Empty<ParamDoc>()),
        ["length"] = new("Returns the number of elements in an array or characters in a string.", Array.Empty<ParamDoc>()),
        ["len"] = new("Returns the number of elements in an array or characters in a string.", Array.Empty<ParamDoc>()),
        ["range"] = new("Produces an inclusive-exclusive integer range as an array.", Array.Empty<ParamDoc>()),
        ["append"] = new("Returns a new array with the element appended at the end.", Array.Empty<ParamDoc>()),
        ["prepend"] = new("Returns a new array with the element inserted at the front.", Array.Empty<ParamDoc>()),

        // ===== Collections =====
        ["head"] = new("Returns the first element of an array. Raises on empty.", Array.Empty<ParamDoc>()),
        ["tail"] = new("Returns every element except the first.", Array.Empty<ParamDoc>()),
        ["last"] = new("Returns the last element of an array.", Array.Empty<ParamDoc>()),
        ["init"] = new("Returns every element except the last.", Array.Empty<ParamDoc>()),
        ["map"] = new("Applies a function to every element, returning a new array.", new ParamDoc[] {
            new("arr", "The source array."),
            new("callback", "Function applied to each element."),
        }),
        ["filter"] = new("Returns a new array keeping only elements where the predicate is true.", new ParamDoc[] {
            new("arr", "The source array."),
            new("callback", "Predicate — returns Bool."),
        }),
        ["reduce"] = new("Folds an array to a single value using an accumulator.", new ParamDoc[] {
            new("arr", "The source array."),
            new("initial", "Initial accumulator value."),
            new("callback", "(acc, item) => newAcc."),
        }),
        ["each"] = new("Invokes a function for each element, discarding results.", Array.Empty<ParamDoc>()),
        ["take"] = new("Returns the first N elements of an array.", Array.Empty<ParamDoc>()),
        ["drop"] = new("Returns every element after the first N.", Array.Empty<ParamDoc>()),
        ["slice"] = new("Returns a sub-array/sub-sequence bounded by start/end (silently clamped).", Array.Empty<ParamDoc>()),
        ["empty"] = new("Returns true iff the array has zero elements.", Array.Empty<ParamDoc>()),
        ["zip"] = new("Combines two arrays into an array of pairs, stopping at the shorter length.", Array.Empty<ParamDoc>()),
        ["contains"] = new("Returns true if the array contains the given element.", Array.Empty<ParamDoc>()),

        // ===== Arithmetic =====
        ["add"] = new("Returns a + b. Overloads for Int, Float, Double.", Array.Empty<ParamDoc>()),
        ["sub"] = new("Returns a - b. Overloads for Int, Float, Double.", Array.Empty<ParamDoc>()),
        ["mul"] = new("Returns a * b. Overloads for Int, Float, Double.", Array.Empty<ParamDoc>()),
        ["div"] = new("Returns a / b. Overloads for Int, Float, Double.", Array.Empty<ParamDoc>()),
        ["mod"] = new("Returns a modulo b.", Array.Empty<ParamDoc>()),
        ["pow"] = new("Returns base raised to the exponent.", Array.Empty<ParamDoc>()),
        ["abs"] = new("Absolute value (Int or Double overloads).", Array.Empty<ParamDoc>()),
        ["min"] = new("Smaller of two values.", Array.Empty<ParamDoc>()),
        ["max"] = new("Larger of two values.", Array.Empty<ParamDoc>()),
        ["floor"] = new("Largest integer <= value.", Array.Empty<ParamDoc>()),
        ["ceil"] = new("Smallest integer >= value.", Array.Empty<ParamDoc>()),
        ["round"] = new("Nearest integer to value.", Array.Empty<ParamDoc>()),
        ["sqrt"] = new("Square root.", Array.Empty<ParamDoc>()),
        ["sin"] = new("Sine of angle in radians.", Array.Empty<ParamDoc>()),
        ["cos"] = new("Cosine of angle in radians.", Array.Empty<ParamDoc>()),
        ["tan"] = new("Tangent of angle in radians.", Array.Empty<ParamDoc>()),
        ["log"] = new("Natural logarithm.", Array.Empty<ParamDoc>()),

        // ===== Audio core =====
        ["buffer"] = new("Creates an empty audio buffer of the given frame count / channels / sample rate.", Array.Empty<ParamDoc>()),
        ["silence"] = new("Produces a buffer of silence for the given duration.", Array.Empty<ParamDoc>()),
        ["sine"] = new("Generates a sine-wave buffer at the given frequency and duration.", new ParamDoc[] {
            new("freq", "Frequency in Hz."),
            new("duration", "Length in seconds."),
        }),
        ["saw"] = new("Generates a sawtooth-wave buffer.", Array.Empty<ParamDoc>()),
        ["square"] = new("Generates a square-wave buffer.", Array.Empty<ParamDoc>()),
        ["triangle"] = new("Generates a triangle-wave buffer.", Array.Empty<ParamDoc>()),
        // Tone constructors. Canonical Hertz-first form (frequency, duration-seconds, amplitude):
        //   (createSineTone 440Hz 1.0 0.5).  Typed duration-first forms also exist:
        //   (createSineTone 1.0s 440Hz 0.5) and (createSineTone 1.0s 440.0 0.5).
        ["createSineTone"] = new("Generates a band-limited sine-wave buffer. Canonical form: (createSineTone freqHz durationSeconds amplitude).", new ParamDoc[] {
            new("frequency", "Pitch — Hertz literal (440Hz) or a Double in Hz."),
            new("duration", "Length in seconds (Double)."),
            new("amplitude", "Peak level 0.0..1.0."),
        }, "(play (createSineTone 440Hz 1.0 0.5))"),
        ["createSawTone"] = new("Generates a naive (aliased) sawtooth-wave buffer. Canonical form: (createSawTone freqHz durationSeconds amplitude). Use the \"saw\" renderSong instrument for PolyBLEP band-limited rendering.", new ParamDoc[] {
            new("frequency", "Pitch — Hertz literal (220Hz) or a Double in Hz."),
            new("duration", "Length in seconds (Double)."),
            new("amplitude", "Peak level 0.0..1.0."),
        }, "(play (createSawTone 220Hz 1.0 0.5))"),
        ["createSquareTone"] = new("Generates a naive (aliased) square-wave buffer. Canonical form: (createSquareTone freqHz durationSeconds amplitude). Use the \"square\" renderSong instrument for PolyBLEP band-limited rendering.", new ParamDoc[] {
            new("frequency", "Pitch — Hertz literal (330Hz) or a Double in Hz."),
            new("duration", "Length in seconds (Double)."),
            new("amplitude", "Peak level 0.0..1.0."),
        }, "(play (createSquareTone 330Hz 1.0 0.5))"),
        ["createTriangleTone"] = new("Generates a triangle-wave buffer. Canonical form: (createTriangleTone freqHz durationSeconds amplitude).", new ParamDoc[] {
            new("frequency", "Pitch — Hertz literal (262Hz) or a Double in Hz."),
            new("duration", "Length in seconds (Double)."),
            new("amplitude", "Peak level 0.0..1.0."),
        }, "(play (createTriangleTone 262Hz 1.0 0.5))"),
        ["noise"] = new("Generates a white-noise buffer.", Array.Empty<ParamDoc>()),
        ["adsr"] = new("Builds an ADSR envelope with the given attack/decay/sustain/release.", new ParamDoc[] {
            new("a", "Attack in ms."),
            new("d", "Decay in ms."),
            new("s", "Sustain level (0..1)."),
            new("r", "Release in ms."),
        }),
        ["applyEnvelope"] = new("Applies an envelope to a buffer, returning a new buffer.", Array.Empty<ParamDoc>()),
        ["mix"] = new("Mixes two buffers sample-by-sample into one.", Array.Empty<ParamDoc>()),
        ["writeWav"] = new("Writes a buffer to disk as a WAV file (path, buffer[, bitDepth]).", new ParamDoc[] {
            new("filepath", "Output path (created if missing)."),
            new("buffer", "Buffer to export."),
        }),
        ["loadWav"] = new("Reads a WAV file into a buffer (16/24/32-bit, resamples to 44100Hz).", Array.Empty<ParamDoc>()),

        // ===== Audio effects =====
        ["reverb"] = new("Applies a reverb effect with room size (and optional damping + mix).", new ParamDoc[] {
            new("buffer", "Input buffer."),
            new("roomSize", "0.0 small room, 1.0 large hall."),
        }, "(reverb (createSineTone 440Hz 1.0 0.5) 0.8)"),
        ["lowpass"] = new("Low-pass filter — removes frequencies above cutoff Hz.", new ParamDoc[] {
            new("buffer", "Input buffer."),
            new("cutoff", "Cutoff frequency in Hz."),
        }, "(lowpass (createSawTone 220Hz 1.0 0.5) 800Hz)"),
        ["highpass"] = new("High-pass filter — removes frequencies below cutoff Hz.", Array.Empty<ParamDoc>()),
        ["bandpass"] = new("Band-pass filter — keeps frequencies between low and high cutoffs.", Array.Empty<ParamDoc>()),
        ["compress"] = new("Dynamic range compressor (threshold dB, ratio, optional attack/release ms).", new ParamDoc[] {
            new("buffer", "Input buffer."),
            new("threshold", "Threshold in dB."),
            new("ratio", "Compression ratio."),
        }),
        ["sidechain"] = new("Sidechain compressor — duck the source when the trigger peaks.", Array.Empty<ParamDoc>()),
        ["delay"] = new("Feedback delay (time ms, feedback, mix).", Array.Empty<ParamDoc>()),
        ["gain"] = new("Applies gain in dB (negative attenuates, positive amplifies).", Array.Empty<ParamDoc>()),
        ["pan"] = new("Constant-power stereo panning (-1.0 hard left, 1.0 hard right).", Array.Empty<ParamDoc>()),

        // ===== Playback =====
        ["play"] = new("Plays a buffer (or sequence) through the audio backend. Blocks.", new ParamDoc[] {
            new("buffer", "Buffer (or sequence) to play."),
        }, "(play (createSineTone 440Hz 1.0 0.5))"),
        ["loop"] = new("Loops a buffer indefinitely or N times (non-blocking).", Array.Empty<ParamDoc>()),
        ["stream"] = new("Plays a buffer in the background without blocking the interpreter.", Array.Empty<ParamDoc>()),
        ["preview"] = new("Low-quality mono preview playback (22050 Hz).", Array.Empty<ParamDoc>()),
        ["stop"] = new("Stops any currently playing audio.", Array.Empty<ParamDoc>()),

        // ===== Harmony =====
        ["chordNotes"] = new("Returns the notes of a chord as an array.", new ParamDoc[] {
            new("c", "The chord."),
        }),
        ["chordRoot"] = new("Returns the root note of a chord.", Array.Empty<ParamDoc>()),
        ["chordQuality"] = new("Returns the quality of a chord (maj, min, dim, etc.).", Array.Empty<ParamDoc>()),
        ["arpeggio"] = new("Expands a chord into an arpeggio sequence.", new ParamDoc[] {
            new("c", "The chord."),
            new("direction", "\"up\", \"down\", or \"updown\"."),
        }),
        ["scaleNotes"] = new("Returns the scale tones of a named key (e.g. \"Cmajor\").", Array.Empty<ParamDoc>()),
        ["resolveNumeral"] = new("Resolves a roman numeral (I, IV, V7) to a chord in the given key.", Array.Empty<ParamDoc>()),
        ["enharmonic"] = new("Returns the enharmonic respelling of a note in the active key.", Array.Empty<ParamDoc>()),

        // ===== Transforms =====
        ["transpose"] = new("Transposes a sequence by an interval (Semitone or Cent).", new ParamDoc[] {
            new("seq", "The sequence."),
            new("interval", "Semitone(n) or Cent(n)."),
        }),
        ["invert"] = new("Inverts a sequence around its first note.", Array.Empty<ParamDoc>()),
        ["retrograde"] = new("Reverses a sequence (retrograde).", Array.Empty<ParamDoc>()),
        ["augment"] = new("Doubles every note duration.", Array.Empty<ParamDoc>()),
        ["diminish"] = new("Halves every note duration.", Array.Empty<ParamDoc>()),
        ["up"] = new("Shifts every note up by N octaves.", Array.Empty<ParamDoc>()),
        ["down"] = new("Shifts every note down by N octaves.", Array.Empty<ParamDoc>()),
        ["repeat"] = new("Repeats a sequence N times (optionally transposing each repeat).", Array.Empty<ParamDoc>()),
        ["crescendo"] = new("Applies a linear velocity ramp from start to end.", Array.Empty<ParamDoc>()),
        ["decrescendo"] = new("Applies a decreasing linear velocity ramp.", Array.Empty<ParamDoc>()),
        ["swell"] = new("Velocity swell — rises to peak mid-sequence then falls.", Array.Empty<ParamDoc>()),
        ["ritardando"] = new("Gradually slows the tempo over the sequence.", Array.Empty<ParamDoc>()),
        ["accelerando"] = new("Gradually speeds up the tempo over the sequence.", Array.Empty<ParamDoc>()),
        ["humanize"] = new("Applies small random timing/velocity offsets for natural feel.", Array.Empty<ParamDoc>()),
        ["trill"] = new("Replaces each note with a rapid alternation at the given interval.", Array.Empty<ParamDoc>()),
        ["tremolo"] = new("Replaces each note with a rapid repetition.", Array.Empty<ParamDoc>()),

        // ===== Musical notation =====
        ["musicalNote"] = new("Constructs a musical note from pitch and duration.", Array.Empty<ParamDoc>()),
        ["rest"] = new("Constructs a rest of the given duration.", Array.Empty<ParamDoc>()),
        ["renderSequence"] = new("Renders a sequence to a buffer using the specified instrument.", Array.Empty<ParamDoc>()),
        ["renderSequences"] = new("Renders multiple sequences and mixes them.", Array.Empty<ParamDoc>()),
        ["renderSong"] = new("Renders a song arrangement to a stereo buffer.", new ParamDoc[] {
            new("song", "The song."),
            new("instrument", "\"piano\", \"brass\", \"sax\", \"drums\", or a user lambda."),
        }),
        ["visualize"] = new("Prints an ASCII piano-roll of a sequence (or buffer waveform).", Array.Empty<ParamDoc>()),
        ["euclidean"] = new("Produces a Euclidean rhythm: N hits spread evenly across M steps.", Array.Empty<ParamDoc>()),
        ["polyrhythm"] = new("Overlays two sequences with different time signatures, aligned at LCM.", Array.Empty<ParamDoc>()),
        ["vary"] = new("Probabilistic variation — mutates pitch/rhythm/rest/velocity by probability.", Array.Empty<ParamDoc>()),
        ["tempoRamp"] = new("Renders a sequence with BPM interpolated from startBpm to endBpm.", Array.Empty<ParamDoc>()),

        // ===== Vocalization =====
        ["sing"] = new("Synthesizes a vowel/syllable at pitch and duration via formant synthesis.", Array.Empty<ParamDoc>()),
        ["tts"] = new("Text-to-speech — returns a buffer via the configured TTS backend.", Array.Empty<ParamDoc>()),

        // ===== MIDI =====
        ["writeMidi"] = new("Writes a song arrangement to a MIDI file.", new ParamDoc[] {
            new("filepath", "Output path."),
            new("song", "The song."),
        }),
    };

    public static Doc? TryGet(string name) =>
        _docs.TryGetValue(name, out var doc) ? doc : null;

    /// <summary>
    /// Phase 41 (DOC-01, D-08): read-only view over every built-in doc entry, so
    /// the <c>flow doc</c> generator (Plan 41-03) can enumerate the ~104 entries
    /// directly with no duplication. The backing <c>_docs</c> dictionary stays
    /// private — only this immutable read view is exposed. The existing
    /// <see cref="TryGet"/> single-name lookup (Phase 38 <c>:help fn</c> surface)
    /// is unchanged.
    /// </summary>
    public static IReadOnlyDictionary<string, Doc> All => _docs;
}
