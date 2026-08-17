# Static Docker and Compose Analysis

## Status

Docker analyzer `0.1.0` and common scanner `0.2.11` provide an experimental,
offline-only Dockerfile, Docker Compose, and `.dockerignore` evidence path.
Repository estimates reuse the unchanged `seed-rules/0.4.0` container-deployment
prior. Docker Change support was introduced in `change-seed/0.16.0`; the current
composite is `change-seed/0.18.2+seed-rules/0.4.0` without changing Docker
normalization or valuation behavior.

No Docker-specific rate was fitted. The repository and Change paths are
uncalibrated, the Docker Change extension is outside the admitted
`change-seed/0.6.0` Stage A boundary, and neither path is production-ready.

## Admitted inputs

The common scanner admits only:

- `Dockerfile`, names beginning with `Dockerfile.`, and names ending in
  `.Dockerfile`, case-insensitively;
- `.dockerignore`; and
- `.yml` or `.yaml` files whose names begin with `compose.` or
  `docker-compose.`, including the conventional `compose.yml` and
  `docker-compose.yml` names.

Arbitrary YAML is not treated as Compose. Kubernetes manifests, Helm charts,
Swarm stack validation beyond the recognized Compose structure, BuildKit Bake,
Containerfiles, and other container ecosystems are not claimed by this
checkpoint.

Every admitted file is read only after checking the common scanner's SHA-256
digest. Input must stay within the selected root, decode as strict UTF-8 text
without binary nulls, and fit the eight-megabyte per-file limit.

## Dockerfile model

A bounded managed logical-instruction pass records parser directives, stages,
external base-image counts, local stage references, build steps, `RUN`, `COPY`,
`ADD`, arguments, environment entries, work directories, users, exposed ports,
volumes, health checks, runtime commands, multi-stage copies, BuildKit secret/SSH
and cache/bind mounts, remote `ADD` boundaries, heredocs, unresolved values, and
unknown instructions.

Continuation lines respect a recognized `# escape=` directive. Quoted arguments
are handled conservatively, token and instruction counts are capped, and
unterminated quoting, continuations, or heredocs lower confidence rather than
inventing complete structure. Image names, arguments, commands, mount IDs,
environment values, and source excerpts are never emitted.

EffortHours does not resolve image tags or digests, inspect registries, expand
shell commands or variables, traverse build contexts, evaluate BuildKit features,
pull images, build layers, or prove that a Dockerfile parses or builds.

## Compose model

The Compose path uses a bounded indentation- and quote-aware YAML structural
scanner rather than a general YAML or Compose runtime. It records documents,
services, image/build definitions, commands, ports, environment entries and
files, volume mounts, networks, dependencies, health checks, profiles, secrets,
configs, deploy settings, security settings, restart policies, extensions,
includes, anchors/aliases/merges, interpolation, block scalars, dynamic values,
and unknown top-level keys.

Literal repository-contained build contexts and Dockerfile paths can form
Compose-to-Dockerfile project references when the target is already admitted by
the common scanner. Paths that are dynamic, external, missing, or outside the
selected root remain explicit unresolved boundaries. Deep YAML typing, merge
semantics, profiles, extension expansion, `include` loading, interpolation,
environment-file loading, secret/config resolution, and schema validation do not
occur.

Multi-document files, malformed flow syntax, tabs in indentation, block scalars,
anchors, aliases, custom tags, interpolation, includes, and unsupported keys lower
confidence or add bounded uncertainty. Recognized static structure remains
visible; dynamic content is not evaluated or disclosed.

## `.dockerignore` model

The analyzer inventories bounded maintained rules, negations, directory-style
rules, and obviously incomplete bracket patterns. It does not traverse a build
context, expand globs, apply Docker's pattern-matching implementation, infer which
files enter a build, or emit pattern values.

## Effort mapping and normalization

Dockerfile, Compose, and `.dockerignore` structure becomes bounded semantic
container units. Those units replace raw Docker file and line volume in the
existing `container-deployment` rule, which maps to packaging, deployment, and
release artifacts. Exact byte-identical bodies of the same artifact kind are
valued once while every path remains traceable. Generic YAML contributes no
Docker semantic units.

This checkpoint deliberately reuses the existing transparent prior. Security-
sensitive Compose keys and secret/SSH mount structure are visible evidence and
diagnostics, but they do not create a new Docker-specific security rate or imply a
security audit. Container configuration does not invent application
implementation effort.

## Change normalization

Change `0.16.0` adds conservative signatures for admitted Docker artifacts:

- Dockerfile instruction keyword case, ordinary comments, blank lines, and
  continuation layout can normalize to zero while directives, arguments,
  literals, stages, and commands remain meaningful. Heredocs fail closed.
- Compose comments, blank lines, indentation width, and mapping-colon spacing can
  normalize to zero while keys, values, sequence structure, and document markers
  remain meaningful. Tabs, malformed flow syntax, and block scalars fail closed.
- `.dockerignore` blank lines, surrounding whitespace, and ordinary comments can
  normalize to zero while ordered patterns and negations remain meaningful.

Analyzer-backed Docker facts route through the existing packaging/deployment
category. No Docker-specific Change prior is added.

## Offline and privacy boundary

EffortHours invokes no Docker CLI, Compose CLI, BuildKit daemon, container runtime,
shell, package manager, or target program. It pulls no image, starts no service,
loads no include or environment file, resolves no secret, installs no dependency,
accesses no network, follows no link outside scope, and writes nothing into the
target repository. Reports expose paths, counts, stable tags, confidence, and
lineage—not configured values or source excerpts.

## Qualitative and performance checkpoints

Standalone mutation suite `docker-0.1.0` contains 13 project-authored
MIT-licensed synthetic repository states and 38 passing relational assertions. It
covers filename qualification, formatting and exact-copy invariance,
`.dockerignore`, build/runtime semantics, Compose service topology, security and
deploy structure, local build references, dynamic YAML bounds, and category
isolation. This is qualitative safeguard evidence, not reviewed labels or
absolute-hour calibration.

The August 12, 2026 fresh-process synthetic million-line checkpoint used common
scanner `0.2.11` and analyzer `0.1.0`. It analyzed 1,000,000 lines across 10,000
alternating Dockerfile and Compose files in 8.097 seconds, with a sampled peak
working set of 203.00 MiB. Target metadata was unchanged and no target execution,
dependency installation, or network access occurred. This single-machine
measurement is reproducible diagnostic evidence, not a cross-platform performance
guarantee.

## Known limitations

The Dockerfile reader is not Docker's parser or BuildKit frontend. The Compose
reader is not a full YAML parser, Compose schema validator, interpolation engine,
or runtime planner. Shell semantics, image contents, build contexts, ARG/ENV
resolution, cache behavior, platform selection, YAML typing and merges, profiles,
includes, external resources, runtime health, networking, volumes, secrets,
permissions, and actual deploy behavior can be missed or remain uncertain.

Native-parser/schema parity, BuildKit Bake and Containerfile policy, Kubernetes
and Helm as separate boundaries, larger real container monorepositories, reviewed
Docker Change labels, independent review, and empirical production observations
remain future work.
