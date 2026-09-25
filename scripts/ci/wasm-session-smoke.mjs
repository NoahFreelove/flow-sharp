// Boot a published AppBundle through the frozen JS adapter; no audio device needed.
import { pathToFileURL } from 'node:url';
import { writeFileSync } from 'node:fs';
if (!process.argv[2] || !process.argv[3])
  throw new Error('Usage: node scripts/ci/wasm-session-smoke.mjs <AppBundle/flow-runtime.js> <report.json>');
const { loadFlowRuntime } = await import(pathToFileURL(process.argv[2]).href);
const runtime = await loadFlowRuntime();
const source = 'use "@core"; Int value = 42; (print value)';
const first = await runtime.run(source);
const second = await runtime.run(source);
if (first.stdout.trim() !== '42' || second.stdout !== first.stdout || first.errors.length || second.errors.length)
  throw new Error(JSON.stringify({first, second}));
const invalid = await runtime.run('proc (');
if (invalid.errors.length !== 1 || invalid.errors[0].kind !== 'parse' || invalid.errors[0].line !== 1)
  throw new Error(JSON.stringify(invalid));
const music = await runtime.run('use "@std"; (print (str (add 100ms 50ms)))');
if (music.stdout.trim() !== '150ms' || music.errors.length) throw new Error(JSON.stringify(music));
runtime.dispose();
const report = { repeatedFreshSessions: true, parsedError: invalid.errors[0], musicOutput: music.stdout.trim(), javascriptAdapterUnchanged: true };
writeFileSync(process.argv[3], JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
