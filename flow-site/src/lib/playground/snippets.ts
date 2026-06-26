// Quick-Start snippet set for the playground (UI-SPEC §Playground left rail / D-49-23).
//
// The example SOURCES no longer live in this module — they ship as plain files under
// `static/examples/*.flow` and are loaded at runtime via `static/examples/manifest.json`
// (composer request: "move examples to a /public folder and load them in from there instead
// of hardcoding"). On Cloudflare Pages those files are served straight from the built static
// output, so the playground stays a pure-static SPA (no new backend, no new dependency).
//
// Every example is authored to run on the Phase 48 WEB target — so NO sampler/OSC modules
// (stripped on Web per Phase 47), no `(micBuffer ...)` (InputFunctions stripped), no `live { }`
// blocks (parse-time error on Web). The default starter matches the dev-smoke harness + HANDOFF §10:
// `use "@audio"` then `(play (createSineTone 440Hz 1.0 0.5))` → audible 440 Hz tone on Run.

/** Metadata for one playground example (the editor source is fetched lazily from `file`). */
export interface SnippetMeta {
	/** Stable id (used as the list key + the active-rail highlight). */
	id: string;
	/** Human-facing label in the snippet list. */
	label: string;
	/** One-line description shown under the label. */
	blurb: string;
	/** The `static/examples/` filename whose contents load into the editor. */
	file: string;
}

/** The default snippet loaded on first mount (HANDOFF §10 — audible 440 Hz tone on Run). */
export const DEFAULT_SNIPPET_ID = 'sine-440';

/** An empty blank-snippet sentinel for "New blank" (UI-SPEC destructive confirm). */
export const BLANK_SOURCE = '';

/** A fetch-shaped function (the global `fetch` by default; injectable so tests can stub it). */
export type FetchLike = (input: string) => Promise<{ ok: boolean; status: number; json(): Promise<unknown>; text(): Promise<string> }>;

/**
 * Load the ordered example manifest from `static/examples/manifest.json`.
 *
 * `fetchFn` defaults to the global `fetch` and is injectable so a unit test can pass a stub.
 * Throws on a non-OK response or malformed JSON — the CALLER (the playground page) is responsible
 * for catching this and degrading charitably (empty rail + console.warn, never a crashed page).
 */
export async function loadManifest(fetchFn: FetchLike = fetch as unknown as FetchLike): Promise<SnippetMeta[]> {
	const res = await fetchFn('/examples/manifest.json');
	if (!res.ok) {
		throw new Error(`manifest fetch failed: ${res.status}`);
	}
	const data = (await res.json()) as SnippetMeta[];
	if (!Array.isArray(data)) {
		throw new Error('manifest is not an array');
	}
	return data;
}

/**
 * Load one example's Flow source from `static/examples/<file>`.
 *
 * `fetchFn` defaults to the global `fetch` and is injectable for tests. Throws on a non-OK
 * response so the caller can fall back to `BLANK_SOURCE` (keeping Monaco mountable).
 */
export async function loadSnippetSource(
	file: string,
	fetchFn: FetchLike = fetch as unknown as FetchLike
): Promise<string> {
	const res = await fetchFn(`/examples/${file}`);
	if (!res.ok) {
		throw new Error(`snippet fetch failed (${file}): ${res.status}`);
	}
	return res.text();
}
