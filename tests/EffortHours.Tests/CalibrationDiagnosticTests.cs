using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class CalibrationDiagnosticTests
{
    [Fact]
    public void DiagnosticRanksEveryResidualAndReconcilesCancellationExactlyInMemory()
    {
        EstimateReport candidate = CreateCancellationCandidate();
        CalibrationCorpus corpus = CreateCancellationCorpus(candidate.Repository.SourceDigest!);

        CalibrationDiagnosticReport first = CalibrationDiagnostics.Diagnose(
            corpus,
            [candidate],
            CalibrationPartition.Validation);
        CalibrationRecord record = Assert.Single(corpus.Records);
        CalibrationDiagnosticReport reordered = CalibrationDiagnostics.Diagnose(
            corpus with { Records = [record with { Targets = [.. record.Targets.Reverse()] }] },
            [candidate],
            CalibrationPartition.Validation);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationDiagnostic,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(reordered));
        Assert.Equal(CalibrationDiagnostics.DiagnosticVersion, first.DiagnosticVersion);
        Assert.Equal(0.8m, first.MaterialContributionThreshold);

        CalibrationRepositoryDiagnostic repository = Assert.Single(first.Repositories);
        Assert.Equal(2m, repository.Range.SignedError.Expected);
        Assert.Equal(10m, repository.GrossComponentExpectedErrorHours);
        Assert.Equal(8m, repository.ComponentCancellationHours);
        Assert.Equal(10m, repository.GrossCategoryExpectedErrorHours);
        Assert.Equal(8m, repository.CategoryCancellationHours);
        Assert.Equal(2, repository.MaterialComponentCount);
        Assert.Equal(
            [4m, 4m, 2m],
            repository.Components.Select(component => component.Range.ExpectedAbsoluteErrorHours));
        Assert.Equal(
            [0.4m, 0.8m, 1m],
            repository.Components.Select(component => component.CumulativeAbsoluteErrorShare));
        Assert.Equal(
            [true, true, false],
            repository.Components.Select(component => component.MaterialContributor));
        Assert.Contains(repository.Components, component =>
            component.MappingStatus == CalibrationResidualMappingStatus.UnmatchedCandidate &&
            component.CandidateEvidenceIds.SequenceEqual(["evidence:c"]));
        Assert.Equal(3, repository.Components.Sum(component => component.CandidateLeafCount));
        Assert.Equal(0, repository.OversizedReviewedTargetCount);
        Assert.All(repository.Components.SelectMany(component => component.CandidateLeaves), leaf =>
            Assert.InRange(leaf.Hours.Expected, 0.5m, 8m));
        Assert.All(repository.Components.SelectMany(component => component.CandidateLeaves), leaf =>
        {
            Assert.StartsWith("sha256:", leaf.EvidenceDigest, StringComparison.Ordinal);
            Assert.Equal(0, leaf.ReasonIndex);
        });
        Assert.Equal(ZeroSignedRange, repository.CategoryReconciliationDelta);
        Assert.Equal(ZeroSignedRange, repository.ComponentReconciliationDelta);
        Assert.Equal(ZeroSignedRange, first.Summary.RepositoryReconciliationDelta);
        Assert.True(repository.Range.CandidateSymmetric);
        Assert.Equal(CalibrationIntervalStatus.Covered, repository.Range.ReviewedExpectedStatus);

        CalibrationDiagnosticReport inconsistent = first with
        {
            Summary = first.Summary with
            {
                CandidateSymmetricRepositoryCount = 0,
            },
        };
        Assert.Contains(
            ContractValidation.Validate(inconsistent),
            error => error.Contains("statuses do not reconcile", StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticRetainsIncompleteMappingsAndUnmatchedReplacementItemsInReconciliation()
    {
        EstimateReport candidate = CreateCancellationCandidate();
        WorkItem replacement = candidate.WorkItems[1] with { Id = "work:b-renamed" };
        EstimateReport renamed = candidate with
        {
            WorkItems = [candidate.WorkItems[0], replacement, candidate.WorkItems[2]],
        };

        CalibrationDiagnosticReport report = CalibrationDiagnostics.Diagnose(
            CreateCancellationCorpus(candidate.Repository.SourceDigest!),
            [renamed],
            CalibrationPartition.Validation);
        CalibrationRepositoryDiagnostic repository = Assert.Single(report.Repositories);

        Assert.Equal(1, repository.IncompleteTargetCount);
        Assert.Equal(2, repository.UnmatchedCandidateWorkItemCount);
        Assert.Equal(14m, repository.GrossComponentExpectedErrorHours);
        Assert.Equal(12m, repository.ComponentCancellationHours);
        CalibrationResidualContribution incomplete = Assert.Single(
            repository.Components,
            component => component.MappingStatus == CalibrationResidualMappingStatus.Incomplete);
        Assert.Equal(["work:b"], incomplete.MissingCandidateWorkItemIds);
        Assert.Empty(incomplete.CandidateLeaves);
        Assert.Equal(-6m, incomplete.Range.SignedError.Expected);
        Assert.Equal(ZeroSignedRange, repository.ComponentReconciliationDelta);
    }

    [Fact]
    public void DiagnosticSeparatesRawAndNormalizedRepositoryWidthCorrelationInMemory()
    {
        DiagnosticCase[] cases =
        [
            CreateCase("a", candidate: Hours(8m, 12m, 16m), reviewed: Hours(8m, 10m, 12m)),
            CreateCase("b", candidate: Hours(90m, 100m, 110m), reviewed: Hours(15m, 20m, 25m)),
            CreateCase("c", candidate: Hours(30m, 50m, 70m), reviewed: Hours(90m, 100m, 110m)),
        ];
        CalibrationCorpus corpus = CreateCorpus(cases.Select(item => item.Record));

        CalibrationDiagnosticReport report = CalibrationDiagnostics.Diagnose(
            corpus,
            [.. cases.Select(item => item.Candidate)],
            CalibrationPartition.Validation);

        Assert.Equal(3, report.Summary.RawRepositoryWidthCorrelationSampleCount);
        Assert.Equal(1m, report.Summary.RawRepositoryWidthCorrelation);
        Assert.Equal(3, report.Summary.NormalizedRepositoryWidthCorrelationSampleCount);
        Assert.True(report.Summary.NormalizedRepositoryWidthCorrelation < 0m);
        Assert.Equal(1, report.Summary.ReviewedExpectedCoveredRepositoryCount);
        Assert.Equal(1, report.Summary.CandidateTooHighRepositoryCount);
        Assert.Equal(1, report.Summary.CandidateTooLowRepositoryCount);
        Assert.Equal(3, report.Summary.CandidateSymmetricRepositoryCount);
        Assert.Equal(3, report.Summary.ReviewedSizeExceptionTargetCount);
        Assert.Equal(3, report.Summary.OversizedReviewedTargetCount);
    }

    private static CalibrationCorpus CreateCancellationCorpus(string sourceDigest)
    {
        CalibrationTarget targetA = Target(
            "target:a",
            EffortCategory.ProductionImplementation,
            "work:a",
            "evidence:a",
            Hours(3m, 4m, 5m));
        CalibrationTarget targetB = Target(
            "target:b",
            EffortCategory.UnitTesting,
            "work:b",
            "evidence:b",
            Hours(5m, 6m, 7m));
        return CreateCorpus(
        [
            Record("cancellation", sourceDigest, [targetA, targetB]),
        ]);
    }

    private static EstimateReport CreateCancellationCandidate()
    {
        WorkItem itemA = Item(
            "work:a",
            EffortCategory.ProductionImplementation,
            "evidence:a",
            Hours(6m, 8m, 10m));
        WorkItem itemB = Item(
            "work:b",
            EffortCategory.UnitTesting,
            "evidence:b",
            Hours(1m, 2m, 3m));
        WorkItem itemC = Item(
            "work:c",
            EffortCategory.Documentation,
            "evidence:c",
            Hours(1m, 2m, 3m));
        return Candidate("cancellation", [itemA, itemB, itemC]);
    }

    private static DiagnosticCase CreateCase(
        string id,
        EffortRange candidate,
        EffortRange reviewed)
    {
        WorkItem item = Item(
            $"work:{id}",
            EffortCategory.ProductionImplementation,
            $"evidence:{id}",
            candidate);
        EstimateReport report = Candidate(id, [item]);
        CalibrationTarget target = Target(
            $"target:{id}",
            EffortCategory.ProductionImplementation,
            item.Id,
            $"evidence:{id}",
            reviewed,
            reviewed.Expected > 8m ? "Synthetic aggregate range-correlation fixture." : null);
        return new DiagnosticCase(
            Record(id, report.Repository.SourceDigest!, [target]),
            report);
    }

    private static CalibrationCorpus CreateCorpus(IEnumerable<CalibrationRecord> records) => new()
    {
        Id = "synthetic-diagnostic",
        Version = "1.0.0",
        Description = "Memory-only calibration residual diagnostic fixture.",
        Rubric = new CalibrationRubricReference
        {
            Id = "ehe-work-item",
            Version = "1.1.0",
        },
        Records = [.. records],
    };

    private static CalibrationRecord Record(
        string id,
        string sourceDigest,
        IReadOnlyList<CalibrationTarget> targets) => new()
        {
            Id = $"record:{id}",
            Repository = new CalibrationRepositoryReference
            {
                Id = $"repository:{id}",
                Name = $"Synthetic {id}",
                SourceDigest = sourceDigest,
            },
            Profile = EstimationProfile.Implementation,
            BaselineId = "senior-contractor-2026-no-ai",
            Partition = CalibrationPartition.Validation,
            SourceEstimatorVersion = "source/1.0.0",
            SourceEstimateDigest = $"sha256:source-estimate-{id}",
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.Synthetic,
                SourceReference = "synthetic:memory-only",
                Revision = "1",
                LicenseExpression = "MIT",
                RedistributionAllowed = true,
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = new DateOnly(2026, 8, 15),
                Reviewers =
                [
                    new CalibrationReviewer
                    {
                        Id = "teacher:diagnostic-test",
                        Kind = CalibrationReviewerKind.HostAi,
                        Role = CalibrationReviewerRole.Teacher,
                        ModelId = "logical-estimator",
                        ModelVersion = "test-1",
                    },
                ],
            },
            Targets = targets,
        };

    private static CalibrationTarget Target(
        string id,
        EffortCategory category,
        string sourceWorkItemId,
        string evidenceId,
        EffortRange hours,
        string? sizeException = null) => new()
        {
            Id = id,
            Category = category,
            Title = id,
            Scope = $"scope:{id}",
            SourceWorkItemIds = [sourceWorkItemId],
            EvidenceIds = [evidenceId],
            Hours = hours,
            Rationale = "Synthetic independently reviewed target.",
            UncertaintyReasons = ["Synthetic range uncertainty."],
            SizeException = sizeException,
        };

    private static EstimateReport Candidate(string id, IReadOnlyList<WorkItem> items) => new()
    {
        EstimatorVersion = "candidate/1.0.0",
        Repository = new RepositoryDescriptor
        {
            Name = $"Synthetic {id}",
            SourceDigest = $"sha256:fixture-{id}",
        },
        Profile = EstimationProfile.Implementation,
        Baseline = new EstimationBaseline
        {
            Id = "senior-contractor-2026-no-ai",
            WorkerProfile = "Competent senior contractor",
            TechnologyBaselineYear = 2026,
            BusinessDomainFamiliar = false,
            UsesAi = false,
            Description = "Synthetic test baseline.",
        },
        TotalEffort = ContractValidation.Sum(items.Select(item => item.Hours)),
        Categories = [.. items
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryEstimate
            {
                Category = group.Key,
                Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
            })],
        WorkItems = items,
        Verification = new VerificationSummary
        {
            Mode = VerificationMode.StaticAssumed,
            WorkingState = WorkingState.AssumedWorking,
            TestsAssumedPassing = true,
        },
    };

    private static WorkItem Item(
        string id,
        EffortCategory category,
        string evidenceId,
        EffortRange hours) => new()
        {
            Id = id,
            Category = category,
            Title = id,
            Scope = $"scope:{id}",
            EvidenceIds = [evidenceId],
            Complexity = ComplexityLevel.Moderate,
            Hours = hours,
            Confidence = 0.75m,
            Reason = "Synthetic diagnostic candidate.",
            Estimator = new EstimatorReference
            {
                Id = "candidate",
                Version = "1.0.0",
                Kind = EstimatorKind.Rule,
            },
            Profiles = [EstimationProfile.Implementation],
            UncertaintyReasons = ["Synthetic candidate uncertainty."],
        };

    private static EffortRange Hours(decimal low, decimal expected, decimal high) => new()
    {
        Low = low,
        Expected = expected,
        High = high,
    };

    private static CalibrationSignedEffortRange ZeroSignedRange { get; } = new()
    {
        Low = 0m,
        Expected = 0m,
        High = 0m,
    };

    private sealed record DiagnosticCase(
        CalibrationRecord Record,
        EstimateReport Candidate);
}
