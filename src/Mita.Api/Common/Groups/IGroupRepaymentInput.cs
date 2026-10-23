using System.Text.Json;

namespace Mita.Api.Common.Groups;

public interface IGroupRepaymentInput
{
    JsonElement? FromMemberId { get; }

    JsonElement? ToMemberId { get; }

    JsonElement? Amount { get; }

    JsonElement? Currency { get; }

    JsonElement? Date { get; }

    JsonElement? Rate { get; }

    JsonElement? Note { get; }
}
