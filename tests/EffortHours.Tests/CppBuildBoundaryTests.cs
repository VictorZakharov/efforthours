using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class CppBuildBoundaryTests
{
    [Fact]
    public async Task CMakeMakeMesonVcxAndCompileCommandsCoexistWithoutExecutingBuildLogic()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "set(SOURCES src/app.cpp)\nadd_executable(app ${SOURCES})\n" +
            "target_include_directories(app PRIVATE include)\nfind_package(CURL REQUIRED)\n" +
            "add_custom_command(OUTPUT generated.cpp COMMAND private-generator)\n");
        repository.WriteText(
            "Makefile",
            "SOURCES := src/app.cpp\napp: src/app.cpp\n\tprivate-compiler-command --secret\ntest: tests/app_test.cpp\n");
        repository.WriteText(
            "meson.build",
            "project('app', 'cpp', default_options: ['cpp_std=c++20'])\n" +
            "executable('app', 'src/app.cpp')\ntest('unit', executable('tests', 'tests/app_test.cpp'))\n");
        repository.WriteText(
            "app.vcxproj",
            "<Project><ItemGroup><ClCompile Include=\"src/app.cpp\" />" +
            "<ProjectReference Include=\"lib.vcxproj\" /></ItemGroup>" +
            "<PropertyGroup><ConfigurationType>Application</ConfigurationType>" +
            "<LanguageStandard>stdcpp20</LanguageStandard></PropertyGroup></Project>");
        repository.WriteText(
            "compile_commands.json",
            "[{\"directory\":\"C:/private\",\"file\":\"src/app.cpp\"," +
            "\"arguments\":[\"clang++\",\"-std=c++20\",\"-DPRIVATE_SECRET=value\"]," +
            "\"command\":\"private command body\"}]\n");
        repository.WriteText(
            "CMakePresets.json",
            "{\"version\":8,\"configurePresets\":[{\"name\":\"private-config\"," +
            "\"cacheVariables\":{\"PRIVATE_TOKEN\":\"secret\"}}]," +
            "\"buildPresets\":[{\"name\":\"private-build\",\"configurePreset\":\"private-config\"}]}\n");
        repository.WriteText("src/app.cpp", "int main() { return 0; }\n");
        repository.WriteText("tests/app_test.cpp", "int test_app() { return 1; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(evidence);
        EvidenceFact build = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer");

        Assert.Equal(6m, Measurement(build, "build-systems"));
        Assert.True(Measurement(build, "targets") >= 5m);
        Assert.Equal(2m, Measurement(build, "configuration-variants"));
        Assert.Contains("declared-standard:c++20", build.Tags);
        Assert.Contains("build-system:cmake", build.Tags);
        Assert.Contains("build-system:make", build.Tags);
        Assert.Contains("build-system:meson", build.Tags);
        Assert.Contains("build-system:msbuild", build.Tags);
        Assert.Contains("build-system:compile-commands", build.Tags);
        Assert.Contains("build-system:cmake-presets", build.Tags);
        Assert.DoesNotContain("private-generator", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-compiler-command", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SECRET", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private command body", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_TOKEN", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-config", json, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task NestedLiteralProjectsCreateUnambiguousLocalIncludeReference()
    {
        InMemoryRepository repository = new();
        repository.WriteText("CMakeLists.txt", "add_subdirectory(lib)\nadd_executable(app src/main.cpp)\ntarget_include_directories(app PRIVATE lib/include)\n");
        repository.WriteText("src/main.cpp", "#include <domain.hpp>\nint main() { return domain(); }\n");
        repository.WriteText("lib/CMakeLists.txt", "add_library(domain src/domain.cpp include/domain.hpp)\ntarget_include_directories(domain PUBLIC include)\n");
        repository.WriteText("lib/include/domain.hpp", "int domain();\n");
        repository.WriteText("lib/src/domain.cpp", "#include \"../include/domain.hpp\"\nint domain() { return 1; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer" && fact.Scope == "." &&
            fact.Tags.Contains("target-scope:lib"));
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8904" &&
            diagnostic.Message.Contains("conflicting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CHeaderLanguageOwnershipIsExplicitForSingleAndMixedLanguageProjects()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "project(mixed LANGUAGES C CXX)\nadd_library(mixed c_api.c cpp_api.cpp api.h)\n");
        repository.WriteText("c_api.c", "int c_api(void) { return 1; }\n");
        repository.WriteText("cpp_api.cpp", "int cpp_api() { return 1; }\n");
        repository.WriteText("api.h", "int c_api(void);\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact source = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer");

        Assert.Contains("c-header-language:unresolved-c-or-cpp", source.Tags);
    }

    [Fact]
    public async Task BuildCallsInsideCommentsStringsAndBracketArgumentsDoNotCreateTargets()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "# add_library(commented fake.cpp)\n" +
            "set(DOC \"add_executable(quoted fake.cpp)\")\n" +
            "set(BRACKET [=[add_library(bracket fake.cpp)]=])\n" +
            "add_library(real src/real.cpp)\n");
        repository.WriteText("src/real.cpp", "int real() { return 1; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact build = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer");

        Assert.Equal(1m, Measurement(build, "targets"));
    }

    [Fact]
    public async Task GenericMsBuildImportsAloneDoNotActivateCppAnalysis()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Directory.Build.props",
            "<Project><PropertyGroup><WarningsAsErrors>true</WarningsAsErrors></PropertyGroup></Project>\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer");
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic =>
            diagnostic.Code.StartsWith("FB89", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArbitraryTargetsRemainGenericEvenWhenCppAnalysisIsActive()
    {
        InMemoryRepository repository = new();
        repository.WriteText("CMakeLists.txt", "add_library(app src/app.cpp)\n");
        repository.WriteText("src/app.cpp", "int app() { return 1; }\n");
        repository.WriteText(
            "eng/custom.targets",
            "<Project><PropertyGroup><ConfigurationType>Application</ConfigurationType>" +
            "<LanguageStandard>stdcpp23</LanguageStandard></PropertyGroup></Project>\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact build = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == "efforthours.cpp-analyzer");

        Assert.DoesNotContain("build-system:msbuild", build.Tags);
        Assert.DoesNotContain("declared-standard:c++23", build.Tags);
        Assert.DoesNotContain(build.Locations, location => location.Path == "eng/custom.targets");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.File &&
            fact.Scope == "eng/custom.targets" && fact.Tags.Contains("role:build-configuration"));
    }

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
