"""Explicit opt-in public-repository historical network checkpoint.

Keeps reports, source caches and provider identity metadata in a new private directory.
No target code runs. Timing observations never gate CI or certify calibration.
"""
import argparse
import json
import hashlib
import os
from pathlib import Path
import platform
import signal
import subprocess
import time


def binary_digest(directory):
    entries = []
    for file in sorted(directory.iterdir()):
        if file.is_file() and file.suffix in ['.dll', '.json']:
            entries.append((file.name, hashlib.sha256(file.read_bytes()).hexdigest()))
    return hashlib.sha256(json.dumps(entries, separators=(',', ':')).encode('utf-8')).hexdigest()


def stop_tree(process):
    if os.name == 'nt':
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    else:
        os.killpg(process.pid, signal.SIGTERM)
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        if os.name != 'nt':
            os.killpg(process.pid, signal.SIGKILL)
        process.wait(timeout=10)


def run_row(args, root, label, repositories, since, until, warm):
    report_path = root / (label + '.json')
    cache = root / (label.rsplit('-', 1)[0] + '-cache')
    environment = os.environ.copy()
    environment['EFFORTHOURS_REPOSITORY_CACHE'] = str(cache / 'repositories')
    environment['EFFORTHOURS_PROVIDER_CACHE'] = str(cache / 'provider')
    command = ['dotnet', str(args.cli_dll), 'change', 'period', '--owner', args.owner,
               '--author', args.author, '--provider-login', args.author,
               '--since', since, '--until', until, '--breakdown', 'day', '--timezone', 'UTC',
               '--scope', 'engineering', '--capacity-hours-per-day', '8', '--no-rate',
               '--compact', '--no-checkpoint', '--discovery-timeout-seconds', str(args.discovery_seconds),
               '--max-acquired-mib', str(args.acquired_mib), '--output', str(report_path)]
    for repository in repositories:
        command.extend(['--repository', repository])
    started = time.monotonic()
    timed_out = False
    with (root / (label + '.stdout.log')).open('w', encoding='utf-8', newline='\n') as stdout, \
            (root / (label + '.stderr.log')).open('w', encoding='utf-8', newline='\n') as stderr:
        process = subprocess.Popen(command, env=environment, stdout=stdout, stderr=stderr,
                                   start_new_session=os.name != 'nt')
        try:
            exit_code = process.wait(timeout=args.run_seconds)
        except subprocess.TimeoutExpired:
            timed_out = True
            stop_tree(process)
            exit_code = process.returncode
        except KeyboardInterrupt:
            stop_tree(process)
            raise
    row = {'label': label, 'cache': 'warm' if warm else 'cold', 'repositories': repositories,
           'sinceInclusive': since, 'untilExclusive': until, 'outerSeconds': round(time.monotonic() - started, 3),
           'exitCode': exit_code, 'driverTimeout': timed_out}
    if report_path.exists():
        report = json.loads(report_path.read_text(encoding='utf-8'))
        execution = report.get('execution', {})
        discovery = report.get('discovery', {})
        selection = report.get('sourcePortfolio', {}).get('selection')
        selection_digest = hashlib.sha256(json.dumps(selection, sort_keys=True, separators=(',', ':')).encode('utf-8')).hexdigest() if selection is not None else None
        row.update({'selectionDigest': selection_digest, 'status': report.get('status'), 'semanticDigest': report.get('verification', {}).get('semanticDigest'),
                    'endToEndMilliseconds': execution.get('endToEndElapsedMilliseconds'),
                    'discoveryMilliseconds': discovery.get('elapsedMilliseconds'),
                    'providerQueries': discovery.get('providerQueryCount'), 'providerPages': discovery.get('providerPageCount'),
                    'providerProcesses': discovery.get('providerProcessCount'),
                    'acquisition': discovery.get('acquisition'),
                    'historicalPullRequests': discovery.get('providerDiagnostics', {}).get('historicalPullRequests'),
                    'defaultHeads': discovery.get('defaultHeadCount'), 'openPrHeads': discovery.get('openPullRequestHeadCount'),
                    'historicalPrHeads': discovery.get('historicalPullRequestHeadCount'),
                    'resources': execution.get('resources'), 'reuse': execution.get('reuse'),
                    'phaseTimings': execution.get('phaseTimings'), 'failures': execution.get('failures'),
                    'agentAction': report.get('agentAction')})
    else:
        row['status'] = 'no-report'
    print(label, row['status'], row['outerSeconds'], 'seconds', flush=True)
    return row


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow-network', action='store_true', required=True)
    parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='New ignored/private directory; never commit caches or reports.')
    parser.add_argument('--owner', required=True)
    parser.add_argument('--author', required=True)
    parser.add_argument('--repository', action='append', required=True, help='First is narrow control; all form broad scope.')
    parser.add_argument('--narrow-since', required=True)
    parser.add_argument('--broad-since', required=True)
    parser.add_argument('--until', required=True)
    parser.add_argument('--discovery-seconds', type=int, default=900)
    parser.add_argument('--acquired-mib', type=int, default=1024)
    parser.add_argument('--run-seconds', type=int, default=1800)
    args = parser.parse_args()
    if not args.cli_dll.is_file() or len(set(args.repository)) != len(args.repository):
        parser.error('CLI must exist and repositories must be unique.')
    if not 1 <= args.discovery_seconds <= 86400 or not 1 <= args.acquired_mib <= 16384 or args.run_seconds < args.discovery_seconds:
        parser.error('Invalid explicit resource bounds.')
    if any(not repository.startswith(args.owner + '/') for repository in args.repository):
        parser.error('Every repository must belong to the selected owner.')
    root = args.output.resolve()
    root.mkdir(parents=True, exist_ok=False)
    args.cli_dll = args.cli_dll.resolve()
    frozen_binary = binary_digest(args.cli_dll.parent)
    rows = []
    try:
        for scope, repositories, since in [('narrow', args.repository[:1], args.narrow_since),
                                           ('broad', args.repository, args.broad_since)]:
            for warm in [False, True]:
                rows.append(run_row(args, root, scope + ('-warm' if warm else '-cold'), repositories, since, args.until, warm))
                if binary_digest(args.cli_dll.parent) != frozen_binary:
                    raise RuntimeError('CLI binaries changed during measurement; checkpoint is invalid.')
            first, second = rows[-2:]
            comparable = first['status'] == second['status'] == 'complete' and first.get('semanticDigest') == second.get('semanticDigest')
            second['sameCompleteSemanticDigest'] = comparable
            second['comparison'] = 'same-result' if comparable else 'incomplete' if first['status'] != 'complete' or second['status'] != 'complete' else 'input-changed' if first.get('selectionDigest') != second.get('selectionDigest') else 'semantic-mismatch'
    finally:
        summary = {'protocol': 'historical-network-checkpoint/1.0.0', 'binaryDigest': frozen_binary, 'platform': platform.platform(),
                   'logicalProcessors': os.cpu_count(), 'discoverySeconds': args.discovery_seconds,
                   'maximumAcquiredMiB': args.acquired_mib, 'runSeconds': args.run_seconds,
                   'checkpointEnabled': False, 'rows': rows}
        (root / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8', newline='\n')
    return 3 if any(row.get('comparison') == 'semantic-mismatch' for row in rows) else 0


if __name__ == '__main__':
    raise SystemExit(main())
