# Reproducing the released alpha.3 public Change diagnostic

Use an authorized public clone that already contains the exact base and head
objects listed in [SOURCES.md](SOURCES.md). Disable its remote before analysis if
an offline reproduction is required. From the EffortHours repository root, run
released `EffortHours.Tool` `0.9.0-alpha.3` with `--no-rate`:

```text
eh change <ardalis-result>
  --base 182c134ef9c5dc0338bd44883bfc46bc7c66a75a
  --head 7b5ff9b6d8140236303b0a7974138e507060ed1d
  --no-rate
  --output calibration/changes/public-real-alpha3/0.1.0/reports/ardalis-result.change-estimate.json
```

Before opening candidate values, create the blind packet:

```text
eh calibration change-scaffold
  calibration/changes/public-real-alpha3/0.1.0/reports/ardalis-result.change-estimate.json
  --repository-family repository:github.com/ardalis/result
  --case change:github.com/ardalis/result:pr-256
  --tag api
  --tag configuration
  --tag dotnet
  --tag feature
  --tag public-real-change
  --tag pull-request
  --tag unit-tests
  --blind
  --output calibration/changes/public-real-alpha3/0.1.0/blind-packets/ardalis-result.blind-authoring.json
```

The checked-in teacher plan is a judgment artifact and is not regenerated from
candidate estimates. After independently reasoning and freezing that plan,
compile, validate, and create the second-pass packet:

```text
eh calibration change-compile
  calibration/changes/public-real-alpha3/0.1.0.teacher-review-plan.json
  calibration/changes/public-real-alpha3/0.1.0/reports/ardalis-result.change-estimate.json
  --output calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json
eh calibration validate
  calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json
eh calibration review-scaffold
  calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json
  --blind
  --output calibration/changes/public-real-alpha3/0.1.0.independent-review-packet.json
```

Evaluate the validation partition only:

```text
eh calibration change-evaluate
  calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json
  calibration/changes/public-real-alpha3/0.1.0/reports/ardalis-result.change-estimate.json
  --partition validation
  --output calibration/changes/public-real-alpha3/0.1.0.teacher-validation-evaluation.json
```

The checked-in corpus must produce digest
`sha256:d24256b28f8561629bbe602fc5f5ddc285ab964ee8d57d5d0ef51835b1b87f70`
in the blind second-pass packet. No development or test record exists in this
corpus, so no other partition is evaluated.
