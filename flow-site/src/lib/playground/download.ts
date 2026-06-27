// Blob-download helper for playground exports (HANDOFF §9 — MIDI/WAV/notation download).
//
// Browser-only: creates an object URL from the bytes, clicks a synthetic anchor, then revokes the
// URL. Used for `RunResult.midi` (audio/midi) today; WAV + notation exports (D-48-19) reuse the
// same mechanism with the appropriate MIME type.

/** How long to keep the object URL alive after the click before revoking it. A 0ms timeout can fire
 *  before the download dialog has read the URL under load in some engines; a small real delay is safer
 *  while still freeing the blob promptly (WR-07). */
const REVOKE_DELAY_MS = 1000;

/**
 * Trigger a browser download for the given bytes.
 *
 * @param bytes - the payload (e.g. `RunResult.midi` Uint8Array).
 * @param filename - suggested download filename (e.g. `flow.mid`).
 * @param mime - MIME type (e.g. `audio/midi`).
 * @returns `true` when the download was dispatched, `false` if it could not start (non-browser, no
 *   `<body>` to mount the anchor, or `a.click()` threw — e.g. a sandboxed context blocking
 *   programmatic downloads). The caller (playground) can surface a "couldn't start download" toast on
 *   `false` rather than silently no-op'ing (WR-07).
 */
export function offerDownload(bytes: Uint8Array | BlobPart, filename: string, mime: string): boolean {
	// SSR / non-browser guard, plus guard a missing <body> (the anchor needs a mount point).
	if (typeof document === 'undefined' || !document.body) return false;

	// Normalize a possibly SharedArrayBuffer-backed Uint8Array into a plain ArrayBuffer-backed copy
	// so the Blob part is a concrete BlobPart under strict lib.dom typing (TS 6). Other BlobParts
	// (string / ArrayBuffer) pass through unchanged.
	let part: BlobPart;
	if (bytes instanceof Uint8Array) {
		const ab = new ArrayBuffer(bytes.byteLength);
		new Uint8Array(ab).set(bytes);
		part = ab;
	} else {
		part = bytes as BlobPart;
	}
	const blob = new Blob([part], { type: mime });
	const url = URL.createObjectURL(blob);
	const a = Object.assign(document.createElement('a'), { href: url, download: filename });
	let dispatched = false;
	try {
		document.body.appendChild(a);
		a.click();
		dispatched = true;
	} catch (e) {
		// A sandboxed / policy-blocked context can throw on programmatic click. Don't swallow silently:
		// log and report failure so the caller can toast. The blob is still revoked in `finally`.
		console.error('[download] could not start download', e);
	} finally {
		a.remove();
		// Revoke after a real delay so the download dialog has read the URL before it is freed.
		setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS);
	}
	return dispatched;
}

/** Convenience for the common MIDI export case (HANDOFF §9). Returns whether the download started. */
export function offerMidiDownload(midi: Uint8Array, filename = 'flow.mid'): boolean {
	return offerDownload(midi, filename, 'audio/midi');
}
