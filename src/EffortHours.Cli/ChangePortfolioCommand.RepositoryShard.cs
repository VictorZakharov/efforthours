using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<ChangePortfolioRepositoryOutcome> ExecuteRepositoryShardAsync(
        ChangePortfolioCommandOptions options,
        ResolvedChangeAuthorPeriodManifest resolved,
        ChangeAuthorPeriodManifestRepository repository,
        GitPortfolioPlanner planner,
        ChangePortfolioRepositoryCheckpointStore? checkpoints,
        ChangePathAdmission? pathAdmission,
        string? scopeProfileDigest,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        string digest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
            resolved.Manifest,
            repository.Id,
            options.Profile,
            ChangeEstimator.Version,
            ChangePortfolioComparisonIdentity.ComputeTextDigest(
                (scopeProfileDigest ?? "") + "\n" + ChangePortfolioFinalDelta.Policy + "\n" +
                (options.CalendarReport || options.Bucket == "independent-day" ? "independent-day" : "joint")));
        ChangePortfolioExecutionTelemetry telemetry =
            CreateExecutionTelemetry(standardError, repository.Id);
        if (checkpoints is not null)
        {
            ChangePortfolioRepositoryCheckpointLoad? cached = await checkpoints.TryLoadAsync(
                repository.Id,
                digest,
                options.Profile,
                cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return new ChangePortfolioRepositoryOutcome
                {
                    RepositoryId = repository.Id,
                    InputDigest = digest,
                    Status = ChangePortfolioRepositoryExecutionStatus.Reused,
                    CheckpointDisposition = ChangePortfolioCheckpointDisposition.Hit,
                    Candidates = cached.Candidates,
                    Diagnostics = cached.Diagnostics,
                    Scope = cached.Scope,
                    CheckpointReadBytes = cached.ReadBytes,
                    AdmittedChangeCount = AdmittedChangeCount(cached.Candidates),
                    ScopeEmptyChangeCount =
                        cached.Candidates.Count - AdmittedChangeCount(cached.Candidates),
                    Telemetry = telemetry,
                };
            }
        }

        GitAuthorPeriodManifestRepositoryScope? measuredScope = null;
        try
        {
            ChangeAuthorPeriodManifest submanifest = resolved.Manifest with
            {
                Repositories = [repository],
            };
            string submanifestDigest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(submanifest);
            GitAuthorPeriodManifestPortfolioPlan plan =
                await planner.PlanAuthorPeriodManifestAsync(
                    submanifest,
                    submanifestDigest,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [repository.Id] = resolved.RepositoryPaths[repository.Id],
                    },
                    telemetry,
                    allowEmptySelection: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            measuredScope = plan.RepositoryScopes.Single();
            using (telemetry.Measure(ChangePortfolioExecutionPhases.Preflight))
            {
                if (measuredScope.SelectedChangeCount != plan.Items.Count)
                {
                    throw new InvalidOperationException(
                        "Internal preflight selected-change accounting is inconsistent.");
                }
            }

            GitChangePlan[] scopedPlans = [.. plan.Items.Select(item => item.Plan with
            {
                RepositoryName = repository.Id,
                PathAdmission = pathAdmission,
            })];
            ChangePortfolioEstimateBatch estimate = plan.Items.Count == 0
                ? new ChangePortfolioEstimateBatch
                {
                    Reports = [],
                    Statistics = new ChangePortfolioExecutionStatistics(),
                }
                : await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                    scopedPlans,
                    options.Profile,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
            List<ChangePortfolioCandidate> candidates = [];
            for (int index = 0; index < plan.Items.Count; index++)
            {
                GitAuthorPeriodManifestPortfolioItem item = plan.Items[index];
                candidates.Add(new ChangePortfolioCandidate
                {
                    RepositoryId = item.RepositoryId,
                    SelectorId = item.SelectorId,
                    Report = estimate.Reports[index],
                    Attribution = item.Attribution,
                });
            }

            if (plan.ReplayRanges.Count > 0)
            {
                using IDisposable reviewPhase = telemetry.Measure(ChangePortfolioExecutionPhases.StaticAnalysis);
                candidates = [.. await ChangePortfolioReplayReviewer.AttachAsync(plan, candidates, options.Profile, options.Scope == "engineering", cancellationToken).ConfigureAwait(false)];
            }

            ChangePortfolioPreparedCandidates prepared = await _changeEstimator.PreparePortfolioFinalDeltasAsync(
                plan.Selection, candidates, scopedPlans, options.Profile, estimate.Statistics,
                options.CalendarReport || options.Bucket == "independent-day", pathAdmission, telemetry,
                cancellationToken).ConfigureAwait(false);
            candidates = [.. prepared.Candidates];
            estimate = estimate with { Statistics = prepared.Statistics };

            ChangePortfolioCheckpointDisposition disposition = checkpoints is null
                ? ChangePortfolioCheckpointDisposition.Disabled
                : ChangePortfolioCheckpointDisposition.MissWritten;
            long checkpointWrittenBytes = 0;
            List<Diagnostic> diagnostics = [.. plan.Diagnostics];
            if (plan.Items.Count > 0)
            {
                diagnostics.Add(estimate.Statistics.CreateDiagnostic());
            }

            if (checkpoints is not null)
            {
                try
                {
                    checkpointWrittenBytes = await checkpoints.WriteAsync(
                        repository.Id,
                        digest,
                        options.Profile,
                        candidates,
                        plan.Diagnostics,
                        measuredScope,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or
                        InvalidOperationException or ArgumentException)
                {
                    disposition = ChangePortfolioCheckpointDisposition.MissFailed;
                    diagnostics.Add(new Diagnostic
                    {
                        Code = "FB5334",
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Repository '{repository.Id}' completed, but its resumable evidence checkpoint could not be written.",
                    });
                }
            }

            return new ChangePortfolioRepositoryOutcome
            {
                RepositoryId = repository.Id,
                InputDigest = digest,
                Status = ChangePortfolioRepositoryExecutionStatus.Complete,
                CheckpointDisposition = disposition,
                Candidates = candidates,
                Diagnostics = diagnostics,
                Telemetry = telemetry,
                Statistics = estimate.Statistics,
                Scope = measuredScope,
                CheckpointWrittenBytes = checkpointWrittenBytes,
                AdmittedChangeCount = AdmittedChangeCount(candidates),
                ScopeEmptyChangeCount = candidates.Count - AdmittedChangeCount(candidates),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
                InvalidOperationException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ChangePortfolioProgress? progress = telemetry.GetLastProgress();
            string phase = progress?.Phase ?? ChangePortfolioExecutionPhases.ManifestValidation;
            Exception root = RootException(exception);
            string message = SafeRepositoryFailure(root.Message, repository, resolved);
            ChangePortfolioComparisonFailure failure = new()
            {
                RepositoryId = repository.Id,
                Phase = phase,
                Category = root.GetType().Name,
                Message = message,
                MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                    root.GetType().Name + "\n" + message),
            };
            await standardError.WriteLineAsync(
                $"eh: portfolio repository={repository.Id} failed phase={phase}; " +
                $"diagnostic={failure.MessageDigest}").ConfigureAwait(false);
            return new ChangePortfolioRepositoryOutcome
            {
                RepositoryId = repository.Id,
                InputDigest = digest,
                Status = ChangePortfolioRepositoryExecutionStatus.Failed,
                CheckpointDisposition = checkpoints is null
                    ? ChangePortfolioCheckpointDisposition.Disabled
                    : ChangePortfolioCheckpointDisposition.MissFailed,
                Scope = measuredScope,
                Telemetry = telemetry,
                ScopeEmptyChangeCount = measuredScope?.SelectedChangeCount ?? 0,
                Failure = failure,
            };
        }
    }

}
