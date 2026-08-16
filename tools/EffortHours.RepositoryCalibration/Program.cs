namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        using CancellationTokenSource cancellation = new();
        void CancelHandler(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += CancelHandler;
        try
        {
            return await RunAsync(arguments, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Repository calibration cancelled.").ConfigureAwait(false);
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Length > 0 && arguments[0] == "review-development")
        {
            return await RunDevelopmentReviewAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "validation-open")
        {
            return await RunValidationOpenAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "review-validation")
        {
            return await RunValidationReviewAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "validation-select")
        {
            return await RunValidationSelectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-preflight")
        {
            return await RunCandidatePreflightAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-fit")
        {
            return await RunLogicalCandidateFitAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-numerical-preflight")
        {
            return await RunLogicalCandidatePreflightAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-project")
        {
            return await RunLogicalCandidateProjectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "manual-qa-candidate-project")
        {
            return await RunManualQaCandidateProjectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "manual-qa-review-freeze")
        {
            return await RunManualQaReviewFreezeAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "manual-qa-decision-template-freeze")
        {
            return await RunManualQaDecisionTemplateAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "manual-qa-decision-compile")
        {
            return await RunManualQaDecisionCompileAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-operational-preflight")
        {
            return await RunLogicalCandidateOperationalAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-benchmark-project")
        {
            return await RunCandidateBenchmarkProjectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-measure")
        {
            return await RunCandidateMeasurementAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-measurement-aggregate")
        {
            return await RunCandidateMeasurementAggregateAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length == 1 && arguments[0] is "--help" or "-h")
        {
            WriteUsage(Console.Out);
            return 0;
        }

        if (!ReproductionOptions.TryParse(arguments, out ReproductionOptions? options, out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await RepositoryCalibrationReproducer.RunAsync(
                    options!,
                    Console.Error,
                    cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            HttpRequestException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static void WriteUsage(TextWriter writer) => writer.WriteLine("""
        Usage:
          dotnet EffortHours.RepositoryCalibration.dll
            --plan <sampling-plan.json>
            --workspace <ignored-directory>
            --cli <efforthours.dll>
            --packets <published-packet-directory>
            --output <reproduction-manifest.json>
            --custody <holdout-custody.json>
            [--gh <gh-executable>]

        Verifies all frozen public snapshots and licenses. Only development families
        are scanned and projected into blind authoring packets. Validation and test
        families are never passed to EffortHours by this command.

        Development review:
          dotnet EffortHours.RepositoryCalibration.dll review-development
            --plan <sampling-plan.json>
            --manifest <reproduction-manifest.json>
            --packets <blind-packet-directory>
            --outputs <ignored-development-output-directory>
            --output <review-plan.json>

        The review command accepts no estimate input. It verifies the selected
        evidence and blind-packet digests before writing the complete 15-family
        development-only host-AI teacher cohort.

        Validation opening:
          dotnet EffortHours.RepositoryCalibration.dll validation-open
            --repository-root <efforthours-checkout>
            --plan <sampling-plan.json>
            --reproduction-manifest <reproduction-manifest.json>
            --custody <holdout-custody.json>
            --candidate-manifest <candidate-manifest.json>
            --source-commit <40-hex-commit>
            --workspace <ignored-directory>
            --cli <efforthours.dll>
            --packets <published-validation-packet-directory>
            --output <validation-opening.json>
            [--gh <gh-executable>]

        Verifies the exact finite candidate, selection rule, complete artifact
        chain, source custody, and nine-family validation matrix before any network
        or output access. It then verifies and scans validation only, emits strict-
        blind packets, and structurally excludes frozen-challenger output and every
        test family.

        Validation review:
          dotnet EffortHours.RepositoryCalibration.dll review-validation
            --plan <sampling-plan.json>
            --opening <validation-opening.json>
            --packets <blind-validation-packet-directory>
            --outputs <ignored-validation-output-directory>
            --output <validation-review-plan.json>

        Accepts no estimate or model input. It verifies the frozen opening, packet,
        evidence, source, and lineage identities before writing the complete
        nine-family strict-blind teacher plan. Test remains inaccessible.

        One-shot validation selection:
          dotnet EffortHours.RepositoryCalibration.dll validation-select
            --repository-root <efforthours-checkout>
            --plan <sampling-plan.json>
            --opening <validation-opening.json>
            --corpus <validation-corpus.json>
            --candidate-manifest <candidate-manifest.json>
            --model <logical-capability-model.json>
            --seed-outputs <ignored-validation-seed-output-directory>
            --candidate-outputs <fresh-ignored-candidate-output-directory>
            --seed-evaluation <fresh-seed-evaluation.json>
            --candidate-evaluation <fresh-candidate-evaluation.json>
            --decision <fresh-validation-selection.json>
            --source-commit <40-hex-commit>

        Verifies the committed evaluator and exact blind validation boundary,
        projects the sole frozen challenger once, evaluates seed and challenger,
        serializes every admission gate, and selects exactly one challenger or
        rejects all. The command accepts no test input and leaves test sealed.

        Candidate preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --source-commit <40-hex-commit>
            --output <candidate-preflight.json>

        Reproduces the seed development evaluation, applies the bounded transparent
        challenger in memory, serializes every computed or deliberately unrun gate,
        and fails closed without reading validation or test source, outputs, or labels.

        Logical-capability fitting:
          dotnet EffortHours.RepositoryCalibration.dll candidate-fit
            --corpus <development-corpus.json>
            --outputs <ignored-development-output-directory>
            --source-commit <40-hex-commit>
            --output <logical-capability-model.json>

        Logical-capability numerical preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-numerical-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --model <logical-capability-model.json>
            --source-commit <40-hex-commit>
            --output <candidate-preflight.json>

        These commands fit and evaluate only the complete development partition.
        They cannot scan, label, or evaluate validation or test repositories.

        Logical-capability saved-evidence projection:
          dotnet EffortHours.RepositoryCalibration.dll candidate-project
            --estimate <seed-estimate.json>
            --evidence <repository-evidence.json>
            --model <logical-capability-model.json>
            --expected-model-digest <sha256:digest>
            --primary-stratum <stratum>
            [--output <candidate-estimate.json>]

        Projects one bounded, digest-matched saved estimate/evidence pair. Supported
        repository-model-admission/1.0.0 strata use the candidate; every other
        stratum retains the complete named seed fallback with a stderr diagnostic.

        Manual-QA ratio candidate projection:
          dotnet EffortHours.RepositoryCalibration.dll manual-qa-candidate-project
            --estimate <seed-estimate.json>
            --policy <manual-qa-policy.json>
            --expected-policy-digest <sha256:digest>
            [--output <candidate-estimate.json>]

        Replaces seed manual-validation items with dependency-linked 30/40/50
        percent items derived only from eligible expected coding effort. This is a
        development-only candidate, not an admitted product estimator.

        Candidate-blind manual-QA review packet freeze:
          dotnet EffortHours.RepositoryCalibration.dll manual-qa-review-freeze
            --corpus <development-corpus.json>
            --policy <manual-qa-review-policy.json>
            --expected-policy-digest <sha256:digest>
            --packets <packet-directory>
            --manifest <packet-manifest.json>

        Projects only eligible coding responsibilities from the exact development
        corpus. It emits no source hours, old QA judgments, candidate values,
        formulas, totals, or review answers and cannot read validation or test data.

        Candidate-blind manual-QA decision template freeze:
          dotnet EffortHours.RepositoryCalibration.dll manual-qa-decision-template-freeze
            --corpus <development-corpus.json>
            --review-policy <manual-qa-review-policy.json>
            --expected-review-policy-digest <sha256:digest>
            --review-manifest <manual-qa-review-manifest.json>
            --packets <review-packet-directory>
            --compiler-policy <manual-qa-decision-policy.json>
            --expected-compiler-policy-digest <sha256:digest>
            --output <blank-decision-plan.json>

        Completed manual-QA decision compilation:
          dotnet EffortHours.RepositoryCalibration.dll manual-qa-decision-compile
            <the same frozen boundary options>
            --plan <completed-decision-plan.json>
            --expected-plan-digest <sha256:digest>
            --output <compiled-development-corpus.json>

        The template contains no answers. The compiler requires exact 955-target
        completeness, immutable packet lineage, evidence-bounded decisions, and a
        completed-plan digest; it never reads validation, test, or candidate values.

        Logical-capability operational preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-operational-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --model <logical-capability-model.json>
            --numerical-preflight <frozen-numerical-preflight.json>
            --source-commit <40-hex-commit>
            --output <operational-preflight.json>

        Evaluates development-only stratum, material-category, shape/size,
        saved-explanation, and safety gates. It stops later measured gates after
        any failure and never reads validation or test source, outputs, or labels.

        Measured operational gates:
          dotnet EffortHours.RepositoryCalibration.dll candidate-measure [options]
          dotnet EffortHours.RepositoryCalibration.dll candidate-measurement-aggregate [options]

        The platform command runs five or more paired fresh-process projections for
        deterministic small/medium/large saved evidence plus scanner, optional
        mutation, and optional installed-package measurements. The aggregate command
        requires exact Windows, Linux, and macOS records and writes the fail-closed
        measured checkpoint. Neither command accepts holdout inputs.
        """);
}
