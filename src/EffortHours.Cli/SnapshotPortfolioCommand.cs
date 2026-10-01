using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class SnapshotPortfolioCommand
{
    public static async Task<int> ExecuteAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        DateTimeOffset observed = DateTimeOffset.UtcNow;
        if (args.Length == 0 || args.Any(a => a is "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return args.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }
        SnapshotPortfolioOptions options;
        try { options = SnapshotPortfolioOptions.Parse(args, observed); }
        catch (InvalidDataException e)
        {
            await stderr.WriteLineAsync(e.Message).ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        await using SnapshotPortfolioResourceBudget budget = new(deadline, options.MemoryMiB);
        string phase = "inputs";
        bool safeOutput = false;
        try
        {
            SnapshotPortfolioManifest manifest = await ReadAsync<SnapshotPortfolioManifest>(options.Manifest,
                "snapshot-portfolio-manifest.schema.json", deadline.Token).ConfigureAwait(false);
            SnapshotPortfolioLocalMap local = await ReadAsync<SnapshotPortfolioLocalMap>(options.Local,
                "snapshot-portfolio-local-map.schema.json", deadline.Token).ConfigureAwait(false);
            manifest = manifest with { Year = options.Year ?? manifest.Year, Timezone = options.Timezone ?? manifest.Timezone };
            SnapshotPortfolioValidation.Validate(manifest);
            foreach (SnapshotProjectLocator locator in local.Projects.Where(p => p.RepositoryPath is not null))
            {
                string root = await new GitClient().ResolveRepositoryRootAsync(locator.RepositoryPath!, deadline.Token).ConfigureAwait(false);
                foreach (string path in new[] { options.Checkpoint, options.Output }.Where(p => p is not null).Cast<string>())
                {
                    if (SnapshotPortfolioPaths.IsWithin(root, path))
                        throw new InvalidDataException("Output and checkpoint paths must be outside measured source repositories.");
                }
            }
            if (options.Preflight && options.Output is not null && File.Exists(options.Output))
                throw new InvalidDataException("Preflight cannot overwrite an existing publication; use stdout or a fresh plan output path.");
            safeOutput = true;
            SnapshotPortfolioReport? previous = options.PreviousResult is not null
                ? await ReadReportAsync(options.PreviousResult, deadline.Token).ConfigureAwait(false)
                : options.Output is not null && File.Exists(options.Output) && !options.Preflight
                ? await ReadReportAsync(options.Output, deadline.Token).ConfigureAwait(false) : null;
            SnapshotPortfolioReport? reproduce = options.Reproduce is null ? null : await ReadReportAsync(options.Reproduce, deadline.Token).ConfigureAwait(false);
            if (reproduce is not null)
            {
                if (reproduce.ManifestDigest != SnapshotMeasurementIdentity.Digest(manifest))
                    throw new InvalidDataException("Reproduction requires the original manifest and policy digest.");
                options = options with { AsOf = reproduce.AsOf };
            }
            string epoch = SnapshotMeasurementIdentity.Digest(SnapshotMeasurementIdentity.Create(manifest.Profile));
            bool incompatible = previous is not null && previous.MeasurementEpoch != epoch;
            if (incompatible && options.Upgrade is null && !options.Preflight)
                throw new InvalidDataException("Measurement semantics changed. Choose --upgrade rebuild or --upgrade new-epoch explicitly.");
            if (options.Preflight && options.FetchMissing)
                throw new InvalidDataException("Preflight is offline/read-only; acquire provider objects explicitly before planning.");
            phase = "lock";
            using SnapshotPortfolioStore store = new(options.Checkpoint, options.CheckpointMiB * 1024L * 1024L);
            await using FileStream? runLock = options.Preflight ? null : await store.AcquireLockAsync(deadline.Token).ConfigureAwait(false);
            await using FileStream? outputLock = options.Preflight || options.Output is null ? null : AcquireOutputLock(options.Output);
            if (!options.Preflight)
            {
                if (options.ImportReceipts is not null)
                {
                    SnapshotPortfolioReport imported = await ReadReportAsync(options.ImportReceipts, deadline.Token).ConfigureAwait(false);
                    await store.ImportAsync(imported, deadline.Token).ConfigureAwait(false);
                    await ImportBindingsAsync(store, imported, manifest, deadline.Token).ConfigureAwait(false);
                }
                if (options.ImportHistorical is not null)
                {
                    EstimateReport historical = await ReadAsync<EstimateReport>(options.ImportHistorical, SchemaNames.EstimateReport,
                        deadline.Token, 32 * 1024 * 1024).ConfigureAwait(false);
                    if (ContractValidation.Validate(historical).Count != 0) throw new InvalidDataException("Legacy estimate is semantically invalid.");
                    await store.SaveAsync("historical", SnapshotMeasurementIdentity.Digest(historical), historical, deadline.Token).ConfigureAwait(false);
                }
            }
            phase = "measurement";
            string producer = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            SnapshotPortfolioReport result = await new SnapshotPortfolioRunner(store).RunAsync(manifest, local, new()
            {
                AsOf = options.AsOf,
                ProducerVersion = producer,
                Preflight = options.Preflight,
                FetchMissing = options.FetchMissing,
                Concurrency = options.Concurrency,
                MaximumArchiveBytes = options.ArchiveMiB * 1024 * 1024,
                RateCard = options.HourlyRate is null ? null : new RateCard
                {
                    Id = "user-supplied-cli-rate",
                    Name = "User-supplied CLI rate",
                    Currency = options.Currency,
                    HourlyRate = options.HourlyRate.Value,
                    Methodology = "Explicit caller override; not an EffortHours market-rate claim.",
                },
                Previous = options.Upgrade == "new-epoch" ? null : previous,
                Reproduce = reproduce,
                PreviousEpochDigest = incompatible ? previous!.SemanticDigest : null,
            }, deadline.Token).ConfigureAwait(false);
            result = result with
            {
                Telemetry = result.Telemetry with
                {
                    ArchiveMiBLimit = options.ArchiveMiB,
                    CheckpointMiBLimit = options.CheckpointMiB,
                    MemoryMiBLimit = options.MemoryMiB,
                    OutputMiBLimit = options.OutputMiB,
                    TimeoutSecondsLimit = options.TimeoutSeconds,
                },
            };
            phase = "validation";
            string json = ContractJson.SerializeDocument(result);
            SchemaValidationResult schema = ContractSchemaValidator.Validate("snapshot-portfolio-report.schema.json", json);
            if (!schema.IsValid) throw new InvalidDataException("Portfolio output failed its schema: " + string.Join("; ", schema.Errors));
            using Process process = Process.GetCurrentProcess();
            if (process.WorkingSet64 > options.MemoryMiB * 1024L * 1024L)
                throw new InvalidOperationException("Observed working-set budget exceeded; previous result is preserved.");
            phase = "publication";
            if (options.Output is null)
            {
                if (Encoding.UTF8.GetByteCount(json) > options.OutputMiB * 1024 * 1024) throw new InvalidOperationException("Output budget exceeded.");
                await stdout.WriteAsync(json).ConfigureAwait(false);
            }
            else
            {
                if (incompatible && !options.Preflight)
                    await SnapshotPortfolioStore.AtomicWriteAsync(options.Output + ".epoch-" + previous!.SemanticDigest[7..] + ".json",
                        ContractJson.SerializeDocument(previous), deadline.Token).ConfigureAwait(false);
                await SnapshotPortfolioStore.AtomicWriteAsync(options.Output, json, deadline.Token, options.OutputMiB * 1024 * 1024).ConfigureAwait(false);
            }
            await stderr.WriteLineAsync($"Snapshot portfolio {result.Status}: {result.Telemetry.EstimatorCalls} estimator calls, " +
                $"{result.Telemetry.ReceiptHits} receipt hits, {result.Telemetry.Exports} exports.").ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException or OperationCanceledException)
        {
            bool cancelled = e is OperationCanceledException;
            string failure = ContractJson.SerializeDocument(new
            {
                schemaVersion = "1.0.0",
                protocolVersion = "snapshot-portfolio-failure/1.0.0",
                status = "incomplete",
                phase,
                code = budget.Exceeded ? "memory-budget" : cancelled ? "cancelled-or-time-budget" : "snapshot-portfolio-failed",
            });
            if (options.Output is null) await stdout.WriteAsync(failure).ConfigureAwait(false);
            else if (safeOutput && phase != "lock")
                await SnapshotPortfolioStore.AtomicWriteAsync(options.Output + ".failure.json", failure, CancellationToken.None).ConfigureAwait(false);
            await stderr.WriteLineAsync($"Snapshot portfolio failed during {phase}: {e.Message}").ConfigureAwait(false);
            return cancelled ? CliExitCodes.Cancelled : CliExitCodes.InvalidInput;
        }
    }

    internal static async Task<T> ReadAsync<T>(string path, string schema, CancellationToken token,
        int maximumBytes = 1024 * 1024)
    {
        if (new FileInfo(path).Length > maximumBytes) throw new InvalidDataException("Input exceeds its bounded byte budget.");
        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, token).ConfigureAwait(false);
        SchemaValidationResult result = ContractSchemaValidator.Validate(schema, json);
        if (!result.IsValid) throw new InvalidDataException("Input failed schema validation: " + string.Join("; ", result.Errors));
        return ContractJson.Deserialize<T>(json);
    }

    internal static async Task<SnapshotPortfolioReport> ReadReportAsync(string path, CancellationToken token)
    {
        SnapshotPortfolioReport report = await ReadAsync<SnapshotPortfolioReport>(path, "snapshot-portfolio-report.schema.json", token,
            32 * 1024 * 1024).ConfigureAwait(false);
        SnapshotPortfolioValidation.Validate(report);
        return report;
    }

    private static FileStream AcquireOutputLock(string output)
    {
        string full = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        try { return new(full + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new InvalidOperationException("Another run holds the output lock."); }
    }

    private static async Task ImportBindingsAsync(SnapshotPortfolioStore store, SnapshotPortfolioReport imported,
        SnapshotPortfolioManifest manifest, CancellationToken token)
    {
        foreach (SnapshotProjectResult project in imported.Projects)
        {
            SnapshotProjectDefinition? definition = manifest.Projects.FirstOrDefault(p => p.Id == project.Id);
            if (definition is null || project.AreasDigest != SnapshotMeasurementIdentity.Digest(definition.Areas)) continue;
            foreach (SnapshotPeriodResult period in project.Periods.Where(p => p.WholeReceiptId is not null))
            {
                SnapshotMeasurementReceipt whole = imported.Receipts.Single(r => r.Id == period.WholeReceiptId);
                string key = SnapshotPortfolioRunner.BindingKey(period.CommitObjectId!, whole.Measurement, project.AreasDigest);
                SnapshotStoredBinding binding = new(key, "", whole.Id, period.Areas);
                binding = binding with { Digest = SnapshotPortfolioStore.BindingDigest(binding) };
                await store.SaveAsync("bindings", key, binding, token).ConfigureAwait(false);
            }
        }
    }

    private const string Help = """
        Usage: eh estimate portfolio --manifest <portfolio.json> --local <local.json>
                  --checkpoint <private-directory> [options]
        Select whole-codebase month-end snapshots; Change EHE is a separate calculation.
          --as-of <ISO-8601-with-offset>   Freeze observation (default: command start)
          --year <year> --timezone <IANA> Override manifest calendar
          --preflight                     Plan offline without export or estimation
          --previous-result <result.json>  Compare prior area inputs during preflight
          --fetch-missing                  Explicit provider acquisition opt-in
          --output <path>                  Atomically publish complete JSON (default: stdout)
          --import-receipts <result.json>  Validate/import portable public receipts
          --import-historical <estimate>   Retain known v1 legacy provenance without reuse
          --reproduce <result.json>        Reselect original immutable pins and observation
          --upgrade <rebuild|new-epoch>     Explicit incompatible-series migration
          --concurrency <1|2>              Bound simultaneous project sessions (default: 1)
          --timeout-seconds <n>            Run deadline (default: 3600)
          --archive-mib <n>                Per-export bound (default: 256, maximum: 512)
          --checkpoint-mib <n>             Private retained-byte budget (default: 512)
          --memory-mib <n>                 Observed process bound (default: 2048)
          --output-mib <n>                 Output budget (default: 32, maximum: 32)
          --hourly-rate <n>                Apply pricing after EHE; default is rate-free
          --no-rate                        Explicit rate-free output
        Repository EHE remains experimental and uncalibrated.
        """;
}
