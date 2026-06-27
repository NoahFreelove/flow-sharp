import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import Knob from './Knob.svelte';
import Toggle from './Toggle.svelte';
import LedIndicator from './LedIndicator.svelte';
import Button from './Button.svelte';
import Tabs from './Tabs.svelte';
import { initThemeStore } from '../../design/theme-store.svelte';

// Component a11y contracts (UI-SPEC §Component Inventory) — pinned BEFORE the visual layer.

describe('<Knob> — role=slider keyboard model (UI-SPEC item 2, D-49-10)', () => {
	beforeEach(() => cleanup());

	function renderKnob(props = {}) {
		return render(Knob, {
			props: { label: 'Volume', min: 0, max: 100, value: 50, step: 1, largeStep: 10, ...props }
		});
	}

	it('exposes the mandatory ARIA slider attributes', () => {
		const { getByRole } = renderKnob();
		const slider = getByRole('slider');
		expect(slider.getAttribute('aria-label')).toBe('Volume');
		expect(slider.getAttribute('aria-valuemin')).toBe('0');
		expect(slider.getAttribute('aria-valuemax')).toBe('100');
		expect(slider.getAttribute('aria-valuenow')).toBe('50');
		expect(slider.getAttribute('aria-valuetext')).toContain('50');
		expect(slider.getAttribute('tabindex')).toBe('0');
	});

	it('ArrowUp / ArrowRight increment by step', async () => {
		const { getByRole } = renderKnob();
		const slider = getByRole('slider');
		await fireEvent.keyDown(slider, { key: 'ArrowUp' });
		expect(slider.getAttribute('aria-valuenow')).toBe('51');
		await fireEvent.keyDown(slider, { key: 'ArrowRight' });
		expect(slider.getAttribute('aria-valuenow')).toBe('52');
	});

	it('ArrowDown / ArrowLeft decrement by step', async () => {
		const { getByRole } = renderKnob();
		const slider = getByRole('slider');
		await fireEvent.keyDown(slider, { key: 'ArrowDown' });
		expect(slider.getAttribute('aria-valuenow')).toBe('49');
		await fireEvent.keyDown(slider, { key: 'ArrowLeft' });
		expect(slider.getAttribute('aria-valuenow')).toBe('48');
	});

	it('Home → min, End → max', async () => {
		const { getByRole } = renderKnob();
		const slider = getByRole('slider');
		await fireEvent.keyDown(slider, { key: 'Home' });
		expect(slider.getAttribute('aria-valuenow')).toBe('0');
		await fireEvent.keyDown(slider, { key: 'End' });
		expect(slider.getAttribute('aria-valuenow')).toBe('100');
	});

	it('PageUp / PageDown move by the large step', async () => {
		const { getByRole } = renderKnob();
		const slider = getByRole('slider');
		await fireEvent.keyDown(slider, { key: 'PageUp' });
		expect(slider.getAttribute('aria-valuenow')).toBe('60');
		await fireEvent.keyDown(slider, { key: 'PageDown' });
		expect(slider.getAttribute('aria-valuenow')).toBe('50');
	});

	it('clamps at min and max', async () => {
		const { getByRole } = renderKnob({ value: 1 });
		const slider = getByRole('slider');
		await fireEvent.keyDown(slider, { key: 'ArrowDown' });
		await fireEvent.keyDown(slider, { key: 'ArrowDown' });
		expect(slider.getAttribute('aria-valuenow')).toBe('0');
	});

	it('aria-valuetext includes the unit when provided', () => {
		const { getByRole } = renderKnob({ unit: 'dB' });
		expect(getByRole('slider').getAttribute('aria-valuetext')).toContain('dB');
	});
});

describe('<Toggle> — role=switch (UI-SPEC item 3)', () => {
	beforeEach(() => {
		cleanup();
		localStorage.clear();
		document.documentElement.removeAttribute('data-theme');
		// Reset the shared theme rune so theme-mode toggle tests start from a known 'light' state
		// (the module-level $state persists across tests in this file).
		initThemeStore('light');
	});

	it('renders role=switch with aria-checked reflecting state', () => {
		const { getByRole } = render(Toggle, { props: { checked: false, label: 'Dark mode' } });
		const sw = getByRole('switch');
		expect(sw.getAttribute('aria-checked')).toBe('false');
		expect(sw.getAttribute('aria-label')).toBe('Dark mode');
	});

	it('flips aria-checked on click', async () => {
		const { getByRole } = render(Toggle, { props: { checked: false, label: 'Dark mode' } });
		const sw = getByRole('switch');
		await fireEvent.click(sw);
		expect(sw.getAttribute('aria-checked')).toBe('true');
	});

	it('the theme variant writes localStorage and sets [data-theme]', async () => {
		window.matchMedia = vi.fn().mockReturnValue({ matches: false }) as unknown as typeof matchMedia;
		const { getByRole } = render(Toggle, { props: { theme: true, label: 'Dark mode' } });
		const sw = getByRole('switch');
		await fireEvent.click(sw);
		expect(localStorage.getItem('flow-theme')).toBe('dark');
		expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
	});

	it('WR-03: two theme toggles stay in lockstep via the shared store', async () => {
		window.matchMedia = vi.fn().mockReturnValue({ matches: false }) as unknown as typeof matchMedia;
		// Two independent theme toggles (mirrors site chrome + playground rail).
		const a = render(Toggle, { props: { theme: true, label: 'A' } });
		const b = render(Toggle, { props: { theme: true, label: 'B' } });
		const swA = a.getByLabelText('A');
		const swB = b.getByLabelText('B');
		// Both start off (light).
		expect(swA.getAttribute('aria-checked')).toBe('false');
		expect(swB.getAttribute('aria-checked')).toBe('false');
		// Flipping ONE updates BOTH (the previous per-instance $state desynced them).
		await fireEvent.click(swA);
		expect(swA.getAttribute('aria-checked')).toBe('true');
		expect(swB.getAttribute('aria-checked')).toBe('true');
		// Flipping the OTHER flips both back.
		await fireEvent.click(swB);
		expect(swA.getAttribute('aria-checked')).toBe('false');
		expect(swB.getAttribute('aria-checked')).toBe('false');
	});

	it('Space / Enter operate the switch', async () => {
		const { getByRole } = render(Toggle, { props: { checked: false, label: 'On' } });
		const sw = getByRole('switch');
		await fireEvent.keyDown(sw, { key: ' ' });
		expect(sw.getAttribute('aria-checked')).toBe('true');
		await fireEvent.keyDown(sw, { key: 'Enter' });
		expect(sw.getAttribute('aria-checked')).toBe('false');
	});
});

describe('<LedIndicator> — status not by colour alone (UI-SPEC item 6, D-49-10)', () => {
	beforeEach(() => cleanup());

	it('mirrors state in a visually-hidden aria-live region', () => {
		const { container } = render(LedIndicator, { props: { state: 'rendering' } });
		const live = container.querySelector('[aria-live="polite"]');
		expect(live).not.toBeNull();
		expect(live?.textContent?.toLowerCase()).toContain('render');
	});

	it('the LED lens node itself is aria-hidden', () => {
		const { container } = render(LedIndicator, { props: { state: 'playing' } });
		expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull();
		const live = container.querySelector('[aria-live="polite"]');
		expect(live?.textContent?.toLowerCase()).toContain('play');
	});

	it('error state announces an error', () => {
		const { container } = render(LedIndicator, { props: { state: 'error' } });
		const live = container.querySelector('[aria-live="polite"]');
		expect(live?.textContent?.toLowerCase()).toContain('error');
	});
});

describe('<Button> — real button + a11y (UI-SPEC item 1)', () => {
	beforeEach(() => cleanup());

	it('renders a real <button> and fires onclick', async () => {
		const onclick = vi.fn();
		const { getByRole } = render(Button, { props: { onclick, children: undefined } });
		const btn = getByRole('button');
		expect(btn.tagName).toBe('BUTTON');
		await fireEvent.click(btn);
		expect(onclick).toHaveBeenCalledOnce();
	});

	it('icon variant carries the provided aria-label', () => {
		const { getByRole } = render(Button, { props: { variant: 'icon', label: 'Copy' } });
		expect(getByRole('button').getAttribute('aria-label')).toBe('Copy');
	});

	it('disabled sets aria-disabled and the disabled attribute', () => {
		const { getByRole } = render(Button, { props: { disabled: true, label: 'Run' } });
		const btn = getByRole('button') as HTMLButtonElement;
		expect(btn.disabled).toBe(true);
		expect(btn.getAttribute('aria-disabled')).toBe('true');
	});
});

describe('<Tabs> — nav with aria-current + external safety (UI-SPEC item 8)', () => {
	beforeEach(() => cleanup());

	const items = [
		{ label: 'Home', href: '/' },
		{ label: 'Docs', href: '/docs' },
		{ label: 'Playground', href: '/playground' },
		{ label: 'Showcase', href: '/showcase' },
		{ label: 'GitHub', href: 'https://github.com/x/y', external: true }
	];

	it('marks the active route with aria-current="page"', () => {
		const { getByText } = render(Tabs, { props: { items, current: '/docs' } });
		expect(getByText('Docs').closest('a')?.getAttribute('aria-current')).toBe('page');
		expect(getByText('Home').closest('a')?.getAttribute('aria-current')).toBeNull();
	});

	it('the external GitHub tab opens in a new tab safely', () => {
		const { getByText } = render(Tabs, { props: { items, current: '/' } });
		const gh = getByText('GitHub').closest('a');
		expect(gh?.getAttribute('target')).toBe('_blank');
		expect(gh?.getAttribute('rel')).toContain('noopener');
		expect(gh?.getAttribute('rel')).toContain('noreferrer');
	});

	it('announces "(opens in new tab)" for the external tab', () => {
		const { getByText } = render(Tabs, { props: { items, current: '/' } });
		expect(getByText(/opens in new tab/i)).toBeTruthy();
	});

	it('wraps the tabs in a labelled nav landmark', () => {
		const { container } = render(Tabs, { props: { items, current: '/' } });
		const nav = container.querySelector('nav');
		expect(nav?.getAttribute('aria-label')).toBe('Primary');
	});
});
