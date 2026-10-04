"""Local-only prototype server. Exposes the workspace and published WASM, not the repository."""
import argparse
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parent
RUNTIME = ROOT.parent / 'flow-lang/bin/Release/net10.0/browser-wasm/AppBundle'

class Handler(SimpleHTTPRequestHandler):
    def translate_path(self, path):
        path = unquote(urlsplit(path).path)
        base = RUNTIME if path.startswith('/runtime/') else ROOT
        relative = path[len('/runtime/'):] if base == RUNTIME else path.lstrip('/')
        if not relative:
            relative = 'Flow Workspace.dc.html'
        target = (base / relative).resolve()
        if not target.is_relative_to(base) or any(part.startswith('.') for part in Path(relative).parts):
            return str(ROOT / '__not_found__')
        return str(target)

    def end_headers(self):
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=4174)
    args = parser.parse_args()
    if not (RUNTIME / 'flow-runtime.js').exists():
        print('Flow WASM is missing; publish flow-lang with FlowTarget=Web for code generation. The workspace demo still works.')
    print(f'Flow Workspace: http://127.0.0.1:{args.port}', flush=True)
    ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
