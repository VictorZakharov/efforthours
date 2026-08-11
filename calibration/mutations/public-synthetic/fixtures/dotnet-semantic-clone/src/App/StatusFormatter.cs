namespace EffortHoursSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => healthy ? "ok" : "down";
}

internal sealed class HealthPresenter
{
    public string Render(bool available) => available switch
    {
        true => "ok",
        false => "down",
    };
}
