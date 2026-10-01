#!/usr/bin/env python3
"""Observe a muted probe's whole-process starvation from outside that process.

Requires Linux, PipeWire's profiler, pw-dump and a built Release probe. No server
configuration changes. Raw registry snapshots stay local: they contain user IDs.
"""
import argparse
import json
from pathlib import Path
import signal
import subprocess
import time

from audio_underflow_check import intact, identity


class Unavailable(ValueError):
    """Evidence is missing, discontinuous or cannot be attributed safely."""


def node_identity(registry, pid):
    clients = {x['id'] for x in registry if x.get('type') == 'PipeWire:Interface:Client'
               and str(x.get('info', {}).get('props', {}).get('application.process.id')) == str(pid)}
    nodes = [x for x in registry if x.get('type') == 'PipeWire:Interface:Node'
             and x.get('info', {}).get('props', {}).get('client.id') in clients
             and x['info']['props'].get('media.class') == 'Stream/Output/Audio']
    if len(nodes) != 1:
        raise Unavailable(f'Expected one output node owned by probe, found {len(nodes)}')
    node = nodes[0]
    props = node['info']['props']
    drivers = [x for x in registry if x.get('type') == 'PipeWire:Interface:Node'
               and x['id'] == props.get('node.driver-id')]
    if len(drivers) != 1:
        raise Unavailable('Output driver identity unavailable')
    driver = drivers[0]
    dp = driver['info']['props']
    return {'nodeId': node['id'], 'nodeSerial': props['object.serial'], 'nodeName': props['node.name'],
            'driverId': driver['id'], 'driverSerial': dp['object.serial'], 'driverName': dp['node.name']}


def profile_rows(records, target):
    # pw-profiler 1.6.2 emits a flat JSON array, groups starting with info, and
    # an empty final object on graceful shutdown. Truncation is not zero errors.
    if not records or records[-1] != {}:
        raise Unavailable('Profiler did not finish a complete JSON stream')
    groups = []
    current = []
    for record in records[:-1]:
        if record.get('type') == 'info':
            if current:
                groups.append(current)
            current = []
        if not current and record.get('type') != 'info':
            raise Unavailable('Profiler record without sequence info')
        current.append(record)
    if current:
        groups.append(current)
    rows = []
    for group in groups:
        drivers = [r for r in group if r.get('type') == 'driver']
        if len(drivers) != 1:
            raise Unavailable('Missing or ambiguous driver record')
        driver = drivers[0]
        if driver['id'] != target['driverId']:
            continue
        if driver['name'] != target['driverName']:
            raise Unavailable('Driver name changed')
        followers = [r for r in group if r.get('type') == 'follower' and r['id'] == target['nodeId']]
        if len(followers) > 1:
            raise Unavailable('Ambiguous follower record')
        follower = followers[0] if followers else None
        if follower and follower['name'] != target['nodeName']:
            raise Unavailable('Follower name changed')
        clocks = [r for r in group if r.get('type') == 'clock']
        if len(clocks) != 1 or clocks[0]['rate'] != '1/48000' or clocks[0]['duration'] != 256:
            raise Unavailable('Unexpected or missing driver rate/quantum')
        rows.append({'sequence': group[0]['count'], 'signalNs': driver['signal'],
                     'driverXruns': driver['xrun_count'],
                     'nodeXruns': follower['xrun_count'] if follower else None})
    return rows


def assess_window(rows, start, end, injected):
    # Half a second on either side demonstrates a stable baseline and recovery.
    before = [i for i, r in enumerate(rows) if r['signalNs'] <= start - 500_000_000]
    after = [i for i, r in enumerate(rows) if r['signalNs'] >= end + 500_000_000]
    if not before or not after:
        raise Unavailable('Observer does not bracket the observation window')
    window = rows[before[-1]:after[0] + 1]
    if len(window) < 2 or any(r['nodeXruns'] is None for r in window):
        raise Unavailable('Follower missing inside observation window')
    # Complete and incomplete notifications can share the same driver signal
    # timestamp. The profiler sequence still advances for each notification.
    for a, b in zip(window, window[1:]):
        if b['sequence'] != a['sequence'] + 1 or not 0 <= b['signalNs'] - a['signalNs'] <= 30_000_000:
            raise Unavailable('Profiler sequence loss, reset or timestamp gap')
        if b['nodeXruns'] < a['nodeXruns'] or b['driverXruns'] < a['driverXruns']:
            raise Unavailable('Xrun counter reset or wrap')
    during = [r for r in window if start <= r['signalNs'] <= end]
    if len({r['signalNs'] for r in during}) < 10:
        raise Unavailable('Insufficient server samples during observation')
    increments = [b for a, b in zip(window, window[1:]) if b['nodeXruns'] > a['nodeXruns']]
    correlated = [r for r in increments if start <= r['signalNs'] <= end + 100_000_000]
    driver_delta = window[-1]['driverXruns'] - window[0]['driverXruns']
    node_delta = window[-1]['nodeXruns'] - window[0]['nodeXruns']
    # Injections must be the only node increments in the assessed window.
    passed = (driver_delta == 0 and
              (node_delta > 0 and len(correlated) == len(increments) if injected else node_delta == 0))
    return {'status': 'detected' if injected and passed else 'clean' if not injected and passed else 'failed',
            'passed': passed, 'injected': injected, 'windowSamples': len(window),
            'samplesDuringPauseWindow': len(during), 'nodeXrunDelta': node_delta,
            'driverXrunDelta': driver_delta, 'rows': window}


def read_registry():
    return json.loads(subprocess.check_output(['pw-dump'], timeout=5))


def run_trial(probe, folder, injected):
    folder.mkdir()
    observer = child = None
    pause_sent = False
    with (folder / 'profiler.raw.json').open('w') as raw, (folder / 'profiler.log').open('w') as errors, \
            (folder / 'probe.stdout.log').open('w') as output, (folder / 'probe.stderr.log').open('w') as stderr:
        try:
            observer = subprocess.Popen(['pw-profiler', '-J'], stdout=raw, stderr=errors)
            child = subprocess.Popen([str(probe.resolve()), '8', 'idle', str((folder / 'probe.json').resolve()),
                                      'ALSA/pipewire', '256'], stdout=output, stderr=stderr)
            deadline = time.monotonic() + 4
            while True:
                if child.poll() is not None or observer.poll() is not None:
                    raise Unavailable('Probe or observer exited during startup')
                registry = read_registry()
                try:
                    target = node_identity(registry, child.pid)
                    break
                except Unavailable:
                    if time.monotonic() >= deadline:
                        raise
                    time.sleep(.1)
            (folder / 'registry-before.raw.json').write_text(json.dumps(registry))
            time.sleep(1)
            if child.poll() is not None or observer.poll() is not None:
                raise Unavailable('Probe or observer exited before observation')
            start = time.monotonic_ns()
            if injected:
                pause_sent = True
                child.send_signal(signal.SIGSTOP)
                stop_deadline = time.monotonic() + 1
                while '\nState:\tT' not in Path(f'/proc/{child.pid}/status').read_text():
                    if time.monotonic() > stop_deadline:
                        raise Unavailable('Probe did not enter stopped state')
                    time.sleep(.001)
            confirmed = time.monotonic_ns()
            try:
                time.sleep(.25)
            finally:
                if pause_sent:
                    child.send_signal(signal.SIGCONT)
                    pause_sent = False
            end = time.monotonic_ns()
            (folder / 'intervention.json').write_text(json.dumps(dict(target=target, startNs=start, endNs=end,
                confirmedStoppedNs=confirmed if injected else None, injected=injected), indent=2) + '\n')
            if observer.poll() is not None:
                raise Unavailable('External observer exited during pause')
            time.sleep(.75)
            registry = read_registry()
            (folder / 'registry-after.raw.json').write_text(json.dumps(registry))
            if node_identity(registry, child.pid) != target:
                raise Unavailable('Node/driver identity changed during observation')
            child.wait(timeout=15)
            if child.returncode != 0:
                raise Unavailable('Probe failed')
            if observer.poll() is not None:
                raise Unavailable('Observer exited before capture completion')
            observer.terminate()
            observer.wait(timeout=5)
            if observer.returncode != 0:
                raise Unavailable('Observer did not shut down cleanly')
            records = json.loads((folder / 'profiler.raw.json').read_text())
            result = assess_window(profile_rows(records, target), start, end, injected)
            report = json.loads((folder / 'probe.json').read_text())
            if not intact(report) or identity(report)[:2] != ('ALSA', 'pipewire') or report['diagnosticStall']['enabled']:
                raise Unavailable('Probe capture or selected route invalid')
            if not injected and report['allCallbacks']['outputUnderflowFlagCount'] != 0:
                result.update(status='failed', passed=False)
            if injected and report['allCallbacks']['maxEntryGapMilliseconds'] < 200:
                raise Unavailable('Probe did not record the expected callback interruption')
            result.update(route=identity(report), target=target, startNs=start, confirmedStoppedNs=confirmed if injected else None,
                          endNs=end, confirmedPauseMilliseconds=(end-confirmed)/1e6 if injected else 0,
                          callbackUnderflowFlags=report['allCallbacks']['outputUnderflowFlagCount'],
                          maxCallbackGapMilliseconds=report['allCallbacks']['maxEntryGapMilliseconds'])
            return result
        finally:
            if child is not None and child.poll() is None:
                if pause_sent:
                    child.send_signal(signal.SIGCONT)
                child.kill()
                child.wait(timeout=5)
            if observer is not None and observer.poll() is None:
                observer.terminate()
                try:
                    observer.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    observer.kill()
                    observer.wait(timeout=5)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe', type=Path, required=True)
    parser.add_argument('--artifacts', type=Path, required=True, help='New directory; raw files contain local identifiers')
    args = parser.parse_args()
    args.artifacts.mkdir(parents=True, exist_ok=False)
    results = []
    try:
        version = subprocess.check_output(['pw-profiler', '--version'], text=True, timeout=5).strip()
        for name in ['baseline', 'paused-1', 'paused-2', 'paused-3']:
            result = run_trial(args.probe, args.artifacts / name, name != 'baseline')
            (args.artifacts / f'{name}.json').write_text(json.dumps(result, indent=2) + '\n')
            results.append({k: v for k, v in result.items() if k != 'rows'})
            print(f"{name}: {result['status']}; node delta {result['nodeXrunDelta']}; callback flags {result['callbackUnderflowFlags']}", flush=True)
        passed = len(results) == 4 and all(r['passed'] and r['route'] == results[0]['route'] for r in results)
        assessment = {'processPauseDetectionValidated': passed, 'profilerVersion': version, 'trials': results,
                      'scope': 'ALSA/pipewire at 48 kHz / 256 frames: external detection of a 250 ms whole-client pause. Node xrun deltas are server scheduling events, not exact lost periods or physical hardware underruns.'}
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        assessment = {'processPauseDetectionValidated': False, 'status': 'unknown', 'error': str(error), 'trials': results}
    (args.artifacts / 'assessment.json').write_text(json.dumps(assessment, indent=2) + '\n')
    return 0 if assessment['processPauseDetectionValidated'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
