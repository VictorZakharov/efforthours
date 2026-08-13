# Public-readiness reproduction checkpoint

## Status

`efforthours-public-readiness/0.2.0` reproduces the 33 source identities frozen in
the `0.1.0` sampling plan. It verifies each pinned commit, complete Git tree, blob,
source-size measurement, and license before publishing any calibration artifact.
Repository source, archives, evidence bundles, and estimates remain under ignored
`artifacts/` paths and are not committed.

The checkpoint publishes:

- a digest manifest for all 33 reproduced families;
- 15 development-only `calibration-authoring/0.2.0` strict-blind packets containing
  2,030 capability summaries;
- source-custody status for nine validation and nine test families, all marked
  `withheld-not-run` and `not-authored`; and
- a first three-family development teacher slice with one .NET, one
  JavaScript/TypeScript, and one mixed family.

It does not admit a repository model. The labels are single-host-AI
`teacher-estimate` weak supervision, have no independent correction, and are
development diagnostics only.

## Reproduction boundary

The maintainer-only `EffortHours.RepositoryCalibration` tool uses `gh api` and a
commit-pinned codeload ZIP. It verifies the commit-to-tree link and every extracted
file against its exact Git blob object ID and byte length. GitHub archives can
normalize line endings or omit export-filtered entries; when that occurs, the tool
loads only the already-pinned blob by object ID, verifies its Git-object hash, and
writes those exact bytes. It never weakens the tree check to accept archive bytes.

Only `partition: development` snapshots are passed to `eh scan`, `eh estimate`,
and `eh calibration scaffold`. Validation and test snapshots are source-verified
but never passed to EffortHours by this command. The ordinary scanner remains
offline and network-free; network use belongs only to this explicit maintainer
reproduction workflow.

From a release build with an authenticated GitHub CLI:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --workspace artifacts/calibration/public-readiness-0.2.0 `
  --cli src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll `
  --packets calibration/corpora/public-readiness/0.2.0/authoring-packets `
  --output calibration/corpora/public-readiness/0.2.0.reproduction-manifest.json `
  --custody calibration/corpora/public-readiness/0.2.0.holdout-custody.json
```

The digest manifest records 112,451 verified blobs and 1,578,210,924 verified
blob bytes across the 33 trees. Archive SHA-256s are audit metadata; Git tree and
blob identities remain the authoritative source boundary.

## Strict-blind authoring

`calibration-authoring/0.2.0` groups source partitions into one capability summary.
In blind mode it hides:

- repository and category totals;
- target hours, confidence, and candidate explanations;
- source work-item IDs and candidate-derived partition counts; and
- professionalization-gap work-item IDs.

The committed packets retain capability/category/scope identity, evidence IDs,
assumptions, exclusions, uncertainty, candidate model identity, and the exact
candidate digest needed for later compilation. Artifact tests verify that every
hidden field is absent or null and that every packet SHA-256 matches the
reproduction manifest.

## First balanced development slice

The frozen review plan covers:

| Primary stratum | Family | Reviewed capabilities | Exact exclusions |
| --- | --- | ---: | ---: |
| .NET | `CarterCommunity/Carter` | 68 | 6 |
| JavaScript/TypeScript | `tj/commander.js` | 13 | 0 |
| Mixed | `oqtane/oqtane.framework` | 88 | 1 |

The review command accepts no estimate path. It verifies packet and evidence
digests, then applies the checked-in transparent teacher policy to the three
strict-blind capability sets. Large cohesive capabilities use explicit size
exceptions because candidate-derived partition counts were intentionally hidden.
Only after the plan existed were the three exact source estimates unlocked for
compilation and development evaluation.

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  review-development `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --manifest calibration/corpora/public-readiness/0.2.0.reproduction-manifest.json `
  --packets calibration/corpora/public-readiness/0.2.0/authoring-packets `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --output calibration/corpora/public-readiness/0.2.0.development-review-plan.json
```

The development-only comparison is:

| Family | Candidate expected | Reviewed expected | Reviewed interval covered? |
| --- | ---: | ---: | --- |
| Carter | 352.25 h | 365.25 h | yes |
| Commander | 393.00 h | 494.50 h | yes |
| Oqtane | 2,098.25 h | 3,920.75 h | no |

Across the three records, reviewed expected EHE is 4,780.50 hours versus 2,843.50
candidate hours. Expected aggregate bias is `-0.4052` and repository expected-point
coverage is `0.6667`. The mixed application is a material underestimation signal;
the result is not a fitted correction, validation score, test score, or admission
decision. No seed prior changes in this checkpoint.

## Next boundary

The remaining 12 development families need rubric-complete labels before fitting
or candidate design. Validation labels must stay blind and unavailable until a
finite candidate manifest is frozen. Test labels have not been authored; before
authoring, their bodies and digests require genuine external custody, and they may
be revealed only after validation selects one frozen candidate.
