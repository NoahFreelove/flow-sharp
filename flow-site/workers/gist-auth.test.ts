import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { handleGistAuth, STATE_COOKIE } from './gist-auth';

// REQ-SITE-SHARE-02 — OAuth worker validates `state` + exchanges code (mocked) (T-49-OAUTH-CSRF,
// T-49-SECRET, T-49-REDIRECT, T-49-SCOPE).
//
// The worker is the security-concentrated surface of Plan 49-06. These tests pin the threat-model
// mitigations WITHOUT ever calling the real GitHub API (mocked `fetch`):
//   - T-49-OAUTH-CSRF: a callback whose `state` does not match the stashed cookie is rejected 400
//     with NO token exchange (the token endpoint is never hit).
//   - the happy path exchanges the code server-side and 302s to a HARD-CODED same-origin
//     `/playground#token=...` (T-49-REDIRECT open-redirect guard — no user-supplied redirect).
//   - T-49-SECRET: GITHUB_CLIENT_SECRET never appears in any response body or redirect URL.
//   - T-49-SCOPE: the authorize redirect requests `scope=gist` only (least privilege).

const ORIGIN = 'https://flow.example.pages.dev';

const ENV = {
	GITHUB_CLIENT_ID: 'test-client-id',
	GITHUB_CLIENT_SECRET: 'super-secret-do-not-leak',
	// CR-02: the redirect origin is pinned to this SERVER-KNOWN constant, not the request Host.
	SITE_ORIGIN: ORIGIN
};

/** Build a Request for the worker; `cookie` simulates the browser sending back the stashed state. */
function req(path: string, opts: { cookie?: string } = {}): Request {
	const headers = new Headers();
	if (opts.cookie) headers.set('cookie', opts.cookie);
	return new Request(`${ORIGIN}${path}`, { headers });
}

/** Pull the `flow_oauth_state` value out of a Set-Cookie header (the worker stashes it). */
function stateFromSetCookie(res: Response): string | null {
	const sc = res.headers.get('set-cookie');
	if (!sc) return null;
	const m = sc.match(new RegExp(`${STATE_COOKIE}=([^;]+)`));
	return m ? m[1] : null;
}

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
	fetchMock = vi.fn();
	vi.stubGlobal('fetch', fetchMock);
});
afterEach(() => {
	vi.unstubAllGlobals();
});

describe('gist-auth worker (OAuth code exchange + state CSRF guard)', () => {
	it('initial request (no code) → 302 to github authorize with scope=gist + a state cookie', async () => {
		const res = await handleGistAuth(req('/api/auth/github'), ENV);
		expect(res.status).toBe(302);

		const location = res.headers.get('location')!;
		expect(location).toContain('https://github.com/login/oauth/authorize');
		expect(location).toContain(`client_id=${ENV.GITHUB_CLIENT_ID}`);
		// T-49-SCOPE — least privilege: gist scope ONLY.
		expect(location).toContain('scope=gist');
		// CR-02: redirect_uri is pinned to the CONFIGURED SITE_ORIGIN, not the request Host.
		expect(location).toContain(encodeURIComponent(`${ENV.SITE_ORIGIN}/api/auth/github`));
		// A crypto state rides the URL and is stashed in an httpOnly cookie.
		const cookie = res.headers.get('set-cookie')!;
		expect(cookie).toContain(STATE_COOKIE);
		expect(cookie.toLowerCase()).toContain('httponly');
		// CR-03: the cookie uses the __Host- prefix (forbids Domain=, requires Path=/ + Secure) so a
		// sibling subdomain cannot write it — closing the state-fixation vector.
		expect(STATE_COOKIE.startsWith('__Host-')).toBe(true);
		expect(cookie).not.toMatch(/domain=/i);
		expect(cookie).toMatch(/path=\//i);
		expect(cookie.toLowerCase()).toContain('secure');
		const cookieState = stateFromSetCookie(res)!;
		expect(location).toContain(`state=${cookieState}`);
		// No token exchange on the authorize leg.
		expect(fetchMock).not.toHaveBeenCalled();
	});

	it('rejects a callback whose `state` does not match the stashed value (CSRF) — no exchange', async () => {
		// Cookie stashes one state; the callback URL carries a DIFFERENT state.
		const res = await handleGistAuth(
			req('/api/auth/github?code=abc123&state=attacker-state', {
				cookie: `${STATE_COOKIE}=legit-stashed-state`
			}),
			ENV
		);
		expect(res.status).toBe(400);
		// The token endpoint must NEVER be called when state fails.
		expect(fetchMock).not.toHaveBeenCalled();
		// Secret never leaks in the rejection body.
		const body = await res.text();
		expect(body).not.toContain(ENV.GITHUB_CLIENT_SECRET);
	});

	it('rejects a callback with a code but NO stashed cookie (CSRF) — no exchange', async () => {
		const res = await handleGistAuth(
			req('/api/auth/github?code=abc123&state=whatever'), // no cookie
			ENV
		);
		expect(res.status).toBe(400);
		expect(fetchMock).not.toHaveBeenCalled();
	});

	it('rejects a callback that arrives on a Host other than SITE_ORIGIN (CR-02) — no exchange', async () => {
		// Simulate the OAuth callback hitting a DIFFERENT host (preview / custom / proxied / attacker)
		// than the configured SITE_ORIGIN. The state cookie even matches — the origin guard fires first.
		const evilOrigin = 'https://attacker.example.com';
		const headers = new Headers();
		headers.set('cookie', `${STATE_COOKIE}=good`);
		const res = await handleGistAuth(
			new Request(`${evilOrigin}/api/auth/github?code=abc&state=good`, { headers }),
			ENV
		);
		expect(res.status).toBe(400);
		// The token endpoint must NEVER be reached on a mismatched-origin callback.
		expect(fetchMock).not.toHaveBeenCalled();
	});

	it('pins the redirect origin to SITE_ORIGIN even if the request Host differs (CR-02)', async () => {
		// The authorize leg arrives on a non-canonical host; the redirect_uri must STILL be the
		// configured SITE_ORIGIN, not the request Host.
		const otherHost = 'https://preview-abc123.flow-music.pages.dev';
		const res = await handleGistAuth(new Request(`${otherHost}/api/auth/github`), ENV);
		const location = res.headers.get('location')!;
		expect(location).toContain(encodeURIComponent(`${ENV.SITE_ORIGIN}/api/auth/github`));
		expect(location).not.toContain(encodeURIComponent(`${otherHost}/api/auth/github`));
	});

	it('valid state → exchanges code server-side, 302s to same-origin /playground#token=…', async () => {
		fetchMock.mockResolvedValueOnce(
			new Response(JSON.stringify({ access_token: 'gho_mocktoken', scope: 'gist' }), {
				status: 200,
				headers: { 'content-type': 'application/json' }
			})
		);

		const state = 'matching-state-123';
		const res = await handleGistAuth(
			req(`/api/auth/github?code=goodcode&state=${state}`, {
				cookie: `${STATE_COOKIE}=${state}`
			}),
			ENV
		);

		// Exactly one server-side exchange to the GitHub token endpoint.
		expect(fetchMock).toHaveBeenCalledTimes(1);
		const [url, init] = fetchMock.mock.calls[0];
		expect(String(url)).toBe('https://github.com/login/oauth/access_token');
		// The secret is sent SERVER→GitHub, with Accept: application/json.
		expect((init as RequestInit).method).toBe('POST');
		const sentBody = JSON.parse((init as RequestInit).body as string);
		expect(sentBody.client_id).toBe(ENV.GITHUB_CLIENT_ID);
		expect(sentBody.client_secret).toBe(ENV.GITHUB_CLIENT_SECRET);
		expect(sentBody.code).toBe('goodcode');

		// 302 to /playground with the token in the fragment. CR-02: the redirect ORIGIN equals the
		// CONFIGURED SITE_ORIGIN constant — NOT something reflected from the request.
		expect(res.status).toBe(302);
		const location = res.headers.get('location')!;
		expect(location.startsWith(`${ENV.SITE_ORIGIN}/playground#token=`)).toBe(true);
		expect(new URL(location).origin).toBe(ENV.SITE_ORIGIN);
		expect(location).toContain('gho_mocktoken');
		// The stale state cookie is cleared on success.
		expect((res.headers.get('set-cookie') ?? '').toLowerCase()).toContain('max-age=0');
	});

	it('never echoes GITHUB_CLIENT_SECRET in any response (initial, error, success)', async () => {
		fetchMock.mockResolvedValue(
			new Response(JSON.stringify({ access_token: 'gho_x', scope: 'gist' }), { status: 200 })
		);

		const initial = await handleGistAuth(req('/api/auth/github'), ENV);
		const error = await handleGistAuth(
			req('/api/auth/github?code=c&state=bad', { cookie: `${STATE_COOKIE}=good` }),
			ENV
		);
		const ok = await handleGistAuth(
			req('/api/auth/github?code=c&state=good', { cookie: `${STATE_COOKIE}=good` }),
			ENV
		);

		for (const res of [initial, error, ok]) {
			const body = await res.clone().text();
			const headers = JSON.stringify([...res.headers.entries()]);
			expect(body).not.toContain(ENV.GITHUB_CLIENT_SECRET);
			expect(headers).not.toContain(ENV.GITHUB_CLIENT_SECRET);
		}
	});

	it('rejects a token-exchange that GitHub answers with an OAuth error (no token in redirect)', async () => {
		fetchMock.mockResolvedValueOnce(
			new Response(JSON.stringify({ error: 'bad_verification_code' }), { status: 200 })
		);
		const state = 's';
		const res = await handleGistAuth(
			req(`/api/auth/github?code=c&state=${state}`, { cookie: `${STATE_COOKIE}=${state}` }),
			ENV
		);
		// Not a success redirect; the body/redirect must not carry an access token.
		expect(res.status).toBeGreaterThanOrEqual(400);
		const body = await res.text();
		expect(body).not.toContain('access_token');
		// CR-03: the state cookie is cleared on exchange failure too — single-use, no replay.
		expect((res.headers.get('set-cookie') ?? '').toLowerCase()).toContain('max-age=0');
	});

	it('clears the state cookie on EVERY terminal callback outcome (CR-03 single-use)', async () => {
		// (a) mismatched origin
		const evil = new Request(`https://attacker.example.com/api/auth/github?code=c&state=good`, {
			headers: new Headers({ cookie: `${STATE_COOKIE}=good` })
		});
		const originRes = await handleGistAuth(evil, ENV);
		expect((originRes.headers.get('set-cookie') ?? '').toLowerCase()).toContain('max-age=0');

		// (b) state mismatch
		const csrfRes = await handleGistAuth(
			req('/api/auth/github?code=c&state=attacker', { cookie: `${STATE_COOKIE}=legit` }),
			ENV
		);
		expect((csrfRes.headers.get('set-cookie') ?? '').toLowerCase()).toContain('max-age=0');

		// (c) successful exchange
		fetchMock.mockResolvedValueOnce(
			new Response(JSON.stringify({ access_token: 'gho_ok' }), { status: 200 })
		);
		const okRes = await handleGistAuth(
			req('/api/auth/github?code=c&state=s', { cookie: `${STATE_COOKIE}=s` }),
			ENV
		);
		expect((okRes.headers.get('set-cookie') ?? '').toLowerCase()).toContain('max-age=0');
	});
});
