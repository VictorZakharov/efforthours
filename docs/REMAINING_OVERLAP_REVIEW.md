# Remaining overlap review

## Result and claim boundary

One further evidence defect is corrected: Rust treated any `std::sync` reference
as concurrency, including shared ownership alone, and also accepted bare
atomic-looking names without standard-library qualification. Qualified locks,
coordination primitives, channels, atomics, thread spawning, and existing external
runtime signals remain eligible. Source construction and maintained edits remain
represented. No numerical prior was fitted, clamped, or subtracted.

This follow-up uses the same private frozen October 1 inputs as
[the preceding budget review](REMAINING_BUDGET_REVIEW.md): implementation profile,
Toronto timezone, committer selection, first-parent merges, co-authors excluded,
46 selected changes, and cutoff `2026-10-01T23:39:39.602Z`. Private source,
manifest identities, aliases, paths, object IDs, native capability IDs, reports,
and audit scripts remain in ignored local storage. Only public rule names and
anonymized aggregates appear here.

Baseline is the merged Change 0.21.2 / portfolio 0.6.2 / Rust analyzer 0.1.0.
Correction is Change 0.21.3 / portfolio 0.6.3 / Rust analyzer 0.1.1. Both reuse
`seed-rules/0.4.0`. Schemas, endpoint proof, complete context, independent-day
policy 1.1.0, and allocation rules are unchanged. New producer identities prevent
reuse of old-producer receipts. This is a correctness diagnostic, not calibration,
admission, release publication, or proof that the remaining hours are reasonable.

## Frozen operand movement

| Project | Prior signed stock | Corrected signed stock | Prior Change | Corrected Change | Corrected Change minus stock |
| --- | ---: | ---: | ---: | ---: | ---: |
| A | 301.00 | 300.75 | 290.75 | 290.00 | -10.75 |
| B | 153.25 | 153.25 | 183.75 | 183.75 | 30.50 |
| C | 11.50 | 11.50 | 23.15 | 23.15 | 11.65 |
| Total | 465.75 | 465.50 | 497.65 | 496.90 | 31.40 |

Both operands must be disclosed. Comparing corrected Change with the saved prior
stock alone gives 31.15 hours; that is a cross-producer diagnostic, not the
corrected same-producer bridge. Neither stock value is a truth label for Change.

A's base/head whole-repository seed totals move from 5,475.75 / 5,776.75 to
5,468.00 / 5,768.75. The only changed seed-rule total is `manual-validation`:
-7.75 at base and -8.00 at head. Removing unsupported semantic evidence affects
the existing QA rule's evidence basis; it does not change its rate. Source and
specialized background stock-rule totals themselves are unchanged. B and C seed
expected totals are unchanged. These large stock offsets mostly cancel in signed
growth; hiding either endpoint would obscure that behavior.

| Project | Stock | Growth omitted | Modification excess | Fallback | Change-level | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 300.75 | -76.25 | 24.75 | 24.00 | 16.75 | 290.00 |
| B | 153.25 | -8.50 | 15.25 | 7.75 | 16.00 | 183.75 |
| C | 11.50 | -0.25 | 1.00 | 4.00 | 6.90 | 23.15 |
| Total | 465.50 | -85.00 | 41.00 | 35.75 | 39.65 | 496.90 |

Negative stock credit, removal work, and net category redistribution remain zero.
The native bridge reconciles exactly:
`465.50 - 85.00 + 41.00 + 35.75 + 39.65 = 496.90`.
The net difference cancels 10.75 hours below stock in A against 42.15 above stock
in B and C. A small aggregate difference therefore cannot certify each project.

## Corrected Rust evidence

The previously charged Rust background group bound to 24 represented files. All
24 head facts had `background-usages=0` and `concurrency-usages=1`. This did not
mean 24 independent background jobs. The old recognizer accepted the namespace
`std::sync`, or the identifiers `AtomicUsize` / `AtomicBool`, anywhere in a file.

A bounded private lexical check found 12 of those files with only `Arc` among the
recognized ownership/synchronization names. Two further files had atomic-looking
names without a directly qualified `std::sync` path. Such unresolved cross-file
names cannot establish standard-library coordination. The corrected native group
binds to 10 files and falls from 8.00 to 6.50 hours under the unchanged modification
rule. Seven retained files also carry Rust test evidence, versus 17 previously.

Four newly unexplained represented production paths enter ordinary fallback;
A's production fallback path count grows from 73 to 77. Its total fallback rises
by 0.75 hours, while modification excess falls by 1.50. No file is dropped solely
because its concurrency evidence was rejected. The net Change movement is -0.75.

Decision: unsupported concurrency eligibility corrected. Shared ownership remains
ordinary source evidence. Qualified concurrency scope remains plausible, but its
6.50-hour magnitude and separation from embedded tests are unvalidated.

The bounded recognizer supports direct standard-library paths and grouped imports,
including nested atomic imports and symbol aliases, with at most 256 qualifier
tokens examined per namespace prefix. It rejects ownership renamed
to a lock-looking name and a visible local `std` module. It does not resolve
wildcards, namespace aliases, arbitrary cross-file imports, or runtime behavior.
Those missing qualifications are uncertainty, not proof of absent functionality.

## Remaining specialized scope

Counts below are charged represented paths, not repository-wide fact counts or
independent calibration observations. A test fact means test-bearing evidence;
it does not prove the entire file is a test artifact.

| Project | Specialized group | Budget | Charged paths | Paths also carrying test facts | Evidence decision |
| --- | --- | ---: | ---: | ---: | --- |
| A | Rust background/concurrency | 6.50 | 10 | 7 | Qualified coordination retained; production versus embedded test scope unresolved |
| A | JavaScript background | 3.50 | 2 | 1 | Two located background facts; lifecycle versus test-support overlap unresolved |
| A | Rust entry point | 1.00 | 1 | 1 | Entry and example/test evidence coexist; separate entry wiring not established |
| A | Scripting entry point | 1.00 | 1 | 0 | Located command/entry growth; numerical modification alternative unvalidated |
| A | JavaScript external integration | 1.25 | 1 | 1 | Located integration-call growth; test-support versus production integration unresolved |
| A | Cargo build tooling | 0.50 | 1 | 0 | Same descriptor as release; combined task may overlap |
| A | Cargo packaging/release | 0.50 | 1 | 0 | Example target added; separate delivery task not established |
| A | Scripting validation | 1.25 | 1 | 0 | Located validation growth; magnitude and supporting-work overlap unresolved |
| B | Python entry points | 2.00 | 2 | 0 | Located entry growth; source construction versus entry wiring unresolved |

The JavaScript test facts in the shared background/integration paths report zero
test cases, suites, assertions, mocks, and accessibility checks. They establish
test context, not independent positive test-case units or proof of a duplicated
production task. The Rust shared paths contain in-source cases/assertions; blanket
removal of every test-bearing file could discard production coordination.

The Cargo descriptor's build target count changes from 10 to 11, and its delivery
example-target count from zero to one. The package context also changes dependency
and source-file counts. This is not evidence of two separate implementation tasks:
adding one example target can alter both build and delivery projections. Conversely,
a shared descriptor alone does not prove that all build and release work is
identical. Decision: probable scope overlap requiring a combined task judgment;
neither charge is certified numerically distinct and neither is deleted speculatively.

These findings narrow the review questions without converting shared paths,
namespace counts, or analyzer facts into reviewed hours. The earlier source/test
aggregate ambiguity remains open, including the unchanged Python stock model.
This correction does not rescale its measurements or certify saved stock.

## Fallback and supporting work

Fallback still has no charged path-ID intersection with capability budgets. That
establishes separation at the native path ledger only. An integration capability
may already cover a helper edit on another path, so feature-level overlap remains
unresolved. Path-count bands are not evidence of independent tasks.

The 39.65 hours of Change comprehension, validation/debugging, and review/integration
are unchanged. Their named scopes differ from source construction and test
creation, but static evidence does not prove the numerical priors are disjoint.
The corrected seed QA movement above further demonstrates that producer evidence
eligibility can affect supporting totals even when source construction is unchanged.
A blanket subtraction of supporting work or all fallback remains unjustified.

## Material assessment needed next

Further numerical improvement needs a credible assessment of the coherent final
Change total for each project. A signed difference of repository stock is a
comparison operand, not that assessment. No new independent low/expected/high
Change labels were supplied or manufactured in this investigation.

Use only a few material work areas and one bounded residual. Record immutable
source boundaries, supplied specification assumptions, contractor baseline,
functional/quality scope, reviewer identity, confidence, and whether prior/candidate
hours were visible. These already inspected private cases are development
diagnostics; they cannot become fresh held-out evidence by hiding their totals now.

Compare each final-Change total and range first. If a credible range accepts the
estimate, stop. For a material miss, inspect only the largest contributors needed
to change the decision: qualified coordination plus embedded tests, integration
plus test support, the combined example/build/release task, fallback supporting
edits, and once-only validation/review. Record retain, duplicate, unsupported, or
unresolved against the combined task, with scope and uncertainty. Do not assign
hours to every analyzer row or force equality with stock.

This follows [CALIBRATION.md](CALIBRATION.md)'s materiality discipline as a review
method; it does not expand repository admission to Change. Any fitted correction
still needs its own candidate identity, governed Change evidence, and a fresh
validation boundary. Existing admission records and sealed test remain untouched.

## Verification

Public in-memory regressions distinguish ownership, namespace/name coincidences,
ordinary async syntax, alias boundaries, qualified locks, nested atomics, channels,
and thread spawning. A Change regression confirms a meaningful ownership edit
remains positive without a background task. Synthetic Markdown golden digests
advance only for the source/portfolio identity text.

The frozen native rerun passes all 46 original selected identities, all three
original endpoint boundaries, exact native endpoint/daily category and range
agreement, exact expected allocations, and October 1 ledger equality when
September 29-30 are added. All 12 old-producer receipts invalidate; recalculation
matches the cold semantic digest. A warm repeat hits all 12 receipts, has zero
snapshot/inventory/blob requests and blob-read bytes, and preserves the digest.

All 18 serialized native evidence, seed, Change, and comparison documents validate
against checked-in JSON schemas and semantic contracts. All 973 unit tests and
305 end-to-end tests pass. Locked restore, formatting verification, Release build,
package creation, file budgets, Markdown links, and UTF-8/LF checks pass. Required
GitHub checks are separately inspected on the PR's current head before handoff.
Private artifacts retain operational receipts; they are excluded from the PR.
