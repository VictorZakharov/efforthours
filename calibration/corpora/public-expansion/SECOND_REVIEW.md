# Public expansion independent-review handoff

## Status

The public expansion remains at `teacher-estimate` maturity. No second reviewer
has corrected it, and this repository does not claim otherwise.

`0.1.0.blind-review-packet.json` is a schema-versioned handoff for a distinct human
or host-AI reviewer. It contains all 3 records and 133 reviewed target structures
while hiding every prior target range, rationale, uncertainty decision, and
repository-total value. Its pinned canonical source-corpus digest is:

```text
sha256:a411961985414eb65228991abdbe8165e5fc8abdeea92934902aacac7f405070
```

The packet itself is always `unreviewed` and cannot be consumed as a calibration
corpus.

## Reviewer workflow

1. Use a publishable reviewer ID distinct from every teacher identity already in
   the source corpus.
2. Follow `calibration/rubrics/ehe-work-item/1.1.0.md` without consulting prior
   ranges or preferred repository totals.
3. Resolve material ambiguity through the listed evidence IDs and bounded source
   inspection. Do not use Git history, prices, or contributor metadata.
4. Create one `calibration-corpus-review-plan` decision for every source target. A
   blind reviewer normally uses `replace` with an independently formed range and
   rationale. `accept` is appropriate only after the prior label is intentionally
   revealed and independently checked.
5. Use an exact `0/0/0` replacement only for an explicit exclusion and provide both
   a rationale and `sizeException`. Never use zero merely to express uncertainty or
   a discount.
6. Use `reviewed` only after a genuine second review. Use `adjudicated` only after a
   distinct adjudicator explicitly resolves disagreements.
7. Compile only against the exact source corpus:

```text
eh calibration review-compile <completed-plan.json> \
  calibration/corpora/public-expansion/0.1.0.corpus.json \
  --output <reviewed-corpus.json>
```

The completed plan must pin `calibration-corpus-review-compiler/0.2.0`. The compiler
rejects source-digest drift, omitted or unknown records or targets, reviewer
identities reused from prior provenance, maturity downgrades, ambiguous zero
ranges, and replacement decisions without complete ranges and rationale. It
preserves target IDs, categories, scopes, source-work-item IDs, evidence IDs,
repository partitions, and original teacher provenance.

## Independence boundary

The AI session that created these labels and implemented this handoff must not be
represented as the independent reviewer. A future review may be performed by
another qualified human or a separately identified host-AI model, but its actual
identity and version must be recorded. Until that happens, the expansion remains
preliminary weak supervision and cannot justify numerical tuning or a production
accuracy statement.
