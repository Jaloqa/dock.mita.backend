namespace Mita.Api.Common.Categories;

public sealed record CategoryDto(Guid Id, string Name, string Kind, string Color, string? System, bool InUse);
