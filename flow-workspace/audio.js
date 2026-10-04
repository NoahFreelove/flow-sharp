import {visibleNotes,sourceOf} from './model.js';
export class PreviewAudio {
    request=0;context=null;playing=false;voices=new Set();scheduled=0;origin=0;offset=0;doc=null;timer=null;
    async ready(){this.context??=new AudioContext();await this.context.resume();}
    position(){return this.playing?this.offset+(this.context.currentTime-this.origin)*this.doc.bpm/60:this.offset;}
    async play(doc,beat=0){const request=++this.request;await this.ready();if(request!==this.request)return;this.stop();this.doc=doc;this.offset=beat;this.origin=this.context.currentTime;this.scheduled=beat;this.playing=true;this.tick();this.timer=setInterval(()=>this.tick(),25);}
    stop(){this.request++;this.offset=this.position();this.playing=false;clearInterval(this.timer);for(const v of this.voices){try{v.stop();}catch{}}this.voices.clear();}
    tick(){if(!this.playing)return;const pos=this.position(),loop=this.doc.loop;
        if(loop.enabled&&pos>=loop.end){void this.play(this.doc,loop.start+(pos-loop.end)%(loop.end-loop.start));return;}
        const endSong=Math.max(4,...this.doc.clips.map(c=>c.start+c.length));if(!loop.enabled&&pos>=endSong){this.stop();this.offset=0;return;}
        const to=Math.min(pos+.12*this.doc.bpm/60,loop.enabled?loop.end:endSong),solo=this.doc.tracks.some(t=>t.solo);
        for(const clip of this.doc.clips){const track=this.doc.tracks.find(t=>t.id===clip.trackId);if(track.mute||(solo&&!track.solo))continue;const source=sourceOf(this.doc,clip);
            for(const n of visibleNotes(source,clip)){let at=clip.start+n.start;const end=at+n.duration;
                if(at<this.scheduled&&this.scheduled===this.offset&&end>this.offset)at=this.offset;
                if(at>=this.scheduled&&at<to){const layer=source.layers.find(l=>l.id===n.layerId);this.voice(this.context,this.context.destination,n.pitch,(end-at)*60/this.doc.bpm,Math.max(this.context.currentTime,this.origin+(at-this.offset)*60/this.doc.bpm),layer,track,n.velocity);}}}
        this.scheduled=to;
    }
    voice(ctx,destination,pitch,duration,when,layer,track,velocity){
        if(ctx===this.context&&this.voices.size>=128)return;
        const gain=ctx.createGain(),filter=ctx.createBiquadFilter(),pan=ctx.createStereoPanner(),delay=ctx.createDelay(1),wet=ctx.createGain();
        let osc;if(layer.instrument==='noise'){
            const buf=ctx.createBuffer(1,Math.ceil(ctx.sampleRate*.15),ctx.sampleRate),data=buf.getChannelData(0);let seed=pitch*997+17;
            for(let i=0;i<data.length;i++){seed=(seed*1664525+1013904223)>>>0;data[i]=seed/2147483648-1;}
            osc=ctx.createBufferSource();osc.buffer=buf;duration=Math.min(duration,.13);
        }else{osc=ctx.createOscillator();osc.type=layer.instrument;osc.frequency.value=440*2**((pitch-69)/12);}
        filter.type='lowpass';filter.frequency.value=layer.cutoff;pan.pan.value=Math.max(-1,Math.min(1,layer.pan+track.pan));
        const level=velocity*layer.gain*track.gain*.23;
        gain.gain.setValueAtTime(0,when);gain.gain.linearRampToValueAtTime(level,when+.008);gain.gain.setValueAtTime(level,when+Math.max(.009,duration-.04));gain.gain.linearRampToValueAtTime(0,when+duration+.035);
        osc.connect(filter);filter.connect(gain);gain.connect(pan);pan.connect(destination);pan.connect(delay);delay.delayTime.value=.23;delay.connect(wet);wet.gain.value=layer.delay;wet.connect(destination);
        if(ctx===this.context)this.voices.add(osc);
        osc.onended=()=>{this.voices.delete(osc);setTimeout(()=>{for(const node of [osc,filter,gain,pan,delay,wet])node.disconnect();},400);};
        osc.start(when);osc.stop(when+duration+.04);
    }
    async audition(pitch,layer,track){await this.ready();this.voice(this.context,this.context.destination,pitch,.2,this.context.currentTime,layer,track,.7);}
    async export(doc){
        const beats=Math.max(4,...doc.clips.map(c=>c.start+c.length)),seconds=beats*60/doc.bpm+.5;if(seconds>180)throw new Error('Prototype WAV export is limited to three minutes.');
        const ctx=new OfflineAudioContext(2,Math.ceil(seconds*44100),44100),solo=doc.tracks.some(t=>t.solo);let count=0;
        for(const c of doc.clips){const t=doc.tracks.find(t=>t.id===c.trackId);if(t.mute||(solo&&!t.solo))continue;const s=sourceOf(doc,c);for(const n of visibleNotes(s,c)){if(++count>10000)throw new Error('Export limit: 10,000 notes');this.voice(ctx,ctx.destination,n.pitch,n.duration*60/doc.bpm,(c.start+n.start)*60/doc.bpm,s.layers.find(l=>l.id===n.layerId),t,n.velocity);}}
        const rendered=await ctx.startRendering(),bytes=new ArrayBuffer(44+rendered.length*4),v=new DataView(bytes),str=(p,s)=>[...s].forEach((c,i)=>v.setUint8(p+i,c.charCodeAt(0)));
        str(0,'RIFF');v.setUint32(4,bytes.byteLength-8,true);str(8,'WAVEfmt ');v.setUint32(16,16,true);v.setUint16(20,1,true);v.setUint16(22,2,true);v.setUint32(24,44100,true);v.setUint32(28,176400,true);v.setUint16(32,4,true);v.setUint16(34,16,true);str(36,'data');v.setUint32(40,bytes.byteLength-44,true);
        const l=rendered.getChannelData(0),r=rendered.getChannelData(1);for(let i=0;i<rendered.length;i++){v.setInt16(44+i*4,Math.round(Math.max(-1,Math.min(1,l[i]))*32767),true);v.setInt16(46+i*4,Math.round(Math.max(-1,Math.min(1,r[i]))*32767),true);}return new Blob([bytes],{type:'audio/wav'});
    }
}
