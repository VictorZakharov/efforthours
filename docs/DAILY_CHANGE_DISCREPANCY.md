# Independent daily Change and supported capability growth

The producer fixes use `independent-local-day-change/1.0.0`,
`change-portfolio/0.4.0`, and `change-seed/0.20.0+seed-rules/0.4.0`.
They correct two mechanisms diagnosed by the consumer audit without fitting
estimator priors to private numerical totals. Change EHE remains experimental
and uncalibrated. The private source audit is not a committed fixture.

## Daily normalization

Previously all selected changes were reconciled across the entire interval and
then allocated to days. Adding an earlier overlapping change could lower a later
day even though its selected inputs were unchanged. Existing low-level joint
modes retain that behavior and identity for compatibility.

`eh calendar` and low-level `--bucket independent-day` now group selected inputs
by local date and repository before reconciliation. Within-day exact/overlap
normalization is unchanged. Immutable snapshot/static analysis, bounded sessions,
and durable checkpoints still run as one batch. Daily category and adjustment
ledgers are retained in JSON, validated against repository and batch totals, and
bound into semantic identity. Day/project low/expected/high values are additive
across dates. Complete no-match dates are zero; failure never exposes an aggregate.
HTML and text disclose the measurement alongside estimator and selection policy.

Calendar defaults to committer dates and prompts for `committer|author` in
interactive mode. `--date-field author` selects author timestamps explicitly.
Merges remain excluded, valid coauthors included, and reachable pinned heads
remain authoritative. Timestamps select evidence; they do not measure work time.
Contributor attribution remains joint within each day. Independent days require
calendar-day buckets and reject isolated contributor normalization.

## Growth in broad capabilities

Previously positive growth in an existing capability always used a bounded
modification range, capped at eight expected hours. Many distinct functions or
test cases could therefore collapse into a small modification budget.

Represented production/test semantic-unit growth now preserves the componentwise
larger of that modification range and the positive normalized repository
capability marginal. These budgets are alternatives. Located semantic facts and
owning-scope aggregate structure facts bind growth to represented changed paths.
File/display partition count alone cannot enable growth. Existing repository
normalization, duplicate/generated exclusions, diminishing returns, and small-task
decomposition remain in force. No private category offset or stock total is a
rule parameter. Other capability categories retain their existing rules.

## Distinct measurements and limits

Independent selected-day Change represents selected normalized deltas. Replacement
stock remains the existing native snapshot daily series with signed differences;
stock growth can be negative. Coherent opening-to-closing Change is a third
calculation and can include unselected interleaved changes. None must be forced
to equal another through scaling, clamping, absolute values, pricing, or per-commit
summation. Daily lineage enables category/project diagnosis without concealing
these different selection and model operands.

Arbitrary alternative commit partitions within a day are not guaranteed to yield
identical selected-delta portfolios. Cross-day invariance and display-partition
invariance are the guarantees of this correction. A new cross-report stock/endpoint
diagnostic command is outside this producer fix; existing stock reports and daily
category lineage remain available separately. The private archives and native
receipts were not supplied, so their exact audit totals are not independently
reproduced or certified here. No frozen admission report or seed prior changes.

Byte-identical bodies added more than once in one final delta now retain one
deterministic represented path; other additions retain exact-duplicate lineage
and zero body effort. This also prevents fallback and validation charges from
rewarding copies of newly added source. Existing copies already present in the
base retain their prior exclusion.
