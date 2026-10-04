import unittest
from audio_server_event_analysis import correlate


class ServerEventTests(unittest.TestCase):
    def test_counter_updates_are_separate_from_callback_flags_and_unknown_clock(self):
        report=dict(diagnosticClock=dict(stopwatchFrequency=1_000_000_000,stopwatchBeforeTicks=0,
            stopwatchAfterTicks=0,firstCallbackStartTicks=0,monotonicNs=0,realtimeNs=100000000000),
            callbackCadence=dict(longestGapNeighborhoods=[]))
        rows=[dict(signalNs=i*1_000_000,nodeXruns=0 if i<100 else 2) for i in range(200)]
        calls=[dict(startRealtimeNs=100100000000,durationNs=9000000,timeout=True)]
        result=correlate(report,rows,calls,0,200_000_000,'available')['events'][0]
        self.assertEqual(2,result['delta']);self.assertEqual(.1,result['secondsFromFirstCallback'])
        self.assertEqual(9,result['longestNearbyPollMilliseconds'])
        self.assertEqual([],result['nearbyCallbackRecords'])
        unknown=correlate(report,rows,calls,0,200_000_000,'unknown')['events'][0]
        self.assertIsNone(unknown['nearbyPollTimeouts']);self.assertEqual([],unknown['nativePollCalls'])
