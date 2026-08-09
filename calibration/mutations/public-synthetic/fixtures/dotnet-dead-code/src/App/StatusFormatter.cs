namespace EffortHoursSynthetic;

public sealed class StatusFormatter
{
    public string Format(bool healthy) => healthy ? "ok" : "down";
}

#if false
[Authorize]
public sealed class DisabledOrdersContext : DbContext
{
    public DbSet<DisabledOrder> Orders { get; } = default!;

    public Task PersistAsync() => SaveChangesAsync();
}

public sealed record DisabledOrder(Guid Id);
#endif
