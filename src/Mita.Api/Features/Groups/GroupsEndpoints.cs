using Mita.Api.Extensions;
using Mita.Api.Features.Groups.AddCategory;
using Mita.Api.Features.Groups.AddMember;
using Mita.Api.Features.Groups.CreateExpense;
using Mita.Api.Features.Groups.CreateGroup;
using Mita.Api.Features.Groups.DeleteExpense;
using Mita.Api.Features.Groups.DeleteGroup;
using Mita.Api.Features.Groups.ExpenseHistory;
using Mita.Api.Features.Groups.GetGroup;
using Mita.Api.Features.Groups.ListExpenses;
using Mita.Api.Features.Groups.ListGroups;
using Mita.Api.Features.Groups.RemoveMember;
using Mita.Api.Features.Groups.RenameMember;
using Mita.Api.Features.Groups.RestoreExpense;
using Mita.Api.Features.Groups.UpdateExpense;
using Mita.Api.Features.Groups.UpdateGroup;

namespace Mita.Api.Features.Groups;

public static class GroupsEndpoints
{
    public static IEndpointRouteBuilder MapGroupsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Groups")
            .MapListGroups()
            .MapCreateGroup()
            .MapGetGroup()
            .MapUpdateGroup()
            .MapDeleteGroup()
            .MapAddMember()
            .MapRenameMember()
            .MapRemoveMember()
            .MapAddCategory()
            .MapListExpenses()
            .MapCreateExpense()
            .MapUpdateExpense()
            .MapDeleteExpense()
            .MapRestoreExpense()
            .MapExpenseHistory();

        return app;
    }
}
