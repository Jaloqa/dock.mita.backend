namespace Mita.Api.Common.Groups;

public sealed record GroupActorDto(string Kind, Guid? MemberId, string Name);
