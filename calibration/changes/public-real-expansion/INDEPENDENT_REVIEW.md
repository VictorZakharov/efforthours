# Independent review handoff

`efforthours-change-public-real-expansion/0.1.0` has one disclosed host-AI teacher
and no independent correction. The blind packet contains six records and 34
targets pinned to corpus digest
`sha256:a60aed52d78368cad69fc39bb7fa399a255dbf237f7739bf78dfd55356c96c7c`.

A distinct reviewer should use only:

- the immutable public base and head revisions in [SOURCES.md](SOURCES.md);
- each public change specification and enough adjacent source to understand the
  established project conventions;
- `0.1.0/blind-packets/` and
  `0.1.0.independent-review-packet.json`; and
- `calibration/rubrics/change-ehe-work-item/1.0.0.md`.

Before returning decisions, do not open the source reports, teacher plan, teacher
corpus, development or validation evaluations, or the result tables in this
directory's README. Disclose accidental unblinding. Work only on complete
repository-family records; do not move a family between partitions.

Return one `replace` decision per target with an independently reasoned
low/expected/high range, rationale, uncertainty reasons, and any size exception.
Use exact `0/0/0` only for a genuine reviewed exclusion and preserve its source
lineage. Record a distinct reviewer identity, the actual model/version when
applicable, completion date, and access notes. Do not change record IDs, target
IDs, repository families, partitions, or the source-corpus digest.

The maintainer can compile the completed plan with:

```text
eh calibration review-compile <completed-plan.json>
  calibration/changes/public-real-expansion/0.1.0.teacher-corpus.json
  --output <independently-reviewed-corpus.json>

eh calibration validate <independently-reviewed-corpus.json>
```

Compilation alone is not review. Development and validation remain diagnostic,
and the test candidate comparison must stay withheld until an independently
reviewed corpus, numerical gates, and a release candidate are frozen.
