"""Public synthetic checkpoint; no target code is executed and no timing gates CI.

python benchmarks/snapshot-preflight/profile.py --baseline <alpha.25-eh> --candidate <eh>
Candidate can instead be a DLL; the runner then invokes dotnet.
"""
import argparse
import json
import os
from pathlib import Path
import statistics
import subprocess
import tempfile
import time


def run(command, cwd=None, env=None):
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    return subprocess.run(command, cwd=cwd, env=env, text=True, encoding="utf-8",
                          capture_output=True, check=True, creationflags=flags)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--files-per-project", type=int, default=1200)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    if not 1 <= args.runs <= 5 or not 100 <= args.files_per_project <= 10000:
        parser.error("runs must be 1..5 and files-per-project 100..10000")
    with tempfile.TemporaryDirectory(prefix="eh-preflight-profile-") as temporary:
        root = Path(temporary)
        projects, local = [], []
        for project in range(12):
            repository = root / f"source-{project}"
            repository.mkdir()
            run(["git", "init", "--quiet", "--initial-branch=main", str(repository)])
            for file in range(args.files_per_project):
                path = repository / f"module-{file % 8}" / f"body-{file:05}.cs"
                path.parent.mkdir(exist_ok=True)
                path.write_text(f"public class Body{file} {{ public int Value => {file}; }}\n", encoding="utf-8", newline="\n")
            (repository / "README.md").write_text("# Public synthetic checkpoint\n", encoding="utf-8", newline="\n")
            run(["git", "add", "--all"], cwd=repository)
            env = dict(os.environ, GIT_AUTHOR_NAME="Synthetic", GIT_AUTHOR_EMAIL="synthetic@example.invalid",
                       GIT_COMMITTER_NAME="Synthetic", GIT_COMMITTER_EMAIL="synthetic@example.invalid",
                       GIT_AUTHOR_DATE="2026-01-15T12:00:00Z", GIT_COMMITTER_DATE="2026-01-15T12:00:00Z")
            run(["git", "commit", "--quiet", "-m", "synthetic snapshot"], cwd=repository, env=env)
            count = 6 if project < 6 else 7
            areas = [{"id": f"area-{i}", "include": [f"module-{i}/**"]} for i in range(count)]
            areas.append({"id": "support", "include": ["**"]})
            projects.append({"id": f"project-{project}", "ref": "main", "areas": areas})
            local.append({"id": f"project-{project}", "repositoryPath": str(repository)})
        manifest = {"schemaVersion": "1.0.0", "protocolVersion": "snapshot-portfolio-manifest/1.0.0",
                    "year": 2026, "timezone": "UTC", "profile": "implementation",
                    "snapshotPolicy": "git-archive/1.0.0", "baselineConvention": "january-1-zero", "projects": projects}
        (root / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8", newline="\n")
        (root / "local.json").write_text(json.dumps({"schemaVersion": "1.0.0", "projects": local}), encoding="utf-8", newline="\n")
        samples, reference = {}, None
        for label, executable in [("baseline", args.baseline), ("candidate", args.candidate)]:
            command = ["dotnet", executable] if executable.endswith(".dll") else [executable]
            version = run(command + ["version"]).stdout.strip()
            measurements = []
            for iteration in range(args.runs):
                before = time.perf_counter()
                result = run(command + ["estimate", "portfolio", "--manifest", str(root / "manifest.json"),
                    "--local", str(root / "local.json"), "--checkpoint", str(root / "checkpoint"),
                    "--as-of", "2026-09-30T20:00:00Z", "--preflight", "--no-rate"])
                elapsed = time.perf_counter() - before
                report = json.loads(result.stdout)
                telemetry = report.pop("telemetry")
                assert telemetry["estimatorCalls"] == telemetry["exports"] == 0
                for field in ["semanticDigest", "measurementEpoch"]:
                    report.pop(field)
                if reference is None:
                    reference = report
                assert reference == report, "Cold planning result changed"
                assert not (root / "checkpoint").exists(), "Preflight wrote checkpoints"
                measurements.append({"wallSeconds": elapsed, "telemetry": telemetry})
                print(f"{label} {iteration + 1}: {elapsed:.3f}s", flush=True)
            samples[label] = {"version": version, "runs": measurements,
                              "medianWallSeconds": statistics.median(m["wallSeconds"] for m in measurements)}
        output = {"protocol": "snapshot-preflight-checkpoint/1.0.0", "projects": 12, "areas": 90,
                  "filesPerProject": args.files_per_project + 1, "selectedMonthsPerProject": 9,
                  "equivalentPlanning": True, "checkpointWrites": 0, "samples": samples}
        Path(args.output).write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
