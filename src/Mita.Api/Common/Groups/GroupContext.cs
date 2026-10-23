using Mita.Domain.Groups;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public sealed record GroupContext(
    Guid GroupId,
    Guid OwnerId,
    Currency Currency,
    GroupActor Actor,
    GroupRole Role,
    Guid? MyMemberId);
