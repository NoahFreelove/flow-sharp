import {makeLayer} from './model.js';
// SMF types 0/1, PPQ timing. Kept separate from the UI and tested against Flow output.
export function decodeMidi(input) {
    const bytes=typeof input==='string'?Uint8Array.from(atob(input),c=>c.charCodeAt(0)):new Uint8Array(input);
    if(bytes.length>8*1024*1024)throw new Error('MIDI preview exceeds 8 MB');
    let p=0;const u8=()=>{if(p>=bytes.length)throw new Error('Truncated MIDI');return bytes[p++];};
    const word=()=>u8()*256+u8(),long=()=>word()*65536+word();
    const str=n=>Array.from({length:n},u8).map(c=>String.fromCharCode(c)).join('');
    const variable=()=>{let v=0;for(let i=0;i<4;i++){const b=u8();v=v*128+(b&127);if(!(b&128))return v;}throw new Error('Invalid MIDI delta');};
    if(str(4)!=='MThd')throw new Error('Not a MIDI file');
    const header=long(),format=word(),tracks=word(),ppq=word();
    if(header<6||format>1||tracks>128||!ppq||(ppq&0x8000))throw new Error('Unsupported MIDI timing or format');
    p+=header-6;const layers=[];let duration=0,total=0;
    for(let track=0;track<tracks;track++){
        if(str(4)!=='MTrk')throw new Error('Missing MIDI track');
        const size=long(),end=p+size;if(end>bytes.length)throw new Error('Truncated MIDI track');
        let ticks=0,running=0,name='';const active=new Map(),notes=new Map();
        const finish=(key,time)=>{const stack=active.get(key);if(!stack?.length)return;const note=stack.shift();note.duration=(time-note.tick)/ppq;delete note.tick;if(note.duration>0){const ch=key>>8;if(!notes.has(ch))notes.set(ch,[]);notes.get(ch).push(note);if(++total>10000)throw new Error('Preview limit: 10,000 notes');}};
        while(p<end){ticks+=variable();let status=u8();if(status<128){p--;status=running;if(!running)throw new Error('Invalid MIDI running status');}else if(status<240)running=status;
            if(status===255){const type=u8(),len=variable();if(p+len>end)throw new Error('Truncated MIDI metadata');if(type===3)name=str(len);else p+=len;running=0;continue;}
            if(status===240||status===247){const len=variable();p+=len;running=0;if(p>end)throw new Error('Truncated MIDI event');continue;}
            const kind=status>>4,ch=status&15;if(kind<8||kind>14)throw new Error('Unsupported MIDI event');const a=u8(),b=kind===12||kind===13?0:u8();if(a>127||b>127)throw new Error('Invalid MIDI data');const key=ch*256+a;
            if(kind===9&&b){if(!active.has(key))active.set(key,[]);active.get(key).push({tick:ticks,start:ticks/ppq,pitch:a,velocity:b/127});}
            else if(kind===8||kind===9)finish(key,ticks);
        }
        if(p!==end)throw new Error('Invalid MIDI track length');
        for(const [key,stack] of active)while(stack.length)finish(key,ticks);
        duration=Math.max(duration,ticks/ppq);
        for(const [channel,events] of notes)layers.push(makeLayer(name||`Voice ${layers.length+1}`,events.sort((a,b)=>a.start-b.start),layers.length));
    }
    if(!layers.length)throw new Error('No notes exported. Finish your Flow source with (writeMidi "workspace.mid" song).');
    if(layers.length>32||duration>4096)throw new Error('Preview exceeds 32 layers or 4,096 beats');
    return {layers,duration:Math.max(1,duration)};
}
