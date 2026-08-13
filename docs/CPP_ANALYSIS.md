# Static C and C++ analysis boundary

## Status

C/C++ analyzer `0.1.0` provides bounded static C and C++ repository evidence plus
C/C++ final-delta Change EHE. It remains
experimental, token-backed, uncalibrated, and outside the admitted Change Stage A
families until separate public Change evidence passes that policy.

The implementation identities are:

- common scanner `0.2.13`;
- C/C++ analyzer `0.1.0`;
- unchanged repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.18.0+seed-rules/0.4.0`.

Frozen reports retain their original identities.

## Decision summary

The first C/C++ analyzer uses an independently authored, bounded managed lexer
and conservative declaration/structure parser. It does not embed libclang,
Tree-sitter, a compiler, a preprocessor, or a native parser runtime.

This is the smallest trustworthy boundary for the current offline global tool:

- the same managed implementation runs on Windows, Linux, and macOS;
- repository input remains behind the existing path, link, byte, encoding, and
  digest boundary;
- no compiler command line, system include tree, SDK, or host-native library can
  silently expand the analyzed scope;
- conditional compilation and macro-generated structure remain visible
  uncertainty rather than false compiler certainty; and
- no new third-party runtime or parser license enters the package.

The parser is reported as `token-backed`, not compiler-backed or
parser-complete. A later native-parser adapter is permissible only if reviewed
evidence demonstrates material error and the adapter preserves this document's
scope, privacy, determinism, packaging, and non-execution rules.

## Language and file boundary

The initial scanner boundary is:

| Role | Admitted forms |
| --- | --- |
| C source | `.c` |
| C++ source | `.cc`, `.cpp`, `.cxx` |
| C++ module interface | `.cppm`, `.ixx` |
| C header | `.h` when ownership is unambiguously C |
| C++ header or maintained template body | `.hh`, `.hpp`, `.hxx`, `.inl`, `.ipp`, `.tpp` |
| Ambiguous shared header | `.h` with mixed or unresolved C/C++ ownership |

An ambiguous `.h` file is analyzed once with the common C/C++ token boundary and
tagged as unresolved C/C++ header ownership. It is never parsed independently as
both languages or valued twice. Build targets and literal local include edges may
establish ownership; filename alone does not turn `.h` into a C++ header.

Objective-C/Objective-C++, CUDA, OpenCL, assembly, resource scripts, SWIG input,
and arbitrary `.inc` fragments remain inventory-only in `0.1.0`. Representative
forms include `.m`, `.mm`, `.cu`, `.cuh`, `.cl`, `.s`, `.S`, `.asm`, `.rc`, `.i`,
and `.ii`. Preprocessed `.i`/`.ii` bodies are conventional generated artifacts and
must not be valued as maintained source. A later extension needs its own explicit
language and toolchain boundary.

## Standards boundary

The managed tokenizer and declaration parser target the structural syntax shared
across:

- ISO C99, C11, C17, and C23; and
- ISO C++11, C++14, C++17, C++20, and C++23.

This means the analyzer recognizes bounded declarations and structural markers
from those editions; it does **not** claim complete grammar conformance for any
edition. Earlier C and C++ commonly tokenize through the same subset but receive
no separate completeness claim. C2Y, C++26 and later syntax remains token-visible
with an unsupported-standard uncertainty tag until deliberately admitted.

Common GNU, Clang, and MSVC attributes, calling conventions, declaration
extensions, and inline-assembly boundaries may be recognized as extension tokens.
They do not establish compiler acceptance, target selection, or portable
semantics. A literal build declaration such as `-std=c17`, `-std=c++20`, or
`/std:c++latest` may supply a declared-standard tag; EffortHours never chooses a
standard or treats that declaration as proof that the checkout builds.

## Safe text and token admission

Every source and build file read by the analyzer must:

- already be admitted by the common scanner as a regular file inside the selected
  root;
- match the scanner-recorded byte length and SHA-256 digest at read time;
- decode as strict UTF-8 without binary nulls;
- be no larger than eight mebibytes; and
- remain cancellable throughout tokenization and analysis.

Each source file is capped at 250,000 tokens. Preprocessor nesting is capped at
128 groups, and all build readers receive explicit collection, nesting, and
expansion limits. Unterminated literals/comments, invalid raw-string delimiters,
unbalanced structural delimiters, malformed directive nesting, truncation, or a
changed digest lowers confidence or fails closed. Unsafe input never becomes a
complete-evidence claim.

The tokenizer recognizes identifiers, keywords, numeric literals, character and
string literals, C++ raw strings, comments, documentation comments, operators,
delimiters, attributes, and logical preprocessor lines. It records counts and
stable structural signatures, not literal values or source excerpts.

## Declaration and structure boundary

The conservative parser recognizes, where structurally unambiguous:

- C functions and prototypes, structures, unions, enumerations, typedefs,
  external declarations, function pointers, generic selections, atomics, branch
  points, and explicit error paths;
- C++ namespaces, classes, structures, unions, enumerations, functions, methods,
  constructors, destructors, conversion/operators, access sections, templates,
  concepts, constraints, lambdas, modules/imports/exports, coroutines, exceptions,
  branches, and synchronization/concurrency markers;
- public and exported declarations using header/module ownership, linkage,
  visibility attributes, and C++ access sections as bounded evidence;
- entry points, libraries, executables, plugins, and test/benchmark targets when
  source and build evidence agree; and
- documentation comments, unsafe/native boundaries, platform branches, and
  unresolved dynamic or generated structure as separate evidence.

Counts are cognitive construction signals for the language-neutral source
backbone. They are not lines-of-code pricing, an AST, a type system, a control-flow
graph, a call graph, or proof that a declaration is reachable or linkable.

Each maintained `.c` or C++ source/module-interface file is a translation-unit
**candidate** under its owning literal build target. EffortHours does not construct
the compiler's translation unit because it does not select preprocessing state,
include search paths, language mode, or target flags. Headers remain separately
maintained bodies connected by bounded edges rather than textually expanded into
every candidate.

Templates are represented from their maintained declaration and body once.
EffortHours does not instantiate them or multiply effort by call sites or possible
specializations. Macro invocations can contribute a bounded macro-use or framework
signal only when qualified; they never manufacture expanded functions, types, or
tests.

## Preprocessor boundary

The lexer recognizes logical preprocessing directives after bounded line-splice
handling. It records:

- quoted and angle-bracket includes;
- object-like and function-like macro definitions, `#undef`, stringification,
  token-pasting, and variadic-macro signals;
- `#if`, `#ifdef`, `#ifndef`, `#elif`, `#elifdef`, `#elifndef`, `#else`, and
  `#endif` groups and nesting;
- `#pragma once`, conventional include guards, and other pragma inventory;
- `#error`, `#warning`, `#line`, feature-test expressions, and embed/include
  uncertainty; and
- compiler-specific directive forms as explicit extension evidence.

EffortHours never expands a macro, evaluates a condition, chooses an active branch,
expands an include, executes `_Pragma`, reads an embedded resource, or infers
generated declarations.

To avoid pricing mutually exclusive implementation volume as simultaneous code,
conditional source is normalized by group:

1. identical normalized declarations across sibling branches count once;
2. distinct externally visible declarations remain represented because the
   artifact may expose different platform/configuration surfaces;
3. residual internal structure uses the component-wise maximum across sibling
   alternatives rather than their sum; and
4. conditional depth and distinct build/platform variants add capped uncertainty
   and build-configuration evidence, never a raw branch multiplier.

Nested groups apply the same rule recursively. If a group is malformed or exceeds
its limit, analysis fails closed for the affected structural projection and emits
an explicit diagnostic.

## Includes, headers, and source ownership

Headers are maintained artifacts, not work repeated for every translation unit.

- A header body contributes source structure once after exact-content
  normalization.
- Include fan-out creates reference and integration context only; it never
  multiplies the included body.
- Header-only libraries and template implementations remain valued once as
  maintained library source.
- A source/header body listed in several build systems or targets remains one
  implementation body, while genuinely distinct maintained target configuration
  may still contribute bounded build work.
- Generated unity/amalgamation drivers do not duplicate their included source.
  A genuinely maintained amalgamated implementation remains explicit and is
  normalized against exact included bodies when ownership is provable.

Only literal repository-local includes are candidates for local edges. Quoted
includes are resolved against the containing directory and literal in-repository
include roots from the owning target. Angle-bracket includes may resolve locally
only when exactly one admitted target/include-root candidate exists. Ambiguous,
dynamic, missing, external, or outside-root paths remain unresolved and are never
followed.

## Static build and package discovery

The implementation inventories coexisting build systems instead of guessing
that one is authoritative. Every reader is static and deliberately narrower than
the corresponding build language.

### CMake

Scanner-admitted `CMakeLists.txt`, `.cmake`, and JSON preset files receive bounded
token/structure analysis. The first subset recognizes literal project/language,
target, source/header-set, include-root, compile-definition, link, package,
subdirectory, test, install, and custom-generation boundaries from common commands
such as `project`, `add_executable`, `add_library`, `target_sources`,
`target_include_directories`, `target_compile_definitions`,
`target_link_libraries`, `find_package`, `add_subdirectory`, `add_test`, and
`install`.

Only simple literal lists and bounded one-hop substitutions from literal `set`
values are resolved. Conditions, functions/macros, loops, generator expressions,
toolchain files, presets, environment values, cache state, FetchContent behavior,
and custom commands remain unresolved. CMake is never invoked.

### Make

`Makefile`, `makefile`, `GNUmakefile`, and maintained `.mk` files receive a
comment/continuation-aware static projection of literal targets, prerequisites,
simple `=`/`:=` assignments, include declarations, compiler/linker configuration
presence, test targets, install/package targets, and recipe presence.

Make functions, wildcards, secondary expansion, implicit rules, conditionals,
recursive make, environment state, included external files, and recipes are not
evaluated. Recipe bodies and variable values are not emitted. Shell functions and
commands are never run.

### Meson

`meson.build`, `meson.options`, and `meson_options.txt` receive a bounded literal
projection of `project`, `files`, `subdir`, executable/library target calls,
include directories, dependencies, tests, benchmarks, install declarations, and
custom-generation boundaries. Literal arrays and direct variable references may
be resolved within one file. Conditions, loops, methods, subprojects, fallback
selection, custom targets, generators, compiler objects, and program execution
remain unresolved. Meson is never invoked.

### Visual C++/MSBuild

`.vcxproj` and repository-local `.props` files receive safe XML parsing with DTD
and external resolution disabled. Literal project references, `ClCompile`,
`ClInclude`, configuration type, language-standard declarations, include roots,
preprocessor-definition presence, and test/build configuration are admitted.
Imports, properties, conditions, item transforms, wildcards, targets, toolsets,
and inherited/effective values are not evaluated. `.filters` is presentation-only;
`.targets` and arbitrary MSBuild projects remain common configuration evidence.
MSBuild and Visual Studio are never invoked.

### Supplementary metadata

Checked-in `compile_commands.json` is non-valued supplementary evidence. A bounded
strict JSON reader may map only an unambiguous `file` entry back to a scanner-
admitted path and recognize an exact language-standard option from an `arguments`
array. `directory`, `command`, arbitrary arguments, absolute/outside-root paths,
and values are ignored and never serialized. The file does not override maintained
build ownership and never causes source outside the selected root to be read.

Static `vcpkg.json` dependency names and literal `conanfile.txt` requirements may
support package boundaries. Executable `conanfile.py`, package-manager state,
lockfiles, registries, recipes, profiles, triplets, and resolved graphs are not
evaluated. No package manager is invoked and no dependency volume becomes effort.

## Project graph and ambiguity

Literal build targets create executable, library, plugin, test, benchmark, and
utility scopes. Explicit source membership wins; otherwise the deepest
unambiguous containing project owns a source file. Literal local target links and
project references form graph edges. If multiple coexisting build descriptions
claim a source incompatibly, the source remains valued once and ownership is
reported ambiguous rather than arbitrarily selected.

A repository with maintained C/C++ source and no admitted build descriptor receives
one explicit fallback C/C++ scope. This preserves source effort without inventing
a build graph.

## Qualified semantic evidence

Specialized facts require compatible include/import, namespace/type/call/macro,
and, where available, build-dependency context. Local namesakes must not qualify.
The initial catalog covers representative:

- HTTP/server/API and RPC surfaces;
- CLI commands and option parsing;
- SQL/database and serialization/data boundaries;
- HTTP, messaging, cloud, device, and operating-system integrations;
- cryptography, authentication, authorization, and credential-handling surfaces;
- validation and error/result handling;
- background work, threads, atomics, synchronization, and coroutines;
- GUI/rendering component boundaries when a recognized framework is qualified;
- C ABI, foreign-function, dynamic-library, and generated-binding boundaries; and
- unit, integration, end-to-end, fuzz, and benchmark frameworks.

These are static represented surfaces. They do not prove route registration,
dependency injection, protocol compatibility, database validity, UI rendering,
thread safety, race/deadlock freedom, memory safety, security, ABI compatibility,
or runtime behavior.

## Tests and quality evidence

Conventional `test`/`tests`, integration, end-to-end, fuzz, and benchmark paths are
combined with qualified framework headers and compatible test-case/assertion/macro
shapes. Initial framework recognition may include GoogleTest/GoogleMock, Catch2,
doctest, Boost.Test, CTest, Meson tests, and common fuzz harness entry points.
Macro registration remains static evidence only.

Discovered tests are assumed to pass on the fastest default path. EffortHours does
not compile or run them, discover macro-expanded cases, load test adapters,
measure coverage, execute sanitizers, or prove that a test reaches its named
behavior.

## Exclusions and generated boundaries

Common generated, vendored, minified, binary, build-output, lockfile, and exact-
duplicate precedence remains authoritative. In particular:

- object/archive/shared-library files, PDBs, precompiled headers, dependency
  files, CMake build trees, Meson build trees, Visual Studio output, and compiler
  caches add no source effort;
- conventional protobuf/gRPC, Qt MOC/UIC/RCC, SWIG, bindgen, parser-generator,
  unity-build, amalgamated third-party, and package-manager output bodies remain
  excluded when confidently classified;
- generator selection, maintained templates/configuration, explicit supported
  customization, and integration may still contribute through existing rules;
  and
- source line count, include count, macro expansion potential, template
  instantiation count, and header fan-out are never direct effort multipliers.

The existing exact EffortHours `<custom-code>` Change boundary remains the only
supported generated-customization projection. This checkpoint does not infer
generator-specific protected regions.

## Effort mapping

C/C++ source structure reuses the unchanged `seed-rules/0.4.0`
`polyglot-source-backbone`. Files, functions/methods, types, public symbols,
templates/generics, async/concurrency units, and branches feed the existing
transparent marginal construction rates with wider token-backed uncertainty.

Existing setup, architecture, entry-point, API, UI, data, integration, security,
validation, background/concurrency, FFI, testing, documentation, build, delivery,
manual-validation, and review rules consume separate qualified facts. Build
variants and preprocessor uncertainty widen confidence or create bounded build
configuration work; they do not multiply source volume.

No C/C++-specific numerical prior, calibration label, or model artifact is added.
Existing ecosystem reports and every frozen estimate must retain identical output.
The C/C++ repository path remains experimental and uncalibrated after
implementation.

## Change EHE boundary

The implemented `change-seed/0.18.0` extension adds conservative C/C++ formatting
normalization without changing any existing prior:

- ordinary layout and non-documentation comments may normalize to zero;
- documentation comments, preprocessor directives and replacement tokens,
  identifiers, operators, delimiters, literals, raw strings, attributes,
  declaration structure, and meaningful ordering remain significant;
- malformed directives, line-splice ambiguity, invalid literals, and unbalanced
  structure fail closed; and
- analyzer-backed production, API, UI, data, integration, security, validation,
  concurrency, FFI, test, build, and delivery roles route through existing Change
  categories.

Because EffortHours does not select an active preprocessor configuration, a
meaningful edit inside any maintained conditional branch remains represented.
Formatting-only build-file changes may normalize only where an artifact-specific
static signature is safe; otherwise uncertain build changes remain represented.
Header fan-out and translation-unit count never multiply final-delta EHE.

This extension does not expand the admitted `change-seed/0.6.0` 4-to-32-
hour Stage A band. C/C++ requires separate decomposed public Change evidence before
any admission claim.

## Privacy and offline boundary

EffortHours does not invoke a compiler, preprocessor, linker, debugger, build
system, package manager, generator, test runner, sanitizer, profiler, target
binary, shell, or native parser library. It does not read compiler/system include
trees, SDKs, package caches, environment variables, build caches, generated build
databases outside the selected root, or links outside scope. It does not install
dependencies, access the network, or write into the target repository.

Ordinary evidence and estimates may contain canonical repository paths, bounded
counts, classifications, declared target/dependency names, technology labels, and
reasoning. They must not contain source excerpts, literal/macro values, compiler
commands, recipe bodies, absolute host paths, credentials, embedded data, or
generated output.

## Dependency and license decision

No parser, grammar, native binary, or new package is adopted by this checkpoint.
The managed implementation is project-authored under EffortHours's MIT
License and must not copy a third-party grammar or compiler source.

The following alternatives were reviewed on August 12, 2026:

| Candidate | License/source | Decision |
| --- | --- | --- |
| Tree-sitter runtime and C/C++ grammars | MIT; <https://github.com/tree-sitter/tree-sitter>, <https://github.com/tree-sitter/tree-sitter-c>, <https://github.com/tree-sitter/tree-sitter-cpp> | Deferred. The native runtime/grammar packaging adds a cross-platform binary and binding boundary without solving preprocessing, build selection, or include-scope policy. |
| Clang/libclang | Apache-2.0 WITH LLVM-exception; <https://github.com/llvm/llvm-project/blob/main/LICENSE.TXT> and <https://clang.llvm.org/docs/LibClang.html> | Deferred. Translation-unit parsing depends on compiler arguments, headers, preprocessing state, and a native runtime, which conflicts with the first bounded repository-only scope. |
| ClangSharp `21.1.8.4` | MIT package/bindings; <https://www.nuget.org/packages/ClangSharp/21.1.8.4> | Deferred with libclang. Managed bindings do not remove the native/runtime, translation-unit, or external-header boundary. |

Tree-sitter and Clang remain technically viable future adapters. Adoption requires
a separate decision that pins exact package and grammar versions or commits,
records all transitive and native licenses in `THIRD-PARTY-NOTICES.md`, verifies
Windows/Linux/macOS package contents, and proves deterministic repository-bounded
behavior before implementation.

Language and build shapes were checked against these primary references:

- GCC's [C/C++ standards and dialect documentation](https://gcc.gnu.org/onlinedocs/gcc/Standards.html)
  and Clang's [C](https://clang.llvm.org/c_status.html) and
  [C++](https://clang.llvm.org/cxx_status.html) support records;
- the official [CMake command documentation](https://cmake.org/cmake/help/latest/manual/cmake-commands.7.html);
- the [GNU make manual](https://www.gnu.org/software/make/manual/make.html);
- the [Meson reference manual](https://mesonbuild.com/Reference-manual.html); and
- Microsoft's [MSBuild reference for C++ projects](https://learn.microsoft.com/en-us/cpp/build/reference/msbuild-reference-cpp?view=msvc-170).

These sources inform the independently authored bounded projection; their text or
grammars are not redistributed.

## Verification coverage

The implemented boundary is covered by:

- memory-only scanner, tokenizer, preprocessor, build-reader, ownership,
  semantics, estimator, privacy, and negative-namesake tests;
- process-level CLI tests for repository and directory-pair Change EHE, stdout/
  stderr separation, determinism, and unchanged target trees;
- mixed C/C++ target tests plus mixed C/C++ with existing supported ecosystems,
  proving one maintained body and one owning scope are not duplicated;
- C and C++ Change tests covering formatting/comments, documentation,
  directives, literals, headers, conditional branches, build files, generated
  output, exact movement/copying, and category isolation;
- a project-authored MIT synthetic mutation slice covering at least formatting,
  exact duplication, generated/vendor exclusion, header fan-out, C structure,
  C++ templates/concepts, conditional variants, local namesakes, build ownership,
  tests, concurrency, FFI, and specialized category directionality;
- separate fresh-process million-line C and C++ benchmark shapes with sampled peak
  memory, allocation, serialized size, analyzer versions, and before/after target
  fingerprints; and
- full format, build, unit, end-to-end, pack, schema, file-budget, and frozen-
  baseline regression gates.

Ordinary unit tests remain storage-independent. Disk-backed source trees and
subprocesses remain confined to the end-to-end and explicitly invoked benchmark
suites.

## Responsibility boundaries

The implementation preserves these focused responsibilities:

1. common file/ecosystem classification and scanner metadata;
2. digest-checked text admission;
3. tokens, documentation, and preprocessing groups;
4. C/C++ declaration and semantic measurement;
5. CMake, Make, Meson, MSBuild, and supplementary metadata readers;
6. target/source/header ownership and local-reference resolution;
7. evidence construction and repository orchestration;
8. estimator routing and exact-body/conditional normalization;
9. C/C++ Change signatures and category routing; and
10. memory-only tests, CLI coverage, mutation fixtures, benchmarks, and public
    implementation documentation.

Every new C# source file uses the ordinary 500-line ceiling and should be split
near 400 lines. CLI files remain below 400 lines. No analyzer boundary justifies a
new file-budget ratchet by itself.

## Explicit non-goals

The first C/C++ boundary does not:

- preprocess or compile source, select defines/includes/target triples, or prove
  language-standard conformance;
- expand macros/includes/modules, instantiate templates, evaluate `constexpr`, or
  run code generation;
- resolve types, overloads, concepts, modules, linkage, symbols, call graphs,
  control flow, reachability, undefined behavior, memory ownership, or lifetimes;
- link binaries, inspect object/debug information, establish ABI compatibility,
  or prove platform portability;
- evaluate CMake, Make, Meson, MSBuild, package recipes, toolchains, presets,
  environments, or compiler commands;
- execute tests, fuzzers, benchmarks, sanitizers, static analyzers, or target code;
- prove correctness, memory/thread safety, security, test quality, runtime
  behavior, numerical calibration, or production readiness; or
- claim semantic support for Objective-C, CUDA, OpenCL, assembly, or later
  language editions merely because their files are inventoried.
