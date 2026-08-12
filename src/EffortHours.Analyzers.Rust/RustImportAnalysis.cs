namespace EffortHours.Analyzers.Rust;

internal static class RustImportAnalysis
{
    public static RustImportContext Read(
        IReadOnlyList<RustToken> tokens,
        CargoPackageModel package,
        RustSourceMetrics metrics)
    {
        RustImportContext context = new();
        context.LocalCrates.UnionWith(package.Dependencies
            .Where(dependency => dependency.PathDirectory is not null)
            .SelectMany(dependency => Names(dependency.Name, dependency.PackageName)));
        HashSet<string> localModules = [.. tokens
            .Select((token, index) => (token, index))
            .Where(item => item.token.Text == "mod" && item.index + 1 < tokens.Count &&
                tokens[item.index + 1].Kind == RustTokenKind.Identifier)
            .Select(item => Normalize(tokens[item.index + 1].Text))];
        context.LocalModules.UnionWith(localModules);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "use")
            {
                metrics.Uses++;
                int cursor = index + 1;
                while (cursor < tokens.Count && tokens[cursor].Text == "::") cursor++;
                string? root = Root(tokens, cursor);
                if (root is not null) Add(root, localModules, context, metrics);
            }
            else if (tokens[index].Text == "extern" && index + 2 < tokens.Count &&
                tokens[index + 1].Text == "crate")
            {
                string? root = Root(tokens, index + 2);
                if (root is not null) Add(root, localModules, context, metrics);
            }
        }

        return context;
    }

    public static string? Technology(string crate)
    {
        string value = crate.Replace('-', '_').ToLowerInvariant();
        if (value is "axum" or "actix_web" or "rocket" or "warp" or "tonic") return value;
        if (value is "sqlx" or "diesel" or "sea_orm" or "rusqlite" or "mongodb") return value;
        if (value is "reqwest" or "hyper" or "rdkafka" or "lapin" or "nats" or "redis") return value;
        if (value.StartsWith("aws_sdk_", StringComparison.Ordinal)) return "aws-sdk";
        if (value is "ring" or "rustls" or "argon2" or "jsonwebtoken" or "oauth2" or "secrecy") return value;
        if (value is "clap" or "argh") return value;
        if (value is "tokio" or "async_std" or "rayon" or "apalis" or "cron") return value;
        if (value is "libc" or "bindgen" or "cxx" or "uniffi" or "wasm_bindgen") return value;
        if (value is "validator" or "garde") return value;
        if (value is "criterion" or "proptest" or "rstest" or "mockall") return value;
        return null;
    }

    private static string? Root(IReadOnlyList<RustToken> tokens, int index)
    {
        if (index >= tokens.Count || tokens[index].Kind is not (RustTokenKind.Identifier or RustTokenKind.Keyword))
            return null;
        string value = tokens[index].Text;
        return Normalize(value);
    }

    private static void Add(
        string root,
        HashSet<string> localModules,
        RustImportContext context,
        RustSourceMetrics metrics)
    {
        context.Crates.Add(root);
        if (root is "crate" or "self" or "super" || context.LocalCrates.Contains(root) ||
            localModules.Contains(root))
        {
            metrics.InternalImports++;
            return;
        }
        string? technology = Technology(root);
        if (technology is not null)
        {
            context.Technologies.Add(technology);
            metrics.Technologies.Add(technology);
        }
    }

    private static IEnumerable<string> Names(string name, string? packageName)
    {
        yield return name.Replace('-', '_');
        if (packageName is not null) yield return packageName.Replace('-', '_');
    }

    private static string Normalize(string value) => value.StartsWith("r#", StringComparison.Ordinal)
        ? value[2..]
        : value;
}
