import test from 'node:test';
import assert from 'node:assert/strict';
import {createDemo,clone,splitClip,duplicateClip,detachClip,sourceOf,visibleNotes,History,validateProject} from '../model.js';
import {decodeMidi} from '../midi.js';

test('split and move retain source timing; undo and redo restore both placements',()=>{
 const doc=createDemo(),clip=doc.clips.at(-1),before=clone(doc),history=new History();
 const id=splitClip(doc,clip.id,12),right=doc.clips.find(c=>c.id===id);
 assert.equal(right.offset,4);assert.equal(right.length,12);assert.equal(right.sourceId,clip.sourceId);
 const notes=visibleNotes(sourceOf(doc,right),right);right.start=28;
 assert.deepEqual(visibleNotes(sourceOf(doc,right),right),notes);
 history.commit('Split and move',before,doc);assert.deepEqual(history.undo(doc),before);assert.deepEqual(history.redo(before),doc);
});
test('conversion crops notes and isolates edits from linked generated clips',()=>{
 const doc=createDemo(),clip=doc.clips.at(-1);duplicateClip(doc,clip.id);clip.offset=2;clip.length=3;
 const original=sourceOf(doc,clip),snapshot=clone(original),notes=visibleNotes(original,clip),converted=detachClip(doc,clip.id);
 assert.equal(clip.offset,0);assert.equal(converted.kind,'notes');assert.equal(converted.layers.flatMap(l=>l.notes).length,notes.length);
 assert.ok(converted.layers.flatMap(l=>l.notes).every(n=>n.start>=0&&n.start+n.duration<=3));
 converted.layers[0].notes[0].pitch=100;assert.deepEqual(original,snapshot);assert.doesNotThrow(()=>validateProject(doc));
});
test('undo branching clears redo and snapshot mutations cannot corrupt history',()=>{
 const doc=createDemo(),before=clone(doc),h=new History();doc.bpm=140;h.commit('Tempo',before,doc);
 const restored=h.undo(doc);restored.bpm=150;assert.equal(h.redo(restored).bpm,140);
 h.undo(doc);h.commit('Branch',before,restored);assert.equal(h.future.length,0);
});
test('project round trip and malformed references are checked',()=>{
 const doc=createDemo();assert.deepEqual(validateProject(JSON.parse(JSON.stringify(doc))),doc);
 doc.clips[0].sourceId='missing';assert.throws(()=>validateProject(doc));
});
test('MIDI decoder retains simultaneous voices, note lengths and velocity',()=>{
 const events=[0,255,3,4,98,97,115,115,0,144,60,100,0,144,64,80,96,128,60,0,0,128,64,0,0,255,47,0];
 const bytes=Uint8Array.from([77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107,0,0,0,events.length,...events]);
 const score=decodeMidi(bytes);assert.equal(score.layers[0].name,'bass');assert.equal(score.duration,1);
 assert.deepEqual(score.layers[0].notes.map(n=>[n.pitch,n.start,n.duration]),[[60,0,1],[64,0,1]]);
 assert.equal(score.layers[0].notes[0].velocity,100/127);assert.throws(()=>decodeMidi(bytes.slice(0,-1)),/Truncated/);
});
