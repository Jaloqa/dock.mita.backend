using System.Text.Json;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupEntryEvents
{
    private static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web);

    public static void Append(
        AppDbContext db,
        GroupContext group,
        GroupEntryKind kind,
        Guid entryId,
        GroupEntryAction action,
        object state,
        DateTimeOffset at) =>
        db.GroupEntryEvents.Add(GroupEntryEvent.Create(
            group.GroupId,
            kind,
            entryId,
            action,
            at,
            group.Actor,
            JsonSerializer.Serialize(state, state.GetType(), StateJson)));
}
