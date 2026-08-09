using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class AnalyzerFalsePositiveTests
{
    [Fact]
    public async Task ProcessStreamsDoNotProducePersistenceEvidenceWhileDatabaseCallsDo()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Process.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "ProcessPipe.cs",
            """
            public sealed class ProcessPipe
            {
                public async Task<string> RunAsync(Command command, Process process)
                {
                    await process.StandardInput.WriteLineAsync("input");
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await command.ExecuteAsync();
                    return output + error;
                }
            }
            """);
        repository.WriteText(
            "DataStore.cs",
            """
            public sealed class DataStore
            {
                private readonly IDbConnection connection;

                public Task<int> SaveAsync() =>
                    connection.ExecuteAsync("insert into events values (1)");
            }
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Id == "dotnet:data:ProcessPipe.cs");
        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "dotnet:data:DataStore.cs");
    }

    [Fact]
    public async Task FrameworkNeutralStateAndEffectsDoNotProduceUiEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {
              "name": "reactive-core",
              "exports": "./src/store.js"
            }
            """);
        repository.WriteText(
            "src/store.js",
            """
            export function configure(callback) {
              const store = createStore({ count: 0 });
              const state = reactive(store);
              useEffect(() => callback(state));
              return state;
            }
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Id == "javascript:ui:src/store.js");
    }

    [Fact]
    public async Task UiFrameworkStateAndEffectsStillProduceUiEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {
              "name": "web-app",
              "dependencies": { "react": "19.0.0" }
            }
            """);
        repository.WriteText(
            "src/counter.js",
            """
            export function counter() {
              const state = useState(0);
              useEffect(() => state[1](1));
              return state;
            }
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "javascript:ui:src/counter.js");
    }

    [Fact]
    public async Task DevelopmentBenchmarkHashbangIsNotAProductEntrypoint()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {
              "name": "cli-library",
              "bin": { "sample-cli": "bin/cli.js" },
              "exports": "./src/index.js",
              "scripts": { "benchmark": "node benchmarks/run.js" }
            }
            """);
        repository.WriteText(
            "bin/cli.js",
            "#!/usr/bin/env node\nconsole.log('product command');\n");
        repository.WriteText(
            "benchmarks/run.js",
            "#!/usr/bin/env node\nconsole.log('development benchmark');\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "javascript:entry-point:bin/cli.js");
        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Id == "javascript:entry-point:benchmarks/run.js");
    }
}
