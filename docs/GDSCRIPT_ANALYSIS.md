# Static GDScript analysis boundary

## Status

GDScript analyzer `0.1.0`, common scanner `0.2.16`, and Change estimator
`change-seed/0.19.0+seed-rules/0.4.0` admit maintained `.gd` source. This is a
bounded managed token analyzer, not Godot's parser or compiler. Repository and
GDScript Change estimates remain experimental and uncalibrated. The existing
`seed-rules/0.4.0` artifact and all numerical priors are unchanged; GDScript is
outside the limited `0.6.0` Stage A Change admission.

## Inputs and ownership

The common scanner identifies `.gd` as `gdscript` and `project.godot` as a
component/package manifest. The deepest maintained scanner-admitted
`project.godot` directory owns a script. Scripts outside a discovered project use
a root fallback scope, so standalone scripts and mixed-language repositories
work without a Godot installation. Project files are digest-checked for admission;
their values, application titles, paths, credentials, and resource bodies are not
copied into semantic output. Ownership does not require evaluating configuration.

Godot's `.godot` and legacy `.import` directories are excluded as generated
caches. Common generated, vendored, minified, binary, ignore, link, and reviewed
ownership exclusions retain precedence. Maintained addons are included; addon
paths alone do not imply third-party ownership. Exact production bodies are
collapsed within their owning scope before structure is aggregated. Test bodies
are excluded from that production projection. These decisions have explicit
`structure:production-only` and `structure:projection-normalized` lineage.

## Token evidence

Source rereads require the scanner's SHA-256 and exact byte length, valid UTF-8,
no binary nulls, repository-contained paths, and at most eight MiB per file.
Tokenization is capped at 250,000 tokens and propagates cancellation. It recognizes
indentation/dedentation, bracket continuations, escaped line continuations,
ordinary and documentation comments, identifiers, numeric literals, operators,
quoted/raw/triple-quoted strings, StringName/NodePath prefixes, and unquoted
`$Node/Child` shorthand. String contents remain opaque.

The structural pass counts the implicit script class, named methods, anonymous
functions, inner classes/enums, public named declarations, signals, `await`/legacy
`yield`, and decision/loop/boolean tokens. Lifecycle methods, annotations,
`@export*`, `@rpc`, and `@tool` are separate descriptive counts; they do not create
guessed integration, security, UI, or editor-tool capabilities. Export arguments,
node names, and literal contents are not emitted. Only the existing generic files,
functions, methods, types, public-symbols, async-units, and branch-points drivers
feed the source backbone.

Evidence reports `syntax:token-backed` and medium parser confidence for a bounded
balanced pass. Invalid indentation, mixed tabs/spaces in one indentation prefix,
mismatched delimiters, unterminated strings, unknown lexical characters, or the
token limit produce low confidence and diagnostic `FB8102`. Digest/size/text
admission failures skip semantic evidence with `FB8101`. Neither confidence level
proves grammar validity, type correctness, runtime reachability, or a working game.

## Tests and Change EHE

Conventional test directories and `test_*.gd`/`*_test.gd` filenames supply test
classification. Within those files, named `test_` functions and `assert`/`assert_*`
calls provide coarse `ecosystem-test` evidence. This accommodates conventional GUT
tests without installing or executing GUT. Other test runners, custom declaration
conventions, shadowing, parameter expansion, and assertion semantics are not
resolved. A classified test file without a recognized declaration retains one
coarse test unit; a production namesake does not become a test.

The `.gd` Change signature shares the analyzer's bounded tokenizer. Ordinary
comments, horizontal spacing, blank lines, continuation layout, final newline,
and consistent indentation-width changes can normalize to zero. Indentation
depth, documentation comments (`##`), literal bodies, node shorthand, identifiers,
multi-character operators, annotations, and statement boundaries stay meaningful.
Unsafe lexical structure fails closed and remains represented. Git changed-scope
analysis includes `.gd` representatives and ancestor `project.godot` context;
directory, saved-evidence, and virtual immutable Git inputs share the same pipeline.
Bodyless evidence cannot prove formatting equivalence and retains its existing
conservative fallback. No schema or effort coefficient changes.

Memory-only unit mutation tests cover formatting, semantic edits, test routing,
exact copies, generated/vendor/cache exclusions, nested/mixed ownership, literal
namesakes, malformed input, size/digest safeguards, cancellation, determinism,
source disclosure, and serialized schemas. A process-level smoke test exercises
scan, estimate, and directory Change with unchanged target fingerprints. The
`--gdscript` scanner benchmark records fresh-process scale separately from CI.

The October 1, 2026 fresh-process checkpoint scanned 10,000 synthetic scripts
(1,000,001 text lines including the project file) in 5.511 seconds, with 132.84
MiB sampled peak working set and 695.55 MiB managed allocation. Target metadata
was unchanged. See `BENCHMARKS.md` for the reproduction command and limitations.

## Limits and source references

This boundary does not execute Godot, GDScript, `@tool` scripts, plugins, tests,
imports, loaders, or target build commands. It does not follow `res://`, `user://`,
UID, preload, load, inheritance, autoload, or scene references; expand runtime
registration; bind cross-script names; validate RPC/network/security behavior;
render a game; or infer visual/art/audio production effort. `.tscn`, `.tres`,
`.gdshader`, binary scenes/resources, and import/UID sidecars have no dedicated
GDScript semantic valuation. Their presence must not imply complete Godot project
coverage. Diagnostic `FB8100` makes the static runtime/scene boundary explicit.

Syntax and project conventions were checked against the primary Godot
[GDScript language reference](https://docs.godotengine.org/en/stable/tutorials/scripting/gdscript/gdscript_basics.html)
and [project organization guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/project_organization.html).
No upstream parser, fixture, engine asset, or third-party dependency is copied.
The implementation and synthetic fixtures are authored under the repository MIT
license. The tokenizer adapts the repository's existing managed Python lexer;
its GDScript semantics and comparison rules remain separate.
