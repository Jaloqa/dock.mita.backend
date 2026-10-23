using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.AddCategory;

public sealed class AddCategoryHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<AddCategoryCommand, Result<GroupCategoryDto>>
{
    public async Task<Result<GroupCategoryDto>> Handle(AddCategoryCommand command, CancellationToken cancellationToken)
    {
        var name = GroupNames.Parse(command.Name);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenAsync(command.Scope, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var existing = await db.GroupCategories.AsNoTracking()
            .Where(category => category.GroupId == context.GroupId)
            .Select(category => category.Name)
            .ToListAsync(cancellationToken);

        if (existing.Any(other => string.Equals(other, name, StringComparison.OrdinalIgnoreCase))
            || StandardGroupCategories.Names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return Rejected("validation.categoryNameTaken");
        }

        if (existing.Count >= Group.MaxCategories)
        {
            return Rejected("validation.tooManyCategories");
        }

        var added = GroupCategory.Create(context.GroupId, name);
        db.GroupCategories.Add(added);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GroupCategoryDto(added.Id.ToString(), false, added.Name);
    }

    private static Error Rejected(string code) =>
        Error.Validation(new Dictionary<string, string[]> { ["name"] = [code] });
}
