// The existing frozen Flow adapter runs in a disposable worker, never on the UI thread.
import {decodeMidi} from './midi.js';
self.onmessage=async({data})=>{
    try {
        const {loadFlowRuntime}=await import('/runtime/flow-runtime.js');
        const runtime=await loadFlowRuntime();
        const result=await runtime.run(data.source);
        if(result.errors?.length){postMessage({errors:result.errors});return;}
        if(!result.midi)throw new Error('No MIDI note output. Add (writeMidi "workspace.mid" song) to your composition.');
        const score=decodeMidi(result.midi);
        postMessage({score,output:[result.stdout,result.stderr].filter(Boolean).join('\n')});
    }catch(error){postMessage({errors:[{message:error.message||String(error)}]});}
};
