# Reproducing the public real Change pilot

Use an authorized public clone named `guardclauses-pr-333` that already contains
the exact base and head objects. Disable its remote before analysis if an offline
reproduction is required. From the EffortHours repository root, run released
`EffortHours.Tool` `0.9.0-alpha.2`:

```text
eh change <path-to>/guardclauses-pr-333
  --base 36051ca70d97183e2ad2091a14b65a384f298c47
  --head 967e717bb88fa0ee0b87d054ca255934105e0f7e
  --no-rate
  --output calibration/changes/public-real/0.1.0/reports/guardclauses-pr-333.change-estimate.json

eh calibration change-scaffold
  calibration/changes/public-real/0.1.0/reports/guardclauses-pr-333.change-estimate.json
  --repository-family repository:github.com/ardalis/guardclauses
  --case change:github.com/ardalis/guardclauses:pr-333
  --tag dotnet --tag feature --tag pull-request
  --tag public-real-change --tag unit-tests
  --output calibration/changes/public-real/0.1.0/authoring-packets/guardclauses-pr-333.reference-authoring.json

eh calibration change-compile
  calibration/changes/public-real/0.1.0.teacher-review-plan.json
  calibration/changes/public-real/0.1.0/reports/guardclauses-pr-333.change-estimate.json
  --output calibration/changes/public-real/0.1.0.teacher-corpus.json

eh calibration validate
  calibration/changes/public-real/0.1.0.teacher-corpus.json

eh calibration review-scaffold
  calibration/changes/public-real/0.1.0.teacher-corpus.json
  --blind
  --output calibration/changes/public-real/0.1.0.independent-review-packet.json

eh calibration change-evaluate
  calibration/changes/public-real/0.1.0.teacher-corpus.json
  calibration/changes/public-real/0.1.0/reports/guardclauses-pr-333.change-estimate.json
  --partition development
  --output calibration/changes/public-real/0.1.0.teacher-development-evaluation.json
```

The canonical report must retain source-estimate digest
`sha256:2df014bff65da8beead6ba8173ef718376afd9e772cf3cf35a85b03ab1afa20b`.
The compiled corpus must produce digest
`sha256:73966db241d7c272b11ad02e3ca87cf1433ef5213809a347648889452374d28a`
in the blind second-pass packet. The teacher plan is a human/host-AI judgment
artifact; compilation and evaluation are mechanical, but that judgment is not
regenerated from the candidate estimate.
