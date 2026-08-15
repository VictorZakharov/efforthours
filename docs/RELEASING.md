# Releasing EffortHours

This checklist covers a quiet experimental public alpha and the `EffortHours.Tool`
.NET global-tool preview. It does not authorize repository visibility changes,
GitHub releases, or NuGet publication; each external action requires an explicit
maintainer decision.

## Release boundary

A public alpha demonstrates the product idea and working reference implementation.
It may ship the transparent seed estimators only while README, CLI, and report
metadata continue to describe them as experimental and uncalibrated. It must not be
presented as a timesheet, invoice, empirically validated billing standard, or
production-ready estimate.

## Prepare the candidate

1. Start from a clean `main` branch synchronized with `origin/main`.
2. Choose an unused prerelease version in `src/EffortHours.Cli/EffortHours.Cli.csproj` and
   update `CHANGELOG.md` and user-facing installation examples together.
3. Complete GitHub issue #28 against the entire reachable Git history, not only the
   working tree. Resolve any public author-email, credential, private evidence,
   proprietary material, or redistribution concern before changing visibility.
4. Recheck `LICENSE`, `THIRD-PARTY-NOTICES.md`, calibration source manifests, rate
   provenance, and every package dependency or workflow action changed since the
   prior audit.
5. Confirm ignored `artifacts/`, `.efforthours/`, `.efforthours-private/`, and
   `calibration/private/` content is not tracked.

Run the release gates:

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --locked-mode
dotnet format EffortHours.slnx --no-restore --verify-no-changes --severity info
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
dotnet pack src/EffortHours.Cli/EffortHours.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
```

Push the candidate normally and require the `Formatting`, all three `Quality`, all
three `End-to-end`, `Pull request commits are linear`, and `Pack preview artifact`
checks to pass. Formatting runs once on Linux because it is platform-independent;
build/unit and process-level end-to-end validation retain separate Windows, Linux,
and macOS matrices. Each end-to-end job performs a locked restore and rebuilds its
own OS-specific project graph, so no compiled output crosses operating systems.

Package compilation runs concurrently from the exact workflow commit and uploads
a run-scoped one-day candidate. `Pack preview artifact` promotes those exact bytes
to the 14-day artifact only after every formatting, quality, and end-to-end job has
passed. A failed or cancelled validation lane therefore cannot produce a successful
package gate. Review that PR-scoped artifact before merging. Do not dispatch a
second dry-run workflow after the same required checks pass.

After the release PR merges, verify that the merge has exactly two parents and
that its tree is identical to the tested PR head (the second parent):

```text
git rev-parse HEAD^2
git diff --quiet HEAD^2 HEAD
```

When the second command exits successfully with no output and GitHub identifies
exactly one matching merged `main` PR, the merge added only commit metadata and CI
reuses the successful aggregate package gate from the second-parent PR head. If it
reports a difference, or if merge provenance cannot be verified, review the added
`main` changes and wait for the full push-to-`main` CI run on the merge commit
before tagging. The release workflow resolves the correct validation commit in
either tree case and waits for the aggregate gate there. That gate depends on
formatting, all quality and end-to-end lanes, history validation, and candidate
packaging. The workflow independently requires the merged PR match; direct,
non-merge, and manually constructed release commits are rejected.

## Configure trusted NuGet publishing

EffortHours uses NuGet.org trusted publishing, which exchanges a GitHub OIDC token for
a short-lived credential. Do not create or store a long-lived NuGet API key in the
repository or GitHub secrets.

Before the first preview:

1. Use the verified NuGet.org organization `WellScoped`, keep two-factor
   authentication enabled for its owners, and decide how ownership succession will
   work.
2. Recheck that `EffortHours.Tool` is still an available package ID.
3. Use the GitHub environment named `nuget.org`, restrict deployment to `v*` tags,
   and set environment variable `NUGET_USER` to the NuGet.org profile name
   `VictorZakharov` used by the trusted-publishing policy, not an email address or
   the separate `WellScoped` organization owner. Add the maintainer as the sole
   required reviewer after the repository is public and before enabling
   publication; leave self-review permitted so the sole maintainer can approve the
   deployment.
4. In NuGet.org **Trusted Publishing**, create a GitHub Actions policy with:
   - repository owner: `VictorZakharov`;
   - repository: `efforthours`;
   - workflow file: `nuget-preview.yml`; and
   - environment: `nuget.org`.
5. If the repository is still private, create the policy close to the release. A
   private-repository bootstrap policy may remain temporarily active for only seven
   days until its first successful token exchange.

Before every publish or retry, verify that the GitHub environment still names the
trusted-publishing policy creator:

```text
gh api repos/VictorZakharov/efforthours/environments/nuget.org/variables/NUGET_USER --jq .value
```

Require the exact output `VictorZakharov` and stop before dispatch on a mismatch.
The `WellScoped` package owner is a separate identity and must not replace this
value.

See the official [NuGet trusted-publishing documentation](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).

## Make the source public

Only after a separate explicit approval:

1. Record the exact candidate commit and final issue #28 audit result.
2. Change repository visibility to public.
3. Enable private vulnerability reporting and confirm branch/ruleset protections.
4. Verify README links, issue templates, Actions, license detection, and the public
   commit history while signed out.
5. Create and push an annotated prerelease tag matching the package version.

Do not rewrite or force-push the public release lineage afterward except for a
documented security emergency.

## Publish the NuGet preview

1. Dispatch `NuGet release` once from the exact release tag. There is no
   non-publishing input or separate dry run.
2. Let the package job verify merge provenance and the aggregate CI gate on either
   the identical PR head or, when parallel changes were combined, the exact merged
   `main` commit. It then restores/builds the CLI graph, packs, inspects,
   smoke-tests, digests, and uploads the tag-built package. It intentionally does
   not repeat formatting, unit tests, or end-to-end tests already covered by that
   successful validation commit.
3. Review the uploaded artifact and SHA-256 digest, then approve the protected
   `nuget.org` environment deployment only for the expected tag and version. The
   publish job rechecks the downloaded bytes against that approved digest before
   requesting its short-lived credential.
4. Wait for NuGet.org validation and indexing, then install from a clean location:

```text
dotnet tool install --global EffortHours.Tool --version <prerelease-version>
eh version
eh --help
eh schema list
```

5. Confirm the NuGet page renders the packaged README, MIT license, repository URL,
   dependencies, and prerelease warning correctly.
6. Create a GitHub prerelease from the same tag and copy the relevant changelog
   section. An announcement is optional and separate.

NuGet package versions are immutable and cannot be deleted. If a preview is wrong,
unlist it when appropriate, fix the problem, increment the prerelease version, and
publish a new package. Never rebuild different bytes under an existing version.
