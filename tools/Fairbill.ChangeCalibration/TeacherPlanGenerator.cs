using System.Text;
using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.ChangeCalibration;

internal static class TeacherPlanGenerator
{
    public const string Version = "change-teacher-plan-generator/0.1.0";

    public static async Task GenerateAsync(
        string policyPath,
        string indexPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        TeacherPolicy policy = ContractJson.Deserialize<TeacherPolicy>(
            await File.ReadAllTextAsync(policyPath, cancellationToken).ConfigureAwait(false));
        GeneratedFixtureIndex index = ContractJson.Deserialize<GeneratedFixtureIndex>(
            await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false));
        string root = Path.GetDirectoryName(Path.GetFullPath(indexPath))
            ?? throw new InvalidDataException("Fixture index has no parent directory.");
        Dictionary<string, ChangeEstimateReport> reports = [];
        foreach (GeneratedFixtureCase fixture in index.Cases)
        {
            string path = Path.Combine(
                root,
                fixture.EstimatePath.Replace('/', Path.DirectorySeparatorChar));
            reports.Add(
                fixture.Id,
                ContractJson.Deserialize<ChangeEstimateReport>(
                    await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)));
        }

        Validate(policy, index, reports);
        Dictionary<string, TeacherCasePolicy> cases = policy.Cases.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        List<CalibrationReviewPlanRecord> records = [];
        foreach (GeneratedFixtureCase fixture in index.Cases.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            ChangeEstimateReport report = reports[fixture.Id];
            TeacherCasePolicy teacher = cases[fixture.Id];
            records.Add(CreateRecord(policy, fixture, report, teacher));
        }

        CalibrationReviewPlan plan = new()
        {
            CompilerVersion = ChangeCalibrationReviewCompiler.CompilerVersion,
            Id = policy.Id,
            Version = policy.Version,
            Description = policy.Description,
            Rubric = new CalibrationRubricReference
            {
                Id = ChangeCalibrationAuthoring.RubricId,
                Version = ChangeCalibrationAuthoring.RubricVersion,
            },
            Records = records,
        };
        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(plan);
        if (semanticErrors.Count > 0)
        {
            throw new InvalidDataException(
                "Generated teacher plan is invalid: " + string.Join(" ", semanticErrors));
        }

        string json = ContractJson.Serialize(plan);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidDataException(
                "Generated teacher plan fails its schema: " + string.Join(" ", schema.Errors));
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidDataException("Teacher-plan output has no parent directory."));
        await File.WriteAllTextAsync(
            fullOutputPath,
            json + Environment.NewLine,
            new UTF8Encoding(false),
            cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(
            $"Generated {records.Count} teacher review-plan records in {fullOutputPath}.")
            .ConfigureAwait(false);
    }

    private static CalibrationReviewPlanRecord CreateRecord(
        TeacherPolicy policy,
        GeneratedFixtureCase fixture,
        ChangeEstimateReport report,
        TeacherCasePolicy teacher)
    {
        ChangeCalibrationReference change = ChangeCalibrationIdentity.CreateReference(
            report,
            fixture.Id,
            fixture.CoverageTags);
        SourceCapability[] capabilities = [.. report.WorkItems
            .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
            .Select(group => new SourceCapability(
                group.Key,
                group.First().Category,
                group.First().Title))
            .OrderBy(item => item.Id, StringComparer.Ordinal)];
        Dictionary<EffortCategory, TeacherCategoryDecision> decisions = teacher.Categories
            .ToDictionary(item => item.Category);
        List<CalibrationCapabilityReviewDecision> reviewed = [];
        foreach (IGrouping<EffortCategory, SourceCapability> categoryGroup in capabilities
                     .GroupBy(item => item.Category)
                     .OrderBy(group => group.Key))
        {
            TeacherCategoryDecision decision = decisions[categoryGroup.Key];
            SourceCapability[] categoryCapabilities = [.. categoryGroup
                .OrderByDescending(IsPreferred)
                .ThenBy(item => item.Title, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)];
            int selectedCount = decision.Action == TeacherCategoryAction.Target
                ? decision.TargetCount ?? categoryCapabilities.Length
                : 0;
            EffortRange[] ranges = selectedCount == 0
                ? []
                : Split(decision.Hours!, selectedCount);
            HashSet<string> selectedIds = [.. categoryCapabilities
                .Take(selectedCount)
                .Select(item => item.Id)];
            int rangeIndex = 0;
            foreach (SourceCapability capability in categoryCapabilities.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                bool selected = selectedIds.Contains(capability.Id);
                reviewed.Add(new CalibrationCapabilityReviewDecision
                {
                    SourceCapabilityId = capability.Id,
                    Rationale = selected
                        ? $"{teacher.Rationale} The teacher allocated the independently reasoned " +
                            $"{Kebab(capability.Category)} category budget across {selectedCount} " +
                            "represented source capability target(s)."
                        : $"{teacher.Rationale} The teacher rejected this source capability as " +
                            "duplicate, mechanically excluded, or outside the final logical category work.",
                    Targets =
                    [
                        selected
                            ? new CalibrationReviewTargetDecision
                            {
                                Hours = ranges[rangeIndex++],
                                UncertaintyReasons = decision.UncertaintyReasons,
                            }
                            : new CalibrationReviewTargetDecision
                            {
                                Hours = Zero,
                                SizeException =
                                    "Explicit false-positive exclusion: the source capability does not " +
                                    "represent distinct final-delta EHE under the Change rubric.",
                            },
                    ],
                });
            }
        }

        return new CalibrationReviewPlanRecord
        {
            Id = $"record:{fixture.Id}",
            Repository = new CalibrationRepositoryReference
            {
                Id = fixture.RepositoryFamilyId,
                Name = report.Repository.Name,
                SourceDigest = change.FinalDeltaDigest,
            },
            Change = change,
            Profile = report.Profile,
            BaselineId = report.Baseline.Id,
            Partition = fixture.Partition,
            SourceEstimatorVersion = report.EstimatorVersion,
            SourceEstimateDigest = CalibrationDigest.Compute(report),
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.Synthetic,
                SourceReference = $"fairbill://calibration/changes/public-synthetic/0.1.0.fixtures.json#{fixture.Id}",
                Revision = $"{report.Selection.Base.ObjectId}..{report.Selection.Head.ObjectId}",
                LicenseExpression = "MIT",
                RedistributionAllowed = true,
                Notes = "Project-owned synthetic immutable states; generated without Git activity metadata.",
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = policy.CompletedOn,
                Reviewers = [policy.Reviewer],
                Notes = policy.ReviewNotes,
            },
            Capabilities = [.. reviewed.OrderBy(item => item.SourceCapabilityId, StringComparer.Ordinal)],
        };
    }

    private static void Validate(
        TeacherPolicy policy,
        GeneratedFixtureIndex index,
        Dictionary<string, ChangeEstimateReport> reports)
    {
        List<string> errors = [];
        if (policy.SchemaVersion != ContractVersions.V1 || string.IsNullOrWhiteSpace(policy.Id) ||
            string.IsNullOrWhiteSpace(policy.Version) || string.IsNullOrWhiteSpace(policy.Description) ||
            string.IsNullOrWhiteSpace(policy.ReviewNotes))
        {
            errors.Add("Teacher policy identity, description, notes, and schema version are required.");
        }

        if (policy.Reviewer.Kind != CalibrationReviewerKind.HostAi ||
            policy.Reviewer.Role != CalibrationReviewerRole.Teacher)
        {
            errors.Add("Synthetic teacher policy requires a host-AI teacher reviewer.");
        }

        AddDuplicates(policy.Cases.Select(item => item.Id), "teacher case", errors);
        HashSet<string> expectedCases = index.Cases.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualCases = policy.Cases.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string missing in expectedCases.Except(actualCases, StringComparer.Ordinal))
        {
            errors.Add($"Teacher policy is missing case '{missing}'.");
        }

        foreach (string extra in actualCases.Except(expectedCases, StringComparer.Ordinal))
        {
            errors.Add($"Teacher policy has unknown case '{extra}'.");
        }

        foreach (TeacherCasePolicy teacher in policy.Cases.Where(item => expectedCases.Contains(item.Id)))
        {
            ChangeEstimateReport report = reports[teacher.Id];
            AddDuplicates(teacher.Categories.Select(item => item.Category.ToString()),
                $"{teacher.Id} category", errors);
            HashSet<EffortCategory> expectedCategories = [.. report.WorkItems.Select(item => item.Category)];
            HashSet<EffortCategory> actualCategories = [.. teacher.Categories.Select(item => item.Category)];
            if (!expectedCategories.SetEquals(actualCategories))
            {
                errors.Add($"{teacher.Id}: policy categories must exactly match source categories.");
            }

            Dictionary<EffortCategory, int> capabilityCounts = report.WorkItems
                .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
                .GroupBy(group => group.First().Category)
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (TeacherCategoryDecision decision in teacher.Categories)
            {
                ValidateDecision(teacher.Id, decision, capabilityCounts.GetValueOrDefault(decision.Category), errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateDecision(
        string caseId,
        TeacherCategoryDecision decision,
        int capabilityCount,
        List<string> errors)
    {
        if (decision.Action == TeacherCategoryAction.Exclude)
        {
            if (decision.Hours is not null || decision.TargetCount is not null)
            {
                errors.Add($"{caseId}/{decision.Category}: exclusions cannot carry hours or target count.");
            }

            return;
        }

        if (decision.Hours is null)
        {
            errors.Add($"{caseId}/{decision.Category}: targeted category requires hours.");
            return;
        }

        int targetCount = decision.TargetCount ?? capabilityCount;
        if (targetCount < 1 || targetCount > capabilityCount)
        {
            errors.Add($"{caseId}/{decision.Category}: target count is outside 1..{capabilityCount}.");
        }

        EffortRange hours = decision.Hours;
        if (hours.Low < 0m || hours.Low > hours.Expected || hours.Expected > hours.High)
        {
            errors.Add($"{caseId}/{decision.Category}: hours must satisfy 0 <= low <= expected <= high.");
        }

        if (targetCount > 0 && (hours.Expected / targetCount < 0.5m || hours.Expected / targetCount > 8m))
        {
            errors.Add($"{caseId}/{decision.Category}: each generated target must normally be 0.5..8 hours.");
        }
    }

    private static EffortRange[] Split(EffortRange total, int count)
    {
        decimal usedLow = 0m;
        decimal usedExpected = 0m;
        decimal usedHigh = 0m;
        return [.. Enumerable.Range(0, count).Select(index => new EffortRange
        {
            Low = Part(total.Low, count, index, ref usedLow),
            Expected = Part(total.Expected, count, index, ref usedExpected),
            High = Part(total.High, count, index, ref usedHigh),
        })];
    }

    private static decimal Part(decimal total, int count, int index, ref decimal used)
    {
        decimal value = index == count - 1
            ? total - used
            : decimal.Round(total / count, 2, MidpointRounding.AwayFromZero);
        used += value;
        return value;
    }

    private static bool IsPreferred(SourceCapability capability) => capability.Category switch
    {
        EffortCategory.SpecificationComprehensionAndDomainLearning =>
            capability.Title.StartsWith("Understand the bounded change", StringComparison.Ordinal),
        EffortCategory.SelfReviewAndSystemIntegration =>
            capability.Title.StartsWith("Review and integrate the completed change", StringComparison.Ordinal),
        _ => true,
    };

    private static void AddDuplicates(IEnumerable<string> values, string label, List<string> errors)
    {
        foreach (string duplicate in values.GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add($"Duplicate {label} '{duplicate}'.");
        }
    }

    private static string Kebab<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? $"-{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));

    private static EffortRange Zero { get; } = new() { Low = 0m, Expected = 0m, High = 0m };

    private sealed record SourceCapability(
        string Id,
        EffortCategory Category,
        string Title);
}
