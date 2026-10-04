#!/usr/bin/env python3
"""Correlate validated server counter increments with nearby retained callback and poll records."""
import argparse
import json
from pathlib import Path
from audio_process_pause_check import profile_rows, assess_window
from audio_poll_analysis import analyze, parse_calls


def correlate(report, rows, calls, start, end, alignment):
    c=report['diagnosticClock'];freq=c['stopwatchFrequency']
    first=c['monotonicNs']+(c['firstCallbackStartTicks']-(c['stopwatchBeforeTicks']+c['stopwatchAfterTicks'])//2)*1_000_000_000//freq
    offset=c['realtimeNs']-c['monotonicNs']
    known={x['index']:x for n in report['callbackCadence']['longestGapNeighborhoods'] for x in n['callbacks']}
    result=[]
    for a,b in zip(rows,rows[1:]):
        if a['nodeXruns'] is None or b['nodeXruns'] is None or b['nodeXruns']<=a['nodeXruns'] or not start<=b['signalNs']<=end:
            continue
        t=b['signalNs']
        near=[x for x in calls if t-50_000_000<=x['startRealtimeNs']-offset<=t+50_000_000] if alignment=='available' else []
        callbacks=[x for x in known.values() if abs(x['secondsFromStart']-(t-first)/1e9)<=.05]
        result.append(dict(secondsFromFirstCallback=(t-first)/1e9,signalNs=t,delta=b['nodeXruns']-a['nodeXruns'],
            nearbyCallbackRecords=sorted(callbacks,key=lambda x:x['index']),
            nearbyPollTimeouts=sum(x['timeout'] for x in near) if alignment=='available' else None,
            longestNearbyPollMilliseconds=max((x['durationNs']/1e6 for x in near),default=None),
            serverRows=[x for x in rows if t-50_000_000<=x['signalNs']<=t+50_000_000],nativePollCalls=near))
    return dict(events=result,clockCorrelation=alignment,clockAlignmentApproximate=True,windowMilliseconds=50,
        scope='Counter update time, not exact failure onset. Native poll entry timestamps within +/-50ms. Callback records are only retained longest-gap neighborhoods, not a full trace. Proximity alone does not establish cause.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();p=args.capture
    report=json.loads((p/'probe.json').read_text());meta=json.loads((p/'intervention.json').read_text())
    rows=profile_rows(json.loads((p/'profiler.raw.json').read_text()),meta['target'],validate_clock=False)
    window=assess_window(rows,meta['startNs'],meta['endNs'],False)['rows']
    lines=(p/'native-poll.raw.log').read_text().splitlines()
    analysis=analyze(report,lines)
    calls,_=parse_calls(lines,report['diagnosticClock']['nativeCallbackThreadId'],True)
    result=correlate(report,window,calls,meta['startNs'],meta['endNs'],analysis['correlation'])
    args.output.write_text(json.dumps(result,indent=2)+'\n')
