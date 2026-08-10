# Independent review handoff

`efforthours-change-public-real-pilot/0.1.0` has one disclosed host-AI teacher and
no independent correction. The blind packet contains five targets pinned to
corpus digest
`sha256:73966db241d7c272b11ad02e3ca87cf1433ef5213809a347648889452374d28a`.

A distinct reviewer should use only:

- the immutable public base and head revisions recorded in [SOURCES.md](SOURCES.md);
- the public change specification and enough adjacent source to understand the
  established project conventions;
- `0.1.0.independent-review-packet.json`; and
- `calibration/rubrics/change-ehe-work-item/1.0.0.md`.

Before returning decisions, do not open the candidate report, reference authoring
packet, teacher plan, teacher corpus, development evaluation, or the frozen-result
table in this directory's README. Disclose accidental unblinding.

Return one `replace` decision per target with an independently reasoned
low/expected/high range, rationale, uncertainty reasons, and any size exception.
Record a distinct reviewer identity, the actual model/version when applicable,
completion date, and access notes. Do not change record IDs, target IDs,
repository family, development partition, or source-corpus digest.

The maintainer can compile the completed plan with:

```text
eh calibration review-compile <completed-plan.json>
  calibration/changes/public-real/0.1.0.teacher-corpus.json
  --output <independently-reviewed-corpus.json>

eh calibration validate <independently-reviewed-corpus.json>
```

Compilation alone is not review. This development record must not be promoted to
held-out evidence or used to claim accuracy.
