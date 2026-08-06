namespace FairbillSynthetic;

public sealed class OrdersContext : DbContext
{
    public DbSet<Order> Orders { get; } = default!;

    public Task PersistAsync() => SaveChangesAsync();
}

public sealed record Order(Guid Id, string Status);
