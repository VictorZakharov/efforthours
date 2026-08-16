# Development uncertainty support and OOD profile

## Status

**The first label-independent repository-held-out support profile is complete;
no interval model or feature is selected.** The profiler covers all 11,161 source
work items behind the 15 public development repositories. Every item has support
from at least three observations in at least two other repository families, but
2,839 items require a broader fallback than the exact structural cell.

This checkpoint reads no reviewed corpus or hours, fits no model, changes no
estimate, and does not reopen validation or test. It measures population support,
not prediction accuracy. `seed-rules/0.4.0` remains the shipped estimator.

## Frozen boundary

| Input or policy | Identity |
| --- | --- |
| Population manifest | `efforthours-public-readiness-uncertainty-support/1.0.0`; `sha256:c86af113b391d7171060f3b0be7c6d01ffa87f04ccb1ae03803f015199e61678` |
| Feature contract | `repository-uncertainty-features/1.0.0`; `sha256:a2fea34b25d0c963bb9e96d8c538e130f6b2b23d4d98d851584a7fbe69916077` |
| Candidate estimator | `candidate-logical-capability/0.3.0+seed-rules/0.4.0` |
| Support profiler | `uncertainty-support-profiler/1.0.0` |
| Support policy | `uncertainty-support-policy/1.0.0` |
| Distance | `gower-bucket-distance/1.0.0` |

The population manifest contains stable record IDs, repository-family IDs, and
immutable source digests only. It has no targets, reviewed ranges, report paths,
or source excerpts. All records are development records and use the same profile,
baseline, projector, estimator, and frozen feature contract.

## Support policy

Each repository family is held out in full. Revisions from the same family cannot
supply support or become a nearest OOD reference. The first cell with at least
three training work items from at least two other repository families is selected:

1. category + expected-size band + resolved work-item ecosystem set + source
   complexity;
2. category + size + ecosystem;
3. category + size;
4. category; and
5. global.

An empty work-item ecosystem set stays empty; the repository's complete ecosystem
inventory is not copied onto generic work. This avoids making a common setup,
documentation, or review item look repository-specific merely because it belongs
to a mixed repository.

| Selected support cell | Work items | Share |
| --- | ---: | ---: |
| Exact structural cell | 8,322 | 74.56% |
| Category + size + ecosystem | 1,581 | 14.17% |
| Category + size | 742 | 6.65% |
| Category | 516 | 4.62% |
| Global | 0 | 0.00% |
| Insufficient at global fallback | 0 | 0.00% |

The fallback identity and counts remain per-item audit data. These counts do not
yet widen or narrow an estimate.

## OOD policy and result

The distance gives equal total weight to 15 fixed dimensions: category, ordinal
size band, ordinal source complexity, ecosystem-set Jaccard distance, and the 11
frozen scalar feature buckets. Matching unavailable states have zero distance;
an availability mismatch has distance one. Available scalar values use their
fixed evaluation-bucket order. The nearest reference must come from another
repository family, with ordinal record/work-item tie-breaking.

| Metric | Result |
| --- | ---: |
| Work items | 11,161 |
| Distinct full profiles | 594 |
| Repository/profile evaluations | 1,044 |
| Eligible cached profile comparisons | 987,860 |
| Items with an exact full-profile match in another family | 5,944 |
| Mean nearest-neighbor OOD score | 0.011802 |
| P90 nearest-neighbor OOD score | 0.036111 |
| Maximum nearest-neighbor OOD score | 0.188889 |

Low nearest-neighbor distance is not evidence that candidate error is low. It
only says a similar bucketed feature profile exists in another development
family. The next measurement must join these values to reviewed targets inside
repository-held-out folds and test whether support or OOD explains residuals or
improves coverage, width, and interval miss.

## Reproduction and artifact policy

```text
eh calibration uncertainty-support \
  calibration/corpora/public-readiness/1.5.0.uncertainty-support-population.json \
  <features.json>... --compact --output <support-profile.json>
```

The compact profile is 17,166,747 bytes with SHA-256
`9bec7ebc0594fc89149aea8943aab5715d10172ad6a24e6ad18ea8e7216dc3fc`.
It stays under ignored `artifacts/` because its 11,161 work-item rows are derived
and reproducible. The checked-in manifest and schemas are the public inputs. No
local path, reviewed value, source excerpt, or generated timestamp is serialized.

The observed run completed in 26.5 seconds on the development workstation. That
wall time is descriptive only; CI gates deterministic output, repository
exclusion, cancellation, and the 250,000-item/50,000,000-comparison bounds rather
than machine speed.

## Next boundary

Aggregate work-item support and OOD to the existing reviewed target mapping
without using held-out labels in either feature. Measure association and fixed
conditioning through the development evaluator. Do not select either signal
unless it improves coverage, normalized width, and interval miss without hiding
repository-fold instability. Any fitted successor still requires a new identity
and a fresh blind validation boundary; test remains sealed.
