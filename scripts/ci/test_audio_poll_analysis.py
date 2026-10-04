import unittest
from audio_poll_analysis import parse_calls,analyze


class PollTests(unittest.TestCase):
    def test_filters_tid_and_pairs_resume_with_original_start(self):
        calls,errors=parse_calls(['9 100.000000 poll([], 0, 6) = 0 (Timeout) <0.006000>',
            '8 100.010000 poll([], 0, 6) = 0 (Timeout) <0.006000>',
            '9 100.020000 ppoll([], 0, NULL, NULL, 8 <unfinished ...>',
            '9 100.030000 <... ppoll resumed>) = 1 <0.010000>'],9)
        self.assertFalse(errors);self.assertEqual(2,len(calls))
        self.assertEqual(100020000000,calls[1]['startRealtimeNs'])
        self.assertEqual(10000000,calls[1]['durationNs']);self.assertTrue(calls[0]['timeout'])

    def test_incomplete_and_unmatched_calls_are_not_silent_success(self):
        for line in ['9 100.0 poll([], 0, 6 <unfinished ...>',
                     '9 100.0 <... poll resumed>) = 1 <0.010000>']:
            self.assertTrue(parse_calls([line],9)[1])

    def test_correlates_underflow_with_overlapping_poll_only(self):
        report=dict(diagnosticClock=dict(stopwatchFrequency=1_000_000_000,stopwatchBeforeTicks=0,stopwatchAfterTicks=2,
            realtimeNs=100000000000,monotonicNs=1,firstCallbackStartTicks=0,nativeCallbackThreadId=9),
            diagnosticClockEnd=dict(stopwatchBeforeTicks=100,stopwatchAfterTicks=102,realtimeNs=100000000100,monotonicNs=101),
            callbackCadence=dict(underflowFlagCallbacks=[dict(index=4,secondsFromStart=.2,entryGapMilliseconds=120)]))
        lines=['9 100.010000 poll([], 0, 6) = 0 (Timeout) <0.006000>',
               '9 100.090000 poll([], 0, 6) = 1 <0.090000>',
               '9 101.000000 +++ exited with 0 +++']
        result=analyze(report,lines)
        self.assertEqual('available',result['correlation'])
        self.assertEqual(1,result['underflowGaps'][0]['overlappingPollCount'])
        self.assertEqual(90,result['underflowGaps'][0]['longestOverlappingPollMilliseconds'])
        self.assertEqual('unknown',analyze(report,lines[:-1])['correlation'])

    def test_clock_drift_marks_correlation_unknown(self):
        report=dict(diagnosticClock=dict(stopwatchFrequency=1_000_000_000,stopwatchBeforeTicks=0,stopwatchAfterTicks=2,
            realtimeNs=100000000000,monotonicNs=1,firstCallbackStartTicks=0,nativeCallbackThreadId=9),
            diagnosticClockEnd=dict(stopwatchBeforeTicks=100,stopwatchAfterTicks=102,realtimeNs=100020000000,monotonicNs=101),
            callbackCadence=dict(underflowFlagCallbacks=[]))
        result=analyze(report,['9 100.0 poll([], 0, 6) = 0 (Timeout) <0.006000>'])
        self.assertEqual('unknown',result['correlation'])
