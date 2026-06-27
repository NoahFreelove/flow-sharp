// Client-side gist creation (D-49-29). After the OAuth worker hands the browser an access token
// (in the URL fragment → sessionStorage), gist creation happens entirely client-side: a single
// authenticated POST to api.github.com/gists with `Authorization: Bearer <token>`. The token never
// returns to our server; the worker only ever holds the secret (T-49-SECRET).
//
// Least privilege (T-49-SCOPE): the token is minted with `scope=gist` by the worker, so it can ONLY
// create/read gists. We store it in sessionStorage (ephemeral — cleared when the tab closes,
// D-49-28) rather than localStorage or a cookie.

/** sessionStorage key the OAuth-returned access token lives under. */
export const GIST_TOKEN_KEY = 'flow_gist_token';

/** The minimal shape we read back from a successful `POST /gists`. */
export interface GistResult {
	/** Human-facing gist URL, e.g. `https://gist.github.com/<user>/<id>`. */
	htmlUrl: string;
	/** The gist id. */
	id: string;
}

/** Read the cached gist token from sessionStorage (null when not authenticated). */
export function getGistToken(): string | null {
	if (typeof sessionStorage === 'undefined') return null;
	return sessionStorage.getItem(GIST_TOKEN_KEY);
}

/** Cache the gist token in sessionStorage (called after the OAuth fragment is read). */
export function setGistToken(token: string): void {
	if (typeof sessionStorage === 'undefined') return;
	sessionStorage.setItem(GIST_TOKEN_KEY, token);
}

/** Drop the cached token (e.g. on a 401 — forces a fresh OAuth round-trip). */
export function clearGistToken(): void {
	if (typeof sessionStorage === 'undefined') return;
	sessionStorage.removeItem(GIST_TOKEN_KEY);
}

/** sessionStorage key used to stash the editor source before OAuth navigation (§6.1). */
export const GIST_PENDING_SOURCE_KEY = 'flow_pending_gist_source';

/**
 * Begin the OAuth flow by navigating to the worker's authorize leg. The worker mints the CSRF
 * `state`, stashes it httpOnly, and bounces to GitHub; on return it drops the token in the URL
 * fragment which the playground reads into sessionStorage.
 *
 * §6.1: before navigating, stash `source` + a pending-save flag in sessionStorage so the playground
 * can auto-resume the save when the OAuth round-trip returns with `#token=`.
 */
export function beginGistAuth(source?: string): void {
	if (typeof window === 'undefined') return;
	if (source !== undefined) {
		sessionStorage.setItem(GIST_PENDING_SOURCE_KEY, source);
	}
	window.location.href = '/api/auth/github';
}

/**
 * Pop and return the pending-save source stashed before an OAuth navigation (§6.1).
 * Returns null when no stash exists (normal page loads). Removes the key after reading
 * so it is consumed exactly once.
 */
export function consumePendingGistSource(): string | null {
	if (typeof sessionStorage === 'undefined') return null;
	const src = sessionStorage.getItem(GIST_PENDING_SOURCE_KEY);
	if (src !== null) {
		sessionStorage.removeItem(GIST_PENDING_SOURCE_KEY);
	}
	return src;
}

/**
 * Create a public gist from Flow source. Returns the gist's html_url + id.
 * @throws Error on a non-2xx GitHub response (the caller maps it to a friendly toast).
 */
export async function createGist(source: string, token: string): Promise<GistResult> {
	const res = await fetch('https://api.github.com/gists', {
		method: 'POST',
		headers: {
			Authorization: `Bearer ${token}`,
			Accept: 'application/vnd.github+json',
			'Content-Type': 'application/json'
		},
		body: JSON.stringify({
			description: 'Shared from the Flow playground',
			public: true,
			files: {
				'snippet.flow': { content: source.length > 0 ? source : '// empty Flow snippet' }
			}
		})
	});
	if (!res.ok) {
		// A 401 means the token expired/was revoked — surface a typed message so the UI can re-auth.
		throw new Error(
			res.status === 401
				? 'Your gist session expired — sign in again to save.'
				: `Couldn’t save to gist (GitHub returned ${res.status}).`
		);
	}
	const data = (await res.json()) as { html_url?: string; id?: string };
	if (!data.html_url || !data.id) {
		throw new Error('Couldn’t save to gist — GitHub returned an unexpected response.');
	}
	return { htmlUrl: data.html_url, id: data.id };
}
