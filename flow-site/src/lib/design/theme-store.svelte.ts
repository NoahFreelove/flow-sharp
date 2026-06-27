/*
 * Shared theme rune — the SINGLE source of truth every <Toggle theme> reads (WR-03).
 *
 * Previously each theme Toggle kept its OWN `$state` checked value, initialised from a side-effecting
 * `getInitialTheme()` read inside `$effect`. With two live toggles (site chrome + playground rail),
 * flipping one never notified the other, so they visibly disagreed until a reload; and deriving
 * bindable state from a side-effecting read in `$effect` risked snapping `checked` back to the stored
 * value if any reactive read were ever added to that effect.
 *
 * Now a single module-level `$state` rune holds the current theme. Every Toggle reflects it via
 * `$derived(currentTheme() === 'dark')`, and `setTheme` (theme.ts) updates this rune — so all toggles
 * stay in lockstep and there is no $effect-init clobber hazard.
 *
 * This module deliberately imports NOTHING from `theme.ts` to avoid a cycle: `theme.ts` imports the
 * setter here and calls it. SSR-safe: the initial value is a plain `'light'` default; the real
 * resolved theme is hydrated once in the browser via `initThemeStore()`.
 */

import type { Theme } from './theme';

let theme = $state<Theme>('light');

/** Read the current shared theme (reactive — use inside components / $derived). */
export function currentTheme(): Theme {
	return theme;
}

/** Update the shared theme rune. Called by `setTheme` (theme.ts) so every Toggle re-derives. */
export function setThemeStore(next: Theme): void {
	theme = next;
}

/** Hydrate the store with the resolved initial theme once, in the browser. Idempotent-safe. */
export function initThemeStore(resolved: Theme): void {
	theme = resolved;
}
