namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private const string CalibrationHelpText = """
        Usage:
          eh calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
          eh calibration change-scaffold <change-estimate.json> --repository-family <id> --case <id> --tag <tag>... [--blind] [--compact] [--output <path>]
          eh calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
          eh calibration change-compile <review-plan.json> <change-estimate.json>... [--compact] [--output <path>]
          eh calibration review-scaffold <corpus.json> [--blind] [--compact] [--output <path>]
          eh calibration review-compile <plan.json> <corpus.json> [--compact] [--output <path>]
          eh calibration mutations <suite.json> <estimate.json>... [--compact] [--output <path>]
          eh calibration validate <corpus.json> [--compact] [--output <path>]
          eh calibration evaluate <corpus.json> <estimate.json>... --partition <name> [--compact] [--output <path>]
          eh calibration diagnose <corpus.json> <estimate.json>... --partition <name> [--compact] [--output <path>]
          eh calibration uncertainty-features <estimate.json> <evidence.json> [--compact] [--output <path>]
          eh calibration uncertainty-structure <estimate.json> <evidence.json> [--compact] [--output <path>]
          eh calibration uncertainty-evaluate <corpus.json> <features.json>... [--compact] [--output <path>]
          eh calibration uncertainty-structure-evaluate <corpus.json> <structural-features.json>... [--compact] [--output <path>]
          eh calibration uncertainty-support <population.json> <features.json>... [--compact] [--output <path>]
          eh calibration uncertainty-support-evaluate <corpus.json> <support-profile.json> <features.json>... [--compact] [--output <path>]
          eh calibration change-evaluate <corpus.json> <change-estimate.json>... --partition <name> [--compact] [--output <path>]

        Calibration is offline and effort-only. Reviewed labels are weak supervision,
        not historical labor or literal ground truth.
        """;
}
