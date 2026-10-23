using Mita.Api.Extensions;
using Mita.Api.Features.Groups.AddCategory;
using Mita.Api.Features.Groups.AddMember;
using Mita.Api.Features.Groups.CancelInvite;
using Mita.Api.Features.Groups.CreateExpense;
using Mita.Api.Features.Groups.CreateGroup;
using Mita.Api.Features.Groups.CreateGroupRepayment;
using Mita.Api.Features.Groups.DeleteExpense;
using Mita.Api.Features.Groups.DeleteGroup;
using Mita.Api.Features.Groups.DeleteGroupRepayment;
using Mita.Api.Features.Groups.ExpenseHistory;
using Mita.Api.Features.Groups.GetGroup;
using Mita.Api.Features.Groups.GetGroupBalances;
using Mita.Api.Features.Groups.GroupRepaymentHistory;
using Mita.Api.Features.Groups.InviteMember;
using Mita.Api.Features.Groups.LeaveGroup;
using Mita.Api.Features.Groups.LinkMember;
using Mita.Api.Features.Groups.ListExpenses;
using Mita.Api.Features.Groups.ListGroupRepayments;
using Mita.Api.Features.Groups.ListGroups;
using Mita.Api.Features.Groups.RemoveMember;
using Mita.Api.Features.Groups.RenameMember;
using Mita.Api.Features.Groups.RestoreExpense;
using Mita.Api.Features.Groups.RestoreGroupRepayment;
using Mita.Api.Features.Groups.ShareGroup;
using Mita.Api.Features.Groups.UnlinkMember;
using Mita.Api.Features.Groups.UnshareGroup;
using Mita.Api.Features.Groups.UpdateExpense;
using Mita.Api.Features.Groups.UpdateGroup;
using Mita.Api.Features.Groups.UpdateGroupRepayment;

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
            .MapShareGroup()
            .MapUnshareGroup()
            .MapAddMember()
            .MapRenameMember()
            .MapRemoveMember()
            .MapLinkMember()
            .MapUnlinkMember()
            .MapInviteMember()
            .MapCancelInvite()
            .MapLeaveGroup()
            .MapAddCategory()
            .MapListExpenses()
            .MapCreateExpense()
            .MapUpdateExpense()
            .MapDeleteExpense()
            .MapRestoreExpense()
            .MapExpenseHistory()
            .MapListGroupRepayments()
            .MapCreateGroupRepayment()
            .MapUpdateGroupRepayment()
            .MapDeleteGroupRepayment()
            .MapRestoreGroupRepayment()
            .MapGroupRepaymentHistory()
            .MapGetGroupBalances();

        return app;
    }
}
