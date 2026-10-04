import copy
import json
from pathlib import Path
import unittest
from audio_sustained_check import assess
from audio_process_pause_check import run_trial


class SustainedTests(unittest.TestCase):
    def setUp(self):
        root = Path(__file__).resolve().parents[2]
        self.report = json.loads((root / 'docs/baselines/phase6/process-pause/baseline-probe.json').read_text())
        self.report.update(mode='isolated', load={'Iterations': 100})
        self.observation = dict(passed=True, status='clean', injected=False,
                                startNs=0, endNs=4_000_000_000, nodeXrunDelta=0, driverXrunDelta=0)

    def test_combined_clean_capture_passes_without_closing_phase6(self):
        result = assess(self.report, self.observation, 8)
        self.assertTrue(result['sustainedTargetMet'], result['checks'])
        self.assertFalse(result['fullPhase6GateComplete'])

    def test_independent_failure_channels_are_not_hidden(self):
        for field in ['outputUnderflowFlagCount', 'over70Percent', 'overDeadline', 'allocatedBytes']:
            report = copy.deepcopy(self.report)
            report['allCallbacks'][field] = 1
            with self.subTest(field=field):
                self.assertFalse(assess(report, self.observation, 8)['sustainedTargetMet'])
        observation = dict(self.observation, passed=False, status='failed', nodeXrunDelta=2)
        result = assess(self.report, observation, 8)
        self.assertFalse(result['sustainedTargetMet'])
        self.assertEqual(0, result['callbackUnderflowFlags'])
        self.assertEqual(2, result['nodeXrunDelta'])

    def test_short_observation_or_capture_fails(self):
        self.assertFalse(assess(self.report, dict(self.observation, endNs=1), 8)['sustainedTargetMet'])
        self.report['allCallbacks']['capturedSpanSeconds'] = 2
        self.assertFalse(assess(self.report, self.observation, 8)['sustainedTargetMet'])

    def test_route_injection_and_load_must_match(self):
        for mutate in [lambda r: r.update(blockFrames=128),
                       lambda r: r['diagnosticStall'].update(enabled=True),
                       lambda r: r['load'].update(Iterations=0),
                       lambda r: r.update(mode='idle')]:
            report = copy.deepcopy(self.report)
            mutate(report)
            self.assertFalse(assess(report, self.observation, 8)['sustainedTargetMet'])

    def test_real_underflow_is_failure_even_when_server_counters_are_clean(self):
        root = Path(__file__).resolve().parents[2] / 'docs/baselines/phase6/combined-telemetry'
        report = json.loads((root / 'probe.json').read_text())
        observation = json.loads((root / 'observer-window.json').read_text())
        result = assess(report, observation, 300)
        self.assertTrue(result['checks']['observerClean'])
        self.assertFalse(result['checks']['zeroCallbackFlags'])
        self.assertFalse(result['sustainedTargetMet'])

    def test_invalid_observation_rejected_before_spawning(self):
        with self.assertRaises(ValueError):
            run_trial(Path('/fake'), Path('/fake'), False, seconds=8, observation_seconds=8)


if __name__ == '__main__':
    unittest.main()
