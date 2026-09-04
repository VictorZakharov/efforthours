using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed class ChangePortfolioNativeOptionValues
{
    public bool Enabled { get; set; }

    public bool Team { get; set; }

    public ChangePortfolioNativePeriodKind? Period { get; private set; }

    public ChangePortfolioNativeBreakdown Breakdown { get; private set; } =
        ChangePortfolioNativeBreakdown.Total;

    public decimal? CapacityHoursPerDay { get; private set; }

    public string? ContributorsFrom { get; private set; }

    public int? SampleSize { get; private set; }

    public string? SampleSeed { get; private set; }

    public List<string> IncludedAuthors { get; } = [];

    public bool Provided { get; private set; }

    public bool TryParse(string option, string value, out string? error)
    {
        error = null;
        switch (option)
        {
            case "--capacity-hours-per-day":
                Provided = true;
                if (!decimal.TryParse(
                        value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out decimal capacity) || capacity <= 0m)
                {
                    error =
                        "Capacity hours per day must be a positive decimal using '.' as the decimal separator.";
                }

                CapacityHoursPerDay = capacity;
                return true;
            case "--period":
                Provided = true;
                if (!ChangePortfolioNamedPeriodResolver.TryParse(value, out ChangePortfolioNativePeriodKind period))
                {
                    error = "Period must be 'this-week', 'last-week', 'this-month', or 'last-month'.";
                }

                Period = period;
                return true;
            case "--breakdown":
                Provided = true;
                string canonical = value.ToLowerInvariant();
                if (canonical is not ("total" or "day"))
                {
                    error = "Breakdown must be 'total' or 'day'.";
                }

                Breakdown = canonical == "day"
                    ? ChangePortfolioNativeBreakdown.CalendarDay
                    : ChangePortfolioNativeBreakdown.Total;
                return true;
            case "--contributors-from":
                Provided = true;
                ContributorsFrom = value;
                return true;
            case "--sample":
                Provided = true;
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int sample) ||
                    sample is < 1 or > 63)
                {
                    error = "Sample must be an integer from 1 through 63.";
                }

                SampleSize = sample;
                return true;
            case "--sample-seed":
                Provided = true;
                SampleSeed = value;
                return true;
            case "--include-author":
                Provided = true;
                IncludedAuthors.Add(value);
                return true;
            default:
                return false;
        }
    }
}
