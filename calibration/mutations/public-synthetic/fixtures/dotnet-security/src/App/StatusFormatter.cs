namespace FairbillSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => healthy ? "ok" : "down";
}
