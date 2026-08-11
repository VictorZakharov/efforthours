namespace EffortHoursSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => healthy ? "ok" : "down";
}

#if true
public sealed class IncludedHealthPolicy
{
    public bool CanServe(bool healthy, bool warmedUp) => healthy && warmedUp;
}
#endif
