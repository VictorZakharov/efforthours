# Reporting output-size checkpoint

## Scope

This checkpoint measures the Milestone 6 reporting surfaces as serialized text.
It is an output-volume comparison, not an estimator-accuracy benchmark or an exact
provider-token measurement.

These historical measurements cover the Milestone 6 `--view review` projection,
not the later `host-review/1.0.0` packet or follow-up queries. The separate
[Milestone 8 public checkpoint](../benchmarks/host-review/public-expansion/0.1.0)
records exact packet/query payload sizes and item/category/total agreement for
three public repositories. Provider tokens, elapsed time, complete paired-session
context sizes, and cost were unavailable, so that checkpoint makes no savings
claim and selects no model-facing budget or automatic default.

The measurements were taken on August 5, 2026 with `eh` version
`0.6.0-alpha.1`, `seed-rules/0.2.0`, and the bundled
`us-senior-software-contractor/2026.1` rate. The EffortHours row uses a static scan of
the Milestone 6 working tree with source digest
`sha256:33b6016cbdc9330e4fabd68cbcff9942ecef4d1a97072f0813d7397dc6c8a9c7`.
The three small rows use the curated evidence bundles under
`tests/fixtures/evidence/`.

The final stdout newline is excluded. `Characters` counts the serialized
characters; these outputs were ASCII after JSON escaping, so the UTF-8 byte and
character counts happen to be equal. `Approx. tokens` is
`ceiling(characters / 4)`, a deliberately provider-neutral size indicator. The
percentage compares each output with compact canonical full JSON for the same
input.

## Results

| Dataset | View | Format | UTF-8 bytes | Characters | Lines | Approx. tokens | Full compact |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| EffortHours | full | pretty JSON | 292,276 | 292,276 | 6,349 | 73,069 | 121.5% |
| EffortHours | full | compact JSON | 240,464 | 240,464 | 1 | 60,116 | 100.0% |
| EffortHours | repository | compact JSON | 4,935 | 4,935 | 1 | 1,234 | 2.1% |
| EffortHours | category | compact JSON | 7,218 | 7,218 | 1 | 1,805 | 3.0% |
| EffortHours | scope | compact JSON | 8,086 | 8,086 | 1 | 2,022 | 3.4% |
| EffortHours | work-item | compact JSON | 32,981 | 32,981 | 1 | 8,246 | 13.7% |
| EffortHours | review | compact JSON | 17,694 | 17,694 | 1 | 4,424 | 7.4% |
| EffortHours | review | Markdown | 8,763 | 8,763 | 106 | 2,191 | 3.6% |
| Small .NET | full | pretty JSON | 24,376 | 24,376 | 670 | 6,094 | 127.1% |
| Small .NET | full | compact JSON | 19,178 | 19,178 | 1 | 4,795 | 100.0% |
| Small .NET | repository | compact JSON | 3,822 | 3,822 | 1 | 956 | 19.9% |
| Small .NET | category | compact JSON | 5,626 | 5,626 | 1 | 1,407 | 29.3% |
| Small .NET | scope | compact JSON | 4,217 | 4,217 | 1 | 1,055 | 22.0% |
| Small .NET | work-item | compact JSON | 7,629 | 7,629 | 1 | 1,908 | 39.8% |
| Small .NET | review | compact JSON | 10,149 | 10,149 | 1 | 2,538 | 52.9% |
| Small .NET | review | Markdown | 5,286 | 5,286 | 84 | 1,322 | 27.6% |
| Small JS/TS | full | pretty JSON | 45,876 | 45,876 | 1,235 | 11,469 | 127.2% |
| Small JS/TS | full | compact JSON | 36,062 | 36,062 | 1 | 9,016 | 100.0% |
| Small JS/TS | repository | compact JSON | 3,164 | 3,164 | 1 | 791 | 8.8% |
| Small JS/TS | category | compact JSON | 5,449 | 5,449 | 1 | 1,363 | 15.1% |
| Small JS/TS | scope | compact JSON | 3,556 | 3,556 | 1 | 889 | 9.9% |
| Small JS/TS | work-item | compact JSON | 7,900 | 7,900 | 1 | 1,975 | 21.9% |
| Small JS/TS | review | compact JSON | 10,105 | 10,105 | 1 | 2,527 | 28.0% |
| Small JS/TS | review | Markdown | 5,220 | 5,220 | 83 | 1,305 | 14.5% |
| Small mixed | full | pretty JSON | 14,852 | 14,852 | 439 | 3,713 | 128.8% |
| Small mixed | full | compact JSON | 11,530 | 11,530 | 1 | 2,883 | 100.0% |
| Small mixed | repository | compact JSON | 3,131 | 3,131 | 1 | 783 | 27.2% |
| Small mixed | category | compact JSON | 4,907 | 4,907 | 1 | 1,227 | 42.6% |
| Small mixed | scope | compact JSON | 3,723 | 3,723 | 1 | 931 | 32.3% |
| Small mixed | work-item | compact JSON | 6,684 | 6,684 | 1 | 1,671 | 58.0% |
| Small mixed | review | compact JSON | 8,986 | 8,986 | 1 | 2,247 | 77.9% |
| Small mixed | review | Markdown | 4,798 | 4,798 | 81 | 1,200 | 41.6% |

## Initial usefulness review

The EffortHours review projection is materially smaller than the canonical report:
7.4% as compact JSON and 3.6% as Markdown. Without opening source or the full
ledger, a reviewer can see the complete EHE and cost range, rate identity,
represented-item inventory, every category, every project scope, the six largest
capabilities, low-confidence or explicitly uncertain capabilities, the excluded
professionalization gap, verification state, warnings, and stable explanation
IDs. The `explain` command was separately exercised against those IDs and returned
the underlying work-item parts, evidence facts, estimator lineage, assumptions,
exclusions, and uncertainty.

The bounded review view saves less on very small inputs because its fixed context
and category summaries are a larger share of the report. It still reduces all
three small fixtures, but callers asking one narrow question should prefer the
repository, category, or scope projection. The work-item projection is the useful
middle layer when capability coverage matters; on EffortHours it reverses 154 ledger
parts into 51 capabilities and uses 13.7% of compact full JSON.

One limitation is visible in the initial EffortHours review: several of the six
lowest-confidence entries are similar manual-validation capabilities from
different projects. They are valid distinct scopes, but a future review policy may
add category or reason diversity if representative host-AI evaluations show that
the repetition displaces more useful uncertainty. The unbounded views and
`explain` prevent this presentation choice from losing information.

## Reproduction method

The EffortHours evidence bundle was produced once so the comparison did not rescan
the repository for every view:

```text
eh scan . --output artifacts/benchmarks/efforthours-m6.repository-evidence.json
```

For each evidence bundle, the canonical full report and each projection were then
rendered with commands of this form:

```text
eh estimate <evidence.json> --view full --compact
eh estimate <evidence.json> --view <repository|category|scope|work-item|review> --compact
eh estimate <evidence.json> --view review --format markdown
```

The ignored `artifacts/` evidence file is not a release artifact and is not needed
to run tests. Measurements should be refreshed when the canonical contracts,
projection fields, review policy, or representative estimator output changes
materially.
