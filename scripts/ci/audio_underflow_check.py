#!/usr/bin/env python3
"""Hardware-only callback starvation calibration; never part of ordinary CI.

Runs muted probes serially. A pass validates callback-starvation detection for
this route/configuration, not all server/device xruns or physical output latency.
"""
import argparse
import json
from pathlib import Path
import subprocess


def identity(report):
    device = report['device']
    # Enumeration indexes are transient; compare the actual named route.
    return (device['HostApiName'], device['Name'], report['nativeVersion'],
            report['actualSampleRate'], report['blockFrames'])


def intact(report):
    return (not report.get('callbackFault', True)
            and report.get('droppedTimingSamples', -1) == 0
            and report.get('muted') is True
            and report.get('actualSampleRate') == report.get('requestedSampleRate')
            and report.get('allCallbacks', {}).get('count', 0) > 0
            and not report.get('callbackCadence', {}).get('underflowFlagRecordsTruncated', True))


def assess(baseline, trials):
    baseline_clean = (intact(baseline)
                      and not baseline['diagnosticStall']['enabled']
                      and baseline['diagnosticStall']['count'] == 0
                      and baseline['allCallbacks']['outputUnderflowFlagCount'] == 0)
    results = []
    for trial in trials:
        cadence = trial.get('callbackCadence', {})
        stalls = cadence.get('injectedStallCallbacks', [])
        flags = cadence.get('underflowFlagCallbacks', [])
        checks = {
            'captureIntact': intact(trial),
            'sameRouteAndConfiguration': identity(trial) == identity(baseline),
            'oneMeasuredStall': (trial['diagnosticStall']['enabled']
                                 and trial['diagnosticStall']['count'] == 1
                                 and len(stalls) == 1
                                 and stalls[0]['bodyMilliseconds'] >= trial['diagnosticStall']['requestedMilliseconds']),
            'underflowFlagAfterStall': False,
            'callbacksContinuedAfterStall': False,
        }
        if len(stalls) == 1:
            stall = stalls[0]
            checks['underflowFlagAfterStall'] = any(
                event['index'] > stall['index'] and event['StatusFlags'] & 4
                and stall['secondsFromStart'] < event['secondsFromStart'] <= stall['secondsFromStart'] + 1
                for event in flags)
            checks['callbacksContinuedAfterStall'] = (
                trial['allCallbacks']['count'] - stall['index'] > trial['actualSampleRate'] / trial['blockFrames'])
        results.append({'checks': checks, 'passed': all(checks.values()),
                        'underflowFlagCount': trial['allCallbacks']['outputUnderflowFlagCount']})
    return {
        'baselineClean': baseline_clean,
        'callbackStarvationDetectionValidated': baseline_clean and len(results) >= 3 and all(r['passed'] for r in results),
        'route': identity(baseline),
        'trials': results,
        'scope': 'Callback-only starvation detection on the recorded route/configuration. Flags can coalesce; not an exact underrun count. Does not certify whole-process pauses, server/hardware xruns, physical latency or sustained glitch-free playback.',
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe', type=Path, required=True, help='Built Release AudioCallbackProbe executable')
    parser.add_argument('--device', required=True, help='Exact HOST/NAME, e.g. ALSA/pipewire (not a transient index)')
    parser.add_argument('--artifacts', type=Path, required=True, help='New directory for raw reports and assessment')
    parser.add_argument('--frames', type=int, choices=[128, 256], default=256)
    args = parser.parse_args()
    if '/' not in args.device:
        parser.error('--device requires HOST/NAME')
    args.artifacts.mkdir(parents=True, exist_ok=False)
    reports = []
    try:
        for name in ['baseline', 'starved-1', 'starved-2', 'starved-3']:
            path = args.artifacts / f'{name}.json'
            command = [str(args.probe.resolve()), '8', 'idle', str(path.resolve()), args.device, str(args.frames)]
            if name != 'baseline':
                command.append('--starve')
            with (args.artifacts / f'{name}.stdout.log').open('w') as out, (args.artifacts / f'{name}.stderr.log').open('w') as err:
                subprocess.run(command, stdout=out, stderr=err, check=True, timeout=38)
            report = json.loads(path.read_text())
            actual = f"{report['device']['HostApiName']}/{report['device']['Name']}"
            if actual != args.device:
                raise ValueError(f'Requested {args.device}, opened {actual}')
            reports.append(report)
            print(f"{name}: {report['allCallbacks']['outputUnderflowFlagCount']} underflow flags", flush=True)
        result = assess(reports[0], reports[1:])
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        result = {'callbackStarvationDetectionValidated': False, 'error': str(error)}
    (args.artifacts / 'assessment.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))
    return 0 if result['callbackStarvationDetectionValidated'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
