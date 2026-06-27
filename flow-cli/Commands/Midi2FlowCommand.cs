using System.CommandLine;
using FlowMidi.Conversion;
using FlowMidi.Midi;

namespace FlowCli.Commands;

// `flow midi2flow <input.mid> [-o <output.flow>]` — Plan 30-09 Task 1.
//
// Real handler that replaces Midi2FlowStubCommand (Plan 30-02 deferral).
// Pipeline: File.ReadAllBytes -> MidiParser.Parse (flow-midi/Midi/) ->
// Quantizer.Quantize (flow-midi/Conversion/) -> FlowGenerator.Generate
// (flow-midi/Conversion/) with roundTrip:true (Plan 30-08) -> File.WriteAllText.
//
// Why roundTrip:true (and not the default false): SPEC-5 mandates flat,
// round-trippable output — no `(play output)` trailer, no `_rh`/`_lh` pitch
// split (Plan 30-07 deleted the heuristic), explicit duration on every note,
// `section roundtrip` + `Song s = [roundtrip]` marker so Plan 30-09 Task 3's
// integration test can splice a `(writeMidi ...)` call into the same scope.
internal static class Midi2FlowCommand
{
    public static Command Build()
    {
        var inputArg = new Argument<FileInfo>("input") { Description = "Input .mid file" };
        var outputOpt = new Option<FileInfo?>("--output", "-o") { Description = "Output .flow file (omit to write to stdout)" };
        var noDynamicsOpt = new Option<bool>("--no-dynamics") { Description = "Omit dynamic markings (ppp..fff) from the generated source" };

        var cmd = new Command("midi2flow", "Convert a MIDI file to round-trippable Flow source");
        cmd.Add(inputArg);
        cmd.Add(outputOpt);
        cmd.Add(noDynamicsOpt);
        cmd.SetAction(parseResult =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var output = parseResult.GetValue(outputOpt);
            var noDynamics = parseResult.GetValue(noDynamicsOpt);

            if (!input.Exists)
            {
                Console.Error.WriteLine($"Error: input file not found: {input.FullName}");
                return 1;
            }

            try
            {
                var bytes = File.ReadAllBytes(input.FullName);
                var midi = MidiParser.Parse(bytes);
                var qr = Quantizer.Quantize(midi);
                var result = FlowGenerator.GenerateWithStats(midi, qr, input.Name, roundTrip: true, emitDynamics: !noDynamics);

                if (output != null)
                {
                    File.WriteAllText(output.FullName, result.Source);
                    Console.Error.WriteLine($"Converted {input.FullName} -> {output.FullName}");
                }
                else
                {
                    Console.Write(result.Source);
                }

                // sweep-0614: an honest exit code. When every track was dropped
                // (drums/empty/all-rest) the output is a comment-only file that
                // `check`s OK and renders SILENCE — previously reported as exit 0,
                // hiding total content loss. Still write the file (charitable) but
                // warn + return a distinct non-zero code so scripts/users notice.
                if (result.PlayableTrackCount == 0)
                {
                    string detail = result.DroppedDrumTrackCount > 0
                        ? $" ({result.DroppedDrumTrackCount} drum track(s) skipped — Flow uses different drum notation)"
                        : " (all tracks were drums/empty/rests)";
                    Console.Error.WriteLine(
                        $"Warning: no playable tracks found in {input.Name} — output is a comment-only file{detail}");
                    return 2;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });
        return cmd;
    }
}
