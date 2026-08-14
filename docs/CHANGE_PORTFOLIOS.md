# Change portfolio reconciliation

## Current boundary

`change-portfolio/0.1.0` composes canonical Change estimates selected as repeated
pull requests, a versioned multi-repository manifest, or a bounded author-period.
It remains experimental and has no empirical production validation.

The result is **repository-attributed Change EHE**: counterfactual replacement
effort represented by selected immutable changes after portfolio normalization.
It is not actual labor, a timesheet, proof of sole authorship, individual
productivity, a performance grade, or compensation advice.

Portfolio reconciliation changes no source Change prior, frozen report, label, or
model-admission decision. Every source item retains the model status and size/
ecosystem boundary of its canonical Change estimate.

## Selectors

One command accepts exactly one selector family:

```text
eh change portfolio <repository> --pr <number-or-url> --pr <number-or-url>
eh change portfolio --manifest <portfolio.json>
eh change portfolio <repository> --author <alias> [--author <alias> ...]
  --since <instant> --until <instant>
```

Repeated PR selectors use the optional `gh pr view` boundary only to resolve a
number/URL and immutable base/head object IDs. Every object must already exist in
the local Git database.

A versioned manifest can select PRs from multiple local repositories and assigns
explicit caller repository and row IDs. Relative repository paths resolve from
the manifest directory and remain execution-only; they are never copied into the
report. Repository IDs and resolved Git roots map one-to-one so caller labels
cannot combine unrelated repositories or bypass same-repository overlap.

Author-period selection is bounded to 10,000 Git-prefiltered identity candidates
and 128 selected rows. Git may traverse a larger reachable graph without returning
its unrelated identity records to EffortHours. The exact selector then validates
structured author/co-author identities and the requested time policy. It provides:

- exact case-insensitive aliases matching author name, email, or
  `Name <email>`;
- inclusive `--since` and exclusive `--until` instants;
- an explicit timezone for offset-free values, with skipped or ambiguous daylight-
  saving times rejected;
- explicit author-versus-committer date selection;
- merge exclusion by default or explicit first-parent valuation;
- explicit inclusion or exclusion of valid `Co-authored-by` trailers; and
- a pinned reachable-history head.

Identity and time select rows only. They do not enter an effort rule or multiplier.
Git returns only structurally extracted co-author values; commit messages are not
retained in contracts or reports.

## Reconciliation policy

Every row is first estimated independently through the canonical Change engine
with pricing disabled. The portfolio reconciler then:

1. groups rows by caller-visible repository identity;
2. exposes every immutable base context used by those rows;
3. keeps disjoint connected components additive;
4. suppresses exact represented PR patch identities inside one repository,
   including cherry-picked equivalents, but never across repositories;
5. maps non-shared category hours through each work item's cited path evidence and
   normalizes the same path/category with an order-independent maximum while
   preserving independent code, test, and documentation paths;
6. follows selected author commits chronologically, excludes an exact object chain
   that returns a path to its initial state, and preserves later reintroduction;
7. represents shared specification, setup, design, validation, and review context
   once inside an overlapping connected component, not globally; and
8. applies an optional rate only after normalized EHE is complete.

Opposing PR effects are not replayed in caller order. They remain represented once
with ordering uncertainty. Interleaved author commits that do not form one exact
object chain remain represented with attribution uncertainty. This is
deterministic structural normalization, not a causal-credit model.

Every row retains its isolated low/expected/high estimate. Repository groups and
the portfolio expose normalized totals, named signed adjustments, and an expected-
hour allocation per row. Deterministic largest-remainder rounding makes allocations
sum exactly to normalized expected EHE. Allocations are audit values, not
reconstructed work hours.

## Contracts and output

The v1 schema catalog includes:

- `change-portfolio-manifest`; and
- `change-portfolio-report`.

Contracts keep selection, immutable base contexts, source Change identity,
observed patch/evidence digests, isolated estimates, normalized categories,
attribution metadata, signed adjustments, exact allocations, diagnostics,
verification, and pricing separate.

JSON and Markdown output omit source excerpts and local repository paths. Markdown
shows every selected row, repository group, adjustment, uncertainty, and safety
warning in a visible ledger.

## Verification boundary

Storage-independent fixtures cover:

- disjoint additivity and selector-order independence;
- exact duplicate/cherry-pick suppression and zero duplicate allocation;
- overlapping paths and unresolved order;
- mechanically split sequential edits;
- implementation, tests, and documentation delivered in separate rows;
- shared test/documentation paths without erasing independent implementation;
- exact add/revert chains and later reintroduction;
- standalone deletion remaining represented;
- cross-repository patches remaining additive;
- exact allocation reconciliation and deterministic output;
- author/committer date, interval, alias, co-authorship, merge, and interleaving
  policy; and
- manifest/report schema and customer-safety wording.

The process-level suite verifies an offline author-period command on a temporary
Git repository, immutable selected objects, deterministic stdout, exact
allocation, no source/host paths, and an unchanged worktree.

Large Git portfolios additionally use the bounded changed-scope and immutable-
inventory reuse rules in `CHANGE_ESTIMATION.md`. Identity and time still select
rows only; neither the candidate count nor the size of the reachable graph enters
an effort rule.

## Safety and limitations

- Ordinary `scan` and repository `estimate` remain history-free. Only an explicit
  author-period selector reads bounded identity, timestamp, parent, and co-author
  metadata.
- Exact patch and object-chain normalization do not provide general semantic-
  clone, rebase, squash, or conflict-resolution equivalence.
- Mechanically split work on distinct paths with no structural overlap stays
  additive. Filenames, timestamps, and contributor identity do not invent shared
  feature identity.
- Pair work, reviews, requirements, design, mentoring, incidents, debugging,
  coordination, and work committed under another identity cannot be recovered as
  individual credit from repository history.
- Cross-repository totals are additive after independent per-repository
  normalization. Similar-looking work in different repositories is never silently
  deduplicated.
- Portfolio aggregation does not widen Change model admission.
- Ranking, grading, performance-review scoring, and compensation workflows are
  deliberately unsupported.

Future evidence may justify reviewed public portfolio examples, broader semantic
equivalence, cross-platform/concurrent monorepository measurements, and universal
regression thresholds. Any extension must preserve the selector-only identity
boundary and explicit uncertainty.
