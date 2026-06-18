// sync-grammar.mjs — copy the Phase 17 Flow TextMate grammar into flow-site (D-49-15, RESEARCH Q4).
//
// The grammar's source of truth is the sibling vscode-extension project:
//   vscode-extension/syntaxes/flow.tmLanguage.json   (scopeName "source.flow")
//
// The Cloudflare Pages build deploys ONLY the flow-site/ directory, so it cannot reach across
// to a sibling project at build time. This script copies the grammar into flow-site so shiki
// has a LOCAL copy to import (src/lib/docs/flow.tmLanguage.json — committed). Run it whenever
// the grammar changes; it is also wired into `prebuild` so CI/CF always have a fresh copy when
// the sibling project is present, and is a no-op (keeps the committed copy) when it is not.
//
// It normalizes `name` to `flow` so shiki registers the language id as `flow` (shiki derives the
// lang id from the grammar `name`). It must NOT add a `flow` alias: an alias equal to the name
// makes shiki register `flow -> flow`, which throws `Circular alias 'flow -> flow'` and silently
// breaks ALL highlighting (every block falls back to plain text).

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const siteDir = resolve(here, '..');
const repoRoot = resolve(siteDir, '..');

const SOURCE = resolve(repoRoot, 'vscode-extension/syntaxes/flow.tmLanguage.json');
const DEST = resolve(siteDir, 'src/lib/docs/flow.tmLanguage.json');

if (!existsSync(SOURCE)) {
	if (existsSync(DEST)) {
		console.log(`[sync-grammar] source not found (${SOURCE}); keeping committed copy at ${DEST}`);
		process.exit(0);
	}
	console.error(`[sync-grammar] FATAL: grammar source missing and no committed copy at ${DEST}`);
	process.exit(1);
}

const grammar = JSON.parse(readFileSync(SOURCE, 'utf8'));

// shiki derives the language id from `name`. Pin it to `flow` and strip any self-referential
// alias (an alias === name => shiki "Circular alias 'flow -> flow'", which kills highlighting).
grammar.name = 'flow';
grammar.aliases = [...new Set(grammar.aliases ?? [])].filter((a) => a !== grammar.name);

writeFileSync(DEST, JSON.stringify(grammar, null, 2) + '\n', 'utf8');
console.log(`[sync-grammar] copied ${SOURCE} -> ${DEST} (name=flow, scopeName=${grammar.scopeName})`);
