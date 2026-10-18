using System.Text.Json;
using System.Text.Json.Serialization;
using Mita.Domain.Users;

namespace Mita.Api.Common.Json;

public sealed class MonthStartJsonConverter : JsonConverter<MonthStart>
{
    public const string LastWorkingDayWireValue = "lastWorkingDay";

    public override MonthStart Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String when reader.ValueTextEquals(LastWorkingDayWireValue):
                return MonthStart.LastWorkingDay;
            case JsonTokenType.Number when reader.TryGetInt32(out var day) && day is >= MonthStart.MinDay and <= MonthStart.MaxDay:
                return MonthStart.Day(day);
            default:
                throw new JsonException($"A month start must be an integer from {MonthStart.MinDay} to {MonthStart.MaxDay} or \"{LastWorkingDayWireValue}\".");
        }
    }

    public override void Write(Utf8JsonWriter writer, MonthStart value, JsonSerializerOptions options)
    {
        if (value.Value is { } day)
        {
            writer.WriteNumberValue(day);
        }
        else
        {
            writer.WriteStringValue(LastWorkingDayWireValue);
        }
    }
}