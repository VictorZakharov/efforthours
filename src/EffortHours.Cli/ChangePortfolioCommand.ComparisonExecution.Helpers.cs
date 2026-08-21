using System.Reflection;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static IReadOnlyList<Diagnostic> CanonicalDiagnostics(
        IReadOnlyList<ChangePortfolioRepositoryOutcome> outcomes,
        ChangePortfolioComparisonCheckpoint checkpoint)
    {
        IEnumerable<Diagnostic> diagnostics = outcomes.SelectMany(outcome => outcome.Diagnostics);
        if (checkpoint.HitCount > 0)
        {
            diagnostics = diagnostics.Append(new Diagnostic
            {
                Code = "FB5333",
                Severity = DiagnosticSeverity.Information,
                Message = $"Reused {checkpoint.HitCount} immutable repository-evidence checkpoint(s); unchanged repositories were not replanned or reanalyzed.",
            });
        }

        return [.. diagnostics
            .DistinctBy(value => $"{value.Code}\0{value.Severity}\0{value.Message}", StringComparer.Ordinal)
            .OrderBy(value => value.Code, StringComparer.Ordinal)
            .ThenBy(value => value.Message, StringComparer.Ordinal)];
    }

    private static string SafeRepositoryFailure(
        string message,
        ChangeAuthorPeriodManifestRepository repository,
        ResolvedChangeAuthorPeriodManifest resolved)
    {
        string safe = message.Replace('\r', ' ').Replace('\n', ' ');
        safe = safe.Replace(repository.RepositoryPath, "<repository-path>", StringComparison.OrdinalIgnoreCase);
        safe = safe.Replace(
            resolved.RepositoryPaths[repository.Id],
            "<repository-path>",
            StringComparison.OrdinalIgnoreCase);
        foreach (string alias in resolved.Manifest.Contributors.SelectMany(value => value.Aliases))
        {
            safe = safe.Replace(alias, "<identity-alias>", StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(safe)
            ? $"Repository '{repository.Id}' failed without an actionable message."
            : safe;
    }

    private static Exception RootException(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static string CliVersion() => typeof(ChangePortfolioCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "unknown";
}
