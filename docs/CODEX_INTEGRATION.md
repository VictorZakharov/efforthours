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
- eligible default branches are grouped into at most 12-repository GraphQL calls;
- each batch reads at most 100 interval commits per repository; a provider error,
  missing field, branch mismatch, malformed response, or `hasNextPage` result
  discards the optimization and runs the complete prior per-repository REST path;
- canonical `--author @me --include-open-prs` mode queries the direct user
  pull-request connection, accepts it only when fully paginated nodes equal its
  `totalCount` and do not exceed 1,000, then resolves
  matching PR commits with at most four concurrent requests; and
- explicit/supplemental identity forms or an incomplete/unavailable account
  connection use the complete fully paginated per-repository open-PR path.

Both paths pass the same commit metadata through the same exact
author/coauthor/date/merge selector. Both pin the same immutable object IDs and
return to local Git for authoritative manifest selection and analysis. Tests
compare batched and REST selected object sets and force every completeness
fallback.

## Provider metadata cache

The private cache uses `github-provider-metadata-cache/1.0.0`. It is keyed by a
digest of owner plus live authenticated viewer. Owner type and verified identity
metadata are fresh for 24 hours; the recorded repository metadata snapshot is
fresh for five minutes. Owner/viewer mismatch, expiry, future/invalid freshness,
malformed content, unsupported protocol, oversized content, or invalid bounds
invalidate the entry.

The repository snapshot is diagnostic/reuse metadata only. EffortHours always
refreshes the complete live repository inventory, so the cache cannot hide a new,
renamed, archived, mirrored, or default-branch-changed repository. The cache is
written atomically under the EffortHours provider cache. By default it lives in
local application data; `EFFORTHOURS_PROVIDER_CACHE` selects an explicit root,
and a configured `EFFORTHOURS_REPOSITORY_CACHE` keeps provider metadata beneath
that managed test/deployment root.

## Operational telemetry

Today discovery reports provider query/page count, child-process count, cumulative
child startup time, and provider metadata-cache hit status. Execution phase timing
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
default/open-PR completeness fallback, bounded open-PR detail concurrency,
metadata cache reuse, process/query/startup accounting, and the existing
unrelated-folder native today workflow.

Live GitHub latency and total agent/conversation latency remain manual
measurements. CI gates deterministic semantics, query/process/reuse counts,
privacy, and bounded fallback behavior; it does not gate provider wall-clock time.
