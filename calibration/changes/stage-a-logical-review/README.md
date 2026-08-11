# Stage A logical-decomposition audit

`0.1.0.decomposition.json` records a disclosed host-AI audit of the five public
Change records used by the first 4-to-32-hour admission gate. It does not replace
or rewrite their frozen rubric-1.0.0 teacher corpora. Instead, it decomposes each
existing teacher expected total into distinct tasks under the stricter
`change-ehe-work-item/1.1.0` reasoning boundary.

Every task has 0.5 to 1.5 expected hours, a concrete outcome, a rationale, a native
effort category, and a parent target whose immutable evidence lineage remains in
the source corpus. Reusing a parent target is allowed here because the artifact
subdivides an already reviewed target; it does not remap or double-count source
work-item IDs. Task sums must equal each frozen teacher expected total exactly.
The original target low/high ranges remain authoritative and are not mechanically
split into false precision.

The audit used the checked-in public teacher targets, their evidence references,
and their rationales. Candidate values were visible, so this is a transparent
logical-decomposition audit rather than a new blind estimate. It changes no
teacher total, partition, maturity, estimator prior, or empirical claim. The label
authority remains one disclosed host-AI teacher and `teacher-estimate` maturity.

`change-logical-decomposition-1.0.0.schema.json` freezes the artifact shape beside
the data; it is a calibration-audit schema, not a new public CLI output contract.

The process-level test `ChangeLogicalDecompositionArtifactTests` checks the schema
identity, model/input provenance, unique task IDs, task-size bounds, distinct
titles and rationales, exact record totals, five-family count, and the aggregate
38-hour teacher total without reading any target source tree.
