using System.Globalization;

namespace EffortHours.Cli;

internal static class ChangePortfolioTimeParser
{
    public static bool TryResolveTimeZone(
        string value,
        out TimeZoneInfo timeZone,
        out string? error)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(value);
            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            error = $"Timezone '{value}' was not found on this host.";
            return false;
        }
    }

    public static bool TryParse(
        string value,
        TimeZoneInfo timeZone,
        out DateTimeOffset result,
        out string? error)
    {
        if (HasExplicitOffset(value))
        {
            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out result))
            {
                result = result.ToUniversalTime();
                error = null;
                return true;
            }

            error = $"Timestamp '{value}' is not a valid ISO-8601 instant.";
            return false;
        }

        if (!DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out DateTime local))
        {
            result = default;
            error = $"Timestamp '{value}' is not valid in timezone '{timeZone.Id}'.";
            return false;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            result = default;
            error = $"Timestamp '{value}' falls in a skipped daylight-saving interval for '{timeZone.Id}'.";
            return false;
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            result = default;
            error = $"Timestamp '{value}' is ambiguous in '{timeZone.Id}'; supply an explicit UTC offset.";
            return false;
        }

        result = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
        error = null;
        return true;
    }

    private static bool HasExplicitOffset(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.EndsWith('Z') || trimmed.EndsWith('z'))
        {
            return true;
        }

        return trimmed.Length >= 6 &&
            trimmed[^3] == ':' &&
            trimmed[^6] is '+' or '-' &&
            char.IsDigit(trimmed[^5]) &&
            char.IsDigit(trimmed[^4]) &&
            char.IsDigit(trimmed[^2]) &&
            char.IsDigit(trimmed[^1]);
    }
}
