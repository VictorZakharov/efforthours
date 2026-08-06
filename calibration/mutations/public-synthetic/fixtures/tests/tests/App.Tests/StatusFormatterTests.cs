using FairbillSynthetic;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class FactAttribute : Attribute;

public sealed class StatusFormatterTests
{
    [Fact]
    public void HealthyStatusIsFormatted()
    {
        Assert.Equal("ok", new StatusFormatter().Format(healthy: true));
    }

    [Fact]
    public void UnhealthyStatusIsFormatted()
    {
        Assert.Equal("down", new StatusFormatter().Format(healthy: false));
    }
}
