/**
 * Wiki link rewriting (D-49-26).
 *
 * GitHub-wiki cross-links use `[[Page-Name]]` (and `[[Page-Name|display text]]`) syntax.
 * `rewriteWikiLinks` converts those to ordinary markdown links so mdsvex can render them:
 *
 *   `[[Quick-Start]]`            -> `[Quick Start](/docs/quick-start)`
 *   `[[Quick-Start|see here]]`   -> `[see here](/docs/quick-start)`
 *
 * IMPORTANT (RESEARCH Pitfall 7): the real flow-sharp wiki has ZERO inter-page links — the only
 * `[[` is array data (`[[1,10], [2,20], [3,30]]`) inside a fenced code block in `Collections.md`.
 * The transform MUST leave anything inside a fenced code block (``` ... ``` or ~~~ ... ~~~) and
 * inside inline code spans (`` `...` ``) untouched, or it would corrupt that array literal. It is
 * unit-tested against a SYNTHETIC fixture, not real wiki content (see `transform.test.ts`).
 *
 * Idempotent: its output never contains a `[[`, so running it twice == running it once.
 *
 * Plain JS so the Node-loaded `svelte.config.js` remark chain can import it directly.
 */

import { toSlug, titleFromPageName } from './slug.js';

/** Matches `[[Page-Name]]` or `[[Page-Name|Display Text]]`. */
const WIKI_LINK = /\[\[([^\]|[]+?)(?:\|([^\]]+?))?\]\]/g;

/**
 * Rewrite a single text segment KNOWN to not be code.
 * @param {string} text
 * @returns {string}
 */
function rewriteSegment(text) {
	return text.replace(WIKI_LINK, (_match, rawPage, rawLabel) => {
		const pageName = String(rawPage).trim();
		const slug = toSlug(pageName);
		const label = rawLabel !== undefined ? String(rawLabel).trim() : titleFromPageName(pageName);
		return `[${label}](/docs/${slug})`;
	});
}

/**
 * Split markdown into alternating non-code / code regions, rewrite `[[...]]` only in the non-code
 * regions, and reassemble. Fenced blocks (``` / ~~~) and inline spans (`` ` ``) are preserved
 * verbatim. Idempotent: rewritten output contains no `[[`, so a second pass is a no-op.
 * @param {string} markdown
 * @returns {string}
 */
export function rewriteWikiLinks(markdown) {
	// Protected regions, in priority order: fenced code blocks first (they can contain backticks),
	// then inline code spans.
	const PROTECTED = /(^[ \t]*(`{3,}|~{3,})[^\n]*\n[\s\S]*?^[ \t]*\2[ \t]*$)|(`+)([\s\S]*?)\3/gm;

	let result = '';
	let lastIndex = 0;
	/** @type {RegExpExecArray | null} */
	let match;

	while ((match = PROTECTED.exec(markdown)) !== null) {
		result += rewriteSegment(markdown.slice(lastIndex, match.index));
		result += match[0];
		lastIndex = PROTECTED.lastIndex;
	}
	result += rewriteSegment(markdown.slice(lastIndex));

	return result;
}
