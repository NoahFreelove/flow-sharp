---
phase: 260701-vqu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-site/src/lib/design/tokens.css
  - flow-site/src/routes/docs/[slug]/+page.svelte
  - flow-site/src/lib/components/SiteToolbar.svelte
  - flow-site/tests/responsive.spec.ts
autonomous: true
requirements: [REQ-SITE-RESPONSIVE-01]

must_haves:
  truths:
    - "On a 1280x500 desktop viewport the docs sidebar (.docs-sidebar) is internally scrollable: scrollHeight > clientHeight."
    - "The last sidebar nav link can be scrolled into view within the sidebar and clicked (navigates to a /docs/ page)."
    - "The pinned sidebar top clears the 58px sticky toolbar (top = toolbar-height + --space-4)."
    - "Mobile (<768px) sidebar behavior is unchanged (position: static, no max-height/overflow applied there)."
    - "Toolbar height is a single shared CSS custom property referenced by both SiteToolbar and the sidebar so the two files cannot drift."
  artifacts:
    - flow-site/src/lib/design/tokens.css
    - flow-site/src/routes/docs/[slug]/+page.svelte
    - flow-site/src/lib/components/SiteToolbar.svelte
    - flow-site/tests/responsive.spec.ts
  key_links:
    - ".docs-sidebar top/max-height arithmetic references --toolbar-height defined once in tokens.css :root"
    - "SiteToolbar .toolbar height and .docs-sidebar both consume var(--toolbar-height)"
---

<objective>
Fix the docs sidebar CSS bug in flow-site: on short desktop viewports (width >=768px, height below the nav's intrinsic height) the lower docs nav categories/links are unreachable because `.docs-sidebar` is `position: sticky` with no `max-height` and no `overflow-y` — a pinned sticky element cannot scroll its own content. Add a regression Playwright test.

Purpose: Composers on short/laptop desktop windows can currently never reach the bottom docs nav links. This makes lower-category pages undiscoverable from the sidebar.
Output: A scrollable pinned sidebar that clears the sticky toolbar, a shared `--toolbar-height` token so the two files can't drift, and a focused Playwright regression guard.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md

# The bug is already fully diagnosed — do NOT re-diagnose. Verify line numbers, implement.
# flow-site/ is greenfield TS/Svelte/Tailwind-v4 web code — the repo-root C# conventions DO NOT apply here.
# Work ONLY inside flow-site/. Do NOT touch flow-lang/, wiki/, or run any dotnet command (a concurrent C# build is running).
@flow-site/src/routes/docs/[slug]/+page.svelte
@flow-site/src/lib/components/SiteToolbar.svelte
@flow-site/src/lib/design/tokens.css
@flow-site/tests/responsive.spec.ts
</context>

<tasks>

<task type="auto">
  <name>Task 1: Hoist a shared --toolbar-height token and make the pinned docs sidebar scroll its own content</name>
  <files>flow-site/src/lib/design/tokens.css, flow-site/src/routes/docs/[slug]/+page.svelte, flow-site/src/lib/components/SiteToolbar.svelte</files>
  <action>
Three coordinated CSS edits — introduce one shared token, then reference it from both consumers.

(1) In flow-site/src/lib/design/tokens.css, inside the `:root` block, add a new theme-independent custom property `--toolbar-height: 58px;`. Place it near the spacing scale (the block starting at `--space-1`, around lines 74-82) since it is a layout dimension, NOT a color — do NOT add it under the `[data-theme='dark']` block (it is theme-independent, matching how the spacing/type/shadow/radius/motion tokens are declared once per the file's header comment). 58px is the current literal height of the sticky toolbar.

(2) In flow-site/src/lib/components/SiteToolbar.svelte, in the `.toolbar` rule (around line 66), change `height: 58px;` to `height: var(--toolbar-height, 58px);`. Keep the fallback `58px` so the component stays self-contained (it declares its other tokens locally by design — see the component's top comment); the `:root` token from tokens.css is what actually applies in the app and is the single source of truth, the fallback only guards the token-absent case. Do NOT touch the local `--aqua-*` / `--metal-edge` / `--font-ui` declarations.

(3) In flow-site/src/routes/docs/[slug]/+page.svelte, in the DESKTOP `.docs-sidebar` rule (around lines 70-78), make three changes and add NOTHING else:
  - change `top: var(--space-4);` to `top: calc(var(--toolbar-height, 58px) + var(--space-4));` so the pinned sidebar clears the sticky toolbar
  - add `max-height: calc(100dvh - var(--toolbar-height, 58px) - 2 * var(--space-4));`
  - add `overflow-y: auto;`
  Keep the existing `padding`, `border-radius`, and `min-width: 0` declarations and the existing explanatory comment. Arithmetic sanity: top + max-height = (58 + 16) + (100dvh - 58 - 32) = 100dvh - 16 = 100dvh - var(--space-4), so the sidebar fits within the viewport leaving a bottom gap equal to --space-4. Use `100dvh` (not `100vh`) as instructed. Do NOT touch the `@media (max-width: 767px)` block (lines ~313-327) where `.docs-sidebar { position: static; }` — mobile must stay static with no max-height/overflow, so those desktop declarations simply do not apply there. Do NOT restructure the grid, add scroll containers elsewhere, or alter the skeuomorphic wood-panel styling.
  </action>
  <verify>
    <automated>cd flow-site && node -e "const fs=require('fs'); const t=fs.readFileSync('src/lib/design/tokens.css','utf8'); const s=fs.readFileSync('src/routes/docs/[slug]/+page.svelte','utf8'); const b=fs.readFileSync('src/lib/components/SiteToolbar.svelte','utf8'); const ok = /--toolbar-height:\s*58px/.test(t) && /var\(--toolbar-height/.test(b) && /max-height:\s*calc\(100dvh/.test(s) && /overflow-y:\s*auto/.test(s) && /top:\s*calc\(var\(--toolbar-height/.test(s); if(!ok){console.error('missing expected declarations'); process.exit(1);} console.log('css-ok');"</automated>
  </verify>
  <done>tokens.css defines `--toolbar-height: 58px` once in `:root`; SiteToolbar `.toolbar` height and the desktop `.docs-sidebar` rule both reference `var(--toolbar-height, 58px)`; `.docs-sidebar` desktop rule now has `top: calc(...)`, `max-height: calc(100dvh - ...)`, and `overflow-y: auto`; the mobile `@media (max-width: 767px)` block is unchanged.</done>
</task>

<task type="auto">
  <name>Task 2: Add a Playwright regression test proving the short-desktop sidebar is scrollable and its last link reachable</name>
  <files>flow-site/tests/responsive.spec.ts</files>
  <action>
Append a new `test.describe` block to flow-site/tests/responsive.spec.ts (do NOT modify the existing describe blocks). Follow the existing patterns in this file and its sibling tests/docs-render.spec.ts: reuse the imported `test`, `expect`, and `type Page` already at the top; gate to the desktop project only with an early return `if (testInfo.project.name !== 'desktop') return;` (the exact convention docs-render.spec.ts uses — desktop is the only project with the two-column sidebar), so the test does not run redundantly under the mobile/mobile-narrow projects. The test steps:
  - Set a short desktop viewport with `page.setViewportSize({ width: 1280, height: 500 })`.
  - Navigate to `/docs/flow-operator` (the docs slug already used elsewhere in this spec's ROUTES and by the mobile disclosure test) and await `page.locator('main').waitFor()`.
  - Locate the sidebar via `page.locator('.docs-sidebar')` and `await expect(sidebar).toBeVisible()`.
  - Assert the sidebar overflows its own box (proves max-height + overflow-y are in effect): read `scrollHeight` and `clientHeight` via `sidebar.evaluate((el) => ({ scrollHeight: el.scrollHeight, clientHeight: el.clientHeight }))` and `expect(scrollHeight).toBeGreaterThan(clientHeight)` with a descriptive message. Before the fix this assertion FAILS (no max-height => content is never clipped => scrollHeight == clientHeight), which is the regression this test guards.
  - Assert the LAST nav link is reachable: `const lastLink = page.locator('.docs-cat__list a').last();` then `await lastLink.scrollIntoViewIfNeeded();` (Playwright scrolls the nearest overflow-y:auto ancestor — the sidebar), `await expect(lastLink).toBeVisible();`, then `await lastLink.click();` and `await expect(page).toHaveURL(/\/docs\//);` (the click is the load-bearing reachability proof — Playwright fails the click if the link cannot be scrolled into view or is obscured).
Give the describe block and test descriptive names that reference the unreachable-nav regression (e.g. describe "docs sidebar reachability on short desktop viewport (unreachable-nav regression, REQ-SITE-RESPONSIVE-01)"). Add a short comment above the block explaining the short-viewport bug it guards, matching the file's existing comment style.
  </action>
  <verify>
    <automated>cd flow-site && node -e "const fs=require('fs'); const s=fs.readFileSync('tests/responsive.spec.ts','utf8'); const ok = /setViewportSize\(\{\s*width:\s*1280,\s*height:\s*500/.test(s) && /project\.name\s*!==\s*'desktop'/.test(s) && /\.docs-sidebar/.test(s) && /scrollHeight/.test(s) && /docs-cat__list a/.test(s); if(!ok){console.error('test scaffold missing expected assertions'); process.exit(1);} console.log('test-scaffold-ok');"</automated>
  </verify>
  <done>A new desktop-gated `test.describe` block exists in responsive.spec.ts that sets a 1280x500 viewport, asserts `.docs-sidebar` scrollHeight > clientHeight, and confirms the last `.docs-cat__list a` link scrolls into view and click-navigates to a /docs/ page. Existing describe blocks are untouched.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (none new) | CSS-only layout change plus a Playwright test. No new input handling, no network, no data crossing a trust boundary. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-vqu-01 | Information Disclosure | `.docs-sidebar` overflow scroll region | low | accept | Sidebar only renders the public docs nav (already in the DOM); making it scrollable exposes no new data. No mitigation needed. |
| T-vqu-SC | Tampering | package installs | low | accept | Zero package-manager installs in this plan (CSS + existing test deps only). No supply-chain surface. |
</threat_model>

<verification>
Run from the repo root. Do NOT run any `dotnet` command; work stays inside flow-site/.

1. Unit/component suite stays green (unrelated but must not regress):
   `pnpm -C flow-site test`

2. Build the site so the preview server (playwright webServer runs `pnpm preview`) serves the updated CSS:
   `pnpm -C flow-site build`

3. If Playwright's chromium is not installed, install chromium only (not all browsers):
   `pnpm -C flow-site exec playwright install chromium`

4. New + existing responsive specs pass (includes the new regression test):
   `pnpm -C flow-site exec playwright test tests/responsive.spec.ts`

5. Docs render baseline still passes at the normal 1280x800 layout (confirms no desktop regression):
   `pnpm -C flow-site exec playwright test tests/docs-render.spec.ts`
</verification>

<success_criteria>
- `--toolbar-height: 58px` is defined exactly once, in tokens.css `:root`, and consumed by both SiteToolbar `.toolbar` and the desktop `.docs-sidebar` rule.
- The desktop `.docs-sidebar` rule has `top: calc(var(--toolbar-height, 58px) + var(--space-4))`, `max-height: calc(100dvh - var(--toolbar-height, 58px) - 2 * var(--space-4))`, and `overflow-y: auto`.
- The mobile `@media (max-width: 767px)` sidebar behavior (`position: static`) is unchanged.
- The new Playwright test fails against the pre-fix CSS and passes against the fixed CSS; `tests/responsive.spec.ts` and `tests/docs-render.spec.ts` both pass.
- `pnpm -C flow-site test` stays green.
</success_criteria>

<output>
Create `.planning/quick/260701-vqu-fix-docs-sidebar-css-bug-unreachable-nav/260701-vqu-SUMMARY.md` when done.
</output>
