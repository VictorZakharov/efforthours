using System.Globalization;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

internal static class ChangePortfolioAcquisitionMarkdown
{
    internal static void Append(StringBuilder text, ChangePortfolioHostDiscovery discovery)
    {
        if (discovery.RepositoryRestriction is { } restriction)
            text.Append("Repository coverage: **explicitly restricted** to ").Append(restriction.RequestedRepositoryCount)
                .Append(" requested repositories; ").Append(restriction.ExcludedRepositoryCount)
                .Append(" accessible owner repositories excluded; restriction digest `").Append(restriction.InputDigest).AppendLine("`.");
        if (discovery.Acquisition is { } acquisition)
            text.Append("Discovery/acquisition budget: ").Append(acquisition.TimeoutSeconds).Append(" seconds; ")
                .Append(acquisition.MaximumBytes.ToString(CultureInfo.InvariantCulture)).Append(" bytes of observed object-store growth; observed ")
                .Append(acquisition.AcquiredBytes.ToString(CultureInfo.InvariantCulture)).Append(" bytes across ")
                .Append(acquisition.RepositoryCount).AppendLine(" repository caches. Growth includes incoming packs and is checked periodically; it is not wire-byte accounting.");
    }
}
