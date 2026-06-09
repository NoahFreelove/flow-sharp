import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

// Tailwind v4 is CSS-first: the @tailwindcss/vite plugin replaces the v3 PostCSS path,
// so there is NO tailwind.config.js (design tokens go in src/app.css @theme — Plan 49-02).
//
// Monaco self-hosts (Plan 49-05): the `monaco-editor` ESM build + its base editor.worker
// (imported with `?worker` in src/lib/monaco/index.ts) are bundled through Vite — NO CDN, so
// the _headers CSP stays tight (script-src 'self' 'wasm-unsafe-eval'). `optimizeDeps.include`
// pre-bundles the large Monaco entry so the first onMount dynamic-import is fast; the worker is
// excluded from the optimizer so the `?worker` suffix resolves to a standalone module worker
// (Firefox needs `type: 'module'`, which `?worker` provides via worker.format = 'es').
//
// NOTE: the Phase 48 `flow-runtime.js` AppBundle under static/wasm/ is NOT processed by Vite —
// it is dynamic-imported with `@vite-ignore` (src/lib/runtime.ts) and self-loads its own
// `./_framework/dotnet.js`. Keep it opaque to the bundler (HANDOFF Pitfall 2).
export default defineConfig({
	plugins: [tailwindcss(), sveltekit()],
	optimizeDeps: {
		include: ['monaco-editor'],
		exclude: ['monaco-editor/esm/vs/editor/editor.worker']
	},
	worker: {
		format: 'es'
	}
});
