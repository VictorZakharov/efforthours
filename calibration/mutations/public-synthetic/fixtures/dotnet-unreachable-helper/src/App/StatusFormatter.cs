namespace EffortHoursSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => healthy ? "ok" : "down";

    private async Task<string> AbandonedAsync()
    {
        using System.Net.Http.HttpClient client = new();
        return await client.GetStringAsync("https://example.invalid/status");
    }
}
