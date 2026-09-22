"""Exercise CI failure reporting without building Flow or altering its checkout."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class VerifyTests(unittest.TestCase):
    def run_scenario(self, scenario):
        with tempfile.TemporaryDirectory(prefix='flow-verifier-') as directory:
            root = Path(directory)
            (root / 'scripts/ci').mkdir(parents=True)
            shutil.copy(Path(__file__).with_name('verify.py'), root / 'scripts/ci/verify.py')
            (root / 'sentinel.txt').write_text('original\n')
            subprocess.run(['git', 'init', '-q'], cwd=root, check=True)
            subprocess.run(['git', 'add', '.'], cwd=root, check=True)
            (root / 'bin').mkdir()
            dotnet = root / 'bin/dotnet'
            dotnet.write_text('''#!/usr/bin/env python3
import os, sys
from pathlib import Path
mode = os.environ['FLOW_VERIFY_SCENARIO']
if sys.argv[1] == 'build':
    sys.exit(1 if mode == 'build-failure' else 0)
if mode == 'mutation':
    Path('sentinel.txt').write_text('changed')
if mode != 'missing-trx':
    output = Path(sys.argv[sys.argv.index('--results-directory') + 1])
    name = sys.argv[sys.argv.index('--logger') + 1].split('LogFileName=')[1]
    executed = '0' if mode == 'empty-trx' else '1'
    (output / name).write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters total="1" executed="' + executed + '" /></ResultSummary></TestRun>')
sys.exit(1 if mode == 'test-failure' else 0)
''')
            dotnet.chmod(0o755)
            output = root / 'artifacts'
            result = subprocess.run(['python3', 'scripts/ci/verify.py', '--tier', 'all',
                                     '--artifacts', str(output)], cwd=root,
                                    env={**os.environ, 'PATH': str(root / 'bin') + os.pathsep + os.environ['PATH'],
                                         'FLOW_VERIFY_SCENARIO': scenario}, capture_output=True, text=True)
            report = json.loads((output / 'verification.json').read_text())
            return result.returncode, report

    def test_valid_run_passes(self):
        code, report = self.run_scenario('success')
        self.assertEqual(0, code)
        self.assertEqual([], report['changed_tracked_files'])
        self.assertEqual(3, len(report['steps']))

    def test_failures_cannot_pass(self):
        for scenario in ('build-failure', 'test-failure', 'missing-trx', 'empty-trx', 'mutation'):
            with self.subTest(scenario=scenario):
                code, report = self.run_scenario(scenario)
                self.assertNotEqual(0, code)
                if scenario == 'mutation':
                    self.assertEqual(['sentinel.txt'], report['changed_tracked_files'])
                if scenario == 'test-failure':
                    self.assertEqual(3, len(report['steps']), 'Both test projects must run after a test failure')


if __name__ == '__main__':
    unittest.main()
