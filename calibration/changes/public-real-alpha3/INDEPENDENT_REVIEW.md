# Independent review handoff

`efforthours-change-public-real-alpha3/0.1.0` has one disclosed host-AI teacher
and no independent correction. The blind packet contains one validation record
and four targets pinned to corpus digest
`sha256:d24256b28f8561629bbe602fc5f5ddc285ab964ee8d57d5d0ef51835b1b87f70`.

A distinct reviewer should use only:

- the immutable public base and head revisions in [SOURCES.md](SOURCES.md);
- the public change specification and enough adjacent source to understand the
  established project conventions;
- `0.1.0/blind-packets/ardalis-result.blind-authoring.json` and
  `0.1.0.independent-review-packet.json`; and
- `calibration/rubrics/change-ehe-work-item/1.0.0.md`.

Before returning decisions, do not open the source report, teacher plan, teacher
corpus, validation evaluation, or the result tables in this directory's README.
Disclose accidental unblinding. Review the complete repository-family record and
do not move it to another partition.

Return one `replace` decision per target with an independently reasoned
low/expected/high range, rationale, uncertainty reasons, and any size exception.
Use exact `0/0/0` only for a genuine reviewed exclusion and preserve its source
lineage. Record a distinct reviewer identity, the actual model/version when
applicable, completion date, and access notes. Do not change record IDs, target
IDs, repository family, partition, or source-corpus digest.

The maintainer can compile the completed plan with:

```text
eh calibration review-compile <completed-plan.json>
  calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json
  --output <independently-reviewed-corpus.json>
eh calibration validate <independently-reviewed-corpus.json>
```

Compilation alone is not review. This validation diagnostic cannot select a model,
set an admission threshold, or establish held-out accuracy.
