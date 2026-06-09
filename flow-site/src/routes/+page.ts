import { highlightFlow } from '$lib/docs/shiki';
import { HERO_EXAMPLES, CODE_FIRST_EXAMPLE, type HomeExample } from '$lib/home/examples';
import type { PageLoad } from './$types';

// Home is prerendered at build time (D-49-13) — static HTML, no WASM, fast LCP for SEO. shiki
// highlighting runs HERE at prerender time so the hero code cards + the code-first snippet ship
// as server-rendered escaped HTML (D-49-15 — zero client highlight JS on the marketing route).
export const prerender = true;

/** A snippet plus its server-rendered shiki markup. */
export interface HighlightedExample extends HomeExample {
	html: string;
}

async function highlightExample(ex: HomeExample): Promise<HighlightedExample> {
	return { ...ex, html: await highlightFlow(ex.source, 'flow') };
}

export const load: PageLoad = async () => {
	// Highlight the 3 hero cards + the code-first snippet in parallel at prerender time.
	const [heroExamples, codeFirst] = await Promise.all([
		Promise.all(HERO_EXAMPLES.map(highlightExample)),
		highlightExample(CODE_FIRST_EXAMPLE)
	]);

	return { heroExamples, codeFirst };
};
