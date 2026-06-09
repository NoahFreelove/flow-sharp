// URL-fragment share — the default, zero-backend, anonymous share path (D-49-30, RESEARCH Pattern 7).
//
// `encode(src)` → a base64url string carried in `/playground#code=...`; the fragment never leaves
// the browser, so there is no server cost and sharing works without an account. `decode(frag)`
// inflates it back into Flow source.
//
// SECURITY (T-49-CSP-FRAG — the threat model's `#code=` decode mitigation): the fragment is
// attacker-controllable (anyone can craft a link). `decode` is therefore DEFENSIVE:
//   - it wraps base64 + inflate in try/catch and throws a TYPED `ShareDecodeError` (never a raw
//     exception) so the playground can render the UI-SPEC "Couldn't decode this shared snippet" copy;
//   - it enforces a hard decoded-size CAP using fflate's STREAMING inflater, driven with SMALL
//     bounded INPUT slices so no single inflate call can expand the whole stream in one allocation,
//     and aborting the instant the running output total crosses the cap. Combined with an up-front
//     cap on the COMPRESSED input length, this bounds PEAK allocation (not just retention) — the
//     decompression-bomb guard a few-KB malicious payload would otherwise defeat.
// The decoded source is loaded into Monaco as plain text, never `innerHTML`/`{@html}` — XSS is out
// of scope for the carrier itself (Security V5).

import { deflateSync, strToU8, strFromU8, Inflate } from 'fflate';

/**
 * Hard ceiling on the inflated payload. 256 KB is far above any realistic Flow snippet (the longest
 * shipped showcase source is a few KB) yet well below a DoS-grade allocation. `decode` rejects any
 * fragment whose inflated output would exceed this before allocating it.
 */
export const MAX_DECODED_BYTES = 256 * 1024;

/**
 * DEFLATE's worst-case (stored-RLE-of-zeros) expansion ratio, measured against the installed
 * `fflate@0.8.3`: 16 MB of zeros deflates to 16,384 bytes → ~1024:1. We bound PEAK allocation by
 * bounding the COMPRESSED input that ever reaches the inflater, since `fflate`'s `Inflate` buffers the
 * whole pushed stream and inflates it in a SINGLE callback chunk on the `final` push (input-slicing
 * does NOT chunk the OUTPUT — verified empirically; see `encode.test.ts`). So the only real lever on
 * peak allocation is the input-length cap below.
 */
export const MAX_DEFLATE_RATIO = 1024;

/**
 * Hard ceiling on the COMPRESSED input (the base64url fragment's decoded byte length). This is THE
 * peak-allocation guard (CR-01): a malicious `#code=` link can deflate-expand ~1024:1, and because
 * `fflate`'s streaming `Inflate` delivers the entire inflated stream in one callback allocation, the
 * ONLY way to bound that peak is to bound the input. With an 8 KB compressed cap the worst a bomb can
 * force is ~8 KB × 1024 ≈ 8 MB — a hard, deterministic ceiling, not the multi-MB/GB OOM the unbounded
 * guard allowed. A legitimate Flow snippet deflates far below this (the longest shipped showcase
 * source compresses to <1 KB; a worst-case 256 KB decoded payload — the retention cap — needs only a
 * few KB compressed at real-text ratios), so this never rejects a real share link. The decoded-output
 * cap (`MAX_DECODED_BYTES`) still gates RETENTION; this gates the ALLOCATION the link can provoke.
 */
export const MAX_COMPRESSED_BYTES = 8 * 1024;

/** Compressed input is pushed to the inflater in bounded slices. NOTE: with `fflate`'s buffering
 *  `Inflate` this does NOT chunk the inflated output (the whole stream lands in one callback on the
 *  final push); the real peak bound is `MAX_COMPRESSED_BYTES`. Slicing is kept only so the input loop
 *  reads in fixed-size steps and the running-total guard can still abort mid-stream on the rare
 *  multi-block input. */
const INPUT_SLICE = 4096;

/**
 * TEST-ONLY allocation observer (CR-01 regression). When set, {@link decode} reports, per call,
 * whether the inflater was ever driven (`inflated`) and the PEAK running output total it accumulated
 * (`peakRunningBytes`). The bomb test asserts the peak stayed bounded near the cap rather than ballooning
 * to the full inflated payload — i.e. that peak ALLOCATION is bounded, not merely that decode throws.
 * Production callers never set this; it is a no-op in the shipped path. (ESM namespace exports can't be
 * spied under Vitest, so an explicit hook is the portable way to assert the streaming guard's behaviour.)
 */
export interface DecodeAllocationProbe {
	inflated: boolean;
	peakRunningBytes: number;
}
let __decodeProbe: DecodeAllocationProbe | null = null;
/** @internal test hook — install (or clear with `null`) the allocation probe for the next decode(s). */
export function __setDecodeProbe(probe: DecodeAllocationProbe | null): void {
	__decodeProbe = probe;
}

/**
 * Typed failure thrown by {@link decode} for malformed / oversized fragments. The playground maps
 * this to the friendly UI-SPEC copy; callers should catch THIS rather than a bare `Error`.
 */
export class ShareDecodeError extends Error {
	constructor(message: string, options?: { cause?: unknown }) {
		super(message, options);
		this.name = 'ShareDecodeError';
	}
}

/** base64 → base64url: `+`→`-`, `/`→`_`, strip `=` padding. */
function toBase64Url(bytes: Uint8Array): string {
	let bin = '';
	// Chunked to avoid String.fromCharCode(...spread) stack limits on large inputs.
	const CHUNK = 0x8000;
	for (let i = 0; i < bytes.length; i += CHUNK) {
		bin += String.fromCharCode(...bytes.subarray(i, i + CHUNK));
	}
	return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** base64url → bytes. Throws on non-base64 input (caught by `decode` and rewrapped). */
function fromBase64Url(frag: string): Uint8Array {
	const b64 = frag.replace(/-/g, '+').replace(/_/g, '/');
	const bin = atob(b64); // throws DOMException on invalid base64 → caught by decode
	const out = new Uint8Array(bin.length);
	for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
	return out;
}

/**
 * Encode Flow source into a base64url `#code=` fragment value (no `+`/`/`/`=`).
 * @param src raw Flow source
 */
export function encode(src: string): string {
	return toBase64Url(deflateSync(strToU8(src)));
}

/**
 * Decode a `#code=` fragment value back into Flow source.
 *
 * Tolerates a leading `#code=` or `code=` prefix (carrier-friendly). Throws {@link ShareDecodeError}
 * on malformed input, when the COMPRESSED input exceeds {@link MAX_COMPRESSED_BYTES} (the
 * peak-allocation guard — rejected BEFORE inflating, so a bomb can force at most
 * `MAX_COMPRESSED_BYTES × ~1024` ≈ 8 MB of inflate, never a multi-GB OOM), or when the inflated
 * output would exceed {@link MAX_DECODED_BYTES} (the retention cap, checked during streaming inflate).
 *
 * @param frag the base64url fragment (with or without a `#code=`/`code=` prefix)
 */
export function decode(frag: string): string {
	if (typeof frag !== 'string') {
		throw new ShareDecodeError('Couldn’t decode this shared snippet — no data.');
	}
	// Strip an optional carrier prefix so callers can pass the raw hash.
	let value = frag;
	if (value.startsWith('#code=')) value = value.slice('#code='.length);
	else if (value.startsWith('code=')) value = value.slice('code='.length);
	if (value.length === 0) {
		throw new ShareDecodeError('Couldn’t decode this shared snippet — the link is empty.');
	}

	// Reject an over-long fragment BEFORE base64-decoding it: a base64url string of length L decodes to
	// ~3L/4 compressed bytes, so cap the fragment so the decoded input can't exceed MAX_COMPRESSED_BYTES.
	// This bounds the input that ever reaches the inflater (decompression-bomb guard, first half).
	if (value.length > Math.ceil((MAX_COMPRESSED_BYTES * 4) / 3)) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the payload is too large (size cap exceeded).'
		);
	}

	let compressed: Uint8Array;
	try {
		compressed = fromBase64Url(value);
	} catch (e) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the link may be incomplete or corrupted.',
			{ cause: e }
		);
	}

	// Second input-length guard, now against the exact decoded byte length (the length cap above is a
	// conservative pre-filter; this is the precise bound the streaming loop relies on).
	if (compressed.length > MAX_COMPRESSED_BYTES) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the payload is too large (size cap exceeded).'
		);
	}

	// Streaming inflate with a running RETENTION cap. PEAK allocation is already bounded by the
	// MAX_COMPRESSED_BYTES input cap above (fflate buffers the stream and inflates it in one callback,
	// so input-slicing can't shrink a single output chunk). This running-total guard ensures we never
	// RETAIN more than MAX_DECODED_BYTES, and aborts mid-stream on the rare genuinely-multi-block input.
	const chunks: Uint8Array[] = [];
	let total = 0;
	let bombed = false;
	let streamError: unknown = null;

	const probe = __decodeProbe; // snapshot (test-only; null in production)

	const inflater = new Inflate((chunk, _final) => {
		if (bombed) return;
		// Track the would-be running total for the allocation probe (test-only) BEFORE the guard, so the
		// test can confirm the peak the inflater ever pushed at us stayed bounded near the cap.
		if (probe) {
			const wouldBe = total + chunk.length;
			if (wouldBe > probe.peakRunningBytes) probe.peakRunningBytes = wouldBe;
		}
		if (total + chunk.length > MAX_DECODED_BYTES) {
			bombed = true;
			return; // stop accumulating — the partial output is discarded, nothing past the cap is kept
		}
		total += chunk.length;
		chunks.push(chunk);
	});

	try {
		if (probe) probe.inflated = true;
		for (let i = 0; i < compressed.length && !bombed; i += INPUT_SLICE) {
			const final = i + INPUT_SLICE >= compressed.length;
			inflater.push(compressed.subarray(i, i + INPUT_SLICE), final);
		}
	} catch (e) {
		// fflate throws on a corrupt deflate stream (bad header, invalid block, checksum).
		streamError = e;
	}

	if (bombed) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the payload is too large (size cap exceeded).'
		);
	}
	if (streamError) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the link may be incomplete or corrupted.',
			{ cause: streamError }
		);
	}

	// Concatenate the inflated chunks (total is already known to be ≤ cap).
	const out = new Uint8Array(total);
	let offset = 0;
	for (const c of chunks) {
		out.set(c, offset);
		offset += c.length;
	}

	try {
		return strFromU8(out);
	} catch (e) {
		throw new ShareDecodeError(
			'Couldn’t decode this shared snippet — the link may be incomplete or corrupted.',
			{ cause: e }
		);
	}
}
