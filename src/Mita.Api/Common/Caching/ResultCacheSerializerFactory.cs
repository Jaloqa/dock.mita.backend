using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Mita.Api.Common.Caching;

public sealed class ResultCacheSerializerFactory : IHybridCacheSerializerFactory
{
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var serializerType = typeof(SuccessValueSerializer<>).MakeGenericType(typeof(T).GetGenericArguments());
            serializer = (IHybridCacheSerializer<T>)Activator.CreateInstance(serializerType)!;
            return true;
        }

        serializer = null;
        return false;
    }

    private sealed class SuccessValueSerializer<TValue> : IHybridCacheSerializer<Result<TValue>>
    {
        public Result<TValue> Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return Result<TValue>.Success(JsonSerializer.Deserialize<TValue>(ref reader, JsonSerializerOptions.Web)!);
        }

        public void Serialize(Result<TValue> value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value.Value, JsonSerializerOptions.Web);
        }
    }
}
