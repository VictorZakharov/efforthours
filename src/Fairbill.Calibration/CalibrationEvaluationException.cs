namespace Fairbill.Calibration;

public sealed class CalibrationEvaluationException : Exception
{
    public CalibrationEvaluationException(IEnumerable<string> errors)
        : base("Calibration evaluation input is invalid.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = [.. errors];
    }

    public IReadOnlyList<string> Errors { get; }
}
