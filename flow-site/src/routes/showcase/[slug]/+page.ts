import { error } from '@sveltejs/kit';
import { PIECES, pieceBySlug, playgroundHref, type ShowcasePiece } from '$lib/showcase/pieces';
import { highlightFlow } from '$lib/docs/shiki';
import type { EntryGenerator, PageLoad } from './$types';

// Each showcase piece detail is prerendered (D-49-13): the curated set is static at build time, so
// shiki highlighting of the embedded source runs HERE (server/build time) and ships as escaped
// HTML — zero client highlight JS, matching the Home + Docs surfaces (D-49-15).
export const prerender = true;

// entries() tells SvelteKit which [slug] values to prerender — every manifest piece.
export const entries: EntryGenerator = () => PIECES.map((p) => ({ slug: p.slug }));

/** A piece plus its server-rendered shiki source HTML (only for pieces with embedded source). */
export interface LoadedPiece extends ShowcasePiece {
	/** Server-rendered shiki markup for `source` (present only when the piece embeds source). */
	sourceHtml: string | null;
	/** Pre-built playground deep-link (present only for Web-runnable pieces). */
	playgroundHref: string | null;
}

export const load: PageLoad = async ({ params }) => {
	const piece = pieceBySlug(params.slug);
	if (!piece) {
		throw error(404, `No showcase piece "${params.slug}"`);
	}

	// Highlight the embedded source at prerender time (escaped, dual-theme). Absent-source pieces
	// (symphony / ragtime) get null — the detail page shows a "view source on GitHub" link instead.
	const sourceHtml = piece.source ? await highlightFlow(piece.source, 'flow') : null;

	const loaded: LoadedPiece = {
		...piece,
		sourceHtml,
		playgroundHref: playgroundHref(piece)
	};
	return { piece: loaded };
};
