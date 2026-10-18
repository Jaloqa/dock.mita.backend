using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mita.Domain.Accounts;
using Mita.Domain.Money;
using Mita.Domain.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence.ReadModels;

namespace Mita.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<AccountMovement> AccountMovements => Set<AccountMovement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
