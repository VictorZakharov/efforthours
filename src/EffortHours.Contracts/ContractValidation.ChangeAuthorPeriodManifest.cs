using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeAuthorPeriodManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "author-period manifest", errors);
        ValidateAuthorPeriodManifestSelection(manifest.Selection, errors);
        ValidateAuthorPeriodManifestContributors(manifest.Contributors, errors);
        ValidateAuthorPeriodManifestRepositories(manifest.Repositories, errors);
        return errors;
    }

    private static void ValidateAuthorPeriodManifestSelection(
        ChangeAuthorPeriodManifestSelection selection,
        List<string> errors)
    {
        if (selection is null)
        {
            errors.Add("An author-period manifest requires selection metadata.");
            return;
        }

        if (selection.SinceInclusive >= selection.UntilExclusive)
        {
            errors.Add("Author-period sinceInclusive must be earlier than untilExclusive.");
        }

        RequireCanonicalText(selection.TimeZone, "selection.timeZone", 256, errors);
        if (!Enum.IsDefined(selection.DateField) || !Enum.IsDefined(selection.MergePolicy) ||
            !Enum.IsDefined(selection.CoauthorPolicy))
        {
            errors.Add("Author-period date, merge, and co-author policies must be recognized values.");
        }

        if (selection.IntervalSemantics != "since-inclusive-until-exclusive")
        {
            errors.Add("Author-period interval semantics must be since-inclusive-until-exclusive.");
        }
    }

    private static void ValidateAuthorPeriodManifestContributors(
        IReadOnlyList<ChangeAuthorPeriodManifestContributor> contributors,
        List<string> errors)
    {
        if (contributors.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumContributors)
        {
            errors.Add(
                $"An author-period manifest must contain between 1 and " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumContributors} contributors.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        int aliasCount = 0;
        foreach (ChangeAuthorPeriodManifestContributor contributor in contributors)
        {
            ValidatePublicId(contributor.Id, "contributor.id", errors);
            if (!ids.Add(contributor.Id))
            {
                errors.Add($"Author-period contributor ID '{contributor.Id}' is duplicated.");
            }

            if (contributor.Aliases.Count is < 1 or >
                ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
            {
                errors.Add(
                    $"Contributor '{contributor.Id}' must contain between 1 and " +
                    $"{ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor} aliases.");
            }

            HashSet<string> contributorAliases = new(StringComparer.OrdinalIgnoreCase);
            foreach (string alias in contributor.Aliases)
            {
                RequireCanonicalText(
                    alias,
                    $"contributor[{contributor.Id}].alias",
                    ChangeAuthorPeriodManifestLimits.MaximumAliasLength,
                    errors);
                if (!contributorAliases.Add(alias))
                {
                    errors.Add($"Contributor '{contributor.Id}' contains a duplicate alias ignoring case.");
                }

                if (!aliases.Add(alias))
                {
                    errors.Add("An author-period identity alias is assigned to more than one contributor.");
                }

                aliasCount++;
            }
        }

        if (aliasCount > ChangeAuthorPeriodManifestLimits.MaximumAliases)
        {
            errors.Add(
                $"An author-period manifest cannot contain more than " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumAliases} aliases in total.");
        }
    }

    private static void ValidateAuthorPeriodManifestRepositories(
        IReadOnlyList<ChangeAuthorPeriodManifestRepository> repositories,
        List<string> errors)
    {
        if (repositories.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumRepositories)
        {
            errors.Add(
                $"An author-period manifest must contain between 1 and " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumRepositories} repositories.");
        }

        HashSet<string> repositoryIds = new(StringComparer.Ordinal);
        int headCount = 0;
        foreach (ChangeAuthorPeriodManifestRepository repository in repositories)
        {
            ValidatePublicId(repository.Id, "repository.id", errors);
            if (!repositoryIds.Add(repository.Id))
            {
                errors.Add($"Author-period repository ID '{repository.Id}' is duplicated.");
            }

            RequireCanonicalText(repository.RepositoryPath, $"repository[{repository.Id}].repositoryPath", 4096, errors);
            if (repository.Heads.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
            {
                errors.Add(
                    $"Repository '{repository.Id}' must contain between 1 and " +
                    $"{ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository} heads.");
            }

            HashSet<string> headIds = new(StringComparer.Ordinal);
            HashSet<string> objectIds = new(StringComparer.Ordinal);
            foreach (ChangeAuthorPeriodManifestHead head in repository.Heads)
            {
                ValidatePublicId(head.Id, $"repository[{repository.Id}].head.id", errors);
                if (!headIds.Add(head.Id))
                {
                    errors.Add($"Repository '{repository.Id}' contains duplicate head ID '{head.Id}'.");
                }

                RequireText(head.ObjectId, $"repository[{repository.Id}].head[{head.Id}].objectId", errors);
                if (!IsObjectId(head.ObjectId))
                {
                    errors.Add(
                        $"Repository '{repository.Id}' head '{head.Id}' must use a lowercase " +
                        "40- or 64-character Git object ID.");
                }
                else if (!objectIds.Add(head.ObjectId))
                {
                    errors.Add(
                        $"Repository '{repository.Id}' repeats immutable head object '{head.ObjectId}'.");
                }

                headCount++;
            }
        }

        if (headCount > ChangeAuthorPeriodManifestLimits.MaximumHeads)
        {
            errors.Add(
                $"An author-period manifest cannot contain more than " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumHeads} heads in total.");
        }
    }

    private static void ValidatePublicId(string value, string path, List<string> errors)
    {
        RequireCanonicalText(value, path, ChangeAuthorPeriodManifestLimits.MaximumIdLength, errors);
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (value[0] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9') ||
            value.Any(character => character is not (
                >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')))
        {
            errors.Add($"{path} must be a public-safe ID using only letters, digits, '.', '_', or '-'.");
        }
    }

    private static void RequireCanonicalText(
        string? value,
        string path,
        int maximumLength,
        List<string> errors)
    {
        RequireText(value, path, errors);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > maximumLength)
        {
            errors.Add($"{path} cannot exceed {maximumLength} characters.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            errors.Add($"{path} cannot contain leading or trailing whitespace.");
        }

        if (value.Contains('\0'))
        {
            errors.Add($"{path} cannot contain a null character.");
        }
    }
}
