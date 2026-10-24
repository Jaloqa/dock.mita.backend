using System.Text.Json.Serialization;
using Mita.Api.Common.Json;
using Mita.Api.Common.Money;

namespace Mita.Api.Common.Groups;

public sealed record ToConfirmLineDto(
    Guid ExpenseId,
    string? Description,
    long Amount,
    string Currency,
    ToConfirmSuggestion Suggestion);

public sealed record ToConfirmItemDto(
    string Kind,
    Guid EntryId,
    Guid GroupId,
    string GroupName,
    string GroupCurrency,
    string Change,
    Guid Version,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset At,
    GroupActorDto Actor,
    object? Current,
    object? Previous,
    long Other,
    IReadOnlyList<ToConfirmLineDto> Lines,
    Guid? SuggestedAccountId)
{
    public static ToConfirmItemDto From(ToConfirmItem item) =>
        new(
            GroupWire.ToWire(item.Kind),
            item.EntryId,
            item.GroupId,
            item.GroupName,
            CurrencyWire.ToWire(item.GroupCurrency),
            ToWire(item.Change),
            item.Version,
            item.At,
            item.Actor,
            item.Current,
            item.Previous,
            item.Other,
            [
                .. item.Lines.Select(line => new ToConfirmLineDto(
                    line.ExpenseId,
                    line.Description,
                    line.Amount,
                    CurrencyWire.ToWire(line.Currency),
                    line.Suggestion))
            ],
            item.SuggestedAccountId);

    private static string ToWire(ToConfirmChange change) => change switch
    {
        ToConfirmChange.New => "new",
        ToConfirmChange.Changed => "changed",
        ToConfirmChange.Removed => "removed",
        _ => throw new ArgumentOutOfRangeException(nameof(change), change, null)
    };
}
