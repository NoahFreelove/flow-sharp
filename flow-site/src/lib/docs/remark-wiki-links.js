// remark-wiki-links.js — mdsvex remark plugin: rewrite wiki cross-links to /docs routes (D-49-26).
//
// The flow-sharp wiki cross-links pages in TWO ways:
//   1. GitHub-wiki `[[Page-Name]]` / `[[Page-Name|label]]` syntax — REUSES Task 1's unit-tested
//      `rewriteWikiLinks` (single source of truth) on text nodes. In practice the real wiki has
//      ZERO of these (RESEARCH Pitfall 7), but the path is wired + correct.
//   2. Ordinary relative markdown links `[Label](Page-Name.md)` / `[Label](Page-Name.md#anchor)` —
//      the ACTUAL style the wiki uses heavily. These are rewritten to `/docs/<kebab-slug>(#anchor)`
//      so the prerendered docs resolve. External (`http(s)://`, `//`) and absolute (`/...`) links
//      and non-`.md` targets are left untouched.
//
// Both run on the MDAST after parsing: `code`/`inlineCode` nodes are structurally separate, so the
// `[[1,10],...]` array literal in a `code` node is never touched. `rewriteWikiLinks`'s own
// fence-awareness is belt-and-braces for the string path.

import { visit } from 'unist-util-visit';
import { rewriteWikiLinks } from './transform.js';
import { toSlug } from './slug.js';

/**
 * True for links that must NOT be rewritten (external / protocol-relative / absolute / anchor).
 * @param {string} url
 * @returns {boolean}
 */
function isExternalOrAbsolute(url) {
	return (
		/^[a-z][a-z0-9+.-]*:/i.test(url) || // has a scheme (http:, https:, mailto:, …)
		url.startsWith('//') || // protocol-relative
		url.startsWith('/') || // already site-absolute
		url.startsWith('#') // in-page anchor
	);
}

/**
 * Rewrite a relative `Page-Name.md(#anchor)` URL to `/docs/<slug>(#anchor)`. Returns null when the
 * URL is not a relative `.md` link (caller leaves it untouched).
 * @param {string} url
 * @returns {string | null}
 */
function rewriteMdLink(url) {
	if (isExternalOrAbsolute(url)) return null;

	const hashIndex = url.indexOf('#');
	const path = hashIndex === -1 ? url : url.slice(0, hashIndex);
	const anchor = hashIndex === -1 ? '' : url.slice(hashIndex); // includes the leading '#'

	// Strip an optional leading `./`; only rewrite paths that end in `.md`.
	const cleaned = path.replace(/^\.\//, '');
	if (!/\.md$/i.test(cleaned)) return null;

	// Single-segment page name only (the wiki is flat); guard against any directory traversal.
	if (cleaned.includes('/')) return null;

	const slug = toSlug(cleaned);
	return `/docs/${slug}${anchor}`;
}

/**
 * mdsvex/remark transformer plugin.
 * @returns {(tree: object) => void}
 */
export function remarkWikiLinks() {
	return (/** @type {any} */ tree) => {
		// 1. `[[Page-Name]]` text-syntax (near-no-op on real content, but wired).
		visit(tree, 'text', (node) => {
			if (node.value && node.value.includes('[[')) {
				node.value = rewriteWikiLinks(node.value);
			}
		});

		// 2. Relative `[Label](Page-Name.md)` link nodes — the style the wiki actually uses.
		visit(tree, 'link', (node) => {
			if (typeof node.url === 'string') {
				const rewritten = rewriteMdLink(node.url);
				if (rewritten !== null) {
					node.url = rewritten;
				}
			}
		});
	};
}

export default remarkWikiLinks;
