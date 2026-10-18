using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Mita.Api.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int Skip => (int)Math.Clamp(Offset(Page, PageSize), 0, int.MaxValue);

    public static long Offset(int page, int pageSize) => (long)(page - 1) * pageSize;
}

public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithErrorCode("validation.invalidValue");
        RuleFor(r => r.Page)
            .Must(_ => false)
            .When(r => PageRequest.Offset(r.Page, r.PageSize) > int.MaxValue)
            .WithErrorCode("validation.invalidValue");
        RuleFor(r => r.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("validation.invalidValue");
    }
}

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class PagingExtensions
{
    public static async Task<PageResponse<T>> ToPageAsync<T>(
        this IQueryable<T> query,
        PageRequest request,
        CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PageResponse<T>(items, request.Page, request.PageSize, total);
    }
}
