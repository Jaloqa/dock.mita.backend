using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.CreateGroup;

public sealed class CreateGroupHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var name = GroupNames.Parse(command.Name);
        var members = GroupMemberNames.Parse(command.Members);
        if (!CurrencyWire.TryParse(command.Currency, out var currency))
        {
            throw new InvalidOperationException("The currency reached the handler without passing validation.");
        }

        var group = Group.Create(currentUser.Id, name, currency);
        db.Groups.Add(group);
        db.GroupMembers.AddRange(members.Select((member, position) => GroupMember.Create(group.Id, member, position)));
        await db.SaveChangesAsync(cancellationToken);

        return await db.LoadAsync(group.Id, cancellationToken);
    }
}
