# Reproducing the public real Change expansion

Use authorized public clones that already contain the exact base and head objects
listed in [SOURCES.md](SOURCES.md). Disable each remote before analysis if an
offline reproduction is required. From the EffortHours repository root, run
released `EffortHours.Tool` `0.9.0-alpha.2` with `--no-rate` and these selectors:

```text
eh change <benchmarkdotnet> --base 17f218589e5a3364b01d1d0ae4c4ca87e5b4bfad --head e90b701ed71a3b3e571ab40a9192715d0e453a0b --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/benchmarkdotnet.change-estimate.json
eh change <spectre-console> --base bbbb5729dde27b58deee44f447a788eea46ee451 --head 8b3a236cc812d8799808ddc06c797ac8d4522360 --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/spectre-console.change-estimate.json
eh change <p-limit> --base 42599ebbbb1228a5bdab381fcf8f4ac20eb8d551 --head 1183b5d50a21a3b5825bdf111341552d3de35701 --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/p-limit.change-estimate.json
eh change <axios> --base 529ce70296334a0397f9e45e7dfa5658f6b6fdba --head 27b9d9761cba6fe477dfa6104527523ba2450ad4 --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/axios.change-estimate.json
eh change <zod> --base 24b4cc7a6fc0008ef3ad129bde3f2bc1816c72fd --head 17bbe5609b423349e4d8fbc1291948497f8d3544 --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/zod.change-estimate.json
eh change <ofetch> --base 47fe80799e23406dd0fb1c504bb493b6a6d0a5af --head 646bb130721efc442517f241ad728786fd704243 --no-rate --output calibration/changes/public-real-expansion/0.1.0/reports/ofetch.change-estimate.json
```

For each report, run `calibration change-scaffold --blind` with the corresponding
repository-family ID, change ID, and coverage tags from `0.1.0.selection.json`.
Write the six packets under `0.1.0/blind-packets/`. Candidate totals and target
hours must remain null in every packet.

After the independently reasoned teacher plan is complete, compile and validate:

```text
eh calibration change-compile
  calibration/changes/public-real-expansion/0.1.0.teacher-review-plan.json
  calibration/changes/public-real-expansion/0.1.0/reports/benchmarkdotnet.change-estimate.json
  calibration/changes/public-real-expansion/0.1.0/reports/spectre-console.change-estimate.json
  calibration/changes/public-real-expansion/0.1.0/reports/p-limit.change-estimate.json
  calibration/changes/public-real-expansion/0.1.0/reports/axios.change-estimate.json
  calibration/changes/public-real-expansion/0.1.0/reports/zod.change-estimate.json
  calibration/changes/public-real-expansion/0.1.0/reports/ofetch.change-estimate.json
  --output calibration/changes/public-real-expansion/0.1.0.teacher-corpus.json

eh calibration validate
  calibration/changes/public-real-expansion/0.1.0.teacher-corpus.json

eh calibration review-scaffold
  calibration/changes/public-real-expansion/0.1.0.teacher-corpus.json
  --blind
  --output calibration/changes/public-real-expansion/0.1.0.independent-review-packet.json
```

Evaluate development and validation separately with the same six reports:

```text
eh calibration change-evaluate <corpus> <reports...>
  --partition development
  --output calibration/changes/public-real-expansion/0.1.0.teacher-development-evaluation.json

eh calibration change-evaluate <corpus> <reports...>
  --partition validation
  --output calibration/changes/public-real-expansion/0.1.0.teacher-validation-evaluation.json
```

Do not evaluate the test partition. The checked-in corpus must produce digest
`sha256:a60aed52d78368cad69fc39bb7fa399a255dbf237f7739bf78dfd55356c96c7c`
in the blind second-pass packet. The teacher plan is a host-AI judgment artifact;
compilation and evaluation are mechanical, but the judgment is not regenerated
from candidate estimates.
