using EffortHours.Analysis;

namespace EffortHours.Review;

public sealed record HostReviewSourceContext(
    string RepositoryRoot,
    IRepositoryFileSystem FileSystem);
