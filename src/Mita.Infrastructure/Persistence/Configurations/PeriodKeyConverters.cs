using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mita.Domain.Periods;

namespace Mita.Infrastructure.Persistence.Configurations;

internal static class PeriodKeyConverters
{
    public static readonly ValueConverter<PeriodKey, string> Required = new(
        key => key.ToString(),
        text => Parse(text));

    public static readonly ValueConverter<PeriodKey?, string?> Optional = new(
        key => key == null ? null : key.Value.ToString(),
        text => text == null ? null : Parse(text));

    private static PeriodKey Parse(string text) =>
        PeriodKey.TryParse(text, out var key) ? key : throw new InvalidOperationException("Invalid stored period key.");
}
