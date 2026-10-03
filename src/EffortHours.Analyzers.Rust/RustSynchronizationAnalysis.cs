namespace EffortHours.Analyzers.Rust;

internal static class RustSynchronizationAnalysis
{
    private const int MaximumQualifierTokens = 256;

    private static readonly HashSet<string> SynchronizationSymbols = new(StringComparer.Ordinal)
    {
        "Mutex", "RwLock", "Condvar", "Barrier", "Once", "OnceLock", "LazyLock", "mpsc",
        "AtomicBool", "AtomicPtr", "AtomicIsize", "AtomicUsize",
        "AtomicI8", "AtomicI16", "AtomicI32", "AtomicI64", "AtomicI128",
        "AtomicU8", "AtomicU16", "AtomicU32", "AtomicU64", "AtomicU128",
    };

    public static bool HasStandardSynchronization(
        IReadOnlyList<RustToken> tokens,
        RustImportContext imports)
    {
        if (imports.LocalModules.Contains("std")) return false;
        for (int index = 0; index + 4 < tokens.Count; index++)
        {
            if (tokens[index].Text != "std" || tokens[index + 1].Text != "::" ||
                tokens[index + 2].Text != "sync" || tokens[index + 3].Text != "::")
                continue;

            // Follow a qualified path or a bounded grouped import, never the rest
            // of the file. Arc/Weak ownership and Ordering alone are not coordination.
            int depth = 0;
            for (int cursor = index + 4; cursor < Math.Min(tokens.Count, index + 4 + MaximumQualifierTokens); cursor++)
            {
                RustToken token = tokens[cursor];
                if (cursor > index + 4 && tokens[cursor - 1].Text == "as") continue;
                if (SynchronizationSymbols.Contains(token.Text)) return true;
                if (token.Text == "{") depth++;
                else if (token.Text == "}")
                {
                    if (--depth <= 0) break;
                }
                else if (token.Text == ";" || depth == 0 &&
                    token.Text != "::" && token.Kind is not RustTokenKind.Identifier)
                    break;
                else if (depth == 0 && token.Text == "as") break;
            }
        }
        return false;
    }
}
