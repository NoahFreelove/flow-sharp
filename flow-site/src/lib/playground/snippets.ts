// Quick-Start snippet set for the playground (UI-SPEC §Playground left rail / D-49-23).
//
// Every snippet is authored to run on the Phase 48 WEB target — so NO sampler/OSC modules
// (stripped on Web per Phase 47), no `(micBuffer ...)` (InputFunctions stripped), no `live { }`
// blocks (parse-time error on Web). The default starter matches the dev-smoke harness + HANDOFF §10:
// `use "@audio"` then `(play (createSineTone 440Hz 1.0 0.5))` → audible 440 Hz tone on Run.

export interface Snippet {
	/** Stable id (used as the list key + the New-blank sentinel). */
	id: string;
	/** Human-facing label in the snippet list. */
	label: string;
	/** One-line description shown under the label. */
	blurb: string;
	/** The Flow source loaded into the editor. */
	source: string;
}

/** The default snippet loaded on first mount (HANDOFF §10 — audible 440 Hz tone on Run). */
export const DEFAULT_SNIPPET_ID = 'sine-440';

/** An empty blank-snippet sentinel for "New blank" (UI-SPEC destructive confirm). */
export const BLANK_SOURCE = '';

export const SNIPPETS: Snippet[] = [
	{
		id: DEFAULT_SNIPPET_ID,
		label: 'Sine tone (440 Hz)',
		blurb: 'The hello-world — a one-second A4 sine. Press Run to hear it.',
		source: 'use "@audio"\n(play (createSineTone 440Hz 1.0 0.5))\n'
	},
	{
		id: 'print-hello',
		label: 'Print to console',
		blurb: 'No audio — just stdout. Shows the console split.',
		source: 'use "@std"\n(print "hello flow")\n(print (str (add 1 2)))\n'
	},
	{
		id: 'note-stream',
		label: 'Note stream melody',
		blurb: 'A C-major run played straight from an inline note stream.',
		source:
			'use "@std"\nuse "@audio"\nuse "@composition"\n\n' +
			'tempo 120 {\n' +
			'  (play | C4q D4q E4q F4q G4q A4q B4q C5h |)\n' +
			'}\n'
	},
	{
		id: 'chord-progression',
		label: 'Chord progression',
		blurb: 'A ii–V–I in C with chord brackets, played as audio.',
		source:
			'use "@std"\nuse "@audio"\nuse "@composition"\n\n' +
			'key Cmajor {\n' +
			'  (play | [D4 F4 A4]h [G4 B4 D5]h [C4 E4 G4]w |)\n' +
			'}\n'
	},
	{
		id: 'song-section',
		label: 'Song from a section',
		blurb: 'Build a Song from a named section and play it.',
		source:
			'use "@std"\nuse "@audio"\nuse "@composition"\n\n' +
			'tempo 100 {\n' +
			'  section verse {\n' +
			'    | E4q E4q F4q G4q G4q F4q E4q D4q C4h |\n' +
			'  }\n' +
			'  Song piece = [verse]\n' +
			'  (writeMidi "verse.mid" piece)\n' +
			'  (print "rendered verse to MIDI")\n' +
			'}\n'
	},
	{
		id: 'print-arith',
		label: 'Print arithmetic',
		blurb: 'No audio — prefix arithmetic + str, straight to stdout.',
		source: 'use "@std"\n(print (str (mul 6 7)))\n(print (str (add 1 (mul 2 3))))\n'
	}
];

/** Look up a snippet by id (falls back to the default). */
export function snippetById(id: string): Snippet {
	return SNIPPETS.find((s) => s.id === id) ?? SNIPPETS[0];
}
