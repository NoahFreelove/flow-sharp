// GitHub gist OAuth — Cloudflare Worker / SvelteKit `/api/auth/github` handler (D-49-28, RESEARCH
// Pattern 6, Simon-Willison "OAuth for a static site"). This is the security-concentrated surface of
// Plan 49-06; every threat-model mitigation lives here:
//   - T-49-SECRET: GITHUB_CLIENT_SECRET is read from `env` and sent ONLY server→GitHub. It is never
//     put in a response body, header, redirect, or log. GITHUB_CLIENT_ID may be public.
//   - T-49-OAUTH-CSRF: a `state` minted with crypto.getRandomValues is stashed in a `__Host-`-prefixed
//     httpOnly cookie on the authorize leg and validated against the cookie on the callback leg BEFORE
//     the exchange. Mismatch / absence → 400, no token endpoint call. The `__Host-` prefix forbids a
//     `Domain=` attribute and requires `Path=/; Secure`, so a sibling subdomain on `*.pages.dev`
//     cannot write the cookie — closing the state-fixation vector (CR-03). The state is treated as
//     strictly SINGLE-USE: the cookie is cleared on EVERY terminal callback outcome (origin mismatch,
//     state mismatch, exchange failure, AND success), so a captured code+state pair can't be replayed.
//   - T-49-REDIRECT: BOTH the `redirect_uri` sent to GitHub AND the post-exchange token redirect are
//     built from a SERVER-KNOWN constant origin (`SITE_ORIGIN` env), NOT from the request `Host`
//     header. The callback is rejected outright if its `url.origin` does not match `SITE_ORIGIN`.
//     This closes the token-theft / open-redirect vector where an attacker who can influence the Host
//     on a preview/custom/proxied host would otherwise have the freshly-minted token land on a host
//     they observe (CR-02). The redirect path stays hard-coded `/playground`; the ORIGIN is now
//     pinned too (it was the security-relevant half that used to be reflected from the request).
//   - T-49-SCOPE: the authorize URL requests `scope=gist` only (least privilege).
// The token rides back in the URL FRAGMENT (`#token=`) so it never reaches the server/logs; the
// client reads it into sessionStorage (D-49-28).

/** Local-dev default origin (vite dev server). Production sets SITE_ORIGIN in CF Pages `[vars]`. */
export const DEFAULT_SITE_ORIGIN = 'http://localhost:5173';

/** Env shape the worker reads (CF Pages env vars; secret value set in the dashboard, never committed). */
export interface GistAuthEnv {
	GITHUB_CLIENT_ID: string;
	GITHUB_CLIENT_SECRET: string;
	/**
	 * The canonical site origin (e.g. `https://flow-music.pages.dev`). The OAuth redirect_uri and the
	 * token-bearing redirect are pinned to THIS — never reflected from the request Host (CR-02). Falls
	 * back to {@link DEFAULT_SITE_ORIGIN} for local dev when unset.
	 */
	SITE_ORIGIN?: string;
}

/**
 * httpOnly cookie name the CSRF `state` is stashed under. The `__Host-` prefix is a browser-enforced
 * lock (CR-03): the cookie is ONLY accepted if it has `Secure`, `Path=/`, and NO `Domain=` attribute,
 * which means a sibling subdomain (e.g. another `*.pages.dev` site) cannot set/overwrite it — closing
 * the state-fixation vector.
 */
export const STATE_COOKIE = '__Host-flow_oauth_state';

/** Set-Cookie value that EXPIRES the state cookie (Max-Age=0). Used on every terminal callback
 *  outcome so the state is strictly single-use (CR-03). Attributes mirror the set form so the browser
 *  matches and clears it. `__Host-` requires Secure + Path=/ + no Domain. */
const CLEAR_STATE_COOKIE = `${STATE_COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0`;

const TOKEN_URL = 'https://github.com/login/oauth/access_token';
const AUTHORIZE_URL = 'https://github.com/login/oauth/authorize';

/**
 * Mint a CSRF `state` from `crypto.getRandomValues` (NEVER Math.random — Security V6). 16 random
 * bytes → 32 hex chars; unguessable, URL-safe, cookie-safe.
 */
function mintState(): string {
	const bytes = crypto.getRandomValues(new Uint8Array(16));
	return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

/** Parse the `state` value out of the request's Cookie header. */
function cookieState(request: Request): string | null {
	const raw = request.headers.get('cookie') ?? '';
	const m = raw.match(new RegExp(`(?:^|;\\s*)${STATE_COOKIE}=([^;]+)`));
	return m ? m[1] : null;
}

/** The canonical `/api/auth/github` handler. Pure of the CF runtime → unit-testable with mocked fetch. */
export async function handleGistAuth(request: Request, env: GistAuthEnv): Promise<Response> {
	const url = new URL(request.url);
	const code = url.searchParams.get('code');

	// CR-02: pin the redirect ORIGIN to a server-known constant — NEVER reflect the request Host.
	// Both the redirect_uri sent to GitHub and the token-bearing redirect below derive from this.
	const siteOrigin = env.SITE_ORIGIN || DEFAULT_SITE_ORIGIN;
	const redirectUri = `${siteOrigin}/api/auth/github`;

	// Leg 1 — no code yet: mint a CSRF state, stash it httpOnly, and 302 to GitHub's consent screen.
	if (!code) {
		const state = mintState();
		const authorize =
			`${AUTHORIZE_URL}?client_id=${encodeURIComponent(env.GITHUB_CLIENT_ID)}` +
			`&redirect_uri=${encodeURIComponent(redirectUri)}&scope=gist&state=${state}`;
		return new Response(null, {
			status: 302,
			headers: {
				location: authorize,
				// __Host- + httpOnly + SameSite=Lax: survives GitHub's redirect back, unreadable to JS,
				// and (via __Host-) unwritable by sibling subdomains. No Domain= (required by __Host-).
				'set-cookie': `${STATE_COOKIE}=${state}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=600`
			}
		});
	}

	// Leg 2 — callback. From here on the `state` cookie is STRICTLY SINGLE-USE: every terminal outcome
	// below clears it (CLEAR_STATE_COOKIE), so a captured code+state pair can never be replayed (CR-03).

	// CR-02: reject the callback outright if it did not arrive on the canonical origin (the request Host
	// does not match SITE_ORIGIN). Clear the state cookie even here. Blocks a token landing on any
	// preview/custom/proxied host an attacker can influence, before any state check or exchange.
	if (url.origin !== siteOrigin) {
		return new Response('Invalid callback origin.', {
			status: 400,
			headers: { 'set-cookie': CLEAR_STATE_COOKIE }
		});
	}

	// Validate state (CSRF) BEFORE any exchange. Clear the cookie on mismatch/absence — single-use.
	const state = url.searchParams.get('state');
	const stashed = cookieState(request);
	if (!state || !stashed || state !== stashed) {
		return new Response('Invalid OAuth state.', {
			status: 400,
			headers: { 'set-cookie': CLEAR_STATE_COOKIE }
		});
	}

	// Exchange the code server-side — the ONLY place the secret is used.
	const res = await fetch(TOKEN_URL, {
		method: 'POST',
		headers: { 'content-type': 'application/json', accept: 'application/json' },
		body: JSON.stringify({
			client_id: env.GITHUB_CLIENT_ID,
			client_secret: env.GITHUB_CLIENT_SECRET,
			code,
			state
		})
	});
	const data = (await res.json().catch(() => ({}))) as { access_token?: string; error?: string };
	if (!data.access_token) {
		// CR-03: clear the state on exchange failure too — the same code+state must not be retried.
		return new Response('OAuth exchange failed.', {
			status: 400,
			headers: { 'set-cookie': CLEAR_STATE_COOKIE }
		});
	}

	// 302 to the pinned SITE_ORIGIN /playground with the token in the FRAGMENT (open-redirect guard —
	// the origin is server-known, NOT reflected from the request Host). Clear the now-consumed state.
	return new Response(null, {
		status: 302,
		headers: {
			location: `${siteOrigin}/playground#token=${encodeURIComponent(data.access_token)}`,
			'set-cookie': CLEAR_STATE_COOKIE
		}
	});
}
