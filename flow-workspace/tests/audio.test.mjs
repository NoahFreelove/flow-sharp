import test from 'node:test';
import assert from 'node:assert/strict';
import {PreviewAudio} from '../audio.js';
import {createDemo} from '../model.js';
test('stop cancels a pending audio resume instead of starting playback later',async()=>{
 const audio=new PreviewAudio();let resume;
 audio.ready=()=>new Promise(resolve=>{resume=resolve;});
 const pending=audio.play(createDemo(),8);audio.stop();resume();await pending;
 assert.equal(audio.playing,false);assert.equal(audio.timer,null);
});
