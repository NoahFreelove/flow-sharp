// `/api/auth/github` — the only server route on an otherwise statically-prerendered site (D-49-13,
// adapter-cloudflare). It delegates to the portable, unit-tested `handleGistAuth` worker handler so
// the OAuth/CSRF/secret logic lives in ONE place (workers/gist-auth.ts) and is exercised by
// workers/gist-auth.test.ts with mocked fetch. The CF Pages env (`platform.env`) supplies
// GITHUB_CLIENT_ID + GITHUB_CLIENT_SECRET (the secret set in the dashboard, never committed —
// T-49-SECRET). This route is dynamic (not prerendered): it runs on every request.

import type { RequestHandler } from './$types';
import { handleGistAuth } from '../../../../../workers/gist-auth';

export const prerender = false;

export const GET: RequestHandler = async ({ request, platform }) => {
	// adapter-cloudflare exposes the Pages/Worker env on `platform.env` (typed in app.d.ts). In dev /
	// when unset, the vars are absent — the worker still mints a state + redirects on the AUTHORIZE leg
	// (the live exchange needs the real OAuth App, which is the human-action checkpoint).
	const env = platform?.env ?? {};

	// WR-04: fail fast on the CALLBACK leg when credentials are absent. Without this we'd hand the
	// worker empty-string id/secret, it would POST blank credentials to GitHub's token endpoint (after
	// the state check passes), get a non-token response, and surface a generic 400 "OAuth exchange
	// failed" — masking a deployment misconfig (forgotten dashboard secret) as a user error and issuing
	// a pointless outbound request. A callback with `code` present but no secret is a SERVER misconfig:
	// answer 503 (Service Unavailable) instead. The authorize leg (no `code`) stays unaffected so the
	// state-mint + redirect still work in dev.
	const isCallback = new URL(request.url).searchParams.get('code') != null;
	if (isCallback && (!env.GITHUB_CLIENT_ID || !env.GITHUB_CLIENT_SECRET)) {
		return new Response('Gist sign-in is not configured.', { status: 503 });
	}

	return handleGistAuth(request, {
		GITHUB_CLIENT_ID: env.GITHUB_CLIENT_ID ?? '',
		GITHUB_CLIENT_SECRET: env.GITHUB_CLIENT_SECRET ?? '',
		// CR-02: pin the OAuth redirect origin to a server-known constant (not the request Host). Unset
		// in dev → the worker falls back to its DEFAULT_SITE_ORIGIN (localhost:5173).
		SITE_ORIGIN: env.SITE_ORIGIN
	});
};
