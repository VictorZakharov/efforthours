namespace EffortHoursSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => Decorate(Normalize(Describe(healthy)));

    private string Normalize(string value) => IsKnown(value) ? Canonicalize(value) : "UNKNOWN";

    private string Describe(bool healthy) => healthy ? "ok" : "down";

    private bool IsKnown(string value) => value is "ok" or "down";

    private string Canonicalize(string value) => value.ToUpperInvariant();

    private string Decorate(string value) => $"[{value}]";
}
