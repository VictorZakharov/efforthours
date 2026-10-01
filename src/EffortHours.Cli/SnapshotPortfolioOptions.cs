using System.Globalization;

namespace EffortHours.Cli;

internal sealed record SnapshotPortfolioOptions
{
    public required string Manifest { get; init; }
    public required string Local { get; init; }
    public required string Checkpoint { get; init; }
    public string? Output { get; init; }
    public string? ImportReceipts { get; init; }
    public string? ImportHistorical { get; init; }
    public string? Reproduce { get; init; }
    public string? Upgrade { get; init; }
    public string? PreviousResult { get; init; }
    public int? Year { get; init; }
    public string? Timezone { get; init; }
    public required DateTimeOffset AsOf { get; init; }
    public bool Preflight { get; init; }
    public bool FetchMissing { get; init; }
    public int Concurrency { get; init; } = 1;
    public int TimeoutSeconds { get; init; } = 3600;
    public int ArchiveMiB { get; init; } = 256;
    public int CheckpointMiB { get; init; } = 512;
    public int MemoryMiB { get; init; } = 2048;
    public int OutputMiB { get; init; } = 32;
    public decimal? HourlyRate { get; init; }
    public string Currency { get; init; } = "USD";

    public static SnapshotPortfolioOptions Parse(string[] args, DateTimeOffset observed)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        HashSet<string> flags = new(StringComparer.Ordinal);
        HashSet<string> allowed = new(StringComparer.Ordinal)
        {
            "--manifest", "--local", "--checkpoint", "--output", "--import-receipts", "--import-historical", "--reproduce",
            "--upgrade", "--year", "--timezone", "--as-of", "--concurrency", "--timeout-seconds", "--archive-mib",
            "--previous-result",
            "--checkpoint-mib", "--memory-mib", "--output-mib", "--hourly-rate", "--currency",
        };
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option is "--preflight" or "--plan" or "--fetch-missing" or "--no-rate")
            {
                if (!flags.Add(option)) throw new InvalidDataException("Duplicate option: " + option);
            }
            else if (!allowed.Contains(option) || i + 1 >= args.Length || !values.TryAdd(option, args[++i]))
                throw new InvalidDataException("Unknown, repeated, or missing-value option: " + option);
        }
        string Required(string key) => values.GetValueOrDefault(key) is { Length: > 0 } value ? value :
            throw new InvalidDataException("Required option: " + key);
        int Integer(string key, int fallback, int minimum, int maximum) => !values.TryGetValue(key, out string? value) ? fallback :
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= minimum && parsed <= maximum
                ? parsed : throw new InvalidDataException($"{key} requires an integer from {minimum} to {maximum}.");
        DateTimeOffset asOf = observed;
        if (values.TryGetValue("--as-of", out string? iso) &&
            (!(iso.EndsWith('Z') || iso.Length >= 6 && iso[^3] == ':' && iso[^6] is '+' or '-') ||
             !DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf)))
            throw new InvalidDataException("--as-of requires ISO-8601 with an explicit UTC offset.");
        if (asOf > observed) throw new InvalidDataException("--as-of cannot be a future instant.");
        decimal? rate = null;
        if (values.TryGetValue("--hourly-rate", out string? raw))
        {
            if (flags.Contains("--no-rate") || !decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out decimal parsed) || parsed < 0 || parsed > 1_000_000)
                throw new InvalidDataException("--hourly-rate requires a nonnegative bounded decimal and cannot combine with --no-rate.");
            rate = parsed;
        }
        string? upgrade = values.GetValueOrDefault("--upgrade");
        string currency = values.GetValueOrDefault("--currency") ?? "USD";
        if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z') || values.ContainsKey("--currency") && rate is null)
            throw new InvalidDataException("--currency requires --hourly-rate and an uppercase three-letter currency code.");
        if (upgrade is not (null or "rebuild" or "new-epoch")) throw new InvalidDataException("--upgrade must be rebuild or new-epoch.");
        return new()
        {
            Manifest = Required("--manifest"),
            Local = Required("--local"),
            Checkpoint = Required("--checkpoint"),
            Output = values.GetValueOrDefault("--output"),
            ImportReceipts = values.GetValueOrDefault("--import-receipts"),
            ImportHistorical = values.GetValueOrDefault("--import-historical"),
            Reproduce = values.GetValueOrDefault("--reproduce"),
            Upgrade = upgrade,
            PreviousResult = values.GetValueOrDefault("--previous-result"),
            Year = values.ContainsKey("--year") ? Integer("--year", 2026, 1970, 9998) : null,
            Timezone = values.GetValueOrDefault("--timezone"),
            AsOf = asOf.ToUniversalTime(),
            HourlyRate = rate,
            Currency = currency,
            Preflight = flags.Contains("--preflight") || flags.Contains("--plan"),
            FetchMissing = flags.Contains("--fetch-missing"),
            Concurrency = Integer("--concurrency", 1, 1, 2),
            TimeoutSeconds = Integer("--timeout-seconds", 3600, 1, 86400),
            ArchiveMiB = Integer("--archive-mib", 256, 1, 512),
            CheckpointMiB = Integer("--checkpoint-mib", 512, 1, 8192),
            MemoryMiB = Integer("--memory-mib", 2048, 128, 8192),
            OutputMiB = Integer("--output-mib", 32, 1, 32),
        };
    }
}
