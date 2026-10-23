namespace Mita.Api.Common.Groups;

public sealed record GroupSummaryDto(Guid Id, string Name, string Currency, int Members, long Total, string Role);
