import { defineConfig } from 'vitest/config';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { svelteTesting } from '@testing-library/svelte/vite';
import { fileURLToPath } from 'node:url';

// Unit/component test runner (Nyquist Wave 0, extended in Plan 49-02 for skeuo components).
// Pure-logic modules: docs transform ([[link]] rewrite + slug), share encode/decode (fflate
// round-trip), gist-auth worker (state CSRF), theme.ts. Component tests (@testing-library/svelte)
// need the svelte compiler plugin + svelteTesting() (jsdom cleanup + browser-resolve condition)
// so `.svelte` imports compile under jsdom. Playwright E2E lives in playwright.config.ts
// (testDir 'tests'), excluded here so the two runners never collide.
//
// The `$lib` alias mirrors SvelteKit's so `.svelte.ts` modules that import `$lib/...` (e.g.
// share-controls.svelte.ts → $lib/share/gist) are unit-testable here (WR-06). SvelteKit injects this
// alias for the app build but vitest doesn't see svelte.config.js, so declare it explicitly.
export default defineConfig({
	plugins: [svelte(), svelteTesting()],
	resolve: {
		alias: {
			$lib: fileURLToPath(new URL('./src/lib', import.meta.url))
		}
	},
	test: {
		environment: 'jsdom',
		globals: true,
		include: ['src/**/*.{test,spec}.ts', 'workers/**/*.{test,spec}.ts'],
		exclude: ['tests/**', 'node_modules/**', '.svelte-kit/**']
	}
});
