"""Native CLI before/after checkpoint with a synthetic provider; never contacts GitHub.

Requires Python 3.10+, .NET 10, and Git. Builds only the benchmark provider;
does not build or execute the synthetic target repository. Results contain no
machine paths. Object caches are prepopulated; the first run has cold metadata
and no evidence checkpoint; subsequent runs reuse metadata and Git objects,
while analysis runs again. No timing thresholds are asserted.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import statistics
import subprocess
import time
import uuid


def command(args, cwd, env=None):
    result = subprocess.run(
        [str(arg) for arg in args], cwd=cwd, env=env,
        text=True, encoding="utf-8", capture_output=True, check=False,
    )
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {result.stderr}\n{result.stdout}")
    return result.stdout.strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--delay-ms", type=int, default=100)
    parser.add_argument("--email-aliases", action="store_true")
    parser.add_argument("--open-pr", action="store_true")
    options = parser.parse_args()
    if options.open_pr and not options.email_aliases:
        parser.error("open-pr requires email-aliases")
    if not 1 <= options.runs <= 10 or not 0 <= options.delay_ms <= 1000:
        parser.error("runs must be 1..10 and delay-ms 0..1000")
    root = Path(__file__).resolve().parent.parent
    workspace = root / "artifacts" / "today-discovery" / uuid.uuid4().hex
    workspace.mkdir(parents=True)
    provider = workspace / "provider"
    provider.mkdir()
    shutil.copyfile(root / "benchmarks/TodayDiscoveryFixture/Program.cs", provider / "Program.cs")
    (provider / "gh.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>'
        '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
        '</PropertyGroup></Project>', encoding="utf-8")
    command(["dotnet", "build", provider / "gh.csproj", "-c", "Release",
             "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
             "-o", provider / "bin"], root)
    env = os.environ.copy()
    env.update({
        "PATH": str(provider / "bin") + os.pathsep + env["PATH"],
        "GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": str(workspace / "empty-git-config"),
        "GIT_TERMINAL_PROMPT": "0", "DOTNET_NOLOGO": "1",
        "EH_BENCHMARK_FIXTURE": str(workspace / "fixture.json"),
    })
    (workspace / "empty-git-config").write_text("", encoding="utf-8")
    # Inherited application configuration must not change this synthetic scope.
    env.pop("EFFORTHOURS_ENGINEERING_SCOPE_PROFILE", None)
    repository = workspace / "source"
    repository.mkdir()
    command(["git", "init", "-b", "main"], repository, env)
    command(["git", "config", "user.name", "selected"], repository, env)
    command(["git", "config", "user.email", "selected@example.test"], repository, env)
    (repository / "Demo.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>', encoding="utf-8")
    commits = []
    for index in range(4):
        timestamp = "2026-09-16T12:00:00Z" if index == 0 else f"2026-09-17T10:0{index}:00Z"
        if index:
            (repository / f"Feature{index}.cs").write_text(
                f"namespace Demo; public sealed class Feature{index} {{ "
                f"public int Adjust(int value) => value > {index} ? value + {index} : 0; }}\n",
                encoding="utf-8")
        command(["git", "add", "."], repository, env)
        commit_env = dict(env, GIT_AUTHOR_DATE=timestamp, GIT_COMMITTER_DATE=timestamp)
        command(["git", "commit", "-m", f"Synthetic feature {index}"], repository, commit_env)
        if index:
            oid = command(["git", "rev-parse", "HEAD"], repository, env)
            parent = command(["git", "rev-parse", "HEAD^"], repository, env)
            identity = {"name": "selected", "email": "selected@example.test", "date": timestamp}
            commits.insert(0, {
                "sha": oid, "parents": [{"sha": parent}], "author": {"login": "selected"},
                "commit": {"author": identity, "committer": identity, "message": f"Feature {index}"},
            })
    open_commit = None
    if options.open_pr:
        command(["git", "switch", "-c", "open-work"], repository, env)
        command(["git", "config", "user.email", "42+selected@users.noreply.github.com"], repository, env)
        command(["git", "config", "user.name", "Different Git Display Name"], repository, env)
        (repository / "OpenFeature.cs").write_text(
            "namespace Demo; public sealed class OpenFeature { public bool Enabled => true; }\n", encoding="utf-8")
        command(["git", "add", "."], repository, env)
        stamp = "2026-09-17T11:00:00Z"
        command(["git", "commit", "-m", "Unmerged synthetic feature"], repository,
                dict(env, GIT_AUTHOR_DATE=stamp, GIT_COMMITTER_DATE=stamp))
        open_commit = json.loads(json.dumps(commits[0]))
        open_commit["sha"] = command(["git", "rev-parse", "HEAD"], repository, env)
        open_commit["parents"] = [{"sha": commits[0]["sha"]}]
        for role in ["author", "committer"]:
            open_commit["commit"][role] = {"name": "Different Git Display Name",
                "email": "42+selected@users.noreply.github.com", "date": stamp}
    (workspace / "fixture.json").write_text(json.dumps({
        "delayMilliseconds": options.delay_ms, "commits": commits,
        "emailAliases": options.email_aliases, "openCommit": open_commit,
    }), encoding="utf-8")
    result = {
        "protocol": "today-discovery-benchmark/1.1.0" if options.email_aliases else "today-discovery-benchmark/1.0.0", "synthetic": True,
        "platform": platform.system(), "sdk": command(["dotnet", "--version"], root),
        "git": command(["git", "--version"], root),
        "repositories": 178, "selectedChanges": 4 if options.open_pr else 3, "activeRepositories": 1,
        "openPullRequestFixture": options.open_pr,
        "incompleteHistoryRepositoryCount": 1, "delayMillisecondsPerProviderProcess": options.delay_ms,
        "warmSamples": options.runs, "measurements": [],
    }
    lanes = [("before", options.before.resolve()), ("after", options.after.resolve())]
    if options.email_aliases:
        lanes.append(("after-fallback", options.after.resolve()))
    for label, cli in lanes:
        version = command(["dotnet", cli, "--version"], workspace, env)
        assembly_hash = hashlib.sha256((cli.parent / "EffortHours.Change.dll").read_bytes()).hexdigest()
        for author in (["two-emails"] if options.email_aliases else ["@me", "selected"]):
            name = label + ("-emails" if options.email_aliases else "-me" if author == "@me" else "-explicit")
            run_root = workspace / name
            run_root.mkdir()
            cache = run_root / "cache"
            bare = cache / "benchmark-owner/repository-0.git"
            bare.parent.mkdir(parents=True)
            command(["git", "clone", "--bare", "--no-hardlinks", repository, bare], workspace, env)
            run_env = dict(env, EFFORTHOURS_REPOSITORY_CACHE=str(cache),
                           EFFORTHOURS_PROVIDER_CACHE=str(run_root / "metadata"))
            run_env["EH_BENCHMARK_FORCE_PR_FALLBACK"] = "1" if label == "after-fallback" else "0"
            author_args = ["--author", "selected@example.test", "--author", "42+selected@users.noreply.github.com"] \
                if options.email_aliases else ["--author", author]
            samples = []
            for sample in range(options.runs + 1):
                start = time.perf_counter()
                output = command([
                    "dotnet", cli, "change", "today", "--owner", "benchmark-owner",
                    *author_args, "--timezone", "UTC", "--scope", "engineering",
                    "--include-open-prs", "--capacity-hours", "8", "--no-rate", "--format", "json",
                    "--generated-at", "2026-09-17T12:00:00Z",
                ], run_root, run_env)
                elapsed = time.perf_counter() - start
                report = json.loads(output)
                (run_root / f"report-{sample}.json").write_text(output, encoding="utf-8")
                discovery = report["discovery"]
                expected_selected = 4 if options.open_pr and label != "before" else 3
                if (report["status"] != "complete" or discovery["activeRepositoryCount"] != 1
                        or report["scopeSummary"]["identitySelectedCommitCount"] != expected_selected
                        or report["scopeProfile"]["source"] != "bundled"):
                    raise RuntimeError("Synthetic selection parity failed")
                series = next(item for item in report["series"] if item["kind"] == "portfolio")
                observation = {
                    "selectedChanges": report["scopeSummary"]["identitySelectedCommitCount"],
                    "openPullRequestHeads": discovery["openPullRequestHeadCount"],
                    "cacheState": "metadata-cold-object-warm" if sample == 0 else "warm",
                    "wallSeconds": round(elapsed, 4),
                    "providerQueries": discovery["providerQueryCount"],
                    "providerPages": discovery["providerPageCount"],
                    "providerProcesses": discovery["providerProcessCount"],
                    "metadataCacheHit": discovery["providerMetadataCacheHit"],
                    "providerDiagnostics": discovery.get("providerDiagnostics"),
                    "acquiredBytes": discovery["acquiredBytes"],
                    "semanticDigest": report["verification"]["semanticDigest"],
                    "effort": series["totalEffort"],
                    "ratio": series["totalCapacityRatio"],
                    "phases": report["execution"]["phaseTimings"],
                }
                samples.append(observation)
                print(f"{name} {observation['cacheState']}: {elapsed:.3f}s, "
                      f"{discovery['providerQueryCount']} queries", flush=True)
            result["measurements"].append({
                "label": label, "authorForm": "two-emails" if options.email_aliases else "viewer" if author == "@me" else "explicit-login",
                "cliVersion": version, "changeAssemblySha256": assembly_hash,
                "warmMedianSeconds": round(statistics.median(item["wallSeconds"] for item in samples[1:]), 4),
                "samples": samples,
            })
    comparable = [measurement for measurement in result["measurements"]
                  if not options.open_pr or measurement["label"] != "before"]
    efforts = [sample["effort"] for measurement in comparable for sample in measurement["samples"]]
    if any(effort != efforts[0] for effort in efforts):
        raise RuntimeError("Before/after EHE differs")
    stable_digests = {
        sample["semanticDigest"] for measurement in result["measurements"]
        if (measurement["label"].startswith("after") or measurement["authorForm"] == "viewer"
            or options.email_aliases and not options.open_pr)
        for sample in measurement["samples"]
    }
    if len(stable_digests) != 1:
        raise RuntimeError("Viewer semantic digest or explicit-login parity differs")
    if any(not sample["metadataCacheHit"]
           for measurement in result["measurements"] if measurement["label"].startswith("after")
           for sample in measurement["samples"][1:]):
        raise RuntimeError("Candidate warm metadata was not reused")
    options.output.parent.mkdir(parents=True, exist_ok=True)
    options.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
