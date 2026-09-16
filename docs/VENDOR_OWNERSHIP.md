# Reviewed third-party ownership

`reviewed-vendor-manifest/1.0.0` is an explicit, language-neutral ownership input
for repository analysis. It identifies complete third-party bodies that a caller
has reviewed and should not value as maintained application implementation. It
does not infer ownership from a license or copyright header, search a provider,
or remove files. Owned integration, configuration, adapters, tests, documentation,
and maintained adaptations must remain in the analyzed scope.

## Commands and scope

```text
eh scan <repository> --vendor-manifest <reviewed.json> --output <evidence.json>
eh estimate <repository> --vendor-manifest <reviewed.json> --no-rate
eh scan --repo <owner/name> --revision <commit> --vendor-manifest <reviewed.json>
eh schema show reviewed-vendor-manifest
```

The option is shared by repository `scan`, `estimate`, `explain`, `review packet`,
and `review query`, for both local trees and immutable Git snapshots. The manifest
path is an explicit local input, interpreted relative to the working directory.
File paths inside it are relative to the selected repository root. A remote scan
still requires `--fetch-missing` for provider/object acquisition; the manifest
never grants network access. No manifest is discovered or applied automatically.

Saved evidence already contains its applied decisions. Estimate or explain that
file without the option. Combining saved evidence with a new manifest is rejected:
rescan the source instead. This input is not a Change/portfolio command option;
those selectors retain their existing scope and classification controls.

## Manifest contract

JSON uses `schemaVersion: "1.0.0"`,
`protocolVersion: "reviewed-vendor-manifest/1.0.0"`, and a `files` array. Each entry
requires:

| Field | Meaning |
| --- | --- |
| `path` | Exact repository-relative file path using `/`; no glob, root, drive, empty, `.` or `..` segments |
| `sha256` | SHA-256 of the exact reviewed file bytes, 64 lowercase hexadecimal characters |
| `classification` | Exactly `third-party-body` |
| `library` | Reviewed upstream identity/version, at most 256 characters |
| `provenance` | Origin or evidence supporting the ownership decision, at most 1,024 characters |
| `rationale` | Why the entire body is excluded and maintained adaptations remain accounted for, at most 2,048 characters |

All text fields are nonempty and disallow control characters. Paths are at most
1,024 characters and unique ignoring case, ensuring portable decisions. The input
is bounded to 1 MiB and 4,096 entries. Unsupported versions, malformed schemas,
unknown fields, and invalid decisions fail with nonzero output before an estimate.

For example, the structure below needs the actual file hash substituted:

```json
{
  "schemaVersion": "1.0.0",
  "protocolVersion": "reviewed-vendor-manifest/1.0.0",
  "files": [{
    "path": "common/widget.js",
    "sha256": "<64 lowercase hexadecimal characters from the reviewed file>",
    "classification": "third-party-body",
    "library": "Example Widget 1.0.0",
    "provenance": "Compared with the retained upstream distribution",
    "rationale": "Unmodified copied body; the owned adapter remains in scope"
  }]
}
```

A modified third-party file is not automatically safe to exclude. Review its
maintained adaptations separately or keep it included. A header is a review lead,
not sufficient proof of unmodified ownership.

## Verification, lineage, and privacy

Every listed file must be admitted by the normal scan and match its reviewed hash.
Missing, ignored, unreadable, linked, or changed files fail the scan, with no
partial aggregate. Existing default vendor-directory exclusions still apply; do
not list files already excluded by scope. Moving a listed file requires updating
its exact path and reviewing the manifest again. No entry broadens scope or
follows a link.

Common scanner `0.2.15` retains the verified file as `role:vendored` and
`classification:vendored` metadata, removes its maintained/test/component role,
and excludes it from semantic source analysis. An `ownership-decision` fact
records the file, hash, applied manifest digest, and `declared-assumed` provenance.
The ownership judgment remains caller-reviewed rather than independently proven.

The manifest digest covers version and every entry field, sorted by path. It is
included in ownership/file tags and in the repository `sourceDigest` calculation
as an additional analyzed-input policy component. Without a manifest, the source
digest algorithm is unchanged. Reordering entries is immaterial; changing any
review decision changes the analyzed-input identity. Saved evidence, estimates,
and host-review identities therefore retain the selected policy.

File caches store ordinary inspection results before reviewed classification.
Listed paths bypass timestamp/length cache hits so their bytes are rehashed;
immutable content-ID inspection reuse remains safe. Every scan reapplies the
current decisions. Adding, removing, or changing a manifest cannot leak a vendor
classification into an ordinary scan or reuse a stale ownership decision.

Library, provenance, and rationale text stays in the caller's manifest; ordinary
reports include only the decision/hash lineage and repository-relative paths.
The manifest's local path is not emitted. Keep private manifests and source
outside the public repository. No target code or package tooling is executed.

## Interpretation

Excluding a copied library removes its body from represented implementation. It
is not a migration-savings claim and adds no guessed replacement allowance.
Existing integration and owned implementation evidence still flows through the
unchanged `seed-rules/0.4.0` model. Corrected input can change EHE and
professionalization gaps; neither the correction nor a more detailed allocation
establishes calibration or production accuracy.
