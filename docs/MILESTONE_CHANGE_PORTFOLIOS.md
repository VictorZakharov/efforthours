# Change Portfolio Checkpoint

## Status

Complete in source as of August 11, 2026. This checkpoint implements issues #3
and #22 through #25 without changing `change-seed/0.7.0`, repository
`seed-rules/0.3.0`, any frozen Change report, or any model-admission decision.
Portfolio reconciliation is identified separately as
`change-portfolio/0.1.0+change-seed/0.9.0+seed-rules/0.4.0` and remains
experimental.

The result is **repository-attributed Change EHE**: counterfactual replacement
effort represented by selected immutable changes after portfolio normalization.
It is not actual labor, a timesheet, proof of sole authorship, individual
productivity, a performance grade, or compensation advice.

## Delivered selectors

One command accepts exactly one selector family:

```text
eh change portfolio <repository> --pr <number-or-url> --pr <number-or-url>
eh change portfolio --manifest <portfolio.json>
eh change portfolio <repository> --author <alias> [--author <alias> ...]
  --since <instant> --until <instant>
```

Repeated PR selectors use the existing optional `gh pr view` boundary only to
resolve number/URL and immutable base/head object IDs. Every object must already
exist in the selected local Git database. A versioned manifest can select PRs
from multiple local repositories and assigns explicit caller repository and row
IDs. Relative repository paths resolve from the manifest's directory and remain
execution-only; they are never copied into the report. Repository IDs and resolved
local Git roots must have a one-to-one mapping, preventing caller labels from
silently combining separate repositories or bypassing same-repository overlap.

Author-period selection is bounded to 10,000 reachable commits and 128 selected
rows. It provides:

- exact case-insensitive aliases matching author name, email, or
  `Name <email>` display form;
- inclusive `--since` and exclusive `--until` instants;
- an explicit timezone for offset-free values, with skipped or ambiguous local
  daylight-saving times rejected;
- an explicit author-versus-committer date field;
- merge exclusion by default or explicit first-parent valuation;
- explicit inclusion or exclusion of valid `Co-authored-by` trailers; and
- a pinned reachable-history head boundary.

Identity and time select rows only. They do not enter any effort rule or
multiplier. Git returns only structurally extracted co-author trailer values;
commit messages are not returned to EffortHours or retained in contracts/output.

## Reconciliation policy

Each selected row is first estimated independently through the canonical Change
engine with pricing disabled. The portfolio reconciler then:

1. groups rows by caller-visible repository identity;
2. exposes every immutable base context used by those rows;
3. keeps disjoint connected components additive;
4. suppresses exact represented PR patch identities within one repository,
   including cherry-picked equivalents, while never deduplicating across
   repositories;
5. maps non-shared category hours through each work item's cited path evidence and
   normalizes the same path/category with an order-independent maximum, preserving
   independent code, test, and documentation contributions on other paths;
6. follows selected author commits chronologically, excludes an exact object
   chain that returns a path to its initial state, and preserves a later
   reintroduction after a revert;
7. represents shared specification, setup, design, validation, and review context
   once inside an overlapping connected component, not globally across disjoint
   work; and
8. applies the optional rate only after normalized EHE is complete.

Opposing PR effects are not replayed in caller order. They are retained once with
explicit ordering uncertainty. Interleaved author commits that do not form one
exact object chain remain represented with explicit attribution uncertainty.
This is deterministic structural normalization, not a causal-credit model.

Every item retains its isolated low/expected/high Change estimate. Repository
groups and the portfolio expose normalized low/expected/high totals, named signed
adjustments, and an expected-hour allocation per row. Allocations use deterministic
largest-remainder rounding and sum exactly to the normalized expected total.
They are audit allocations, not reconstructed hours worked.

## Contracts and output

The v1 schema catalog adds:

- `change-portfolio-manifest`; and
- `change-portfolio-report`.

Contracts keep selection, immutable base contexts, source Change identity,
observed patch/evidence digests, isolated estimates, normalized categories,
attribution metadata, signed adjustments, exact allocations, diagnostics,
verification, and pricing separate. JSON and Markdown renderers emit no source
excerpt or local repository path. Markdown gives every selected row, repository
normalization group, adjustment, uncertainty, and safety warning its own visible
ledger.

## Verification

Memory-only fixtures cover:

- clean disjoint additivity and selector-order independence;
- exact duplicate/cherry-pick suppression and zero duplicate allocation;
- overlapping PR paths and unresolved order;
- mechanically split sequential edits on one represented path;
- implementation, tests, and documentation delivered in separate additive rows;
- shared test and documentation paths normalized without erasing independent
  implementation paths;
- exact chronological add/revert net zero;
- standalone deletion remaining represented;
- add/revert/reintroduction remaining nonzero;
- identical patches in separate repositories remaining additive;
- exact allocation reconciliation and deterministic output;
- author/committer date choice, interval boundaries, aliases, co-authorship,
  merge policy, and interleaving uncertainty; and
- v1 manifest/report schema validation and customer-facing rendering safeguards.

The separate end-to-end suite creates a temporary Git repository and verifies an
offline author-period command, immutable selected objects, deterministic stdout,
exact allocation, no source excerpts or host paths, and an unchanged worktree.
Ordinary unit tests remain storage-independent.

## Safety and limitations

- Ordinary `scan` and repository `estimate` remain history-free. Only the explicit
  author-period selector reads bounded Git identity, timestamp, parent, and
  co-author metadata.
- Exact patch identity and object-chain normalization do not provide general
  semantic clone, rebase, squash, or conflict-resolution equivalence.
- Mechanically split work on distinct paths with no structural overlap remains
  additive. EffortHours does not invent a shared feature identity from filenames,
  timestamps, or contributor identity.
- Shared credit for pair work, reviews, requirements, design, mentoring,
  incidents, debugging, coordination, and work committed by another person is not
  recoverable from repository history.
- Cross-repository totals are additive after independent per-repository
  normalization. Similar-looking work in different repositories is never silently
  deduplicated.
- Portfolio aggregation does not widen Change model admission. Each source item
  inherits the experimental model's existing ecosystem and 4-to-32-hour Stage A
  boundary; the portfolio reconciler itself has no empirical production
  validation.
- Ranking, grading, performance-review scoring, and compensation workflows are
  deliberately unsupported rather than deferred product features.

Future work may add reviewed public multi-PR/author-period examples, broader
semantic equivalence, and measured large-history/monorepository performance. It
must preserve the selector-only identity boundary and honest uncertainty.
