#!/usr/bin/env python3
"""Correlate callback gap neighborhoods with sampled owned-thread /proc data.

Reports observed state and counter deltas, never infers exact blocking time or
root cause from sparse samples. Clock brackets retain alignment uncertainty.
"""
import argparse
import json
from collections import Counter
from pathlib import Path


def analyze(report, records):
    clock = report['diagnosticClock']
    before, after, freq = clock['stopwatchBeforeTicks'], clock['stopwatchAfterTicks'], clock['stopwatchFrequency']
    if freq <= 0 or after < before:
        raise ValueError('Invalid clock calibration')
    midpoint = (before + after) // 2
    first_ns = clock['monotonicNs'] + (clock['firstCallbackStartTicks'] - midpoint) * 1_000_000_000 // freq
    uncertainty = (after - before) * 1_000_000_000 // freq + 1
    tid = clock['nativeCallbackThreadId']
    if tid <= 0:
        raise ValueError('Callback native thread identity unavailable')
    complete = bool(records and records[0].get('type') == 'header' and records[-1].get('type') == 'end'
                    and not any(r.get('type') == 'error' for r in records))
    samples = []
    for row in records:
        if row.get('type') != 'sample':
            continue
        threads = [t for t in row['threads'] if t['tid'] == tid]
        samples.append(dict(begin=row['beginNs'], end=row['endNs'], thread=threads[0] if len(threads)==1 else None,
                            enabled=row['schedstatsEnabled']))
    events = {n['gapIndex']: next(c for c in n['callbacks'] if c['index']==n['gapIndex'])
              for n in report['callbackCadence']['longestGapNeighborhoods']}
    events.update({e['index']:e for e in report['callbackCadence']['underflowFlagCallbacks']})
    results=[]
    for index,event in sorted(events.items()):
        end = first_ns + round(event['secondsFromStart']*1e9)
        start = end - round(event['entryGapMilliseconds']*1e6)
        left = [i for i,r in enumerate(samples) if r['end'] <= start-uncertainty]
        right = [i for i,r in enumerate(samples) if r['begin'] >= end+uncertainty]
        result = dict(callbackIndex=index, secondsFromStart=event['secondsFromStart'],
                      gapMilliseconds=event['entryGapMilliseconds'], statusFlags=event['StatusFlags'],
                      startNs=start, endNs=end, coverage='unknown')
        if left and right:
            window=samples[left[-1]:right[0]+1]
            valid=all(r['thread'] is not None and r['begin']<=r['end'] for r in window)
            if valid:
                a,b=window[0],window[-1]
                keys=['runtimeNs','runqueueWaitNs','slices','voluntarySwitches','involuntarySwitches']
                valid=all(r['thread']['startTicks']==a['thread']['startTicks'] for r in window)
                valid=valid and all(y['begin']>=x['end'] and 0 <= y['begin']-x['begin'] <= 50_000_000
                                   and all(y['thread'][k]>=x['thread'][k] for k in keys)
                                   for x,y in zip(window,window[1:]))
                inside=[r for r in window if r['begin']>=start+uncertainty and r['end']<=end-uncertainty]
                enabled=all(r['enabled'] for r in window)
                result.update(coverage='sampled' if complete and valid and inside else 'unknown',
                    bracketMilliseconds=(b['end']-a['begin'])/1e6,
                    cpuMilliseconds=(b['thread']['runtimeNs']-a['thread']['runtimeNs'])/1e6,
                    runqueueWaitMilliseconds=(b['thread']['runqueueWaitNs']-a['thread']['runqueueWaitNs'])/1e6 if enabled else None,
                    voluntarySwitchDelta=b['thread']['voluntarySwitches']-a['thread']['voluntarySwitches'],
                    involuntarySwitchDelta=b['thread']['involuntarySwitches']-a['thread']['involuntarySwitches'],
                    observedStates=dict(Counter(r['thread']['state'] for r in inside)),
                    observedWaitChannels=dict(Counter(r['thread']['waitChannel'] for r in inside)),
                    policies=sorted({r['thread']['policy'] for r in window}),
                    maxSamplerIntervalMilliseconds=max((y['begin']-x['begin'])/1e6 for x,y in zip(window,window[1:])),
                    insideSamples=len(inside))
        results.append(result)
    return dict(callbackThreadId=tid, clockUncertaintyNs=uncertainty, captureComplete=complete,
                gaps=results, observerCost=records[-1] if complete else None,
                scope='20ms snapshots are not a scheduler trace. Counter deltas cover bracketing samples, not exactly the callback gap. Missing/slow sampling is unknown. Null runqueue wait means schedstats disabled. R state includes runnable as well as running. Sleeping/wchan samples show where the thread was observed, not what caused a missed wakeup; zero involuntary switches does not exclude CPU contention.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe',type=Path,required=True)
    parser.add_argument('--samples',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    result=analyze(json.loads(args.probe.read_text()),[json.loads(line) for line in args.samples.read_text().splitlines()])
    args.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
