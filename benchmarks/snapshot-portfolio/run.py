"""Explicit synthetic checkpoint; never a wall-clock CI gate."""
import argparse
import io
import json
import os
from pathlib import Path
import platform
import subprocess
import tarfile
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--directory', required=True, help='Fresh private fixture/output directory')
parser.add_argument('--cli', required=True, help='Built efforthours.dll')
parser.add_argument('--baseline', default='eh', help='Installed baseline command')
args = parser.parse_args()
root = Path(args.directory).resolve()
root.mkdir(parents=True, exist_ok=False)
cli = str(Path(args.cli).resolve())


def run(command, cwd=None, env=None, binary=False):
    result = subprocess.run(command, cwd=cwd, env=env, capture_output=True,
                            text=not binary, encoding=None if binary else 'utf-8',
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    if result.returncode:
        raise RuntimeError(result.stderr if not binary else result.stderr.decode('utf-8'))
    return result.stdout


def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding='utf-8', newline='\n')


projects, locators, archives = [], [], []
for index in range(12):
    project_id = f'project-{index:02}'
    repository = root / project_id
    repository.mkdir()
    run(['git', 'init', '--quiet', '--initial-branch=main', str(repository)])
    run(['git', 'config', 'user.name', 'Synthetic benchmark'], repository)
    run(['git', 'config', 'user.email', 'benchmark@example.invalid'], repository)
    count = 7 if index < 6 else 6
    areas = []
    for area_index in range(count):
        folder = f'area-{area_index}'
        areas.append({'id': folder, 'include': [folder + '/**']})
        write(repository / folder / 'Area.csproj', '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
              '<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n')
        for file_index in range(4):
            write(repository / folder / f'Body{file_index}.cs',
                  f'namespace Project{index}.Area{area_index}; public class Body{file_index} {{\n'
                  + ''.join(f' public int Compute{method}(int x) => x > {method} ? x + {method} : x - {method};\n'
                            for method in range(20)) + '}\n')
    areas.append({'id': 'support', 'include': ['**']})
    write(repository / 'README.md', f'# Synthetic project {index}\n')
    write(repository / '.hidden', f'project {index}\n')
    write(repository / '.gitignore', 'ignored.cs\n')
    run(['git', 'add', '--all'], repository)
    environment = dict(os.environ, GIT_AUTHOR_DATE='2026-01-15T12:00:00Z', GIT_COMMITTER_DATE='2026-01-15T12:00:00Z')
    run(['git', 'commit', '--quiet', '-m', 'synthetic snapshot'], repository, environment)
    projects.append({'id': project_id, 'ref': 'main', 'areas': areas})
    locators.append({'id': project_id, 'repositoryPath': str(repository)})
    archives.append((repository, count))
manifest = {'schemaVersion': '1.0.0', 'protocolVersion': 'snapshot-portfolio-manifest/1.0.0',
            'year': 2026, 'timezone': 'UTC', 'profile': 'implementation',
            'snapshotPolicy': 'git-archive/1.0.0', 'baselineConvention': 'january-1-zero', 'projects': projects}
write(root / 'portfolio.json', json.dumps(manifest))
write(root / 'local.json', json.dumps({'schemaVersion': '1.0.0', 'projects': locators}))
command = ['dotnet', cli, 'estimate', 'portfolio', '--manifest', str(root / 'portfolio.json'),
           '--local', str(root / 'local.json'), '--checkpoint', str(root / 'checkpoint'),
           '--as-of', '2026-02-15T12:00:00Z', '--no-rate']
measurements = {}
reports = {}
for label in ['cold', 'warm']:
    started = time.perf_counter()
    reports[label] = json.loads(run(command))
    measurements[label] = {'seconds': time.perf_counter() - started, **reports[label]['telemetry']}
assert reports['cold']['semanticDigest'] == reports['warm']['semanticDigest']
assert reports['warm']['telemetry']['estimatorCalls'] == reports['warm']['telemetry']['exports'] == 0
receipts = {receipt['id']: receipt for receipt in reports['cold']['receipts']}
started = time.perf_counter()
baseline_calls = 0
baseline_archive_bytes = 0
for index, (repository, count) in enumerate(archives):
    data = run(['git', '-c', 'core.autocrlf=false', '-c', 'core.eol=lf', 'archive', '--format=tar', 'HEAD'], repository, binary=True)
    baseline_archive_bytes += len(data)
    whole = root / 'baseline' / str(index) / 'whole' / 'snapshot'
    whole.mkdir(parents=True)
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        for member in archive:
            if not member.isfile():
                continue
            relative = Path(member.name)
            if relative.is_absolute() or '..' in relative.parts:
                raise RuntimeError('Unsafe generated archive path')
            target = whole / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(member).read())
    period = reports['cold']['projects'][index]['periods'][1]
    for area_index in range(-1, count + 1):
        receipt_id = period['wholeReceiptId'] if area_index == -1 else period['areas'][area_index]['receiptId']
        selected = whole if area_index == -1 else root / 'baseline' / str(index) / str(area_index) / 'snapshot'
        if area_index != -1:
            folder = f'area-{area_index}' if area_index < count else None
            for source in whole.rglob('*'):
                if not source.is_file():
                    continue
                relative = source.relative_to(whole)
                if (folder is None and len(relative.parts) == 1) or (folder and relative.parts[0] == folder) or relative.as_posix() == '.gitignore':
                    target = selected / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(source.read_bytes())
        estimated = json.loads(run([args.baseline, 'estimate', str(selected), '--no-rate']))
        assert estimated['totalEffort'] == receipts[receipt_id]['hours'], (index, area_index, estimated['totalEffort'], receipts[receipt_id]['hours'])
        assert estimated['categories'] == receipts[receipt_id]['categories'], (index, area_index)
        baseline_calls += 1
measurements['baseline'] = {'seconds': time.perf_counter() - started, 'estimatorCalls': baseline_calls,
                            'exports': 12, 'materializations': 102, 'archiveBytes': baseline_archive_bytes}
result = {'fixture': {'projects': 12, 'areas': 90, 'sourceFilesPerArea': 4, 'methodsPerFile': 20},
          'os': platform.system(), 'architecture': platform.machine(), 'logicalProcessors': os.cpu_count(),
          'sdk': run(['dotnet', '--version']).strip(), 'baselineVersion': run([args.baseline, 'version']).strip(),
          'measurements': measurements, 'exactRangeAndCategoryParity': True}
write(root / 'measurements.json', json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
