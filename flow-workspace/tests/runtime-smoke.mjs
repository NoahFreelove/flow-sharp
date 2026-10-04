// Optional integration check; needs the existing published Flow WASM bundle.
import assert from 'node:assert/strict';
import {SOURCE} from '../model.js';
import {decodeMidi} from '../midi.js';
import {loadFlowRuntime} from '../../flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js';
const runtime=await loadFlowRuntime();
try {
 const result=await runtime.run(SOURCE);
 assert.deepEqual(result.errors,[],JSON.stringify(result.errors));
 assert.ok(result.midi,'Flow must export MIDI');
 const score=decodeMidi(result.midi);
 assert.equal(score.layers.length,3);assert.equal(score.duration,16);
 assert.ok(score.layers.every(l=>l.notes.length>0));
 console.log(JSON.stringify({layers:score.layers.map(l=>({name:l.name,notes:l.notes.length})),duration:score.duration}));
} finally {runtime.dispose();}
