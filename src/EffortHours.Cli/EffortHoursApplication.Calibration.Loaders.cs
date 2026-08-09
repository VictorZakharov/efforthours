using System.Globalization;
using System.Reflection;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<CalibrationCorpus?> LoadCalibrationCorpusAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Calibration corpus path was not found: {inputPath}")
                .ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not read calibration corpus: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration corpus does not satisfy the calibration-corpus schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationCorpus corpus;
        try
        {
            corpus = ContractJson.Deserialize<CalibrationCorpus>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration corpus: {exception.Message}").ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(corpus);
        if (errors.Count == 0)
        {
            return corpus;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task<CalibrationReviewPlan?> LoadCalibrationReviewPlanAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Calibration review-plan path was not found: {inputPath}")
                .ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not read calibration review plan: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration review plan does not satisfy the calibration-review-plan schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationReviewPlan plan;
        try
        {
            plan = ContractJson.Deserialize<CalibrationReviewPlan>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration review plan: {exception.Message}").ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(plan);
        if (errors.Count == 0)
        {
            return plan;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task<CalibrationCorpusReviewPlan?> LoadCalibrationCorpusReviewPlanAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync(
                $"Calibration corpus review-plan path was not found: {inputPath}")
                .ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read calibration corpus review plan: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPlan,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration corpus review plan does not satisfy its schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationCorpusReviewPlan plan;
        try
        {
            plan = ContractJson.Deserialize<CalibrationCorpusReviewPlan>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration corpus review plan: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(plan);
        if (errors.Count == 0)
        {
            return plan;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task<CalibrationMutationSuite?> LoadCalibrationMutationSuiteAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync(
                $"Calibration mutation-suite path was not found: {inputPath}")
                .ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read calibration mutation suite: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationMutationSuite,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration mutation suite does not satisfy the calibration-mutation-suite schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationMutationSuite suite;
        try
        {
            suite = ContractJson.Deserialize<CalibrationMutationSuite>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration mutation suite: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(suite);
        if (errors.Count == 0)
        {
            return suite;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task WriteCalibrationErrorsAsync(
        TextWriter standardError,
        IReadOnlyList<string> errors)
    {
        await standardError.WriteLineAsync("Calibration input is semantically invalid:")
            .ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }
    }

    private static bool TryParseCalibrationPartition(
        string value,
        out CalibrationPartition partition)
    {
        switch (value.ToLowerInvariant())
        {
            case "development":
                partition = CalibrationPartition.Development;
                return true;
            case "validation":
                partition = CalibrationPartition.Validation;
                return true;
            case "test":
                partition = CalibrationPartition.Test;
                return true;
            default:
                partition = default;
                return false;
        }
    }

}
