namespace EffortHours.ChangeBenchmarks;

internal sealed record BenchmarkSourceTreeCopy(
    int FileCount,
    int DirectoryCount,
    int SkippedLinkCount);

internal static class BenchmarkSourceTree
{
    public static BenchmarkSourceTreeCopy CopyTo(string sourcePath, string destinationPath)
    {
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(
                "The caller-supplied benchmark source tree was not found or is inaccessible.");
        }

        string sourceRoot = Path.GetPathRoot(source) ?? string.Empty;
        if (PathComparer.Equals(
            Path.TrimEndingDirectorySeparator(source),
            Path.TrimEndingDirectorySeparator(sourceRoot)))
        {
            throw new ArgumentException(
                "The caller-supplied benchmark source tree cannot be a filesystem root.",
                nameof(sourcePath));
        }

        if (IsWithin(destination, source) || IsWithin(source, destination))
        {
            throw new ArgumentException(
                "The benchmark source and generated repository must not contain one another.",
                nameof(destinationPath));
        }

        int files = 0;
        int directories = 0;
        int skippedLinks = 0;
        Stack<(string Source, string Relative)> pending = new();
        pending.Push((source, string.Empty));
        while (pending.Count > 0)
        {
            (string directory, string relative) = pending.Pop();
            string targetDirectory = Path.Combine(destination, relative);
            Directory.CreateDirectory(targetDirectory);
            directories++;
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                string name = Path.GetFileName(path);
                if (relative.Length == 0 &&
                    string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skippedLinks++;
                    continue;
                }

                string childRelative = Path.Combine(relative, name);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push((path, childRelative));
                }
                else
                {
                    File.Copy(path, Path.Combine(destination, childRelative));
                    files++;
                }
            }
        }

        return new BenchmarkSourceTreeCopy(files, directories, skippedLinks);
    }

    private static bool IsWithin(string candidate, string root)
    {
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, PathComparison);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
