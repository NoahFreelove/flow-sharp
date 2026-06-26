# Deferred Items — 260626-n7r

## Pre-existing `pnpm -C flow-site check` redness (out of scope, NOT introduced by this task)

`svelte-check` reports errors on baseline `dev` independent of this refactor:

- All `*.test.ts` files fail on `describe`/`it`/`expect` ("Cannot find name …")
  because the svelte-kit-generated `.svelte-kit/tsconfig.json` has
  `"types": ["node"]` only — no `vitest/globals` — and under pnpm's isolated
  `node_modules` layout `@types/node` (present only in `node_modules/.pnpm/`) isn't
  resolvable, so even the `node` lib is a warning. Affected untouched files:
  `home-a11y.test.ts`, `home-deeplinks.test.ts`, `home-tokens.test.ts`.
- `src/lib/runtime.ts:73` — "Unused '@ts-expect-error' directive" (untouched).

These run GREEN under `pnpm test` (vitest `globals: true` works at runtime). The
edited source files in this task (`snippets.ts`, `state.svelte.ts`, `+page.svelte`)
are check-clean.

Suggested fix (separate task): add `"vitest/globals"` to the project's
`tsconfig.json` `compilerOptions.types` (e.g. `["node", "vitest/globals"]`) and/or
add `@types/node` as a direct devDependency / hoist it, then clear the stale
`@ts-expect-error` in `runtime.ts`.
