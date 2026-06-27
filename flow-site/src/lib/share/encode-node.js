// Node-side mirror of `encode.ts`'s `encode()` — for build-time carriers that run under Node (the
// docs mdsvex highlighter in `svelte.config.js`, which Node's ESM loader can't pull a `.ts` into).
//
// This MUST stay byte-identical to `encode.ts`'s `encode()`: fflate deflateSync → base64url (no
// `+`/`/`/`=`). The browser `decode()` consumes the result of BOTH, so any drift here would break
// docs deep-links while leaving Home/playground intact. `share/encode-node.test.ts` pins the parity.

import { deflateSync, strToU8 } from 'fflate';

/**
 * base64url-encode bytes: `+`→`-`, `/`→`_`, strip `=`. Chunked for large inputs.
 * @param {Uint8Array} bytes
 * @returns {string}
 */
function toBase64Url(bytes) {
	let bin = '';
	const CHUNK = 0x8000;
	for (let i = 0; i < bytes.length; i += CHUNK) {
		bin += String.fromCharCode(...bytes.subarray(i, i + CHUNK));
	}
	// btoa exists in Node 16+; fall back to Buffer when it doesn't.
	const b64 =
		typeof btoa === 'function'
			? btoa(bin)
			: Buffer.from(bin, 'binary').toString('base64');
	return b64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/**
 * Encode Flow source into a base64url `#code=` fragment value — identical output to encode.ts.
 * @param {string} src raw Flow source
 * @returns {string}
 */
export function encode(src) {
	return toBase64Url(deflateSync(strToU8(src)));
}
