using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Tags;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Id).ValueGeneratedNever();

        builder.Property(tag => tag.Name).HasMaxLength(Tag.NameMaxLength).IsRequired();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(tag => tag.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
