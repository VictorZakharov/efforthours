using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasurementProcess
{
    public static async Task<CandidateProjectionProcessResult> RunProjectionAsync(
        string calibrationAssemblyPath,
        CandidateMeasurementInput input,
        bool candidate,
        string modelPath,
        string modelDigest,
        int sequence,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            calibrationAssemblyPath,
            "candidate-benchmark-project",
            "--evidence", input.Path,
            "--mode", candidate ? "candidate" : "seed",
        ];
        if (candidate)
        {
            arguments.AddRange(["--model", modelPath, "--expected-model-digest", modelDigest]);
        }

        MeasuredProcessResult process = await RunAsync(
            "dotnet",
            arguments,
            cancellationToken).ConfigureAwait(false);
        if (process.PeakWorkingSetBytes <= 0)
        {
            throw new InvalidDataException(
                "Fresh-process peak working set could not be sampled.");
        }

        EstimateReport report = ContractJson.Deserialize<EstimateReport>(process.Output);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Candidate measurement projection is invalid: {string.Join("; ", errors)}");
        }

        string digest = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(process.Output))).ToLowerInvariant();
        string normalizedDigest = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeLineEndings(process.Output))))
            .ToLowerInvariant();
        CandidateMeasurementRun run = new()
        {
            Sequence = sequence,
            ElapsedMilliseconds = Round((decimal)process.Elapsed.TotalMilliseconds),
            PeakWorkingSetMib = Round(process.PeakWorkingSetBytes / 1024m / 1024m),
            OutputDigest = digest,
            LfNormalizedOutputDigest = normalizedDigest,
        };
        return new CandidateProjectionProcessResult(run, report.WorkItems.Count);
    }

    public static async Task<MeasuredProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        Stopwatch timer = Stopwatch.StartNew();
        if (!process.Start())
        {
            throw new IOException($"Could not start '{fileName}'.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        long peak = SampleWorkingSet(process, 0);
        try
        {
            while (!process.HasExited)
            {
                peak = SampleWorkingSet(process, peak);
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken)
                    .ConfigureAwait(false);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        finally
        {
            timer.Stop();
        }

        string output = await outputTask.ConfigureAwait(false);
        string error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"'{fileName}' exited with code {process.ExitCode}: {error.Trim()}");
        }

        return new MeasuredProcessResult(output, error, timer.Elapsed, peak);
    }

    internal static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static long SampleWorkingSet(Process process, long currentPeak)
    {
        try
        {
            process.Refresh();
            return Math.Max(currentPeak, Math.Max(process.WorkingSet64, process.PeakWorkingSet64));
        }
        catch (InvalidOperationException)
        {
            return currentPeak;
        }
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

internal sealed record CandidateProjectionProcessResult(
    CandidateMeasurementRun Run,
    int WorkItemCount);

internal sealed record MeasuredProcessResult(
    string Output,
    string Error,
    TimeSpan Elapsed,
    long PeakWorkingSetBytes);
