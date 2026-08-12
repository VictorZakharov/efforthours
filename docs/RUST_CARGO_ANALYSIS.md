# Static Rust and Cargo Analysis

## Status

Rust analyzer `0.1.0` and common scanner `0.2.10` provide an experimental,
offline-only Rust/Cargo evidence path. Repository estimates reuse the unchanged
`seed-rules/0.4.0` language-neutral source backbone and existing specialized
priors. Current Change estimates use `change-seed/0.18.0+seed-rules/0.4.0`.

No Rust-specific rate was fitted. The repository and Change paths are
uncalibrated, the Rust Change extension is outside the admitted
`change-seed/0.6.0` Stage A boundary, and neither path is production-ready.

## Admitted inputs

The common scanner admits:

- maintained `.rs` source, including `build.rs`;
- conventional `tests/` and `benches/` source roles;
- `Cargo.toml` as a package or workspace manifest;
- `Cargo.lock` as mechanical package-manager inventory only; and
- `.cargo/config`, `.cargo/config.toml`, toolchain, rustfmt, and Clippy
  configuration as build/quality inventory.

The analyzer reads each admitted source or manifest only after checking the common
scanner's SHA-256 digest. Input must stay within the selected root, decode as
strict UTF-8 text without binary nulls, and fit the eight-megabyte per-file limit.

## Cargo package and workspace model

Bounded static TOML analysis records packages, virtual workspaces, literal and
simple wildcard workspace members, dependency names and kinds, renamed packages,
literal repository-local paths, inherited workspace dependencies, features,
crate types, procedural-macro declarations, build scripts, and explicit or
conventional library, binary, example, test, and benchmark targets. Deepest
manifest ownership wins for nested packages.

Literal in-scope path dependencies, workspace membership, and matching crate uses
can form local project references. Dynamic, inherited, malformed, external, or
out-of-scope values remain explicit unresolved boundaries. EffortHours does not
interpret `Cargo.lock` as a resolved graph, evaluate build metadata, resolve the
active feature set or target triple, download crates, inspect Cargo caches, invoke
Cargo, or run manifest commands.

## Source and semantic evidence

A bounded managed tokenizer records modules, uses, functions, methods, structs,
enums, traits, unions, implementations, public symbols, generics, lifetimes,
async/await, branches, unsafe blocks, error paths, documentation comments,
attributes, macro signals, entry points, and extern/FFI boundaries. The token
ceiling is 250,000 per file. Unbalanced delimiters, unterminated strings or nested
comments, invalid raw strings, and token truncation lower confidence instead of
silently inventing complete evidence.

Framework semantics require matching crate imports or dependency-plus-use context.
The initial catalog covers representative server/API, data, HTTP/messaging/cloud,
cryptography/security, CLI, async/background/concurrency, validation, testing, and
FFI/bindings surfaces. Local modules that merely reuse framework-looking names do
not qualify when the local declaration or path crate is statically visible in the
bounded file/package model. Evidence is static and bounded; it does not prove name
or type resolution, trait selection, borrow checking, control flow, runtime
registration, reflection, or reachability.

## Tests, examples, and generated boundaries

The analyzer distinguishes in-source `#[test]` cases, conventional integration
tests, examples, benchmarks, documentation-test fences, assertions, parameterized
test signals, and mocks. These are represented at the quality level statically
present; EffortHours does not compile or execute them.

Declarative and procedural macro definitions, invocations, attributes, `cfg` and
feature gates, build scripts, `include!`, bindgen-style dependencies, and generated
binding signals remain explicit uncertainty. Macro-expanded items and generated
bodies are never guessed. Conventional `target/`, `vendor/`, generated, binary,
minified, lockfile, and exact-duplicate content does not add Rust semantic effort.

## Change normalization

Change `0.15.0` uses a conservative Rust signature. Ordinary layout and
non-documentation comments can normalize to zero. Rustdoc, identifiers including
raw identifiers, operators, delimiters, strings and raw strings, character
literals, numbers, lifetimes, attributes, and compiler directives remain
meaningful. Incomplete lexical or delimiter structure fails closed so an uncertain
change remains represented. Analyzer-backed API, data, integration, security,
validation, background/concurrency, FFI, build, benchmark, and test roles route
through existing Change categories without adding a Rust-specific prior.

## Offline and privacy boundary

EffortHours does not invoke Cargo, rustc, rustdoc, rustfmt, Clippy, a linker, build
scripts, procedural macros, generators, examples, benchmarks, tests, or target
code. It does not resolve features or dependencies, install crates, access the
network, follow links outside the selected scope, write into the target repository,
or emit source values or excerpts. Source trees remain untrusted input and all
recognized evidence is derived from bounded static text and metadata.

## Qualitative and performance checkpoints

Standalone mutation suite `rust-0.1.0` contains 14 project-authored MIT-licensed
synthetic repository states and 62 passing relational assertions. It covers
formatting, exact-copy and conventional-exclusion behavior, workspace/package
ownership, semantic and category directionality, FFI/build uncertainty, tests,
and namesake rejection. This is qualitative safeguard evidence, not reviewed
labels or absolute-hour calibration.

The August 12, 2026 fresh-process synthetic million-line checkpoint used common
scanner `0.2.10` and analyzer `0.1.0`. It analyzed 1,000,004 lines across 10,001
files in 7.540 seconds, with a sampled peak working set of 139.88 MiB. Target
metadata was unchanged and no target code, dependency installation, or network
access occurred. This single-machine measurement is reproducible diagnostic
evidence, not a cross-platform performance guarantee.

## Known limitations

The tokenizer is not rustc's parser, type system, borrow checker, macro expander,
or linter, and Cargo analysis is not Cargo's resolver or build-plan engine. Macro-
generated APIs and tests, conditional compilation, target-specific dependency
selection, build-script output, generated bindings, complex workspace inheritance,
edition-sensitive parsing, trait dispatch, cross-crate type flow, and runtime
registration can be missed or remain uncertain. Native-parser parity, larger real
Cargo workspaces, reviewed Rust Change labels, independent review, and empirical
production observations remain future work.
