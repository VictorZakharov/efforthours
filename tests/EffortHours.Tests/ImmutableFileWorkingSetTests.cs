using System.Collections.Concurrent;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class ImmutableFileWorkingSetTests
{
    private const string Project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";

    [Fact]
    public async Task StableMixedFilesAreReadOnceBeyondTheHistoricalCacheLimit()
    {
        Dictionary<string, string> files = Files();
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        ConcurrentDictionary<string, int> reads = new(StringComparer.Ordinal);
        await Scan(files, cache, reads);
        int entries = reads["entries"];
        int csMetadata = reads["metadata:app/File1.cs"];
        int csReads = reads["app/File1.cs"];
        int tsReads = reads["web/File1.ts"];
        int sqlReads = reads["app/sql/File1.sql"];
        int htmlReads = reads["web/component.html"];
        files["app/File0.cs"] = "public class Changed { public int Extra() => 1; }";
        RepositoryEvidence warm = await Scan(files, cache, reads);
        RepositoryEvidence cold = await Scan(files);
        Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(warm));
        Assert.Equal(entries, reads["entries"]);
        Assert.Equal(csMetadata, reads["metadata:app/File1.cs"]);
        Assert.Equal(csReads, reads["app/File1.cs"]);
        Assert.Equal(tsReads, reads["web/File1.ts"]);
        Assert.Equal(sqlReads, reads["app/sql/File1.sql"]);
        Assert.Equal(htmlReads, reads["web/component.html"]);
        Assert.True(cache.GetStatistics().Hits >= 70);
        Assert.Equal(4, cache.GetStatistics().PeakEntries);
    }

    [Fact]
    public async Task MutationsRecomputeOwnershipRolesDuplicatesAndCompleteStockExactly()
    {
        Dictionary<string, string> files = Files();
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        await Scan(files, cache);
        Action<Dictionary<string, string>>[] mutations =
        [
            values => values["app/sql/File0.sql"] = "CREATE TABLE changed (id INT, name TEXT);",
            values => values["app/sql/A-copy.sql"] = values["app/sql/File1.sql"],
            values => values.Remove("app/sql/File1.sql"),
            values => values["app/App.csproj"] = Project.Replace("</PropertyGroup>",
                "<IsTestProject>true</IsTestProject></PropertyGroup>", StringComparison.Ordinal),
            values => values["web/package.json"] = "{\"name\":\"web\",\"dependencies\":{\"express\":\"1.0.0\"}}",
            values => values["web/File0.ts"] = "export function changed(x: number) { return x + 3; }",
            values => values["app/sql/Inner/Nested.csproj"] = Project,
            values => values["app/sql/Inner/query.sql"] = "SELECT * FROM changed WHERE id > 2;",
            values => values[".efforthoursignore"] = "web/File2.ts\napp/sql/File2.sql\n",
            values => values.Remove("app/App.csproj"),
            values => values["web/component.ts"] = "import { Component } from '@angular/core'; " +
                "@Component({ selector: 'app-main', templateUrl: './changed.html' }) export class Main {}",
            values => values["web/changed.html"] = "<form><input aria-label='Name'></form>",
            values => values.Remove("web/component.html"),
            values => values["web/main.css"] = ".changed { display: grid; color: blue; }",

        ];
        foreach (Action<Dictionary<string, string>> mutate in mutations)
        {
            mutate(files);
            RepositoryEvidence warm = await Scan(files, cache);
            RepositoryEvidence cold = await Scan(files);
            Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(warm));
            Assert.Empty(ContractValidation.Validate(warm));
            SeedEstimator estimator = new();
            EstimateReport warmStock = estimator.Estimate(warm, EstimationProfile.Implementation);
            EstimateReport coldStock = estimator.Estimate(cold, EstimationProfile.Implementation);
            Assert.Equal(ContractJson.Serialize(coldStock), ContractJson.Serialize(warmStock));
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence,
                ContractJson.Serialize(warm)).IsValid);
        }
    }

    [Fact]
    public async Task IncompleteTraversalDoesNotFreezeAnUnreadableFileExclusion()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal) { ["unreadable.txt"] = "fixture" };
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        ConcurrentDictionary<string, int> reads = new(StringComparer.Ordinal);
        RepositoryEvidence failed = await Scan(files, cache, reads, failRead: true);
        Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Code == "FB2001");
        int priorEntries = reads["entries"];
        RepositoryEvidence recovered = await Scan(files, cache, reads);
        Assert.Contains(recovered.Facts, fact => fact.Id == "file:unreadable.txt");
        Assert.DoesNotContain(recovered.Diagnostics, diagnostic => diagnostic.Code == "FB2001");
        Assert.True(reads["entries"] > priorEntries);
        Assert.Equal(ContractJson.Serialize(await Scan(files)), ContractJson.Serialize(recovered));
    }

    private static Dictionary<string, string> Files()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["app/App.csproj"] = Project,
            ["web/package.json"] = "{\"name\":\"web\",\"dependencies\":{\"@angular/core\":\"1.0.0\"}}",
            ["web/component.ts"] = "import { Component } from '@angular/core'; " +
                "@Component({ selector: 'app-main', templateUrl: './component.html', styleUrls: ['./main.css'] }) " +
                "export class Main {}",
            ["web/component.html"] = "<form><label for='name'>Name</label><input id='name'></form>",
            ["web/main.css"] = ".main { display: flex; color: red; }",

        };
        for (int index = 0; index < 16; index++)
        {
            files[$"app/File{index}.cs"] = $"public class File{index} {{ public int Run(int x) => x + {index}; }}";
            files[$"web/File{index}.ts"] = $"export function file{index}(x: number) {{ return x + {index}; }}";
            files[$"app/sql/File{index}.sql"] = $"CREATE TABLE table{index} (id INT PRIMARY KEY);";
        }
        return files;
    }

    private static async Task<RepositoryEvidence> Scan(Dictionary<string, string> files,
        RepositoryAnalysisArtifactCache? cache = null,
        ConcurrentDictionary<string, int>? reads = null, bool failRead = false)
    {
        GitArchiveSnapshot snapshot = new(files.ToDictionary(pair => pair.Key,
            pair => Encoding.UTF8.GetBytes(pair.Value), StringComparer.Ordinal), cache);
        CountingFileSystem fs = new(snapshot, reads ?? new(StringComparer.Ordinal), failRead);
        return await new RepositoryAnalysisPipeline(fs, analysisArtifactCache: cache)
            .ScanAsync(snapshot.RootPath);
    }

    private sealed class CountingFileSystem(GitArchiveSnapshot inner,
        ConcurrentDictionary<string, int> reads, bool failRead) : IRepositoryFileSystem,
        IRepositoryAnalysisArtifactCacheProvider, IRepositoryImmutableIdentityProvider, IRepositoryTraversalIdentityProvider
    {
        public RepositoryAnalysisArtifactCache? AnalysisArtifactCache => inner.AnalysisArtifactCache;
        public string RepositoryPathSetIdentity { get; } = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', inner.Files.Keys.Order(StringComparer.Ordinal)))));
        public bool TryGetFileContentId(string path, out string contentId)
        {
            if (!inner.FileExists(path)) { contentId = string.Empty; return false; }
            contentId = inner.GetFileMetadata(path).ContentId!;
            return true;
        }
        public string RepositoryTraversalIdentity { get; } = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', inner.Files.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + (System.IO.Path.GetFileName(pair.Key) is ".gitignore" or ".efforthoursignore"
                        ? "\0" + Convert.ToHexString(pair.Value) : string.Empty))))));
        public string GetFullPath(string path) => inner.GetFullPath(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public bool FileExists(string path) => inner.FileExists(path);
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public string[] GetFileSystemEntries(string directoryPath)
        {
            reads.AddOrUpdate("entries", 1, (_, count) => count + 1);
            return inner.GetFileSystemEntries(directoryPath);
        }
        public RepositoryFileMetadata GetFileMetadata(string path)
        {
            reads.AddOrUpdate("metadata:" + Path.GetRelativePath(inner.RootPath, path).Replace('\\', '/'),
                1, (_, count) => count + 1);
            return inner.GetFileMetadata(path);
        }
        private void Count(string path) => reads.AddOrUpdate(
            Path.GetRelativePath(inner.RootPath, path).Replace('\\', '/'), 1, (_, count) => count + 1);
        public Stream OpenRead(string path, int bufferSize)
        {
            Count(path);
            return inner.OpenRead(path, bufferSize);
        }
        public ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            Count(path);
            if (failRead) throw new IOException("Synthetic unavailable object.");
            return inner.ReadAllBytesAsync(path, cancellationToken);
        }
        public ValueTask<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
        {
            Count(path);
            return inner.ReadAllLinesAsync(path, cancellationToken);
        }
    }
}
