# Host-assisted author-period manifest scaffolding

Status: accepted design; optional implementation deferred

Decision date: 2026-08-14

## Decision

Host-assisted discovery must remain outside the static estimator. If implemented,
the first provider integration should be an optional companion adapter that writes:

1. an ordinary `change-author-period-manifest/1.0.0` document; and
2. a separate, local-only discovery provenance document.

The caller reviews and pins that output before invoking:

```text
eh change portfolio --author-period-manifest <manifest.json>
```

The core command does not call the adapter, contact a provider, fetch Git objects,
or interpret provider metadata. Once the reviewed manifest and local object
databases are fixed, estimation retains its existing offline, deterministic
behavior.

This decision does not add a command, schema, provider dependency, or estimation
rule. It defines the boundary an optional implementation must satisfy.

## Why a companion adapter

Three shapes were considered:

| Shape | Result | Reason |
|---|---|---|
| A provider-specific `eh change portfolio scaffold` subcommand | Rejected for the first implementation | It brings authentication, network behavior, pagination, and provider release cadence into the core CLI. |
| An optional companion adapter that emits v1 | Recommended | It creates a hard, reviewable handoff while reusing the stable provider-independent contract unchanged. |
| A general provider plug-in surface | Deferred | One provider and one workflow do not justify a public extension contract yet. Extract a provider-neutral orchestration library only after another implementation demonstrates the common boundary. |

The existing optional `gh pr view` resolver is intentionally narrower: it resolves
an explicitly named pull request to base and head object IDs. Broad repository and
open-change discovery has materially different privacy, completeness, and failure
semantics and should not expand that boundary implicitly.

## Boundary

The workflow has two distinct trust domains:

```text
caller-approved scope
        |
        v
optional provider adapter --network--> hosting provider
        |
        +--> reviewable v1 manifest
        +--> local-only provenance
                    |
                    v
             human review and pin
                    |
                    v
offline `eh change portfolio` --> deterministic report
```

The adapter may discover candidates. It may not estimate them. The estimator may
analyze pinned local objects. It may not broaden the reviewed scope.

Identity, time, repository activity, commit count, pull-request count, and head
reachability remain selectors or diagnostics only. None may alter an effort prior,
work item, multiplier, uncertainty factor, reconciliation rule, or allocation.

## Caller-approved input

The adapter should require an explicit local request or catalog. It contains:

- the shared interval, timezone, date field, merge policy, and co-author policy;
- caller-chosen contributor IDs and exact Git display-name/email aliases;
- optional provider account IDs used only to discover open changes;
- caller-chosen repository IDs, local paths, and expected provider repository
  identities;
- the repositories, owners, or organizations the caller permits the adapter to
  query; and
- whether current open changes are discovered exhaustively within that scope or
  restricted to an explicit provider-account/change allowlist.

Provider accounts and Git identities are separate inputs. A hosting-provider login
must never be converted automatically into a Git author alias. Provider APIs expose
raw Git author metadata separately from the hosting account associated with a
commit, and either association can be absent or different. Exact contributor
matching therefore remains local and follows the existing manifest semantics.

An organization-wide repository query may propose catalog entries, but proposed
repositories must not flow directly into an executable manifest. The caller first
admits a stable repository ID and local path. This avoids silent scope expansion,
implicit cloning, and unreliable repository selection based on host activity or
commit search.

## Discovery rules

For every admitted repository, the adapter may resolve:

- the provider's stable repository identity and current display name;
- the current default branch and its immutable commit object ID; and
- current open-change heads allowed by the request.

The adapter must use complete pagination for every list query. Provider ordering
must not affect output; repositories, contributors, aliases, and heads are
canonicalized before the v1 manifest is written.

Open-change discovery has two explicit modes:

- **Repository-complete:** inspect all currently open changes in each admitted
  repository. This avoids treating provider account identity as author
  attribution, but can exceed provider or v1 execution budgets.
- **Caller-filtered:** inspect only explicitly admitted changes or changes opened
  by listed provider accounts. This is a discovery convenience, not proof that all
  matching Git commits were found. The provenance document records the restriction.

The adapter must not filter default-branch history or value work using provider
activity, pull-request dates, review events, comment counts, commit counts, or
search ranking. The local author-period selector applies the exact time and Git
identity rules after all heads are pinned.

Only heads that are open at discovery time are discovered automatically. Closed-
unmerged, deleted, or historical heads are never inferred. A caller who wants one
must supply and admit its immutable object ID explicitly.

## Immutable IDs and local preflight

Every generated head entry contains a full immutable commit object ID. The adapter
must verify, before producing an executable manifest, that:

- each admitted local repository exists and resolves to one Git root;
- each provider repository maps to the expected admitted repository identity;
- every pinned object exists as a commit in that repository's local object
  database; and
- the complete output fits the existing v1 repository, head, contributor, alias,
  selected-commit, and input-size budgets.

The adapter must not clone, fetch, checkout, update a worktree, install a
dependency, or execute target code. Missing objects are an actionable failure: the
caller may fetch them through a separately approved workflow and rerun discovery.

A fork-based open change is still represented under the admitted target
repository. Its pinned head must already be reachable by object ID from that local
repository's object database. Provider fork coordinates remain in provenance only.

Equal head object IDs within one repository are deduplicated before manifest
materialization. The provenance document retains every provider source that led to
the object. Equal object-ID text in different repositories remains distinct.

Caller-chosen contributor and repository IDs cross into the report. The default
head may use the reviewed ID `default`. An adapter-generated open-head ID should be
a privacy-safe deterministic token derived from stable provider repository and
change IDs; the sidecar maps it back to the provider record. The caller may replace
that suggestion with another admitted public ID during review.

## Repository renames and forks

Provider repository names are mutable. The adapter keys discovery provenance by a
stable provider repository ID, records the currently resolved owner/name, and
warns when it differs from the catalog. It must not silently remap a catalog entry
by display name alone. This matters because redirects after a repository rename
can stop working if the old name is reused.

Fork status, source repository identity, and cross-repository change status are
recorded in provenance. They do not create a second EffortHours repository group
unless the caller separately admits that repository and local object database.

## Provenance sidecar

The v1 manifest deliberately contains no provider query history. A separately
versioned, local-only provenance document should record enough information to
review discovery without changing report identity:

- adapter name/version, provider, API version, and provider hostname;
- discovery start/completion instants and request digest;
- admitted provider repository IDs, resolved current names, and catalog mismatch
  warnings;
- query type, filters, page count, item count, and completion status for every
  paginated query;
- available rate-limit/reset metadata and any retry decision;
- discovered default/open-change IDs and pinned commit object IDs;
- fork, inaccessible/deleted-head, duplicate-object, and omission diagnostics;
- whether discovery was repository-complete or caller-filtered;
- local preflight results without copying source or Git object contents; and
- the canonical digest of the emitted v1 manifest.

The sidecar may contain provider account names, repository names, change numbers,
URLs, local paths, and discovery filters. It is sensitive operational data: keep it
out of reports, stdout, fixtures, committed examples, and default logs. The v1
manifest also contains execution-only local paths and aliases and should be stored
and reviewed accordingly.

Network discovery necessarily reveals the authenticated caller and the requested
repository, organization, account, or change filters to the provider. The caller
must opt into that disclosure. An adapter should request the least privilege needed,
must never copy credentials into either output, and must redact provider response
bodies and authorization details from ordinary diagnostics.

An implementation should require explicit output paths, refuse accidental
overwrite by default, and write the executable manifest atomically only after all
queries and local preflights succeed. A failed run may write a clearly marked
incomplete diagnostic/provenance artifact, but never a manifest that appears ready
for estimation.

## Failure policy

The default is fail closed. The adapter must not emit an executable manifest when:

- authentication or authorization prevents complete discovery;
- any requested page is missing, truncated, or malformed;
- a provider returns an inaccessible, deleted, or unresolved admitted head;
- a rate or secondary limit interrupts discovery;
- a repository identity is ambiguous or conflicts with the admitted catalog;
- a pinned object or local repository is missing;
- provider results exceed manifest or configured discovery budgets; or
- cancellation occurs.

On `403` or `429`, the adapter should stop issuing requests and surface the
provider's retry/reset guidance. It should not hide a long retry loop. Requests
should be serialized initially to reduce secondary-rate-limit risk; bounded
concurrency requires separate evidence before adoption.

Strict failure is not a claim that a provider can prove global completeness. The
sidecar states exactly which scopes and filters completed. Inaccessible private
repositories, provider indexing behavior, user-selected filters, and changes not
represented by an open head remain explicit limitations.

## Review and pin workflow

1. The caller prepares the local catalog, stable report IDs, exact Git aliases,
   interval, and allowed provider scope.
2. The optional adapter performs network discovery and writes a candidate manifest
   plus provenance sidecar.
3. The caller reviews repository scope, identity aliases, provider-account filters,
   default/open heads, fork mappings, omissions, deduplication, and warnings.
4. The caller ensures every chosen object exists locally through a separate,
   deliberate Git workflow, then reruns the adapter if preflight previously failed.
5. If review changes scope or public IDs, the caller updates the request and reruns
   materialization. The caller then approves a manifest whose canonical digest
   matches the sidecar and treats that digest as the pinned execution input.
6. In a separate invocation, the caller runs the ordinary host-independent
   estimator. The estimator neither reads the sidecar nor contacts the provider.
7. The caller retains or deletes the sensitive discovery artifacts according to
   local policy. The public report contains only admitted IDs, immutable objects,
   selection policy, and the manifest digest.

Rerunning discovery later may produce a different candidate because default refs,
open changes, permissions, and provider state change. Rerunning estimation with the
same reviewed manifest and unchanged local object databases must preserve the
existing deterministic output contract.

## Test strategy for an implementation

The optional adapter requires tests without live provider access in ordinary CI:

- fake-provider unit tests for pagination, canonical ordering, head
  deduplication, rate limits, redirects, renames, forks, inaccessible heads,
  incomplete discovery, and budget failures;
- contract tests proving the emitted document is accepted unchanged by the v1
  manifest loader;
- tests that provider accounts never become Git aliases and that provider
  activity/count fields never enter estimation inputs;
- process tests with a fake provider executable or HTTP handler for cancellation,
  stderr/stdout separation, atomic output, and no executable manifest on failure;
- local synthetic Git tests for missing objects, fork-head objects, repository
  identity mismatch, read-only worktrees, and no implicit fetch;
- privacy tests proving reports contain no local paths, aliases, provider names,
  account IDs, change numbers, URLs, filters, or sidecar fields; and
- equivalence tests proving a reviewed generated manifest and the same hand-written
  manifest produce byte-identical semantic reports.

Live-provider checks are manual diagnostics, not a CI gate. They must use a
purpose-created public test scope, record provider/API versions, and avoid timing
thresholds or private fixture data.

## Implementation gate

This research decision is complete without shipping the adapter. Implementation
should begin only when a concrete workflow justifies its authentication,
maintenance, and privacy cost. Before release, it needs:

- an explicit adapter/provenance contract and versioning policy;
- a privacy review of queries, local artifacts, logs, and failure output;
- evidence from at least one complete reviewed workflow;
- a documented installation and authentication boundary; and
- confirmation that the core package and estimator remain provider-independent.

A second provider or materially different discovery workflow should trigger a
review of whether provider-neutral orchestration is now warranted. Until then, a
small companion adapter is the narrower public surface.

## References

- GitHub CLI documents explicit REST/GraphQL pagination for
  [`gh api`](https://cli.github.com/manual/gh_api).
- GitHub's REST API documents paginated
  [repository listing](https://docs.github.com/en/rest/repos/repos) and
  [pull-request listing](https://docs.github.com/en/rest/pulls/pulls).
- GitHub CLI documents the immutable base/head fields exposed by
  [`gh pr list`](https://cli.github.com/manual/gh_pr_list).
- GitHub documents [REST API rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)
  and [REST API operational best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api).
- GitHub documents the redirect and old-name reuse implications of
  [renaming a repository](https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository).
- GitHub's commit response separates Git author fields from the associated hosting
  account in the [commits API](https://docs.github.com/en/rest/commits/commits).
