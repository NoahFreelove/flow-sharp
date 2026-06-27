import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getInitialTheme, setTheme, toggleTheme, applyTheme } from './theme';

// theme.ts persistence contract (D-49-20, revised): a stored choice wins, otherwise default to
// LIGHT. The OS prefers-color-scheme is intentionally NOT honoured (dark is opt-in via the toggle).

describe('theme.ts persistence (D-49-20)', () => {
	// jsdom does not implement matchMedia — install a controllable stub per test.
	function stubMatchMedia(matches: boolean) {
		window.matchMedia = vi.fn().mockReturnValue({ matches }) as unknown as typeof matchMedia;
	}

	beforeEach(() => {
		localStorage.clear();
		document.documentElement.removeAttribute('data-theme');
		stubMatchMedia(false);
	});

	it('defaults to light when nothing is stored and OS is not dark', () => {
		stubMatchMedia(false);
		expect(getInitialTheme()).toBe('light');
	});

	it('defaults to light on first visit even when the OS prefers dark (dark is opt-in)', () => {
		stubMatchMedia(true);
		expect(getInitialTheme()).toBe('light');
	});

	it('a stored choice wins over the OS preference', () => {
		stubMatchMedia(true);
		localStorage.setItem('flow-theme', 'light');
		expect(getInitialTheme()).toBe('light');
	});

	it('setTheme writes localStorage and applies [data-theme]', () => {
		setTheme('dark');
		expect(localStorage.getItem('flow-theme')).toBe('dark');
		expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
	});

	it('toggleTheme flips and returns the new theme', () => {
		expect(toggleTheme('light')).toBe('dark');
		expect(localStorage.getItem('flow-theme')).toBe('dark');
		expect(toggleTheme('dark')).toBe('light');
	});

	it('applyTheme sets the attribute without persisting', () => {
		applyTheme('dark');
		expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
		expect(localStorage.getItem('flow-theme')).toBeNull();
	});
});
