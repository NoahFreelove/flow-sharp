# Flow Language (flow-lang)
A music production language.

### AI Disclaimer
This entire repo was vibecoded with the help of the [GSD Framework](https://github.com/gsd-build/get-shit-done) and Claude Opus.
I did direct the features and testing so it was mainly supervised - but expect bugs to appear.


# What is flow-lang?
Flow-lang is a statically moderately-strongly typed functional interpreted language. The goal of flow-lang is to make a tool for code-minded folk like myself to create music in a fun way. The goal is also to not prefer one genre over another. You should be able to make rock, pop, jazz, or a symphony, all in one place - and all in the same buffer.

Flow-lang prioritizes ergonomics over almost everything. This language is interpreted, its not fast, and its not trying to be fast (though it takes the easy wins where possible). 

Many operations that would be errors in some languages are not in flow-lang because it always takes the most cheritable interpretation of your code. You could call this the JavaScript approach though I don't think we're as vulgar as JavaScript's type coercion. For example:
```
  Buffer wet = reverb(input, 5.0, 5.0, 5.0)      
  //                          ^    ^    ^ 
  //                       roomSize, damping, mix. flow-lang clamps all to [0, 1.0]
```
Flow-lang will silently fix this stuff for you. So you can use variables in position of arguments pretty freely without worrying about adjusting one variable means it being out of domain for some other function where you use it.

## What ISN'T flow-lang?
Flow-lang is not AI generated music. Flow lang is just a way to generate music. You still have to place the notes and make the samples, just how you would in a standard DAW except you use code. This is completely different to how AI generated music is created.

You could use AI to create `.flow` files but this still isn't really AI generated music, its more *vibecoded music* I suppose. As much as I love claude, it cannot generate anything super pleasant sounding in flow-lang yet (sorry claude!).

Flow-lang is also not trying to do some crazy GPU accelerated parallel rendering pipeline stuff. I'm not trying to optimize the hell out of flow-lang.

I hope my direction on where I want flow-lang to go was clear. If it has to be one sentence: `Flow-lang prioritizes the development experience and the artist regardless of the performance of the program.`

## Features
See [FEATURES.md](./FEATURES.md) for a complete list of features.


## Bugs?
This tool is just for fun, not any serious professional work. Bug reports may or may not be addressed.

## Editor support
Flow ships with a **Language Server (`flow-lsp`)**:

### VSCode / Cursor / VSCodium / Windsurf

Install the **Flow Language** extension which is bundled with this repo. Its not on the marketplace as of now.

### Emacs, Neovim, and other LSP editors

The `flow-lsp` server speaks plain LSP 3.17 over stdio, so any editor
with an LSP client can drive it. See
[`docs/editor-setup/`](./docs/editor-setup/README.md) for per-editor
config snippets (Neovim `nvim-lspconfig`, Helix `languages.toml`,
Emacs `lsp-mode`/`eglot`) and binary install guidance.
