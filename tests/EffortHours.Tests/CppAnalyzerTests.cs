using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CppAnalyzerTests
{
    private const string Analyzer = "efforthours.cpp-analyzer";

    [Fact]
    public async Task CMakeCppStructureSemanticsIncludesTestsAndEffortAreTraceable()
    {
        InMemoryRepository repository = RichRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact source = AnalyzerFact(evidence, EvidenceKinds.SourceStructure, ".");

        Assert.Contains("analysis-status:analyzed", Language(evidence, "cpp").Tags);
        Assert.Contains("analysis-depth:token-backed", Language(evidence, "cpp").Tags);
        Assert.Equal("0.1.0", source.Provenance.AnalyzerVersion);
        Assert.True(Measurement(source, "translation-units") >= 2m);
        Assert.True(Measurement(source, "headers") >= 1m);
        Assert.True(Measurement(source, "types") >= 3m);
        Assert.True(Measurement(source, "functions") + Measurement(source, "methods") >= 4m);
        Assert.True(Measurement(source, "public-symbols") >= 3m);
        Assert.True(Measurement(source, "templates") >= 1m);
        Assert.True(Measurement(source, "concepts") >= 1m);
        Assert.True(Measurement(source, "branch-points") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:grpc"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:sqlite"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:curl"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:openssl"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("integration-kind:ffi"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:gtest"));
        EvidenceFact build = AnalyzerFact(evidence, EvidenceKinds.BuildConfiguration, ".");
        Assert.True(Measurement(build, "targets") >= 3m);
        Assert.True(Measurement(build, "conditional-groups") >= 1m);
        Assert.Contains(build.Tags, tag => tag == "declared-standard:c++20");
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.SecurityAndAccessibility);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.UnitTesting);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task LocalNamesakesDoNotCreateQualifiedSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "add_library(grpc src/grpc.cpp)\nadd_library(sqlite src/sqlite.cpp)\n" +
            "add_library(openssl src/openssl.cpp)\nadd_executable(app src/main.cpp)\n" +
            "target_link_libraries(app PRIVATE grpc sqlite openssl)\n");
        repository.WriteText("src/grpc.cpp", "struct Server {};\n");
        repository.WriteText("src/sqlite.cpp", "int query() { return 1; }\n");
        repository.WriteText("src/openssl.cpp", "int verify() { return 1; }\n");
        repository.WriteText(
            "src/main.cpp",
            "struct Server {}; int query() { return 1; } int verify() { return 1; } int main() { return query(); }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer && fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess or EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task HeaderFanoutAndExactDuplicatesDoNotMultiplyEstimatedEffort()
    {
        const string header = "template<class T> struct Store { T value; };\n";
        EstimateReport single = await EstimateAsync(
            ("include/store.hpp", header),
            ("src/one.cpp", "#include \"../include/store.hpp\"\nint one() { return 1; }\n"));
        EstimateReport fanout = await EstimateAsync(
            ("include/store.hpp", header),
            ("src/one.cpp", "#include \"../include/store.hpp\"\nint one() { return 1; }\n"),
            ("src/two.cpp", "#include \"../include/store.hpp\"\nint two() { return 2; }\n"));
        EstimateReport duplicate = await EstimateAsync(
            ("include/store.hpp", header),
            ("include/store-copy.hpp", header),
            ("src/one.cpp", "#include \"../include/store.hpp\"\nint one() { return 1; }\n"));

        Assert.True(fanout.TotalEffort.Expected > single.TotalEffort.Expected);
        decimal sourceDelta = Category(fanout, EffortCategory.ProductionImplementation).Expected -
            Category(single, EffortCategory.ProductionImplementation).Expected;
        Assert.True(sourceDelta < Category(single, EffortCategory.ProductionImplementation).Expected);
        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    [Fact]
    public async Task MixedCppRustAndJavaScriptRepositoryKeepsAnalyzerOwnershipSeparate()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/app.cpp", "struct Order { int id; };\n");
        repository.WriteText("native/Cargo.toml", "[package]\nname=\"native\"\nversion=\"1.0.0\"\n");
        repository.WriteText("native/src/lib.rs", "pub struct Order { pub id: i64 }\n");
        repository.WriteText("web/order.js", "export function showOrder(order) { return order.id; }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "src/app.cpp"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.rust-analyzer" &&
            fact.Locations.Any(location => location.Path == "native/src/lib.rs"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.javascript-analyzer" &&
            fact.Locations.Any(location => location.Path == "web/order.js"));
        Assert.DoesNotContain(evidence.Facts.Where(fact => fact.Provenance.Analyzer == Analyzer)
            .SelectMany(fact => fact.Locations), location =>
                location.Path is "native/src/lib.rs" or "web/order.js");
    }

    [Fact]
    public async Task CDeclarationsGenericErrorsAndAtomicsProduceBoundedStructure()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Makefile",
            "status: status.c\n\t$(CC) -std=c17 status.c -o status\n");
        repository.WriteText(
            "status.c",
            "#include <errno.h>\n#include <stdatomic.h>\n" +
            "typedef struct Status { atomic_int value; } Status;\n" +
            "int normalize(Status* status) { errno = 0; return _Generic(status->value, int: 1, default: 0); }\n" +
            "int main(void) { Status status = { 1 }; return normalize(&status); }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact source = AnalyzerFact(evidence, EvidenceKinds.SourceStructure, ".");

        Assert.True(Measurement(source, "types") >= 1m);
        Assert.True(Measurement(source, "functions") >= 2m);
        Assert.True(Measurement(source, "branch-points") >= 1m);
        Assert.True(Measurement(source, "error-paths") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Provenance.Analyzer == Analyzer);
    }

    [Fact]
    public async Task QualifiedUiSerializationAndEndToEndTestsUseDistinctEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "find_package(Qt6 COMPONENTS Widgets REQUIRED)\n" +
            "find_package(nlohmann_json REQUIRED)\nadd_library(app src/window.cpp)\n");
        repository.WriteText(
            "src/window.cpp",
            "#include <QWidget>\n#include <nlohmann/json.hpp>\n" +
            "QWidget* make_window() { auto value = nlohmann::json::parse(\"{}\"); return new QWidget(); }\n");
        repository.WriteText(
            "end-to-end/window_test.cpp",
            "#include <gtest/gtest.h>\nTEST(WindowFlow, Opens) { EXPECT_EQ(1, 1); }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.UserInterface &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:qt"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:serialization"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("test-type:end-to-end"));
    }

    private static InMemoryRepository RichRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "cmake_minimum_required(VERSION 3.25)\nproject(store LANGUAGES C CXX)\n" +
            "set(CMAKE_CXX_STANDARD 20)\nfind_package(gRPC REQUIRED)\nfind_package(OpenSSL REQUIRED)\n" +
            "find_package(CURL REQUIRED)\nfind_package(SQLite3 REQUIRED)\nfind_package(GTest REQUIRED)\n" +
            "add_library(store src/service.cpp include/service.hpp)\n" +
            "target_include_directories(store PUBLIC include)\n" +
            "target_link_libraries(store PRIVATE gRPC OpenSSL CURL SQLite3)\n" +
            "add_executable(store_cli src/main.cpp)\nadd_executable(store_tests tests/service_test.cpp)\n" +
            "target_link_libraries(store_tests PRIVATE GTest store)\nadd_test(NAME store COMMAND store_tests)\ninstall(TARGETS store)\n");
        repository.WriteText(
            "include/service.hpp",
            "#pragma once\n#if defined(_WIN32)\nstruct Platform { int value; };\n#else\nstruct Platform { int value; };\n#endif\n" +
            "template<class T> concept Item = requires(T value) { value.id; };\n" +
            "template<Item T> class Store { public: T load(); void save(T value); };\n" +
            "extern \"C\" int native_status();\n");
        repository.WriteText(
            "src/service.cpp",
            "#include \"service.hpp\"\n#include <grpcpp/grpcpp.h>\n#include <sqlite3.h>\n" +
            "#include <curl/curl.h>\n#include <openssl/evp.h>\n#include <thread>\n#include <atomic>\n" +
            "struct Server { void route(); }; struct Connection { void query(); };\n" +
            "void serve() { Server server; server.route(); Connection db; db.query(); " +
            "curl_easy_perform(nullptr); EVP_DigestVerifyInit(nullptr,nullptr,nullptr,nullptr,nullptr); " +
            "std::thread worker([]{}); if (native_status() != 0) throw 1; }\n");
        repository.WriteText("src/main.cpp", "int main(int, char**) { return 0; }\n");
        repository.WriteText(
            "tests/service_test.cpp",
            "#include <gtest/gtest.h>\nTEST(Store, Loads) { EXPECT_EQ(2 + 2, 4); }\n");
        return repository;
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(await ScanAsync(repository), EstimationProfile.Implementation);
    }

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string kind, string scope) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind && fact.Scope == scope &&
            fact.Provenance.Analyzer == Analyzer);

    private static EvidenceFact Language(RepositoryEvidence evidence, string language) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains($"language:{language}", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;
}
