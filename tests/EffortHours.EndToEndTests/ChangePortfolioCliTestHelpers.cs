using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static readonly string[] ContributorAAliases =
        ["Contributor A", "selected-a@example.invalid"];

    private static readonly string[] ContributorBAliases =
        ["selected-b@example.invalid", "Contributor B"];

    private static async Task CloneAsync(string sourcePath, string targetPath)
    {
        string workingDirectory = Path.GetDirectoryName(targetPath)!;
        ProcessStartInfo startInfo = StartInfo("git", workingDirectory);
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--no-hardlinks");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(targetPath);
        ProcessResult result = await RunAsync(startInfo);
        Assert.True(result.ExitCode == 0, $"git clone failed: {result.StandardError}");
    }

    private static async Task CloneBareAsync(string sourcePath, string targetPath)
    {
        string workingDirectory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(workingDirectory);
        ProcessStartInfo startInfo = StartInfo("git", workingDirectory);
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--bare");
        startInfo.ArgumentList.Add("--no-hardlinks");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(targetPath);
        ProcessResult result = await RunAsync(startInfo);
        Assert.True(result.ExitCode == 0, $"git clone --bare failed: {result.StandardError}");
    }

    private static void WriteManifest(string path, params object[] repositories)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0.0",
                selection = new
                {
                    sinceInclusive = "2020-01-01T00:00:00Z",
                    untilExclusive = "2030-01-01T00:00:00Z",
                    timeZone = "UTC",
                    dateField = "author",
                    mergePolicy = "exclude",
                    coauthorPolicy = "include",
                    intervalSemantics = "since-inclusive-until-exclusive",
                },
                contributors = new[]
                {
                    new { id = "contributor-a", aliases = ContributorAAliases },
                },
                repositories,
            }));
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
