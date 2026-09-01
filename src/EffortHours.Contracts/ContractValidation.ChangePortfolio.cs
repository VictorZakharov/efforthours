using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangePortfolioSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        List<string> errors = [];
        ValidatePortfolioSelection(selection, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangePortfolioManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "change portfolio manifest", errors);
        if (manifest.Items.Count is < 1 or > ChangePortfolioLimits.MaximumManifestItems)
        {
            errors.Add(
                $"A change portfolio manifest must contain between 1 and " +
                $"{ChangePortfolioLimits.MaximumManifestItems} items.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangePortfolioManifestItem item in manifest.Items)
        {
            RequireText(item.Id, "manifestItem.id", errors);
            RequireText(item.RepositoryId, $"manifestItem[{item.Id}].repositoryId", errors);
            bool hasRepositoryPath = !string.IsNullOrWhiteSpace(item.RepositoryPath);
            bool hasGitHubRepository = !string.IsNullOrWhiteSpace(item.GitHubRepository);
            if (hasRepositoryPath == hasGitHubRepository)
            {
                errors.Add(
                    $"Manifest item '{item.Id}' requires exactly one of repositoryPath or " +
                    "gitHubRepository.");
            }
            else if (hasRepositoryPath)
            {
                RequireText(item.RepositoryPath, $"manifestItem[{item.Id}].repositoryPath", errors);
            }
            RequireText(item.PullRequest, $"manifestItem[{item.Id}].pullRequest", errors);
            if (hasGitHubRepository)
            {
                RequireText(item.GitHubRepository, $"manifestItem[{item.Id}].gitHubRepository", errors);
                ValidateGitHubRepositoryIdentity(
                    item.GitHubRepository!,
                    $"manifestItem[{item.Id}].gitHubRepository",
                    errors);
            }

            if (!ids.Add(item.Id))
            {
                errors.Add($"Portfolio manifest item ID '{item.Id}' is duplicated.");
            }
        }

        return errors;
    }

    private static void ValidateGitHubRepositoryIdentity(
        string value,
        string path,
        List<string> errors)
    {
        string[] parts = value.Split('/');
        if (value.Length > 512 || parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace) ||
            parts.Any(part => part is "." or ".." || part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            errors.Add($"{path} must be a canonical owner/repository GitHub identity.");
        }
    }

    public static IReadOnlyList<string> Validate(ChangePortfolioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "change portfolio report", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.SourceChangeEstimatorVersion, "sourceChangeEstimatorVersion", errors);
        if (!Enum.IsDefined(report.Profile))
        {
            errors.Add("Change portfolio profile is invalid.");
        }

        ValidatePortfolioSelection(report.Selection, errors);
        ValidateRange(report.IsolatedEffort, "isolatedEffort", errors);
        ValidateRange(report.TotalEffort, "totalEffort", errors);
        ValidateRateAndCost(report.RateCard, report.TotalCost, report.TotalEffort, "totalCost", errors);
        ValidatePortfolioCategories(report.Categories, report.TotalEffort, "categories", errors);
        RequireUniqueText(report.Assumptions, "assumptions", errors);

        bool validEmpty = report.Items.Count == 0 && report.Selection.ManifestBased &&
            report.Selection.AuthorPeriodManifest is not null;
        if ((!validEmpty && report.Items.Count < 1) ||
            report.Items.Count > ChangePortfolioLimits.MaximumReportItems)
        {
            errors.Add(
                $"A change portfolio report must contain between 1 and " +
                $"{ChangePortfolioLimits.MaximumReportItems} item rows, or a manifest-based " +
                "author-period report may contain a complete zero selection.");
        }

        HashSet<string> itemIds = new(StringComparer.Ordinal);
        HashSet<string> selectorIds = new(StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioItemEstimate> itemsById =
            PortfolioItemIndex(report.Items);
        foreach (ChangePortfolioItemEstimate item in report.Items)
        {
            RequireText(item.Id, "portfolioItem.id", errors);
            RequireText(item.SelectorId, $"portfolioItem[{item.Id}].selectorId", errors);
            RequireText(item.RepositoryId, $"portfolioItem[{item.Id}].repositoryId", errors);
            RequireText(item.BaseContextId, $"portfolioItem[{item.Id}].baseContextId", errors);
            RequireText(item.EvidenceDigest, $"portfolioItem[{item.Id}].evidenceDigest", errors);
            RequireText(item.PatchDigest, $"portfolioItem[{item.Id}].patchDigest", errors);
            ValidateChangeSelection(item.Selection, errors);
            ValidateRange(item.IsolatedEffort, $"portfolioItem[{item.Id}].isolatedEffort", errors);
            ValidatePortfolioCategories(
                item.Categories,
                item.IsolatedEffort,
                $"portfolioItem[{item.Id}].categories",
                errors);
            ValidatePortfolioAttribution(report.Selection, item, errors);
            RequireUniqueText(
                item.UncertaintyReasons,
                $"portfolioItem[{item.Id}].uncertaintyReasons",
                errors);
            if (item.AllocatedExpectedHours < 0m)
            {
                errors.Add($"Portfolio item '{item.Id}' has a negative allocation.");
            }

            if (item.RepresentedPathCount < 0)
            {
                errors.Add($"Portfolio item '{item.Id}' has a negative represented-path count.");
            }

            if (!itemIds.Add(item.Id))
            {
                errors.Add($"Portfolio item ID '{item.Id}' is duplicated.");
            }
            if (!selectorIds.Add(item.SelectorId))
            {
                errors.Add($"Portfolio selector ID '{item.SelectorId}' is duplicated.");
            }
        }

        foreach (ChangePortfolioItemEstimate item in report.Items)
        {
            if (item.DuplicateOfItemId is not null)
            {
                if (!itemIds.Contains(item.DuplicateOfItemId) || item.DuplicateOfItemId == item.Id)
                {
                    errors.Add($"Portfolio item '{item.Id}' has an invalid duplicateOfItemId.");
                }

                if (item.AllocatedExpectedHours != 0m)
                {
                    errors.Add($"Exact duplicate portfolio item '{item.Id}' must have zero allocation.");
                }

                if (itemsById.TryGetValue(
                        item.DuplicateOfItemId,
                        out ChangePortfolioItemEstimate? original) &&
                    (original.RepositoryId != item.RepositoryId || original.PatchDigest != item.PatchDigest))
                {
                    errors.Add($"Exact duplicate portfolio item '{item.Id}' must share repository and patch identity with its original.");
                }
            }

            if (report.RateCard is null && item.AllocatedExpectedCost is not null)
            {
                errors.Add($"Portfolio item '{item.Id}' has allocated cost without a rate card.");
            }
            else if (report.RateCard is not null &&
                item.AllocatedExpectedCost != RoundMoney(item.AllocatedExpectedHours * report.RateCard.HourlyRate))
            {
                errors.Add($"Portfolio item '{item.Id}' allocated cost is inconsistent with its allocation.");
            }
        }

        ValidatePortfolioGroups(report, itemIds, errors);
        ValidatePortfolioAggregation(report, itemIds, errors);
        HashSet<string> adjustmentIds = new(StringComparer.Ordinal);
        foreach (ChangePortfolioAdjustment adjustment in report.Adjustments)
        {
            RequireText(adjustment.Id, "portfolioAdjustment.id", errors);
            RequireText(adjustment.Reason, $"portfolioAdjustment[{adjustment.Id}].reason", errors);
            if (!adjustmentIds.Add(adjustment.Id))
            {
                errors.Add($"Portfolio adjustment ID '{adjustment.Id}' is duplicated.");
            }

            if (!Enum.IsDefined(adjustment.Kind))
            {
                errors.Add($"Portfolio adjustment '{adjustment.Id}' has an invalid kind.");
            }

            if (adjustment.AffectedPathCount < 0 || adjustment.ItemIds.Any(id => !itemIds.Contains(id)))
            {
                errors.Add($"Portfolio adjustment '{adjustment.Id}' has invalid path counts or item references.");
            }

            RequireUniqueText(
                adjustment.ItemIds,
                $"portfolioAdjustment[{adjustment.Id}].itemIds",
                errors);
            if (adjustment.ItemIds.Count == 0)
            {
                errors.Add($"Portfolio adjustment '{adjustment.Id}' must reference at least one item.");
            }
        }

        EffortRange itemTotal = Sum(report.Items.Select(item => item.IsolatedEffort));
        if (itemTotal != report.IsolatedEffort)
        {
            errors.Add("Portfolio isolated effort does not equal the sum of item rows.");
        }

        decimal allocated = report.Items.Sum(item => item.AllocatedExpectedHours);
        if (allocated != report.TotalEffort.Expected)
        {
            errors.Add("Portfolio item allocations do not sum exactly to normalized expected effort.");
        }

        SignedEffortRange adjustmentTotal = SumSigned(report.Adjustments.Select(item => item.EffortDelta));
        if (adjustmentTotal.Low != report.TotalEffort.Low - report.IsolatedEffort.Low ||
            adjustmentTotal.Expected != report.TotalEffort.Expected - report.IsolatedEffort.Expected ||
            adjustmentTotal.High != report.TotalEffort.High - report.IsolatedEffort.High)
        {
            errors.Add("Portfolio adjustments do not reconcile isolated and normalized effort exactly.");
        }

        return errors;
    }

    private static void ValidatePortfolioSelection(
        ChangePortfolioSelection selection,
        List<string> errors)
    {
        RequireVersion(selection.SchemaVersion, "change portfolio selection", errors);
        if (!Enum.IsDefined(selection.Kind))
        {
            errors.Add("Change portfolio selection kind is invalid.");
            return;
        }

        if (selection.Kind == ChangePortfolioSelectionKind.PullRequests)
        {
            if (selection.AuthorPeriod is not null || selection.AuthorPeriodManifest is not null)
            {
                errors.Add("A pull-request portfolio cannot contain author-period selection metadata.");
            }

            return;
        }

        if (selection.ManifestBased)
        {
            if (selection.AuthorPeriod is not null)
            {
                errors.Add("A manifest-based author-period portfolio cannot contain execution alias metadata.");
            }

            if (selection.AuthorPeriodManifest is null)
            {
                errors.Add("A manifest-based author-period portfolio requires privacy-safe manifest metadata.");
                return;
            }

            ValidateAuthorPeriodManifestReportSelection(selection.AuthorPeriodManifest, errors);
            return;
        }

        if (selection.AuthorPeriodManifest is not null)
        {
            errors.Add("A direct author-period portfolio cannot contain manifest selection metadata.");
        }

        if (selection.AuthorPeriod is null)
        {
            errors.Add("An author-period portfolio requires authorPeriod metadata.");
            return;
        }

        ChangePortfolioAuthorPeriodSelection author = selection.AuthorPeriod;
        if (author.Aliases.Count is < 1 or > 128 || author.Aliases.Any(string.IsNullOrWhiteSpace) ||
            author.Aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count() != author.Aliases.Count)
        {
            errors.Add("Author-period aliases must be non-empty and unique ignoring case.");
        }

        if (author.SinceInclusive >= author.UntilExclusive)
        {
            errors.Add("Author-period sinceInclusive must be earlier than untilExclusive.");
        }

        RequireText(author.TimeZone, "selection.authorPeriod.timeZone", errors);
        RequireText(author.HeadSelector, "selection.authorPeriod.headSelector", errors);
        RequireText(author.HeadObjectId, "selection.authorPeriod.headObjectId", errors);
        if (!IsObjectId(author.HeadObjectId))
        {
            errors.Add("Author-period headObjectId must be a lowercase 40- or 64-character Git object ID.");
        }

        if (!Enum.IsDefined(author.DateField) || !Enum.IsDefined(author.MergePolicy) ||
            !Enum.IsDefined(author.CoauthorPolicy))
        {
            errors.Add("Author-period date, merge, and co-author policies must be recognized values.");
        }

        if (author.IntervalSemantics != "since-inclusive-until-exclusive")
        {
            errors.Add("Author-period interval semantics must be since-inclusive-until-exclusive.");
        }
    }

    private static void ValidatePortfolioAttribution(
        ChangePortfolioSelection selection,
        ChangePortfolioItemEstimate item,
        List<string> errors)
    {
        ChangePortfolioAttribution attribution = item.Attribution;
        if (!Enum.IsDefined(attribution.Kind) || attribution.ParentCount < 0 ||
            attribution.MergeCommit != (attribution.ParentCount > 1))
        {
            errors.Add($"Portfolio item '{item.Id}' has invalid attribution metadata.");
        }

        RequireUniqueText(
            attribution.AmbiguityReasons,
            $"portfolioItem[{item.Id}].attribution.ambiguityReasons",
            errors);
        if (selection.Kind == ChangePortfolioSelectionKind.PullRequests)
        {
            if (item.Selection.Kind != ChangeSelectionKind.PullRequest ||
                attribution.Kind != ChangePortfolioAttributionKind.PullRequest ||
                attribution.SelectedTimestamp is not null ||
                attribution.ContributorMatches is not null ||
                attribution.HeadIds is not null)
            {
                errors.Add($"Portfolio item '{item.Id}' does not match pull-request attribution semantics.");
            }

            return;
        }

        if (selection.ManifestBased)
        {
            ValidateManifestAttribution(selection, item, errors);
            return;
        }

        ChangePortfolioAuthorPeriodSelection? author = selection.AuthorPeriod;
        if (item.Selection.Kind != ChangeSelectionKind.Commit ||
            attribution.Kind is not (ChangePortfolioAttributionKind.DirectAuthor or
                ChangePortfolioAttributionKind.Coauthor) ||
            attribution.SelectedTimestamp is null || author is null ||
            attribution.SelectedTimestamp < author.SinceInclusive ||
            attribution.SelectedTimestamp >= author.UntilExclusive ||
            attribution.ContributorMatches is not null ||
            attribution.HeadIds is not null)
        {
            errors.Add($"Portfolio item '{item.Id}' does not match author-period attribution semantics.");
        }
    }

    private static void ValidatePortfolioCategories(
        IReadOnlyList<CategoryEstimate> categories,
        EffortRange expected,
        string path,
        List<string> errors)
    {
        HashSet<EffortCategory> seen = [];
        foreach (CategoryEstimate category in categories)
        {
            ValidateRange(category.Hours, $"{path}[{category.Category}]", errors);
            if (!seen.Add(category.Category))
            {
                errors.Add($"{path} contains duplicate category '{category.Category}'.");
            }
        }

        if (Sum(categories.Select(category => category.Hours)) != expected)
        {
            errors.Add($"{path} does not reconcile to its effort total.");
        }
    }

    private static bool IsObjectId(string value) =>
        value.Length is 40 or 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static SignedEffortRange SumSigned(IEnumerable<SignedEffortRange> ranges)
    {
        decimal low = 0m;
        decimal expected = 0m;
        decimal high = 0m;
        foreach (SignedEffortRange range in ranges)
        {
            low += range.Low;
            expected += range.Expected;
            high += range.High;
        }

        return new SignedEffortRange { Low = low, Expected = expected, High = high };
    }
}
