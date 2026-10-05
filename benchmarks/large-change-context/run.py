"""Explicit synthetic complete-context checkpoint; never runs target code."""
import argparse
import datetime
import hashlib
import json
import pathlib
import re
import subprocess
import time


def invoke(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True,
                          encoding="utf-8", **kwargs).stdout.strip()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def csharp(index, revision=0):
    methods = "".join(
        "public int Method%d(int x) { if (x > %d) return x + %d; return x - %d; }\n"
        % (method, method, method, method) for method in range(12))
    return ("namespace Synthetic; public class File%d { %s public int Revision%d() => %d; }\n"
            % (index, methods, revision, revision)).encode("utf-8")


def prepare(directory, shape, commits):
    if directory.exists():
        raise ValueError("Preparation requires a new directory; existing fixtures are never replaced.")
    if commits < 32 or commits > 10_000:
        raise ValueError("Use 32 through 10,000 commits for the 32-head fixture.")
    directory.mkdir(parents=True)
    repo = directory / "synthetic.git"
    invoke(["git", "init", "--bare", str(repo)])
    stream = bytearray()

    def append(value):
        stream.extend(value.encode("utf-8"))

    def file(path, content):
        append("M 100644 inline %s\ndata %d\n" % (path, len(content)))
        stream.extend(content)
        append("\n")

    append("commit refs/heads/main\nmark :1\n"
           "committer Fixture <fixture@example.invalid> 1609459200 +0000\ndata 4\nbase\n")
    file("Synthetic.csproj", b'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
         b'<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    for index in range(10000 if shape == "csharp" else 9000):
        file("src/File%d.cs" % index, csharp(index))
    if shape == "mixed":
        file("web/package.json", b'{"name":"web","dependencies":{"@angular/core":"1.0.0"}}')
        for index in range(7000):
            file("web/File%d.ts" % index,
                 ("export class File%d { run(x: number) { if (x > 2) return x + 1; return 0; } }"
                  % index).encode("utf-8"))
            file("sql/File%d.sql" % index,
                 ("CREATE TABLE table%d (id INT PRIMARY KEY, name TEXT); SELECT * FROM table%d WHERE id > 2;"
                  % (index, index)).encode("utf-8"))
        for index in range(6998):
            file("assets/File%d.txt" % index, ("asset %d\n" % index).encode("utf-8"))
    append("\n")
    for revision in range(1, commits + 1):
        stamp = 1640995200 + revision
        append("commit refs/heads/main\nmark :%d\nauthor Developer <developer@example.invalid> %d +0000\n"
               "committer Fixture <fixture@example.invalid> %d +0000\ndata 6\nchange\nfrom :%d\n"
               % (revision + 1, stamp, stamp, revision))
        file("src/File0.cs", csharp(0, revision))
        if shape == "mixed":
            file("web/File0.ts", ("export class Changed%d { run(x: number) { return x + %d; } }"
                                 % (revision, revision)).encode("utf-8"))
            file("sql/File0.sql", ("CREATE TABLE changed%d (id INT PRIMARY KEY, name TEXT);"
                                  % revision).encode("utf-8"))
        append("\n")
    append("done\n")
    subprocess.run(["git", "-c", "gc.auto=0", "-c", "maintenance.auto=false", "--git-dir", str(repo),
                    "fast-import", "--quiet", "--export-marks=" + str(directory / "marks.txt")],
                   input=bytes(stream), capture_output=True, check=True)
    marks = dict(line.split() for line in (directory / "marks.txt").read_text(encoding="utf-8").splitlines())
    stride = min(64 if shape == "csharp" else 3, (commits - 1) // 31)
    heads = [{"id": "head-%02d" % index, "objectId": marks[":%d" % (commits + 1 - index * stride)]}
             for index in range(32)]
    write_json(directory / "fixture.json", {"shape": shape, "commits": commits, "heads": heads,
               "files": 10001 if shape == "csharp" else 30000})


def fingerprint(repo):
    digest = hashlib.sha256()
    for path in sorted(path for path in repo.rglob("*") if path.is_file()):
        stat = path.stat()
        digest.update((path.relative_to(repo).as_posix() + "\0" + str(stat.st_size)
                       + "\0" + str(stat.st_mtime_ns) + "\n").encode("utf-8"))
        with path.open("rb") as handle:
            while block := handle.read(1024 * 1024):
                digest.update(block)
    return digest.hexdigest()


def semantic(value):
    # FB5325 is the operational reuse diagnostic; retain every other report field.
    if isinstance(value, dict):
        return {key: semantic(item) for key, item in value.items()}
    if isinstance(value, list):
        return [semantic(item) for item in value
                if not (isinstance(item, dict) and item.get("code") == "FB5325")]
    return value


def run(directory, cli, label, selected, timeout):
    fixture = json.loads((directory / "fixture.json").read_text(encoding="utf-8"))
    if not re.fullmatch(r"[A-Za-z0-9._-]{1,64}", label):
        raise ValueError("Use a simple filename label.")
    if not 1 <= selected <= fixture["commits"]:
        raise ValueError("Selected count must be within the prepared history.")
    repo = directory / "synthetic.git"
    manifest = {"schemaVersion": "1.0.0", "selection": {
        "sinceInclusive": "2022-01-01T00:00:00+00:00",
        "untilExclusive": datetime.datetime.fromtimestamp(1640995200 + selected + 1,
                               datetime.timezone.utc).isoformat(),
        "timeZone": "UTC", "dateField": "author", "mergePolicy": "exclude",
        "coauthorPolicy": "include", "intervalSemantics": "since-inclusive-until-exclusive"},
        "contributors": [{"id": "developer", "aliases": ["developer@example.invalid"]}],
        "repositories": [{"id": "synthetic", "repositoryPath": str(repo), "heads": fixture["heads"]}]}
    manifest_path = directory / (label + ".manifest.json")
    output = directory / (label + ".report.json")
    write_json(manifest_path, manifest)
    args = (["dotnet", str(cli)] if cli.suffix.lower() == ".dll" else [str(cli)]) + [
        "change", "portfolio", "--author-period-manifest", str(manifest_path), "--profile",
        "implementation", "--format", "json", "--compact", "--no-rate", "--output", str(output)]
    before = fingerprint(repo)
    started = time.perf_counter()
    timed_out = False
    with (directory / (label + ".stderr.txt")).open("w", encoding="utf-8", newline="\n") as stderr:
        process = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=stderr,
                                   encoding="utf-8")
        try:
            exit_code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            timed_out, exit_code = True, None
    seconds = time.perf_counter() - started
    unchanged = before == fingerprint(repo)
    stderr = (directory / (label + ".stderr.txt")).read_text(encoding="utf-8")
    peaks = re.findall(r"observed-peak-working-set=([0-9.]+) MiB", stderr)
    progress = [{"completed": int(done), "elapsedMs": float(elapsed), "workingSetMib": float(memory)}
                for done, elapsed, memory in re.findall(
                    r"static-analysis progress ([0-9]+)/[0-9]+ selected changes;[^\n]*?"
                    r"elapsed=([0-9.]+) ms; working-set=([0-9.]+) MiB", stderr)]
    phases = {name: float(milliseconds) for name, milliseconds in re.findall(
        r"portfolio phase ([a-z-]+) ([0-9.]+) ms", stderr)}
    result = {"shape": fixture["shape"], "files": fixture["files"], "changes": selected,
              "heads": 32, "seconds": round(seconds, 3), "exitCode": exit_code,
              "timedOut": timed_out, "targetUnchanged": unchanged,
              "progressPeakWorkingSetMib": max(map(float, peaks)) if peaks else None,
              "progress": progress, "overlappingPhaseMilliseconds": phases}
    if exit_code == 0:
        report = json.loads(output.read_text(encoding="utf-8"))
        result["items"] = len(report["items"])
        result["semanticSha256"] = hashlib.sha256(json.dumps(semantic(report), sort_keys=True,
                                      separators=(",", ":")).encode("utf-8")).hexdigest()
        result["reuse"] = [item["message"] for item in report["diagnostics"] if item["code"] == "FB5325"]
    write_json(directory / (label + ".measurement.json"), result)
    print(json.dumps(result))
    if not unchanged:
        raise RuntimeError("The fixture changed during analysis.")
    if exit_code not in (0, None):
        raise RuntimeError("Analysis failed; inspect the local stderr record.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["prepare", "run"])
    parser.add_argument("--directory", type=pathlib.Path, required=True)
    parser.add_argument("--shape", choices=["csharp", "mixed"], default="mixed")
    parser.add_argument("--commits", type=int, default=128)
    parser.add_argument("--cli", type=pathlib.Path)
    parser.add_argument("--label", default="candidate")
    parser.add_argument("--selected", type=int, default=16)
    parser.add_argument("--timeout", type=int, default=900)
    options = parser.parse_args()
    directory = options.directory.resolve()
    if options.action == "prepare":
        prepare(directory, options.shape, options.commits)
    elif options.cli is None:
        parser.error("run requires --cli")
    else:
        run(directory, options.cli.resolve(), options.label, options.selected, options.timeout)
