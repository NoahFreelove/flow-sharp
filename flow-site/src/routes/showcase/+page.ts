import { PIECES } from '$lib/showcase/pieces';
import type { PageLoad } from './$types';

// The showcase gallery is prerendered at build time (D-49-13): the curated piece set is known
// statically, so the grid ships as server-rendered HTML with no client data fetch. Wiring this
// route also resolves the /showcase prerender warning that Plan 49-03's nav tab tolerated.
export const prerender = true;

export const load: PageLoad = () => {
	// Pass the manifest through; the gallery renders one card per piece.
	return { pieces: PIECES };
};
