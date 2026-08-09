# Independent calibration review handoff

EffortHours currently publishes three blind second-pass packets. All source corpora
remain `teacher-estimate`; none has received independent correction or
adjudication.

| Corpus | Blind packet | Records | Targets | Canonical source-corpus digest |
|---|---|---:|---:|---|
| `efforthours-public-pilot/0.1.0` | [`public-pilot/0.1.0.blind-review-packet.json`](corpora/public-pilot/0.1.0.blind-review-packet.json) | 3 | 99 | `sha256:43b73a6e7ecc743612037349e07cd93c43fc258c926dac326734f304f4a75222` |
| `efforthours-public-expansion/0.1.0` | [`public-expansion/0.1.0.blind-review-packet.json`](corpora/public-expansion/0.1.0.blind-review-packet.json) | 3 | 133 | `sha256:a411961985414eb65228991abdbe8165e5fc8abdeea92934902aacac7f405070` |
| `efforthours-change-public-synthetic/0.1.0` | [`changes/public-synthetic/0.1.0.independent-review-packet.json`](changes/public-synthetic/0.1.0.independent-review-packet.json) | 24 | 121 | `sha256:ecfdb867ed2ba4912c9550277fc050b5e5511d0e15a107c8a08c044f61793c10` |

The corresponding source manifests, first-pass review methods, numerical
baselines, and corpus-specific instructions are next to each packet. A reviewer
may take one corpus independently; reviewing more than one is useful but not
required.

## Assignment boundary

An independent reviewer must:

- be a person or separately identified host-AI model that did not create the
  first-pass labels;
- use a new publishable reviewer identity and record actual model/version details
  for host-AI work;
- form target ranges from the rubric, evidence IDs, and bounded source inspection
  without seeing first-pass hours or totals;
- avoid Git history, contributor output, price, and preferred-total signals; and
- disclose any accidental unblinding before maturity is advanced.

The current repository maintainers may prepare packets, validate structure, and
compile a completed plan, but that mechanical work does not constitute review.

## Deliverables

A complete handoff return consists of:

1. one schema-valid `calibration-corpus-review-plan` decision for every target;
2. reviewer provenance and completion date;
3. replacement rationale, range, uncertainty reasons, and any size exception for
   each independently changed target;
4. exact `0/0/0` plus rationale and `sizeException` for every explicit exclusion;
5. a note describing source access, accidental unblinding, or unresolved ambiguity;
   and
6. no changes to repository identity, partition, target lineage, or source-corpus
   digest.

Compile against the pinned source corpus with the current CLI:

```text
eh calibration review-compile <completed-plan.json> <source-corpus.json> --output <reviewed-corpus.json>
eh calibration validate <reviewed-corpus.json>
```

After compilation, maintainers must inspect the diff, reproduce validation, update
`MODEL_REVIEWS.md`, and record agreement and disagreement without tuning against
validation or test records. `reviewed` and `adjudicated` are evidence states, not
automatic quality claims.
