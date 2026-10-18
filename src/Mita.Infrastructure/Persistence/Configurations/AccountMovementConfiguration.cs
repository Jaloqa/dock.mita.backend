using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Infrastructure.Persistence.ReadModels;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class AccountMovementConfiguration : IEntityTypeConfiguration<AccountMovement>
{
    public void Configure(EntityTypeBuilder<AccountMovement> builder)
    {
        builder.HasNoKey();
        builder.ToView("account_movements");
    }
}
