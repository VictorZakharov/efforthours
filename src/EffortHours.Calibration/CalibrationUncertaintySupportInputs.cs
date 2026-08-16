using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintySupportInputs
{
    public static IReadOnlyList<CalibrationUncertaintySupportInput> Match(
        CalibrationUncertaintySupportPopulation population,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> reports)
    {
        List<string> errors = [.. ContractValidation.Validate(population)];
        Dictionary<string, CalibrationUncertaintyFeatureReport> reportsBySource =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureReport report in reports)
        {
            errors.AddRange(ContractValidation.Validate(report).Select(error =>
                $"Feature report '{report.RepositorySourceDigest}': {error}"));
            if (report.FeatureContractDigest != CalibrationDigest.Compute(report.FeatureContract))
            {
                errors.Add(
                    $"Feature report '{report.RepositorySourceDigest}' has a feature-contract " +
                    "digest mismatch.");
            }

            if (!reportsBySource.TryAdd(report.RepositorySourceDigest, report))
            {
                errors.Add(
                    $"More than one feature report uses repository source digest " +
                    $"'{report.RepositorySourceDigest}'.");
            }
        }

        if (reports.Count != population.Repositories.Count)
        {
            errors.Add(
                $"Support population contains {population.Repositories.Count} records but " +
                $"{reports.Count} feature reports were supplied.");
        }

        List<CalibrationUncertaintySupportInput> matches = [];
        HashSet<string> usedSources = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintySupportPopulationRepository repository in
                 population.Repositories.OrderBy(item => item.RecordId, StringComparer.Ordinal))
        {
            if (!reportsBySource.TryGetValue(
                    repository.SourceDigest,
                    out CalibrationUncertaintyFeatureReport? report))
            {
                errors.Add(
                    $"No feature report matches population record '{repository.RecordId}' with " +
                    $"source digest '{repository.SourceDigest}'.");
                continue;
            }

            usedSources.Add(repository.SourceDigest);
            if (report.Profile != population.Profile ||
                !string.Equals(report.BaselineId, population.BaselineId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Feature report for record '{repository.RecordId}' does not match profile " +
                    $"'{population.Profile}' and baseline '{population.BaselineId}'.");
            }

            if (report.FeatureContract.Version != population.FeatureContractVersion ||
                report.FeatureContractDigest != population.FeatureContractDigest)
            {
                errors.Add(
                    $"Feature report for record '{repository.RecordId}' does not match the " +
                    "population feature contract.");
            }

            matches.Add(new CalibrationUncertaintySupportInput
            {
                Population = repository,
                Report = report,
            });
        }

        foreach (string unused in reportsBySource.Keys.Except(usedSources, StringComparer.Ordinal))
        {
            errors.Add($"Feature report source digest '{unused}' is not in the support population.");
        }

        int workItemCount = matches.Sum(match => match.Report.WorkItems.Count);
        if (workItemCount == 0)
        {
            errors.Add("Uncertainty support profiling requires at least one work item.");
        }
        else if (workItemCount > CalibrationUncertaintySupportProfiler.MaximumWorkItemCount)
        {
            errors.Add(
                $"Uncertainty support population contains {workItemCount} work items; the v1 " +
                $"bound is {CalibrationUncertaintySupportProfiler.MaximumWorkItemCount}.");
        }

        int populatedFamilies = matches
            .Where(match => match.Report.WorkItems.Count > 0)
            .Select(match => match.Population.RepositoryId)
            .Distinct(StringComparer.Ordinal).Count();
        if (populatedFamilies < 2)
        {
            errors.Add(
                "At least two repository families must contain work items for cross-repository " +
                "OOD comparison.");
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors.Distinct(StringComparer.Ordinal));
        }

        return matches;
    }
}
