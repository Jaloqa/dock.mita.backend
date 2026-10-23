using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupEntryEventConfiguration : IEntityTypeConfiguration<GroupEntryEvent>
{
    public void Configure(EntityTypeBuilder<GroupEntryEvent> builder)
    {
        builder.ToTable("group_entry_events");
        builder.HasKey(entryEvent => entryEvent.Id);
        builder.Property(entryEvent => entryEvent.Id).ValueGeneratedNever();

        builder.Property(entryEvent => entryEvent.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(entryEvent => entryEvent.Action).HasConversion<string>().HasMaxLength(10);
        builder.Property(entryEvent => entryEvent.State).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(entryEvent => new { entryEvent.GroupId, entryEvent.Kind, entryEvent.EntryId, entryEvent.At });
        builder.HasIndex(entryEvent => entryEvent.ActorUserId);
        builder.HasIndex(entryEvent => entryEvent.ActorMemberId);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(entryEvent => entryEvent.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(entryEvent => entryEvent.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(entryEvent => entryEvent.ActorMemberId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
