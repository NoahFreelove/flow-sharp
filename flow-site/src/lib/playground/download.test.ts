import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { offerDownload, offerMidiDownload } from './download';

// WR-07 — offerDownload robustness. The MIDI download is the only export affordance, so it must:
//   - return true when the click dispatches (and revoke the object URL afterwards),
//   - return false (not silently no-op) when a.click() throws (sandboxed / policy-blocked context),
//   - return false when there is no <body> to mount the anchor,
//   - always revoke the object URL (no leak) even on a click failure.

describe('offerDownload (WR-07 robustness)', () => {
	let createSpy: ReturnType<typeof vi.fn>;
	let revokeSpy: ReturnType<typeof vi.fn>;

	beforeEach(() => {
		vi.useFakeTimers();
		createSpy = vi.fn(() => 'blob:mock-url');
		revokeSpy = vi.fn();
		// jsdom doesn't implement these — stub them.
		vi.stubGlobal('URL', Object.assign(URL, {
			createObjectURL: createSpy,
			revokeObjectURL: revokeSpy
		}));
		vi.spyOn(console, 'error').mockImplementation(() => {});
	});

	afterEach(() => {
		vi.runOnlyPendingTimers();
		vi.useRealTimers();
		vi.restoreAllMocks();
		vi.unstubAllGlobals();
	});

	it('returns true and revokes the URL on a successful click', () => {
		const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
		const ok = offerDownload(new Uint8Array([1, 2, 3]), 'flow.mid', 'audio/midi');
		expect(ok).toBe(true);
		expect(clickSpy).toHaveBeenCalledOnce();
		// The URL is revoked after the delay, not synchronously.
		expect(revokeSpy).not.toHaveBeenCalled();
		vi.runAllTimers();
		expect(revokeSpy).toHaveBeenCalledWith('blob:mock-url');
	});

	it('returns false (and still revokes) when a.click() throws', () => {
		vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {
			throw new Error('blocked by sandbox');
		});
		const ok = offerDownload(new Uint8Array([1, 2, 3]), 'flow.mid', 'audio/midi');
		expect(ok).toBe(false);
		// No leak: the blob URL is still revoked.
		vi.runAllTimers();
		expect(revokeSpy).toHaveBeenCalledWith('blob:mock-url');
	});

	it('returns false when there is no <body> to mount the anchor', () => {
		const realBody = document.body;
		Object.defineProperty(document, 'body', { value: null, configurable: true });
		try {
			const ok = offerDownload(new Uint8Array([1]), 'flow.mid', 'audio/midi');
			expect(ok).toBe(false);
			// No URL was even created.
			expect(createSpy).not.toHaveBeenCalled();
		} finally {
			Object.defineProperty(document, 'body', { value: realBody, configurable: true });
		}
	});

	it('offerMidiDownload forwards the result', () => {
		vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
		expect(offerMidiDownload(new Uint8Array([0x4d, 0x54, 0x68, 0x64]))).toBe(true);
		vi.runAllTimers();
	});
});
