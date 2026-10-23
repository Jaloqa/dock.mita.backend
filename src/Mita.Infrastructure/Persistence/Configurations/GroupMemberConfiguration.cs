using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();

        builder.Property(member => member.Name).HasMaxLength(GroupMember.NameMaxLength).IsRequired();
        builder.Property(member => member.InvitedEmail).HasMaxLength(GroupMember.EmailMaxLength);

        builder.HasIndex(member => new { member.GroupId, member.Position }).IsUnique();
        builder.HasIndex(member => new { member.GroupId, member.UserId }, "IX_group_members_group_user")
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");
        builder.HasIndex(member => member.InvitedEmail, "IX_group_members_invited_email")
            .HasFilter("\"InvitedEmail\" IS NOT NULL");

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
