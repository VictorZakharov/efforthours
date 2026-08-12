# Static Terraform and HCL Analysis

## Status

Terraform/HCL analyzer `0.1.0` is an experimental, offline, token-backed analyzer.
Common scanner `0.2.8` admits maintained Terraform/HCL artifacts, and repository
estimation continues to use the unchanged `seed-rules/0.4.0` model. No fitted
Terraform-specific rate, reviewed Terraform label, provider-derived prior, or
production-accuracy claim is introduced.

Change version `0.13.0` added conservative HCL-aware formatting comparison and
analyzer-backed routing. The current composite source identity is
`change-seed/0.14.0+seed-rules/0.4.0`; the additive PHP extension does not change
Terraform normalization or valuation. Terraform/HCL is not admitted by the earlier
`change-seed/0.6.0` Stage A gate, whose frozen records contain no Terraform or HCL
paths.

## Admitted artifacts

The common scanner recognizes:

- maintained `.tf`, `.tfvars`, `.auto.tfvars`, and `.tfbackend` files;
- Terraform test and mock files ending in `.tftest.hcl` or `.tfmock.hcl`;
- `.terraformrc` and `terraform.rc` CLI configuration;
- relevant `.hcl` dialects, including named Terragrunt, Packer, and Nomad files;
  and
- `.tf.json` and `.tfvars.json` as explicit inventory-only Terraform JSON.

Terraform JSON remains inventory-only in this checkpoint because the HCL analyzer
does not pretend that JSON bytes were parsed as HCL. Generic or named non-Terraform
HCL receives bounded structural visibility and an explicit
`terraform-semantics:not-assumed` tag; it does not receive guessed Terraform
infrastructure units.

## Static evidence

The analyzer performs digest-verified UTF-8 admission followed by bounded,
comment/string/heredoc-aware tokenization and conservative HCL body analysis. For
Terraform dialects it records:

- resources and distinct resource types;
- data sources and distinct data-source types;
- module calls and literal local, registry, Git, HTTP, object-storage, other
  external, missing, or dynamic source classes;
- variables, outputs, input assignments, locals, descriptions, and sensitive
  interfaces;
- providers, required-provider families, Terraform blocks, and backend types;
- lifecycle, dynamic, provisioner/connection, and explicit dependency structure;
- bounded traversal, function, conditional, `for`, and template-expression units;
- validation, precondition, postcondition, check, test-run, and assertion
  structure; and
- credential-shaped names and policy/security-shaped configuration without
  retaining or emitting configured values.

Facts separate infrastructure implementation, external boundaries,
security-sensitive configuration, validation, Terraform tests, interface
documentation, CLI/build configuration, and Terraform version/backend delivery
configuration. Parser confidence and unresolved/dynamic boundaries remain visible
diagnostics rather than guessed runtime semantics.

## Module ownership and external boundaries

Configuration ownership is directory-local. Test, variable-value, and CLI files
attach to the nearest discovered configuration directory. A literal `./` or `../`
module source is normalized only within the analyzed repository and resolves only
to a discovered local module directory. Escaping, missing, and unresolved local
sources remain explicit.

Registry, Git, HTTP, object-storage, other external, missing, and expression-
derived sources are classified as boundaries only. EffortHours does not fetch a
module or expose its literal source address in evidence. Provider and backend
families are bounded normalized identifiers; provider schemas and backend state
are not loaded.

## Effort mapping and normalization

Raw Terraform physical lines no longer drive the generic infrastructure rule.
Each module emits bounded semantic infrastructure units based on distinct types,
interfaces, module boundaries, lifecycle/dependency structure, and expression
structure. Repeated conventional blocks of an already represented type enter
diminishing bands. Byte-identical maintained bodies are traceable but contribute
semantic units once.

Those units map transparently to the existing `ci-infrastructure` prior in
`seed-rules/0.4.0`. Existing integration, security, validation, test,
documentation, build, and packaging/delivery priors consume their corresponding
facts. This reuses public language-neutral rules; it is not empirical Terraform
calibration and does not claim that provider families have equal real-world cost.

## Exclusions and safety

Default scanning excludes `.terraform/` caches and links. State, backup-state,
plan, `.terraform.lock.hcl`, generated, vendored, minified, binary, oversized,
invalid-text, changed-after-scan, and exact-duplicate bodies do not add Terraform
semantic effort. Lock and state mechanics remain inventory/exclusion evidence
where visible and do not become build or infrastructure drivers.

The analyzer never:

- starts Terraform, OpenTofu, Terragrunt, Packer, Nomad, a provider, or a module;
- runs `init`, `validate`, `plan`, `apply`, tests, formatters, or policy engines;
- installs providers or modules, resolves a registry, or accesses a network;
- contacts a backend, reads state semantics, or evaluates a saved plan;
- loads provider schemas, evaluates interpolation, expands dynamic blocks, or
  computes `count`/`for_each` instances;
- proves type, reference, policy, security, graph, plan, apply, drift, or runtime
  correctness; or
- emits source excerpts, literal module addresses, credentials, configured values,
  or absolute target paths.

Input is bounded to eight MiB per admitted file, 250,000 tokens per file, 128 HCL
nesting levels, bounded captured identifier classes, and 50 evidence locations per
fact. Safeguard hits lower confidence and keep recognized evidence visibly
incomplete.

## Change behavior

HCL-aware Change comparison supports `.tf`, `.tfvars`, `.tfbackend`, `.hcl`,
`.terraformrc`, and `terraform.rc`. It ignores horizontal formatting and blank-line
count while preserving semantic newlines, comments, identifiers, operators,
literals, delimiters, templates, and heredoc bodies. Unterminated strings,
comments, heredocs, or unbalanced delimiters fail closed, so uncertain differences
remain represented.

Formatting-only bodies, exact moves/copies, state, plans, caches, locks, generated
content, and other ordinary Change exclusions remain zero. Analyzer-backed paths
route infrastructure, integration, security, tests, documentation, build, and
delivery capabilities to their native categories. Formatting normalization never
changes repository evidence or the final-delta selection boundary.

## Qualitative and scale checkpoint

Standalone suite `terraform-0.1.0` freezes 14 project-authored synthetic repository
states and 48 passing relations under `seed-rules/0.4.0`. It covers formatting,
duplicates, excluded mechanics, distinct and repeated resources, data sources,
local/external modules, tests, security, validation, documentation, delivery, and
generic-HCL conservatism. This is a relational implementation guardrail, not a
reviewed accuracy dataset.

On the documented Windows workstation, the fresh-process one-million-line
Terraform shape (10,000 files by 100 lines) completed static analysis in 8.239
seconds with a 303.72 MiB sampled peak working set. Target metadata remained
unchanged; target execution, dependency installation, and network access were not
performed. A single-machine checkpoint is not a cross-platform regression limit.

## Known limitations

- The HCL pass is a bounded structural parser, not HashiCorp's native HCL parser.
- Terraform JSON is inventory-only.
- Generic HCL dialect semantics are not inferred from matching block names.
- Module ownership is directory-based; remote subdirectories and registry version
  semantics are not resolved.
- Repetition bands do not prove that two resource instances have equivalent
  customization or operational risk.
- Provider aliases, inherited configurations, moved/import blocks, policy
  languages, generated configuration, and cross-workspace/environment intent can
  be only partially visible.
- Plans, provider schemas, interpolation results, policy validation, drift, and
  runtime correctness are not evaluated.
