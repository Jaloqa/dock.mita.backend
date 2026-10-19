using Mita.Domain.Categories;

namespace Mita.Api.Common.Reporting;

public sealed record CategorySpending(Guid CategoryId, string Name, CategoryColor Color, SystemCategory? System, long Amount);
