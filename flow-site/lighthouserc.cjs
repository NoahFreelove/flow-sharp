// Lighthouse CI gate (D-49-31, ROADMAP AC-6). Asserts Performance / Accessibility /
// Best-Practices / SEO each >= 0.9 ("≥90") across the three primary routes, UNCONDITIONALLY
// (no carve-out — locked D-49-31). /playground ships the Phase 48 WASM runtime, but the
// D-49-34 lazy-load fetches it only inside onMount (off the LCP path) so the above-fold shell
// never blocks on it — the planned mitigation that keeps /playground Performance ≥90.
//
// Uses .cjs because lhci expects CommonJS and package.json is "type":"module".
//
// SERVER: scripts/lh-serve.mjs (NOT `vite preview`, NOT staticDistDir). Two reasons:
//   1. /playground is client-only (prerender=false, ssr=false) — no playground.html exists in
//      the static cloudflare output, so lhci's bundled static server (and staticDistDir) 404 it.
//      lh-serve.mjs SPA-falls-back to index.html exactly as Cloudflare Pages does.
//   2. `vite preview` serves UNCOMPRESSED with no cache headers, which penalises the WASM-heavy
//      /playground Performance by ~11 points as a pure dev-server artifact. Cloudflare Pages
//      brotli-compresses + long-cache every asset; lh-serve.mjs mimics that so the gate measures
//      the PRODUCTION condition, not a straw man. (See lh-serve.mjs header for the full rationale.)
//
// FORM FACTOR: covered for BOTH mobile + desktop via the LHCI_FORM_FACTOR env var. The
// `lh:audit` script runs lhci twice (mobile, then desktop) so the four-axis ≥0.9 bar is proven
// on each. Default is mobile (Lighthouse's own default + the harder Performance target).
//
// CHROME: CHROME_PATH is honored by Lighthouse; `--no-sandbox` is required for snap-confined /
// sandboxless Chrome in some Linux envs and is harmless on CI's real Chrome.
//
// USAGE: `pnpm build` first (lh-serve.mjs serves .svelte-kit/cloudflare), then `pnpm lh:audit`.
const formFactor = process.env.LHCI_FORM_FACTOR === 'desktop' ? 'desktop' : 'mobile';

const desktopSettings = {
	formFactor: 'desktop',
	screenEmulation: { mobile: false, width: 1350, height: 940, deviceScaleFactor: 1, disabled: false },
	throttling: {
		rttMs: 40,
		throughputKbps: 10 * 1024,
		cpuSlowdownMultiplier: 1,
		requestLatencyMs: 0,
		downloadThroughputKbps: 0,
		uploadThroughputKbps: 0
	}
};

module.exports = {
	ci: {
		collect: {
			startServerCommand: 'node scripts/lh-serve.mjs',
			startServerReadyPattern: 'lh-serve ready',
			startServerReadyTimeout: 30000,
			url: [
				'http://localhost:4182/',
				'http://localhost:4182/docs',
				'http://localhost:4182/playground'
			],
			// 3 runs → lhci asserts against the MEDIAN, which absorbs single-run FCP/LCP timing
			// spikes from CPU contention on loaded CI machines (a 1-run gate is too flaky for the
			// WASM-heavy /playground route). The four-axis ≥0.9 bar holds on the median.
			numberOfRuns: 3,
			settings: {
				chromeFlags: '--no-sandbox --headless=new',
				...(formFactor === 'desktop' ? desktopSettings : {})
			}
		},
		assert: {
			assertions: {
				'categories:performance': ['error', { minScore: 0.9 }],
				'categories:accessibility': ['error', { minScore: 0.9 }],
				'categories:best-practices': ['error', { minScore: 0.9 }],
				'categories:seo': ['error', { minScore: 0.9 }]
			}
		},
		upload: {
			target: 'temporary-public-storage'
		}
	}
};
