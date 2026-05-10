## Features at a glance                

(Claude generated this, it looks good to me from a high level but may not be complete or have small issues.)
                                                                                                                      
  Status: **Fully** = shipped · **Partial** = caveated or limited · **Not yet** = planned                                                                                                                                                                                
   
  ### Core language                                                                                                                                                                                                                                                      
                                                                                                                      
  | Feature | Status | Notes |                 
  |---|---|---|
  | Static typing with music-aware types | Fully | Note, Chord, Sequence, Song, Beat, Bar, etc. |
  | Type inference | Partial | Inferred in some contexts; explicit annotations elsewhere |                                                                                                                                                                               
  | Flow operator `->` for chaining | Fully | |                                                                                                                                                                                                                          
  | `proc` declarations with implicit returns | Fully | |                                                                                                                                                                                                                
  | Lambdas (`fn x => ...`) + function types | Fully | |                                                                                                                                                                                                                 
  | Arrays, indexing (`@N`), slicing | Fully | Slice supports negative-from-end |                                                                                                                                                                                        
  | Lazy evaluation | Fully | |                                                                                                                                                                                                                                          
  | Module imports (`use "@stdlib"` / relative) | Fully | |                                                                                                                                                                                                              
  | Pragma system (`enable <pragma>;`) | Fully | File-scope, top-of-file only |                                                                                                                                                                                          
  | String interpolation | Fully | |                                                                                                                                                                                                                                     
  | Loops (`for` / `while`) | Fully | |                                                                                                                                                                                                                                  
  | `//` line comments | Fully | `;` Lisp-style and `Note:`/`TODO:` not yet recognized |                                                                                                                                                                                 
  | `range(Int, Int[, Int])` | Fully | |                                                                                                                                                                                                                                 
                                                                                                                                                                                                                                                                         
  ### Notation & musical syntax                                                                                                                                                                                                                                          
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                                                                                                                                                                           
  |---|---|---|                                
  | Musical context blocks (`tempo`, `timesig`, `key`, `swing`) | Fully | |                                                                                                                                                                                              
  | `gain { }`, `reverbTime { }` blocks | Fully | |                                                                                                                                                                                                                      
  | Note streams `\| C4 D4 E4 \|` | Fully | |                                                                                                                                                                                                                            
  | Duration suffixes (`q h w e s`) + dotted, tied | Fully | |                                                                                                                                                                                                           
  | Rests (`_`) | Fully | |                                                                                                                                                                                                                                              
  | Cent offsets (`C4+50c`) | Fully | |                                                                                                                                                                                                                                  
  | Chord brackets in streams (`[C4 E4 G4]q`) | Fully | |                                                                                                                                                                                                                
  | Chord literals (`Cmaj7`, `Dm`, `F#dim`) | Fully | |                                                                                                                                                                                                                  
  | Roman numerals in `key { }` (`I`, `ii`, `V7`) | Fully | |                                                                                                                                                                                                            
  | Random choice (`(? ...)`, weighted, seeded `(?? ...)`) | Fully | |                                                                                                                                                                                                   
  | Section declarations + Song expressions | Fully | `[intro verse*2 chorus]` |                                                                                                                                                                                         
  | Tuplets `{N:M ...}` + fractional durations (`C4/12`) | Fully | |                                                                                                                                                                                                     
  | Multi-letter enharmonic edges (E↔Fb, B↔Cb, etc.) | Fully | |                                                                                                                                                                                                         
  | H-as-B alias (German notation) | Fully | Via `enable hAlias;` |                                                                                                                                                                                                      
  | Sequence visualization (ASCII piano roll) | Fully | |                                                                                                                                                                                                                
                                                                                                                                                                                                                                                                         
  ### Harmony & transforms                                                                                                                                                                                                                                               
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | `transpose`, `invert`, `retrograde`, `augment`, `diminish` | Fully | |
  | `up`, `down`, `repeat`, `concat` | Fully | |                                                                                                                                                                                                                         
  | Arpeggio with rate / direction / pattern params | Fully | |                                                                                                                                                                                                          
  | Chord inversions & voicings | Fully | |                                                                                                                                                                                                                              
  | Roman-numeral resolution from key context | Fully | |                                                                                                                                                                                                                
  | Chord progression DSL | Fully | |                                                                                                                                                                                                                                    
  | Snap-to-grid quantize | Fully | |                                                                                                                                                                                                                                    
  | Legato / portamento articulations | Fully | |                                                                                                                                                                                                                        
  | Scale linting (out-of-key warnings) | Fully | LSP-only, opt-in via `enable scaleLint;` |                                                                                                                                                                             
                                                                                                                                                                                                                                                                         
  ### Generative                                                                                                                                                                                                                                                         
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | Euclidean rhythms | Fully | |                                                                                                                                                                                                                                        
  | Swing | Fully | |
  | Humanize (uniform) | Fully | |                                                                                                                                                                                                                                       
  | Humanize (Gaussian via Box-Muller) | Fully | `humanizeGaussian()` |                                                                                                                                                                                                  
  | Markov chains / pattern mutation operators | Not yet | Planned extension to `(? ...)` syntax |                                                                                                                                                                       
                                                                                                                                                                                                                                                                         
  ### Synthesis                                                                                                                                                                                                                                                          
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                                                                                                                                                                           
  |---|---|---|                                
  | Built-in synths: piano, brass, sax, drums | Fully | "Old-MIDI charm" — not orchestral-realistic |                                                                                                                                                                    
  | Built-in synths: strings, organ, bell | Fully | Detuned saws / Hammond additive / Risset inharmonic |                                                                                                                                                                
  | Custom oscillator definitions (user `proc` as oscillator) | Fully | |                                                                                                                                                                                                
  | Formant vocal synthesis (`sing(phoneme, note, dur)`) | Fully | |                                                                                                                                                                                                     
  | External TTS hook (`tts(text)`, `setTtsCommand`) | Fully | |                                                                                                                                                                                                         
  | Sample-based instruments (single-sample varispeed) | Fully | `loadWav` with pitch shift via resample |                                                                                                                                                               
  | Multi-sample sampler (SFZ libraries) | Not yet | Headlines v1.4 |                                                                                                                                                                                                    
  | Vocaloid-style voice synthesis | Not yet | Planned |                                                                                                                                                                                                                 
                                                                                                                                                                                                                                                                         
  ### Effects (DSP)                                                                                                                                                                                                                                                      
                                                                                                                      
  | Feature | Status | Notes |                                                                                                                                                                                                                                           
  |---|---|---|
  | `reverb` | Fully | |                                                                                                                                                                                                                                                 
  | `lowpass`, `highpass`, `bandpass` | Fully | |                                                                     
  | `compress` (incl. sidechain input) | Fully | |
  | `delay` (ms) + delay-sync to NoteValue | Fully | |                                                                                                                                                                                                                   
  | `gain`, `mix(buf, buf)` | Fully | Mono→stereo promotion in `mix` |
  | Stereo panning (constant-power) | Fully | |                                                                                                                                                                                                                          
  | `tempoRamp(seq, startBPM, endBPM)` | Fully | |                                                                    
                                                                                                                                                                                                                                                                         
  ### Audio I/O & playback                                                                                            
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | WAV export (`writeWav` / `exportWav`) | Fully | |
  | WAV import (`loadWav`, varispeed) | Fully | |                                                                                                                                                                                                                        
  | Real-time playback (`play`, `loop`, `preview`, `stop`) | Fully | PulseAudio (Linux) |
  | Audio device enumeration & selection | Fully | |                                                                                                                                                                                                                     
  | macOS / Windows backends | Not yet | `IAudioBackend` abstraction in place; only PulseAudio implemented |          
                                                                                                                                                                                                                                                                         
  ### MIDI                                                                                                            
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | MIDI file import | Fully | |
  | MIDI file export (with TPQN auto-elevation, cap 9600) | Fully | Via DryWetMidi |
  | MIDI velocity through to render | Fully | |                                                                                                                                                                                                                          
  | `flow2midi` / `midi2flow` CLI subcommands | Not yet | Coming soon |
                                                                                                                                                                                                                                                                         
  ### Microtonal & tuning                                                                                             
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | Cent offsets in note streams | Fully | |                                                                                                                                                                                                                             
  | Named tunings (just intonation, Pythagorean, 12-EDO) | Fully | Per-key via pragma |
  | Full Scala (`.scl`) loader | Not yet | Deferred to v1.4 |                                                                                                                                                                                                            
  | Custom temperaments (user-defined ratios) | Not yet | Will land with the Scala loader |                                                                                                                                                                              
                                                                                                                                                                                                                                                                         
  ### Polyphony & timing                                                                                                                                                                                                                                                 
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | Polyphonic voice allocation | Fully | |
  | Polyrhythm support (parallel `timesig` per voice) | Fully | |
  | Beat-synced live reload | Fully | Quantizes file-watch reloads to next bar |                                                                                                                                                                                         
  
  ### Tooling & DX                                                                                                                                                                                                                                                       
                                                                                                                      
  | Feature | Status | Notes |                                                                                                                                                                                                                                           
  |---|---|---|
  | REPL (`dotnet run --project flow-interpreter`) | Fully | |                                                                                                                                                                                                           
  | Script execution + `-e` eval flag | Fully | |                                                                     
  | Watch mode (`--watch`) | Fully | |                                                                                                                                                                                                                                   
  | `--verbose` diagnostics on stderr | Fully | |
  | Math stdlib (sin/cos/tan/sqrt/min/max/floor/ceil/pow/log + `pi`/`tau`) | Fully | |                                                                                                                                                                                   
  | `flow` CLI binary + system install | Not yet | Coming soon |                                                                                                                                                                                         
                                                                                                                                                                                                                                                                         
  ### Editor support                                                                                                                                                                                                                                                     
                                                                                                                                                                                                                                                                         
  | Feature | Status | Notes |                                                                                        
  |---|---|---|                                
  | Language Server (`flow-lsp`, LSP 3.17 over stdio) | Fully | |
  | Diagnostics, completion, hover, go-to-def, signature help | Fully | |                                                                                                                                                                                                
  | Context-aware roman-numeral completion in `key { }` | Fully | |
  | VSCode extension | Partial | Bundles per-platform LSP binaries; Marketplace + OpenVSX publish deferred to first release tag |                                                                                                                                        
  | Neovim / Helix / Emacs / Zed via LSP | Fully | See `docs/editor-setup/` |                                         
  | JetBrains plugin | Not yet | Coming soon |                                                                                                                                                                                                              
  | Varargs in signature help | Not yet | Coming soon |                                                                                                                                                                                                          
  | `;` Lisp-style + `Note:`/`TODO:` comment recognition | Not yet | Coming soon | 
