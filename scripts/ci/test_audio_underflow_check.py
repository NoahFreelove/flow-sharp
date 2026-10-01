import unittest
from audio_underflow_check import assess


def report(starved=False):
    return {
        'device': {'HostApiName': 'ALSA', 'Name': 'pipewire'}, 'nativeVersion': 'test',
        'actualSampleRate': 48000, 'requestedSampleRate': 48000, 'blockFrames': 256,
        'muted': True, 'callbackFault': False, 'droppedTimingSamples': 0,
        'diagnosticStall': {'enabled': starved, 'count': int(starved), 'requestedMilliseconds': 100 if starved else 0},
        'allCallbacks': {'count': 1500, 'outputUnderflowFlagCount': int(starved)},
        'callbackCadence': {
            'underflowFlagRecordsTruncated': False,
            'injectedStallCallbacks': [{'index': 375, 'secondsFromStart': 2, 'bodyMilliseconds': 100.1}] if starved else [],
            'underflowFlagCallbacks': [{'index': 376, 'secondsFromStart': 2.1, 'StatusFlags': 4}] if starved else [],
        },
    }


class UnderflowAssessmentTests(unittest.TestCase):
    def test_three_correlated_native_flags_validate_detection(self):
        self.assertTrue(assess(report(), [report(True) for _ in range(3)])['callbackStarvationDetectionValidated'])

    def test_zero_flags_cannot_pass_even_when_stall_was_measured(self):
        trial = report(True)
        trial['callbackCadence']['underflowFlagCallbacks'] = []
        trial['allCallbacks']['outputUnderflowFlagCount'] = 0
        self.assertFalse(assess(report(), [trial] * 3)['callbackStarvationDetectionValidated'])

    def test_startup_or_unrelated_late_flags_do_not_validate_stall(self):
        for index, seconds in [(0, 0), (1000, 5)]:
            trial = report(True)
            trial['callbackCadence']['underflowFlagCallbacks'][0].update(index=index, secondsFromStart=seconds)
            self.assertFalse(assess(report(), [trial] * 3)['callbackStarvationDetectionValidated'])

    def test_incomplete_recovery_capture_or_route_mismatch_cannot_pass(self):
        for section, key, value in [
            ('device', 'Name', 'pulse'), ('allCallbacks', 'count', 400),
            ('callbackCadence', 'underflowFlagRecordsTruncated', True),
            ('diagnosticStall', 'count', 0),
        ]:
            trial = report(True)
            trial[section][key] = value
            self.assertFalse(assess(report(), [trial] * 3)['callbackStarvationDetectionValidated'])
        trial = report(True)
        trial['droppedTimingSamples'] = 1
        self.assertFalse(assess(report(), [trial] * 3)['callbackStarvationDetectionValidated'])

    def test_dirty_baseline_or_insufficient_trials_cannot_pass(self):
        baseline = report()
        baseline['allCallbacks']['outputUnderflowFlagCount'] = 1
        self.assertFalse(assess(baseline, [report(True)] * 3)['callbackStarvationDetectionValidated'])
        self.assertFalse(assess(report(), [report(True)] * 2)['callbackStarvationDetectionValidated'])


if __name__ == '__main__':
    unittest.main()
