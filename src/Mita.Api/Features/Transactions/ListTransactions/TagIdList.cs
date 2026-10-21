using Mita.Domain.Tags;

namespace Mita.Api.Features.Transactions.ListTransactions;

public static class TagIdList
{
    public static bool TryParse(string? value, out IReadOnlyList<Guid> ids)
    {
        ids = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parsed = new List<Guid>();
        foreach (var part in value.Split(','))
        {
            if (!Guid.TryParse(part.Trim(), out var id))
            {
                return false;
            }

            if (!parsed.Contains(id))
            {
                parsed.Add(id);
            }
        }

        if (parsed.Count > Tag.MaxPerTransaction)
        {
            return false;
        }

        ids = parsed;
        return true;
    }
}
