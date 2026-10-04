#!/usr/bin/env python3
"""Summarize owned callback poll/ppoll traces, including unfinished/resumed calls.

Tracing changes scheduling. These durations are diagnostic observations, not
untraced kernel latency or proof of the historical callback gap's cause.
"""
import argparse
from decimal import Decimal
import json
from pathlib import Path
import re


def ns(value):
    return int(Decimal(value)*1_000_000_000)


def parse_calls(lines, tid, require_exit=False):
    calls=[];pending=None;errors=[];exited=False
    for line in lines:
        match=re.match(r'^(\d+)\s+(\d+\.\d+)\s+(.*)$',line.strip())
        if not match or int(match[1])!=tid:
            continue
        stamp,payload=ns(match[2]),match[3]
        if payload.startswith('+++ exited with') or payload.startswith('+++ killed by'):
            exited=True
        resumed=re.match(r'<\.\.\. (p?poll) resumed>',payload)
        entry=re.match(r'(p?poll)\(',payload)
        if not (resumed or entry):
            continue
        if '<unfinished ...>' in payload:
            if entry is None:
                errors.append('Unsupported repeated unfinished resume');continue
            if pending is not None:errors.append('Overlapping pending syscall')
            pending=(stamp,entry[1]);continue
        duration=re.search(r'<([0-9.]+)>$',payload)
        if not duration:
            errors.append('Missing syscall duration');continue
        if resumed:
            if pending is None or pending[1]!=resumed[1]:
                errors.append('Unmatched resumed syscall');continue
            start,kind=pending;pending=None
        else:
            start,kind=stamp,entry[1]
            if pending is not None:errors.append('Pending syscall without resume')
        calls.append(dict(startRealtimeNs=start,durationNs=ns(duration[1]),kind=kind,
                          timeout=bool(re.search(r'= 0(?:\s|$)',payload)),interrupted='EINTR' in payload))
    if pending is not None:errors.append('Unfinished syscall at end of capture')
    if require_exit and not exited:errors.append('Missing callback-thread exit marker')
    return calls,errors


def analyze(report, lines):
    clock,end=report['diagnosticClock'],report['diagnosticClockEnd']
    freq=clock['stopwatchFrequency'];a=clock['stopwatchBeforeTicks'];b=clock['stopwatchAfterTicks']
    if freq<=0 or b<a or end['stopwatchAfterTicks']<end['stopwatchBeforeTicks']:
        raise ValueError('Invalid clock brackets')
    uncertainty=(b-a+end['stopwatchAfterTicks']-end['stopwatchBeforeTicks'])*1_000_000_000//freq+1
    offset=clock['realtimeNs']-clock['monotonicNs']
    drift=abs(end['realtimeNs']-end['monotonicNs']-offset)
    first=clock['monotonicNs']+(clock['firstCallbackStartTicks']-(a+b)//2)*1_000_000_000//freq+offset
    calls,errors=parse_calls(lines,clock['nativeCallbackThreadId'],require_exit=True)
    between=[(a['startRealtimeNs']+a['durationNs'],b['startRealtimeNs']) for a,b in zip(calls,calls[1:])
             if b['startRealtimeNs']>=a['startRealtimeNs']+a['durationNs']]
    events=report['callbackCadence']['underflowFlagCallbacks']
    gaps=[]
    for event in events:
        finish=first+round(event['secondsFromStart']*1e9)
        start=finish-round(event['entryGapMilliseconds']*1e6)
        overlap=[c for c in calls if c['startRealtimeNs']<=finish+uncertainty and c['startRealtimeNs']+c['durationNs']>=start-uncertainty]
        gaps.append(dict(callbackIndex=event['index'],gapMilliseconds=event['entryGapMilliseconds'],
                         overlappingPollCount=len(overlap), overlappingTimeouts=sum(c['timeout'] for c in overlap),
                         longestBetweenPollOverlapMilliseconds=max((max(0,min(b,finish)-max(a,start))/1e6 for a,b in between),default=None),
                         longestOverlappingPollMilliseconds=max((c['durationNs']/1e6 for c in overlap),default=None)))
    return dict(callbackThreadId=clock['nativeCallbackThreadId'],completedPollCalls=len(calls),
                clockOffsetDriftNs=drift,clockBracketUncertaintyNs=uncertainty,
                correlation='available' if calls and not errors and drift<=uncertainty+1_000_000 else 'unknown',
                parseErrors=errors,timeouts=sum(c['timeout'] for c in calls),
                interruptedCalls=sum(c['interrupted'] for c in calls),
                longestBetweenPollMilliseconds=max(((b-a)/1e6 for a,b in between),default=None),
                longestPollMilliseconds=max((c['durationNs']/1e6 for c in calls),default=None),
                underflowGaps=gaps,
                scope='Only poll/ppoll are traced, not all native waits or CPU scheduling. ptrace perturbs timing. Real-time clock endpoints bound observed drift, not transient clock jumps; correlation is approximate. Clean traced runs do not close an earlier untraced failure.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe',type=Path,required=True)
    parser.add_argument('--trace',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    result=analyze(json.loads(args.probe.read_text()),args.trace.read_text().splitlines())
    args.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
