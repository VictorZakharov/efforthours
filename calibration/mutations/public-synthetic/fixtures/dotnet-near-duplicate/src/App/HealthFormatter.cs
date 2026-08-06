namespace FairbillSynthetic;

internal sealed class HealthFormatter
{
    public string Render(bool available) => available ? "ok" : "down";
}
