import { describe, it, expect } from 'vitest';
import { encode as encodeTs, decode } from './encode';
import { encode as encodeNode } from './encode-node.js';

// The docs mdsvex carrier (svelte.config.js, Node) encodes `#code=` deep-links with encode-node.js;
// Home/showcase/playground use encode.ts. They MUST agree byte-for-byte so EVERY carrier's link
// round-trips through the single browser decode(). This pins that parity.

const SOURCES = [
	'',
	'(print "hello flow")',
	'use "@audio"\n(play | C4q D4q E4q F4q |)',
	'tempo 120 {\n  key Cmajor {\n    (play | Cmaj7 Dm7 G7 Cmaj7 |)\n  }\n}',
	'// café — naïve façade ☕ 音楽\n(print "Ünïcödé ✓")'
];

describe('encode-node parity with encode.ts', () => {
	for (const src of SOURCES) {
		it(`encode-node === encode.ts for ${JSON.stringify(src.slice(0, 24))}`, () => {
			expect(encodeNode(src)).toBe(encodeTs(src));
		});
		it(`decode(encode-node(src)) === src for ${JSON.stringify(src.slice(0, 24))}`, () => {
			expect(decode(encodeNode(src))).toBe(src);
		});
	}
});
