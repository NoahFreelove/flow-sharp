import { defineConfig, devices } from '@playwright/test';
import { existsSync } from 'node:fs';

// System-chromium fallback: Playwright's bundled chromium has no build for some newer Linux
// distros (e.g. ubuntu 26.04), and `playwright install chrome` needs sudo. When a system
// chromium is present we drive it via executablePath + --no-sandbox so the suite still runs.
// Override with PLAYWRIGHT_CHROMIUM_PATH; on CI (bundled browser available) leave both unset.
const SYSTEM_CHROMIUM =
	process.env.PLAYWRIGHT_CHROMIUM_PATH ??
	['/snap/bin/chromium', '/usr/bin/chromium', '/usr/bin/chromium-browser'].find((p) =>
		existsSync(p)
	);
const chromiumLaunch = SYSTEM_CHROMIUM
	? { executablePath: SYSTEM_CHROMIUM, args: ['--no-sandbox'] }
	: undefined;

// E2E / smoke runner (Nyquist Wave 0). Specs live in tests/ (separate from vitest's src/**).
// Three viewport projects per the Phase 49 responsive contract:
//   - desktop       1280×800  : the three-column playground + full nav
//   - mobile        375×667   : the <768px read-only-Monaco breakpoint (D-49-09)
//   - mobile-narrow 320×568   : the narrowest supported target (ROADMAP acceptance #7)
// webServer boots the production preview so prerendered routes + the committed WASM bundle
// are exercised exactly as CF Pages serves them.
export default defineConfig({
	testDir: 'tests',
	fullyParallel: true,
	forbidOnly: !!process.env.CI,
	retries: process.env.CI ? 2 : 0,
	reporter: process.env.CI ? 'github' : 'list',
	use: {
		baseURL: 'http://localhost:4173',
		trace: 'on-first-retry'
	},
	webServer: {
		command: 'pnpm preview --port 4173',
		port: 4173,
		reuseExistingServer: !process.env.CI,
		timeout: 120_000
	},
	projects: [
		{
			name: 'desktop',
			use: {
				...devices['Desktop Chrome'],
				viewport: { width: 1280, height: 800 },
				...(chromiumLaunch ? { launchOptions: chromiumLaunch } : {})
			}
		},
		{
			name: 'mobile',
			use: {
				...devices['Pixel 5'],
				viewport: { width: 375, height: 667 },
				...(chromiumLaunch ? { launchOptions: chromiumLaunch } : {})
			}
		},
		{
			name: 'mobile-narrow',
			use: {
				...devices['Pixel 5'],
				viewport: { width: 320, height: 568 },
				...(chromiumLaunch ? { launchOptions: chromiumLaunch } : {})
			}
		}
	]
});
