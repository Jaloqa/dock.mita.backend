using Serilog.Core;
using Serilog.Events;

namespace Mita.Api.Common.Logging;

public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public const string Mask = "***";

    private static readonly string[] SensitiveFragments = ["password", "token", "cookie", "authorization", "secret"];

    private static readonly ScalarValue MaskValue = new(Mask);

    public static bool IsSensitive(string name) =>
        SensitiveFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            var masked = IsSensitive(name) ? MaskValue : MaskNested(value);
            if (!ReferenceEquals(masked, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
            }
        }
    }

    private static LogEventPropertyValue MaskNested(LogEventPropertyValue value)
    {
        switch (value)
        {
            case StructureValue structure:
            {
                var properties = structure.Properties
                    .Select(property => IsSensitive(property.Name)
                        ? new LogEventProperty(property.Name, MaskValue)
                        : WithValue(property, MaskNested(property.Value)))
                    .ToList();
                return properties.Zip(structure.Properties).All(pair => ReferenceEquals(pair.First, pair.Second))
                    ? value
                    : new StructureValue(properties, structure.TypeTag);
            }

            case DictionaryValue dictionary:
            {
                var changed = false;
                var entries = dictionary.Elements.Select(entry =>
                {
                    var masked = entry.Key.Value is string key && IsSensitive(key) ? MaskValue : MaskNested(entry.Value);
                    changed |= !ReferenceEquals(masked, entry.Value);
                    return new KeyValuePair<ScalarValue, LogEventPropertyValue>(entry.Key, masked);
                }).ToList();
                return changed ? new DictionaryValue(entries) : value;
            }

            case SequenceValue sequence:
            {
                var elements = sequence.Elements.Select(MaskNested).ToList();
                return elements.Zip(sequence.Elements).All(pair => ReferenceEquals(pair.First, pair.Second))
                    ? value
                    : new SequenceValue(elements);
            }

            default:
                return value;
        }
    }

    private static LogEventProperty WithValue(LogEventProperty property, LogEventPropertyValue value) =>
        ReferenceEquals(value, property.Value) ? property : new LogEventProperty(property.Name, value);
}
