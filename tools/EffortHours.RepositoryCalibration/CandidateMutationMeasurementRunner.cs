using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMutationMeasurementRunner
{
    private static readonly HashSet<string> LegacyDotNetFixtureAliases = new(StringComparer.Ordinal)
    {
        "dotnet-base",
        "dotnet-formatting",
        "dotnet-duplicate",
        "dotnet-generated",
        "dotnet-api",
        "dotnet-tests",
        "dotnet-documentation",
        "dotnet-integration",
    };

    public static async Task<CandidateMutationMeasurement> RunAsync(
        CandidateMeasurementOptions options,
        LogicalCandidateModel model,
        CancellationToken cancellationToken)
    {
        string suiteJson = await File.ReadAllTextAsync(
                options.MutationSuitePath!,
                cancellationToken)
            .ConfigureAwait(false);
        CalibrationMutationSuite suite = ContractJson.Deserialize<CalibrationMutationSuite>(suiteJson);
        if (suite.Id != "efforthours-public-synthetic-mutations" || suite.Version != "0.8.0")
        {
            throw new InvalidDataException(
                "Candidate measurement requires exact public aggregate mutation suite 0.8.0.");
        }

        string work = Path.Combine(options.WorkspacePath, "mutations");
        Directory.CreateDirectory(work);
        List<EstimateReport> candidates = [];
        int applied = 0;
        foreach (CalibrationMutationCase mutationCase in suite.Cases.OrderBy(
                     item => item.Id,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fixture = FixturePath(options.MutationFixturesPath!, mutationCase.Id);
            string evidencePath = Path.Combine(work, $"{mutationCase.Id}.evidence.json");
            string estimatePath = Path.Combine(work, $"{mutationCase.Id}.seed.json");
            await ExternalProcess.RunAsync(
                "dotnet",
                [options.CliPath!, "scan", fixture, "--output", evidencePath],
                cancellationToken).ConfigureAwait(false);
            await ExternalProcess.RunAsync(
                "dotnet",
                [
                    options.CliPath!, "estimate", evidencePath, "--profile", "implementation",
                    "--no-rate", "--output", estimatePath,
                ],
                cancellationToken).ConfigureAwait(false);

            string evidenceJson = await File.ReadAllTextAsync(evidencePath, cancellationToken)
                .ConfigureAwait(false);
            string estimateJson = await File.ReadAllTextAsync(estimatePath, cancellationToken)
                .ConfigureAwait(false);
            RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(evidenceJson);
            EstimateReport seed = ContractJson.Deserialize<EstimateReport>(estimateJson);
            if (seed.EstimatorVersion != model.BaselineEstimatorVersion ||
                seed.Repository.SourceDigest != mutationCase.SourceDigest)
            {
                throw new InvalidDataException(
                    $"Mutation case '{mutationCase.Id}' does not reproduce its frozen source identity.");
            }

            string stratum = PrimaryStratum(mutationCase.Id);
            if (stratum is "dotnet" or "javascript-typescript" or
                "mixed-dotnet-javascript-typescript")
            {
                applied++;
            }

            candidates.Add(LogicalCandidateProjectionRunner.Project(
                seed,
                evidence,
                model,
                stratum,
                cancellationToken).Estimate);
        }

        CalibrationMutationReport report = CalibrationMutationEvaluator.Evaluate(suite, candidates);
        string reportJson = ContractJson.Serialize(report) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(options.MutationReportPath!)!);
        await File.WriteAllTextAsync(
            options.MutationReportPath!,
            reportJson,
            cancellationToken).ConfigureAwait(false);
        return new CandidateMutationMeasurement
        {
            SuiteId = suite.Id,
            SuiteVersion = suite.Version,
            SuiteDigest = JsonArtifactDigest.Compute(suiteJson),
            ReportDigest = JsonArtifactDigest.Compute(reportJson),
            CaseCount = report.CaseCount,
            CandidateAppliedCaseCount = applied,
            SeedFallbackCaseCount = report.CaseCount - applied,
            AssertionCount = report.AssertionCount,
            PassedCount = report.PassedCount,
            FailedCount = report.FailedCount,
            Passed = report.AllPassed,
        };
    }

    private static string FixturePath(string root, string caseId)
    {
        string fixtureId = LegacyDotNetFixtureAliases.Contains(caseId)
            ? caseId["dotnet-".Length..]
            : caseId;
        string path = Path.Combine(root, fixtureId);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"Mutation fixture '{caseId}' was not found at '{path}'.");
        }

        return path;
    }

    private static string PrimaryStratum(string caseId)
    {
        if (caseId.StartsWith("dotnet-", StringComparison.Ordinal))
        {
            return "dotnet";
        }

        if (caseId.StartsWith("javascript-", StringComparison.Ordinal) ||
            caseId.StartsWith("typescript-", StringComparison.Ordinal) ||
            caseId.StartsWith("frontend-", StringComparison.Ordinal))
        {
            return "javascript-typescript";
        }

        return caseId.StartsWith("mixed-", StringComparison.Ordinal)
            ? "mixed-dotnet-javascript-typescript"
            : $"outside-policy:{caseId.Split('-', 2)[0]}";
    }
}
