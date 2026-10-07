# Offline CLI documentation

The installed `EffortHours.Tool` carries its own documentation. Reading help,
examples or documentation does not require a source checkout, GitHub or a network.
The bundled text is compiled from the same source tree as the executable, so it
explains that installed version rather than a moving online branch.

```text
eh docs
eh docs list
eh docs show getting-started
eh docs show historical-refresh-integration
eh docs show historical-refresh-example
eh docs export ./eh-docs
```

`eh --docs` is an alias. `docs` and `docs list` print stable topic names and titles;
`docs show <topic>` prints complete Markdown to stdout. Topic names are the
lowercase documentation filenames with underscores replaced by hyphens. Special
names include `getting-started`, `documentation-index`, `release-notes`,
`third-party-notices` and `historical-refresh-example`. Unknown topics and invalid
command syntax exit 2 with diagnostics on stderr.

The bundle contains all top-level `docs/*.md` living contracts and engineering
records, the root README, changelog and third-party notices, the MIT license, and
all Markdown, JSON and C# assets in `examples/historical-refresh`. Schemas remain
available through `eh schema list` and `eh schema show <name>`. The docs include
research and contributor references; their separately referenced source trees,
scripts, datasets and benchmark results are not part of the documentation bundle.
External citations are optional references, never fetched by this command.

`docs export <new-directory>` explicitly writes the entire bundle with exact
source bytes and relative paths. Its parent directory must already exist. The
export stages a complete directory beside the destination and renames it into
place; it never overwrites an existing directory or file, including a destination
created during export. An expected I/O failure exits 3 without a successful
export message. Cancellation exits 130 and cleans up staging where possible;
unexpected internal failures retain the ordinary exit 4. There is no report or
estimation schema change.

After exporting, open `README.md` or `docs/README.md` in any Markdown reader.
Relative links between bundled documents work offline. Run the synthetic workflow
from `examples/historical-refresh` inside the export using new output paths. Its
README, complete inputs, expected receipts and tested example adapter are included.
No consumer credentials, real entry snapshots or private repository evidence are
bundled. Examples never execute merely because they are listed, read or exported.

Embedded resources have a deterministic catalog, at most 256 files, 2 MiB per file
and 16 MiB total. Reading a topic buffers only that bounded file; export copies
one file at a time and honors cancellation. There are no target-source reads,
provider requests, repository analysis or time-entry writes. The resource path
allowlist is fixed at build time and checked before export.

CLI help and recipes point to `eh docs show <topic>` for required guidance. Keep
the governing source documents current instead of maintaining shortened copies
that could disagree. Bundling documentation does not change EHE, allocation,
pricing, or the experimental and uncalibrated model boundary.
