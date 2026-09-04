using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioNamedPeriodRange(
    ChangePortfolioNativePeriodKind Kind,
    DateTimeOffset SinceInclusive,
    DateTimeOffset UntilExclusive);

internal static class ChangePortfolioNamedPeriodResolver
{
    public static bool TryParse(string value, out ChangePortfolioNativePeriodKind kind)
    {
        string canonical = value.ToLowerInvariant();
        kind = canonical switch
        {
            "this-week" => ChangePortfolioNativePeriodKind.ThisWeek,
            "last-week" => ChangePortfolioNativePeriodKind.LastWeek,
            "this-month" => ChangePortfolioNativePeriodKind.ThisMonth,
            "last-month" => ChangePortfolioNativePeriodKind.LastMonth,
            _ => default,
        };
        return canonical is "this-week" or "last-week" or "this-month" or "last-month";
    }

    public static ChangePortfolioNamedPeriodRange Resolve(
        ChangePortfolioNativePeriodKind kind,
        DateTimeOffset asOf,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTimeOffset utcAsOf = asOf.ToUniversalTime();
        DateTime localDate = TimeZoneInfo.ConvertTime(utcAsOf, zone).Date;
        DateTime currentWeek = localDate.AddDays(-WeekdayOffset(localDate.DayOfWeek));
        DateTime currentMonth = new(localDate.Year, localDate.Month, 1);
        (DateTime start, DateTime? completeEnd) = kind switch
        {
            ChangePortfolioNativePeriodKind.ThisWeek => (currentWeek, (DateTime?)null),
            ChangePortfolioNativePeriodKind.LastWeek => (currentWeek.AddDays(-7), currentWeek),
            ChangePortfolioNativePeriodKind.ThisMonth => (currentMonth, (DateTime?)null),
            ChangePortfolioNativePeriodKind.LastMonth =>
                (currentMonth.AddMonths(-1), currentMonth),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new ChangePortfolioNamedPeriodRange(
            kind,
            ResolveLocal(start, zone),
            completeEnd is null ? utcAsOf : ResolveLocal(completeEnd.Value, zone));
    }

    private static DateTimeOffset ResolveLocal(DateTime value, TimeZoneInfo zone)
    {
        DateTime local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
        {
            throw new InvalidOperationException(
                $"Calendar boundary '{local:O}' is not unique in timezone '{zone.Id}'.");
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static int WeekdayOffset(DayOfWeek day) =>
        ((int)day - (int)DayOfWeek.Monday + 7) % 7;
}
