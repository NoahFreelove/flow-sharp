import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getInitialTheme, setTheme, toggleTheme, applyTheme } from './theme';

// theme.ts persistence contract (D-49-20): localStorage wins, else prefers-color-scheme.

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

	it('honours prefers-color-scheme: dark on first visit', () => {
		stubMatchMedia(true);
		expect(getInitialTheme()).toBe('dark');
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
