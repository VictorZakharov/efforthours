# Independent Change calibration handoff

The source corpus `efforthours-change-public-synthetic/0.1.0` has one host-AI teacher
and no independent correction. Its blind packet contains 24 records and 121
targets pinned to corpus digest
`sha256:ecfdb867ed2ba4912c9550277fc050b5e5511d0e15a107c8a08c044f61793c10`.

The separate one-record real public pilot has its own five-target blind handoff in
[`public-real/INDEPENDENT_REVIEW.md`](public-real/INDEPENDENT_REVIEW.md). It shares
the same no-unblinding and distinct-reviewer requirements.

## Blind assignment

A reviewer must be a person or separately identified host-AI that did not create
the teacher policy. Work by complete repository family so related cases never
cross reviewers or partitions:

- development: `change-dn-lib-a`, `change-js-service-a`, `change-ts-package-a`,
  and `change-mixed-app-a`;
- validation: `change-dn-data-b` and `change-js-lib-b`; and
- test: `change-ts-service-c` and `change-mixed-tool-c`.

Use only:

- `public-synthetic/0.1.0.fixtures.json` for bounded synthetic base/head source;
- `public-synthetic/0.1.0/blind-packets/` for source capability and evidence
  descriptions;
- `public-synthetic/0.1.0.independent-review-packet.json` for second-pass target
  identities; and
- `calibration/rubrics/change-ehe-work-item/1.0.0.md` for semantics.

Do not open the source reports, teacher policy, teacher review plan, teacher corpus,
or development/validation evaluation files before returning decisions. Disclose
any accidental unblinding.

## Required return

For every non-empty target, use `replace` and provide an independently reasoned
low/expected/high range, rationale, uncertainty reasons, and any size exception.
Use exact `0/0/0` plus rationale and `sizeException` only for a genuine reviewed
exclusion. For each of the four empty exact-zero records, return a record-level
review decision with an empty target-decision list and a note confirming the final
delta is mechanical or absent.

Record a distinct reviewer identity, actual model/version when applicable,
completion date, and blind-access notes. Do not change target IDs, record IDs,
repository family, partition, lineage, or source-corpus digest.

The maintainer compiles the returned plan mechanically:

```text
eh calibration review-compile <completed-plan.json>
  calibration/changes/public-synthetic/0.1.0.teacher-corpus.json
  --output <reviewed-corpus.json>

eh calibration validate <reviewed-corpus.json>
```

Compilation alone is not review. Do not compute or disclose the held-out test
comparison until the independent corpus, numerical gates, and release candidate
are frozen.
