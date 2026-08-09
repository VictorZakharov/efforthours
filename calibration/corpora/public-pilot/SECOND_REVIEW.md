# Public pilot independent-review handoff

## Status

The public pilot remains at `teacher-estimate` maturity. No second reviewer has
corrected it, and this repository does not claim otherwise.

`0.1.0.blind-review-packet.json` is a compact, schema-versioned handoff for a
distinct human or host-AI reviewer. It contains all 3 records and 99 reviewed
target structures while hiding every prior target range, rationale, uncertainty
decision, and repository-total value. Its pinned source-corpus digest is:

```text
sha256:43b73a6e7ecc743612037349e07cd93c43fc258c926dac326734f304f4a75222
```

The packet itself is always `unreviewed` and cannot be consumed as a calibration
corpus.

## Reviewer workflow

1. Use a publishable reviewer ID distinct from every teacher identity already in
   the source corpus.
2. Follow `calibration/rubrics/ehe-work-item/1.0.0.md` without consulting prior
   ranges or preferred repository totals.
3. Resolve material ambiguity through the listed evidence IDs and bounded source
   inspection. Do not use Git history, prices, or contributor metadata.
4. Create one `calibration-corpus-review-plan` decision for every source target.
   A blind reviewer normally uses `replace` with an independently formed range and
   rationale. `accept` is appropriate only after the prior label is intentionally
   revealed and independently checked.
5. Use `reviewed` only after a genuine second review. Use `adjudicated` only after
   a distinct adjudicator explicitly resolves disagreements.
6. Compile only against the exact source corpus:

```text
eh calibration review-compile <completed-plan.json> \
  calibration/corpora/public-pilot/0.1.0.corpus.json \
  --output <reviewed-corpus.json>
```

The compiler rejects source-digest drift, omitted or unknown records/targets,
reviewer identities reused from prior provenance, maturity downgrades, and
replacement decisions without complete ranges and rationale. It preserves target
IDs, categories, scopes, source work-item IDs, evidence IDs, repository partitions,
and original teacher provenance.

## Independence boundary

The AI session that created the first pilot labels and the implementation of this
handoff must not be represented as the independent reviewer. A future review may
be performed by another qualified human or a separately identified host-AI model,
but its actual identity and version must be recorded. Until that happens, the
pilot remains preliminary weak supervision and cannot justify numerical tuning or
a production accuracy statement.
