using System.Text.Json;
using JG.CacheKit.Abstractions;

namespace JG.CacheKit.Serialization;

/// <summary>
/// Provides System.Text.Json based serialization for cache values.
/// </summary>
/// <remarks>This type is thread-safe after construction.</remarks>
public sealed class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonCacheSerializer"/> class.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    public JsonCacheSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Deserialize<T>(payload.Span, _options);
    }
}
