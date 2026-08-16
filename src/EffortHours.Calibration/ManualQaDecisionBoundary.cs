using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record ManualQaDecisionBoundaryContext
{
    public required string SourceCorpusDigest { get; init; }

    public required string ReviewManifestDigest { get; init; }

    public required IReadOnlyDictionary<string, CalibrationRecord> SourceRecords { get; init; }

    public required IReadOnlyDictionary<string, ManualQaReviewPacket> Packets { get; init; }

    public required IReadOnlyDictionary<string, ManualQaReviewManifestPacket> ManifestEntries { get; init; }
}

internal static class ManualQaDecisionBoundary
{
    public static ManualQaDecisionBoundaryContext Validate(
        CalibrationCorpus sourceCorpus,
        ManualQaReviewPolicy reviewPolicy,
        string reviewPolicyDigest,
        ManualQaReviewManifest reviewManifest,
        IReadOnlyList<ManualQaReviewPacket> packets,
        ManualQaDecisionCompilerPolicy compilerPolicy)
    {
        ArgumentNullException.ThrowIfNull(sourceCorpus);
        ArgumentNullException.ThrowIfNull(reviewPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewPolicyDigest);
        ArgumentNullException.ThrowIfNull(reviewManifest);
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(compilerPolicy);

        List<string> errors = [.. ContractValidation.Validate(sourceCorpus).Select(error =>
            $"Source corpus: {error}")];
        errors.AddRange(ContractValidation.Validate(reviewPolicy).Select(error =>
            $"Review policy: {error}"));
        errors.AddRange(ContractValidation.Validate(reviewManifest).Select(error =>
            $"Review manifest: {error}"));
        errors.AddRange(ContractValidation.Validate(compilerPolicy).Select(error =>
            $"Compiler policy: {error}"));
        foreach (ManualQaReviewPacket packet in packets)
        {
            errors.AddRange(ContractValidation.Validate(packet).Select(error =>
                $"Review packet '{packet.SourceRecordId}': {error}"));
        }

        ValidateCompilerPolicyIdentity(compilerPolicy, errors);
        string sourceCorpusDigest = CalibrationDigest.Compute(sourceCorpus);
        string reviewManifestDigest = CalibrationDigest.Compute(reviewManifest);
        ValidateReferences(
            sourceCorpus,
            sourceCorpusDigest,
            reviewPolicy,
            reviewPolicyDigest,
            reviewManifest,
            reviewManifestDigest,
            compilerPolicy,
            errors);

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        ManualQaReviewPacketSet reproduced = ManualQaReviewAuthoring.Scaffold(
            sourceCorpus,
            reviewPolicy,
            reviewPolicyDigest);
        if (!string.Equals(
                ContractJson.SerializeDocument(reproduced.Manifest),
                ContractJson.SerializeDocument(reviewManifest),
                StringComparison.Ordinal))
        {
            errors.Add("Review manifest is not the exact deterministic projection of the source corpus.");
        }

        Dictionary<string, ManualQaReviewPacket> suppliedPackets = IndexPackets(packets, errors);
        Dictionary<string, ManualQaReviewPacket> expectedPackets = reproduced.Packets.ToDictionary(
            packet => packet.SourceRecordId,
            StringComparer.Ordinal);
        foreach (string missing in expectedPackets.Keys.Except(
                     suppliedPackets.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            errors.Add($"No review packet was supplied for source record '{missing}'.");
        }

        foreach (string unknown in suppliedPackets.Keys.Except(
                     expectedPackets.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            errors.Add($"Unexpected review packet was supplied for source record '{unknown}'.");
        }

        foreach (string recordId in expectedPackets.Keys.Intersect(
                     suppliedPackets.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!string.Equals(
                    ContractJson.SerializeDocument(expectedPackets[recordId]),
                    ContractJson.SerializeDocument(suppliedPackets[recordId]),
                    StringComparison.Ordinal))
            {
                errors.Add($"Review packet '{recordId}' differs from its deterministic projection.");
            }
        }

        ValidateCounts(sourceCorpus, reviewManifest, compilerPolicy, errors);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        return new ManualQaDecisionBoundaryContext
        {
            SourceCorpusDigest = sourceCorpusDigest,
            ReviewManifestDigest = reviewManifestDigest,
            SourceRecords = sourceCorpus.Records.ToDictionary(
                record => record.Id,
                StringComparer.Ordinal),
            Packets = suppliedPackets,
            ManifestEntries = reviewManifest.Packets.ToDictionary(
                packet => packet.SourceRecordId,
                StringComparer.Ordinal),
        };
    }

    private static void ValidateCompilerPolicyIdentity(
        ManualQaDecisionCompilerPolicy policy,
        List<string> errors)
    {
        if (policy.PolicyVersion != ManualQaDecisionVersions.PolicyV1 ||
            policy.Id != ManualQaDecisionAuthoring.PolicyId ||
            policy.CompilerVersion != ManualQaDecisionVersions.CompilerV1 ||
            policy.AuthoringVersion != ManualQaDecisionVersions.AuthoringV1 ||
            policy.LicenseExpression != "MIT" ||
            policy.Maturity != ManualQaDecisionAuthoring.Maturity ||
            policy.ReviewRubric.Id != ManualQaReviewVersions.RubricId ||
            policy.ReviewRubric.Version != ManualQaReviewVersions.RubricV1 ||
            policy.OutputCorpus.Rubric.Id != ManualQaDecisionVersions.OutputRubricId ||
            policy.OutputCorpus.Rubric.Version != ManualQaDecisionVersions.OutputRubricV1 ||
            policy.Partition != CalibrationPartition.Development ||
            policy.Profile != EstimationProfile.Implementation ||
            policy.ReplacedCategory != EffortCategory.ManualValidationDebuggingAndHardening ||
            policy.SourceWorkItemLineageVersion != ManualQaSourceWorkItemLineage.Version)
        {
            errors.Add("Manual-QA decision compiler policy identity or frozen semantic boundary differs.");
        }
    }

    private static void ValidateReferences(
        CalibrationCorpus sourceCorpus,
        string sourceCorpusDigest,
        ManualQaReviewPolicy reviewPolicy,
        string reviewPolicyDigest,
        ManualQaReviewManifest reviewManifest,
        string reviewManifestDigest,
        ManualQaDecisionCompilerPolicy compilerPolicy,
        List<string> errors)
    {
        if (compilerPolicy.SourceCorpus.Id != sourceCorpus.Id ||
            compilerPolicy.SourceCorpus.Version != sourceCorpus.Version ||
            compilerPolicy.SourceCorpus.Digest != sourceCorpusDigest)
        {
            errors.Add("Compiler policy source-corpus reference differs from the supplied corpus.");
        }

        if (compilerPolicy.ReviewPolicy.Id != reviewPolicy.Id ||
            compilerPolicy.ReviewPolicy.Version != reviewPolicy.PolicyVersion ||
            compilerPolicy.ReviewPolicy.Digest != reviewPolicyDigest)
        {
            errors.Add("Compiler policy review-policy reference differs from the supplied policy.");
        }

        if (compilerPolicy.ReviewManifest.Version != reviewManifest.ManifestVersion ||
            compilerPolicy.ReviewManifest.Digest != reviewManifestDigest)
        {
            errors.Add("Compiler policy review-manifest reference differs from the supplied manifest.");
        }

        if (reviewManifest.Policy.Id != reviewPolicy.Id ||
            reviewManifest.Policy.Version != reviewPolicy.PolicyVersion ||
            reviewManifest.Policy.Digest != reviewPolicyDigest ||
            reviewManifest.SourceCorpus.Id != sourceCorpus.Id ||
            reviewManifest.SourceCorpus.Version != sourceCorpus.Version ||
            reviewManifest.SourceCorpus.Digest != sourceCorpusDigest)
        {
            errors.Add("Review manifest does not bind the supplied source corpus and review policy.");
        }

        if (compilerPolicy.ReviewRubric != reviewManifest.Rubric ||
            compilerPolicy.Partition != reviewManifest.Partition ||
            compilerPolicy.BaselineId != reviewPolicy.BaselineId ||
            compilerPolicy.Profile != reviewPolicy.Profile)
        {
            errors.Add("Compiler policy review rubric, partition, profile, or baseline differs.");
        }
    }

    private static Dictionary<string, ManualQaReviewPacket> IndexPackets(
        IReadOnlyList<ManualQaReviewPacket> packets,
        List<string> errors)
    {
        Dictionary<string, ManualQaReviewPacket> result = new(StringComparer.Ordinal);
        foreach (ManualQaReviewPacket packet in packets)
        {
            if (!result.TryAdd(packet.SourceRecordId, packet))
            {
                errors.Add($"Review packet '{packet.SourceRecordId}' is supplied more than once.");
            }
        }

        return result;
    }

    private static void ValidateCounts(
        CalibrationCorpus sourceCorpus,
        ManualQaReviewManifest reviewManifest,
        ManualQaDecisionCompilerPolicy policy,
        List<string> errors)
    {
        int removed = sourceCorpus.Records.Sum(record => record.Targets.Count(target =>
            target.Category == policy.ReplacedCategory));
        int preserved = sourceCorpus.Records.Sum(record => record.Targets.Count(target =>
            target.Category != policy.ReplacedCategory));
        if (sourceCorpus.Records.Count != policy.ExpectedRecordCount ||
            reviewManifest.RecordCount != policy.ExpectedRecordCount ||
            reviewManifest.TargetCount != policy.ExpectedDecisionCount ||
            removed != policy.ExpectedRemovedTargetCount ||
            preserved != policy.ExpectedPreservedTargetCount ||
            preserved + reviewManifest.TargetCount != policy.ExpectedOutputTargetCount)
        {
            errors.Add(
                "Manual-QA decision source counts differ from the frozen compiler policy: " +
                $"records {sourceCorpus.Records.Count}, decisions {reviewManifest.TargetCount}, " +
                $"removed {removed}, preserved {preserved}, output {preserved + reviewManifest.TargetCount}.");
        }
    }
}
