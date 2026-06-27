/**
 * Server-rendered syntax highlighting for static Flow code blocks (D-49-15).
 *
 * shiki 4.x is filesystem-agnostic: we import the Phase 17 TextMate grammar JSON object (copied
 * into flow-site by `scripts/sync-grammar.mjs` from `vscode-extension/syntaxes/flow.tmLanguage.json`
 * — RESEARCH Pattern 4, corrected path) and pass it to `createHighlighter`. Highlighting runs at
 * BUILD/SSR time and emits escaped HTML with light + dark theme variants — ZERO client JS ships
 * for highlighting.
 *
 * Plain JS so the Node-loaded `svelte.config.js` mdsvex `highlight` hook can import `highlightFlow`
 * directly (Node ESM cannot import `.ts`). `src/lib/docs/shiki.ts` re-exports it as the typed
 * contract that Plan 49-03's static code cards consume.
 */

import { createHighlighter } from 'shiki';
// Import attribute required by Node ESM (svelte.config.js loads this directly); Vite honors it too.
import flowGrammar from './flow.tmLanguage.json' with { type: 'json' };

/** Light + dark themes — the docs render both and CSS swaps per `[data-theme]`. */
const LIGHT_THEME = 'github-light';
const DARK_THEME = 'github-dark';

/** Built-in languages the docs fences use besides `flow`. */
const BUILTIN_LANGS = ['bash'];

/** @type {Promise<import('shiki').Highlighter> | null} */
let highlighterPromise = null;

/**
 * Shiki derives the language id from the grammar `name` and ALSO registers every entry in
 * `aliases`. If `aliases` contains the name itself (`name: 'flow'` + `aliases: ['flow']`) shiki
 * registers `flow -> flow`, throws `Circular alias 'flow -> flow'`, and EVERY code block silently
 * falls back to plain unhighlighted text. Strip any self-referential alias defensively so a stale
 * synced grammar can never break highlighting again.
 */
const safeFlowGrammar = {
	...flowGrammar,
	aliases: (flowGrammar.aliases ?? []).filter((a) => a !== flowGrammar.name)
};

/** Lazily create (and memoize) the shiki highlighter with the Flow grammar loaded. */
function getHighlighter() {
	if (!highlighterPromise) {
		highlighterPromise = createHighlighter({
			themes: [LIGHT_THEME, DARK_THEME],
			// `safeFlowGrammar.name === 'flow'`, scopeName 'source.flow' — registers lang id `flow`.
			langs: [safeFlowGrammar, ...BUILTIN_LANGS]
		});
	}
	return highlighterPromise;
}

/** Languages shiki knows after the grammar + built-ins are loaded. */
const KNOWN_LANGS = new Set(['flow', ...BUILTIN_LANGS]);

/**
 * @param {string} s
 * @returns {string}
 */
function escapeHtml(s) {
	return s
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;')
		.replace(/"/g, '&quot;');
}

/**
 * Highlight a fenced code block to server-rendered, dual-theme HTML.
 * @param {string} code  the raw source of the fenced block
 * @param {string} [lang] the fence info-string (`flow`, `bash`); unknown langs fall back to `flow`
 * @returns {Promise<string>} a `<pre class="shiki ...">…</pre>` string with light+dark variables
 */
export async function highlightFlow(code, lang = 'flow') {
	const resolvedLang = KNOWN_LANGS.has(lang) ? lang : 'flow';
	try {
		const hl = await getHighlighter();
		const html = hl.codeToHtml(code, {
			lang: resolvedLang,
			themes: { light: LIGHT_THEME, dark: DARK_THEME },
			defaultColor: 'light'
		});
		return makeScrollRegionFocusable(html);
	} catch {
		// Charitable fallback (never break the docs build on a highlight hiccup).
		return makeScrollRegionFocusable(
			`<pre class="shiki shiki-fallback"><code>${escapeHtml(code)}</code></pre>`
		);
	}
}

/**
 * Make the shiki `<pre>` a keyboard-accessible horizontal scroll region (axe
 * scrollable-region-focusable, D-49-10): a code block that scrolls on overflow-x must be
 * reachable + scrollable by keyboard. Injects `tabindex="0"` + `role="region"` + a generic
 * label onto the opening `<pre …>` tag once. Idempotent (skips if a tabindex is already present).
 * @param {string} html shiki `<pre …>…</pre>` output
 * @returns {string}
 */
function makeScrollRegionFocusable(html) {
	if (/<pre[^>]*\btabindex=/.test(html)) return html;
	return html.replace(
		/<pre(\s|>)/,
		'<pre tabindex="0" role="region" aria-label="Code block (scrollable)"$1'
	);
}
