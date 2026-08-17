using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

internal static class IgnoreRuleDiagnostic
{
    public static Diagnostic Create(
        string relativePath,
        string source,
        IgnoreRuleCreationFailure failure)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
        string reason = failure switch
        {
            IgnoreRuleCreationFailure.PatternTooLong =>
                $"exceeded the {IgnoreGlobPattern.MaximumPatternCharacters}-character static matching limit",
            IgnoreRuleCreationFailure.InvalidCharacterClass =>
                "contained an invalid character-class range",
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        return new Diagnostic
        {
            Code = "FB2003",
            Severity = DiagnosticSeverity.Warning,
            Message = $"Ignore rule 'sha256:{digest}' {reason} and was skipped.",
            Locations = [new EvidenceLocation { Path = relativePath }],
        };
    }
}
