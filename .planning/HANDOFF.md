# Handoff — flowlang.dev deploy + flow-site polish (2026-06-12)

Branch: `dev`. Working in `flow-site/` (greenfield SvelteKit — TS/pnpm conventions, NOT the repo's C# rules). Dev server convention: `pnpm -C flow-site dev` → `http://localhost:5173`. Tests: `pnpm -C flow-site test` (vitest), `pnpm -C flow-site exec playwright test <spec>` (e2e; webServer = `pnpm preview` on :4173 — **it serves the BUILT output, so `pnpm -C flow-site build` first or it tests stale code**).

## What happened this session (all DONE)

1. **v1.5 milestone closed + tagged `v1.5.0`** (local, NOT pushed). Archived to `.planning/milestones/v1.5-*`; `.planning/v1.5-MILESTONE-AUDIT.md` (status tech_debt, 0 unsatisfied). REQUIREMENTS.md removed (fresh for v1.6). Next milestone not started.
2. **flowlang.dev deployed to Cloudflare Pages** — project `flow-music`, LIVE at `https://flow-music.pages.dev`. Deployed via Wrangler CLI (`wrangler login` done this session; account = user's). `wrangler.toml` name → `flow-music` (matches `SITE_ORIGIN`). **Deploy model = MANUAL wrangler upload** (no git-integration yet): `cd flow-site && pnpm build && pnpm exec wrangler pages deploy .svelte-kit/cloudflare --branch main --commit-dirty=true`.
3. **flow-site visual pass — committed `7083199`** (+ earlier bottom-bar fix `83eadb5`, quick-task `260611-sqk`):
   - Removed home's iOS-6 **bottom tab bar**; single top nav site-wide.
   - **Dark-mode legibility**: new theme-stable `--color-on-chrome` token replaced 18 `color:var(--color-paper)` text-on-dark-chrome spots (panel headers were dark-on-dark). Light mode byte-identical.
   - **Shared `<SiteToolbar>`** (`flow-site/src/lib/components/SiteToolbar.svelte`): the iOS-6 brushed-aluminum bar (aqua ♪ glyph + "Flow" Helvetica Neue Bold wordmark + pill nav) now renders on every non-home route via `+layout.svelte`; old `.site-chrome`/Tabs/hamburger dropped. Home keeps its identical inline bar. **Nav no longer jumps.**
   - Theme **toggle removed from the light-only home** (decision A); lives only on dark-capable routes via SiteToolbar.
   - Home content got a viewport-scaling side buffer (`clamp(20px,4vw,48px)`).
   - Tests retargeted (nav/responsive/a11y/render-strategy/home-a11y); visual baselines regenerated. **vitest 132 + chrome playwright specs green.**

> **The live `flow-music.pages.dev` is the FIRST deploy — it does NOT yet have the visual pass.** Re-deploy (command above) to push the committed visual fixes live.

## Composer-confirmed design decisions (don't relitigate)

- Single top nav everywhere; **home stays light-only**, no toggle on home (decision A).
- The shared toolbar is **always light brushed-aluminum** even on dark routes (a "hardware bezel" over the dark "screen") — composer confirmed this looks good.
- Custom domain plan: **`flow.<godaddy-domain>.com`** later — keep DNS at GoDaddy, add ONE CNAME `flow → flow-music.pages.dev`, then CF dashboard → Custom domains. Do NOT move nameservers (apex is on Firebase). Not done yet.
- Cloudflare free tier; no billing. "Save to gist" OAuth + env vars deferred (default `#code=` share works without it).

## TODO — three new tasks from composer (NOT started)

### 1. Remove the "How it sounds" section + its prerendered audio
- Lives in `flow-site/src/routes/+page.svelte`: the `<!-- HOW IT SOUNDS -->` block (~line 263) + `<div class="h-rule"><h2>How it sounds</h2>…` + the leather-rack data (~line 65 `// Leather "How it sounds" rack — melodies/types`). Uses `flow-site/src/lib/home/AudioEmbed.svelte`.
- Remove: the section markup, its data/melodies, the AudioEmbed usage + the related interactive state (the `playingIndex` $state + reactive VU meters + Web-Audio `tones` play logic are tied to this section — remove the now-dead parts, watch for `home-a11y.test.ts` / `home-deeplinks.test.ts` assertions about the leather section / play buttons).
- **Audio assets:** check `flow-site/static/audio/`. Per `src/lib/showcase/pieces.ts:16`, `flow-showcase.wav` + `microtonal-ji.wav` are ALSO used by `/showcase` — **do NOT delete shared assets**; only remove home-only ones. "We'll do one proper demo later."
- After: `pnpm build` + run `home-a11y.test.ts`, `render-strategy.spec.ts`, `nav.spec.ts`; update any test asserting the section/audio.

### 2. Docs render markdown more faithfully (code vs text hard to tell apart)
- Pipeline: docs are mdsvex + server-rendered **shiki** code blocks (zero client JS), wrapped in `<figure class="docs-codeblock">` or bare `<pre>`. Styling lives in `flow-site/src/routes/docs/[slug]/+page.svelte` `<style>` — `.docs-prose :global(pre)` (~line 149), `.docs-body`/`.docs-prose`. Highlighter that injects the figure + "Open in playground" button: `flow-site/src/lib/docs/` (`.js` files — shiki/remark; see `shiki.ts` / `remark-*`).
- Composer wants clearer **code-vs-text differentiation**: e.g. a **box around code blocks** (distinct background + border + padding + radius — currently they blend into the `.surface-paper` body) and **bolder/larger headers** (`.docs-prose :global(h1/h2/h3)` — add weight/size, maybe a bottom hairline). Mostly a CSS change in `docs/[slug]/+page.svelte`; the shiki theme colors are fine, it's the container + heading hierarchy that's weak.
- Iterate live in `pnpm -C flow-site dev` (visit `/docs/flow-operator`). Visual baselines only cover `/design`, so docs CSS won't trip them — but rebuild before playwright.

### 3. Web player: support `Note: <comment>` syntax (currently expects a variable declaration)
- **This is a flow-lang (C#) LANGUAGE change, not flow-site.** Today `Note` is the music type, so `Note: <text>` makes the parser expect `Note <name> = <value>` and error. Lexer: `flow-lang/Lexing/SimpleLexer.cs` (comments are `//` line + `///` doc; no `Note:` form). Parser: `flow-lang/Parsing/Parser.cs`.
- **OPEN QUESTION — clarify with composer before implementing:** what exactly should `Note: <comment>` mean / what's the use case? (A free-text annotation/comment? Only the literal word `Note`, or any `Label:`?) Does it conflict with `Note` the type? **First reproduce native behavior:** `dotnet run --project flow-interpreter -- -e 'Note: hello world'` to see the current error, then decide the lexer rule.
- After implementing in flow-lang: **rebuild the WASM bundle** → `bash flow-site/scripts/sync-runtime.sh` (needs `dotnet workload install wasm-tools`), commit the regenerated `flow-site/static/wasm/**`, then re-deploy. The runtime is otherwise frozen — only a real engine change justifies regenerating it.

## TODO — still-open audio bugs (from earlier, NOT started)

Reported in the live playground (composer says probably present on localhost too):
- **Note streams cut off after ~1 bar** — `(play | C4q D4q E4q F4q G4q A4q B4q C5h |)` only first 4 notes audible; `key Cmajor { (play | [chords] |) }` only first 2 chords.
- **`createSineTone` far too quiet** — `(play (createSineTone 440Hz 1.0 0.5))` near-inaudible.
- **Playground "stop" LED stays lit forever** — never detects playback end. **Likely a flow-site bug** (playground `state.svelte.ts` not resetting run-status / not wiring playback-complete), NOT the frozen runtime — check `flow-site/src/lib/playground/state.svelte.ts` + `runtime.ts`.
- **Isolation step:** run the snippets in NATIVE Flow with `writeWav` and inspect duration/peak — if native truncates/is-quiet too → core engine bug (`flow-lang`); if only WASM → `WebAudioBackend.cs` (needs WASM rebuild). The stuck-LED is almost certainly fixable in flow-site alone.

## Housekeeping
- 6 dirty `.planning/phases/42-*/42-AUDIT-data/*.txt` + `48-BUNDLE-SIZE.md` are unrelated **test-regen noise** (dirty since session start) — discard (`git restore`) or ignore; keep them OUT of commits.
- Background dev server may still be running (`pnpm -C flow-site dev`, :5173).
- GSD note: repo CLAUDE.md asks to route edits through GSD; composer OK'd keeping these rapid flow-site visual tweaks as **direct edits** for speed.
