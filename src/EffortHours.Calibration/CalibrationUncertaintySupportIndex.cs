using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed class CalibrationUncertaintySupportIndex
{
    private readonly Dictionary<CalibrationUncertaintyExactSupportKey, RepositoryCounts> _exact = [];
    private readonly Dictionary<CalibrationUncertaintyCategorySizeEcosystemKey, RepositoryCounts>
        _categorySizeEcosystem = [];
    private readonly Dictionary<CalibrationUncertaintyCategorySizeKey, RepositoryCounts>
        _categorySize = [];
    private readonly Dictionary<EffortCategory, RepositoryCounts> _category = [];
    private readonly RepositoryCounts _global = new();

    public CalibrationUncertaintySupportIndex(
        IReadOnlyList<CalibrationUncertaintySupportObservation> observations)
    {
        foreach (CalibrationUncertaintySupportObservation observation in observations)
        {
            Add(_exact, ExactKey(observation), observation.RepositoryId);
            Add(
                _categorySizeEcosystem,
                CategorySizeEcosystemKey(observation),
                observation.RepositoryId);
            Add(_categorySize, CategorySizeKey(observation), observation.RepositoryId);
            Add(_category, observation.Category, observation.RepositoryId);
            _global.Add(observation.RepositoryId);
        }
    }

    public IReadOnlyList<CalibrationUncertaintySupportCell> Cells(
        CalibrationUncertaintySupportObservation observation)
    {
        CalibrationUncertaintySupportCount[] counts =
        [
            Get(_exact, ExactKey(observation), observation.RepositoryId),
            Get(
                _categorySizeEcosystem,
                CategorySizeEcosystemKey(observation),
                observation.RepositoryId),
            Get(_categorySize, CategorySizeKey(observation), observation.RepositoryId),
            Get(_category, observation.Category, observation.RepositoryId),
            _global.Excluding(observation.RepositoryId),
        ];
        int selected = Array.FindIndex(counts, IsSufficient);
        if (selected < 0)
        {
            selected = counts.Length - 1;
        }

        return
        [
            .. CalibrationUncertaintySupportProfiler.SupportHierarchy.Select((level, index) =>
                new CalibrationUncertaintySupportCell
                {
                    Level = level,
                    TrainingObservationCount = counts[index].ObservationCount,
                    TrainingRepositoryCount = counts[index].RepositoryCount,
                    Sufficient = IsSufficient(counts[index]),
                    Selected = index == selected,
                }),
        ];
    }

    private static bool IsSufficient(CalibrationUncertaintySupportCount count) =>
        count.ObservationCount >= CalibrationUncertaintySupportProfiler.MinimumObservationCount &&
        count.RepositoryCount >= CalibrationUncertaintySupportProfiler.MinimumRepositoryCount;

    private static CalibrationUncertaintyExactSupportKey ExactKey(
        CalibrationUncertaintySupportObservation observation) => new(
            observation.Category,
            observation.ExpectedSizeBand,
            observation.EcosystemKey,
            observation.SourceComplexity);

    private static CalibrationUncertaintyCategorySizeEcosystemKey CategorySizeEcosystemKey(
        CalibrationUncertaintySupportObservation observation) => new(
            observation.Category,
            observation.ExpectedSizeBand,
            observation.EcosystemKey);

    private static CalibrationUncertaintyCategorySizeKey CategorySizeKey(
        CalibrationUncertaintySupportObservation observation) => new(
            observation.Category,
            observation.ExpectedSizeBand);

    private static void Add<TKey>(
        Dictionary<TKey, RepositoryCounts> index,
        TKey key,
        string repositoryId) where TKey : notnull
    {
        if (!index.TryGetValue(key, out RepositoryCounts? counts))
        {
            counts = new RepositoryCounts();
            index.Add(key, counts);
        }

        counts.Add(repositoryId);
    }

    private static CalibrationUncertaintySupportCount Get<TKey>(
        Dictionary<TKey, RepositoryCounts> index,
        TKey key,
        string repositoryId) where TKey : notnull =>
        index.TryGetValue(key, out RepositoryCounts? counts)
            ? counts.Excluding(repositoryId)
            : new CalibrationUncertaintySupportCount(0, 0);

    private sealed class RepositoryCounts
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        private int _total;

        public void Add(string repositoryId)
        {
            _counts.TryGetValue(repositoryId, out int count);
            _counts[repositoryId] = count + 1;
            _total++;
        }

        public CalibrationUncertaintySupportCount Excluding(string repositoryId)
        {
            _counts.TryGetValue(repositoryId, out int excluded);
            return new CalibrationUncertaintySupportCount(
                _total - excluded,
                _counts.Count - (excluded > 0 ? 1 : 0));
        }
    }
}
