# Change Calibration Infrastructure

## Status

The first Change EHE calibration checkpoint is complete as of August 6, 2026. It
adds review, provenance, compilation, independent-correction, and evaluation
boundaries around `change-seed/0.1.0` without changing a numerical prior or adding
an ML dependency. The global-tool package advances to `EffortHours.Tool`
`0.8.0-alpha.2`.

This infrastructure checkpoint initially contained no public Change EHE label
corpus. `MILESTONE_CHANGE_3.md` records the subsequent preliminary teacher corpus;
the estimator remains experimental and uncalibrated.

The August 11, 2026 SQL analyzer extension changes no contract, rubric, corpus,
label, partition, metric, compiler, or review maturity. Its current source identity
is `change-seed/0.13.0+seed-rules/0.4.0`, but no SQL, Python, Go, Java, Kotlin,
Shell, PowerShell, Terraform, or HCL Change calibration record exists and the earlier Stage A gate
must not be generalized to those paths.

## Delivered scope

- `change-ehe-work-item/1.0.0`, a final-delta review rubric that forbids history,
  activity, price, and preferred-total signals;
- a backward-compatible v1 Change calibration reference embedded in existing
  authoring, review-plan, corpus, and second-review records;
- content-derived final-delta identity plus immutable base/head object and evidence
  provenance;
- `eh calibration change-scaffold`, `change-compile`, and
  `change-evaluate`;
- reuse of ordinary corpus validation plus blind `review-scaffold` and exact-digest
  `review-compile` for genuinely independent correction;
- one shared implementation of repository and Change WAPE, bias, absolute-error,
  mapping, and interval metrics;
- explicit zero-final-delta records with no invented zero-hour work item;
- explicit reviewer rejection of false-positive or duplicate source capabilities
  as lineage-preserving exact-zero targets rather than forced positive labels;
- repository-family partition isolation across every change from that family;
- a 24-case, eight-family development/validation/test matrix frozen before labels;
- a deterministic in-memory generator, 24 canonical source reports, and 24 blind
  authoring packets that materialize that matrix without target-repository disk
  I/O; and
- `change-model-admission/0.1.0`, which freezes the metric set and decision order
  while deferring numerical thresholds until realistic independently reviewed
  error scales exist.

## Contract compatibility

Repository calibration records serialize exactly as before because the new
`change` member is optional and omitted when null. Existing corpus IDs, canonical
digests, blind packets, and review plans therefore remain reproducible. A corpus
cannot mix repository and Change records, and only rubric
`change-ehe-work-item` may carry Change provenance.

The Change final-delta digest derives from the base and head repository-evidence
digests, not selector spelling, commit activity, or contributor metadata. Candidate
matching also requires profile and baseline. The compiler separately verifies
base/head object IDs, selection kind, evidence digests, estimator identity, and the
canonical source-report digest.

## Review and admission boundary

`change-scaffold` always emits `unreviewed` data. Reference mode exposes candidate
hours as suggestions; blind mode hides totals, categories, work-item hours, and
confidence. Teacher compilation creates only `teacher-estimate` maturity. The
existing second-pass compiler requires distinct reviewer identities and preserves
Change provenance through accept/replace decisions.

The metric identities are frozen before any ML experiment, but numerical
admission thresholds are not. Development can establish error scales; validation
selects among a finite frozen candidate set; test is evaluated once for a release
decision. Until those gates and independent labels exist, failure or uncertainty
leaves `change-seed/0.1.0` as the deterministic experimental fallback.

## Verification

Memory-only tests cover blind authoring, schema validity, immutable provenance,
compilation, exact candidate evaluation, partition leakage, second-review lineage,
and empty-target zero deltas. The separate process-level suite writes explicit
temporary reports and exercises scaffold, compile, validate, and evaluate through
the CLI without network access. A bounded generator test writes one small suite
twice and verifies byte-identical, semantically valid, schema-valid artifacts.

The source-file budget gate covers every new production and test file. Shared
metric and target-compilation responsibilities were extracted before either
repository or Change calibration implementations approached their file ceilings.

## Next checkpoint

1. Hand the generated blind packet, grouped by repository family, to
   genuinely distinct reviewers.
2. Preserve the completed `change-seed/0.4.0` generated-customization boundary,
   including the inherited 0.3.0 logical-marginality correction and separately
   versioned diagnostics, while compiling independent reviews. The current source
   composes repository `seed-rules/0.4.0`; frozen Change reports retain their
   original repository-model identity.
3. Add multiple real observations per ecosystem/partition cell, then freeze
   numerical admission thresholds from development/validation behavior before
   deciding whether transparent corrections or local ML merit evaluation.
