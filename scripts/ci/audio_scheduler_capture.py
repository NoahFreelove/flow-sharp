#!/usr/bin/env python3
"""Read-only /proc sampling for one owned probe. No scheduler/sysctl changes.

Wait location and state are instantaneous samples, not a trace of the whole gap.
Runqueue wait counts are unavailable when kernel sched_schedstats is disabled.
"""
import argparse
import json
from pathlib import Path
import resource
import signal
import time


def read_thread(path):
    fields = dict(line.split(':', 1) for line in (path / 'status').read_text().splitlines() if ':' in line)
    stat = (path / 'stat').read_text().rsplit(')', 1)[1].split()
    runtime, wait, slices = map(int, (path / 'schedstat').read_text().split())
    return dict(tid=int(path.name), startTicks=int(stat[19]), state=fields['State'].strip(),
                runtimeNs=runtime, runqueueWaitNs=wait, slices=slices,
                voluntarySwitches=int(fields['voluntary_ctxt_switches']),
                involuntarySwitches=int(fields['nonvoluntary_ctxt_switches']),
                waitChannel=(path / 'wchan').read_text().strip(), policy=int(stat[38]), rtPriority=int(stat[37]))


def selected_paths(root, identity_file, pid):
    if identity_file is None:
        return sorted((root / 'task').iterdir())
    if not identity_file.exists():
        return []
    owner, tid = map(int, identity_file.read_text().split())
    if owner != pid or tid <= 0:
        raise ValueError('Callback identity does not belong to the owned probe')
    return [root / 'task' / str(tid)]


def capture(pid, output, interval=.02, identity_file=None):
    running = True
    def stop(*_):
        nonlocal running
        running = False
    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    root = Path('/proc') / str(pid)
    # Fail on process replacement; thread start times are also retained in samples.
    identity = (root / 'stat').read_text().rsplit(')', 1)[1].split()[19]
    with output.open('w') as stream:
        def emit(value):
            stream.write(json.dumps(value, separators=(',', ':')) + '\n')
        stats = Path('/proc/sys/kernel/sched_schedstats')
        emit(dict(type='header', intervalSeconds=interval, selection='callback' if identity_file else 'all-threads', schedstatsEnabled=stats.read_text().strip() == '1'))
        while running and root.exists():
            begin = time.monotonic_ns()
            try:
                if (root / 'stat').read_text().rsplit(')', 1)[1].split()[19] != identity:
                    emit(dict(type='error', message='Probe PID reused'))
                    break
                threads, errors = [], []
                for path in selected_paths(root, identity_file, pid):
                    try:
                        threads.append(read_thread(path))
                    except (OSError, ValueError, KeyError, IndexError) as error:
                        errors.append(dict(tid=path.name, error=type(error).__name__))
                emit(dict(type='sample', beginNs=begin, endNs=time.monotonic_ns(), threads=threads, errors=errors,
                          schedstatsEnabled=stats.read_text().strip() == '1'))
            except FileNotFoundError:
                break
            except (ValueError, PermissionError) as error:
                emit(dict(type='error', message=str(error)))
                break
            time.sleep(max(0, interval - (time.monotonic_ns()-begin)/1e9))
        usage = resource.getrusage(resource.RUSAGE_SELF)
        emit(dict(type='end', userSeconds=usage.ru_utime, systemSeconds=usage.ru_stime))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', type=int, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--identity', type=Path, help='Atomic PID/TID file published by the probe control thread')
    args = parser.parse_args()
    capture(args.pid, args.output, identity_file=args.identity)
