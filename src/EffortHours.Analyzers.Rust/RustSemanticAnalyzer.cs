namespace EffortHours.Analyzers.Rust;

internal static class RustSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<RustToken> tokens,
        RustImportContext imports,
        CargoPackageModel package,
        string path,
        RustSourceMetrics metrics)
    {
        HashSet<string> technologies = [.. imports.Technologies];
        foreach (CargoDependency dependency in package.Dependencies)
        {
            if (dependency.PathDirectory is not null) continue;
            if (imports.LocalModules.Contains(dependency.Name.Replace('-', '_'))) continue;
            string? technology = RustImportAnalysis.Technology(
                dependency.PackageName ?? dependency.Name);
            if (technology is not null && UsesDependencyCrate(tokens, dependency))
                technologies.Add(technology);
        }
        metrics.Technologies.UnionWith(technologies);

        foreach (string technology in technologies)
        {
            if (technology is "axum" or "actix_web" or "rocket" or "warp" or "tonic" &&
                HasAny(tokens, "Router", "route", "service", "handler", "get", "post", "put", "delete"))
                metrics.ApiSurfaces++;
            if (technology is "sqlx" or "diesel" or "sea_orm" or "rusqlite" or "mongodb" &&
                HasAny(tokens, "query", "query_as", "execute", "Connection", "Entity", "table", "collection"))
                metrics.DataCalls++;
            if (technology is "reqwest" or "hyper" or "tonic" or "aws-sdk" or
                "rdkafka" or "lapin" or "nats" or "redis" &&
                HasAny(tokens, "Client", "connect", "send", "publish", "request", "produce", "consume"))
                metrics.IntegrationCalls++;
            if (technology is "ring" or "rustls" or "argon2" or "jsonwebtoken" or "oauth2" or "secrecy" &&
                HasAny(tokens, "encrypt", "decrypt", "verify", "hash", "sign", "encode", "decode",
                    "Secret", "Argon2", "TlsConnector", "EncodingKey", "DecodingKey"))
                metrics.SecurityUsages++;
            if (technology is "clap" or "argh" &&
                HasAny(tokens, "Parser", "Subcommand", "Command", "Args", "FromArgs"))
                metrics.CliCommands++;
            if (technology is "tokio" or "async_std" or "rayon" or "apalis" or "cron" &&
                HasAny(tokens, "spawn", "task", "JoinSet", "par_iter", "Worker", "schedule"))
                metrics.BackgroundUsages++;
            if (technology is "tokio" or "async_std" or "rayon" &&
                HasAny(tokens, "spawn", "join", "select", "Mutex", "RwLock", "channel", "par_iter"))
                metrics.ConcurrencyUsages++;
            if (technology is "libc" or "bindgen" or "cxx" or "uniffi" or "wasm_bindgen")
            {
                metrics.FfiBoundaries++;
                if (technology == "bindgen") metrics.GeneratedBindingSignals++;
            }
            if (technology is "validator" or "garde" &&
                HasAny(tokens, "Validate", "validate", "garde")) metrics.ValidationRules++;
            if (technology == "criterion" && HasAny(tokens, "criterion_group", "bench_function"))
                metrics.Benchmarks++;
            if (technology is "proptest" or "rstest" && HasAny(tokens, "proptest", "rstest", "case"))
                metrics.ParameterizedCases++;
            if (technology == "mockall") metrics.MockUsages++;
        }

        if (HasSequence(tokens, "std", "::", "thread", "::", "spawn") ||
            HasSequence(tokens, "thread", "::", "spawn"))
        {
            metrics.BackgroundUsages++;
            metrics.ConcurrencyUsages++;
        }
        if (HasSequence(tokens, "std", "::", "sync") || HasAny(tokens, "AtomicUsize", "AtomicBool"))
            metrics.ConcurrencyUsages++;
        if (HasAny(tokens, "assert", "assert_eq", "assert_ne", "debug_assert", "matches") && IsTestPath(path))
            metrics.Assertions = Math.Max(1, metrics.Assertions);
    }

    private static bool UsesDependencyCrate(
        IReadOnlyList<RustToken> tokens,
        CargoDependency dependency)
    {
        string crate = dependency.Name.Replace('-', '_');
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind == RustTokenKind.Identifier && tokens[index + 1].Text == "::" &&
                tokens[index].Text == crate)
                return true;
        }
        return false;
    }

    private static bool HasAny(IReadOnlyList<RustToken> tokens, params string[] values) =>
        tokens.Any(token => values.Contains(token.Text, StringComparer.Ordinal));

    private static bool HasSequence(IReadOnlyList<RustToken> tokens, params string[] values)
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
        .Any(segment => segment.Equals("tests", StringComparison.OrdinalIgnoreCase));
}
