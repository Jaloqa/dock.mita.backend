using System.Linq.Expressions;
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
    public PageRequestValidator() => this.RuleForPaging(r => r.Page, r => r.PageSize);
}

public static class PagingRules
{
    private const string InvalidValue = "validation.invalidValue";

    public static void RuleForPaging<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, int>> page,
        Expression<Func<T, int>> pageSize)
    {
        var pageOf = page.Compile();
        var pageSizeOf = pageSize.Compile();

        validator.RuleFor(page).GreaterThanOrEqualTo(1).WithErrorCode(InvalidValue);
        validator.RuleFor(page)
            .Must(_ => false)
            .When(request => PageRequest.Offset(pageOf(request), pageSizeOf(request)) > int.MaxValue)
            .WithErrorCode(InvalidValue);
        validator.RuleFor(pageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode(InvalidValue);
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
