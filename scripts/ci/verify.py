#!/usr/bin/env python3
"""Run a CI tier and reject tracked-file mutations, including on test failure."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FILTERS = {
    'core': 'Category!=Platform&Category!=LongRunning',
    'platform': 'Category=Platform',
    'long': 'Category=LongRunning',
    'all': None,
}


def snapshot():
    paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
    return {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest()
            if (ROOT / p).is_file() else None for p in paths if p}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tier', choices=FILTERS, default='all')
    parser.add_argument('--artifacts', type=Path)
    args = parser.parse_args()
    output = (args.artifacts or Path(tempfile.mkdtemp(prefix='flow-ci-'))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    before = snapshot()
    results = []

    def run(name, command):
        print(f'{name}: {" ".join(command)}', flush=True)
        with (output / f'{name}.log').open('w') as log:
            try:
                code = subprocess.run(command, cwd=ROOT, stdout=log,
                                      stderr=subprocess.STDOUT, timeout=1800).returncode
            except subprocess.TimeoutExpired:
                code = 124
        results.append({'step': name, 'exit_code': code})
        print(f'{name}: exit {code}; log {output / (name + ".log")}', flush=True)
        return code == 0

    try:
        if run('build', ['dotnet', 'build', 'flow-sharp.sln', '-p:FlowTarget=Desktop']):
            projects = ['flow-lang.Tests']
            if args.tier in ('all', 'core'):
                projects.append('flow-midi.Tests')
            for project in projects:
                # A fresh TRX name prevents a failed launch from reusing stale evidence.
                trx = output / f'{project}.trx'
                trx.unlink(missing_ok=True)
                command = ['dotnet', 'test', f'{project}/{project}.csproj', '--no-build',
                           '--logger', f'trx;LogFileName={trx.name}', '--results-directory', str(output)]
                if FILTERS[args.tier] and project == 'flow-lang.Tests':
                    command += ['--filter', FILTERS[args.tier]]
                run(project, command)
                if not trx.exists():
                    raise RuntimeError(f'Missing test evidence: {trx}')
                ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
                counters = ET.parse(trx).find('.//t:Counters', ns)
                if counters is None or int(counters.get('executed', '0')) == 0:
                    raise RuntimeError(f'No tests executed: {trx}')
                results[-1]['tests'] = counters.attrib
                print(json.dumps(counters.attrib), flush=True)
    finally:
        after = snapshot()
        changed = sorted(p for p in before.keys() | after.keys() if before.get(p) != after.get(p))
        report = {'tier': args.tier, 'steps': results, 'changed_tracked_files': changed}
        (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n')
        print(f'Tracked content changes: {changed}; artifacts: {output}', flush=True)
    return 1 if changed or any(r['exit_code'] for r in results) else 0


if __name__ == '__main__':
    raise SystemExit(main())
