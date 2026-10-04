import copy
import unittest
from audio_gap_analysis import analyze


def fixture():
    report=dict(diagnosticClock=dict(stopwatchBeforeTicks=0,stopwatchAfterTicks=2,stopwatchFrequency=1_000_000_000,
                                    monotonicNs=1,firstCallbackStartTicks=0,nativeCallbackThreadId=5),
                callbackCadence=dict(longestGapNeighborhoods=[],underflowFlagCallbacks=[dict(index=1,
                    secondsFromStart=.2,entryGapMilliseconds=120,StatusFlags=4)]))
    records=[dict(type='header')]
    for i in range(20):
        t=dict(tid=5,startTicks=9,runtimeNs=i*1000,runqueueWaitNs=0,slices=i,voluntarySwitches=i,
               involuntarySwitches=0,state='S (sleeping)',waitChannel='poll_schedule_timeout',policy=0)
        records.append(dict(type='sample',beginNs=i*20_000_000,endNs=i*20_000_000+100_000,
                            threads=[t],schedstatsEnabled=False))
    records.append(dict(type='end'))
    return report,records


class GapTests(unittest.TestCase):
    def test_correlates_callback_thread_and_does_not_invent_runqueue_wait(self):
        report,records=fixture()
        gap=analyze(report,records)['gaps'][0]
        self.assertEqual('sampled',gap['coverage'])
        self.assertGreater(gap['insideSamples'],0)
        self.assertIsNone(gap['runqueueWaitMilliseconds'])
        self.assertEqual({'S (sleeping)'},set(gap['observedStates']))

    def test_dropped_samples_reset_reuse_missing_tid_or_truncation_are_unknown(self):
        for kind in ['drop','reset','reuse','tid','truncated']:
            report,records=fixture()
            if kind=='drop':del records[6:10]
            if kind=='reset':records[8]['threads'][0]['runtimeNs']=0
            if kind=='reuse':records[8]['threads'][0]['startTicks']=10
            if kind=='tid':records[8]['threads'][0]['tid']=99
            if kind=='truncated':records.pop()
            with self.subTest(kind=kind):
                self.assertEqual('unknown',analyze(report,records)['gaps'][0]['coverage'])

    def test_short_gap_with_no_internal_sample_is_unknown(self):
        report,records=fixture()
        report['callbackCadence']['underflowFlagCallbacks'][0]['entryGapMilliseconds']=1
        self.assertEqual('unknown',analyze(report,records)['gaps'][0]['coverage'])

    def test_invalid_clock_or_thread_is_rejected(self):
        report,records=fixture()
        report['diagnosticClock']['nativeCallbackThreadId']=0
        with self.assertRaises(ValueError):analyze(report,records)
