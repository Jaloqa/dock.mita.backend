using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Categories;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(category => category.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(category => category.Color).HasConversion<string>().HasMaxLength(10);
        builder.Property(category => category.System).HasConversion<string>().HasMaxLength(10);
        builder.Property(category => category.StandardKey).HasMaxLength(Category.StandardKeyMaxLength);

        builder.HasIndex(category => new { category.UserId, category.Kind });
        builder.HasIndex(category => new { category.UserId, category.Kind }, "IX_categories_user_kind_other")
            .IsUnique()
            .HasFilter("\"System\" = 'Other'");
        builder.HasIndex(
                category => new { category.UserId, category.Kind, category.StandardKey },
                "IX_categories_user_kind_standard_key")
            .IsUnique()
            .HasFilter("\"StandardKey\" IS NOT NULL");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(category => category.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
