# GitHub-assisted today-to-date author portfolios

Status: implemented explicit orchestration boundary

## Purpose

The common “what is my EHE-to-capacity ratio today?” workflow is available as one
CLI invocation:

```text
eh change portfolio \
  --owner <github-owner> \
  --workspace <local-checkout-root> \
  --author "@me" \
  --today \
  --timezone America/Toronto \
  --include-open-prs \
  --fetch-missing \
  --capacity-hours 8 \
  --format markdown \
  --no-rate
```

This command composes provider discovery, immutable object acquisition, the
existing v1 author-period manifest, portfolio estimation, one partial daily
bucket, and a caller-supplied reference denominator. It does not introduce a
second estimator or change an EHE prior.

## Trust boundary

`--today` is an explicit opt-in to GitHub access through the authenticated `gh`
CLI. The adapter may query provider metadata and, only with `--fetch-missing`,
perform narrow Git acquisition. Once every selected head is an immutable local
object, the adapter hands an in-memory `change-author-period-manifest/1.0.0` to
the ordinary local planner and estimator.

The handoff has these invariants:

- repository paths and aliases remain execution-only;
- the report retains privacy-safe IDs, immutable object IDs, policies, digests,
  discovery counts, and completeness state;
- provider activity, commit count, time, identity, and PR reachability only
  discover or select rows and never change effort; and
- target code is never built or executed.

The low-level manifest command remains provider-independent and offline. A caller
who needs a reviewed long-lived input can continue to materialize and inspect a
manifest outside this convenience workflow.

## Caller-approved scope

`--owner` and `--workspace` define an owner/workspace intersection. The adapter
lists repositories visible for the requested GitHub owner, scans the bounded
workspace for Git worktrees, normalizes GitHub remote identities, and considers
only unambiguous matches. Directory names are not repository identity.

The workspace scan is bounded, ignores reparse-point traversal, and admits at
most the v1 repository count. A malformed `.git` marker is not a repository
boundary: the scan ignores the marker, continues through other descendants, and
does not traverse the marker's own metadata directory. Git dubious-ownership and
other unreadable-repository failures remain fail-closed because silently omitting
one could publish an incomplete result as complete. A checkout outside the
supplied workspace is outside the approved scope. Multiple checkouts claiming the
same provider identity fail rather than silently choosing one.

Within each mapped repository the adapter pins the current default head. With
`--include-open-prs`, it fully paginates current open PRs and each PR commit
inventory, retains only PR heads containing an interval/identity match, and then
lets local Git repeat the exact author/coauthor/date/merge selection. PR author,
PR age, comments, reviews, and activity counts are not selection proxies.

## `@me` identity resolution

`--author "@me"` forms candidate exact aliases from:

- the active GitHub login;
- verified GitHub-associated commit emails when that endpoint is authorized;
- `user.name` and `user.email` from mapped local checkouts; and
- any additional repeated explicit `--author` values.

The local selector remains authoritative: aliases match Git author name, email,
or `Name <email>`, plus valid `Co-authored-by` identities under the chosen policy.
The report records only an identity-source classification and digests, never the
raw aliases. Provider account association can conservatively admit an open head
for local verification; it cannot by itself create an EHE row.

## Today and capacity semantics

`--today` means local midnight through the next local midnight in `--timezone`.
The start is inclusive and the end is exclusive. Named-zone conversion handles
offset changes; a non-unique or invalid boundary fails closed. The report carries
an explicit UTC `asOf` instant and marks the single daily bucket `partialEnd=true`.

`--capacity-hours` is a positive full-day reference denominator. It creates the
single contributor/bucket capacity cell internally. The reported ratio is:

```text
expectedRatio = expected EHE / reference capacity hours
```

Capacity does not change EHE and is not attendance, actual labor, productivity,
performance, compensation, or schedule duration. JSON preserves the six-decimal
ratio contract; Markdown rounds ratios to two decimals for display.

A fully completed discovery and local selection with no matching commits is a
valid zero report. Any discovery, acquisition, or repository-analysis failure
exits nonzero and never publishes a partial aggregate or a misleading zero.

## Object acquisition

Every provider head is first checked in its mapped local object database. Missing
objects fail unless `--fetch-missing` is present. The acquisition command requests
only the discovered default/open-PR source refs and uses:

- no checkout;
- no tag fetch;
- no submodule recursion;
- no local-ref update; and
- no `FETCH_HEAD` update.

The acquired object ID is verified after fetch. A moved or deleted provider ref
fails instead of substituting a different head.

## Output and diagnostics

JSON and concise Markdown write to stdout unless `--output` is supplied. The
Markdown result leads with expected ratio, EHE, capacity, range, `asOf`, selected
change count, active repository count, open-head count, and shared-credit count.

The versioned report additionally records:

- discovery protocol and privacy-safe scope digest;
- provider/workspace/considered/active repository counts;
- provider query and page counts;
- default/open head counts;
- local versus acquired object counts;
- discovery, repository-shard, and one-command elapsed observations; and
- the existing deterministic selection, execution, cache, resource, and
  reconciliation lineage.

Operational timings and discovery observations are excluded from the semantic
digest. Reordering provider pages, repositories, aliases, or heads cannot change
semantic output for the same immutable handoff.

## Failure and privacy policy

Provider authentication, authorization, malformed or incomplete pagination,
ambiguous remote mapping, missing objects, moved refs, invalid time boundaries,
and cancellation fail closed. Ordinary errors redact repository paths and raw
identity aliases. Credentials, provider response bodies, source excerpts, local
paths, raw aliases, repository display names, and PR numbers are not copied into
reports.

Network discovery necessarily reveals the authenticated account and requested
owner/repository scope to GitHub. The caller opts into that disclosure by using
`--today`; ordinary repository, change, and manifest estimation retains the
offline boundary.

## Verification boundary

Ordinary CI uses fake-provider JSON, local synthetic Git repositories, contract
validation, privacy assertions, complete-zero coverage, relevant-open-head
filtering, and a process-level one-command smoke test. Live-provider access and
wall-clock targets remain manual diagnostics rather than CI gates.
