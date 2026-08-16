using System.Reflection;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ContractSchemaCatalog
{
    private const string ResourcePrefix = "EffortHours.Schemas.V1.";

    public static IReadOnlyList<string> Names { get; } =
    [
        SchemaNames.CalibrationAuthoringPacket,
        SchemaNames.CalibrationCorpus,
        SchemaNames.CalibrationCorpusReviewPacket,
        SchemaNames.CalibrationCorpusReviewPlan,
        SchemaNames.CalibrationDiagnostic,
        SchemaNames.CalibrationEvaluation,
        SchemaNames.CalibrationUncertaintyEvaluation,
        SchemaNames.CalibrationUncertaintyFeatures,
        SchemaNames.CalibrationUncertaintyGraphFeatures,
        SchemaNames.CalibrationUncertaintyStructuralFeatures,
        SchemaNames.CalibrationUncertaintyStructuralEvaluation,
        SchemaNames.CalibrationUncertaintySupportPopulation,
        SchemaNames.CalibrationUncertaintySupportProfile,
        SchemaNames.CalibrationUncertaintySupportEvaluation,
        SchemaNames.CalibrationMutationReport,
        SchemaNames.CalibrationMutationSuite,
        SchemaNames.CalibrationReviewPlan,
        SchemaNames.CalibrationValidation,
        SchemaNames.ChangeEstimateExplanation,
        SchemaNames.ChangeEstimateReport,
        SchemaNames.ChangeEvidence,
        SchemaNames.ChangeAuthorPeriodManifest,
        SchemaNames.ChangePortfolioManifest,
        SchemaNames.ChangePortfolioReport,
        SchemaNames.Diagnostic,
        SchemaNames.EstimateExplanation,
        SchemaNames.EstimateReport,
        SchemaNames.EstimateView,
        SchemaNames.HostReviewAdjustment,
        SchemaNames.HostReviewBenchmark,
        SchemaNames.HostReviewMeasurement,
        SchemaNames.HostReviewPacket,
        SchemaNames.HostReviewQueryResult,
        SchemaNames.HostReviewValidation,
        SchemaNames.RateCard,
        SchemaNames.RateCardModel,
        SchemaNames.RepositoryEvidence,
        SchemaNames.RepositoryScanCache,
        SchemaNames.SeedRuleModel,
        SchemaNames.WorkItem,
    ];

    public static string Read(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Names.Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown EffortHours schema name.");
        }

        Assembly assembly = typeof(ContractSchemaCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"Embedded schema '{name}' was not found.");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
