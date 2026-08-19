using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    private static async Task<FileInspectionResult> InspectFileAsync(
        ScanState state,
        FileInspectionWork work,
        bool bufferContent,
        CancellationToken cancellationToken)
    {
        try
        {
            FileInspection inspection;
            if (!bufferContent)
            {
                inspection = await FileInspection.CreateAsync(
                    state.FileSystem,
                    work.FullPath,
                    state.Options,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using IDisposable readLease = await RepositoryAnalysisConcurrency
                    .AcquireBufferedFileReadAsync(cancellationToken)
                    .ConfigureAwait(false);
                byte[] content = await state.FileSystem.ReadAllBytesAsync(
                    work.FullPath,
                    cancellationToken).ConfigureAwait(false);
                using IDisposable cpuLease = await RepositoryAnalysisConcurrency
                    .AcquireFileAnalysisAsync(
                        RepositoryAnalysisWorkKind.CommonFileInspection,
                        cancellationToken)
                    .ConfigureAwait(false);
                readLease.Dispose();
                inspection = FileInspection.Create(
                    work.FullPath,
                    content,
                    state.Options,
                    cancellationToken);
            }
            if (work.ArtifactKey is not null)
            {
                state.AnalysisArtifactCache?.Add(work.ArtifactKey, inspection);
            }
            work.ArtifactRequest?.Complete(inspection);

            RepositoryFileMetadata refreshedMetadata =
                state.FileSystem.GetFileMetadata(work.FullPath);
            return new FileInspectionResult(
                work.RelativePath,
                CreateScannedFile(
                    work.RelativePath,
                    inspection,
                    refreshedMetadata,
                    work.LastWriteTimeUtcTicks),
                Error: null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            work.ArtifactRequest?.Fail(exception);
            return new FileInspectionResult(work.RelativePath, File: null, exception);
        }
        catch (Exception exception)
        {
            work.ArtifactRequest?.Fail(exception);
            throw;
        }
    }

    private static ScannedFile CreateScannedFile(
        string relativePath,
        FileInspection inspection,
        RepositoryFileMetadata refreshedMetadata,
        long lastWriteTimeUtcTicks) => new(
            relativePath,
            inspection,
            FileClassifier.Classify(relativePath, inspection),
            inspection.Bytes,
            refreshedMetadata.Exists
                ? refreshedMetadata.LastWriteTimeUtcTicks
                : lastWriteTimeUtcTicks);

    private static void ApplyInspectionResult(
        ScanState state,
        FileInspectionResult result)
    {
        if (result.File is not null)
        {
            state.Files.Add(result.File);
            return;
        }

        state.Exclusions.Add(new ExcludedEntry(
            result.RelativePath,
            "unreadable",
            false));
        state.AddUnreadableDiagnostic(result.RelativePath, result.Error!);
    }

    private sealed class FileInspectionPipeline : IAsyncDisposable
    {
        private readonly CancellationToken _callerCancellationToken;
        private readonly CancellationTokenSource _stop;
        private readonly Channel<PreparedFileInspectionWork> _work;
        private readonly ConcurrentQueue<FileInspectionResult> _results = new();
        private readonly ConcurrentQueue<Task<FileInspectionResult>> _pendingResults = new();
        private readonly Task[] _workers;
        private readonly Lock _failureGate = new();
        private Exception? _failure;
        private bool _completed;
        private bool _disposed;

        public FileInspectionPipeline(
            ScanState state,
            CancellationToken cancellationToken)
        {
            _callerCancellationToken = cancellationToken;
            _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _work = Channel.CreateBounded<PreparedFileInspectionWork>(
                new BoundedChannelOptions(
                    RepositoryAnalysisConcurrency.MaximumPendingFileInspections)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true,
                });
            _workers =
            [
                .. Enumerable.Range(
                    0,
                    RepositoryAnalysisConcurrency.MaximumCommonFileInspections)
                    .Select(_ => Task.Run(() => RunWorkerAsync(state))),
            ];
        }

        public async ValueTask EnqueueAsync(
            FileInspectionWork work,
            bool bufferContent)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
            {
                throw new InvalidOperationException(
                    "The file-inspection pipeline has already completed.");
            }

            try
            {
                await _work.Writer.WriteAsync(
                    new PreparedFileInspectionWork(work, bufferContent),
                    _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!_callerCancellationToken.IsCancellationRequested)
            {
                ThrowWorkerFailure();
                throw;
            }
        }

        public void TrackPending(
            ScanState state,
            FileInspectionWork work,
            Task<FileInspection> inspection)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
            {
                throw new InvalidOperationException(
                    "The file-inspection pipeline has already completed.");
            }

            _pendingResults.Enqueue(CreatePendingResultAsync(
                state,
                work,
                inspection,
                _stop.Token));
        }

        public async Task<IReadOnlyList<FileInspectionResult>> CompleteAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
            {
                throw new InvalidOperationException(
                    "The file-inspection pipeline has already completed.");
            }

            _work.Writer.TryComplete();
            try
            {
                await Task.WhenAll(_workers).ConfigureAwait(false);
            }
            catch
            {
                _callerCancellationToken.ThrowIfCancellationRequested();
                ThrowWorkerFailure();
                throw;
            }

            _callerCancellationToken.ThrowIfCancellationRequested();
            _completed = true;
            FileInspectionResult[] pending = await Task.WhenAll(_pendingResults)
                .ConfigureAwait(false);
            return [.. _results, .. pending];
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _work.Writer.TryComplete();
            if (!_completed)
            {
                _stop.Cancel();
            }

            try
            {
                await Task.WhenAll(_workers).ConfigureAwait(false);
            }
            catch
            {
                // CompleteAsync surfaces worker failures. Disposal must not mask
                // an exception already leaving traversal.
            }

            try
            {
                await Task.WhenAll(_pendingResults).ConfigureAwait(false);
            }
            catch
            {
                // Disposal observes follower cancellation/failure without replacing
                // the exception already leaving traversal.
            }

            FailQueuedWork();
            _stop.Dispose();
        }

        private async Task RunWorkerAsync(ScanState state)
        {
            try
            {
                await foreach (PreparedFileInspectionWork work in
                    _work.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
                {
                    FileInspectionResult result = await InspectFileAsync(
                        state,
                        work.Work,
                        work.BufferContent,
                        _stop.Token).ConfigureAwait(false);
                    _results.Enqueue(result);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (_failureGate)
                {
                    _failure ??= exception;
                }

                _work.Writer.TryComplete(exception);
                _stop.Cancel();
                throw;
            }
        }

        private static async Task<FileInspectionResult> CreatePendingResultAsync(
            ScanState state,
            FileInspectionWork work,
            Task<FileInspection> inspectionTask,
            CancellationToken cancellationToken)
        {
            try
            {
                FileInspection inspection = await inspectionTask
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                RepositoryFileMetadata refreshedMetadata =
                    state.FileSystem.GetFileMetadata(work.FullPath);
                return new FileInspectionResult(
                    work.RelativePath,
                    CreateScannedFile(
                        work.RelativePath,
                        inspection,
                        refreshedMetadata,
                        work.LastWriteTimeUtcTicks),
                    Error: null);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return new FileInspectionResult(work.RelativePath, File: null, exception);
            }
        }

        private void ThrowWorkerFailure()
        {
            Exception? failure;
            lock (_failureGate)
            {
                failure = _failure;
            }

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private void FailQueuedWork()
        {
            Exception failure;
            lock (_failureGate)
            {
                failure = _failure ?? new OperationCanceledException(
                    "File inspection stopped before queued immutable work completed.",
                    _callerCancellationToken);
            }

            while (_work.Reader.TryRead(out PreparedFileInspectionWork? abandoned))
            {
                abandoned.Work.ArtifactRequest?.Fail(failure);
            }
        }
    }

    private sealed record FileInspectionWork(
        string FullPath,
        string RelativePath,
        string? ArtifactKey,
        long LastWriteTimeUtcTicks,
        RepositoryAnalysisArtifactCache.RepositoryAnalysisArtifactRequest<FileInspection>?
            ArtifactRequest);

    private sealed record PreparedFileInspectionWork(
        FileInspectionWork Work,
        bool BufferContent);

    private sealed record FileInspectionResult(
        string RelativePath,
        ScannedFile? File,
        Exception? Error);
}
