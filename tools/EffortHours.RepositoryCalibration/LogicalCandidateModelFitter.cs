using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateModelFitter
{
    public const string ModelVersion = "repository-logical-capability-model/0.3.0";
    public const string ModelId = "efforthours-public-readiness-logical-capability";
    public const string CandidateId = "logical-capability/0.3.0";
    public const string EstimatorVersion = "candidate-logical-capability/0.3.0+seed-rules/0.4.0";
    public const string FeatureContractVersion = "logical-capability-features/1.2.0";
    public const string BaselineEstimatorVersion = "seed-rules/0.4.0";
    public const decimal MinimumFactor = 0.25m;
    public const decimal MaximumFactor = 3m;
    public const decimal SpecificationComprehensionMaximumFactor = 4m;
    public const decimal SeedAnchorFactor = 0.5m;
    public const decimal SeedAnchorMaximumLogicalHours = 1m;
    public const decimal LowerQuantile = 0.15m;
    public const decimal UpperQuantile = 0.76m;
    public const decimal RetiredUpperQuantile = 0.8m;
    public const int MinimumRangeSamples = 3;

    public static readonly string[] SeedAnchorWorkItemKinds =
        ["dotnet-source-backbone", "javascript-source-backbone"];

    public static async Task RunAsync(
        LogicalCandidateModelOptions options,
        CancellationToken cancellationToken)
    {
        string corpusJson = await File.ReadAllTextAsync(options.CorpusPath, cancellationToken)
            .ConfigureAwait(false);
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs =
            await LogicalCandidateDevelopmentLoader.LoadAsync(
                options.OutputDirectory,
                cancellationToken).ConfigureAwait(false);
        LogicalCandidateModel model = Fit(
            corpus,
            JsonArtifactDigest.Compute(corpusJson),
            inputs,
            options.SourceCommit);
        Directory.CreateDirectory(Path.GetDirectoryName(options.ModelPath)!);
        await File.WriteAllTextAsync(
            options.ModelPath,
            ContractJson.SerializeDocument(model),
            cancellationToken).ConfigureAwait(false);
    }

    internal static LogicalCandidateModel Fit(
        CalibrationCorpus corpus,
        string corpusDigest,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        string sourceCommit)
    {
        ValidateBoundary(corpus, inputs);
        Dictionary<CandidateKey, LogicalCandidateDevelopmentInput> inputIndex = inputs.ToDictionary(
            input => Key(input.Estimate));
        List<TrainingRow> rows = [];
        List<LogicalCandidateTrainingSource> sources = [];
        foreach (CalibrationRecord record in corpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            LogicalCandidateDevelopmentInput input = inputIndex[new CandidateKey(
                record.Repository.SourceDigest,
                record.Profile,
                record.BaselineId)];
            if (input.EstimateDigest != record.SourceEstimateDigest)
            {
                throw new InvalidDataException(
                    $"Development estimate digest differs for '{record.Id}'.");
            }

            IReadOnlyList<LogicalCapabilityGroup> capabilities = LogicalCandidateScorer.Build(
                input.Estimate,
                input.Evidence);
            Dictionary<string, LogicalCapabilityGroup> itemIndex = capabilities
                .SelectMany(capability => capability.Items.Select(item => (item.Id, Capability: capability)))
                .ToDictionary(pair => pair.Id, pair => pair.Capability, StringComparer.Ordinal);
            foreach (CalibrationTarget target in record.Targets)
            {
                LogicalCapabilityGroup[] matched =
                [
                    .. target.SourceWorkItemIds.Select(id => itemIndex[id])
                        .DistinctBy(capability => capability.Id, StringComparer.Ordinal),
                ];
                if (matched.Length != 1)
                {
                    throw new InvalidDataException(
                        $"Development target '{target.Id}' does not map to one logical capability.");
                }

                LogicalCapabilityGroup capability = matched[0];
                rows.Add(new TrainingRow(
                    capability.Kind,
                    capability.LogicalSizeBand,
                    capability.LogicalExpected,
                    capability.SeedExpected,
                    target.Hours.Expected));
            }

            sources.Add(new LogicalCandidateTrainingSource
            {
                RecordId = record.Id,
                RepositoryId = record.Repository.Id,
                SourceDigest = record.Repository.SourceDigest,
                EstimateDigest = input.EstimateDigest,
                EvidenceDigest = input.EvidenceDigest,
            });
        }

        IReadOnlyList<LogicalCandidatePointFactor> pointFactors = FitPointFactors(rows);
        Dictionary<PointKey, decimal> pointIndex = pointFactors.ToDictionary(
            factor => new PointKey(factor.WorkItemKind, factor.LogicalSizeBand),
            factor => factor.Factor);
        TrainingPrediction[] predictions =
        [
            .. rows.Select(row => new TrainingPrediction(
                row.Kind,
                AnchoredExpected(row) +
                row.LogicalExpected * pointIndex[new PointKey(row.Kind, row.LogicalSizeBand)],
                row.ReviewedExpected)),
        ];
        IReadOnlyList<LogicalCandidateRangeFactor> rangeFactors = FitRangeFactors(predictions);
        return new LogicalCandidateModel
        {
            ModelVersion = ModelVersion,
            Id = ModelId,
            CandidateId = CandidateId,
            EstimatorVersion = EstimatorVersion,
            BaselineEstimatorVersion = BaselineEstimatorVersion,
            FeatureContractVersion = FeatureContractVersion,
            EffectiveDate = "2026-08-13",
            LicenseExpression = "MIT",
            Training = new LogicalCandidateTraining
            {
                CorpusId = corpus.Id,
                CorpusVersion = corpus.Version,
                CorpusDigest = corpusDigest,
                ImplementationCommit = sourceCommit,
                RepositoryCount = corpus.Records.Count,
                TargetCount = corpus.Records.Sum(record => record.Targets.Count),
                Sources = sources,
            },
            Point = new LogicalCandidatePointModel
            {
                ScorerVersion = LogicalCandidateScorer.Version,
                Features =
                [
                    "work-item-kind",
                    "exact-content-normalized-evidence-size-band",
                    "seed-normalized-capability-anchor",
                ],
                SizeBands = LogicalCandidateScorer.SizeBands,
                MinimumFactor = MinimumFactor,
                MaximumFactor = MaximumFactor,
                MaximumFactorOverrides =
                [
                    new LogicalCandidatePointFactorCeiling
                    {
                        WorkItemKind = "specification-comprehension",
                        MaximumFactor = SpecificationComprehensionMaximumFactor,
                    },
                ],
                SeedAnchorFactor = SeedAnchorFactor,
                SeedAnchorMaximumLogicalHours = SeedAnchorMaximumLogicalHours,
                SeedAnchorWorkItemKinds = SeedAnchorWorkItemKinds,
                UnknownGroupBehavior = "use the complete seed capability without candidate adjustment",
                Factors = pointFactors,
            },
            Range = new LogicalCandidateRangeModel
            {
                Features = ["work-item-kind", "candidate-expected-size-band"],
                LowerQuantile = LowerQuantile,
                UpperQuantile = UpperQuantile,
                MinimumExactGroupSamples = MinimumRangeSamples,
                SparseGroupFallback = "same-kind residuals, then all positive development residuals",
                UnknownGroupBehavior = "use the complete seed capability range",
                Factors = rangeFactors,
            },
            Limitations =
            [
                "Development-only logical weak supervision from one disclosed host-AI teacher.",
                "No validation or test labels, empirical production observations, or independent correction were used.",
                "Exact-content evidence is normalized before scoring; API and integration boundaries have explicit minimum logical points; source backbones at or below one logical hour retain a fixed 0.50 seed contribution before residual fitting.",
                "Development-empty size bands use the nearest same-kind point factor; unknown work-item kinds retain the complete seed capability.",
                "Specification-comprehension groups use a bounded 4.00 factor ceiling; every other group retains the 3.00 ceiling.",
                "The table is not admitted for product use and must fall back to seed-rules/0.4.0 outside its exact groups.",
            ],
        };
    }

    private static IReadOnlyList<LogicalCandidatePointFactor> FitPointFactors(
        IReadOnlyList<TrainingRow> rows)
    {
        LogicalCandidatePointFactor[] exact =
        [
            .. rows.GroupBy(row => new PointKey(row.Kind, row.LogicalSizeBand))
            .OrderBy(group => group.Key.Kind, StringComparer.Ordinal)
            .ThenBy(group => BandOrder(group.Key.SizeBand))
            .Select(group =>
            {
                decimal logical = group.Sum(row => row.LogicalExpected);
                decimal reviewed = group.Sum(row => row.ReviewedExpected);
                decimal seed = group.Sum(row => row.SeedExpected);
                decimal residual = decimal.Max(
                    0m,
                    reviewed - group.Sum(AnchoredExpected));
                decimal factor = logical == 0m ? 1m : residual / logical;
                return new LogicalCandidatePointFactor
                {
                    WorkItemKind = group.Key.Kind,
                    LogicalSizeBand = group.Key.SizeBand,
                    SampleCount = group.Count(),
                    LogicalExpectedHours = logical,
                    SeedExpectedHours = seed,
                    ReviewedExpectedHours = reviewed,
                    Factor = Round8(decimal.Clamp(
                        factor,
                        MinimumFactor,
                        MaximumFactorFor(group.Key.Kind))),
                    SampleSource = "exact-kind-and-size",
                };
            }),
        ];
        return
        [
            .. exact.GroupBy(factor => factor.WorkItemKind, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .SelectMany(group => ExpandPointBands([.. group])),
        ];
    }

    private static IEnumerable<LogicalCandidatePointFactor> ExpandPointBands(
        IReadOnlyList<LogicalCandidatePointFactor> exact)
    {
        foreach (string band in SizeBandNames())
        {
            LogicalCandidatePointFactor? observed = exact.FirstOrDefault(factor =>
                factor.LogicalSizeBand == band);
            if (observed is not null)
            {
                yield return observed;
                continue;
            }

            LogicalCandidatePointFactor nearest = exact
                .OrderBy(factor => Math.Abs(BandOrder(factor.LogicalSizeBand) - BandOrder(band)))
                .ThenBy(factor => BandOrder(factor.LogicalSizeBand))
                .First();
            yield return nearest with
            {
                LogicalSizeBand = band,
                SampleCount = 0,
                LogicalExpectedHours = 0m,
                SeedExpectedHours = 0m,
                ReviewedExpectedHours = 0m,
                SampleSource = $"nearest-same-kind:{nearest.LogicalSizeBand}",
            };
        }
    }

    private static decimal MaximumFactorFor(string workItemKind) =>
        workItemKind == "specification-comprehension"
            ? SpecificationComprehensionMaximumFactor
            : MaximumFactor;

    private static decimal AnchoredExpected(TrainingRow row) =>
        row.LogicalExpected <= SeedAnchorMaximumLogicalHours &&
        SeedAnchorWorkItemKinds.Contains(row.Kind, StringComparer.Ordinal)
            ? row.SeedExpected * SeedAnchorFactor
            : 0m;

    private static IReadOnlyList<LogicalCandidateRangeFactor> FitRangeFactors(
        IReadOnlyList<TrainingPrediction> rows)
    {
        TrainingPrediction[] usable =
        [.. rows.Where(row => row.CandidateExpected > 0m && row.ReviewedExpected > 0m)];
        Dictionary<RangeKey, TrainingPrediction[]> exact = usable
            .GroupBy(row => new RangeKey(row.Kind, LogicalCandidateScorer.SizeBand(row.CandidateExpected)))
            .ToDictionary(group => group.Key, group => group.ToArray());
        Dictionary<string, TrainingPrediction[]> byKind = usable
            .GroupBy(row => row.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        decimal[] global = [.. usable.Select(Ratio).Order()];
        return
        [
            .. rows.Select(row => new RangeKey(
                    row.Kind,
                    LogicalCandidateScorer.SizeBand(row.CandidateExpected)))
                .Distinct()
                .OrderBy(key => key.Kind, StringComparer.Ordinal)
                .ThenBy(key => BandOrder(key.SizeBand))
                .Select(key =>
                {
                    TrainingPrediction[] exactRows = exact.GetValueOrDefault(key, []);
                    TrainingPrediction[] kindRows = byKind.GetValueOrDefault(key.Kind, []);
                    decimal[] samples;
                    string source;
                    if (exactRows.Length >= MinimumRangeSamples)
                    {
                        samples = [.. exactRows.Select(Ratio).Order()];
                        source = "exact-kind-and-size";
                    }
                    else if (kindRows.Length >= MinimumRangeSamples)
                    {
                        samples = [.. kindRows.Select(Ratio).Order()];
                        source = "same-kind";
                    }
                    else
                    {
                        samples = global;
                        source = "all-positive-development-targets";
                    }

                    return new LogicalCandidateRangeFactor
                    {
                        WorkItemKind = key.Kind,
                        ExpectedSizeBand = key.SizeBand,
                        SampleSource = source,
                        SampleCount = samples.Length,
                        LowFactor = RoundDown8(
                            decimal.Max(0m, decimal.Min(1m, Quantile(samples, LowerQuantile)))),
                        HighFactor = RoundUp8(decimal.Max(1m, Quantile(samples, UpperQuantile))),
                    };
                }),
        ];
    }

    private static void ValidateBoundary(
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs)
    {
        if (corpus.Id != "efforthours-public-readiness-development" ||
            corpus.Version != "0.3.0" ||
            corpus.Records.Count != 15 ||
            corpus.Records.Sum(record => record.Targets.Count) != 2030 ||
            inputs.Count != 15 ||
            corpus.Records.Any(record =>
                record.Partition != CalibrationPartition.Development ||
                record.SourceEstimatorVersion != BaselineEstimatorVersion))
        {
            throw new InvalidDataException(
                "Logical candidate fitting requires the exact public-readiness 0.3.0 development boundary.");
        }
    }

    private static CandidateKey Key(EstimateReport estimate) => new(
        estimate.Repository.SourceDigest
            ?? throw new InvalidDataException("A development estimate source digest is missing."),
        estimate.Profile,
        estimate.Baseline.Id);

    private static decimal Ratio(TrainingPrediction row) =>
        row.ReviewedExpected / row.CandidateExpected;

    private static decimal Quantile(decimal[] ordered, decimal probability)
    {
        int index = decimal.ToInt32(decimal.Ceiling(ordered.Length * probability)) - 1;
        return ordered[Math.Max(0, index)];
    }

    private static int BandOrder(string band) => band switch
    {
        "xs" => 0,
        "s" => 1,
        "m" => 2,
        "l" => 3,
        "xl" => 4,
        "2xl" => 5,
        "3xl" => 6,
        "4xl" => 7,
        _ => 8,
    };

    private static IEnumerable<string> SizeBandNames() =>
        LogicalCandidateScorer.SizeBands.Select(band => band.Split(':', 2)[0]);

    private static decimal Round8(decimal value) =>
        decimal.Round(value, 8, MidpointRounding.AwayFromZero);

    internal static decimal RoundDown8(decimal value) =>
        decimal.Floor(value * 100_000_000m) / 100_000_000m;

    internal static decimal RoundUp8(decimal value) =>
        decimal.Ceiling(value * 100_000_000m) / 100_000_000m;

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

    private readonly record struct PointKey(string Kind, string SizeBand);

    private readonly record struct RangeKey(string Kind, string SizeBand);

    private sealed record TrainingRow(
        string Kind,
        string LogicalSizeBand,
        decimal LogicalExpected,
        decimal SeedExpected,
        decimal ReviewedExpected);

    private sealed record TrainingPrediction(
        string Kind,
        decimal CandidateExpected,
        decimal ReviewedExpected);
}
