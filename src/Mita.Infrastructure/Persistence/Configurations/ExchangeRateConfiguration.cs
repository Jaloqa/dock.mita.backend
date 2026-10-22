using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Money;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");
        builder.HasKey(rate => new { rate.Currency, rate.Date });

        builder.Property(rate => rate.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(rate => rate.PlnValue).HasPrecision(24, 12);
    }
}
