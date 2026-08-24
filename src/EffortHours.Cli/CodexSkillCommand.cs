using System.Reflection;
using System.Text;

namespace EffortHours.Cli;

internal static class CodexSkillCommand
{
    internal const string IntegrationContract = "efforthours-codex/1.0.0";
    internal const string SkillsRootEnvironmentVariable = "EFFORTHOURS_CODEX_SKILLS_ROOT";
    private const string ResourceName =
        "EffortHours.Cli.Integrations.Codex.EffortHours.SKILL.md";

    public static async Task<int> ExecuteAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken,
        string? skillsRoot = null)
    {
        if (arguments.Length == 0 || arguments is ["codex", "--help"] or ["codex", "-h"])
        {
            await standardOutput.WriteLineAsync(AgentHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (!string.Equals(arguments[0], "codex", StringComparison.OrdinalIgnoreCase) ||
            arguments.Length > 2)
        {
            return await UsageAsync(standardError).ConfigureAwait(false);
        }

        string packaged = ReadPackagedSkill();
        if (arguments.Length == 1)
        {
            await standardOutput.WriteAsync(packaged).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        string root = ResolveSkillsRoot(skillsRoot);
        return arguments[1] switch
        {
            "--install" => await InstallAsync(root, packaged, standardOutput, cancellationToken)
                .ConfigureAwait(false),
            "--check" => await CheckAsync(root, packaged, standardOutput, cancellationToken)
                .ConfigureAwait(false),
            _ => await UsageAsync(standardError).ConfigureAwait(false),
        };
    }

    internal static string ReadPackagedSkill()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException("The packaged EffortHours Codex skill is unavailable.");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static async Task<int> CheckAsync(
        string root,
        string packaged,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        string path = SkillPath(root);
        if (!File.Exists(path))
        {
            await WriteStatusAsync(output, "missing").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string installed = (await File.ReadAllTextAsync(path, cancellationToken)
            .ConfigureAwait(false)).ReplaceLineEndings("\n").TrimEnd() + "\n";
        bool current = string.Equals(installed, packaged, StringComparison.Ordinal);
        await WriteStatusAsync(output, current ? "current" : "stale").ConfigureAwait(false);
        return current ? CliExitCodes.Success : CliExitCodes.InvalidInput;
    }

    private static async Task<int> InstallAsync(
        string root,
        string packaged,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        string path = SkillPath(root);
        string directory = Path.GetDirectoryName(path)!;
        RejectReparsePoint(root);
        Directory.CreateDirectory(directory);
        RejectReparsePoint(directory);
        if (File.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The Codex skill file cannot be a reparse point.");
        }

        string temporary = Path.Combine(directory, ".SKILL.md." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (StreamWriter writer = new(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await writer.WriteAsync(packaged.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        await WriteStatusAsync(output, "current").ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static string ResolveSkillsRoot(string? explicitRoot)
    {
        string? configured = explicitRoot ??
            Environment.GetEnvironmentVariable(SkillsRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new InvalidOperationException("The host does not expose a user profile for Codex skills.");
        }

        return Path.Combine(profile, ".agents", "skills");
    }

    private static string SkillPath(string root)
    {
        string fullRoot = Path.GetFullPath(root);
        string path = Path.GetFullPath(Path.Combine(fullRoot, "efforthours", "SKILL.md"));
        string relative = Path.GetRelativePath(fullRoot, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Codex skill destination escaped its configured root.");
        }

        return path;
    }

    private static void RejectReparsePoint(string path)
    {
        if (Directory.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The Codex skill destination cannot be a reparse point.");
        }
    }

    private static Task WriteStatusAsync(TextWriter output, string status) =>
        output.WriteLineAsync($"status={status} integrationContract={IntegrationContract}");

    private static async Task<int> UsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("eh: expected 'agent codex', 'agent codex --install', or 'agent codex --check'.")
            .ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }

    private const string AgentHelpText = """
        Usage:
          eh agent codex
          eh agent codex --install
          eh agent codex --check

        Print, explicitly install/update, or check the packaged EffortHours Codex skill.
        """;
}
