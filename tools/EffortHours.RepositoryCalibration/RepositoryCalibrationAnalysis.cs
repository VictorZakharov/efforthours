using System.Text.Json;

namespace EffortHours.RepositoryCalibration;

internal static partial class RepositoryCalibrationReproducer
{
    private static async Task<DevelopmentAnalysis> AnalyzeBlindAsync(
        ReproductionOptions options,
        SamplingFamily family,
        string snapshotPath,
        string slug,
        CancellationToken cancellationToken)
    {
        string outputDirectory = Path.Combine(options.WorkspacePath, "outputs", slug);
        Directory.CreateDirectory(outputDirectory);
        string evidencePath = Path.Combine(outputDirectory, "repository-evidence.json");
        string estimatePath = Path.Combine(outputDirectory, "source-estimate.json");
        string packetPath = Path.Combine(options.PacketDirectory, $"{slug}.blind-authoring.json");
        DeleteGeneratedFile(evidencePath);
        DeleteGeneratedFile(estimatePath);
        DeleteGeneratedFile(packetPath);

        await RunEffortHoursAsync(
            options.CliPath,
            ["scan", snapshotPath, "--output", evidencePath],
            cancellationToken).ConfigureAwait(false);
        await RunEffortHoursAsync(
            options.CliPath,
            [
                "estimate", evidencePath,
                "--profile", "implementation",
                "--no-rate",
                "--compact",
                "--output", estimatePath,
            ],
            cancellationToken).ConfigureAwait(false);
        await RunEffortHoursAsync(
            options.CliPath,
            [
                "calibration", "scaffold", estimatePath,
                "--blind",
                "--repository-family", family.Id,
                "--repository-name", family.RepositoryName,
                "--compact",
                "--output", packetPath,
            ],
            cancellationToken).ConfigureAwait(false);

        using JsonDocument evidence = JsonDocument.Parse(
            await File.ReadAllTextAsync(evidencePath, cancellationToken).ConfigureAwait(false));
        using JsonDocument estimate = JsonDocument.Parse(
            await File.ReadAllTextAsync(estimatePath, cancellationToken).ConfigureAwait(false));
        using JsonDocument packet = JsonDocument.Parse(
            await File.ReadAllTextAsync(packetPath, cancellationToken).ConfigureAwait(false));
        ValidateBlindPacket(packet.RootElement, family);

        JsonElement packetCandidate = packet.RootElement.GetProperty("candidate");
        JsonElement rubric = packet.RootElement.GetProperty("rubric");
        string sourceDigest = evidence.RootElement.GetProperty("repository")
            .GetProperty("sourceDigest").GetString()!;
        string estimateSourceDigest = estimate.RootElement.GetProperty("repository")
            .GetProperty("sourceDigest").GetString()!;
        if (!string.Equals(sourceDigest, estimateSourceDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Evidence/estimate source mismatch for {family.RepositoryName}.");
        }

        return new DevelopmentAnalysis
        {
            EvidenceSourceDigest = sourceDigest,
            EvidenceArtifactSha256 = await JsonArtifactDigest.ComputeFileAsync(
                evidencePath,
                cancellationToken).ConfigureAwait(false),
            EstimatorVersion = estimate.RootElement.GetProperty("estimatorVersion").GetString()!,
            EstimateDigest = packetCandidate.GetProperty("estimateDigest").GetString()!,
            EstimateArtifactSha256 = await JsonArtifactDigest.ComputeFileAsync(
                estimatePath,
                cancellationToken).ConfigureAwait(false),
            AuthoringVersion = packet.RootElement.GetProperty("authoringVersion").GetString()!,
            Rubric = $"{rubric.GetProperty("id").GetString()}/{rubric.GetProperty("version").GetString()}",
            PacketFile = Path.GetFileName(packetPath),
            PacketSha256 = await JsonArtifactDigest.ComputeFileAsync(packetPath, cancellationToken)
                .ConfigureAwait(false),
            PacketTargetCount = packet.RootElement.GetProperty("targets").GetArrayLength(),
        };
    }

    private static void ValidateBlindPacket(JsonElement packet, SamplingFamily family)
    {
        if (packet.GetProperty("candidateVisibility").GetString() != "blind" ||
            packet.GetProperty("candidate").GetProperty("totalHours").ValueKind != JsonValueKind.Null ||
            packet.GetProperty("candidate").GetProperty("categories").GetArrayLength() != 0 ||
            packet.GetProperty("repository").GetProperty("id").GetString() != family.Id)
        {
            throw new InvalidDataException($"Packet for {family.RepositoryName} is not estimate-blind.");
        }

        foreach (JsonElement target in packet.GetProperty("targets").EnumerateArray())
        {
            JsonElement candidate = target.GetProperty("candidate");
            if (candidate.GetProperty("hours").ValueKind != JsonValueKind.Null ||
                candidate.GetProperty("confidence").ValueKind != JsonValueKind.Null ||
                candidate.GetProperty("reason").ValueKind != JsonValueKind.Null)
            {
                throw new InvalidDataException(
                    $"Packet target for {family.RepositoryName} exposes candidate guidance.");
            }
        }
    }

    private static Task<string> RunEffortHoursAsync(
        string cliPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) => ExternalProcess.RunAsync(
            "dotnet",
            [cliPath, .. arguments],
            cancellationToken);

    private static void DeleteGeneratedFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
