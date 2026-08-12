namespace EffortHours.Analyzers.Cpp;

internal static class CppSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<CppToken> tokens,
        string path,
        IReadOnlySet<string> declaredDependencies,
        CppSourceMetrics metrics)
    {
        string[] includes = [.. tokens
            .Where(token => token.Kind == CppTokenKind.Preprocessor)
            .Select(token => IncludeName(token.Text))
            .Where(value => value is not null)
            .Select(value => value!)];
        HashSet<string> technologies = [.. includes
            .Select(Technology)
            .Where(value => value is not null)
            .Select(value => value!)];
        technologies.UnionWith(declaredDependencies
            .Select(Technology)
            .Where(value => value is not null)
            .Select(value => value!));
        metrics.Technologies.UnionWith(technologies);

        foreach (string technology in technologies)
        {
            if (technology is "grpc" or "crow" or "drogon" or "cpp-httplib" or "boost-beast" &&
                HasAny(tokens, "route", "Get", "Post", "listen", "Server", "Service", "stub"))
                metrics.ApiSurfaces++;
            if (technology is "sqlite" or "postgresql" or "mysql" or "soci" or "odb" or "serialization" &&
                HasAny(tokens, "prepare", "execute", "query", "Connection", "session", "transaction",
                    "parse", "dump", "from_json", "to_json"))
                metrics.DataCalls++;
            if (technology is "curl" or "grpc" or "boost-asio" or "aws-sdk" or "mqtt" or "amqp" &&
                HasAny(tokens, "perform", "request", "send", "publish", "connect", "Client", "channel"))
                metrics.IntegrationCalls++;
            if (technology is "openssl" or "libsodium" or "botan" or "cryptopp" &&
                HasAny(tokens, "encrypt", "decrypt", "verify", "sign", "hash", "SSL", "EVP",
                    "EVP_DigestVerifyInit", "EVP_DigestSignInit"))
                metrics.SecurityUsages++;
            if (technology is "cli11" or "cxxopts" or "boost-program-options" &&
                HasAny(tokens, "add_option", "parse", "Options", "App", "Command"))
                metrics.CliCommands++;
            if (technology is "qt" or "sdl" or "sfml" or "imgui" or "opengl" or "vulkan" &&
                HasAny(tokens, "Widget", "Window", "render", "draw", "Button", "Application",
                    "QWidget", "QWindow", "QApplication", "SDL_CreateWindow", "ImGui",
                    "glDrawArrays", "glDrawElements", "vkCmdDraw"))
                metrics.UiSurfaces++;
            if (technology is "gtest" or "gmock" && HasAny(tokens, "TEST", "TEST_F", "TEST_P"))
                metrics.TestCases += CountAny(tokens, "TEST", "TEST_F", "TEST_P");
            if (technology is "catch2" or "doctest" && HasAny(tokens, "TEST_CASE", "SCENARIO"))
                metrics.TestCases += CountAny(tokens, "TEST_CASE", "SCENARIO");
            if (technology == "boost-test" && HasAny(tokens, "BOOST_AUTO_TEST_CASE", "BOOST_DATA_TEST_CASE"))
                metrics.TestCases += CountAny(tokens, "BOOST_AUTO_TEST_CASE", "BOOST_DATA_TEST_CASE");
            if (technology == "benchmark" && HasAny(tokens, "BENCHMARK", "State")) metrics.Benchmarks++;
        }

        if (HasAny(tokens, "std::thread", "jthread", "mutex", "shared_mutex", "atomic", "semaphore",
            "latch", "barrier", "condition_variable") ||
            HasAny(tokens, "_Atomic", "atomic_int", "atomic_load", "atomic_store", "pthread_create",
                "pthread_mutex_lock", "pthread_cond_wait") ||
            HasSequence(tokens, "std", "::", "thread") || HasSequence(tokens, "std", "::", "atomic"))
        {
            metrics.ConcurrencyUsages++;
        }
        if (HasAny(tokens, "_Generic")) metrics.BranchPoints++;
        if (HasAny(tokens, "errno", "perror", "strerror", "setjmp", "longjmp")) metrics.ErrorPaths++;
        if (HasAny(tokens, "assert", "ASSERT_EQ", "EXPECT_EQ", "CHECK", "REQUIRE") &&
            IsTestPath(path)) metrics.Assertions++;
        if (HasAny(tokens, "LLVMFuzzerTestOneInput", "FUZZ_TEST")) metrics.FuzzTargets++;
        if (HasAny(tokens, "dlopen", "dlsym", "LoadLibrary", "GetProcAddress")) metrics.FfiBoundaries++;
        if (HasAny(tokens, "validate", "is_valid", "invalid_argument", "expected") &&
            HasAny(tokens, "throw", "return", "error")) metrics.ValidationRules++;
    }

    private static string? IncludeName(string directive)
    {
        int include = directive.IndexOf("include", StringComparison.Ordinal);
        if (include < 0) return null;
        int start = directive.IndexOfAny(['"', '<'], include + 7);
        if (start < 0) return null;
        char close = directive[start] == '"' ? '"' : '>';
        int end = directive.IndexOf(close, start + 1);
        return end < 0 ? null : directive[(start + 1)..end].Replace('\\', '/').ToLowerInvariant();
    }

    internal static string? Technology(string include)
    {
        string value = include.ToLowerInvariant();
        if (value.Contains("grpc", StringComparison.Ordinal)) return "grpc";
        if (value.Contains("httplib", StringComparison.Ordinal)) return "cpp-httplib";
        if (value.StartsWith("crow", StringComparison.Ordinal)) return "crow";
        if (value.StartsWith("drogon", StringComparison.Ordinal)) return "drogon";
        if (value.Contains("boost/beast", StringComparison.Ordinal)) return "boost-beast";
        if (value.Contains("boost/asio", StringComparison.Ordinal)) return "boost-asio";
        if (value.Contains("boost/program_options", StringComparison.Ordinal)) return "boost-program-options";
        if (value.Contains("boost/test", StringComparison.Ordinal)) return "boost-test";
        if (value.Contains("sqlite", StringComparison.Ordinal)) return "sqlite";
        if (value is "libpq-fe.h" or "postgresql/libpq-fe.h") return "postgresql";
        if (value.Contains("mysql", StringComparison.Ordinal)) return "mysql";
        if (value.StartsWith("soci/", StringComparison.Ordinal)) return "soci";
        if (value.StartsWith("odb/", StringComparison.Ordinal)) return "odb";
        if (value.Contains("nlohmann/json", StringComparison.Ordinal) ||
            value.Contains("rapidjson", StringComparison.Ordinal)) return "serialization";
        if (value.Contains("curl", StringComparison.Ordinal)) return "curl";
        if (value.Contains("aws/", StringComparison.Ordinal)) return "aws-sdk";
        if (value.Contains("mqtt", StringComparison.Ordinal)) return "mqtt";
        if (value.Contains("amqp", StringComparison.Ordinal)) return "amqp";
        if (value.Contains("openssl", StringComparison.Ordinal)) return "openssl";
        if (value.Contains("sodium", StringComparison.Ordinal)) return "libsodium";
        if (value.Contains("botan", StringComparison.Ordinal)) return "botan";
        if (value.Contains("cryptopp", StringComparison.Ordinal) || value.Contains("crypto++", StringComparison.Ordinal))
            return "cryptopp";
        if (value.Contains("cli/cli", StringComparison.Ordinal) || value.Contains("cli11", StringComparison.Ordinal))
            return "cli11";
        if (value.Contains("cxxopts", StringComparison.Ordinal)) return "cxxopts";
        if (value.StartsWith("qt", StringComparison.Ordinal) || value.Contains("qwidget", StringComparison.Ordinal))
            return "qt";
        if (value.Contains("sdl", StringComparison.Ordinal)) return "sdl";
        if (value.Contains("sfml", StringComparison.Ordinal)) return "sfml";
        if (value.Contains("imgui", StringComparison.Ordinal)) return "imgui";
        if (value.Contains("vulkan", StringComparison.Ordinal)) return "vulkan";
        if (value.Contains("gl/", StringComparison.Ordinal) || value.Contains("opengl", StringComparison.Ordinal))
            return "opengl";
        if (value.Contains("gtest", StringComparison.Ordinal)) return "gtest";
        if (value.Contains("gmock", StringComparison.Ordinal)) return "gmock";
        if (value.Contains("catch2", StringComparison.Ordinal)) return "catch2";
        if (value.Contains("doctest", StringComparison.Ordinal)) return "doctest";
        if (value.Contains("benchmark/benchmark", StringComparison.Ordinal)) return "benchmark";
        return null;
    }

    private static bool HasAny(IReadOnlyList<CppToken> tokens, params string[] values) =>
        tokens.Any(token => values.Contains(token.Text, StringComparer.Ordinal));

    private static int CountAny(IReadOnlyList<CppToken> tokens, params string[] values) =>
        tokens.Count(token => values.Contains(token.Text, StringComparer.Ordinal));

    private static bool HasSequence(IReadOnlyList<CppToken> tokens, params string[] values)
    {
        for (int index = 0; index + values.Length <= tokens.Count; index++)
        {
            bool match = true;
            for (int offset = 0; offset < values.Length; offset++)
                match &= tokens[index + offset].Text == values[offset];
            if (match) return true;
        }
        return false;
    }

    private static bool IsTestPath(string path) => path.Split('/')
        .Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("spec", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("specs", StringComparison.OrdinalIgnoreCase));
}
