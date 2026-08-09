namespace EffortHours.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int UsageError = 2;
    public const int InvalidInput = 3;
    public const int InternalError = 4;
    public const int CalibrationRegression = 5;
    public const int Cancelled = 130;
}
