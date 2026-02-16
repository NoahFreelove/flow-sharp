namespace FlowMidi;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string? inputPath = null;
        string? outputPath = null;
        bool dump = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintUsage();
                    return 0;
                case "--dump":
                    dump = true;
                    break;
                case "-o":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Error: -o requires an output file path.");
                        return 1;
                    }
                    outputPath = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-'))
                    {
                        Console.Error.WriteLine($"Error: Unknown option '{args[i]}'.");
                        return 1;
                    }
                    if (inputPath == null)
                        inputPath = args[i];
                    else if (outputPath == null)
                        outputPath = args[i];
                    else
                    {
                        Console.Error.WriteLine("Error: Too many arguments.");
                        return 1;
                    }
                    break;
            }
        }

        if (inputPath == null)
        {
            Console.Error.WriteLine("Error: No input file specified.");
            PrintUsage();
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: File not found: {inputPath}");
            return 1;
        }

        try
        {
            var bytes = File.ReadAllBytes(inputPath);
            var midiFile = Midi.MidiParser.Parse(bytes);

            if (dump)
            {
                Diagnostics.Dump(midiFile);
                return 0;
            }

            var quantizeResult = Conversion.Quantizer.Quantize(midiFile);
            var flowCode = Conversion.FlowGenerator.Generate(midiFile, quantizeResult, Path.GetFileName(inputPath));

            if (outputPath != null)
            {
                File.WriteAllText(outputPath, flowCode);
                Console.Error.WriteLine($"Converted {inputPath} -> {outputPath}");
            }
            else
            {
                Console.Write(flowCode);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.Error.WriteLine("flow-midi: Convert MIDI files to Flow (.flow) scripts");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  flow-midi <input.mid>              Print .flow to stdout");
        Console.Error.WriteLine("  flow-midi <input.mid> <output.flow>  Write .flow to file");
        Console.Error.WriteLine("  flow-midi <input.mid> -o out.flow    Write .flow to file");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -h, --help    Show this help message");
    }
}
