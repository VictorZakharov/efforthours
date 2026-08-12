# Static Python analysis boundary

## Status

Python 3 is a first-class, token-backed EffortHours ecosystem beginning with:

- common scanner `0.2.3`;
- Python analyzer `0.1.0`;
- repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.8.0+seed-rules/0.4.0`.

The later scripting extension advances the current composite source identity to
`change-seed/0.12.0+seed-rules/0.4.0` without changing Python normalization or
valuation behavior.

This boundary is experimental and uncalibrated. It adds static Python evidence and
transparent analogous source priors; it does not establish numerical accuracy or
production admission. The previously admitted Change Stage A slice remains the
non-SQL, non-Python, non-Go, non-Java, non-Kotlin, non-scripting
`change-seed/0.6.0` boundary documented in
`CHANGE_MODEL_ADMISSION.md`.

## Admitted files and package metadata

The analyzer admits maintained `.py` and `.pyi` files selected by the common
scanner. It discovers package scopes from static metadata including:

- `pyproject.toml`, including standard project, Poetry, PDM, and script sections;
- `setup.cfg`;
- literal-only values in `setup.py`;
- `requirements*.txt` and `requirements*.in`;
- `Pipfile`; and
- checked-in Poetry, PDM, and uv lock surfaces.

Metadata parsing is deliberately conservative. Dependency names, package names,
and declared scripts are read only when they are statically represented. The
analyzer does not invoke Python, import a module, evaluate a marker, discover an
environment, resolve dependency versions, install a package, or execute
`setup.py`. Lockfiles remain build/reproducibility evidence rather than a source of
dependency-volume effort.

The deepest discovered package directory owns a source file. A root fallback
scope owns maintained Python files outside nested packages. Import roots that
unambiguously match another discovered local package create a project-reference
edge; unresolved and dynamic imports do not create guessed edges.

## Token and structure analysis

Each scanner-admitted source file is reread only after its size and SHA-256 digest
are checked against common-scanner evidence. Inputs are limited to eight MiB per
file and valid supported text. The managed tokenizer is bounded to 250,000 tokens
per file and recognizes:

- identifiers, keywords, literals, operators, comments, and line continuations;
- single, double, raw/byte/f-string-prefixed, and triple-quoted strings as opaque
  string tokens;
- bracket continuations; and
- indentation and dedentation with tab expansion.

The structural pass records packages, files, imports, functions, methods, classes,
public symbols, decorators, type annotations, async units, and branch points. It
does not build a Python AST, type-check, expand f-string expressions, bind names
across arbitrary modules, evaluate descriptors or metaclasses, infer runtime
reachability, or claim compiler-grade parsing. Evidence is explicitly tagged
`syntax:token-backed`, with medium confidence for a balanced bounded pass and low
confidence plus a diagnostic when a token, string, indentation, or delimiter
safeguard is reached.

## Conservative semantic evidence

Framework evidence requires a matching import and qualified use. A local class,
function, or variable named like a framework is insufficient. The first boundary
recognizes:

- FastAPI, Flask, and Django API/routes;
- argparse, Click, and Typer command surfaces;
- SQLAlchemy, Django ORM, and Alembic persistence/migration surfaces;
- requests, httpx, boto3, Google Cloud, Azure, and OpenAI client calls as external
  integrations;
- python-jose/JWT, passlib, bcrypt, FastAPI security, and Django authentication;
- Celery and RQ background work;
- Pydantic and Marshmallow validation; and
- pytest and unittest tests, including parameterization, assertions, and common
  mock use.

Detection is not framework compilation or runtime route discovery. Imports alone
record technology context but do not create semantic work. Calls, decorators, or
base types must resolve through that context. Dynamic imports, monkey patches,
plugin registration, dependency injection performed only at runtime, and custom
framework conventions can be missed and must remain uncertainty rather than
invented units.

## Tests, exclusions, and source disclosure

Python tests are recognized from conventional `test_*.py`, `*_test.py`, and
`conftest.py` paths plus bounded test declarations. They use the language-neutral
`ecosystem-test` evidence contract and feed the existing unit, integration, and
end-to-end test priors.

Virtual environments, `site-packages`, bytecode caches, tox/nox output, pytest,
mypy, and Ruff caches are excluded before semantic analysis. Generated paths and
headers, vendored files, binary inputs, and conventional generated bodies remain
excluded. Exact maintained copies are normalized by content digest. Scanner
evidence retains excluded paths/reasons for auditability but the Python analyzer
does not read or value their bodies.

Ordinary evidence and estimate output contains paths, counts, classifications,
technology labels, and reasoning, not source excerpts or literal bodies. Paths and
declared package/dependency names are still repository metadata and should be
handled according to the caller's privacy requirements.

## Estimation and Change EHE

`seed-rules/0.4.0` preserves every `0.3.0` .NET, JavaScript/TypeScript, frontend,
SQL, and specialized marginal prior. It adds only
`polyglot-source-backbone`, whose file/function/method/type/public-symbol/async/
branch rates transparently reuse analogous `0.3.0` construction rates with wider
uncertainty. No fitted calibration or private observation was used to choose those
rates. Python semantic evidence continues through the existing entry-point, API,
data, integration, security, validation, background, test, setup, manual-validation,
and review rules.

Change EHE `0.8.0` admits `.py` and `.pyi` to an indentation-aware formatting
comparison. Horizontal formatting, consistent indentation-width changes, blank
lines, and comments outside strings can normalize to zero; indentation depth,
literals, docstrings, identifiers, operators, and other token changes remain
meaningful. Repository evidence then routes Python final deltas through the same
category reconciliation as other ecosystems. This is an experimental extension,
not an expansion of the current 4-to-32-hour admitted Change band.

Public mutation suite `0.8.0` contains 88 synthetic states and 339 passing
relations. Eleven Python states cover formatting/comments, exact duplication,
generated output, API, tests, data, integration, security, background work, and
framework namesakes. The prior 77 states keep their frozen `seed-rules/0.3.0`
reports; only Python states use `0.4.0`.

## Performance checkpoint

On the documented August 11, 2026 workstation, a fresh process analyzed a generated
10,000-file, 1,000,003-line Python tree in 13.354 seconds. The sampled process peak
was 109.63 MiB and cumulative managed allocation was 804.09 MiB. Target metadata
was unchanged. This is a many-small-files scalability checkpoint, not a frozen
cross-platform regression threshold or a realistic distribution of Python syntax.

## Explicit non-goals

This boundary does not execute Python; discover or activate virtual environments;
install, import, or inspect distributions; evaluate `setup.py`; parse notebooks;
run tests; measure runtime coverage; type-check; expand dynamic imports; execute
framework configuration; discover runtime routes; validate database models; prove
security; or establish calibration/production readiness. Jupyter notebooks remain
separate because outputs, magics, embedded data, and mixed kernels require a
different safety and effort policy.
