#!/usr/bin/env python3
"""Muted sustained callback + external PipeWire telemetry, fixed calibrated 48k/256 route.

Raw profiler and registry files contain local identities; keep them outside git.
A clean steady window does not certify startup, physical latency or device recovery.
"""
import argparse
import json
from pathlib import Path
import subprocess
from audio_process_pause_check import run_trial
from audio_underflow_check import intact, identity


def assess(report, observation, seconds, mode="isolated"):
    callbacks = report['allCallbacks']
    checks = {
        'captureIntact': intact(report),
        'calibratedRoute': identity(report)[:2] == ('ALSA', 'pipewire') and report['blockFrames'] == 256
                           and 'e1b70d33' in report['nativeVersion'],
        'frameCountMatches': callbacks['renderedFrames'] == callbacks['count'] * report['blockFrames'],
        'notInjected': not report['diagnosticStall']['enabled'] and not observation['injected'],
        'durationCaptured': callbacks['capturedSpanSeconds'] >= seconds - .1,
        'observerCoverage': (observation['endNs'] - observation['startNs']) / 1e9 >= seconds - 4,
        'observerClean': observation.get('serverTelemetryPassed', observation['passed']) and observation.get('serverTelemetryStatus', observation['status']) == 'clean',
        'zeroCallbackFlags': callbacks['outputUnderflowFlagCount'] == 0,
        'bodyHeadroom': callbacks['over70Percent'] == 0,
        'bodyDeadline': callbacks['overDeadline'] == 0,
        'noBodyAllocations': callbacks['allocatedBytes'] == 0,
        'noParentCollections': sum(report['parentGcCollections']) == 0,
        'replacementRetired': report['Generation'] == 2 and report['retired'] == 1,
        'loadModeMatches': report['mode'] == mode and (mode == 'idle' or report['load']['Iterations'] > 0),
    }
    return {'status': 'passed' if all(checks.values()) else 'failed', 'checks': checks,
            'sustainedTargetMet': all(checks.values()), 'fullPhase6GateComplete': False,
            'requestedSeconds': seconds, 'route': identity(report),
            'callbackUnderflowFlags': callbacks['outputUnderflowFlagCount'],
            'nodeXrunDelta': observation['nodeXrunDelta'], 'driverXrunDelta': observation['driverXrunDelta'],
            'worstBodyMilliseconds': callbacks['maxMilliseconds'],
            'maxCallbackGapMilliseconds': callbacks['maxEntryGapMilliseconds'],
            'reportedOutputLatencySeconds': report['reportedOutputLatencySeconds'],
            'scope': 'Callback metrics cover the full muted capture; external telemetry covers the bracketed steady observation window only. Server counters are scheduling events, not exact physical underrun counts. Zero reported output latency is unavailable. No startup, device recovery, listening, UI or general DSP certification.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe', type=Path, required=True)
    parser.add_argument('--artifacts', type=Path, required=True)
    parser.add_argument('--seconds', type=int, default=300)
    parser.add_argument('--native-trace', action='store_true', help='Intrusive owned-process poll tracing; diagnostic, not an untraced performance baseline')
    parser.add_argument('--mode', choices=['idle', 'isolated'], default='isolated')
    parser.add_argument('--scheduler', action='store_true', help='Read-only owned-thread samples at 20ms; adds diagnostic observer load')
    args = parser.parse_args()
    if not 8 <= args.seconds <= 1800:
        parser.error('--seconds must be between 8 and 1800')
    args.artifacts.mkdir(parents=True, exist_ok=False)
    try:
        version = subprocess.check_output(['pw-profiler', '--version'], text=True, timeout=5).strip()
        observation = run_trial(args.probe, args.artifacts / 'capture', False,
                                seconds=args.seconds, mode=args.mode, observation_seconds=args.seconds - 4, scheduler=args.scheduler, native_trace=args.native_trace)
        report = json.loads((args.artifacts / 'capture/probe.json').read_text())
        # Summaries are shareable only after reviewing route/driver metadata.
        compact = {k: v for k, v in observation.items() if k != 'rows'}
        (args.artifacts / 'observation.json').write_text(json.dumps(compact, indent=2) + '\n')
        result = assess(report, observation, args.seconds, args.mode)
        result['nativeTraceRequested'] = args.native_trace
        result['schedulerCaptureRequested'] = args.scheduler
        result['profilerVersion'] = version
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        result = {'status': 'unknown', 'sustainedTargetMet': False,
                  'fullPhase6GateComplete': False, 'error': str(error)}
    (args.artifacts / 'assessment.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2), flush=True)
    return 0 if result['sustainedTargetMet'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
