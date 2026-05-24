-- Flow Language Server configuration for Neovim (nvim-lspconfig)
--
-- Save to: ~/.config/nvim/lua/plugins/flow-lsp.lua (or adapt to your setup).
--
-- Prerequisite: `flow` (or `flow-lsp`) on PATH.
--   Option A -- `flow install`:
--     curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash
--   Option B -- Download a release tarball from:
--     https://github.com/NoahFreelove/flow-sharp/releases
--   Option C -- Build from source:
--     dotnet publish flow-lsp/flow-lsp.csproj \
--       -c Release -r linux-x64 --self-contained \
--       -p:PublishSingleFile=true \
--       -o ~/.local/bin/flow-lsp-dir
--     (replace linux-x64 with win-x64, osx-x64, or osx-arm64 as needed)
--     Then swap `cmd` below to `{ 'flow-lsp' }`.
--
-- IMPORTANT (Option C only): the standalone flow-lsp binary requires the
-- six shipped stdlib .flow files (std, collections, audio, bars, notation,
-- composition) to sit in the same directory. dotnet publish copies them
-- via the csproj's <CopyToOutputDirectory> settings -- do not move the
-- binary alone. (Options A / B handle this automatically.)
--
-- LSP capabilities shipped: diagnostics, semantic tokens, completion,
-- hover, go-to-definition, signature help. Not yet implemented: rename,
-- find-references, code actions, document/workspace symbols, inlay hints,
-- formatter. See FEATURES.md § LSP for the authoritative table.

local lspconfig = require('lspconfig')
local configs = require('lspconfig.configs')

if not configs.flow then
  configs.flow = {
    default_config = {
      -- Default: use the `flow lsp` subcommand shipped by `flow install`.
      -- Swap to { 'flow-lsp' } if you went Option C.
      cmd = { 'flow', 'lsp' },
      filetypes = { 'flow' },
      root_dir = lspconfig.util.root_pattern('.git', '.flowproject'),
      settings = {},
    },
    docs = {
      description = 'Flow music-production language server',
    },
  }
end

-- Register Flow as a filetype for *.flow files.
vim.filetype.add({ extension = { flow = 'flow' } })

-- Attach and enable.
lspconfig.flow.setup({
  on_attach = function(client, bufnr)
    -- Your LSP keybindings go here; see :help lsp-buf for standard mappings.
    -- Example:
    --   vim.keymap.set('n', 'K',     vim.lsp.buf.hover,           { buffer = bufnr })
    --   vim.keymap.set('n', 'gd',    vim.lsp.buf.definition,      { buffer = bufnr })
    --   vim.keymap.set('i', '<C-s>', vim.lsp.buf.signature_help,  { buffer = bufnr })
  end,
})
