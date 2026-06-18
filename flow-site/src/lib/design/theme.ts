/*
 * Theme persistence (D-49-20) — light is the default; dark is a SECOND skeuo theme.
 *
 * First visit honours `prefers-color-scheme`; an explicit toggle persists the choice in
 * localStorage and applies it as `[data-theme]` on <html> so the tokens.css dark-block
 * custom-property overrides take effect. Consumed by the <Toggle with-icons> theme switch.
 *
 * SSR-safe: every browser-only access is guarded so this module imports cleanly during
 * SvelteKit prerender (where window/localStorage/document don't exist).
 */

import { setThemeStore } from './theme-store.svelte';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'flow-theme';

/** True only in a browser context (guards SSR / prerender). */
function hasWindow(): boolean {
	return typeof window !== 'undefined';
}

/**
 * Resolve the initial theme: an explicit localStorage choice wins; otherwise default to LIGHT.
 *
 * Light is Flow's primary skeuo theme, so a first visit always opens light — the OS
 * `prefers-color-scheme: dark` is intentionally NOT honoured here (it was making the docs open
 * dark for dark-OS users who never asked for it). Dark is opt-in via the toggle, which persists.
 */
export function getInitialTheme(): Theme {
	if (!hasWindow()) return 'light';
	try {
		const stored = window.localStorage.getItem(STORAGE_KEY);
		if (stored === 'light' || stored === 'dark') return stored;
	} catch {
		/* localStorage may be unavailable (private mode / blocked) — fall through */
	}
	return 'light';
}

/** Apply a theme to the document root via `[data-theme]` (no persistence). Also syncs the shared
 *  theme rune so every <Toggle theme> re-derives in lockstep (WR-03). */
export function applyTheme(theme: Theme): void {
	// Sync the shared rune even on the server (cheap; keeps SSR + hydration consistent). The DOM
	// write below is browser-only.
	setThemeStore(theme);
	if (!hasWindow()) return;
	document.documentElement.setAttribute('data-theme', theme);
}

/** Persist a theme choice to localStorage AND apply it to the document root (+ shared rune). */
export function setTheme(theme: Theme): void {
	if (!hasWindow()) return;
	try {
		window.localStorage.setItem(STORAGE_KEY, theme);
	} catch {
		/* persistence best-effort — still apply the visual change below */
	}
	applyTheme(theme);
}

/** Toggle between light and dark, persisting + applying the result. Returns the new theme. */
export function toggleTheme(current: Theme): Theme {
	const next: Theme = current === 'dark' ? 'light' : 'dark';
	setTheme(next);
	return next;
}
