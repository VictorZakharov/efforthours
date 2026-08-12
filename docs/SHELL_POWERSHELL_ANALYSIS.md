# Static Shell and PowerShell Analysis

## Status

Scripting analyzer `0.1.0` is an experimental, token-backed source boundary for
maintained POSIX-family shell/Bash and PowerShell scripts. It improves script
evidence beyond generic file inventory without starting a shell, resolving a
command or module, dot-sourcing content, evaluating expansions, installing
dependencies, accessing the network, or emitting source excerpts.

The analyzer reuses the unchanged experimental `seed-rules/0.4.0` priors. Its
standalone public qualitative gate contains 13 project-authored repository states
and 46 passing relations. That gate checks logical movement and exclusions; it is
not numerical calibration, empirical accuracy evidence, or production admission.

## Admitted inputs

The common scanner recognizes:

- `.sh`, `.bash`, `.ksh`, `.bats`, and conventional Bash profile files;
- `.ps1`, `.psm1`, and `.psd1`;
- extensionless files and `.command` files whose first line selects `sh`, `ash`,
  `bash`, `dash`, `ksh`, `pwsh`, or `powershell` through a shebang.

Only scanner-admitted maintained text is analyzed. Each read remains inside the
repository root, is checked against the scanner's SHA-256 digest, accepts strict
UTF-8 plus BOM-detected Unicode text, and is limited to eight mebibytes. Both
managed tokenizers stop at 300,000 tokens and lower confidence when a delimiter or
token limit makes the projection incomplete. Invocation context is bounded to 500
admitted package, build, CI, container, or infrastructure files; `FB8506` reports
deterministic truncation when more contexts are present.

Generated, vendored, minified, and binary bodies retain the common scanner
exclusions. Byte-identical maintained scripts remain traceable but their
construction evidence is normalized. Conventional generated completions, copied
`gradlew` and `mvnw` launchers, copied `dotnet-install.sh` and
`install-powershell.ps1` installers, and files carrying recognized generated
markers are not treated as maintained script bodies.

## Evidence

For product scripts and reusable modules, the analyzer records bounded counts for
functions, PowerShell methods and types, public symbols, parameters,
conditionals, loops, branch points, pipelines, error-handling surfaces, external
commands/cmdlets, file/network/process operations, module operations, asynchronous
units, dynamic expansions, and sourced-file references. Shell here-document and
PowerShell here-string contents are treated as opaque text rather than recursively
interpreted source.

Literal recognized network commands can create integration evidence. Credential-
shaped identifiers and secure-input commands can create security evidence without
recording values. Strict mode, traps, exits, throws, catches, and explicit error
actions can create validation evidence. Local functions named like recognized
remote commands do not create integration evidence.

Test roles produce test-case, assertion, and mock evidence. Build, CI, delivery,
and infrastructure roles produce their corresponding existing evidence kinds;
they do not also become product-source work. Shell and PowerShell share the
language-neutral polyglot source backbone and existing integration, security,
testing, build, delivery, infrastructure, and validation priors. No script-
specific fitted rate is introduced.

## Role classification

Role classification uses common-scanner path evidence first, then exact relative
script references found in admitted manifest and automation context. Invocation
conflicts resolve deterministically in this order: test, CI, infrastructure,
delivery, then build, with a diagnostic retaining the ambiguity. A `.psm1`, or a
functions/types-only file with no top-level commands, is otherwise treated as a
reusable module; the remaining maintained scripts are product command surfaces.

The classifier never resolves an executable through `PATH`, imports a module,
follows a sourced path, expands a glob, evaluates a package script, or infers an
invocation from Git history.

## Change EHE

Change source identity `change-seed/0.12.0+seed-rules/0.4.0` adds conservative
Shell and PowerShell formatting signatures and routes analyzer-backed script roles
and semantic facts. Ordinary whitespace and non-directive comments may normalize
to zero. Shebangs, PowerShell `#requires`, identifiers, operators, delimiters, and
literal contents remain meaningful. Shell here-documents and PowerShell here-
strings deliberately fail closed: a change containing one remains represented
rather than being discarded as formatting.

Exact moves, exact copies, generated output, vendored content, and copied launchers
retain the normal Change exclusions. The existing logically admitted Change Stage
A boundary remains `change-seed/0.6.0`; Shell and PowerShell were not represented
in that gate, so `0.12.0` remains experimental and unadmitted.

## Explicit limitations

This is a tolerant static tokenizer and classifier, not a POSIX conformance
checker, Bash parser, PowerShell parser, language server, shell, or platform
emulator. It does not prove quoting or expansion behavior, pipeline exit status,
command availability, permissions, filesystem effects, process behavior, network
behavior, module binding, dot-source resolution, trap semantics, platform
portability, runtime reachability, or successful execution. Dynamic invocation,
unresolved sourcing/import, low tokenizer confidence, and conflicting automation
roles remain explicit diagnostics or evidence tags.

Passing the qualitative suite prevents the covered perverse movements. It does
not establish that an absolute repository or Change hour is numerically correct.
