using System.Diagnostics;
using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class CalendarCommand
{
    public static async Task<int> ExecuteAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (args.Any(a => a is "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        try
        {
            CalendarOptions options = CalendarOptions.Parse(args.Length == 0 ? ["--interactive"] : args);
            TextWriter progress = TextWriter.Synchronized(stderr);
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(await SettingAsync(options, "--timezone",
                "Timezone", TimeZoneInfo.Local.Id, progress, token).ConfigureAwait(false));
            DateTime localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
            DateOnly monthEnd = DateOnly.FromDateTime(new DateTime(localNow.Year, localNow.Month, 1)).AddDays(-1);
            DateOnly monthStart = new(monthEnd.Year, monthEnd.Month, 1);
            DateOnly from = CalendarOptions.Date(await SettingAsync(options, "--from", "First date (inclusive)",
                monthStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), progress, token).ConfigureAwait(false));
            DateOnly to = CalendarOptions.Date(await SettingAsync(options, "--to", "Last date (inclusive)",
                monthEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), progress, token).ConfigureAwait(false));
            if (to < from || to.DayNumber - from.DayNumber >= ChangePortfolioComparisonLimits.MaximumBuckets ||
                to >= DateOnly.FromDateTime(localNow))
                throw new ArgumentException("Choose 1 through 512 complete local calendar days; --to must precede today.");
            decimal capacity = CalendarOptions.Capacity(await SettingAsync(options, "--capacity-hours-per-day",
                "Reference hours per calendar day", "8", progress, token).ConfigureAwait(false));
            string format = await SettingAsync(options, "--format", "Format (html, text, json)", "html", progress, token).ConfigureAwait(false);
            if (format is not ("html" or "text" or "json")) throw new ArgumentException("Format must be html, text, or json.");
            string? output = options.Values.GetValueOrDefault("--output");
            if (options.Interactive && output is null)
                output = await PromptAsync("Output path (- for stdout)", Path.Combine(Path.GetTempPath(),
                    $"efforthours-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.{(format == "text" ? "md" : format)}"), progress, token).ConfigureAwait(false);
            if (output == "-") output = null;
            string? checkpoint = options.Values.GetValueOrDefault("--checkpoint");
            string head = await SettingAsync(options, "--head", "Reachable head revision", "HEAD", progress, token).ConfigureAwait(false);
            if (!int.TryParse(options.Values.GetValueOrDefault("--timeout-seconds") ?? "3600", NumberStyles.None,
                CultureInfo.InvariantCulture, out int timeout) || timeout is < 1 or > 86400)
                throw new ArgumentException("Timeout must be 1 through 86400 seconds.");
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(timeout));
            token = deadline.Token;
            await SelectProjectsAsync(options, progress, token).ConfigureAwait(false);
            if (options.Authors.Count == 0 && options.Projects.All(p => p.Provider))
            {
                if (!options.Interactive) throw new ArgumentException("Checkout-free projects require --author email or name.");
                string aliases = await PromptAsync("Git identity aliases (email or name, separated by ;)", "", progress, token).ConfigureAwait(false);
                options.Authors.AddRange(aliases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                if (options.Authors.Count == 0) throw new ArgumentException("Checkout-free projects require a Git identity.");
            }
            List<string> roots = [];
            List<ChangeAuthorPeriodManifestRepository> repositories = [];
            Dictionary<string, string> paths = new(StringComparer.Ordinal);
            foreach ((string id, string locator, bool provider) in options.Projects)
            {
                if (paths.ContainsKey(id)) throw new ArgumentException("Repeated project ID.");
                if (provider && options.Interactive && !options.FetchMissing)
                    options.FetchMissing = await PromptAsync("Acquire missing GitHub objects (yes/no)", "no", progress, token).ConfigureAwait(false) == "yes";
                string root;
                string pin;
                if (provider)
                {
                    ManagedRepositoryHead managed = await ProgressAsync(new ManagedGitQueryPlanner().PrepareHeadAsync(
                        locator, head, options.FetchMissing, token), $"{id}: provider resolution/acquisition", progress, token).ConfigureAwait(false);
                    root = managed.RepositoryPath; pin = managed.ObjectId;
                }
                else
                {
                    await progress.WriteLineAsync($"eh: calendar {id}: resolving local head").ConfigureAwait(false);
                    root = await new GitClient().ResolveRepositoryRootAsync(locator, token).ConfigureAwait(false);
                    pin = await new GitClient().ResolveCommitAsync(root, head, token).ConfigureAwait(false);
                    roots.Add(root);
                }
                if (paths.Values.Contains(root, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                    throw new ArgumentException("Each Git repository may be selected only once.");
                foreach (string destination in new[] { output, checkpoint }.OfType<string>())
                    if (SnapshotPortfolioPaths.IsWithin(root, destination))
                        throw new ArgumentException("Reports and checkpoints must be outside selected repositories.");
                paths.Add(id, root);
                repositories.Add(new() { Id = id, RepositoryPath = root, Heads = [new() { Id = "selected", ObjectId = pin }] });
            }
            if (options.Authors.Count == 0)
            {
                foreach (string root in roots)
                    if (await new GitClient().ReadConfiguredEmailAsync(root, token).ConfigureAwait(false) is { } email)
                        options.Authors.Add(email);
                string defaults = string.Join(";", options.Authors.Distinct(StringComparer.OrdinalIgnoreCase));
                if (options.Interactive)
                {
                    string aliases = await PromptAsync("Git identity aliases (email or name, separated by ;)", defaults, progress, token).ConfigureAwait(false);
                    options.Authors.Clear(); options.Authors.AddRange(aliases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
            }
            if (options.Authors.Count == 0) throw new ArgumentException("No Git user.email configured; supply --author email or name.");
            ChangeAuthorPeriodManifest manifest = new()
            {
                Selection = new()
                {
                    SinceInclusive = Boundary(from, zone),
                    UntilExclusive = Boundary(to.AddDays(1), zone),
                    TimeZone = zone.Id,
                    DateField = ChangePortfolioDateField.Author,
                    MergePolicy = ChangePortfolioMergePolicy.Exclude,
                    CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
                },
                Contributors = [new() { Id = "self", Aliases = [.. options.Authors.Distinct(StringComparer.OrdinalIgnoreCase)] }],
                Repositories = repositories,
            };
            IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
            if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors));
            ResolvedChangeAuthorPeriodManifest resolved = new(manifest,
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest), paths);
            ChangePortfolioCommandOptions calculation = new()
            {
                CalendarReport = true,
                Bucket = "calendar-day",
                Format = format,
                CapacityHoursPerDay = capacity,
                OutputPath = output,
                CheckpointPath = checkpoint,
                NoRate = true,
                ReportTitle = "Daily Change EHE calendar",
            };
            await progress.WriteLineAsync($"eh: calendar {repositories.Count} projects, {from:yyyy-MM-dd} through {to:yyyy-MM-dd}; selecting and estimating").ConfigureAwait(false);
            int code = await ProgressAsync(new ChangePortfolioCommand().ExecuteCalendarAsync(calculation, resolved,
                stdout, progress, token), "selection/analysis/reconciliation", progress, token).ConfigureAwait(false);
            if (output is not null && code == 0) await progress.WriteLineAsync("eh: calendar saved " + Path.GetFullPath(output)).ConfigureAwait(false);
            return code;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException or FormatException or
            TimeZoneNotFoundException or InvalidTimeZoneException or UnauthorizedAccessException)
        {
            await stderr.WriteLineAsync("eh: calendar " + e.Message).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
    }

    private static DateTimeOffset Boundary(DateOnly date, TimeZoneInfo zone)
    {
        if (!ChangePortfolioTimeParser.TryParse(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), zone,
            out DateTimeOffset instant, out string? error)) throw new ArgumentException(error);
        return instant;
    }

    private static async Task<string> SettingAsync(CalendarOptions options, string flag, string label, string fallback,
        TextWriter stderr, CancellationToken token) => options.Values.GetValueOrDefault(flag) ??
        (options.Interactive ? await PromptAsync(label, fallback, stderr, token).ConfigureAwait(false) : fallback);

    internal static async Task<string> PromptAsync(string label, string fallback, TextWriter stderr, CancellationToken token)
    {
        await stderr.WriteAsync($"{label} [{fallback}]: ").ConfigureAwait(false);
        await stderr.FlushAsync(token).ConfigureAwait(false);
        string response = await Console.In.ReadLineAsync(token).ConfigureAwait(false) ??
            throw new ArgumentException("Interactive input ended. Supply flags for unattended use, or run in a terminal.");
        return string.IsNullOrWhiteSpace(response) ? fallback : response.Trim();
    }

    private static async Task<T> ProgressAsync<T>(Task<T> task, string phase, TextWriter stderr, CancellationToken token)
    {
        Stopwatch watch = Stopwatch.StartNew();
        await stderr.WriteLineAsync("eh: calendar " + phase + " started").ConfigureAwait(false);
        using CancellationTokenSource done = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task heartbeat = HeartbeatAsync();
        try { return await task.ConfigureAwait(false); }
        finally { await done.CancelAsync().ConfigureAwait(false); await heartbeat.ConfigureAwait(false); }
        async Task HeartbeatAsync()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), done.Token).ConfigureAwait(false);
                    await stderr.WriteLineAsync($"eh: calendar {phase} still running; elapsed {watch.Elapsed.TotalSeconds:F0}s").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (done.IsCancellationRequested) { }
        }
    }

    private static async Task SelectProjectsAsync(CalendarOptions options, TextWriter stderr, CancellationToken token)
    {
        if (options.Projects.Count > 0) return;
        string current = Directory.GetCurrentDirectory();
        string? root = await CalendarWorkspace.RootAsync(options.Repository ?? current, token).ConfigureAwait(false);
        if (options.Interactive && options.Repository is null && !options.AllRepos && !options.Values.ContainsKey("--workspace"))
        {
            string scope = await PromptAsync("Repositories (checkout or all)", root is null ? "all" : "checkout", stderr, token).ConfigureAwait(false);
            if (scope is not ("checkout" or "all")) throw new ArgumentException("Repository scope must be checkout or all.");
            options.AllRepos = scope == "all";
        }
        if (options.AllRepos || options.Values.ContainsKey("--workspace") || root is null && options.Repository is null)
        {
            string workspaceDefault = root is not null && options.AllRepos ? Path.GetDirectoryName(root) ?? current : current;
            string workspace = options.Values.GetValueOrDefault("--workspace") ??
                (options.Interactive ? await PromptAsync("Workspace to discover", workspaceDefault, stderr, token).ConfigureAwait(false) : workspaceDefault);
            options.Projects.AddRange(CalendarWorkspace.Discover(workspace, token));
            if (options.Interactive)
            {
                foreach (var (Id, Locator, Provider) in options.Projects) await stderr.WriteLineAsync($"  {Id}: {Locator}").ConfigureAwait(false);
                string selected = await PromptAsync("Projects (all or comma-separated IDs)", "all", stderr, token).ConfigureAwait(false);
                if (selected != "all")
                {
                    HashSet<string> ids = [.. selected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
                    if (ids.Count == 0 || ids.Except(options.Projects.Select(p => p.Id)).Any()) throw new ArgumentException("Unknown or empty project selection.");
                    options.Projects.RemoveAll(p => !ids.Contains(p.Id));
                }
            }
        }
        else if (root is not null) options.Projects.Add(("project-1", root, false));
        else throw new ArgumentException("The selected path is not a Git checkout.");
    }

    private const string Help = """
        Usage: eh calendar [repository] [options]
        Plain eh calendar prompts with defaults; flags run unattended unless --interactive is explicit.
          --interactive                Prompt for unspecified settings
          --from yyyy-MM-dd --to yyyy-MM-dd  Inclusive complete dates (default: last complete month)
          --timezone <zone>            Default: local timezone
          --author <email-or-name>     Repeat aliases; default: selected local Git user.email values
          --project <id=path>          Repeat exact local projects
          --repo <id=owner/name>       Repeat checkout-free GitHub projects; requires --author
          --fetch-missing              Explicit managed-cache provider/object acquisition opt-in
          --all-repos --workspace <directory>  Discover Git checkouts under a folder
          --head <revision>            Default: HEAD; resolve once per project
          --format <html|text|json>    Default: offline interactive HTML; text is Markdown
          --output <path>              Default: stdout; interactive suggests an external temp file
          --checkpoint <directory>    Stable reuse across output names (default: <output>.eh-checkpoint)
          --capacity-hours-per-day <n> Reference denominator, default 8, including idle days
          --timeout-seconds <n>        Cancellable run deadline, default 3600
        Inside a checkout defaults to that repository. Outside defaults to repositories under cwd.
        Author dates select changes; merges excluded, valid coauthors included. Only pinned reachable
        work is represented. Project toggles filter one jointly reconciled result without reanalysis.
        Experimental Change EHE and reference ratios are not actual labor, productivity, or AI skill scores.
        """;
}
