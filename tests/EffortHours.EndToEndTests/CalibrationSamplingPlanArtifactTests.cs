using System.Text.Json;
using System.Text.RegularExpressions;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class CalibrationSamplingPlanArtifactTests
{
    private const string PlanRelativePath = "calibration/corpora/public-readiness/0.1.0.sampling-plan.json";
    private const string SizeMetric = "repository-source-shape-file-count/1.0.0";

    private static readonly string[] Strata =
    [
        "dotnet",
        "javascript-typescript",
        "mixed-dotnet-javascript-typescript",
    ];

    private static readonly string[] Partitions = ["development", "validation", "test"];
    private static readonly string[] SizeBands = ["small", "medium", "large"];

    [Fact]
    public void PublicReadinessSamplingPlanFreezesCompleteUnlabeledMatrix()
    {
        string repositoryRoot = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(repositoryRoot, PlanRelativePath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement plan = document.RootElement;

        Assert.Equal("repository-sampling-plan/1.0.0", plan.GetProperty("samplingPlanVersion").GetString());
        Assert.Equal("repository-model-admission/1.0.0", plan.GetProperty("policy").GetString());
        Assert.Equal("source-cohort-frozen-before-candidate-totals", plan.GetProperty("status").GetString());
        Assert.Equal("implementation", plan.GetProperty("profile").GetString());

        JsonElement[] families = [.. plan.GetProperty("families").EnumerateArray()];
        Assert.Equal(33, families.Length);
        Assert.Equal(33, families.Select(StringProperty("id")).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            33,
            families.Select(StringProperty("repositoryUrl")).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        AssertMatrixCounts(families);
        AssertShapeCoverage(plan, families);
        AssertFamilyProvenance(families);
        AssertExistingPartitionAssignments(repositoryRoot, families);
    }

    private static void AssertMatrixCounts(JsonElement[] families)
    {
        foreach (string stratum in Strata)
        {
            Assert.Equal(11, families.Count(item => Is(item, "primaryStratum", stratum)));
            Assert.Equal(5, CountCell(families, stratum, "development"));
            Assert.Equal(3, CountCell(families, stratum, "validation"));
            Assert.Equal(3, CountCell(families, stratum, "test"));

            foreach (string partition in Partitions)
            {
                JsonElement[] cell =
                [
                    .. families.Where(item =>
                        Is(item, "primaryStratum", stratum) && Is(item, "partition", partition)),
                ];
                Assert.True(
                    cell.Select(StringProperty("productShape")).Distinct(StringComparer.Ordinal).Count() >= 2,
                    $"{stratum}/{partition} does not contain two distinct product shapes.");
                Assert.True(
                    cell.SelectMany(item => item.GetProperty("shapeTags").EnumerateArray())
                        .Select(item => item.GetString())
                        .Distinct(StringComparer.Ordinal)
                        .Count() >= 2,
                    $"{stratum}/{partition} does not contain two policy shape tags.");

                if (partition != "development")
                {
                    Assert.Equal(
                        SizeBands,
                        cell.Select(item => item.GetProperty("size").GetProperty("band").GetString()!)
                            .OrderBy(BandOrder));
                }
            }
        }

        foreach (string partition in new[] { "validation", "test" })
        {
            foreach (string band in SizeBands)
            {
                Assert.Equal(
                    3,
                    families.Count(item =>
                        Is(item, "partition", partition) && Is(item.GetProperty("size"), "band", band)));
            }
        }
    }

    private static void AssertShapeCoverage(JsonElement plan, JsonElement[] families)
    {
        string[] requiredTags =
        [
            .. plan.GetProperty("requiredShapeTags").EnumerateArray().Select(item => item.GetString()!),
        ];
        Assert.Equal(6, requiredTags.Length);

        foreach (string tag in requiredTags)
        {
            Assert.True(
                families.Count(item => HasTag(item, tag)) >= 3,
                $"Shape tag '{tag}' does not cover three families.");
            Assert.Contains(families, item => !Is(item, "partition", "development") && HasTag(item, tag));
        }
    }

    private static void AssertFamilyProvenance(JsonElement[] families)
    {
        HashSet<string> permittedLicenses = new(StringComparer.Ordinal)
        {
            "Apache-2.0",
            "BSD-3-Clause",
            "MIT",
        };

        foreach (JsonElement family in families)
        {
            string id = family.GetProperty("id").GetString()!;
            string name = family.GetProperty("repositoryName").GetString()!;
            Assert.Equal($"repository:github.com/{name.ToLowerInvariant()}", id);

            JsonElement source = family.GetProperty("sourceSnapshot");
            string commit = source.GetProperty("commitSha").GetString()!;
            Assert.Matches(GitObjectPattern(), commit);
            Assert.Matches(GitObjectPattern(), source.GetProperty("gitTreeSha1").GetString()!);
            Assert.True(source.GetProperty("treeListingComplete").GetBoolean());
            Assert.EndsWith(commit, source.GetProperty("archiveUrl").GetString()!, StringComparison.Ordinal);

            JsonElement license = family.GetProperty("license");
            Assert.Contains(license.GetProperty("expression").GetString()!, permittedLicenses);
            Assert.Matches(GitObjectPattern(), license.GetProperty("gitBlobSha1").GetString()!);
            Assert.Matches(Sha256Pattern(), license.GetProperty("contentSha256").GetString()!);
            Assert.True(license.GetProperty("redistributionAllowed").GetBoolean());

            JsonElement size = family.GetProperty("size");
            Assert.Equal(SizeMetric, size.GetProperty("metric").GetString());
            int files = size.GetProperty("eligibleFiles").GetInt32();
            Assert.True(files > 0);
            Assert.True(size.GetProperty("eligibleBytes").GetInt64() > 0);
            Assert.Equal(ExpectedBand(files), size.GetProperty("band").GetString());

            if (Is(family, "primaryStratum", "mixed-dotnet-javascript-typescript"))
            {
                JsonElement ecosystems = size.GetProperty("ecosystemFiles");
                int dotnetFiles = ecosystems.GetProperty("dotnet").GetInt32();
                int javascriptFiles = ecosystems.GetProperty("javascriptTypescript").GetInt32();
                Assert.True(dotnetFiles > 0);
                Assert.True(javascriptFiles > 0);
                Assert.Equal(files, dotnetFiles + javascriptFiles);
            }

            string partition = family.GetProperty("partition").GetString()!;
            string expectedLabelStatus = partition switch
            {
                "development" => "not-authored-development",
                "validation" => "not-authored-blind-validation",
                "test" => "not-authored-sealed-test",
                _ => throw new InvalidOperationException($"Unexpected partition '{partition}'."),
            };
            Assert.Equal(expectedLabelStatus, family.GetProperty("labelStatus").GetString());
            Assert.False(family.TryGetProperty("targets", out _));
            Assert.False(family.TryGetProperty("hours", out _));
            Assert.False(family.TryGetProperty("estimateDigest", out _));
            Assert.False(family.TryGetProperty("sourceDigest", out _));
        }

        JsonElement[] inherited =
        [
            .. families.Where(item =>
                Is(item.GetProperty("partitionAssignment"), "kind", "inherited")),
        ];
        Assert.Equal(
            [
                "repository:github.com/axios/axios",
                "repository:github.com/colinhacks/zod",
                "repository:github.com/spectreconsole/spectre.console",
            ],
            inherited.Select(StringProperty("id")).Order(StringComparer.Ordinal));
        Assert.All(inherited, item => Assert.Equal(
            "calibration/changes/public-real-expansion/0.1.0.selection.json",
            item.GetProperty("partitionAssignment").GetProperty("source").GetString()));
    }

    private static void AssertExistingPartitionAssignments(string repositoryRoot, JsonElement[] families)
    {
        Dictionary<string, string> assignments = new(StringComparer.OrdinalIgnoreCase);
        string[] corpusPaths =
        [
            "calibration/corpora/public-pilot/0.1.0.corpus.json",
            "calibration/corpora/public-expansion/0.1.0.corpus.json",
            "calibration/changes/public-real/0.1.0.teacher-corpus.json",
            "calibration/changes/public-real-expansion/0.1.0.teacher-corpus.json",
            "calibration/changes/public-real-alpha3/0.1.0.teacher-corpus.json",
        ];

        foreach (string path in corpusPaths)
        {
            CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(
                File.ReadAllText(Path.Combine(repositoryRoot, path)));
            foreach (CalibrationRecord record in corpus.Records)
            {
                AddAssignment(assignments, record.Repository.Id, record.Partition.ToString().ToLowerInvariant());
            }
        }

        foreach (JsonElement family in families)
        {
            AddAssignment(
                assignments,
                family.GetProperty("id").GetString()!,
                family.GetProperty("partition").GetString()!);
        }
    }

    private static void AddAssignment(Dictionary<string, string> assignments, string id, string partition)
    {
        if (assignments.TryGetValue(id, out string? existing))
        {
            Assert.Equal(existing, partition);
            return;
        }

        assignments.Add(id, partition);
    }

    private static int CountCell(JsonElement[] families, string stratum, string partition) =>
        families.Count(item => Is(item, "primaryStratum", stratum) && Is(item, "partition", partition));

    private static Func<JsonElement, string?> StringProperty(string name) =>
        item => item.GetProperty(name).GetString();

    private static bool Is(JsonElement item, string property, string value) =>
        string.Equals(item.GetProperty(property).GetString(), value, StringComparison.Ordinal);

    private static bool HasTag(JsonElement family, string tag) =>
        family.GetProperty("shapeTags").EnumerateArray().Any(item => item.GetString() == tag);

    private static int BandOrder(string band) => Array.IndexOf(SizeBands, band);

    private static string ExpectedBand(int files) => files switch
    {
        < 250 => "small",
        < 2000 => "medium",
        _ => "large",
    };

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitObjectPattern();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
