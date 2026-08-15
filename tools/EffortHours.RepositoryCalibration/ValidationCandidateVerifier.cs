using System.Text.Json;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationCandidateVerifier
{
    private const string ExpectedCandidateId = "logical-capability/0.3.0";
    private const string ExpectedEstimatorVersion =
        "candidate-logical-capability/0.3.0+seed-rules/0.4.0";
    private const string ExpectedModelDigest =
        "sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea";

    public static async Task ValidateAsync(
        JsonElement manifest,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        if (String(manifest, "manifestVersion") != "repository-candidate-manifest/1.0.0" ||
            String(manifest, "id") != "efforthours-public-readiness-candidate-freeze" ||
            String(manifest, "version") != "1.2.0" ||
            String(manifest, "policyVersion") != "repository-model-admission/1.0.0" ||
            String(manifest, "status") != "frozen-before-validation")
        {
            throw new InvalidDataException("Candidate manifest identity is not the frozen validation boundary.");
        }

        JsonElement attempt = manifest.GetProperty("attempt");
        JsonElement[] challengers = [.. manifest.GetProperty("challengers").EnumerateArray()];
        JsonElement challenger = challengers.SingleOrDefault();
        if (String(attempt, "baselineEstimatorVersion") != "seed-rules/0.4.0" ||
            attempt.GetProperty("challengerCount").GetInt32() != 1 ||
            attempt.GetProperty("maximumChallengerCount").GetInt32() != 4 ||
            challengers.Length != 1 ||
            String(challenger, "id") != ExpectedCandidateId ||
            String(challenger, "candidateKind") != "transparent-rule" ||
            String(challenger, "implementationKind") != "transparent-fitted-table" ||
            String(challenger, "estimatorVersion") != ExpectedEstimatorVersion ||
            String(challenger, "fallbackEstimatorVersion") != "seed-rules/0.4.0" ||
            String(challenger.GetProperty("artifact"), "digest") != ExpectedModelDigest)
        {
            throw new InvalidDataException("Frozen candidate set or challenger identity changed.");
        }

        JsonElement holdouts = manifest.GetProperty("holdouts");
        JsonElement decision = manifest.GetProperty("decision");
        if (!String(holdouts, "validation").StartsWith(
                "authorized-but-unopened",
                StringComparison.Ordinal) ||
            !String(holdouts, "test").StartsWith("withheld-not-run", StringComparison.Ordinal) ||
            holdouts.GetProperty("candidateOutputsGenerated").GetBoolean() ||
            holdouts.GetProperty("labelsAuthored").GetBoolean() ||
            !decision.GetProperty("candidateManifestFrozen").GetBoolean() ||
            !decision.GetProperty("validationAuthorized").GetBoolean() ||
            decision.GetProperty("testAuthorized").GetBoolean() ||
            decision.GetProperty("candidateAdmitted").GetBoolean() ||
            String(decision, "shippedEstimatorVersion") != "seed-rules/0.4.0")
        {
            throw new InvalidDataException("Holdout authorization changed or a holdout was opened early.");
        }

        JsonElement boundary = manifest.GetProperty("developmentBoundary");
        if (boundary.GetProperty("recordIds").GetArrayLength() != 15 ||
            boundary.GetProperty("excludedFamilies").GetProperty("validation").GetArrayLength() != 9 ||
            boundary.GetProperty("excludedFamilies").GetProperty("test").GetArrayLength() != 9 ||
            boundary.GetProperty("contaminatedRecords").GetArrayLength() != 0)
        {
            throw new InvalidDataException("Development, exclusion, or contamination boundary changed.");
        }

        JsonElement observed = manifest.GetProperty("resourceBudget")
            .GetProperty("measurement").GetProperty("observed");
        if (observed.GetProperty("measuredGatesPassed").GetInt32() != 7 ||
            observed.GetProperty("measuredGatesFailed").GetInt32() != 0 ||
            observed.GetProperty("totalOperationalGatesPassed").GetInt32() != 12 ||
            observed.GetProperty("totalOperationalGatesFailed").GetInt32() != 0 ||
            !observed.GetProperty("rawCanonicalBytesIdentical").GetBoolean())
        {
            throw new InvalidDataException("Measured resource admission boundary is not a complete pass.");
        }

        ValidateSelectionRule(manifest.GetProperty("validationSelectionRule"));
        List<JsonElement> references =
        [
            .. challenger.GetProperty("buildInputs").EnumerateArray(),
            challenger.GetProperty("artifact"),
            boundary.GetProperty("numericalPreflight"),
            boundary.GetProperty("developmentOperationalPreflight"),
            boundary.GetProperty("measuredOperationalReport"),
            boundary.GetProperty("aggregateOperationalPreflight"),
            boundary.GetProperty("publicMutationSuite"),
            boundary.GetProperty("publicMutationReport"),
            .. manifest.GetProperty("resourceBudget").GetProperty("measurement")
                .GetProperty("platformArtifacts").EnumerateArray(),
        ];
        foreach (JsonElement reference in references)
        {
            string path = ValidationBoundaryVerifier.RequireContainedPath(
                repositoryRoot,
                String(reference, "path"));
            await ValidationBoundaryVerifier.RequireDigestAsync(
                    path,
                    String(reference, "digest"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void ValidateSelectionRule(JsonElement rule)
    {
        JsonElement learned = rule.GetProperty("learnedModelMinimumImprovement");
        JsonElement[] ties = [.. rule.GetProperty("tieBreakers").EnumerateArray()];
        if (String(rule, "primaryMetric") != "repository-total expected WAPE" ||
            String(rule, "direction") != "lowest" ||
            rule.GetProperty("absoluteWapeTieTolerance").GetDecimal() != 0.01m ||
            ties.Length != 4 ||
            !ties.Select(tie => tie.GetProperty("ordinal").GetInt32())
                .SequenceEqual([1, 2, 3, 4]) ||
            learned.GetProperty("absoluteRepositoryExpectedWape").GetDecimal() != 0.02m ||
            learned.GetProperty("relativeRepositoryExpectedWape").GetDecimal() != 0.1m)
        {
            throw new InvalidDataException("Validation selection rule changed after candidate freeze.");
        }
    }

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"Manifest property '{property}' is null.");
}
