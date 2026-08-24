namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static Task<int> AgentAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken) =>
        CodexSkillCommand.ExecuteAsync(
            arguments,
            standardOutput,
            standardError,
            cancellationToken);
}
