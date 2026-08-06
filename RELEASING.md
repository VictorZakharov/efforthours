# Releasing Fairbill

This checklist covers a quiet experimental public alpha and the `Fairbill.Tool`
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
2. Choose an unused prerelease version in `src/Fairbill.Cli/Fairbill.Cli.csproj` and
   update `CHANGELOG.md` and user-facing installation examples together.
3. Complete GitHub issue #28 against the entire reachable Git history, not only the
   working tree. Resolve any public author-email, credential, private evidence,
   proprietary material, or redistribution concern before changing visibility.
4. Recheck `LICENSE`, `THIRD-PARTY-NOTICES.md`, calibration source manifests, rate
   provenance, and every package dependency or workflow action changed since the
   prior audit.
5. Confirm ignored `artifacts/`, `.fairbill/`, `.fairbill-private/`, and
   `calibration/private/` content is not tracked.

Run the release gates:

```text
dotnet restore Fairbill.slnx --configfile NuGet.Config --force-evaluate
dotnet format Fairbill.slnx --no-restore --verify-no-changes --severity info
dotnet build Fairbill.slnx --no-restore --configuration Release
dotnet test tests/Fairbill.Tests/Fairbill.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/Fairbill.EndToEndTests/Fairbill.EndToEndTests.csproj --no-build --no-restore --configuration Release
dotnet pack src/Fairbill.Cli/Fairbill.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
```

Push the candidate normally and require the `CI` workflow to pass on Windows,
Linux, and macOS. Run `NuGet preview` manually with `publish` left false. Download
and inspect the resulting package artifact before creating a tag.

## Configure trusted NuGet publishing

Fairbill uses NuGet.org trusted publishing, which exchanges a GitHub OIDC token for
a short-lived credential. Do not create or store a long-lived NuGet API key in the
repository or GitHub secrets.

Before the first preview:

1. Create or select the NuGet.org owner account, enable two-factor authentication,
   and decide how ownership succession will work.
2. Recheck that `Fairbill.Tool` is still an available package ID.
3. Create a protected GitHub environment named `nuget.org` with required reviewer
   approval. Add environment variable `NUGET_USER` containing the NuGet.org profile
   name, not an email address.
4. In NuGet.org **Trusted Publishing**, create a GitHub Actions policy with:
   - repository owner: `VictorZakharov`;
   - repository: `fairbill`;
   - workflow file: `nuget-preview.yml`; and
   - environment: `nuget.org`.
5. If the repository is still private, create the policy close to the release. A
   private-repository bootstrap policy may remain temporarily active for only seven
   days until its first successful token exchange.

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

1. Dispatch `NuGet preview` from the exact release tag with `publish` true.
2. Review the protected `nuget.org` environment deployment and approve only the
   expected tag, version, and package digest.
3. Wait for NuGet.org validation and indexing, then install from a clean location:

```text
dotnet tool install --global Fairbill.Tool --version <prerelease-version>
fairbill version
fairbill --help
fairbill schema list
```

4. Confirm the NuGet page renders the packaged README, MIT license, repository URL,
   dependencies, and prerelease warning correctly.
5. Create a GitHub prerelease from the same tag and copy the relevant changelog
   section. An announcement is optional and separate.

NuGet package versions are immutable and cannot be deleted. If a preview is wrong,
unlist it when appropriate, fix the problem, increment the prerelease version, and
publish a new package. Never rebuild different bytes under an existing version.
