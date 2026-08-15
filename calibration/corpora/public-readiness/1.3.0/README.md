# Logical-capability v0.3 blind validation opening

## Status

**The complete nine-family strict-blind validation is frozen and the sole
challenger is rejected.** The opening and label compilation succeeded with no
failure or contamination record. Labels were committed before candidate values
were generated. The one-shot selection then evaluated the seed and exact
`logical-capability/0.3.0` challenger, applied every precommitted gate, rejected
the challenger, and left the test partition sealed.

This checkpoint adds a dedicated one-shot `validation-open` path. Before any
network request or output creation, it verifies the exact candidate freeze, its
complete referenced-artifact chain, the sampling and custody records, the selected
checkout commit, and the complete three-stratum by three-size validation matrix.
It refuses pre-existing workspace, packet, or opening-record paths.

## Frozen inputs

| Input | Normalized SHA-256 |
| --- | --- |
| Candidate manifest `1.2.0` | `sha256:206b3955d53af9902996b588e9255ab9396e7b7624731a6d6e09896ce5026f23` |
| Sampling plan `0.1.0` | `sha256:c4c5f0026112b0d495e79e9ca7a5b3d03a763710db75360d02fd333afd282aa1` |
| Reproduction manifest `0.2.0` | `sha256:0d36e4178a65a523fd4705e6bc353ec5365f565b3b27f52692d7bed2e47b5159` |
| Holdout custody `0.2.0` | `sha256:09bbd23d6a6b5aca4094ce9651d9016dc15bbc6cb860e7f73d1c242a1fafdd50` |

The candidate set is exactly the `seed-rules/0.4.0` baseline and the sole
`logical-capability/0.3.0` challenger. The frozen primary selection metric is
repository-total expected WAPE, followed only by the already-recorded tolerance,
eligibility, and tie-break rules. Nothing in this checkpoint changes that rule.

## Validation matrix

| Stratum | Size | Repository | Eligible files | Pinned commit |
| --- | --- | --- | ---: | --- |
| .NET | Small | `Cysharp/ConsoleAppFramework` | 53 | `6f40d3e520bbd81b86ee9bf1f4cb193b0b2b88be` |
| .NET | Medium | `spectreconsole/spectre.console` | 450 | `0acc92fada6c42f13984e79c2b5f3d993bdfb099` |
| .NET | Large | `dotnet/efcore` | 5,766 | `5e8896500ed9c91f53481e6825ddc42528180184` |
| JavaScript/TypeScript | Small | `sindresorhus/ky` | 53 | `3419113b48e034fdcf8fa6bd3be3da7b3d0d758f` |
| JavaScript/TypeScript | Medium | `axios/axios` | 250 | `d19040bda7a8be2f82c3c6e1a5bc03917daee39a` |
| JavaScript/TypeScript | Large | `nrwl/nx` | 5,354 | `7e5b452eb771d66097a1aae4192b31d9a2158789` |
| Mixed | Small | `jasontaylordev/CleanArchitecture` | 169 | `68c026833f73f3eca5aaab4bccc83d01593b637d` |
| Mixed | Medium | `ElectronNET/Electron.NET` | 364 | `7fe3f9b5b64134c07bee70a8cc97cbcdc4f6ed04` |
| Mixed | Large | `OrchardCMS/OrchardCore` | 8,058 | `1da359f83ac75c105a67d30fbc8edc5fd6025764` |

For each family, the opener re-verifies the pinned commit, tree, complete blob
inventory, archive digest and size, source-shape metric, and license identity
against the previously frozen custody record. It then runs only the shipped seed
estimator and emits a strict-blind authoring packet with all estimate guidance
removed. The ignored workspace retains evidence needed for review; the checked-in
opening record retains digests and provenance without local paths or source text.

## One-shot execution

The one-shot opening ran from implementation commit
`efad050c3b0985ec4d8fdf9de7673222103a12fd`:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll validation-open `
  --repository-root . `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --reproduction-manifest calibration/corpora/public-readiness/0.2.0.reproduction-manifest.json `
  --custody calibration/corpora/public-readiness/0.2.0.holdout-custody.json `
  --candidate-manifest calibration/corpora/public-readiness/1.2.0/1.2.0.candidate-manifest.json `
  --source-commit efad050c3b0985ec4d8fdf9de7673222103a12fd `
  --workspace artifacts/calibration/public-readiness-v03-validation `
  --cli src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll `
  --packets calibration/corpora/public-readiness/1.3.0/authoring-packets `
  --output calibration/corpora/public-readiness/1.3.0/1.3.0.validation-opening.json
```

The opening implementation does not accept a test-family selector or a challenger
output path. A family failure writes an explicit failed-closed opening record and
stops. A successful opening must contain exactly nine verified validation records
and nine blind packets.

## Opening result

The opening completed in approximately 177 seconds on the recorded workstation.
It reproduced all nine prior archive, tree, blob, size, and license custody
records; generated 2,747 blind review targets; and wrote exactly nine packets.
Every packet reports `candidateVisibility: blind`, a null candidate total, empty
candidate categories, and null candidate guidance on every target. The opening
manifest records zero failures, zero contaminated families, no test access, and no
validation or test candidate output.

The packet digests in `1.3.0.validation-opening.json` reproduce byte-for-byte.
A privacy scan found no local Windows or Unix home path, source excerpt field, or
secret value. Path tokens containing `home`, `password`, or `secrets` are public
repository-relative evidence identities, not machine paths or configured values.

## Frozen blind review

`../1.3.0.validation-review-plan.json` contains all 2,747 capability judgments
across the nine validation families. The byte-identical ignored draft and final
artifact have normalized SHA-256
`sha256:f68e1e590d60547e657f551a883291f153b45d31022187e7dd064ceef55b9cd1`.

The single disclosed host-AI teacher assigned `46,045.50` expected EHE with
`37,809.00/56,110.25` low/high bounds. There are 111 explicit `0/0/0`
rubric-qualified exclusions for test/benchmark/build double counting or false
semantic signals. There are 538 cohesive targets above the ordinary eight-hour
review size, each with an explicit size exception; this is 19.6% of the cohort,
consistent with the packet's repository/package-level decomposition rather than a
candidate-derived partition.

| Repository | Targets | Exclusions | Reviewed expected |
| --- | ---: | ---: | ---: |
| `sindresorhus/ky` | 15 | 1 | 536.75 h |
| `axios/axios` | 37 | 10 | 966.75 h |
| `nrwl/nx` | 715 | 7 | 14,644.75 h |
| `Cysharp/ConsoleAppFramework` | 44 | 3 | 274.00 h |
| `spectreconsole/spectre.console` | 51 | 6 | 1,009.25 h |
| `dotnet/efcore` | 275 | 61 | 13,535.00 h |
| `jasontaylordev/CleanArchitecture` | 82 | 3 | 440.50 h |
| `ElectronNET/Electron.NET` | 94 | 6 | 699.25 h |
| `OrchardCMS/OrchardCore` | 1,434 | 14 | 13,939.25 h |

The checked-in review policy records repository-specific judgments from the blind
packets, verified evidence, and pinned public source. It accepts no estimate,
model, or candidate-report argument. The review plan records `partition:
validation`, `teacher-estimate` maturity, the public revision/license lineage, and
the same disclosed teacher identity used for development.

After the review plan was frozen, `../1.3.0.validation-corpus.json` compiled all
nine matching immutable seed-report lineages without changing any judgment. Its
normalized SHA-256 is
`sha256:dad8ba8c4af5162522bc6fbfc7ee1733eb0ace0f6cdd3b185de98ebb531c3be7`.
The compact corpus contains exactly nine validation records and 2,747 targets,
retains all 111 zero decisions, and contains no local path or source excerpt.

## Frozen validation selection

The one-shot evaluator was committed and pushed at
`96d9d37e62348fb259c92ebffcb43c8b6ff2e155` before any challenger validation
value was generated. It reverified the exact manifest, model, opening, corpus,
seed evidence, and estimate lineage; required fresh output paths; accepted no test
input; projected all nine validation records in memory; and wrote the candidate
outputs, two evaluation reports, and decision exactly once.

The challenger substantially improves aggregate point agreement, but it does not
pass the complete policy:

| Metric | Seed | Challenger | Gate |
| --- | ---: | ---: | --- |
| Repository expected WAPE | 0.2279 | 0.0940 | pass; 58.75% improvement |
| Absolute aggregate bias | 0.2235 | 0.0634 | pass |
| Median repository absolute error | 127.7500 h | 134.2769 h | **fail**; worse than seed |
| Repository expected-point coverage | 1.0000 | 0.6667 | **fail**; below 0.80 |
| Mean repository normalized width | 1.4832 | 0.4110 | pass |
| P90 repository normalized width | 1.8232 | 0.6044 | pass |
| Ordinary-family error pass rate | n/a | 0.8889 | **fail**; below 0.90 |
| Matched-target expected coverage | n/a | 0.7150 | **fail**; below 0.75 |
| Matched-target normalized width | n/a | 0.8452 | **fail**; above 0.75 |

The pooled material-category WAPE improves from `0.3358` to `0.1577`, but the
material-category gate still fails because eight individual categories regress
the seed by more than `0.05` WAPE or exceed the absolute-bias limit. Overall, 15
validation gates pass and six fail. All four boundary gates and all 12 previously
frozen operational gates pass.

## Post-decision residual diagnostic

After rejection, the opened validation labels were used only as development
diagnostics. `eh calibration diagnose` compared the same nine immutable challenger
reports with the frozen corpus. It changed no report, candidate, prior, gate, or
decision and did not access test.

| Diagnostic | Result |
| --- | ---: |
| Reviewed / candidate expected | `46,045.5000 / 48,964.8599 h` |
| Net candidate-minus-reviewed expected | `+2,919.3599 h` |
| Gross repository / category / component disagreement | `4,328.0045 / 7,803.2480 / 9,771.0281 h` |
| Reviewed expected covered / candidate too high / candidate too low | `6 / 1 / 2` repositories |
| Symmetric challenger repository ranges | `0 / 9` |
| Raw candidate-to-reviewed repository-width correlation | `0.9327` |
| Expected-normalized candidate-to-reviewed width correlation | `-0.1291` |
| Reviewed components / candidate leaves | `2,747 / 15,609` |
| Explicit size exceptions / reviewed targets above eight hours | `649 / 538` |
| Low / expected / high reconciliation delta | `0.0000 / 0.0000 / 0.0000 h` |

The largest repository net residual is `+1,888.8888 h`, while its component-level
gross disagreement is `2,720.5070 h`; `831.6182 h` cancels between over- and
underestimated components. Its largest reviewed target is `2,868.7500 h` versus
`3,087.2245 h` candidate and maps to 1,202 normally small candidate leaves. The
reviewed target's existing size exception already says candidate-hidden blind
review preserved a cohesive aggregate that later review may decompose. This is
why issue #137 requires both largest-first residual ranking and leaf expansion:
aggregate agreement alone cannot reveal which evidence-backed pieces are off.

The full compact diagnostic is generated under ignored `artifacts/` because it
contains all 15,609 leaf projections and is intentionally not added as a large
derived repository artifact. The versioned command, schema, semantic validation,
memory-only reconciliation fixture, process-level CLI fixture, and this frozen
summary are checked in. The diagnostic is reproducible from the same ignored
candidate outputs used by the already-frozen selection, and those outputs remain
bound by their checked-in candidate evaluation digest.

The seed and candidate evaluation digests are
`sha256:83d30549c5b0bd86f7246b36a052fe97b36d558ec80e8d2374652f5e463d4678`
and
`sha256:39a148e912be0c19e9ba565eeb6e1f7a43e3fec8026159d889d5f1451e43d86a`.
The selection record digest is
`sha256:fff040cdb13aba610c410d8e101e7770a99e0365d4d0ccf3b915f7de99ab5c56`;
commit `07880cf84a9dfbe84a407875a60abbeea81f9afd` freezes it unchanged.

## Next boundary

`logical-capability/0.3.0` is retired without test disclosure. It must not be
retuned and reevaluated against these validation labels as though they remained
held out. Any successor requires a new candidate identity, a fresh blind
validation boundary, and a new finite manifest and selection decision. The
existing test partition remains sealed; no test source body, label, seed output, or
challenger output was accessed, and `seed-rules/0.4.0` remains the shipped
estimator and mandatory fallback.
