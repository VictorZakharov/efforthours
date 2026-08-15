using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationRepositoryDiagnosticBuilder
{
    public static CalibrationRepositoryDiagnostic Build(
        CalibrationRecord record,
        CalibrationCandidateView candidate,
        decimal materialContributionThreshold)
    {
        EffortRange reviewedTotal = CalibrationDiagnosticMath.Sum(
            record.Targets.Select(target => target.Hours));
        CalibrationRangeDiagnostic repositoryRange = CalibrationDiagnosticMath.Describe(
            reviewedTotal,
            candidate.TotalEffort);
        Dictionary<string, WorkItem> candidateItems = candidate.WorkItems.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        HashSet<string> referencedCandidateIds = new(StringComparer.Ordinal);
        List<CalibrationResidualContribution> componentSeeds = [];

        foreach (CalibrationTarget target in record.Targets.OrderBy(
                     target => target.Id,
                     StringComparer.Ordinal))
        {
            WorkItem[] resolved = [.. target.SourceWorkItemIds
                .Where(candidateItems.ContainsKey)
                .Select(id => candidateItems[id])
                .OrderBy(item => item.Id, StringComparer.Ordinal)];
            string[] missing = [.. target.SourceWorkItemIds
                .Where(id => !candidateItems.ContainsKey(id))
                .Order(StringComparer.Ordinal)];
            foreach (WorkItem item in resolved)
            {
                referencedCandidateIds.Add(item.Id);
            }

            CalibrationResidualMappingStatus mappingStatus = missing.Length > 0
                ? CalibrationResidualMappingStatus.Incomplete
                : resolved.Any(item => item.Category != target.Category)
                    ? CalibrationResidualMappingStatus.CategoryMismatch
                    : CalibrationResidualMappingStatus.Matched;
            EffortRange predicted = CalibrationDiagnosticMath.Sum(
                resolved.Select(item => item.Hours));
            string[] candidateReasons = UniqueSorted(resolved.Select(item => item.Reason));

            componentSeeds.Add(new CalibrationResidualContribution
            {
                Rank = 0,
                Kind = CalibrationResidualComponentKind.ReviewedTarget,
                MappingStatus = mappingStatus,
                Id = $"target:{target.Id}",
                ReviewedTargetId = target.Id,
                Category = target.Category,
                Title = target.Title,
                Scope = target.Scope,
                ReviewedRationale = target.Rationale,
                ReviewedSizeException = target.SizeException,
                CandidateLeafCount = resolved.Length,
                CandidateLeaves = [.. resolved.Select(item => BuildLeaf(item, candidateReasons))],
                MissingCandidateWorkItemIds = missing,
                ReviewedEvidenceIds = [.. target.EvidenceIds.Order(StringComparer.Ordinal)],
                CandidateEvidenceIds = UniqueSorted(resolved.SelectMany(item => item.EvidenceIds)),
                CandidateReasons = candidateReasons,
                Range = CalibrationDiagnosticMath.Describe(target.Hours, predicted),
                CandidateConfidence = resolved.Length == 0
                    ? null
                    : resolved.Min(item => item.Confidence),
                ReviewedUncertaintyReasons = [.. target.UncertaintyReasons.Order(StringComparer.Ordinal)],
                CandidateUncertaintyReasons = UniqueSorted(
                    resolved.SelectMany(item => item.UncertaintyReasons)),
                AbsoluteErrorShare = 0m,
                CumulativeAbsoluteErrorShare = 0m,
                MaterialContributor = false,
            });
        }

        foreach (WorkItem item in candidate.WorkItems
                     .Where(item => !referencedCandidateIds.Contains(item.Id))
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            string[] candidateReasons = [item.Reason];
            componentSeeds.Add(new CalibrationResidualContribution
            {
                Rank = 0,
                Kind = CalibrationResidualComponentKind.UnmatchedCandidateWorkItem,
                MappingStatus = CalibrationResidualMappingStatus.UnmatchedCandidate,
                Id = $"candidate-work-item:{item.Id}",
                Category = item.Category,
                Title = item.Title,
                Scope = item.Scope,
                CandidateLeafCount = 1,
                CandidateLeaves = [BuildLeaf(item, candidateReasons)],
                CandidateEvidenceIds = UniqueSorted(item.EvidenceIds),
                CandidateReasons = candidateReasons,
                Range = CalibrationDiagnosticMath.Describe(
                    CalibrationDiagnosticMath.ZeroRange,
                    item.Hours),
                CandidateConfidence = item.Confidence,
                CandidateUncertaintyReasons = [.. item.UncertaintyReasons.Order(StringComparer.Ordinal)],
                AbsoluteErrorShare = 0m,
                CumulativeAbsoluteErrorShare = 0m,
                MaterialContributor = false,
            });
        }

        CalibrationResidualContribution[] components = RankComponents(
            componentSeeds,
            materialContributionThreshold);
        CalibrationCategoryResidual[] categories = BuildCategories(
            record,
            candidate,
            materialContributionThreshold);
        decimal grossComponentError = components.Sum(component =>
            component.Range.ExpectedAbsoluteErrorHours);
        decimal grossCategoryError = categories.Sum(category =>
            category.Range.ExpectedAbsoluteErrorHours);

        return new CalibrationRepositoryDiagnostic
        {
            Rank = 0,
            RecordId = record.Id,
            RepositoryId = record.Repository.Id,
            SourceDigest = record.Repository.SourceDigest,
            Profile = record.Profile,
            BaselineId = record.BaselineId,
            CandidateEstimatorVersion = candidate.EstimatorVersion,
            CandidateEstimateDigest = candidate.EstimateDigest,
            Range = repositoryRange,
            TargetCount = record.Targets.Count,
            CandidateWorkItemCount = candidate.WorkItems.Count,
            MatchedTargetCount = components.Count(component =>
                component.MappingStatus == CalibrationResidualMappingStatus.Matched),
            IncompleteTargetCount = components.Count(component =>
                component.MappingStatus == CalibrationResidualMappingStatus.Incomplete),
            CategoryMismatchTargetCount = components.Count(component =>
                component.MappingStatus == CalibrationResidualMappingStatus.CategoryMismatch),
            UnmatchedCandidateWorkItemCount = components.Count(component =>
                component.MappingStatus == CalibrationResidualMappingStatus.UnmatchedCandidate),
            ReviewedSizeExceptionTargetCount = record.Targets.Count(target =>
                target.SizeException is not null),
            OversizedReviewedTargetCount = record.Targets.Count(target =>
                target.Hours.Expected > 8m),
            GrossCategoryExpectedErrorHours = grossCategoryError,
            CategoryCancellationHours = grossCategoryError -
                repositoryRange.ExpectedAbsoluteErrorHours,
            GrossComponentExpectedErrorHours = grossComponentError,
            ComponentCancellationHours = grossComponentError -
                repositoryRange.ExpectedAbsoluteErrorHours,
            MaterialCategoryCount = categories.Count(category => category.MaterialContributor),
            MaterialComponentCount = components.Count(component => component.MaterialContributor),
            CategoryReconciliationDelta = Reconciliation(
                categories.Select(category => category.Range.SignedError),
                repositoryRange.SignedError),
            ComponentReconciliationDelta = Reconciliation(
                components.Select(component => component.Range.SignedError),
                repositoryRange.SignedError),
            Categories = categories,
            Components = components,
        };
    }

    private static CalibrationCategoryResidual[] BuildCategories(
        CalibrationRecord record,
        CalibrationCandidateView candidate,
        decimal materialContributionThreshold)
    {
        Dictionary<EffortCategory, EffortRange> reviewed = record.Targets
            .GroupBy(target => target.Category)
            .ToDictionary(
                group => group.Key,
                group => CalibrationDiagnosticMath.Sum(group.Select(target => target.Hours)));
        Dictionary<EffortCategory, EffortRange> predicted = candidate.WorkItems
            .GroupBy(item => item.Category)
            .ToDictionary(
                group => group.Key,
                group => CalibrationDiagnosticMath.Sum(group.Select(item => item.Hours)));
        List<CalibrationCategoryResidual> seeds = [];
        foreach (EffortCategory category in Enum.GetValues<EffortCategory>())
        {
            EffortRange reviewedRange = reviewed.GetValueOrDefault(
                category,
                CalibrationDiagnosticMath.ZeroRange);
            EffortRange candidateRange = predicted.GetValueOrDefault(
                category,
                CalibrationDiagnosticMath.ZeroRange);
            if (reviewedRange == CalibrationDiagnosticMath.ZeroRange &&
                candidateRange == CalibrationDiagnosticMath.ZeroRange)
            {
                continue;
            }

            seeds.Add(new CalibrationCategoryResidual
            {
                Rank = 0,
                Category = category,
                Range = CalibrationDiagnosticMath.Describe(reviewedRange, candidateRange),
                AbsoluteErrorShare = 0m,
                CumulativeAbsoluteErrorShare = 0m,
                MaterialContributor = false,
            });
        }

        decimal gross = seeds.Sum(seed => seed.Range.ExpectedAbsoluteErrorHours);
        decimal cumulative = 0m;
        return [.. seeds
            .OrderByDescending(seed => seed.Range.ExpectedAbsoluteErrorHours)
            .ThenBy(seed => seed.Category)
            .Select((seed, index) =>
            {
                decimal previous = cumulative;
                cumulative += seed.Range.ExpectedAbsoluteErrorHours;
                return seed with
                {
                    Rank = index + 1,
                    AbsoluteErrorShare = CalibrationDiagnosticMath.Share(
                        seed.Range.ExpectedAbsoluteErrorHours,
                        gross),
                    CumulativeAbsoluteErrorShare = CalibrationDiagnosticMath.Share(cumulative, gross),
                    MaterialContributor = seed.Range.ExpectedAbsoluteErrorHours > 0m &&
                        CalibrationDiagnosticMath.Share(previous, gross) < materialContributionThreshold,
                };
            })];
    }

    private static CalibrationResidualContribution[] RankComponents(
        IReadOnlyList<CalibrationResidualContribution> seeds,
        decimal materialContributionThreshold)
    {
        decimal gross = seeds.Sum(seed => seed.Range.ExpectedAbsoluteErrorHours);
        decimal cumulative = 0m;
        return [.. seeds
            .OrderByDescending(seed => seed.Range.ExpectedAbsoluteErrorHours)
            .ThenBy(seed => seed.Id, StringComparer.Ordinal)
            .Select((seed, index) =>
            {
                decimal previous = cumulative;
                cumulative += seed.Range.ExpectedAbsoluteErrorHours;
                return seed with
                {
                    Rank = index + 1,
                    AbsoluteErrorShare = CalibrationDiagnosticMath.Share(
                        seed.Range.ExpectedAbsoluteErrorHours,
                        gross),
                    CumulativeAbsoluteErrorShare = CalibrationDiagnosticMath.Share(cumulative, gross),
                    MaterialContributor = seed.Range.ExpectedAbsoluteErrorHours > 0m &&
                        CalibrationDiagnosticMath.Share(previous, gross) < materialContributionThreshold,
                };
            })];
    }

    private static CalibrationSignedEffortRange Reconciliation(
        IEnumerable<CalibrationSignedEffortRange> contributions,
        CalibrationSignedEffortRange repository) =>
        CalibrationDiagnosticMath.Round4(CalibrationDiagnosticMath.Difference(
            CalibrationDiagnosticMath.Sum(contributions),
            repository));

    private static string[] UniqueSorted(IEnumerable<string> values) => [.. values
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)];

    private static CalibrationCandidateLeafDiagnostic BuildLeaf(
        WorkItem item,
        string[] candidateReasons) => new()
        {
            Id = item.Id,
            Category = item.Category,
            Title = item.Title,
            Scope = item.Scope,
            Complexity = item.Complexity,
            Hours = item.Hours,
            Confidence = item.Confidence,
            EvidenceCount = item.EvidenceIds.Count,
            EvidenceDigest = CalibrationDigest.ComputeStringSet(item.EvidenceIds),
            ReasonIndex = Array.IndexOf(candidateReasons, item.Reason),
            AssumptionCount = item.Assumptions.Count,
            ExclusionCount = item.Exclusions.Count,
            UncertaintyReasonCount = item.UncertaintyReasons.Count,
        };
}
