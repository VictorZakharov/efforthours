using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationCorpusReviewAuthoring
{
    public const string AuthoringVersion = "calibration-corpus-review-authoring/0.1.0";
    public const string Warning =
        "UNREVIEWED SECOND-PASS PACKET: prior labels are weak supervision, not ground truth. " +
        "An independent reviewer must decide every target before review maturity can advance.";

    public static CalibrationCorpusReviewPacket Scaffold(
        CalibrationCorpus corpus,
        bool blind = false)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        IReadOnlyList<string> corpusErrors = ContractValidation.Validate(corpus);
        if (corpusErrors.Count > 0)
        {
            throw new CalibrationEvaluationException(
                [.. corpusErrors.Select(error => $"Source corpus: {error}")]);
        }

        CalibrationCandidateVisibility visibility = blind
            ? CalibrationCandidateVisibility.Blind
            : CalibrationCandidateVisibility.Reference;
        CalibrationCorpusReviewPacket packet = new()
        {
            AuthoringVersion = AuthoringVersion,
            Status = CalibrationAuthoringStatus.Unreviewed,
            Warning = Warning,
            SourceCorpus = new CalibrationCorpusReference
            {
                Id = corpus.Id,
                Version = corpus.Version,
                Digest = CalibrationDigest.Compute(corpus),
            },
            Rubric = corpus.Rubric,
            CandidateVisibility = visibility,
            Records = [.. corpus.Records
                .OrderBy(record => record.Id, StringComparer.Ordinal)
                .Select(record => CreateRecord(record, blind))],
            Instructions =
            [
                "Use a reviewer identity distinct from every teacher or reviewer already recorded on the source record.",
                "Check each target against its evidence and the referenced rubric; do not use repository history, price, or a preferred total.",
                "Choose accept only after independently checking the prior target; choose replace with a complete range and rationale when correcting it.",
                "Decide every source target exactly once and retain its structural lineage.",
                "Use reviewed only for a genuine second review; use adjudicated only after disagreements are explicitly resolved by a distinct adjudicator.",
                "Compile the completed corpus-review plan only against the exact source-corpus digest in this packet.",
            ],
        };

        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration corpus review authoring produced an invalid packet: " +
                string.Join("; ", packetErrors));
        }

        return packet;
    }

    private static CalibrationCorpusReviewRecord CreateRecord(
        CalibrationRecord record,
        bool blind) => new()
        {
            SourceRecordId = record.Id,
            Repository = record.Repository,
            Change = record.Change,
            Profile = record.Profile,
            BaselineId = record.BaselineId,
            Partition = record.Partition,
            SourceReviewStatus = record.Review.Status,
            CandidateTotalHours = blind
                ? null
                : ContractValidation.Sum(record.Targets.Select(target => target.Hours)),
            Targets = [.. record.Targets
                .OrderBy(target => target.Id, StringComparer.Ordinal)
                .Select(target => CreateTarget(target, blind))],
        };

    private static CalibrationCorpusReviewTarget CreateTarget(
        CalibrationTarget target,
        bool blind) => new()
        {
            SourceTargetId = target.Id,
            Category = target.Category,
            Title = target.Title,
            Scope = target.Scope,
            SourceWorkItemIds = [.. target.SourceWorkItemIds.Order(StringComparer.Ordinal)],
            EvidenceIds = [.. target.EvidenceIds.Order(StringComparer.Ordinal)],
            Candidate = new CalibrationCorpusReviewCandidate
            {
                Hours = blind ? null : target.Hours,
                Rationale = blind ? null : target.Rationale,
                UncertaintyReasons = blind
                    ? []
                    : [.. target.UncertaintyReasons.Order(StringComparer.Ordinal)],
                SizeException = blind ? null : target.SizeException,
            },
            Review = new CalibrationCorpusReviewFields(),
        };
}
