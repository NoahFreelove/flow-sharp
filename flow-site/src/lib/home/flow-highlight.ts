// Tiny Flow syntax highlighter for the iOS-6 home-page code "wells".
//
// Ported from the Claude Design handoff `flow.js` (claude.ai/design). Pure — no DOM — so it runs
// at prerender time and the hero snippets ship as escaped, server-rendered HTML (no client JS).
// This is deliberately a lightweight, marketing-grade highlighter, NOT the real shiki grammar the
// /docs + /playground routes use; it only has to make the three landing snippets look right.

const KEYWORDS = [
	'use', 'play', 'stream', 'loop', 'preview', 'tempo', 'timesig', 'key', 'swing',
	'voicePool', 'tuning', 'pan', 'gain', 'dynamics', 'rit', 'accel', 'for', 'while',
	'break', 'continue', 'match', 'fn', 'let', 'return', 'section', 'song', 'arrange',
	'jam', 'progression', 'transpose', 'invert', 'retrograde', 'every', 'fast', 'slow', 'jux'
];

function esc(s: string): string {
	return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/** Highlight Flow source into a string of `<span class="tk-*">` tokens (already HTML-escaped). */
export function highlightFlow(src: string): string {
	let out = '';
	let i = 0;
	const n = src.length;
	const kwRe = new RegExp('^(?:' + KEYWORDS.join('|') + ')\\b');

	while (i < n) {
		const rest = src.slice(i);
		const ch = src[i];

		// line comment
		if (ch === '/' && src[i + 1] === '/') {
			let nl = src.indexOf('\n', i);
			if (nl < 0) nl = n;
			out += '<span class="tk-com">' + esc(src.slice(i, nl)) + '</span>';
			i = nl;
			continue;
		}
		// string literal
		if (ch === '"') {
			let j = i + 1;
			while (j < n && src[j] !== '"') {
				if (src[j] === '\\') j++;
				j++;
			}
			j = Math.min(j + 1, n);
			out += '<span class="tk-str">' + esc(src.slice(i, j)) + '</span>';
			i = j;
			continue;
		}
		// note token, e.g. C4q, F#5h, A4]h, [D4
		const note = rest.match(/^[A-G][#b]?\d[a-z]?/);
		if (note) {
			out += '<span class="tk-num">' + esc(note[0]) + '</span>';
			i += note[0].length;
			continue;
		}
		// keyword
		const kw = rest.match(kwRe);
		if (kw) {
			out += '<span class="tk-kw">' + esc(kw[0]) + '</span>';
			i += kw[0].length;
			continue;
		}
		// @module
		const mod = rest.match(/^@\w+/);
		if (mod) {
			out += '<span class="tk-fn">' + esc(mod[0]) + '</span>';
			i += mod[0].length;
			continue;
		}
		// number / Hz literal
		const num = rest.match(/^\d+(\.\d+)?(Hz)?/);
		if (num && /\d/.test(num[0])) {
			out += '<span class="tk-num">' + esc(num[0]) + '</span>';
			i += num[0].length;
			continue;
		}
		// operators / brackets
		if ('|→↝⇒{}()[]<>='.indexOf(ch) >= 0) {
			out += '<span class="tk-op">' + esc(ch) + '</span>';
			i++;
			continue;
		}
		// identifier / function call
		const id = rest.match(/^[A-Za-z_]\w*/);
		if (id) {
			const after = src[i + id[0].length];
			const isFn = after === '(' || /^create/.test(id[0]);
			out += isFn ? '<span class="tk-fn">' + esc(id[0]) + '</span>' : esc(id[0]);
			i += id[0].length;
			continue;
		}
		out += esc(ch);
		i++;
	}
	return out;
}
