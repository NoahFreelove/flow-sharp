#!/usr/bin/env python3
"""Exercise real LSP completion/hover and optionally trace device/sample access."""
import argparse
import json
from pathlib import Path
import queue
import shutil
import subprocess
import tempfile
import threading
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('binary', type=Path)
    parser.add_argument('--artifacts', type=Path)
    parser.add_argument('--trace', action='store_true', help='Require Linux strace for device/sample access evidence')
    args = parser.parse_args()
    output = (args.artifacts or Path(tempfile.mkdtemp(prefix='flow-analysis-smoke-'))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    command = [str(args.binary.resolve())]
    if args.trace:
        if not shutil.which('strace'):
            raise RuntimeError('--trace requires strace')
        command = ['strace', '-f', '-e', 'trace=openat,connect', '-o', str(output / 'access.trace'), *command]
    messages = queue.Queue()
    with (output / 'stderr.log').open('wb') as stderr:
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr)

        def read():
            try:
                while True:
                    headers = {}
                    while (line := process.stdout.readline()) not in (b'\r\n', b'\n', b''):
                        key, value = line.decode().split(':', 1)
                        headers[key.lower()] = value.strip()
                    if not line:
                        return
                    messages.put(json.loads(process.stdout.read(int(headers['content-length']))))
            except Exception as error:
                messages.put(error)

        threading.Thread(target=read, daemon=True).start()

        def send(method, params, request_id=None):
            message = {'jsonrpc': '2.0', 'method': method, 'params': params}
            if request_id is not None:
                message['id'] = request_id
            body = json.dumps(message).encode()
            process.stdin.write(f'Content-Length: {len(body)}\r\n\r\n'.encode() + body)
            process.stdin.flush()

        def response(request_id):
            deadline = time.monotonic() + 20
            while (remaining := deadline - time.monotonic()) > 0:
                message = messages.get(timeout=remaining)
                if isinstance(message, Exception):
                    raise message
                if message.get('id') == request_id:
                    if 'error' in message:
                        raise RuntimeError(message['error'])
                    return message['result']
            raise TimeoutError(f'No response for {request_id}')

        try:
            send('initialize', {'processId': None, 'rootUri': None, 'capabilities': {}}, 1)
            response(1)
            send('initialized', {})
            uri = (output / 'main.flow').as_uri()
            send('textDocument/didOpen', {'textDocument': {
                'uri': uri, 'languageId': 'flow', 'version': 1,
                'text': 'use "@core"\n(print "hello")\n'}})
            send('textDocument/completion', {'textDocument': {'uri': uri}, 'position': {'line': 2, 'character': 0}}, 2)
            completion = response(2)
            items = completion if isinstance(completion, list) else completion['items']
            labels = {item['label'] for item in items}
            assert 'print' in labels and 'map' in labels, labels
            assert 'oscListen' not in labels, 'Unimported OSC signature leaked into completion'
            send('textDocument/hover', {'textDocument': {'uri': uri}, 'position': {'line': 1, 'character': 3}}, 3)
            hover = response(3)
            assert 'print(' in json.dumps(hover), hover
            send('shutdown', None, 4)
            response(4)
            send('exit', None)
            process.stdin.close()
            process.wait(timeout=20)
            assert process.returncode == 0, process.returncode
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()
    forbidden = []
    if args.trace:
        for line in (output / 'access.trace').read_text().splitlines():
            if any(marker in line.lower() for marker in (
                '/samples/', '.wav"', '.sfz"', '/dev/snd', 'libpulse', 'libasound', 'librtmidi', '/pulse/native')):
                forbidden.append(line)
        assert not forbidden, '\n'.join(forbidden)
    report = {'completion': True, 'hover': True, 'clean_exit': True,
              'traced': args.trace, 'device_or_sample_accesses': forbidden}
    (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
