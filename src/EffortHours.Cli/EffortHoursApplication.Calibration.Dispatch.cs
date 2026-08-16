namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> CalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string[] remaining = [.. arguments.Skip(1)];
        return arguments[0] switch
        {
            "scaffold" => await ScaffoldCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "change-scaffold" => await ScaffoldChangeCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "compile" => await CompileCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "change-compile" => await CompileChangeCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "review-scaffold" => await ScaffoldCorpusReviewAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "review-compile" => await CompileCorpusReviewAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "mutations" => await EvaluateMutationsAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "validate" => await ValidateCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "evaluate" => await EvaluateCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "diagnose" => await DiagnoseCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-features" => await ProjectUncertaintyFeaturesAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-structure" => await ProjectUncertaintyStructureAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-graph" => await ProjectUncertaintyGraphAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-evaluate" => await EvaluateUncertaintyFeaturesAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-structure-evaluate" => await EvaluateUncertaintyStructureAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-support" => await ProfileUncertaintySupportAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "uncertainty-support-evaluate" => await EvaluateUncertaintySupportAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            "change-evaluate" => await EvaluateChangeCalibrationAsync(
                remaining, standardOutput, standardError, cancellationToken).ConfigureAwait(false),
            _ => await UsageErrorAsync(standardError, CalibrationCommandExpectation)
                .ConfigureAwait(false),
        };
    }

    private const string CalibrationCommandExpectation =
        "Expected 'calibration scaffold', 'calibration change-scaffold', " +
        "'calibration compile', 'calibration change-compile', " +
        "'calibration review-scaffold', 'calibration review-compile', " +
        "'calibration mutations', 'calibration validate', 'calibration evaluate', " +
        "'calibration diagnose', 'calibration uncertainty-features', " +
        "'calibration uncertainty-structure', 'calibration uncertainty-graph', " +
        "'calibration uncertainty-evaluate', " +
        "'calibration uncertainty-structure-evaluate', " +
        "'calibration uncertainty-support', " +
        "'calibration uncertainty-support-evaluate', or " +
        "'calibration change-evaluate'.";
}
