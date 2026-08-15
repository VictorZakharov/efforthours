using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class ValidationSelectionVerifierTests
{
    [Fact]
    public void ValidationSelectionHasNoTestInputOption()
    {
        string[] arguments =
        [
            "--repository-root", ".",
            "--plan", "plan.json",
            "--opening", "opening.json",
            "--corpus", "corpus.json",
            "--candidate-manifest", "manifest.json",
            "--model", "model.json",
            "--seed-outputs", "seed",
            "--candidate-outputs", "candidate",
            "--seed-evaluation", "seed-evaluation.json",
            "--candidate-evaluation", "candidate-evaluation.json",
            "--decision", "decision.json",
            "--source-commit", new string('a', 40),
            "--test-corpus", "test.json",
        ];

        Assert.False(ValidationSelectionOptions.TryParse(
            arguments,
            out ValidationSelectionOptions? options,
            out string? error));
        Assert.Null(options);
        Assert.Contains("--test-corpus", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCandidateOutputRejectsBeforeFrozenInputsAreRead()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(
            root,
            "artifacts",
            "validation-selection-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string seedOutputs = Path.Combine(temporary, "seed");
            string candidateOutputs = Path.Combine(temporary, "candidate");
            Directory.CreateDirectory(seedOutputs);
            Directory.CreateDirectory(candidateOutputs);
            string[] inputs = ["plan", "opening", "corpus", "manifest", "model"];
            foreach (string input in inputs)
            {
                await File.WriteAllTextAsync(Path.Combine(temporary, input + ".json"), "{}");
            }

            ValidationSelectionOptions options = new()
            {
                RepositoryRoot = root,
                SamplingPlanPath = Path.Combine(temporary, "plan.json"),
                OpeningPath = Path.Combine(temporary, "opening.json"),
                CorpusPath = Path.Combine(temporary, "corpus.json"),
                CandidateManifestPath = Path.Combine(temporary, "manifest.json"),
                ModelPath = Path.Combine(temporary, "model.json"),
                SeedOutputDirectory = seedOutputs,
                CandidateOutputDirectory = candidateOutputs,
                SeedEvaluationPath = Path.Combine(temporary, "seed-evaluation.json"),
                CandidateEvaluationPath = Path.Combine(temporary, "candidate-evaluation.json"),
                DecisionPath = Path.Combine(temporary, "decision.json"),
                SourceCommit = new string('a', 40),
            };

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ValidationSelectionVerifier.VerifyAsync(options, CancellationToken.None));

            Assert.Contains("one-shot", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(options.SeedEvaluationPath));
            Assert.False(File.Exists(options.CandidateEvaluationPath));
            Assert.False(File.Exists(options.DecisionPath));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
