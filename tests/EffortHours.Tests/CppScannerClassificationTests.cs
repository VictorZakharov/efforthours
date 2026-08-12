using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class CppScannerClassificationTests
{
    [Fact]
    public async Task ScannerClassifiesSupportedSourcesHeadersBuildMetadataAndInventoryOnlyNeighbors()
    {
        InMemoryRepository repository = new();
        repository.WriteText("CMakeLists.txt", "project(app LANGUAGES C CXX)\nadd_executable(app src/main.cpp)\n");
        repository.WriteText("CMakePresets.json", "{\"version\":8,\"configurePresets\":[]}\n");
        repository.WriteText("src/c_api.c", "int value(void) { return 1; }\n");
        repository.WriteText("src/main.cpp", "int main() { return 0; }\n");
        repository.WriteText("include/api.h", "int value(void);\n");
        repository.WriteText("include/api.hh", "struct Value {};\n");
        repository.WriteText("include/api.hpp", "struct Api {};\n");
        repository.WriteText("include/api.hxx", "struct More {};\n");
        repository.WriteText("include/api.inl", "inline int run() { return 1; }\n");
        repository.WriteText("include/api.ipp", "template<class T> T run(T value) { return value; }\n");
        repository.WriteText("include/api.tpp", "template<class T> struct Store {};\n");
        repository.WriteText("modules/app.cppm", "export module app;\n");
        repository.WriteText("modules/api.ixx", "export module api;\n");
        repository.WriteText("tests/api_test.cc", "int test_value() { return 1; }\n");
        repository.WriteText("src/legacy.cxx", "int legacy() { return 1; }\n");
        repository.WriteText("src/objective.m", "int objective(void) { return 1; }\n");
        repository.WriteText("src/kernel.cu", "__global__ void kernel() {}\n");
        repository.WriteText("src/preprocessed.i", "int generated;\n");

        RepositoryEvidence evidence = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);

        Assert.Contains("cpp", evidence.Repository.Ecosystems);
        AssertFileTag(evidence, "CMakeLists.txt", "role:build-configuration");
        AssertFileTag(evidence, "CMakePresets.json", "role:build-configuration");
        AssertFileTag(evidence, "src/c_api.c", "language:c");
        foreach (string path in new[]
        {
            "src/main.cpp", "include/api.hh", "include/api.hpp", "include/api.hxx",
            "include/api.inl", "include/api.ipp", "include/api.tpp", "modules/app.cppm",
            "modules/api.ixx", "tests/api_test.cc", "src/legacy.cxx",
        }) AssertFileTag(evidence, path, "language:cpp");
        AssertFileTag(evidence, "tests/api_test.cc", "classification:test");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "language:objective-c");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "language:cuda");
        Assert.Equal("0.2.13", RepositoryScanner.AnalyzerVersion);
    }

    private static void AssertFileTag(RepositoryEvidence evidence, string path, string tag)
    {
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == $"file:{path}");
        Assert.Contains(tag, file.Tags);
    }
}
