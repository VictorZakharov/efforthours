# Static Jupyter notebook analysis boundary

## Status

Safe Jupyter notebook analysis begins with:

- common scanner `0.2.12`;
- Python analyzer `0.2.0`;
- unchanged repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.17.0+seed-rules/0.4.0`.

Later additive C/C++ support advances the current composite source identity to
`change-seed/0.18.2+seed-rules/0.4.0` without changing Jupyter normalization or
valuation behavior.

The boundary is experimental and uncalibrated. It represents maintained Python
code-cell and Markdown effort through existing transparent priors. It does not
claim scientific validity, output correctness, reproducibility, numerical
calibration, or production admission. The admitted Change Stage A boundary remains
`change-seed/0.6.0`; no Jupyter record was present in that gate.

## Admission and ownership

The common scanner classifies maintained `.ipynb` files as Jupyter language in the
Python ecosystem. The deepest discovered Python package owns a notebook; a root
fallback owns notebooks outside declared packages. Conventional generated paths
and markers, vendored or binary inputs, and exact excluded bodies retain their
normal common-scanner treatment. `.ipynb_checkpoints` directories are excluded by
default before notebook parsing.

Every admitted notebook is reread only through the existing repository filesystem
boundary after its root containment, scanner SHA-256 digest, declared byte count,
strict UTF-8 encoding, and eight-MiB file limit are checked. JSON parsing disallows
comments and trailing commas, limits nesting to 64 levels, and caps traversal at
10,000 cells. One cell source is limited to one MiB. A failed digest, invalid JSON,
unsupported encoding, or reached safeguard produces a diagnostic rather than a
guessed semantic body.

EffortHours does not launch Jupyter, start a kernel, import Python, resolve an
environment, install a package, execute a cell, or run notebook hooks.

## Language and cell boundary

Declared `language_info` and `kernelspec` metadata are reduced to conservative
language families: Python, R, Julia, JavaScript, SQL, mixed, or unknown. Conflicting
declarations remain mixed uncertainty. Ordinary code cells are analyzed only when
the notebook is unambiguously Python. An explicit `%%python`, `%%python3`, or
`%%ipython` cell can admit that cell in a non-Python notebook; other cell magics
remain excluded.

Admitted Python source passes through the same bounded managed tokenizer and
indentation-aware structure analyzer as `.py` source. Cell order is retained in
the maintained projection. Exact duplicate Python cells are valued once. Raw
cells, unsupported language cells, shell escapes, line magics, help syntax, and
unsupported cell magics are counted as uncertainty but contribute no source body
or guessed effort. EffortHours does not attempt to model the stateful kernel
environment between cells.

Markdown cells are projected separately from code. Nonblank lines, headings, and
link structure contribute bounded documentation evidence; attachment bodies and
link targets are not emitted. Exact duplicate Markdown cells are valued once.
Cell tags are retained only as digest input because they can be maintained
execution semantics; other transient cell metadata is ignored.

Across notebooks in one package, an exact maintained projection is canonicalized
to one value-bearing notebook. Inventory facts remain traceable for every admitted
file, including duplicate, output-bearing, mixed, or incomplete notebooks.

## Excluded execution and payload state

The following never contribute EHE:

- cell outputs, display data, stream text, error payloads, and MIME bundles;
- execution counts and ordering history;
- widget state;
- attachments and embedded/base64 payloads;
- transient UI state such as trusted or collapsed flags;
- raw and unsupported-language cell bodies; and
- checkpoint and generated notebook bodies.

Reports may record bounded counts such as output-bearing cells, execution-count
cells, attachments, unsupported cells, magics, and shell escapes so the exclusion
is auditable. They do not emit output values, source excerpts, embedded payloads,
or notebook metadata values. Paths and recognized technology/package names remain
repository metadata and should be handled according to the caller's privacy
requirements.

## Represented evidence

The maintained projection can produce:

- token-backed Python functions, methods, classes, public symbols, async units,
  branches, imports, and local package edges;
- conventional Python API, CLI, persistence, integration, security, validation,
  background-work, and test facts when the existing import-qualified rules match;
- bounded data-analysis facts for import-qualified NumPy, pandas, Polars, SciPy,
  scikit-learn, TensorFlow, and PyTorch calls;
- bounded visualization/UI facts for import-qualified Matplotlib, Seaborn, Plotly,
  Bokeh, and Altair calls; and
- maintained Markdown documentation facts.

Imports alone do not create data, visualization, or framework work. A matching
qualified call is required. Local namesakes remain ordinary Python structure.
These signals reuse existing analogous category priors with the wider uncertainty
of `seed-rules/0.4.0`; no notebook-specific rate was fitted.

## Repository and Change EHE

Repository estimates normalize notebook source structure from the maintained cell
projection rather than physical JSON lines. This prevents pretty-printing,
outputs, execution state, and exact duplicate cells/notebooks from multiplying
source effort. Code, narrative, tests, data analysis, integrations, and
visualizations remain separate evidence families and route only when statically
unambiguous.

Change `0.17.0` adds a bounded notebook signature. JSON field order, indentation,
source string-versus-array representation, outputs, execution counts, widget
state, attachments, transient metadata, raw cells, unsupported-language cells,
magics, and shell escapes can normalize to zero. Python token changes, Markdown
content, declared language, maintained cell tags, and meaningful cell ordering
remain significant. Markdown-only changes route to documentation; language/tag-
only changes route to configuration; analyzer-backed Python semantics retain their
native categories. Invalid, oversized, or incomplete notebook content fails
closed instead of receiving formatting exclusion.

This Change extension adds no fitted prior and does not expand the admitted
4-to-32-hour Stage A boundary.

## Public guardrails

Standalone suite `jupyter-0.1.0` contains 14 project-authored synthetic repository
states and 52 passing relations under unchanged `seed-rules/0.4.0`. It covers JSON
serialization, execution/payload exclusion, duplicate cells, duplicate notebooks,
generated paths, checkpoints, unsupported syntax, Python structure, Markdown,
data analysis, visualization, integration, tests, and category isolation. Earlier
aggregate and standalone reports remain frozen.

These are qualitative invariance, directionality, and isolation guardrails. They
are not reviewed labels, held-out accuracy evidence, interval calibration,
scientific validation, or production admission.

## Performance checkpoint

On the documented August 12, 2026 workstation, a fresh process analyzed a generated
10,000-notebook shape with 1,000,000 requested and 1,120,003 physical JSON lines in
7.561 seconds. The sampled process peak was 159.33 MiB and cumulative managed
allocation was 1,347.94 MiB. The 10,001 included files occupied 30,018,949 bytes,
produced 40,007 facts and 62.71 MiB of evidence JSON, and retained identical target
metadata. No target execution, dependency installation, or network access occurred.

This is a reproducible many-small-notebooks checkpoint on one workstation, not a
cross-platform threshold or a realistic scientific-workload distribution.

## Explicit non-goals

EffortHours does not verify execution state, data provenance, environment or
dependency reproducibility, output correctness, plot meaning, statistical or
scientific validity, model quality, security, privacy of the source repository,
kernel compatibility, cross-cell runtime state, or whether the notebook runs from
top to bottom. It does not load external datasets, expand embedded data, inspect
output payloads, execute magics or shell commands, resolve dynamic imports, or
claim that a static visualization call represents a usable result.
