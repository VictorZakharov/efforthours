# Static Unreal Engine analysis boundary

## Status

Unreal analyzer `0.1.1`, common scanner `0.2.17`, and Change estimator
`change-seed/0.22.0+seed-rules/0.4.0` admit Unreal Engine projects and plugins.
This is a bounded inventory and size-inference analyzer, not an Unreal package
parser. Repository and Unreal Change estimates remain experimental and
uncalibrated. The `seed-rules/0.4.0` artifact and all numerical priors are
unchanged; Unreal is outside the limited `0.6.0` Stage A Change admission.

## Why size inference

Blueprints, materials, Niagara systems, sound cues, and levels are binary
`.uasset`/`.umap` packages. Most Unreal repositories store them in Git LFS, so a
checkout-free `git-archive/1.0.0` snapshot (and any clone without `git lfs pull`)
contains only pointer text: the spec version, the body's SHA-256 object ID, and
its byte size. EffortHours never fetches LFS objects. The evidence that is
honestly available in every input form is therefore:

- `.uproject`/`.uplugin` descriptors (JSON);
- package paths, extensions, and conventional asset-name prefixes;
- serialized size: the scanner's byte length for a present binary body, or the
  declared `size` of a valid Git LFS v1 pointer; and
- content identity: the binary SHA-256 or the pointer's object ID, which are the
  same digest of the same body.

A binary body and its LFS pointer therefore produce identical evidence and
identical estimates. Package bodies are never parsed, deserialized, loaded,
imported, cooked, or executed, and no engine or editor is required.

## Inputs and ownership

The common scanner identifies `.uproject` and `.uplugin` as Unreal package
manifests and adds the `unreal` ecosystem. The deepest descriptor directory owns
`.uasset` and `.umap` files beneath it; a project's `Plugins/<name>/` descriptor
therefore owns that plugin's content. Packages outside every descriptor directory
are counted in diagnostic `FB8303` but not valued. Descriptors are read with
digest, size (1 MiB), repository-containment, and JSON-depth bounds; only enabled
plugin and code-module counts are retained. Names, descriptions, engine
associations, and paths inside descriptors are not emitted. An unreadable
descriptor keeps ownership and reports `FB8302`.

Snapshot-portfolio area scans add every ancestor `.uproject`/`.uplugin` of an
owned package that the area does not itself own as a context-only file (scanner
tag `scope:context-only`; see `SNAPSHOT_PORTFOLIOS.md`). Packages therefore
resolve the same owner as in the whole scan. A context-only descriptor adds no
entry point, and no facts at all when the area owns none of its packages, so the
project's entry point is counted only by the area that owns its descriptor.
Analyzer `0.1.0` treated such area packages as unowned and gave Blueprint-only
areas no implementation value.

Project-relative `Binaries`, `DerivedDataCache`, `Intermediate`, and `Saved`
directories are excluded as generated. Scanner generated, vendored, and minified
classifications retain precedence. Exact duplicate bodies (same content digest)
are valued once per owning project. Text `.uasset`/`.umap` files are admitted only
as valid LFS v1 pointers of at most 1,024 bytes (version line, one `oid sha256:`,
one `size`, optional `ext-` lines, declared size at most 1 TiB). Anything else,
or a digest/size change after scanning, reports `FB8301` and is not valued.

## Asset kinds

Kinds come from the extension, `__ExternalActors__`/`__ExternalObjects__` paths,
and the conventional prefixes used by Epic samples and common Unreal style guides.
Prefixes are matched case-insensitively and require a non-empty name after the
underscore. Naming is a convention, not proof; misnamed packages are misclassified
and unprefixed packages are inventoried as unclassified without value.

| Kind | Prefixes | Treatment |
| --- | --- | --- |
| Blueprint | `BP_`, `BPI_`, `BPFL_`, `BPML_`, `BPC_`, `B_` | Graph; Blueprint classes named `*HUD*` also count as UI types |
| Widget Blueprint | `WBP_`, `WB_`, `W_`, `UI_` | Graph and UI type |
| Animation Blueprint | `ABP_`, `AnimBP_` | Graph |
| Control Rig | `CtrlRig_`, `CR_` | Graph |
| AI graph | `BT_`, `EQS_`, `ST_` | Graph |
| Material graph | `M_`, `MF_` | Graph |
| Effect graph | `NS_`, `NE_`, `NMS_`, `NM_`, `PS_` | Graph |
| Sound graph | `SCue_`, `SC_`, `MS_`, `MSS_`, `MSP_` | Graph |
| Declarative asset | `MI_`, `MIC_`, `MPC_`, `IA_`, `IMC_`, `DA_`, `PDA_`, `DT_`, `CT_`, `Curve_`, `ATT_`, `BB_`, `E_`, `ENUM_` | One file unit, no graph |
| Level | `.umap` | One level surface |
| External actor/object | `__ExternalActors__/`, `__ExternalObjects__/` | Counted only |
| Imported media | `SM_`, `SK_`, `SKM_`, `SKEL_`, `PHYS_`, `PA_`, `T_`, `TX_`, `A_`, `AS_`, `AM_`, `AO_`, `ABS_`, `BS_`, `S_`, `SW_`, `SFX_`, `AMB_`, `VO_`, `F_`, `Font_` | Counted only |
| Unclassified | anything else | Counted only, `FB8303` |

Imported meshes, textures, audio, animation sequences, fonts, and level actor
placement are not valued. They are usually purchased, generated, or produced by
art tools, and their bytes measure content volume rather than engineering effort.

## Size-inferred graph structure

For each unique graph package, inferred nodes are
`floor((bytes - base) / bytesPerNode)`, zero below the base and capped at 4,000
nodes per package so one large binary cannot dominate:

| Kind | Base | Bytes per node |
| --- | ---: | ---: |
| Blueprint, Widget Blueprint | 16 KiB | 2,560 |
| Animation Blueprint | 16 KiB | 6 KiB |
| Control Rig | 16 KiB | 16 KiB |
| AI graph | 8 KiB | 2 KiB |
| Material graph | 8 KiB | 1 KiB |
| Effect graph | 64 KiB | 8 KiB |
| Sound graph | 4 KiB | 1 KiB |

The project's `source-structure` fact then reports `types` (unique graph
packages), `functions` (0.025 inferred callable units per node), `branch-points`
(0.06 per node), and `files` (graph plus declarative packages). It also records
`inferred-graph-nodes`, `declarative-assets`, and `serialized-bytes`, which are
descriptive and not drivers. The fact is `Inferred`, tagged `syntax:size-inferred`,
`parser-confidence:low`, and `inference:uncalibrated`.

The ratios come from one development sample: a private Unreal Engine 5 Blueprint-
only prototype whose LFS bodies were reviewed outside EffortHours with a read-only
census of package export tables (graph-node exports per package; no package was
loaded or executed). Its 14 Blueprint classes held about 3,600 graph nodes at 2.3
to 3.3 KiB per node; Blueprint callable entries (events, custom events, and
function entries) were about 2.1% of nodes and branch/select nodes about 5.8%.
Material, Niagara, and sound-cue ratios were read from the same census. Animation,
Control Rig, and AI-graph ratios have little or no sample support. One sample is
not calibration: editor version, embedded data, comments, and Blueprint style all
move bytes per node.

## Estimation mapping

No seed rule or prior is added or changed. The analyzer emits existing evidence
kinds so the existing rules price them:

- `source-structure` feeds `polyglot-source-backbone` with the derived drivers.
  Because size carries no decision-density signal, size-inferred structure always
  uses neutral `Moderate` complexity instead of the code branch-density thresholds,
  adds a 0.08 confidence penalty, and states the uncalibrated inference in the
  work item's uncertainty reasons;
- `user-interface` feeds `ui-surface` with `ui-types` (widget and HUD classes) and
  `pages` (unique levels), tagged `level-placement:not-valued`;
- `entry-point` contributes one startup surface per `.uproject` (not plugins); and
- `ecosystem-package` creates the `unreal` scope (`application` for projects,
  `plugin` for plugins) with inventory counts by kind.

The `unreal` ecosystem is supported, so it no longer reports `FB1002`. Diagnostic
`FB8300` states this static boundary on every Unreal scan.

## Change EHE

Change `0.22.0` adds no Unreal-specific normalizer. Changed `.uasset`/`.umap`
paths keep their existing generic treatment. Before/after snapshot evidence now
includes the Unreal scope, so growth in size-inferred graph structure is valued
through the existing bounded source-expansion budget. An edit that leaves a
package's size nearly unchanged adds little or no expansion. Deleted or shrunk
packages are never negative. Formatting equivalence cannot be proven for binary
bodies. Existing non-Unreal Change semantics are unchanged; the identity advances
because Unreal changes can now produce different results.

## Single-case comparison

On the development sample at one fixed commit, the previous analyzer valued only
six Python helper scripts and documentation: 64.75 expected hours (30.25 to
124.25). With this analyzer the estimate is 144.75 (63.5 to 283.5), identical from
an LFS-smudged directory and from a checkout-free `--repo` archive containing only
pointers. An independent manual replacement estimate from the readable sample
(feature list, asset census, scripts, configuration, and documentation) was 148.5
expected hours (92 to 246). Agreement is partly offsetting: the estimator places
more hours on the Python helper scripts and fewer on Unreal graphs and HUD/level
work than the manual estimate. This is a sanity check of one repository, not
calibration, and the ratios were not tuned to the total.

## Limits and source references

This boundary does not parse Unreal package headers or bodies, read LFS object
stores, follow soft/hard object references, resolve Blueprint inheritance or
interfaces, evaluate C++ modules (handled, if present, by the C/C++ analyzer),
render levels, or judge gameplay quality. Marketplace or template content copied
into owned folders is valued like owned content unless a reviewed vendor manifest
or vendored path excludes it. Cooked/unversioned packages, very old engine
formats, and nonstandard naming are unsupported.

Pointer format follows the [Git LFS specification](https://github.com/git-lfs/git-lfs/blob/main/docs/spec.md).
Asset prefixes follow widely used Unreal naming conventions and Epic sample content.
No Unreal Engine source, asset, or third-party dependency is copied. The
implementation and synthetic fixtures are authored under the repository MIT
license.
