using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public sealed record ManualQaReviewPacketSet
{
    public IReadOnlyList<ManualQaReviewPacket> Packets { get; init; } = [];

    public required ManualQaReviewManifest Manifest { get; init; }
}

public static class ManualQaReviewAuthoring
{
    public const string PolicyId = "efforthours-manual-qa-development-review";
    public const string Maturity = "development-only-candidate-blind-label-correction";
    public const string Warning =
        "UNREVIEWED CANDIDATE-BLIND MANUAL-QA PACKET: estimate manual validation, " +
        "debugging, and hardening independently. Do not consult seed or candidate hours, " +
        "category totals, repository totals, ratio formulas, or prior QA judgments.";

    public static ManualQaReviewPacketSet Scaffold(
        CalibrationCorpus corpus,
        ManualQaReviewPolicy policy,
        string policyDigest)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyDigest);

        List<string> errors = [.. ContractValidation.Validate(corpus).Select(error =>
            $"Source corpus: {error}")];
        errors.AddRange(ContractValidation.Validate(policy).Select(error => $"Policy: {error}"));
        ValidatePolicyIdentity(policy, errors);
        ValidateSourceBoundary(corpus, policy, errors);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        CalibrationCorpusReference sourceCorpus = new()
        {
            Id = corpus.Id,
            Version = corpus.Version,
            Digest = CalibrationDigest.Compute(corpus),
        };
        ManualQaReviewPolicyReference policyReference = new()
        {
            Id = policy.Id,
            Version = policy.PolicyVersion,
            Digest = policyDigest,
        };

        ManualQaReviewPacket[] packets =
        [
            .. corpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal)
                .Select(record => CreatePacket(record, policy, policyReference, sourceCorpus)),
        ];
        int targetCount = packets.Sum(packet => packet.Targets.Count);
        if (targetCount != policy.ExpectedTargetCount)
        {
            throw new CalibrationEvaluationException(
            [
                $"Manual-QA review policy expects {policy.ExpectedTargetCount} targets, but " +
                $"the source corpus projects {targetCount}.",
            ]);
        }

        ManualQaReviewManifest manifest = new()
        {
            ManifestVersion = ManualQaReviewVersions.ManifestV1,
            AuthoringVersion = ManualQaReviewVersions.AuthoringV1,
            Policy = policyReference,
            SourceCorpus = sourceCorpus,
            Rubric = policy.Rubric,
            CandidateVisibility = CalibrationCandidateVisibility.Blind,
            Partition = CalibrationPartition.Development,
            RecordCount = packets.Length,
            TargetCount = targetCount,
            Packets = [.. packets.Select(CreateManifestPacket)],
            Instructions =
            [
                "Verify every packet digest before review and keep the complete packet set immutable.",
                "Freeze a separate complete decision/compiler contract before authoring any target hours.",
                "Do not treat packet generation as reviewed labels, model evaluation, or admission evidence.",
            ],
        };
        IReadOnlyList<string> manifestErrors = ContractValidation.Validate(manifest);
        if (manifestErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Manual-QA review authoring produced an invalid manifest: " +
                string.Join("; ", manifestErrors));
        }

        return new ManualQaReviewPacketSet
        {
            Packets = packets,
            Manifest = manifest,
        };
    }

    private static void ValidatePolicyIdentity(
        ManualQaReviewPolicy policy,
        List<string> errors)
    {
        if (policy.PolicyVersion != ManualQaReviewVersions.PolicyV1 ||
            policy.Id != PolicyId ||
            policy.AuthoringVersion != ManualQaReviewVersions.AuthoringV1 ||
            policy.LicenseExpression != "MIT" ||
            policy.Maturity != Maturity ||
            policy.Rubric.Id != ManualQaReviewVersions.RubricId ||
            policy.Rubric.Version != ManualQaReviewVersions.RubricV1 ||
            policy.CandidateVisibility != CalibrationCandidateVisibility.Blind ||
            !policy.EligibleCategories.SequenceEqual(EligibleCodingEffortVersions.Categories))
        {
            errors.Add("Manual-QA review policy identity or frozen category boundary differs.");
        }
    }

    private static void ValidateSourceBoundary(
        CalibrationCorpus corpus,
        ManualQaReviewPolicy policy,
        List<string> errors)
    {
        string corpusDigest = CalibrationDigest.Compute(corpus);
        if (policy.SourceCorpus.Id != corpus.Id ||
            policy.SourceCorpus.Version != corpus.Version ||
            policy.SourceCorpus.Digest != corpusDigest)
        {
            errors.Add(
                $"Manual-QA review policy expects source corpus '{policy.SourceCorpus.Id}/" +
                $"{policy.SourceCorpus.Version}' at '{policy.SourceCorpus.Digest}', but received " +
                $"'{corpus.Id}/{corpus.Version}' at '{corpusDigest}'.");
        }

        if (corpus.Records.Count != policy.ExpectedRecordCount)
        {
            errors.Add(
                $"Manual-QA review policy expects {policy.ExpectedRecordCount} records, but " +
                $"the source corpus contains {corpus.Records.Count}.");
        }

        foreach (CalibrationRecord record in corpus.Records)
        {
            if (record.Change is not null ||
                record.Partition != policy.Partition ||
                record.Profile != policy.Profile ||
                record.BaselineId != policy.BaselineId)
            {
                errors.Add(
                    $"Source record '{record.Id}' is outside the frozen repository development boundary.");
            }

            if (record.Source.DataClassification !=
                    CalibrationDataClassification.PublicRedistributable ||
                !record.Source.RedistributionAllowed)
            {
                errors.Add($"Source record '{record.Id}' is not public-redistributable.");
            }

            HashSet<string> sourceWorkItemIds = new(StringComparer.Ordinal);
            foreach (CalibrationTarget target in record.Targets.Where(target =>
                         policy.EligibleCategories.Contains(target.Category)))
            {
                foreach (string sourceWorkItemId in target.SourceWorkItemIds)
                {
                    if (!sourceWorkItemIds.Add(sourceWorkItemId))
                    {
                        errors.Add(
                            $"Eligible source work item '{sourceWorkItemId}' appears in more than " +
                            "one manual-QA review target.");
                    }
                }
            }
        }
    }

    private static ManualQaReviewPacket CreatePacket(
        CalibrationRecord record,
        ManualQaReviewPolicy policy,
        ManualQaReviewPolicyReference policyReference,
        CalibrationCorpusReference sourceCorpus)
    {
        ManualQaReviewPacket packet = new()
        {
            AuthoringVersion = ManualQaReviewVersions.AuthoringV1,
            Status = CalibrationAuthoringStatus.Unreviewed,
            Warning = Warning,
            Policy = policyReference,
            SourceCorpus = sourceCorpus,
            Rubric = policy.Rubric,
            CandidateVisibility = CalibrationCandidateVisibility.Blind,
            SourceRecordId = record.Id,
            Repository = record.Repository,
            Source = new ManualQaReviewSourceReference
            {
                DataClassification = record.Source.DataClassification,
                SourceReference = record.Source.SourceReference,
                Revision = record.Source.Revision,
                LicenseExpression = record.Source.LicenseExpression,
                RedistributionAllowed = record.Source.RedistributionAllowed,
            },
            Profile = record.Profile,
            BaselineId = record.BaselineId,
            Partition = record.Partition,
            Targets =
            [
                .. record.Targets.Where(target => policy.EligibleCategories.Contains(target.Category))
                    .OrderBy(target => target.Scope, StringComparer.Ordinal)
                    .ThenBy(target => target.Category)
                    .ThenBy(target => target.Title, StringComparer.Ordinal)
                    .ThenBy(target => target.Id, StringComparer.Ordinal)
                    .Select(target => CreateTarget(record.Id, target)),
            ],
            Instructions =
            [
                "Use the manual-qa-work-item/1.0.0 rubric and the immutable public source only.",
                "Estimate the manual validation, debugging, and hardening needed after recreating each represented coding responsibility.",
                "Review all targets sharing overlapGroupId together; assign shared validation once and make exclusions explicit.",
                "Keep automated-test authoring separate, while including the bounded work needed to run, inspect, and debug represented tests.",
                "Do not infer actual labor, use Git history or activity, or force a percentage or preferred repository total.",
                "Use exact 0/0/0 only for a wholly excluded or duplicate responsibility and explain the exclusion.",
                "Keep expected targets normally between 0.5 and 8 hours; explain any cohesive size exception.",
            ],
        };
        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Manual-QA review authoring produced an invalid packet for '{record.Id}': " +
                string.Join("; ", packetErrors));
        }

        return packet;
    }

    private static ManualQaReviewTarget CreateTarget(
        string recordId,
        CalibrationTarget source) => new()
        {
            SourceTargetId = source.Id,
            SourceLineageDigest = CalibrationDigest.ComputeSequence(
            [
                recordId,
                source.Id,
                .. source.SourceWorkItemIds.Order(StringComparer.Ordinal),
            ]),
            SourceCategory = source.Category,
            Title = source.Title,
            Scope = source.Scope,
            OverlapGroupId = $"manual-qa-overlap:{StableToken(recordId + "\n" + source.Scope)}",
            EvidenceIds = [.. source.EvidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
        };

    private static ManualQaReviewManifestPacket CreateManifestPacket(
        ManualQaReviewPacket packet) => new()
        {
            SourceRecordId = packet.SourceRecordId,
            RepositoryId = packet.Repository.Id,
            RepositorySourceDigest = packet.Repository.SourceDigest,
            FileName = $"{Slug(packet.Repository.Name)}.manual-qa-review.json",
            PacketDigest = CalibrationDigest.Compute(packet),
            LineageDigest = CalibrationDigest.ComputeSequence(packet.Targets
                .OrderBy(target => target.SourceTargetId, StringComparer.Ordinal)
                .Select(target => $"{target.SourceTargetId}\n{target.SourceLineageDigest}")),
            TargetCount = packet.Targets.Count,
        };

    private static string StableToken(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 10)).ToLowerInvariant();
    }

    private static string Slug(string value)
    {
        StringBuilder builder = new();
        bool separator = false;
        foreach (char character in value.Normalize(NormalizationForm.FormKC))
        {
            char lower = char.ToLower(character, CultureInfo.InvariantCulture);
            if (char.IsAsciiLetterOrDigit(lower))
            {
                builder.Append(lower);
                separator = false;
            }
            else if (!separator && builder.Length > 0)
            {
                builder.Append('-');
                separator = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "repository" : builder.ToString();
    }
}
