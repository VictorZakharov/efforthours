using System.Text.Json;

namespace EffortHours.ChangeCalibration;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            WriteUsage(Console.Out);
            return 0;
        }

        bool fixtureMode = args.Length == 4 && args[0] == "--suite" && args[2] == "--output";
        bool teacherMode = args.Length == 6 && args[0] == "--teacher-policy" &&
            args[2] == "--index" && args[4] == "--output";
        if (!fixtureMode && !teacherMode)
        {
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            if (fixtureMode)
            {
                await FixtureSuiteGenerator.GenerateAsync(args[1], args[3]).ConfigureAwait(false);
            }
            else
            {
                await TeacherPlanGenerator.GenerateAsync(args[1], args[3], args[5]).ConfigureAwait(false);
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static void WriteUsage(TextWriter writer) => writer.WriteLine("""
        Usage:
          dotnet EffortHours.ChangeCalibration.dll --suite <fixtures.json> --output <directory>
          dotnet EffortHours.ChangeCalibration.dll --teacher-policy <policy.json> --index <index.json> --output <plan.json>
        """);
}
