using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();

        builder.Property(group => group.Name).HasMaxLength(Group.NameMaxLength).IsRequired();
        builder.Property(group => group.Currency).HasConversion<string>().HasMaxLength(3);

        builder.HasIndex(group => new { group.OwnerId, group.CreatedAt });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(group => group.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
