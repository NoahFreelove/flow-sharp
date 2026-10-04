export const COLORS = ['#ff637e','#e8bf51','#55bce8','#76d99a','#ad8ae6','#e5a279'];
export const uid = () => crypto.randomUUID();
export const clone = value => structuredClone(value);
export const clamp = (v,a,b) => Math.max(a,Math.min(b,v));
export const SOURCE = `use "@std"
use "@audio"

// Each sequence becomes its own note layer.
// Move or split the clip without changing this source.
section motif {
    Sequence bass = | A2q A2q C3q E3q |
    Sequence lead = | A4e C5e E5q D5e C5e A4q |
    Sequence pad  = | {voice A3w} {voice E4w} |
}
Song song = [motif*4]
(writeMidi "workspace.mid" song)
`;
export function makeLayer(name,notes,i=0) {
    return {id:uid(),name,color:COLORS[i%COLORS.length],instrument:['sine','triangle','sawtooth'][i%3],
        gain:.35,pan:0,cutoff:8000,delay:0,notes:notes.map(n=>({...n,id:uid()}))};
}
export function createDemo() {
    const tracks = ['KICK','DRUMS','BASS','LEAD','PAD','FLOW'].map((name,i)=>({id:uid(),name,color:COLORS[i],mute:false,solo:false,gain:.7,pan:0}));
    const sources=[],clips=[];
    const add=(track,start,length,name,layerNotes,generated=false)=>{
        const source={id:uid(),name,code:generated?SOURCE:'',builtCode:generated?SOURCE:'',revision:1,
            kind:generated?'generated':'notes',duration:length,layers:layerNotes};
        sources.push(source); clips.push({id:uid(),name,trackId:tracks[track].id,sourceId:source.id,start,length,offset:0});
        return source;
    };
    const pattern=(pitches,step,len,velocity=.75)=>Array.from({length:Math.floor(len/step)},(_,i)=>({start:i*step,duration:step*.8,pitch:pitches[i%pitches.length],velocity}));
    for(const [i,start,len,name,pitches,step] of [[0,0,32,'FOUR ON THE FLOOR',[36],1],[1,8,24,'OFFBEAT',[42,46,42,38],.5],
        [2,0,16,'BASS A',[45,45,48,52],1],[2,16,16,'BASS B',[41,41,45,48],1],
        [3,8,8,'LEAD A',[69,72,76,74,72,69,67,69],.5],[3,20,8,'LEAD B',[72,74,76,79,77,76,74,72],.5],
        [4,0,32,'GLASS PAD',[57,64,60,67],4]]) {
        const l=makeLayer(name,pattern(pitches,step,len),i);l.color=COLORS[i];
        l.instrument=i<2?'noise':i===2?'triangle':i===4?'sine':'sawtooth';l.gain=i<2?.18:.22;l.cutoff=i===3?2400:6000;l.delay=i===3?.16:0;
        add(i,start,len,name,[l]);
    }
    // This is an explicitly seeded demo result, not a claim that the editor ran.
    add(5,8,16,'MOTIF.flow',[
        makeLayer('bass',pattern([45,45,48,52],1,16),2),
        makeLayer('lead',pattern([69,72,76,74,72,69],.5,16),3),
        makeLayer('pad',pattern([57,64],2,16),4)],true);
    return {version:1,name:'night_drive',bpm:124,loop:{enabled:true,start:8,end:24},tracks,sources,clips};
}
export function sourceOf(doc,clip) {return doc.sources.find(s=>s.id===clip.sourceId);}
export function visibleNotes(source,clip) {
    return source.layers.flatMap(layer=>layer.notes.flatMap(note=>{
        const start=Math.max(note.start,clip.offset),end=Math.min(note.start+note.duration,clip.offset+clip.length);
        return end>start?[{...note,start:start-clip.offset,duration:end-start,layerId:layer.id,color:layer.color}]:[];
    }));
}
export function splitClip(doc,id,beat) {
    const clip=doc.clips.find(c=>c.id===id);
    if(!clip||beat<=clip.start||beat>=clip.start+clip.length)return null;
    const left=beat-clip.start,right={...clip,id:uid(),start:beat,offset:clip.offset+left,length:clip.length-left};
    clip.length=left;doc.clips.push(right);return right.id;
}
export function duplicateClip(doc,id) {
    const clip=doc.clips.find(c=>c.id===id);if(!clip)return null;
    const next={...clip,id:uid(),start:clip.start+clip.length};doc.clips.push(next);return next.id;
}
export function detachClip(doc,id) {
    const clip=doc.clips.find(c=>c.id===id);if(!clip)return;
    const old=sourceOf(doc,clip),source={...clone(old),id:uid(),kind:'notes',duration:clip.length};
    source.layers=old.layers.map(layer=>({...clone(layer),id:uid(),notes:visibleNotes({layers:[layer]},clip).map(n=>({...n,id:uid()}))}));
    doc.sources.push(source);clip.sourceId=source.id;clip.offset=0;return source;
}
// Each project edit is an action implementing its own undo/redo contract.
export class DocumentAction {
    constructor(label,before,after){this.label=label;this.before=clone(before);this.after=clone(after);}
    undo(){return clone(this.before);}
    redo(){return clone(this.after);}
}
export class History {
    past=[];future=[];
    commit(label,before,after){if(JSON.stringify(before)===JSON.stringify(after))return false;this.past.push(new DocumentAction(label,before,after));if(this.past.length>100)this.past.shift();this.future=[];return true;}
    undo(doc){const action=this.past.pop();if(!action)return doc;this.future.push(action);return action.undo();}
    redo(doc){const action=this.future.pop();if(!action)return doc;this.past.push(action);return action.redo();}
}
export function validateProject(value) {
    const fail=()=>{throw new Error('Invalid Flow workspace project');};
    const finite=(n,a,b)=>typeof n==='number'&&Number.isFinite(n)&&n>=a&&n<=b;
    if(!value||value.version!==1||typeof value.name!=='string'||value.name.length>120||!finite(value.bpm,30,300))fail();
    if(!Array.isArray(value.tracks)||value.tracks.length>64||!Array.isArray(value.sources)||value.sources.length>256||!Array.isArray(value.clips)||value.clips.length>1024)fail();
    const ids=new Set();const id=x=>{if(typeof x!=='string'||ids.has(x))fail();ids.add(x);};
    const text=x=>{if(typeof x!=='string'||x.length>100000)fail();};const color=x=>{if(!/^#[0-9a-f]{6}$/i.test(x))fail();};
    for(const t of value.tracks){id(t.id);text(t.name);color(t.color);if(!finite(t.gain,0,1)||!finite(t.pan,-1,1)||typeof t.mute!=='boolean'||typeof t.solo!=='boolean')fail();}
    let count=0;
    for(const s of value.sources){id(s.id);text(s.name);text(s.code);text(s.builtCode);if(!['notes','generated'].includes(s.kind)||!finite(s.duration,.01,4096)||!Number.isInteger(s.revision)||s.revision<1||!Array.isArray(s.layers)||s.layers.length>32)fail();
        for(const l of s.layers){id(l.id);text(l.name);color(l.color);if(!['sine','triangle','sawtooth','square','noise'].includes(l.instrument)||!finite(l.gain,0,1)||!finite(l.pan,-1,1)||!finite(l.cutoff,40,20000)||!finite(l.delay,0,.8)||!Array.isArray(l.notes))fail();
            for(const n of l.notes){id(n.id);if(!finite(n.start,0,4096)||!finite(n.duration,.001,4096)||!Number.isInteger(n.pitch)||n.pitch<0||n.pitch>127||!finite(n.velocity,0,1))fail();if(++count>50000)fail();}}}
    for(const c of value.clips){id(c.id);text(c.name);if(!value.tracks.some(t=>t.id===c.trackId)||!value.sources.some(s=>s.id===c.sourceId)||!finite(c.start,0,4096)||!finite(c.offset,0,4096)||!finite(c.length,.0625,4096))fail();}
    if(!value.loop||typeof value.loop.enabled!=='boolean'||!finite(value.loop.start,0,4096)||!finite(value.loop.end,.0625,4096)||value.loop.end<=value.loop.start)fail();
    return clone(value);
}
