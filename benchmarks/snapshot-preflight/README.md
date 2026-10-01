# Snapshot preflight integration checkpoint

Protocol: `snapshot-preflight-checkpoint/1.0.0`. Public synthetic fixture under the
repository MIT license; no consumer source or authored product studies are used.

The October 1, 2026 checkpoint compares installed `0.10.0-alpha.25` with this PR's
working-tree build. The CLI producer version is unchanged in this fix PR. Both
use .NET SDK 10.0.203 on Windows 11 Pro 10.0.26200, AMD Ryzen 9 5900X, 24 logical
processors, and approximately 128 GiB RAM. This is exploratory desktop telemetry:
some baseline samples overlapped development validation/builds. The recorded
dispersion and that interference prevent a general latency or hardware-scaling
claim. No ordinary CI test gates these times or working-set samples.

One generated immutable tree per project is selected in nine months. There are
twelve projects, ninety reviewed areas, and 1,201 files per project (14,412 total).
The checkpoint intentionally stresses repeated immutable selection and the
selector-search compilation defect. Each command is a fresh process, uses one
project session, and has no receipts/checkpoints. Fixture generation is outside
the command timing. Three runs per implementation are recorded in
[measurements.json](measurements.json).

| Observation | alpha.25 | Candidate |
| --- | ---: | ---: |
| Wall seconds, samples | 161.178 / 166.416 / 178.403 | 12.357 / 10.129 / 12.997 |
| Median wall seconds | 166.416 | 12.357 |
| Inventory reads | Not instrumented | 12 |
| Area planning calls | Not instrumented | 12 |
| Selector compilations | Not instrumented | 90 |
| Within-run planning/inventory hits | Not instrumented | 192 |
| Exports / estimator calls / checkpoint writes | 0 / 0 / 0 | 0 / 0 / 0 |

The median is 13.47 times lower on this fixture. All pins, periods, statuses,
cutoffs, public scope digests, and path-only planning dispositions are identical
after removing measurement epoch, semantic digest, and operational telemetry.
The new source implementation conservatively changes the measurement identity;
the comparison does not assert receipt compatibility across that change. The
script also checks that no checkpoint directory is created. This is a narrow
work-elimination result, not the private consumer's measured runtime or a promise
for distinct historical trees, attribute-heavy repositories, or interactive work.

Reproduce with an installed alpha.25 tool and a freshly built candidate DLL:

```text
python benchmarks/snapshot-preflight/profile.py --baseline <alpha.25-eh> --candidate <efforthours.dll> --runs 3 --files-per-project 1200 --output <private-results.json>
```

The script generates its own temporary repositories, never executes their code,
uses UTF-8 subprocess/file I/O, hides Windows children, checks cold planning
equivalence, and removes only its own temporary fixture directory. Run on an idle
host for a cleaner replication. Unit/process regressions independently gate
operation counts, exact allocation, privacy, offline/read-only safety, and cold/
warm semantics.
