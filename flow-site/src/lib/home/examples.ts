// Curated Home snippets (D-49-21 §Hero + §Code-first) consumed by Plan 49-03's CodeCard.
//
// Every snippet is authored to run on the Phase 48 WEB target — so NO sampler/OSC modules
// (`@sfz`/`@osc` stripped on Web per Phase 47), no `(micBuffer ...)` (InputFunctions stripped),
// no `live { }` blocks (parse-time error on Web). These mirror the proven playground Quick-Start
// set (`$lib/playground/snippets`) so the hero "Play in playground" cards deep-link to code that
// is known-audible on Run.

export interface HomeExample {
	/** Stable id (list key). */
	id: string;
	/** Short card title. */
	title: string;
	/** One-line description of what you'll hear / see. */
	blurb: string;
	/** The Flow source — runs on the Web target. */
	source: string;
}

/**
 * The 3 hero "Play in playground" cards (D-49-21 §Hero). Each is a small, immediately-audible
 * taste of a different Flow idiom: a raw tone, an inline note-stream melody, and a chord
 * progression in a key context.
 */
export const HERO_EXAMPLES: HomeExample[] = [
	{
		id: 'sine-440',
		title: 'A pure tone',
		blurb: 'The hello-world — a one-second A4 sine. Press Play to hear it.',
		source: 'use "@audio"\n(play (createSineTone 440Hz 1.0 0.5))\n'
	},
	{
		id: 'note-stream',
		title: 'A note-stream melody',
		blurb: 'A C-major run, written straight as an inline note stream at 120 BPM.',
		source:
			'use "@std"\nuse "@audio"\nuse "@composition"\n\n' +
			'tempo 120 {\n' +
			'  (play | C4q D4q E4q F4q G4q A4q B4q C5h |)\n' +
			'}\n'
	},
	{
		id: 'chord-progression',
		title: 'A chord progression',
		blurb: 'A ii–V–I in C, played from chord brackets inside a key context.',
		source:
			'use "@std"\nuse "@audio"\nuse "@composition"\n\n' +
			'key Cmajor {\n' +
			'  (play | [D4 F4 A4]h [G4 B4 D5]h [C4 E4 G4]w |)\n' +
			'}\n'
	}
];

/**
 * The single ~20-line code-first snippet (D-49-21 §Code-first). Shows off the three signature
 * Flow ideas the margin annotations call out: the `->` flow operator, inline note streams, and
 * scoped musical-context blocks. Runs on the Web target.
 */
export const CODE_FIRST_EXAMPLE: HomeExample = {
	id: 'code-first',
	title: 'Music as code',
	blurb: 'The flow operator, note streams, and a musical-context block — all in one piece.',
	source:
		'use "@std"\n' +
		'use "@audio"\n' +
		'use "@composition"\n' +
		'\n' +
		'; a melody is just a note stream — pitches, octaves, durations\n' +
		'tempo 110 {\n' +
		'  key Cmajor {\n' +
		'    section verse {\n' +
		'      | E4q E4q F4q G4q G4q F4q E4q D4q C4h |\n' +
		'    }\n' +
		'\n' +
		'    ; build a Song, then chain it through transforms with ->\n' +
		'    Song piece = [verse]\n' +
		'    piece -> (transpose +2st) -> play\n' +
		'  }\n' +
		'}\n'
};

/**
 * Margin annotations for the code-first snippet (D-49-21 §Code-first — "annotations explaining
 * `->`, note streams, musical context"). Each points at a 1-based line of CODE_FIRST_EXAMPLE.
 */
export interface CodeAnnotation {
	/** 1-based line number in CODE_FIRST_EXAMPLE.source the note points at. */
	line: number;
	/** The short margin label. */
	label: string;
	/** The explanation. */
	text: string;
}

export const CODE_FIRST_ANNOTATIONS: CodeAnnotation[] = [
	{
		line: 6,
		label: 'Musical context',
		text: 'tempo / key blocks scope timing + harmony — nest them like rack modules.'
	},
	{
		line: 9,
		label: 'Note streams',
		text: 'Write pitches and durations inline between | bars |. No boilerplate.'
	},
	{
		line: 14,
		label: 'The flow operator',
		text: 'x -> f(arg) feeds x into f as its first argument — chain transforms left to right.'
	}
];
