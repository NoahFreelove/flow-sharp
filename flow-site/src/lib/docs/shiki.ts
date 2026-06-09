/**
 * Typed contract for the server-rendered Flow highlighter (D-49-15).
 *
 * shiki 4.x is filesystem-agnostic: the Phase 17 TextMate grammar JSON object — copied into
 * flow-site by `scripts/sync-grammar.mjs` from `vscode-extension/syntaxes/flow.tmLanguage.json`
 * (RESEARCH Pattern 4, corrected path) — is passed to shiki's `createHighlighter({ langs: [...] })`.
 *
 * The RUNTIME implementation lives in `highlight.js` (plain JS) so the Node-loaded
 * `svelte.config.js` mdsvex `highlight` hook can import `highlightFlow` without a `.ts` loader.
 * This module is the TYPED entry point app code imports — notably Plan 49-03's Home/Showcase
 * static code cards:
 *
 *   import { highlightFlow } from '$lib/docs/shiki';
 *   const html = await highlightFlow(code, 'flow'); // server-rendered, dual-theme, escaped
 *
 * The export NAME (`highlightFlow`) and SIGNATURE `(code: string, lang?: string) => Promise<string>`
 * are a frozen contract for Plan 49-03 — do not rename.
 */

import { createHighlighter, type Highlighter } from 'shiki';
import flowGrammar from './flow.tmLanguage.json';
import { highlightFlow as highlightFlowImpl } from './highlight.js';

/** The Flow grammar's TextMate scope (`source.flow`) — exposed for tooling / Plan 49-05 Monaco. */
export const FLOW_SCOPE_NAME: string = (flowGrammar as { scopeName: string }).scopeName;

/** shiki language id the grammar registers under (its `name`, normalized to `flow` at sync). */
export const FLOW_LANG_ID = 'flow';

/**
 * Create a standalone shiki highlighter with the Flow grammar loaded (light + dark themes). The
 * docs pipeline uses the memoized `highlightFlow` instead; this typed factory is exported for any
 * caller that needs a raw `Highlighter` (e.g. a future tool) and to pin the `createHighlighter`
 * + `flow.tmLanguage.json` integration shape in one typed place.
 */
export function createFlowHighlighter(): Promise<Highlighter> {
	return createHighlighter({
		themes: ['github-light', 'github-dark'],
		langs: [flowGrammar as never, 'bash']
	});
}

/**
 * Highlight a fenced code block to server-rendered, dual-theme HTML using the Phase 17 Flow
 * TextMate grammar. Unknown languages fall back to the `flow` grammar; highlight failures fall
 * back to a plain escaped `<pre>` (the docs never fail to render on an unexpected fence).
 *
 * Delegates to the memoized `highlight.js` implementation so the whole app shares ONE highlighter.
 */
export function highlightFlow(code: string, lang = 'flow'): Promise<string> {
	return highlightFlowImpl(code, lang);
}
