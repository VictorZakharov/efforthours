using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateAuthorPeriodManifestReportSelection(
        ChangePortfolioAuthorPeriodManifestSelection selection,
        List<string> errors)
    {
        ValidateDigest(selection.ManifestDigest, "selection.authorPeriodManifest.manifestDigest", errors);
        if (selection.SinceInclusive >= selection.UntilExclusive)
        {
            errors.Add("Manifest author-period sinceInclusive must be earlier than untilExclusive.");
        }

        if (selection.SinceInclusive.Offset != TimeSpan.Zero ||
            selection.UntilExclusive.Offset != TimeSpan.Zero)
        {
            errors.Add("Manifest author-period report instants must be normalized to UTC.");
        }

        RequireCanonicalText(selection.TimeZone, "selection.authorPeriodManifest.timeZone", 256, errors);
        if (!Enum.IsDefined(selection.DateField) || !Enum.IsDefined(selection.MergePolicy) ||
            !Enum.IsDefined(selection.CoauthorPolicy))
        {
            errors.Add("Manifest author-period date, merge, and co-author policies must be recognized values.");
        }

        if (selection.IntervalSemantics != "since-inclusive-until-exclusive")
        {
            errors.Add("Manifest author-period interval semantics must be since-inclusive-until-exclusive.");
        }

        ValidateManifestSelectionContributors(selection.ContributorIds, errors);
        ValidateManifestSelectionRepositories(selection.Repositories, errors);
    }

    private static void ValidateManifestSelectionContributors(
        IReadOnlyList<string> contributorIds,
        List<string> errors)
    {
        if (contributorIds.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumContributors)
        {
            errors.Add("Manifest author-period selection has an invalid contributor count.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string contributorId in contributorIds)
        {
            ValidatePublicId(contributorId, "selection.authorPeriodManifest.contributorId", errors);
            if (!seen.Add(contributorId))
            {
                errors.Add($"Manifest author-period contributor ID '{contributorId}' is duplicated.");
            }
        }

        if (!contributorIds.SequenceEqual(
            contributorIds.Order(StringComparer.Ordinal),
            StringComparer.Ordinal))
        {
            errors.Add("Manifest author-period contributor IDs must use canonical ordinal order.");
        }
    }

    private static void ValidateManifestSelectionRepositories(
        IReadOnlyList<ChangePortfolioAuthorPeriodManifestRepository> repositories,
        List<string> errors)
    {
        if (repositories.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumRepositories)
        {
            errors.Add("Manifest author-period selection has an invalid repository count.");
        }

        HashSet<string> repositoryIds = new(StringComparer.Ordinal);
        int headCount = 0;
        foreach (ChangePortfolioAuthorPeriodManifestRepository repository in repositories)
        {
            ValidatePublicId(repository.Id, "selection.authorPeriodManifest.repository.id", errors);
            if (!repositoryIds.Add(repository.Id))
            {
                errors.Add($"Manifest author-period repository ID '{repository.Id}' is duplicated.");
            }

            headCount += ValidateManifestSelectionHeads(repository, errors);
        }

        if (!repositories.Select(repository => repository.Id).SequenceEqual(
            repositories.Select(repository => repository.Id).Order(StringComparer.Ordinal),
            StringComparer.Ordinal))
        {
            errors.Add("Manifest author-period repositories must use canonical ordinal order.");
        }

        if (headCount > ChangeAuthorPeriodManifestLimits.MaximumHeads)
        {
            errors.Add("Manifest author-period selection exceeds the total head limit.");
        }
    }

    private static int ValidateManifestSelectionHeads(
        ChangePortfolioAuthorPeriodManifestRepository repository,
        List<string> errors)
    {
        if (repository.Heads.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
        {
            errors.Add($"Manifest author-period repository '{repository.Id}' has an invalid head count.");
        }

        HashSet<string> headIds = new(StringComparer.Ordinal);
        HashSet<string> objectIds = new(StringComparer.Ordinal);
        foreach (ChangePortfolioAuthorPeriodManifestHead head in repository.Heads)
        {
            ValidatePublicId(head.Id, $"selection.authorPeriodManifest.repository[{repository.Id}].head.id", errors);
            if (!headIds.Add(head.Id))
            {
                errors.Add($"Manifest author-period repository '{repository.Id}' repeats head ID '{head.Id}'.");
            }

            RequireText(
                head.ObjectId,
                $"selection.authorPeriodManifest.repository[{repository.Id}].head[{head.Id}].objectId",
                errors);
            if (!IsObjectId(head.ObjectId))
            {
                errors.Add(
                    $"Manifest author-period repository '{repository.Id}' head '{head.Id}' has an invalid object ID.");
            }
            else if (!objectIds.Add(head.ObjectId))
            {
                errors.Add(
                    $"Manifest author-period repository '{repository.Id}' repeats head object '{head.ObjectId}'.");
            }
        }

        if (!repository.Heads.Select(head => head.Id).SequenceEqual(
            repository.Heads.Select(head => head.Id).Order(StringComparer.Ordinal),
            StringComparer.Ordinal))
        {
            errors.Add($"Manifest author-period repository '{repository.Id}' heads must use canonical ordinal order.");
        }

        return repository.Heads.Count;
    }

    private static void ValidateManifestAttribution(
        ChangePortfolioSelection selection,
        ChangePortfolioItemEstimate item,
        List<string> errors)
    {
        ChangePortfolioAttribution attribution = item.Attribution;
        ChangePortfolioAuthorPeriodManifestSelection? manifest = selection.AuthorPeriodManifest;
        ChangePortfolioAuthorPeriodManifestRepository? repository = manifest?.Repositories.FirstOrDefault(
            candidate => candidate.Id == item.RepositoryId);
        if (item.Selection.Kind != ChangeSelectionKind.Commit ||
            attribution.Kind is not (ChangePortfolioAttributionKind.DirectAuthor or
                ChangePortfolioAttributionKind.Coauthor) ||
            attribution.SelectedTimestamp is null || manifest is null || repository is null ||
            attribution.SelectedTimestamp < manifest.SinceInclusive ||
            attribution.SelectedTimestamp >= manifest.UntilExclusive)
        {
            errors.Add($"Portfolio item '{item.Id}' does not match manifest author-period attribution semantics.");
            return;
        }

        ValidateManifestContributorMatches(item.Id, attribution, manifest, errors);
        ValidateManifestHeadReachability(item.Id, attribution.HeadIds, repository, errors);
    }

    private static void ValidateManifestContributorMatches(
        string itemId,
        ChangePortfolioAttribution attribution,
        ChangePortfolioAuthorPeriodManifestSelection manifest,
        List<string> errors)
    {
        if (attribution.ContributorMatches is null || attribution.ContributorMatches.Count == 0)
        {
            errors.Add($"Portfolio item '{itemId}' requires at least one contributor match.");
            return;
        }

        HashSet<string> contributorIds = new(StringComparer.Ordinal);
        foreach (ChangePortfolioContributorMatch match in attribution.ContributorMatches)
        {
            if (!manifest.ContributorIds.Contains(match.ContributorId, StringComparer.Ordinal) ||
                !contributorIds.Add(match.ContributorId) || !Enum.IsDefined(match.Kind))
            {
                errors.Add($"Portfolio item '{itemId}' has an invalid or repeated contributor match.");
            }
        }

        if (!attribution.ContributorMatches.Select(match => match.ContributorId).SequenceEqual(
            attribution.ContributorMatches.Select(match => match.ContributorId).Order(StringComparer.Ordinal),
            StringComparer.Ordinal))
        {
            errors.Add($"Portfolio item '{itemId}' contributor matches must use canonical ordinal order.");
        }

        ChangePortfolioAttributionKind expectedKind = attribution.ContributorMatches.Any(
            match => match.Kind == ChangePortfolioContributorMatchKind.DirectAuthor)
            ? ChangePortfolioAttributionKind.DirectAuthor
            : ChangePortfolioAttributionKind.Coauthor;
        if (attribution.Kind != expectedKind)
        {
            errors.Add($"Portfolio item '{itemId}' attribution kind does not match its contributor matches.");
        }
    }

    private static void ValidateManifestHeadReachability(
        string itemId,
        IReadOnlyList<string>? headIds,
        ChangePortfolioAuthorPeriodManifestRepository repository,
        List<string> errors)
    {
        if (headIds is null || headIds.Count == 0 ||
            headIds.Count > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository ||
            headIds.Distinct(StringComparer.Ordinal).Count() != headIds.Count ||
            !headIds.SequenceEqual(headIds.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            headIds.Any(headId => !repository.Heads.Any(head => head.Id == headId)))
        {
            errors.Add($"Portfolio item '{itemId}' has invalid manifest head reachability.");
        }
    }
}
