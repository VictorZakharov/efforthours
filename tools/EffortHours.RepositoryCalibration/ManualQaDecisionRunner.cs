using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed record ManualQaDecisionArtifacts
{
    public required CalibrationCorpus SourceCorpus { get; init; }

    public required ManualQaReviewPolicy ReviewPolicy { get; init; }

    public required ManualQaReviewManifest ReviewManifest { get; init; }

    public required IReadOnlyList<ManualQaReviewPacket> Packets { get; init; }

    public required ManualQaDecisionCompilerPolicy CompilerPolicy { get; init; }
}

internal static class ManualQaDecisionRunner
{
    public static async Task FreezeTemplateAsync(
        ManualQaDecisionTemplateOptions options,
        CancellationToken cancellationToken)
    {
        ManualQaDecisionArtifacts artifacts = await LoadAsync(
            options.CorpusPath,
            options.ReviewPolicyPath,
            options.ExpectedReviewPolicyDigest,
            options.ReviewManifestPath,
            options.PacketDirectory,
            options.CompilerPolicyPath,
            options.ExpectedCompilerPolicyDigest,
            cancellationToken).ConfigureAwait(false);
        ManualQaDecisionPlan template = ManualQaDecisionAuthoring.CreateTemplate(
            artifacts.SourceCorpus,
            artifacts.ReviewPolicy,
            options.ExpectedReviewPolicyDigest,
            artifacts.ReviewManifest,
            artifacts.Packets,
            artifacts.CompilerPolicy,
            options.ExpectedCompilerPolicyDigest);
        string document = ContractJson.SerializeDocument(template);
        ValidateSchema(SchemaNames.CalibrationManualQaDecisionPlan, document, "decision template");
        await WriteImmutableAsync(options.OutputPath, document, cancellationToken).ConfigureAwait(false);
    }

    public static async Task CompileAsync(
        ManualQaDecisionCompileOptions options,
        CancellationToken cancellationToken)
    {
        ManualQaDecisionArtifacts artifacts = await LoadAsync(
            options.CorpusPath,
            options.ReviewPolicyPath,
            options.ExpectedReviewPolicyDigest,
            options.ReviewManifestPath,
            options.PacketDirectory,
            options.CompilerPolicyPath,
            options.ExpectedCompilerPolicyDigest,
            cancellationToken).ConfigureAwait(false);
        string planJson = await File.ReadAllTextAsync(options.PlanPath, cancellationToken)
            .ConfigureAwait(false);
        ValidateDigest(planJson, options.ExpectedPlanDigest, "manual-QA decision plan");
        ValidateSchema(SchemaNames.CalibrationManualQaDecisionPlan, planJson, "decision plan");
        ManualQaDecisionPlan plan = ContractJson.Deserialize<ManualQaDecisionPlan>(planJson);
        CalibrationCorpus output = ManualQaDecisionCompiler.Compile(
            artifacts.SourceCorpus,
            artifacts.ReviewPolicy,
            options.ExpectedReviewPolicyDigest,
            artifacts.ReviewManifest,
            artifacts.Packets,
            artifacts.CompilerPolicy,
            options.ExpectedCompilerPolicyDigest,
            plan);
        string document = ContractJson.SerializeDocument(output);
        ValidateSchema(SchemaNames.CalibrationCorpus, document, "compiled corpus");
        await WriteImmutableAsync(options.OutputPath, document, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ManualQaDecisionArtifacts> LoadAsync(
        string corpusPath,
        string reviewPolicyPath,
        string reviewPolicyDigest,
        string reviewManifestPath,
        string packetDirectory,
        string compilerPolicyPath,
        string compilerPolicyDigest,
        CancellationToken cancellationToken)
    {
        string corpusJson = await File.ReadAllTextAsync(corpusPath, cancellationToken)
            .ConfigureAwait(false);
        string reviewPolicyJson = await File.ReadAllTextAsync(reviewPolicyPath, cancellationToken)
            .ConfigureAwait(false);
        string manifestJson = await File.ReadAllTextAsync(reviewManifestPath, cancellationToken)
            .ConfigureAwait(false);
        string compilerPolicyJson = await File.ReadAllTextAsync(compilerPolicyPath, cancellationToken)
            .ConfigureAwait(false);
        ValidateDigest(reviewPolicyJson, reviewPolicyDigest, "manual-QA review policy");
        ValidateDigest(compilerPolicyJson, compilerPolicyDigest, "manual-QA compiler policy");
        ValidateSchema(SchemaNames.CalibrationCorpus, corpusJson, "source corpus");
        ValidateSchema(SchemaNames.CalibrationManualQaReviewPolicy, reviewPolicyJson, "review policy");
        ValidateSchema(SchemaNames.CalibrationManualQaReviewManifest, manifestJson, "review manifest");
        ValidateSchema(
            SchemaNames.CalibrationManualQaDecisionPolicy,
            compilerPolicyJson,
            "compiler policy");

        ManualQaReviewManifest manifest = ContractJson.Deserialize<ManualQaReviewManifest>(manifestJson);
        List<ManualQaReviewPacket> packets = [];
        HashSet<string> expectedFileNames = manifest.Packets.Select(entry => entry.FileName)
            .ToHashSet(StringComparer.Ordinal);
        string? unexpected = Directory
            .EnumerateFiles(packetDirectory, "*.manual-qa-review.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .FirstOrDefault(fileName => !expectedFileNames.Contains(fileName));
        if (unexpected is not null)
        {
            throw new InvalidDataException(
                $"Manual-QA packet directory contains unexpected artifact '{unexpected}'.");
        }

        foreach (ManualQaReviewManifestPacket entry in manifest.Packets)
        {
            string path = Path.Combine(packetDirectory, entry.FileName);
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            ValidateSchema(SchemaNames.CalibrationManualQaReviewPacket, json, $"packet '{entry.FileName}'");
            ManualQaReviewPacket packet = ContractJson.Deserialize<ManualQaReviewPacket>(json);
            if (CalibrationDigest.Compute(packet) != entry.PacketDigest)
            {
                throw new InvalidDataException($"Manual-QA packet '{entry.FileName}' digest differs.");
            }

            packets.Add(packet);
        }

        return new ManualQaDecisionArtifacts
        {
            SourceCorpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson),
            ReviewPolicy = ContractJson.Deserialize<ManualQaReviewPolicy>(reviewPolicyJson),
            ReviewManifest = manifest,
            Packets = packets,
            CompilerPolicy = ContractJson.Deserialize<ManualQaDecisionCompilerPolicy>(compilerPolicyJson),
        };
    }

    internal static void ValidateDigest(string json, string expected, string description)
    {
        string actual = JsonArtifactDigest.Compute(json);
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"The {description} digest differs: expected {expected}; actual {actual}.");
        }
    }

    private static void ValidateSchema(string schema, string json, string description)
    {
        SchemaValidationResult result = ContractSchemaValidator.Validate(schema, json);
        if (!result.IsValid)
        {
            throw new InvalidDataException(
                $"The {description} is schema-invalid: {string.Join("; ", result.Errors)}");
        }
    }

    private static async Task WriteImmutableAsync(
        string path,
        string document,
        CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            string existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (existing != document)
            {
                throw new InvalidDataException(
                    $"Refusing to overwrite different frozen manual-QA artifact '{path}'.");
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, document, cancellationToken).ConfigureAwait(false);
    }
}
