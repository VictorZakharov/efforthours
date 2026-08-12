# Static Go analysis boundary

## Status

Go is a first-class, token-backed EffortHours ecosystem beginning with:

- common scanner `0.2.4`;
- Go analyzer `0.1.0`;
- unchanged repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.9.0+seed-rules/0.4.0`.

Later additive ecosystem extensions advance the current composite source identity to
`change-seed/0.17.0+seed-rules/0.4.0` without changing Go normalization or
valuation behavior.

This boundary is experimental and uncalibrated. It adds static Go evidence and
reuses transparent analogous priors; it does not establish numerical accuracy or
production admission. The admitted Change Stage A slice remains the pre-Go
`change-seed/0.6.0` boundary documented in `CHANGE_MODEL_ADMISSION.md`.

## Admitted files and module ownership

The analyzer admits maintained `.go` files selected by the common scanner and
reads scanner-admitted `go.mod` and `go.work` files. `go.sum` is retained as
lock/build evidence and is not a dependency-volume effort driver.

Static module and workspace discovery records:

- module paths and literal `require` entries from `go.mod`;
- literal local `replace` targets that remain inside repository scope;
- literal `use` and local `replace` entries from `go.work`;
- packages grouped by maintained source directory;
- the deepest containing module as a file's owner; and
- local module-reference edges supported by an import root or local replacement.

When more than one `go.work` is present, analyzer `0.1.0` selects the shallowest
deterministic workspace and emits a warning rather than merging potentially
incompatible workspace contexts.

Modules with a static command entry point or exported symbols expose a bounded
binary/library release surface to the existing packaging and release rule. This
does not claim that a binary was built or a public module version was published.

Outside-repository local paths are rejected with a diagnostic and are never
followed. The analyzer does not run `go env`, `go list`, `go work`, module graph
selection, Minimal Version Selection, dependency download, or workspace commands.
A repository with maintained `.go` files and no manifest receives one explicit
repository-level fallback module rather than an invented external package graph.

## Source structure

A bounded managed tokenizer recognizes identifiers, keywords, numeric literals,
interpreted and raw strings, rune literals, comments, compiler directives,
operators, and delimiters. A conservative declaration/structure pass records:

- packages, files, imports, and imports under the owning module path;
- functions, methods, types, interfaces, exported symbols, and generic
  declarations;
- branch points and common explicit error paths;
- goroutines, channel operations, synchronization calls, and asynchronous units;
- `package main` plus `main` entry points; and
- parser confidence and the exact static-analysis method.

This is token-backed structural evidence, not a Go parser, type checker, compiler,
SSA/control-flow engine, or call-graph analysis. Counts are bounded cognitive
construction signals; physical lines are not the principal effort driver.

## Import-qualified semantic evidence

Framework and standard-library evidence requires a recognized import, including a
local import alias where applicable, and a compatible qualified call or type
shape. Local variables or packages that merely reuse framework names do not
qualify. Recognized bounded surfaces include:

- HTTP APIs through `net/http`, Gin, Echo, Chi, and Gorilla Mux;
- CLI commands/options through `flag`, Cobra, and urfave/cli;
- persistence and migrations through `database/sql`, GORM, sqlx, Ent, Goose, and
  golang-migrate;
- RPC, cloud, HTTP-client, Kafka, and NATS integrations;
- cryptography, JWT, and OAuth security usage;
- cron, Asynq, and Temporal background work;
- validator-based validation; and
- `sync` synchronization usage.

These facts establish represented static surfaces, not runtime configuration,
route reachability, schema validity, authentication correctness, protocol
compatibility, security, or operational behavior.

## Tests

The common scanner classifies `*_test.go` as test source. The analyzer recognizes
static `Test*`, `Benchmark*`, `Example*`, and `Fuzz*` declarations, subtests and
table-driven shapes, common `testing.T` assertions, and import-qualified Testify,
go-cmp, and GoMock usage. Paths containing explicit integration, end-to-end, or
`testdata` conventions are routed conservatively to the corresponding test role;
other Go tests remain unit-test evidence.

Discovered tests are assumed to pass on the default fastest path. EffortHours does
not invoke `go test`, run benchmarks or fuzzing, measure coverage, resolve build
tags, or prove that a test is reachable or valid.

## Build and runtime uncertainty

The analyzer records the presence of:

- `//go:build` and legacy `// +build` constraints;
- recognized operating-system or architecture filename suffixes;
- `//go:embed` declarations;
- `//go:generate` declarations;
- `import "C"`; and
- blank imports that may register runtime behavior.

All scanner-admitted constrained files are analyzed. EffortHours does not select a
GOOS/GOARCH/tag set, expand embedded patterns, check asset existence, execute code
generation, parse or compile cgo preambles, verify an ABI, load a plugin, run
`init`, or prove blank-import/runtime registration effects. These limitations are
emitted as facts, tags, or diagnostics rather than silently treated as certainty.

## Safety, exclusions, and privacy

Every analyzer read is restricted to a scanner-admitted regular file inside the
selected root, then checked against the scanner-recorded byte length and SHA-256
digest. Source text must be valid UTF-8 and no larger than eight mebibytes; each
file is capped at 250,000 tokens. Changed, unsafe, oversized, invalid, truncated,
or malformed inputs fail closed or retain low confidence.

Vendored, conventional generated, minified, binary, build-output, and exact-copy
bodies retain common normalization precedence. Conventional generated Go files
and files in vendor/tool-output paths do not contribute body semantics. Exact
maintained copies are normalized by content digest before structural effort is
valued.

Ordinary evidence and estimate output contains paths, counts, classifications,
declared module/dependency names, technology labels, and reasoning—not source
excerpts, literals, comments, cgo preambles, or embedded asset contents. Paths and
declared names remain repository metadata and should be handled according to the
caller's privacy requirements.

## Estimation and Change EHE

Go source structure consumes the existing `seed-rules/0.4.0`
`polyglot-source-backbone`. Its file/function/method/type/public-symbol/async/
branch rates transparently reuse analogous construction priors with wider
uncertainty. Go semantic evidence continues through existing setup, architecture,
entry-point, API, data, integration, security, validation, background, testing,
build, manual-validation, and review rules. No Go-specific fitted rate, private
observation, or model artifact was added.

Change EHE `0.9.0` admits source-readable `.go` paths to a Go-aware comparison.
Ordinary formatting and comments can normalize to zero. Compiler directives, cgo
comments, literal values, identifiers, operators, and changes to implicit
semicolon placement remain meaningful. Scanner/analyzer evidence then routes Go
final deltas through existing category reconciliation. This is an experimental
extension and does not expand the admitted 4-to-32-hour Change band.

The standalone public Go mutation suite contains 13 project-authored MIT states
and 56 passing relations. It covers formatting/comments, exact duplication,
generated output, API, tests, data, integration, security, background work, build
semantics, concurrency, and local framework namesakes. It is qualitative
invariance, directionality, isolation, and false-positive evidence—not reviewed
hour labels, held-out accuracy evidence, interval calibration, or admission.

## Performance checkpoint

On the documented August 11, 2026 workstation, a fresh process analyzed a
generated 10,000-file, 1,000,003-line Go tree in 6.577 seconds. The sampled process
peak was 119.95 MiB and cumulative managed allocation was 736.15 MiB. Target
metadata was unchanged. The analyzer did not execute target code, invoke the Go
toolchain, install dependencies, or access the network.

This is a many-small-files scalability checkpoint, not a frozen cross-platform
regression threshold or a representative distribution of real Go syntax.

## Explicit non-goals

This boundary does not invoke the Go toolchain; download or resolve modules;
evaluate build constraints; compile, type-check, or link; execute generators,
tests, fuzzers, examples, benchmarks, or target programs; expand embedded assets;
compile cgo; interpret assembly; load plugins; prove reflection, runtime
registration, reachability, race freedom, deadlock freedom, API correctness,
security, or production readiness. General generated-code customization and
toolchain-specific protected regions remain separate safety decisions.
