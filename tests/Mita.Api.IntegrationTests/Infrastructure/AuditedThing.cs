using Microsoft.EntityFrameworkCore;
using Mita.Domain.Common;
using Mita.Infrastructure.Persistence.Interceptors;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class AuditedThing : IAuditable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AuditedThingDbContext(DbContextOptions<AuditedThingDbContext> options) : DbContext(options)
{
    public DbSet<AuditedThing> Things => Set<AuditedThing>();

    public static async Task<AuditedThingDbContext> CreateAsync(
        string connectionString,
        TimeProvider clock,
        CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AuditedThingDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new AuditingInterceptor(clock))
            .Options;
        var context = new AuditedThingDbContext(options);
        await context.Database.EnsureCreatedAsync(ct);
        return context;
    }
}

public sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
