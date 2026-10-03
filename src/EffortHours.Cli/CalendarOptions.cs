using System.Globalization;

namespace EffortHours.Cli;

internal sealed class CalendarOptions
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public List<(string Id, string Locator, bool Provider)> Projects { get; } = [];
    public List<string> Authors { get; } = [];
    public bool Interactive { get; private set; }
    public bool AllRepos { get; set; }
    public bool FetchMissing { get; set; }
    public string? Repository { get; private set; }

    public static CalendarOptions Parse(string[] args)
    {
        CalendarOptions result = new();
        HashSet<string> allowed = ["--from", "--to", "--timezone", "--format", "--output", "--checkpoint",
            "--head", "--workspace", "--capacity-hours-per-day", "--timeout-seconds", "--date-field"];
        HashSet<string> flags = [];
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--interactive" or "--all-repos" or "--fetch-missing")
            {
                if (!flags.Add(arg)) throw new ArgumentException("Repeated option: " + arg);
                if (arg == "--interactive") result.Interactive = true;
                if (arg == "--all-repos") result.AllRepos = true;
                if (arg == "--fetch-missing") result.FetchMissing = true;
            }
            else if (arg is "--project" or "--repo" or "--author" || allowed.Contains(arg))
            {
                if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Missing value for " + arg);
                string value = args[i];
                if (arg == "--author") result.Authors.Add(value);
                else if (arg is "--project" or "--repo")
                {
                    int split = value.IndexOf('=');
                    if (split < 1 || split == value.Length - 1) throw new ArgumentException(arg + " requires id=locator.");
                    result.Projects.Add((value[..split], value[(split + 1)..], arg == "--repo"));
                }
                else if (!result.Values.TryAdd(arg, value)) throw new ArgumentException("Repeated option: " + arg);
            }
            else if (!arg.StartsWith('-') && result.Repository is null) result.Repository = arg;
            else throw new ArgumentException("Unknown or repeated argument: " + arg);
        }
        if (result.Repository is not null && (result.Projects.Count > 0 || result.AllRepos || result.Values.ContainsKey("--workspace")))
            throw new ArgumentException("Use a positional repository, explicit projects, or --all-repos.");
        if (result.Projects.Count > 0 && (result.AllRepos || result.Values.ContainsKey("--workspace")))
            throw new ArgumentException("Explicit projects cannot combine with workspace discovery.");
        if (result.Projects.Count > 256 || result.Authors.Count > 16) throw new ArgumentException("Input exceeds the manifest envelope.");
        return result;
    }

    public static DateOnly Date(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) && date.Year is >= 1970 and <= 9998
        ? date : throw new ArgumentException("Dates require yyyy-MM-dd, years 1970 through 9998.");

    public static decimal Capacity(string value) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint,
        CultureInfo.InvariantCulture, out decimal hours) && hours is > 0 and <= 1000000
        ? hours : throw new ArgumentException("Reference hours must be positive, at most 1000000.");
}
