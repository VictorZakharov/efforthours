using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class ManualQaSourceWorkItemLineage
{
    public const string Version = ManualQaDecisionVersions.SourceWorkItemLineageV1;

    public static string CreateCandidateWorkItemId(string sourceWorkItemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceWorkItemId);

        return $"work:manual-qa-coding-ratio:{Token(sourceWorkItemId)}:part-0001";
    }

    public static string CreateReviewedTargetId(string sourceRecordId, string sourceTargetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRecordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTargetId);

        return $"target:manual-qa-review:{Token(sourceRecordId + "\n" + sourceTargetId)}";
    }

    private static string Token(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 10)).ToLowerInvariant();
    }
}
