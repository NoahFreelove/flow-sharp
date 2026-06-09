// Share + Save-to-gist controls — Svelte 5 runes (D-49-23). A single `ShareControls` instance owns
// the toast/confirmation state the playground left rail renders, plus the two actions:
//   - shareLink(): encode the editor value → copy `/playground#code=...` → "Link copied" toast.
//   - saveToGist(): use the sessionStorage token if present, else start the OAuth flow; on a token,
//     createGist → "Saved to gist.github.com/<user>/<id>" + a Copy-link affordance.
// Copy is UI-SPEC §Copywriting verbatim. Echoed gist URLs / errors render as ESCAPED Svelte text
// (the page uses curly-expr interpolation, never {@html}) — T-49-XSS-SHARE.

import { encode } from '$lib/share/encode';
import { createGist, getGistToken, setGistToken, clearGistToken, beginGistAuth } from '$lib/share/gist';

export type ShareToastKind = 'info' | 'success' | 'error';

export interface ShareToast {
	kind: ShareToastKind;
	message: string;
	/** A copy-able link shown beside the toast (the share URL or the saved gist URL). */
	copyLink?: string;
}

export class ShareControls {
	/** The current toast (null when nothing to show). Rendered as escaped text by the page. */
	toast = $state<ShareToast | null>(null);
	/** True while the gist save is in flight (disables the button + shows "Saving…"). */
	saving = $state(false);

	/** Build the shareable `/playground#code=...` URL for the given source. */
	buildShareUrl(source: string): string {
		const origin =
			typeof window !== 'undefined' && window.location ? window.location.origin : '';
		return `${origin}/playground#code=${encode(source)}`;
	}

	/** Copy text to the clipboard, tolerating the absence of the async clipboard API. */
	private async copy(text: string): Promise<boolean> {
		try {
			if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
				await navigator.clipboard.writeText(text);
				return true;
			}
		} catch {
			// fall through — the toast still shows the link so the user can copy it manually
		}
		return false;
	}

	/** Encode the editor value, copy the share link, and toast the UI-SPEC confirmation. */
	async shareLink(source: string): Promise<void> {
		const url = this.buildShareUrl(source);
		await this.copy(url);
		// UI-SPEC §Copywriting: "Link copied — anyone can open this snippet".
		this.toast = {
			kind: 'success',
			message: 'Link copied — anyone can open this snippet',
			copyLink: url
		};
	}

	/** Copy the link currently attached to the toast (the "Copy link" affordance). */
	async copyToastLink(): Promise<void> {
		if (this.toast?.copyLink) await this.copy(this.toast.copyLink);
	}

	/** Dismiss the toast. */
	dismiss(): void {
		this.toast = null;
	}

	/**
	 * Save the current source as a gist. If no token is cached, kick off the OAuth flow (the page
	 * re-runs this after the token round-trip lands). On success, toast the gist URL + Copy link.
	 */
	async saveToGist(source: string): Promise<void> {
		const token = getGistToken();
		if (!token) {
			// No session token — start OAuth. The worker redirects back with #token=…; the page reads
			// it into sessionStorage and the composer presses Save again (or we auto-resume — page logic).
			this.toast = { kind: 'info', message: 'Opening GitHub sign-in to save your snippet…' };
			beginGistAuth();
			return;
		}

		this.saving = true;
		try {
			const gist = await createGist(source, token);
			// UI-SPEC §Copywriting: "Saved to gist.github.com/{user}/{id}" + Copy link.
			const shown = gist.htmlUrl.replace(/^https?:\/\//, '');
			this.toast = { kind: 'success', message: `Saved to ${shown}`, copyLink: gist.htmlUrl };
		} catch (e) {
			const message = e instanceof Error ? e.message : 'Couldn’t save to gist.';
			// On a 401 the token is dead — clear it so the next Save re-auths cleanly.
			if (message.includes('expired')) clearGistToken();
			this.toast = { kind: 'error', message };
		} finally {
			this.saving = false;
		}
	}
}

/**
 * GitHub OAuth/PAT token prefixes. A user-to-server OAuth token (what the gist worker mints) is `gho_`;
 * the others are accepted defensively in case GitHub rotates the OAuth token format. A bare random
 * string from an arbitrary `#token=` navigation will NOT match — closing the token-injection path.
 */
const GITHUB_TOKEN_PREFIXES = ['gho_', 'ghu_', 'ghp_', 'ghs_', 'ghr_'];

/**
 * True only for a string SHAPED like a GitHub token (WR-06). Validates the known prefix plus a sane
 * length/charset bound. This is a shape gate, not authentication — an attacker-chosen string that
 * happens to be prefix-valid still only yields a 401 from api.github.com — but it stops an arbitrary
 * `…/playground#token=ATTACKER` navigation (link / redirect / iframe) from seeding sessionStorage
 * with junk, which (paired with CR-02) was the second half of a token-injection chain.
 */
function isGitHubTokenShape(token: string): boolean {
	if (!GITHUB_TOKEN_PREFIXES.some((p) => token.startsWith(p))) return false;
	// GitHub tokens are prefix + base62 body; require a reasonable length and a strict charset.
	return token.length >= 20 && token.length <= 255 && /^[A-Za-z0-9_]+$/.test(token);
}

/**
 * Read an OAuth `#token=...` fragment (the worker's same-origin redirect target) into sessionStorage
 * and return true if a SHAPE-VALID token was found. The page calls this on mount, then cleans the URL
 * so the token never lingers in the address bar / history.
 *
 * WR-06: the fragment is attacker-influenceable (any link/redirect/iframe can put a value there), so
 * the value is shape-validated (`gho_`-style prefix + charset/length) before it is ever stored. A
 * non-token `#token=` value is ignored rather than cached as a bogus credential.
 */
export function captureOAuthToken(): boolean {
	if (typeof window === 'undefined') return false;
	const hash = window.location.hash;
	if (!hash.startsWith('#token=')) return false;
	try {
		const token = decodeURIComponent(hash.slice('#token='.length));
		if (token && isGitHubTokenShape(token)) {
			setGistToken(token);
			return true;
		}
	} catch {
		// malformed fragment — ignore (no token cached)
	}
	return false;
}
