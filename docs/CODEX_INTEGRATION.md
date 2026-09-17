# Codex companion integration

Status: implemented versioned orchestration guidance

## Purpose

EffortHours packages a small Codex skill so an explicit request for EffortHours,
`eh`, or EHE selects the highest-level native command before an agent starts
discovering repositories or reconstructing estimation logic. The integration is
orchestration guidance only. It does not introduce another estimator, selection
rule, provider adapter, or arithmetic implementation.

The integration contract is `efforthours-codex/1.0.0`. It is versioned
independently from the CLI, repository estimator, Change estimator, and report
schemas, so ordinary package releases do not make an installed skill stale.

## Packaged skill and explicit installation

The public package contains `integrations/codex/efforthours/SKILL.md` as both an
embedded CLI resource and a visible NuGet package asset. The supported commands
are:

```text
eh agent codex
eh agent codex --install
eh agent codex --check
```

The first command prints the exact packaged skill to stdout and never creates a
Codex directory. `--install` is the only mutating form. It writes UTF-8 without a
byte-order mark through a same-directory temporary file and atomically replaces
the user skill at `~/.agents/skills/efforthours/SKILL.md`. The install rejects a
reparse-point destination. `EFFORTHOURS_CODEX_SKILLS_ROOT` may select an explicit
skills root for managed or test environments.

`--check` is read-only and emits one stable status line:

```text
status=current integrationContract=efforthours-codex/1.0.0
status=missing integrationContract=efforthours-codex/1.0.0
status=stale integrationContract=efforthours-codex/1.0.0
```

Current exits zero. Missing and stale exit with the ordinary invalid-input code.
Staleness is exact packaged-content inequality; only an explicit `--install`
updates it. Ordinary estimates never read, create, or rewrite Codex configuration.

## Skill behavior

The skill directs Codex to treat native EffortHours output as the calculation of
record. For today-to-date GitHub requests it runs one `eh change today` from the
user home folder with only caller-requested owner, identity, timezone, open-PR,
scope, capacity, format, and output values.

Because this native mode invokes authenticated `gh`, reads its user
configuration, uses the network, and writes an EffortHours-managed cache, a
sandboxed agent requests sufficient permission on the first attempt when the
environment supports it. The narrow reusable prefix is `eh change today`.

The skill forbids agent-side workspace enumeration, repository scans, manifest
construction, separate `gh` calls, clones, helper scripts, EffortHours source
inspection, and manual aggregation for native today mode. It allows the native
command to finish, surfaces native progress, and keeps EffortHours end-to-end
runtime separate from conversation latency. It never interprets EHE as actual
labor and never infers actual hours from capacity.

## Structured failure action

Every incomplete today setup report carries an optional failure-level
`agentAction`; today workflow failures require it. The same action is emitted as
one compact JSON stderr record after progress diagnostics:

```json
{
  "schema": "efforthours-agent-action/1.0",
  "failureCode": "github-cli-config-access-denied",
  "phase": "provider-authentication",
  "suggestedAction": "retry-exact-command-with-permission",
  "suggestedApprovalPrefix": ["eh", "change", "today"],
  "retryLimit": 1
}
```

JSON reports serialize those fields under the preserved root failure. Markdown
reports render the same compact action. Category, safe message, and opaque message
digest remain available for correlation. The serialized action and message never
contain provider stderr, credentials, raw aliases, owner/repository display names,
PR numbers, local repository paths, provider configuration paths, or managed-cache
paths.

Stable codes distinguish:

- `github-cli-executable-missing`;
- `github-cli-config-access-denied`;
- `github-cli-unauthenticated`;
- `github-owner-forbidden-or-not-found`;
- `github-provider-rate-limited`;
- `github-network-unavailable`;
- `github-provider-response-malformed`;
- `managed-cache-access-denied`; and
- bounded generic scope/provider failures when no narrower safe classification is
  supported.

Only `github-cli-config-access-denied` authorizes
`retry-exact-command-with-permission`, the exact three-token approval prefix, and
one retry. Every other action has an empty approval prefix and a zero retry limit.
An agent reports the incomplete result instead of substituting external discovery
or arithmetic.

## Provider batching and exact fallback

Provider discovery retains live provider inventory plus exact local Git selection.
Optimization never turns a likely-candidate surface into selection authority:

- the authenticated viewer is resolved live on every invocation;
- the requested owner's repository inventory is fully paginated live on every
  invocation and remains the authoritative candidate membership;
- eligible default branches use at most four concurrent GraphQL calls, each with
  at most 12 repositories and 100 interval commits per repository;
- incomplete/malformed repository histories fall back only for the affected
  repository; batch-level failures fall back for that batch and preserve all
  successful other batches;
- a single `--author @me` or explicit GitHub login uses the selected user's direct
  pull-request connection, accepts it only when fully paginated nodes equal its
  `totalCount` within 1,000, then resolves matching PR commits with at most four
  concurrent requests; and
- email/name aliases, supplemental aliases, team selection, or an incomplete
  account connection use the fully paginated per-repository open-PR path.

The selected contributor is independent of authentication. The explicit viewer
login resolves the same verified emails as `@me`; another login can use
provider-linked commit author emails observed in the current responses, bounded
by the existing alias limit. Unlinked/coauthor-only identities still require
explicit aliases. The viewer is never implicitly included in another user's
PR inventory. Both discovery paths pin immutable object IDs and return to local
Git for authoritative manifest selection and analysis.

## Provider metadata cache

The private cache uses `github-provider-metadata-cache/1.1.0`, keyed by a digest
of owner plus the live authenticated viewer. Owner type and optional verified
viewer emails expire independently within 24 hours of observation. Hits preserve
their original expiry; selecting another user can cache owner type without
inventing a fresh empty viewer-email set. All identity forms write usable
metadata. Legacy protocol entries trigger one cold refresh. Owner/viewer
mismatch, expiry, invalid freshness, malformed or oversized content, and
unsupported protocols prevent reuse.

EffortHours always refreshes the complete live repository inventory, so the cache
cannot hide a new, renamed, archived, mirrored, or default-branch-changed repository.
Writes are atomic. By default the cache lives in local application data;
`EFFORTHOURS_PROVIDER_CACHE` selects an explicit root, and a configured
`EFFORTHOURS_REPOSITORY_CACHE` keeps metadata beneath that managed root.

## Operational telemetry

Today discovery reports provider query/page count, child-process count, cumulative
child startup time, and provider metadata-cache hit status. The optional v1
`providerDiagnostics` object adds a fixed cache hit/miss reason, default-head
batch/query counts, account-wide PR/query counts, and aggregated fallback
phase/reason/repository counts. It contains no identities, paths, or provider
response text and remains compatible with older reports that omit it.
Execution phase timing
separates:

- `provider-authentication`;
- `owner-inventory`;
- `candidate-discovery`;
- `default-head-discovery`;
- `open-pr-discovery`; and
- `provider-process-startup`.

These fields account for both optimized and complete-fallback calls, including the
observed 138-query cold-path shape. They are operational observations only:
provider/cache timing, process/query counts, cache hits, and conversation latency
never enter semantic digests, selection rules, EHE, or X arithmetic.

## Verification

Acceptance coverage includes embedded/public package identity, print without
mutation, missing/current/stale checks, explicit atomic install/update, provider
failure classification and privacy, schema-validated JSON/Markdown incomplete
reports, exact one-retry policy, batched-versus-REST commit identity, forced
default/open-PR completeness fallback, exact GraphQL endpoint invocation,
constant account-wide PR request counts for 1 and 247 repository inventories,
bounded open-PR detail concurrency,
metadata cache reuse, process/query/startup accounting, and the existing
unrelated-folder native today workflow.

Live GitHub latency and total agent/conversation latency remain manual
measurements. CI gates deterministic semantics, query/process/reuse counts,
privacy, and bounded fallback behavior; it does not gate provider wall-clock time.
