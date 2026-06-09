import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import ErrorBox from './__fixtures__/ErrorBox.svelte';
import type { RunError } from '$lib/runtime';

// WR-05 — RunResult.errors[] is UNTRUSTED (derived from user-supplied Flow source: a parse error
// echoes tokens straight from the source). The playground renders these via Svelte curly
// interpolation, which auto-escapes. This pins that contract: a parse error whose message /
// sourceSnippet carries a `<script>` payload must render as ESCAPED TEXT, never live HTML.
//
// The fixture (__fixtures__/ErrorBox.svelte) mirrors the exact +page.svelte error-box markup so this
// runs WITHOUT booting Monaco + the WASM runtime. If a sink here ever became {@html}, the
// "no injected <script> element" assertion goes red.

describe('playground error box escapes untrusted RunError text (WR-05)', () => {
	afterEach(() => cleanup());

	const xssErr: RunError = {
		kind: 'parse',
		message: 'unexpected token <script>alert(1)</script>',
		sourceSnippet: '(<img src=x onerror=alert(1)>)'
	};

	it('does NOT inject a live <script> element from the error message', () => {
		const { container } = render(ErrorBox, { props: { errors: [xssErr] } });
		// If the message were routed through {@html}, this would parse a real <script> node.
		expect(container.querySelectorAll('script')).toHaveLength(0);
		// The onerror-bearing snippet must not have produced a live <img> either.
		expect(container.querySelectorAll('img')).toHaveLength(0);
	});

	it('renders the angle-bracketed payload as visible TEXT (escaped, not stripped)', () => {
		const { getByTestId } = render(ErrorBox, { props: { errors: [xssErr] } });
		const region = getByTestId('errors');
		// textContent carries the literal markup characters — proof they were escaped, not executed.
		expect(region.textContent).toContain('<script>alert(1)</script>');
		expect(region.textContent).toContain('<img src=x onerror=alert(1)>');
	});

	it('the rendered HTML entity-encodes the angle brackets', () => {
		const { getByTestId } = render(ErrorBox, { props: { errors: [xssErr] } });
		const region = getByTestId('errors');
		// Svelte auto-escaping emits &lt;/&gt; in the serialized HTML, never raw <script>.
		expect(region.innerHTML).toContain('&lt;script&gt;');
		expect(region.innerHTML).not.toContain('<script>alert(1)</script>');
	});
});
