using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private async Task EstimateRepositoryCandidatesAsync(
        IReadOnlyList<IndexedPortfolioPlan> repository,
        EstimationProfile profile,
        ChangeEstimateReport?[] reports,
        SnapshotAnalysisCache snapshotAnalyses,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        Action reportCompleted,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource pipelineCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Channel<PreparedPortfolioPlan> prepared = Channel.CreateBounded<PreparedPortfolioPlan>(
            new BoundedChannelOptions(MaximumConcurrentPortfolioChangesPerRepository)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
            });
        Lock failureGate = new();
        ExceptionDispatchInfo? firstFailure = null;

        bool CaptureFirstFailure(Exception exception)
        {
            lock (failureGate)
            {
                if (firstFailure is not null)
                {
                    return false;
                }

                firstFailure = ExceptionDispatchInfo.Capture(exception);
                return true;
            }
        }

        void StopAfterFailure(Exception exception)
        {
            if (!CaptureFirstFailure(exception))
            {
                return;
            }

            try
            {
                pipelineCancellation.Cancel();
            }
            catch (Exception cancellationFailure)
            {
                _ = CaptureFirstFailure(cancellationFailure);
            }
        }

        async Task ConsumeAsync()
        {
            try
            {
                await foreach (PreparedPortfolioPlan entry in prepared.Reader
                    .ReadAllAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    if (pipelineCancellation.IsCancellationRequested)
                    {
                        await DisposeUnclaimedAsync(entry, CaptureFirstFailure)
                            .ConfigureAwait(false);
                        continue;
                    }

                    try
                    {
                        await entry.Dependency.WaitAsync(pipelineCancellation.Token)
                            .ConfigureAwait(false);
                        reports[entry.Entry.Index] = await EstimateCoreAsync(
                            entry.Input,
                            profile,
                            rateCard: null,
                            snapshotAnalyses,
                            entry.Entry.CacheNamespace,
                            executionTelemetry,
                            pipelineCancellation.Token).ConfigureAwait(false);
                        reportCompleted();
                        entry.Complete();
                    }
                    catch (Exception exception)
                    {
                        StopAfterFailure(exception);
                        entry.Cancel();
                        await DisposeUnclaimedAsync(entry, CaptureFirstFailure)
                            .ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                StopAfterFailure(exception);
            }
        }

        Task[] consumers =
        [
            .. Enumerable.Range(0, MaximumConcurrentPortfolioChangesPerRepository)
                .Select(_ => ConsumeAsync()),
        ];
        try
        {
            try
            {
                await ProducePreparedPlansAsync(
                    repository,
                    prepared.Writer,
                    executionTelemetry,
                    pipelineCancellation.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                StopAfterFailure(exception);
            }
            finally
            {
                prepared.Writer.TryComplete();
            }

            await Task.WhenAll(consumers).ConfigureAwait(false);
        }
        finally
        {
            prepared.Writer.TryComplete();
            while (prepared.Reader.TryRead(out PreparedPortfolioPlan? abandoned))
            {
                await DisposeUnclaimedAsync(abandoned, CaptureFirstFailure)
                    .ConfigureAwait(false);
            }
        }

        firstFailure?.Throw();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task ProducePreparedPlansAsync(
        IReadOnlyList<IndexedPortfolioPlan> repository,
        ChannelWriter<PreparedPortfolioPlan> writer,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Task> priorHeadCompletions = new(StringComparer.OrdinalIgnoreCase);
        foreach (IndexedPortfolioPlan[] chunk in repository.Chunk(PortfolioDeltaPrimeChunkSize))
        {
            using (executionTelemetry?.Measure(
                ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
            {
                foreach (GitSnapshotSession session in chunk
                    .Select(entry => entry.Plan.SnapshotSession)
                    .OfType<GitSnapshotSession>()
                    .Distinct())
                {
                    await session.PrimeIncrementalDeltasAsync(
                        chunk.Select(entry => entry.Plan.Selection.Head.ObjectId),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (IndexedPortfolioPlan entry in chunk)
            {
                IChangeSnapshot baseSnapshot;
                using (executionTelemetry?.Measure(
                    ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
                {
                    baseSnapshot = await entry.Plan.OpenBaseAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                IChangeSnapshot headSnapshot;
                try
                {
                    using (executionTelemetry?.Measure(
                        ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
                    {
                        headSnapshot = await entry.Plan.OpenHeadAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch
                {
                    await baseSnapshot.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                Task dependency = priorHeadCompletions.GetValueOrDefault(
                    entry.Plan.Selection.Base.ObjectId) ?? Task.CompletedTask;
                PreparedPortfolioPlan prepared = new(
                    entry,
                    CreateInput(entry.Plan),
                    baseSnapshot,
                    headSnapshot,
                    dependency);
                priorHeadCompletions.TryAdd(
                    entry.Plan.Selection.Head.ObjectId,
                    prepared.Completion);
                try
                {
                    await writer.WriteAsync(prepared, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await prepared.DisposeIfUnclaimedAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }
    }

    private static async Task DisposeUnclaimedAsync(
        PreparedPortfolioPlan prepared,
        Func<Exception, bool> captureFailure)
    {
        try
        {
            await prepared.DisposeIfUnclaimedAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _ = captureFailure(exception);
        }
    }
}
