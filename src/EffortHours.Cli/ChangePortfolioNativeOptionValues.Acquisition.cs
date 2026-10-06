using System.Globalization;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioNativeOptionValues
{
    public List<string> Repositories { get; } = [];
    public int? DiscoveryTimeoutSeconds { get; private set; }
    public int? MaximumAcquiredMebibytes { get; private set; }

    private bool TryParseAcquisition(string option, string value, out string? error)
    {
        error = null;
        switch (option)
        {
            case "--repository":
                Repositories.Add(value);
                return true;
            case "--discovery-timeout-seconds":
            case "--max-acquired-mib":
                int maximum = option == "--discovery-timeout-seconds" ? 86400 : 16384;
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 1 || number > maximum)
                    error = $"{option} must be an integer from 1 through {maximum}.";
                if (option == "--discovery-timeout-seconds")
                {
                    if (DiscoveryTimeoutSeconds is not null) error = "Specify --discovery-timeout-seconds only once.";
                    DiscoveryTimeoutSeconds = number;
                }
                else
                {
                    if (MaximumAcquiredMebibytes is not null) error = "Specify --max-acquired-mib only once.";
                    MaximumAcquiredMebibytes = number;
                }
                return true;
            default:
                return false;
        }
    }
}
