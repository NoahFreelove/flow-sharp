import { describe, it, expect, afterEach } from 'vitest';
import { deflateSync, strToU8 } from 'fflate';
import {
	encode,
	decode,
	ShareDecodeError,
	MAX_DECODED_BYTES,
	MAX_COMPRESSED_BYTES,
	MAX_DEFLATE_RATIO,
	__setDecodeProbe,
	type DecodeAllocationProbe
} from './encode';

// REQ-SITE-SHARE-01 — URL-fragment encode <-> decode round-trips (T-49-CSP-FRAG).
//
// The default, zero-backend share path (D-49-30): `encode(src)` produces a base64url string that
// rides in `/playground#code=...`; `decode(frag)` inflates it back. Three security-critical
// properties are proven here (the threat model's T-49-CSP-FRAG mitigation):
//   1. round-trip equality for arbitrary Flow source,
//   2. base64url safety (no `+`, `/`, `=` — fragment-safe, no URL-encoding ambiguity),
//   3. DEFENSIVE decode: malformed input throws a TYPED ShareDecodeError (the playground maps it
//      to the friendly "Couldn't decode this shared snippet" copy — never a raw exception),
//   4. decompression-bomb guard: a payload whose inflated size would exceed the cap is rejected
//      BEFORE the full output is materialised (no unbounded allocation).

// Representative Flow source — note streams, music literals, prefix arithmetic, comments, unicode.
const SOURCES: Array<[string, string]> = [
	['empty', ''],
	['one-liner', '(print "hello flow")'],
	['note stream', 'use "@audio"\n(play | C4q D4q E4q F4q |)'],
	[
		'music context + chords',
		'tempo 120 {\n  key Cmajor {\n    (play | Cmaj7 Dm7 G7 Cmaj7 |)\n  }\n}'
	],
	['music literals', '(createSineTone 440Hz 1.0 0.5)\n(transpose seq +2st)\n(gain buf -12dB)'],
	['unicode + comment', '// café — naïve façade ☕ 音楽\n(print "Ünïcödé ✓")'],
	['long', '(print "line")\n'.repeat(500)]
];

describe('share encode/decode (fflate deflate + base64url)', () => {
	afterEach(() => {
		__setDecodeProbe(null);
	});

	for (const [name, src] of SOURCES) {
		it(`round-trips: decode(encode(src)) === src — ${name}`, () => {
			expect(decode(encode(src))).toBe(src);
		});
	}

	it('produces a base64url-safe fragment (no +, /, or =)', () => {
		for (const [, src] of SOURCES) {
			const frag = encode(src);
			expect(frag).not.toMatch(/[+/=]/);
		}
	});

	it('tolerates the `#code=` prefix on decode (carrier-friendly)', () => {
		const src = '(play | C4 E4 G4 |)';
		expect(decode('#code=' + encode(src))).toBe(src);
		expect(decode('code=' + encode(src))).toBe(src);
	});

	it('throws a TYPED ShareDecodeError on malformed input (not a raw exception)', () => {
		// Garbage base64url that does not inflate.
		expect(() => decode('not-valid-deflate-data')).toThrow(ShareDecodeError);
		expect(() => decode('@@@not-even-base64@@@')).toThrow(ShareDecodeError);
		expect(() => decode('')).toThrow(ShareDecodeError);
	});

	it('rejects a large decompression-bomb fragment via the COMPRESSED-input cap (peak-allocation guard)', () => {
		// CR-01: the headline bomb. 16 MB of zeros deflates to ~16 KB — which is ABOVE the
		// MAX_COMPRESSED_BYTES input cap, so decode rejects it BEFORE inflating at all. This is the
		// guard that actually bounds peak allocation: the 16 MB is never materialised.
		const bomb = new Uint8Array(MAX_DECODED_BYTES + 16 * 1024 * 1024); // all zeros
		const deflated = deflateSync(bomb);
		const frag = toBase64Url(deflated);
		expect(deflated.length).toBeGreaterThan(MAX_COMPRESSED_BYTES); // exceeds the input cap

		const probe: DecodeAllocationProbe = { inflated: false, peakRunningBytes: 0 };
		__setDecodeProbe(probe);
		try {
			decode(frag);
			throw new Error('decode should have thrown');
		} catch (e) {
			expect(e).toBeInstanceOf(ShareDecodeError);
			expect((e as ShareDecodeError).message).toMatch(/too large|cap|size/i);
		}
		// PROOF the full payload was never materialised: the inflater was never even driven.
		expect(probe.inflated).toBe(false);
		expect(probe.peakRunningBytes).toBe(0);
	});

	it('bounds PEAK allocation for a bomb that fits under the input cap (not merely throws)', () => {
		// A bomb whose COMPRESSED size is just under the input cap still gets inflated — but the
		// peak it can force is hard-bounded by MAX_COMPRESSED_BYTES × the worst-case deflate ratio.
		// Build the largest all-zeros payload whose deflate output still fits under MAX_COMPRESSED_BYTES,
		// then PROVE (via the allocation probe) the running total decode ever saw is bounded by that
		// ceiling — categorically NOT an unbounded multi-MB/GB allocation.
		// MAX_COMPRESSED_BYTES (8 KB) × ~1024 ≈ 8 MB ceiling; a ~6 MB zero payload deflates to <8 KB.
		const underCapBomb = new Uint8Array(6 * 1024 * 1024); // 6 MB of zeros
		const deflated = deflateSync(underCapBomb);
		expect(deflated.length).toBeLessThanOrEqual(MAX_COMPRESSED_BYTES); // inflate WILL run
		const frag = toBase64Url(deflated);

		const probe: DecodeAllocationProbe = { inflated: false, peakRunningBytes: 0 };
		__setDecodeProbe(probe);
		// 6 MB > MAX_DECODED_BYTES retention cap → still rejected.
		expect(() => decode(frag)).toThrow(ShareDecodeError);

		// The inflater ran, but the peak running total is hard-bounded by the input cap × max ratio.
		expect(probe.inflated).toBe(true);
		expect(probe.peakRunningBytes).toBeGreaterThan(MAX_DECODED_BYTES); // it DID exceed the retention cap
		expect(probe.peakRunningBytes).toBeLessThanOrEqual(MAX_COMPRESSED_BYTES * MAX_DEFLATE_RATIO);
	});

	it('rejects an over-long fragment by INPUT length before inflating at all', () => {
		// An attacker can also just send a giant compressed blob. The input-length cap rejects it
		// up front (decoded compressed bytes > MAX_COMPRESSED_BYTES) WITHOUT ever inflating.
		const oversizedCompressed = new Uint8Array(MAX_COMPRESSED_BYTES + 1024); // > cap, not valid deflate
		const frag = toBase64Url(oversizedCompressed);
		const probe: DecodeAllocationProbe = { inflated: false, peakRunningBytes: 0 };
		__setDecodeProbe(probe);
		expect(() => decode(frag)).toThrow(ShareDecodeError);
		// The guard rejected on input length — the inflater was never driven.
		expect(probe.inflated).toBe(false);
		expect(probe.peakRunningBytes).toBe(0);
	});

	it('accepts a payload at the cap boundary (just under is fine)', () => {
		// A real (non-bomb) payload comfortably under the cap round-trips.
		const big = 'x'.repeat(64 * 1024); // 64 KB of source — well under MAX_DECODED_BYTES
		expect(decode(encode(big))).toBe(big);
		expect(big.length).toBeLessThan(MAX_DECODED_BYTES);
	});
});

// Local base64url helper for the bomb test (mirrors encode.ts internals without importing them).
function toBase64Url(bytes: Uint8Array): string {
	let bin = '';
	for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
	return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// Reference the import so an unused-symbol lint never strips the type-only `strToU8` use above.
void strToU8;
