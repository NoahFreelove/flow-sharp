import * as path from 'path';
import * as fs from 'fs';
import { workspace, ExtensionContext, window } from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, Executable, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

function platformDir(): string {
  const platform = process.platform;  // 'linux' | 'win32' | 'darwin'
  const arch = process.arch;          // 'x64' | 'arm64'
  return `${platform}-${arch}`;       // e.g. 'linux-x64', 'darwin-arm64'
}

function defaultBinaryPath(context: ExtensionContext): string {
  const dir = platformDir();
  const exe = process.platform === 'win32' ? 'flow-lsp.exe' : 'flow-lsp';
  return context.asAbsolutePath(path.join('server', dir, exe));
}

export async function activate(context: ExtensionContext) {
  const config = workspace.getConfiguration('flow');
  const override = (config.get<string>('server.path') ?? '').trim();
  const binary = override !== '' ? override : defaultBinaryPath(context);

  if (!fs.existsSync(binary)) {
    window.showErrorMessage(`Flow LSP binary not found at ${binary}`);
    return;
  }

  if (process.platform !== 'win32') {
    try { fs.chmodSync(binary, 0o755); } catch { /* best-effort */ }
  }

  const exe: Executable = {
    command: binary,
    transport: TransportKind.stdio,
    options: { env: process.env }
  };
  const serverOptions: ServerOptions = { run: exe, debug: exe };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'flow' }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/*.flow')
    },
    traceOutputChannel: window.createOutputChannel('Flow LSP Trace')
  };

  client = new LanguageClient('flow', 'Flow Language Server', serverOptions, clientOptions);
  await client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}
