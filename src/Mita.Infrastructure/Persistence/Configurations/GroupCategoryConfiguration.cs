using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupCategoryConfiguration : IEntityTypeConfiguration<GroupCategory>
{
    public void Configure(EntityTypeBuilder<GroupCategory> builder)
    {
        builder.ToTable("group_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.Name).HasMaxLength(GroupCategory.NameMaxLength).IsRequired();

        builder.HasIndex(category => category.GroupId);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(category => category.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
