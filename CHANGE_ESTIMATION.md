# Deferred Change and Contribution Estimation

## Status

Deferred expansion design. This mode is not part of Milestone 6 and is not yet
implemented.

## Purpose

Repository-wide recreation is only one billing context. Fairbill should eventually
estimate the Equivalent Human Effort embodied in a completed incremental change,
while retaining the same current-artifact, no-churn valuation principles.

The output is **Change EHE**: the conventional senior-contractor effort represented
by the final functional and quality delta. It is not elapsed time, historical labor,
or a reconstruction of what a contributor actually did.

## Required selectors

The provider-neutral change engine should support:

- a base and head snapshot or evidence bundle;
- one commit compared with a selected parent;
- a revision range compared by its final base and head;
- a pull request, with GitHub available through an optional `gh` CLI adapter; and
- commits selected by one or more author identities and a time interval, such as a
  calendar month.

Likely CLI shape:

```text
fairbill change --base <ref-or-path> --head <ref-or-path>
fairbill change --commit <revision> [--parent <revision>]
fairbill change --range <base>..<head>
fairbill change --author <identity> --since <instant> --until <instant>
  [--date-field <author|committer>] [--timezone <iana-zone>]
fairbill change --pr <number-or-url> [--repo <owner/name>]
```

The exact commands remain subject to implementation measurements. Local snapshot
and Git-ref inputs must not depend on GitHub. Pull-request resolution may use `gh`
only when the caller explicitly selects it and the executable is installed and
authenticated.

## History boundary

Ordinary `scan` and repository `estimate` commands continue to ignore Git history.
Change mode is an explicit opt-in exception whose history access is limited to:

- resolving requested base, head, parent, PR, author, and time selectors;
- constructing final snapshots and normalized patches; and
- retaining stable provenance for the selected change.

Commit count, elapsed duration, review duration, author count, message length,
timestamp spacing, branch shape, and intermediate churn are never effort
multipliers.

## Valuation semantics

For a PR or revision range, the preferred estimate compares the final base and head
states. Rewriting the same feature ten times inside the range does not increase
Change EHE. The change analyzer should value additions, modifications, deletions,
tests, documentation, migrations, configuration, integration effects, and required
cross-cutting validation as a coherent net result.

A single commit is compared with its first parent by default. Root and merge commits
need explicit, visible rules: a root may use an empty base; a merge should normally
require a chosen parent or emit an ambiguity warning rather than count the merged
branch wholesale.

Formatting, generated output, vendored content, exact duplication, lockfile noise,
and mechanical movement must not create implementation value. Deletion is not
negative hours: deliberate removal or simplification can represent bounded design,
implementation, migration, and validation work, but deleted volume alone is not an
effort signal.

## Author-and-period portfolios

Author and time values are selectors, not labor evidence. Identity aliases, author
versus committer timestamp, timezone, interval inclusivity, merge handling, and
co-authorship must be explicit in the report.

The selected portfolio requires more normalization than a simple range because
other contributors' commits may be interleaved. Before this mode is considered
credible, prototypes must address:

- exact duplicate and cherry-picked patches;
- reverts and net-zero change sequences;
- overlapping edits across selected commits;
- merge commits and rebases;
- co-authored and pair-programmed changes;
- changes whose tests or documentation land in another contributor's commit; and
- work moved across the interval boundary.

Fairbill may expose both per-change rows and a normalized portfolio total, but it
must not sum raw commit estimates when doing so would double count the same final
capability. Any unresolved attribution ambiguity widens ranges and remains visible.

## Use in performance reviews

An author-period report may be useful as one source of evidence in a performance
conversation. Its name and disclaimers must make clear that it measures
**repository-attributed change EHE**, not the person's productivity, value, actual
hours, or total contribution.

Repositories do not capture many valuable activities: requirements work,
architecture discussions, code review, mentoring, incident response, debugging,
coordination, research, pair work, and work credited through another person's
commit. Conversely, commit authorship does not prove sole responsibility for the
change. Fairbill must not generate employee rankings, performance grades, or
compensation recommendations from this signal alone.

## Privacy and safety

The default offline engine should invoke local Git only for an explicitly requested
change operation. The optional `gh` adapter may access network data and credentials;
the CLI must announce that boundary and avoid persisting PR bodies, author emails,
private diffs, or repository evidence unless the caller requests an output path.

Public fixtures must use synthetic identities and repositories. Private company
contribution data must never enter Fairbill's public calibration corpus by default.

## Preconditions for implementation

- Repository-level estimation and reporting must be stable first.
- A language-neutral change-evidence contract must distinguish added, changed,
  removed, moved, generated, and unchanged context.
- Mutation fixtures must prove that internal churn and commit splitting do not
  increase a range or PR estimate.
- Portfolio aggregation and shared-credit behavior need reviewed examples before
  performance-review use is described as anything beyond experimental.
