using System.Text.Json;
using System.Text.Json.Serialization;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public sealed record ExpenseTerms(
    long Amount,
    string Currency,
    string Date,
    long Share,
    string CategoryId,
    IReadOnlyList<string> Tags,
    string? Description);

public sealed record RepaymentTerms(
    string Direction,
    Guid MemberId,
    long Amount,
    string Currency,
    string Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? GroupAmount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<GroupCoverDto>? Covers);

public static class GroupTerms
{
    public const string Paid = "paid";
    public const string Received = "received";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ExpenseTerms? For(GroupExpense expense, Guid memberId) =>
        expense.IsDeleted || expense.PayerId != memberId
            ? null
            : new ExpenseTerms(
                expense.Amount,
                CurrencyWire.ToWire(expense.Currency),
                IsoDate.ToWire(expense.Date),
                expense.Shares.Where(share => share.MemberId == memberId).Sum(share => share.Amount),
                GroupWire.ToWireCategory(expense.StandardCategory, expense.CategoryId),
                [.. expense.Tags],
                expense.Description);

    public static RepaymentTerms? For(GroupRepayment repayment, Guid memberId)
    {
        if (repayment.IsDeleted)
        {
            return null;
        }

        if (repayment.FromMemberId == memberId)
        {
            return Terms(
                Paid,
                repayment.ToMemberId,
                repayment.GroupAmount,
                [
                    .. repayment.Covers
                        .OrderBy(cover => cover.Position)
                        .Select(cover => new GroupCoverDto(cover.ExpenseId, cover.GroupAmount))
                ]);
        }

        return repayment.ToMemberId == memberId ? Terms(Received, repayment.FromMemberId, null, null) : null;

        RepaymentTerms Terms(string direction, Guid otherMemberId, long? groupAmount, IReadOnlyList<GroupCoverDto>? covers) =>
            new(
                direction,
                otherMemberId,
                repayment.Amount,
                CurrencyWire.ToWire(repayment.Currency),
                IsoDate.ToWire(repayment.Date),
                groupAmount,
                covers);
    }

    public static string? Canonical(object? terms) =>
        terms is null ? null : JsonSerializer.Serialize(terms, terms.GetType(), Json);

    public static object? Read(GroupEntryKind kind, string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        return kind switch
        {
            GroupEntryKind.Expense => JsonSerializer.Deserialize<ExpenseTerms>(stored, Json),
            GroupEntryKind.Repayment => JsonSerializer.Deserialize<RepaymentTerms>(stored, Json),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
