# Static PHP and Composer Analysis

## Status

PHP analyzer `0.1.0` and common scanner `0.2.9` provide an experimental,
offline-only PHP/Composer evidence path. Repository estimates reuse the unchanged
`seed-rules/0.4.0` language-neutral source backbone and existing specialized
priors. Change estimates use `change-seed/0.14.0+seed-rules/0.4.0`.

No PHP-specific rate was fitted. The repository and Change paths are uncalibrated,
the PHP Change extension is outside the admitted `change-seed/0.6.0` Stage A
boundary, and neither path is production-ready.

## Admitted inputs

The common scanner admits:

- maintained `.php` source, including `.blade.php` templates;
- PHP test naming and conventional test directories;
- `composer.json` as a package manifest;
- `composer.lock` as mechanical package-manager inventory only; and
- common PHPUnit, PHPStan, and Psalm configuration as build/quality inventory.

The analyzer reads each admitted source or manifest only after checking the common
scanner's SHA-256 digest. Input must stay within the selected root, decode as strict
UTF-8 text without binary nulls, and fit the eight-megabyte per-file limit.

## Composer package model

Strict, bounded JSON analysis records Composer package names and types, runtime and
development dependency names, PSR-0/PSR-4/classmap/files autoload surfaces, script
names, binary entry points, and package roles. Literal in-repository `path`
repositories and dependency/import evidence can form local package references.
Deepest manifest ownership wins for nested packages.

Dynamic, missing, external, wildcard-ambiguous, or out-of-scope paths remain
explicit unresolved boundaries. EffortHours does not interpret `composer.lock` as
a resolved dependency graph, download packages, inspect `vendor/`, run Composer,
execute scripts or plugins, or load an autoloader.

## Source and semantic evidence

A bounded managed tokenizer records PHP regions, namespaces, imports, functions,
methods, classes, interfaces, traits, enums, attributes, public symbols, branches,
exceptions, documentation comments, and selected dynamic-language boundaries. The
token ceiling is 250,000 per file. Unbalanced delimiters, unterminated strings or
comments, invalid heredoc/nowdoc structure, and token truncation lower confidence
instead of silently inventing complete evidence.

Framework semantics require matching import-qualified names or other explicit
static context. The initial catalog covers representative Laravel, Symfony,
Doctrine, Guzzle/HTTP, cloud/client, JWT/security, queue/background, validation,
CLI, PHPUnit, and Pest surfaces. Local classes that merely reuse framework-looking
names do not qualify. Evidence is static and bounded; it does not prove type
resolution, control flow, container bindings, route registration, reflection,
magic-method dispatch, variable calls, dynamic includes, or runtime reachability.

## Templates and mixed repositories

Maintained PHP and Blade templates contribute bounded UI evidence for template
regions, interpolations, directives/control flow, forms, bindings, and component
use. Raw template line volume is not priced. Generated framework template caches
are excluded.

The PHP path does not render templates, compile Blade, follow runtime view names,
or analyze linked frontend assets. Scanner-owned HTML/CSS/JavaScript analysis stays
separate, so a mixed repository can represent each maintained artifact without
charging the same body twice. Scanner-owned SQL files likewise remain under the
SQL boundary; PHP database calls contribute integration/data boundary evidence,
not duplicate SQL statement bodies.

## Exclusions and normalization

Conventional `vendor/` content, generated code, framework caches, build output,
binary/minified content, lockfile mechanics, and exact duplicate bodies do not add
PHP semantic effort. Exact-content normalization still values one maintained body
once. Meaningful customization is represented only when it survives the ordinary
scanner and generated-content policies.

Change `0.14.0` uses a conservative PHP signature. Ordinary layout and
non-documentation comments can normalize to zero. PHPDoc, identifiers, variables,
operators, delimiters, strings, numbers, heredoc/nowdoc bodies, PHP open/close tags,
and inline template content remain meaningful. Incomplete lexical or delimiter
structure fails closed so an uncertain change remains represented. Analyzer-backed
API, UI, data, integration, security, validation, background, build, and test roles
route through existing Change categories without adding a PHP-specific prior.

## Offline and privacy boundary

EffortHours does not invoke PHP, Composer, a framework bootstrap, a dependency
resolver, an autoloader, a service container, a route compiler, reflection, tests,
or target code. It does not install dependencies, access the network, follow links
outside the selected scope, write into the target repository, or emit source values
or excerpts. Source trees remain untrusted input and all recognized evidence is
derived from bounded static text and metadata.

## Qualitative and performance checkpoints

Standalone mutation suite `php-0.1.0` contains 14 project-authored MIT-licensed
synthetic repository states and 59 passing relational assertions. It covers
formatting, exact-copy and conventional-exclusion behavior, package ownership,
semantic and category directionality, templates, tests, and namesake rejection.
This is qualitative safeguard evidence, not reviewed labels or absolute-hour
calibration.

The August 12, 2026 fresh-process synthetic million-line checkpoint used common
scanner `0.2.9` and analyzer `0.1.0`. It analyzed 1,000,001 lines across 10,001
files in 7.920 seconds, with a sampled peak working set of 441.57 MiB. Target
metadata was unchanged and no target code, dependency installation, or network
access occurred. This single-machine measurement is reproducible diagnostic
evidence, not a cross-platform performance guarantee.

## Known limitations

The tokenizer is not PHP's native parser or type system, and Composer analysis is
not Composer's resolver. Runtime-created symbols, dynamic configuration, generated
container/routes/proxies, annotations interpreted by plugins, complex autoload
behavior, framework conventions without explicit static evidence, and cross-file
data flow can be missed or remain uncertain. Native-parser parity, larger real
Composer monorepositories, reviewed PHP Change labels, independent review, and
empirical production observations remain future work.
