// Curated showcase manifest (D-49-24 — 6–12 pieces, this set ships 10) consumed by the
// `/showcase` gallery + `/showcase/[slug]` detail pages.
//
// HONEST WORKTREE REALITY (49-CONTEXT §interfaces, STATE.md commits cd9f053/9990782):
//   - Pieces whose `.flow` source is PRESENT in the worktree embed the REAL source text
//     (read verbatim from examples/ + flow-lang/improv/styles/ at the SHA this manifest was
//     authored). They render a shiki source block on the detail page.
//   - Pieces whose source was DELETED from the worktree (the Phase 34 "In Five Voices" symphony
//     + "Stride & Stomp" ragtime) carry a `sourceRef` to the GitHub repo + a clear note — NO
//     fabricated `.flow` is shown. D-49-24 named them; we keep them in the gallery honestly.
//   - The v1.5 third-genre piece (Phase 41 SHOWCASE-01) is NOT yet built — OMITTED here rather
//     than invented (49-CONTEXT). It joins the gallery when Phase 41 ships.
//
// AUDIO (D-49-01 gesture-gated, NO autoplay):
//   - Two first-party rendered Flow audio assets exist under static/audio/ (shared with Home's
//     "How it sounds" embeds): `flow-showcase.wav` (a multi-voice render) + `microtonal-ji.wav`
//     (a just-intonation microtonal sketch). Pieces with a faithful audio match set `audioSrc`.
//   - Pieces WITHOUT a pre-rendered asset set NO `audioSrc` — the detail page shows a "hear it in
//     the playground" poster + an Open-in-playground button carrying the source, rather than
//     fabricating audio. Real audible playback for those is a 49-08 HUMAN-UAT item.
//
// OPEN-IN-PLAYGROUND (D-49-08 deep-link contract — matches Home CodeCard + docs "Open in
// playground"): only set `runnableOnWeb: true` for pieces whose embedded source actually runs on
// the Phase 48 Web target (no @sfz/@osc, no micBuffer, no live{}, no filesystem reads). The detail
// page builds `/playground#code=${encodeURIComponent(source)}` for those, exactly as CodeCard does.
//
// Source text is INLINED verbatim (read from the worktree at authoring time) rather than imported
// with `?raw` from `../../../../examples/`: the examples/ + flow-lang/ trees live OUTSIDE the
// flow-site/ project root, and a `?raw` import across the root would depend on Vite's
// `server.fs.allow` and on the CF Pages build context including those sibling directories. Inlining
// keeps the showcase build self-contained and matches the repo convention ($lib/home/examples.ts).
// If a source file changes, re-sync it here. Each string is the exact file content.

import {
	MARKOV_JAZZ_SOURCE,
	GRANULAR_SOURCE,
	TIDAL_SOURCE,
	STRETCH_SOURCE,
	PARAMETERIZED_SOURCE,
	SCALA_INTRO_SOURCE,
	JAZZ_STYLE_SOURCE,
	BLUES_STYLE_SOURCE,
	MARKOV_JAZZ_SOURCE_WEB,
	GRANULAR_SOURCE_WEB,
	TIDAL_SOURCE_WEB,
	STRETCH_SOURCE_WEB,
	PARAMETERIZED_SOURCE_WEB
} from './sources';
import { encode } from '$lib/share/encode';

/** Where a showcase piece is hosted when its source is absent from the worktree. */
const REPO_URL = 'https://github.com/noahfreelove/flow-sharp';

export interface ShowcasePiece {
	/** URL slug (`/showcase/<slug>`). Lowercase-kebab, unique. */
	slug: string;
	/** Display title. */
	title: string;
	/** One-line genre tag (gallery card sub-label). */
	genre: string;
	/** The Flow phase this piece showcases (caption / provenance). */
	phase: string;
	/**
	 * Embedded `.flow` source (verbatim from the worktree). Present for in-repo pieces; absent for
	 * pieces whose source was deleted (those carry `sourceRef` instead). Never fabricated.
	 */
	source?: string;
	/** The repo path the source lives at (provenance line under an embedded source block). */
	sourcePath?: string;
	/** External source link for pieces whose `.flow` is NOT in the worktree (no fabricated source). */
	sourceRef?: string;
	/** First-party pre-rendered audio asset under static/audio/ — only when a faithful match exists. */
	audioSrc?: string;
	/** True only when the embedded `source` runs on the Phase 48 Web target (Open-in-playground). */
	runnableOnWeb?: boolean;
	/**
	 * §6.2 — web-playable variant of `source` with `(play <buf>)` appended. Used for the
	 * Open-in-playground deep-link instead of `source` so the playground produces audible output
	 * rather than silently calling `(writeWav ...)` and producing no sound.
	 */
	webSource?: string;
	/** Composer notes — "why this piece, what Flow features show up". Faithful to the actual source. */
	notes: string;
}

export const PIECES: ShowcasePiece[] = [
	{
		slug: 'in-five-voices',
		title: 'In Five Voices',
		genre: 'Classical — five-voice counterpoint',
		phase: 'Phase 34',
		sourceRef: REPO_URL,
		audioSrc: '/audio/flow-showcase.wav',
		notes:
			'A five-voice contrapuntal study — the piece that proved Flow could carry independent ' +
			'melodic lines through voice blocks and a multi-section Song. The hero audio is a real ' +
			'multi-voice Flow render. The original `examples/symphony/` source was removed from this ' +
			'worktree during the v1.5 cleanup, so the source lives on GitHub rather than being ' +
			'reproduced here — what you hear is genuine Flow output, not a mock-up.'
	},
	{
		slug: 'stride-and-stomp',
		title: 'Stride & Stomp',
		genre: 'Ragtime — stride piano',
		phase: 'Phase 34',
		sourceRef: REPO_URL,
		notes:
			'A stride-piano ragtime romp — left-hand bass-and-chord alternation against a syncopated ' +
			'right-hand melody, written entirely in note streams with articulations. Like "In Five ' +
			'Voices", the `examples/ragtime/` source was removed from this worktree, so we link to the ' +
			'repo rather than fabricate the `.flow`. It is here to show Flow’s genre range, honestly ' +
			'attributed.'
	},
	{
		slug: 'markov-jazz',
		title: 'Markov Jazz',
		genre: 'Jazz — generative / chord-aware improvisation',
		phase: 'Phase 36',
		source: MARKOV_JAZZ_SOURCE,
		sourcePath: 'examples/generative/markov_jazz.flow',
		runnableOnWeb: true,
		webSource: MARKOV_JAZZ_SOURCE_WEB,
		notes:
			'The headline Phase 36 generative showcase. A short C-major scale teaches a Markov chain ' +
			'step-wise melodic motion; `(markov ...)` one-shots it, then the train/generate split ' +
			'`(markovTrain ...) -> MarkovModel` + `(markovGenerate ...)` reuses the model across bars. ' +
			'`(jam over=chords style=#jazz ...)` improvises chord-aware lines over a ii–V–I–vi ' +
			'progression, and `@patterns` combinators (`every`, `fast`, `sometimes`) vary the result. ' +
			'Every stochastic call routes through the PRNG registry, so two renders are byte-identical.'
	},
	{
		slug: 'granular-clouds',
		title: 'Granular Clouds',
		genre: 'Sound design — granular synthesis',
		phase: 'Phase 37',
		source: GRANULAR_SOURCE,
		sourcePath: 'examples/dsp/granular.flow',
		runnableOnWeb: true,
		webSource: GRANULAR_SOURCE_WEB,
		notes:
			'Flow’s `granular` builtin pulling grain-sized chunks from a source buffer at jittered ' +
			'offsets, windowing each grain, and overlap-adding at a density rate. This piece sweeps the ' +
			'composer-facing knobs — `grain=50ms`, `density=20Hz`, `jitter`, `windowing=#hann/#gaussian` ' +
			'— then composes the clouds with `reverb` and `pan`. The jitter PRNG routes through the ' +
			'registry, so the texture is reproducible to the byte.'
	},
	{
		slug: 'time-and-pitch',
		title: 'Time & Pitch',
		genre: 'Sound design — time-stretch + pitch-shift',
		phase: 'Phase 37',
		source: STRETCH_SOURCE,
		sourcePath: 'examples/dsp/stretch_pitchshift.flow',
		runnableOnWeb: true,
		webSource: STRETCH_SOURCE_WEB,
		notes:
			'`stretch` changes duration without touching pitch; `pitchShift` does the inverse. This ' +
			'piece runs a sustained tone through all three engines — `#vocoder` (phase-locked, for ' +
			'harmonic material), `#psola` (transient-preserving), and `#auto` (per-frame HPS ' +
			'classification, with a stderr advisory reporting the picked ratio) — and demonstrates the ' +
			'identity fast-paths (`factor=1.0`, `0c`) that return the input byte-for-byte. All ' +
			'hand-rolled DSP; no GPL libraries.'
	},
	{
		slug: 'tidal-combinators',
		title: 'Tidal Combinators',
		genre: 'Generative — pattern algebra',
		phase: 'Phase 36',
		source: TIDAL_SOURCE,
		sourcePath: 'examples/generative/tidal_combinators.flow',
		runnableOnWeb: true,
		webSource: TIDAL_SOURCE_WEB,
		notes:
			'All thirteen Tidal-style `@patterns` combinators chained on one 4-bar sequence: `rev`, ' +
			'`palindrome`, `every`, `fast`/`slow`, `chunk`, `phase`, `iter`, `jux`, `superimpose`, then ' +
			'the stochastic `sometimes`/`sparseSeq`. The cycle unit is bars, transform-arg combinators ' +
			'take lambdas, and the whole chain stays deterministic because the stochastic combinators ' +
			'route their randomness through the PRNG registry.'
	},
	{
		slug: 'parameterized-sections',
		title: 'Parameterized Sections',
		genre: 'Song structure — section overloading',
		phase: 'Phase 36',
		source: PARAMETERIZED_SOURCE,
		sourcePath: 'examples/sections/parameterized.flow',
		runnableOnWeb: true,
		webSource: PARAMETERIZED_SOURCE_WEB,
		notes:
			'Sections that take arguments. One `verse` name carries three overloads — a `Note` binding, ' +
			'a tuple destructure `<<Note, Int>>`, and a chord-literal extractor `Cmaj7` — and the ' +
			'overload resolver picks the highest-specificity match at each call site. Defaults ' +
			'(`intro(Note root = C4, Int repeats = 2)`), the `*N` repeat operator (`verse(C4)*3`), and ' +
			'legacy zero-arg sections all compose in one `Song` list. Pattern syntax from the matching ' +
			'engine, applied to song form.'
	},
	{
		slug: 'carlos-alpha-microtonal',
		title: 'Carlos Alpha & Friends',
		genre: 'Microtonal — Scala tunings',
		phase: 'Phase 32',
		source: SCALA_INTRO_SOURCE,
		sourcePath: 'examples/scala/intro.flow',
		// NOT runnable on Web: (loadScala "...") reads .scl files off disk; the filesystem isn't
		// available in the browser sandbox. Source is shown for reading; no Open-in-playground.
		runnableOnWeb: false,
		audioSrc: '/audio/microtonal-ji.wav',
		notes:
			'Microtonality as a first-class Flow value. `(loadScala "x.scl")` parses any of ~5300 ' +
			'community Scala tunings into a `Tuning`, applied via the `tuning t { ... }` musical-context ' +
			'block. This piece renders four sections under four tuning systems — Partch’s 43-tone just ' +
			'intonation, Wendy Carlos’ non-octave-repeating Alpha, a 5-limit JI arpeggio, and a return ' +
			'to Partch — so each is audibly distinct. The hero audio is a just-intonation sketch. ' +
			'(The source reads `.scl` files off disk, so it runs on the desktop CLI, not in the browser ' +
			'playground.)'
	},
	{
		slug: 'jazz-style-pack',
		title: 'The #jazz Style Pack',
		genre: 'Improv — editable rule pack',
		phase: 'Phase 36',
		source: JAZZ_STYLE_SOURCE,
		sourcePath: 'flow-lang/improv/styles/jazz.flow',
		// The pack is musical CONTENT loaded at engine init — it registers a style, it doesn't render
		// audio on its own. Shown as readable source; Open-in-playground would print nothing audible.
		runnableOnWeb: false,
		notes:
			'Improv styles are musical content, not engine internals — they ship as editable Flow files. ' +
			'This is the baseline `#jazz` rule pack that `(jam style=#jazz ...)` consults: chord-tone-' +
			'heavy weights on strong beats, scale tones favored on weak beats, light chromatic passing, ' +
			'an eighth-note swing template, and offbeat accents. A composer can copy it to ' +
			'`~/.config/flow/styles/` and retune the feel without touching the language. Read it to see ' +
			'exactly how Flow’s improviser thinks.'
	},
	{
		slug: 'blues-style-pack',
		title: 'The #blues Style Pack',
		genre: 'Improv — editable rule pack',
		phase: 'Phase 36',
		source: BLUES_STYLE_SOURCE,
		sourcePath: 'flow-lang/improv/styles/blues.flow',
		runnableOnWeb: false,
		notes:
			'The companion `#blues` rule pack. Compared to `#jazz` it loosens the chord-tone grip on ' +
			'strong beats and pushes far more chromatic passing on weak beats — the bent-note feel — ' +
			'with tenuto/marcato/accent articulations driving a grittier groove. Side-by-side with the ' +
			'`#jazz` pack it shows how a small Dict of weights swings the improviser between genres, ' +
			'all in composer-editable Flow.'
	}
];

/** Pieces with embedded, Web-runnable source — the ones the playground can actually run. */
export function isRunnable(piece: ShowcasePiece): boolean {
	return Boolean(piece.runnableOnWeb && piece.source);
}

/**
 * Build the playground deep-link for a runnable piece (same contract as Home's CodeCard).
 * §6.2: prefers `webSource` (the browser-audible variant with `(play ...)` appended) over
 * the verbatim `source` (which calls `writeWav` and produces no sound in the browser).
 */
export function playgroundHref(piece: ShowcasePiece): string | null {
	if (!isRunnable(piece) || !piece.source) return null;
	// Plan 49-06: the REAL #code= fragment (fflate-deflate + base64url via encode), what the
	// playground's decode() consumes. `&run=1` carries the auto-run signal (D-49-08).
	const src = piece.webSource ?? piece.source;
	return `/playground#code=${encode(src)}&run=1`;
}

/** Lookup a piece by slug (detail page + entries()). */
export function pieceBySlug(slug: string): ShowcasePiece | undefined {
	return PIECES.find((p) => p.slug === slug);
}
