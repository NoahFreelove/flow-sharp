-- Flow Language Server configuration for Neovim (nvim-lspconfig)
--
-- Save to: ~/.config/nvim/lua/plugins/flow-lsp.lua (or adapt to your setup).
--
-- Prerequisite: the `flow-lsp` binary on your PATH.
--   Option A -- Download a release tarball from:
--     https://github.com/noah-freelove/flow-sharp/releases
--   Option B -- Build from source:
--     dotnet publish flow-lsp/flow-lsp.csproj \
--       -c Release -r linux-x64 --self-contained \
--       -p:PublishSingleFile=true \
--       -o ~/.local/bin/flow-lsp-dir
--     (replace linux-x64 with win-x64, osx-x64, or osx-arm64 as needed)
--
-- IMPORTANT: the binary requires the six shipped stdlib .flow files
-- (std, collections, audio, bars, notation, composition) to sit in the
-- same directory. dotnet publish copies them via the csproj's
-- <CopyToOutputDirectory> settings -- do not move the binary alone.

local lspconfig = require('lspconfig')
local configs = require('lspconfig.configs')

if not configs.flow then
  configs.flow = {
    default_config = {
      cmd = { 'flow-lsp' },
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
