using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyBucketing
{
    public static string SizeBand(decimal expectedHours) => expectedHours switch
    {
        <= 0m => "zero",
        <= 1m => "xs",
        <= 2m => "s",
        <= 4m => "m",
        <= 8m => "l",
        <= 16m => "xl",
        <= 32m => "2xl",
        <= 64m => "3xl",
        _ => "4xl",
    };

    public static int SizeBandOrder(string band) => band switch
    {
        "zero" => 0,
        "xs" => 1,
        "s" => 2,
        "m" => 3,
        "l" => 4,
        "xl" => 5,
        "2xl" => 6,
        "3xl" => 7,
        "4xl" => 8,
        _ => throw new InvalidOperationException($"Unknown expected-size band '{band}'."),
    };

    public static int ComplexityOrder(ComplexityLevel complexity) => complexity switch
    {
        ComplexityLevel.Routine => 0,
        ComplexityLevel.Moderate => 1,
        ComplexityLevel.High => 2,
        ComplexityLevel.Exceptional => 3,
        _ => throw new InvalidOperationException($"Unknown complexity level '{complexity}'."),
    };

    public static CalibrationUncertaintyBucket FeatureBucket(
        CalibrationUncertaintyFeatureValueKind kind,
        decimal value) => kind switch
        {
            CalibrationUncertaintyFeatureValueKind.Count or
            CalibrationUncertaintyFeatureValueKind.Ordinal => CountBucket(value),
            CalibrationUncertaintyFeatureValueKind.Ratio => RatioBucket(value),
            CalibrationUncertaintyFeatureValueKind.Rate => RateBucket(value),
            _ => throw new InvalidOperationException(
                $"Feature value kind '{kind}' cannot be bucketed by protocol v1."),
        };

    public static int FeatureMaximumOrder(CalibrationUncertaintyFeatureValueKind kind) =>
        kind switch
        {
            CalibrationUncertaintyFeatureValueKind.Count or
            CalibrationUncertaintyFeatureValueKind.Ordinal or
            CalibrationUncertaintyFeatureValueKind.Ratio => 4,
            CalibrationUncertaintyFeatureValueKind.Rate => 6,
            _ => throw new InvalidOperationException(
                $"Feature value kind '{kind}' has no scalar bucket order."),
        };

    private static CalibrationUncertaintyBucket CountBucket(decimal value) => value switch
    {
        <= 0m => new("zero", 0),
        <= 1m => new("one", 1),
        <= 3m => new("two-to-three", 2),
        <= 7m => new("four-to-seven", 3),
        _ => new("eight-plus", 4),
    };

    private static CalibrationUncertaintyBucket RatioBucket(decimal value) => value switch
    {
        <= 0m => new("zero", 0),
        <= 0.25m => new("up-to-0.25", 1),
        <= 0.50m => new("0.25-to-0.50", 2),
        <= 0.75m => new("0.50-to-0.75", 3),
        _ => new("above-0.75", 4),
    };

    private static CalibrationUncertaintyBucket RateBucket(decimal value) => value switch
    {
        <= 0m => new("zero", 0),
        <= 0.25m => new("up-to-0.25", 1),
        <= 0.50m => new("0.25-to-0.50", 2),
        <= 1m => new("0.50-to-1", 3),
        <= 2m => new("1-to-2", 4),
        <= 4m => new("2-to-4", 5),
        _ => new("above-4", 6),
    };
}
